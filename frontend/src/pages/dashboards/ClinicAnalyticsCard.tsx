import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { BarChart3 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { LineSeriesChart } from "@/components/admin/Charts";
import { analyticsApi } from "@/api/clinicPlus";

const pct = (n: number) => `${(n * 100).toFixed(1)}%`;

/** The signed-in vet's own practice analytics: revenue, no-shows, capacity use and patient returns. */
export function ClinicAnalyticsCard() {
  const [days, setDays] = useState("90");
  const { data, isLoading } = useQuery({
    queryKey: ["vets", "me", "analytics", days],
    queryFn: () => {
      const to = new Date(); const from = new Date(); from.setDate(to.getDate() - (Number(days) - 1));
      const iso = (d: Date) => d.toISOString().slice(0, 10);
      return analyticsApi.mine({ from: iso(from), to: iso(to), granularity: Number(days) <= 31 ? "day" : Number(days) <= 100 ? "week" : "month" });
    }
  });

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="flex items-center gap-2 text-base"><BarChart3 className="h-4 w-4" /> Clinic analytics</CardTitle>
        <div className="w-36">
          <Select value={days} onValueChange={setDays}>
            <SelectTrigger aria-label="Period"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="30">Last 30 days</SelectItem>
              <SelectItem value="90">Last 90 days</SelectItem>
              <SelectItem value="365">Last 12 months</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading || !data ? <Skeleton className="h-48" /> : (
          <>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
              <Stat label="Revenue (paid, completed)" value={data.totalRevenue.toFixed(2)} />
              <Stat label="No-show rate" value={pct(data.noShowRate)} hint={`${data.noShows} no-shows`} />
              <Stat label="Capacity utilisation" value={pct(data.capacityUtilization)} hint={`${data.slotsBooked}/${data.slotsOffered} slots`} />
              <Stat label="Patient return (90d / 180d)" value={`${pct(data.return90Rate)} / ${pct(data.return180Rate)}`} hint={`${data.uniquePatients} patients, last 12 months`} />
            </div>
            {data.series.length > 0 && (
              <LineSeriesChart data={data.series} xKey="period"
                series={[{ key: "revenue", label: "Revenue" }, { key: "appointments", label: "Appointments" }, { key: "noShows", label: "No-shows" }]} />
            )}
            {data.clinicDoctors && data.clinicDoctors.length > 1 && (
              <div className="space-y-2">
                <h3 className="text-sm font-medium">Your clinic (doctors sharing the same clinic name)</h3>
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead><tr className="text-left text-xs text-muted-foreground"><th className="py-1">Doctor</th><th className="text-right">Appointments</th><th className="text-right">Revenue</th><th className="text-right">No-show rate</th></tr></thead>
                    <tbody>
                      {data.clinicDoctors.map((d) => (
                        <tr key={d.doctorId} className="border-t">
                          <td className="py-1.5">{d.doctorName}</td>
                          <td className="text-right">{d.appointments}</td>
                          <td className="text-right">{d.revenue.toFixed(2)}</td>
                          <td className="text-right">{pct(d.noShowRate)}</td>
                        </tr>
                      ))}
                      <tr className="border-t font-semibold">
                        <td className="py-1.5">Clinic total</td>
                        <td className="text-right">{data.clinicDoctors.reduce((s, d) => s + d.appointments, 0)}</td>
                        <td className="text-right">{data.clinicDoctors.reduce((s, d) => s + d.revenue, 0).toFixed(2)}</td>
                        <td />
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-md border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-lg font-semibold">{value}</p>
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}
