import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { BarChart3 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/common/PageHeader";
import { adminApi } from "@/api/adminV2";
import { AreaSeriesChart, DonutChart, HBarChart, LineSeriesChart } from "@/components/admin/Charts";

/**
 * Deep-dive analytics — same shape as the overview dashboard but with a
 * configurable lookback window and side-by-side breakdowns. Kept separate from
 * the Overview KPI cards so this page can grow without crowding the landing.
 */
export function AdminAnalyticsPage() {
  const [days, setDays] = useState(30);

  const { data: series, isLoading: seriesLoading } = useQuery({
    queryKey: ["admin-series", days],
    queryFn: () => adminApi.series(days)
  });
  const { data: top, isLoading: topLoading } = useQuery({
    queryKey: ["admin-top", days],
    queryFn: () => adminApi.top(days)
  });

  return (
    <div className="space-y-6">
      <PageHeader title="Analytics" icon={BarChart3} description="Platform trends, deep-dives and rankings." />

      <div className="flex justify-end">
        <Select value={String(days)} onValueChange={(v) => setDays(Number(v))}>
          <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="7">Last 7 days</SelectItem>
            <SelectItem value="14">Last 14 days</SelectItem>
            <SelectItem value="30">Last 30 days</SelectItem>
            <SelectItem value="60">Last 60 days</SelectItem>
            <SelectItem value="90">Last 90 days</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div className="grid lg:grid-cols-2 gap-4">
        <Card>
          <CardHeader><CardTitle className="text-base">User & order activity</CardTitle></CardHeader>
          <CardContent>
            {seriesLoading || !series ? <p className="py-10 text-center text-muted-foreground text-sm">Loading…</p>
              : <LineSeriesChart data={series} xKey="date" series={[
                  { key: "users",  label: "New users" },
                  { key: "orders", label: "Orders" }
                ]} />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Revenue</CardTitle></CardHeader>
          <CardContent>
            {seriesLoading || !series ? <p className="py-10 text-center text-muted-foreground text-sm">Loading…</p>
              : <AreaSeriesChart data={series} xKey="date" dataKey="revenue" label="Revenue ($)" />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Adoption by animal type</CardTitle></CardHeader>
          <CardContent>
            {topLoading || !top?.animals?.length ? <p className="py-10 text-center text-muted-foreground text-sm">No adoption data.</p>
              : <DonutChart data={top.animals} dataKey="listings" nameKey="type" height={280} />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Top products by revenue</CardTitle></CardHeader>
          <CardContent>
            {topLoading || !top?.products?.length ? <p className="py-10 text-center text-muted-foreground text-sm">No sales yet.</p>
              : <HBarChart data={top.products} labelKey="name" dataKey="revenue" height={280} />}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
