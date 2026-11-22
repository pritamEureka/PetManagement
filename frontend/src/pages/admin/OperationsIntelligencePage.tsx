import { useMemo, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Activity, Download } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { AreaSeriesChart, HBarChart, LineSeriesChart } from "@/components/admin/Charts";
import { intelligenceApi, type IntelligenceDataset } from "@/api/intelligence";
import { ScheduledReportsPanel } from "@/components/admin/ScheduledReportsPanel";

function Section({ title, dataset, loading, empty, children, note }: {
  title: string; dataset: IntelligenceDataset; loading: boolean; empty?: boolean; children: ReactNode; note?: string;
}) {
  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">{title}</CardTitle>
        <Button size="sm" variant="outline" onClick={() => intelligenceApi.exportCsv(dataset)} aria-label={`Export ${title} as CSV`}>
          <Download className="h-4 w-4 mr-1" /> CSV
        </Button>
      </CardHeader>
      <CardContent>
        {loading ? <p className="py-10 text-center text-muted-foreground text-sm">Loading...</p>
          : empty ? <p className="py-10 text-center text-muted-foreground text-sm">No data yet.</p>
          : children}
        {note && <p className="mt-2 text-xs text-muted-foreground">{note}</p>}
      </CardContent>
    </Card>
  );
}

