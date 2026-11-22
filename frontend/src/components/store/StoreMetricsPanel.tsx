import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { LineSeriesChart } from "@/components/admin/Charts";
import { logisticsApi } from "@/api/logisticsPlus";

const pct = (n: number) => `${(n * 100).toFixed(1)}%`;

function Tile({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-md border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-xl font-semibold">{value}</div>
      {hint && <div className="text-xs text-muted-foreground">{hint}</div>}
    </div>
  );
}

/** Seller scorecard: cancellation, late-shipment, return, dispute and conversion rates plus revenue trend and open SLA breaches. */
export function StoreMetricsPanel() {
  const [days, setDays] = useState(30);
  const { data, isLoading } = useQuery({ queryKey: ["store-metrics", days], queryFn: () => logisticsApi.myMetrics(days), retry: false });
  const { data: breaches } = useQuery({ queryKey: ["store-sla-open"], queryFn: () => logisticsApi.myBreaches(true), retry: false });

  if (isLoading) return <Skeleton className="h-48" />;
  if (!data) return null;
  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base">Seller performance</CardTitle>
        <select className="border rounded px-2 py-1 text-sm bg-background" value={days} onChange={(e) => setDays(Number(e.target.value))} aria-label="Period">
          {[7, 30, 90, 365].map((d) => <option key={d} value={d}>Last {d} days</option>)}
        </select>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <Tile label="Cancellation rate" value={pct(data.cancellationRate)} hint={`${data.cancelledOrders} of ${data.orders} orders`} />
          <Tile label="Late shipments" value={pct(data.lateShipmentRate)} hint={`${data.lateShipments} of ${data.shipSlaOrders} orders`} />
          <Tile label="Return rate" value={pct(data.returnRate)} hint={`${data.returnRequests} requests`} />
          <Tile label="Disputed returns" value={pct(data.disputeRate)} hint={`${data.rejectedReturns} rejected`} />
          <Tile label="Conversion" value={data.conversionAvailable ? pct(data.conversionRate) : "n/a"} hint={`${data.productViewers} viewers`} />
          <Tile label="Avg rating" value={data.reviewCount ? data.averageRating.toFixed(2) : "n/a"} hint={`${data.reviewCount} reviews`} />
          <Tile label="Revenue" value={data.revenue.toFixed(2)} />
          <Tile label="Open SLA breaches" value={String(breaches?.total ?? 0)} />
        </div>
        {data.dailyRevenue.length > 0 && (
          <LineSeriesChart data={data.dailyRevenue} xKey="label" series={[{ key: "value", label: "Revenue" }]} height={200} />
        )}
        {data.topProducts.length > 0 && (
          <div className="text-sm space-y-1">
            {data.topProducts.map((t) => (
              <div key={t.productId} className="flex justify-between"><span className="truncate">{t.name}</span>
                <span className="text-muted-foreground">{t.units} units, {t.revenue.toFixed(2)}</span></div>
            ))}
          </div>
        )}
        {!!breaches?.items.length && (
          <div className="text-sm space-y-1">
            {breaches.items.slice(0, 5).map((b) => (
              <div key={b.id} className="flex items-center gap-2"><Badge variant="destructive">{b.kind}</Badge>
                <span>Order {b.orderNumber ?? b.orderId}</span></div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