export function OperationsIntelligencePage() {
  const demand = useQuery({ queryKey: ["intel-demand"], queryFn: () => intelligenceApi.demand(60) });
  const noShow = useQuery({ queryKey: ["intel-noshow"], queryFn: () => intelligenceApi.noShow(8) });
  const capacity = useQuery({ queryKey: ["intel-capacity"], queryFn: intelligenceApi.capacity });
  const vaccines = useQuery({ queryKey: ["intel-vaccines"], queryFn: intelligenceApi.vaccines });
  const lost = useQuery({ queryKey: ["intel-lost"], queryFn: () => intelligenceApi.lostPets(12) });
  const store = useQuery({ queryKey: ["intel-store"], queryFn: () => intelligenceApi.store(12) });
  const inventory = useQuery({ queryKey: ["intel-inventory"], queryFn: intelligenceApi.inventory });
  const anomalies = useQuery({ queryKey: ["intel-anomalies"], queryFn: intelligenceApi.anomalies });

  // Roll doctor-level rows up to one point per week for the trend chart.
  const noShowWeekly = useMemo(() => {
    const byWeek = new Map<string, { weekStart: string; total: number; noShows: number; cancelled: number }>();
    for (const r of noShow.data ?? []) {
      const w = byWeek.get(r.weekStart) ?? { weekStart: r.weekStart, total: 0, noShows: 0, cancelled: 0 };
      w.total += r.total; w.noShows += r.noShows; w.cancelled += r.cancelled;
      byWeek.set(r.weekStart, w);
    }
    return [...byWeek.values()].map(w => ({
      weekStart: w.weekStart,
      noShowRate: w.total ? +(w.noShows * 100 / w.total).toFixed(1) : 0,
      cancellationRate: w.total ? +(w.cancelled * 100 / w.total).toFixed(1) : 0
    }));
  }, [noShow.data]);

  const worstDoctors = useMemo(() => {
    const byDoc = new Map<string, { name: string; total: number; noShows: number; cancelled: number }>();
    for (const r of noShow.data ?? []) {
      const d = byDoc.get(r.doctorId) ?? { name: r.doctorName, total: 0, noShows: 0, cancelled: 0 };
      d.total += r.total; d.noShows += r.noShows; d.cancelled += r.cancelled;
      byDoc.set(r.doctorId, d);
    }
    return [...byDoc.values()].filter(d => d.total >= 3)
      .map(d => ({ ...d, noShowRate: +(d.noShows * 100 / d.total).toFixed(1), cancelRate: +(d.cancelled * 100 / d.total).toFixed(1) }))
      .sort((a, b) => b.noShowRate - a.noShowRate).slice(0, 5);
  }, [noShow.data]);

  return (
    <div className="space-y-6">
      <PageHeader title="Operations Intelligence" icon={Activity}
        description="Demand forecasts, capacity, no-shows, coverage, store trends and anomaly flags." />

      <div className="grid lg:grid-cols-2 gap-4">
        <Section title="Appointment demand forecast" dataset="demand" loading={demand.isLoading}
          empty={!demand.data?.points.length}
          note={demand.data ? `Trend ${demand.data.trendPerDay >= 0 ? "+" : ""}${demand.data.trendPerDay}/day - about ${demand.data.projectedTotalNext14Days} appointments projected over the next 14 days.` : undefined}>
          <LineSeriesChart data={demand.data?.points ?? []} xKey="date" series={[
            { key: "actual", label: "Daily" },
            { key: "movingAvg7", label: "7-day average" },
            { key: "projected", label: "Projected" }
          ]} />
        </Section>

        <Section title="Capacity utilisation (booked vs available slots)" dataset="capacity" loading={capacity.isLoading}
          empty={!capacity.data?.length}>
          <AreaSeriesChart data={capacity.data ?? []} xKey="date" dataKey="utilisationPct" label="Utilisation %" />
        </Section>

        <Section title="No-show and cancellation rates by week" dataset="no-show" loading={noShow.isLoading}
          empty={!noShowWeekly.length}>
          <LineSeriesChart data={noShowWeekly} xKey="weekStart" series={[
            { key: "noShowRate", label: "No-show %" },
            { key: "cancellationRate", label: "Cancellation %" }
          ]} />
          {worstDoctors.length > 0 && (
            <table className="mt-3 w-full text-sm">
              <thead><tr className="text-left text-muted-foreground">
                <th className="py-1 font-medium">Doctor (8 wks)</th><th className="font-medium">Appts</th>
                <th className="font-medium">No-show</th><th className="font-medium">Cancelled</th>
              </tr></thead>
              <tbody>
                {worstDoctors.map(d => (
                  <tr key={d.name} className="border-t">
                    <td className="py-1">{d.name}</td><td>{d.total}</td><td>{d.noShowRate}%</td><td>{d.cancelRate}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Section>

        <Section title="Vaccine coverage by species" dataset="vaccine-coverage" loading={vaccines.isLoading}
          empty={!vaccines.data?.length} note="Share of pets with at least one vaccination record and nothing overdue.">
          <HBarChart data={vaccines.data ?? []} dataKey="coveragePct" labelKey="species" />
        </Section>

        <Section title="Lost-pet recovery trend" dataset="lost-pets" loading={lost.isLoading} empty={!lost.data?.length}>
          <LineSeriesChart data={lost.data ?? []} xKey="weekStart" series={[
            { key: "reported", label: "Reported" },
            { key: "reunited", label: "Reunited" },
            { key: "stillOpen", label: "Still open" }
          ]} />
        </Section>

        <Section title="Store conversion and returns" dataset="store" loading={store.isLoading} empty={!store.data?.length}
          note="Conversion = ordering customers / customers who added to cart that week. Return rate = return requests / orders.">
          <LineSeriesChart data={store.data ?? []} xKey="weekStart" series={[
            { key: "conversionPct", label: "Conversion %" },
            { key: "returnRatePct", label: "Return rate %" }
          ]} />
        </Section>
      </div>

      <div className="grid lg:grid-cols-2 gap-4">
        <Section title="Inventory run-out forecast" dataset="inventory" loading={inventory.isLoading}
          empty={!inventory.data?.length} note="Stock divided by average daily sales over the last 30 days.">
          <div className="overflow-x-auto max-h-80 overflow-y-auto">
            <table className="w-full text-sm">
              <thead><tr className="text-left text-muted-foreground">
                <th className="py-1 font-medium">Product</th><th className="font-medium">Stock</th>
                <th className="font-medium">Sales/day</th><th className="font-medium">Days left</th>
              </tr></thead>
              <tbody>
                {(inventory.data ?? []).slice(0, 20).map(p => (
                  <tr key={p.productId} className="border-t">
                    <td className="py-1 pr-2">{p.name}</td>
                    <td>{p.stock}</td>
                    <td>{p.avgDailySales}</td>
                    <td>
                      {p.daysRemaining == null ? "-" : (
                        <Badge variant={p.daysRemaining <= 7 ? "destructive" : p.daysRemaining <= 21 ? "warning" : "success"}>
                          {p.daysRemaining}d
                        </Badge>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Section>

        <Section title="Anomaly flags (z-score)" dataset="anomalies" loading={anomalies.isLoading}
          empty={!anomalies.data?.length} note="Daily orders and appointments more than 2.5 standard deviations from the 60-day mean.">
          <ul className="divide-y text-sm max-h-80 overflow-y-auto">
            {(anomalies.data ?? []).map(a => (
              <li key={`${a.metric}-${a.date}`} className="flex items-center justify-between py-1.5">
                <span>{a.date} - {a.metric}</span>
                <span className="flex items-center gap-2">
                  <span className="text-muted-foreground">{a.value} (z={a.zScore})</span>
                  <Badge variant={a.direction === "spike" ? "warning" : "info"}>{a.direction}</Badge>
                </span>
              </li>
            ))}
          </ul>
        </Section>
      </div>

      <ScheduledReportsPanel />
    </div>
  );
}
