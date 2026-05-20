import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ShoppingBag, Package, DollarSign, Star, Boxes, Settings, Plus, AlertTriangle } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { storesApi } from "@/api/marketplace";

export function StoreDashboardPage() {
  const { data: store } = useQuery({ queryKey: ["my-store"], queryFn: () => storesApi.mine() });

  const sinceMs = 30 * 24 * 60 * 60 * 1000;
  const { data: report } = useQuery({
    queryKey: ["store-report", store?.id],
    queryFn: () => storesApi.report(store!.id,
      new Date(Date.now() - sinceMs).toISOString(),
      new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString()),
    enabled: !!store
  });

  if (!store) {
    return (
      <Card><CardContent className="py-16 text-center space-y-3">
        <ShoppingBag className="h-12 w-12 mx-auto text-muted-foreground" />
        <p className="font-medium">You don't have a store yet.</p>
        <Button asChild><Link to="/store/register">Become a seller</Link></Button>
      </CardContent></Card>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <ShoppingBag className="h-6 w-6 text-primary" /> {store.name}
            <Badge variant={store.approvalStatus === "Approved" ? "default" : "outline"}>{store.approvalStatus}</Badge>
          </h1>
          <p className="text-sm text-muted-foreground">Orders, inventory, and product performance.</p>
        </div>
        <div className="flex gap-2">
          <Button asChild variant="outline"><Link to="/dashboard/store/products"><Boxes className="h-4 w-4 mr-1" /> Products</Link></Button>
          <Button asChild variant="outline"><Link to="/dashboard/store/orders"><Package className="h-4 w-4 mr-1" /> Orders</Link></Button>
          <Button asChild><Link to="/dashboard/store/products/new"><Plus className="h-4 w-4 mr-1" /> New product</Link></Button>
        </div>
      </div>

      {store.approvalStatus !== "Approved" && (
        <Card>
          <CardContent className="py-3 flex items-center gap-2 text-sm">
            <AlertTriangle className="h-4 w-4 text-amber-500" />
            Your store is <Badge variant="outline">{store.approvalStatus}</Badge>. Products can be created but won't be listed publicly until approved.
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <Stat label="Orders (30d)" value={report?.ordersCount.toString() ?? "—"} Icon={Package} />
        <Stat label="Revenue (30d)" value={report ? `$${report.grossRevenue.toFixed(0)}` : "—"} Icon={DollarSign} />
        <Stat label="Commission (30d)" value={report ? `$${report.commission.toFixed(0)}` : "—"} Icon={Settings} />
        <Stat label="Avg rating" value={store.avgRating.toFixed(1)} Icon={Star} />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card>
          <CardHeader><CardTitle className="text-base">Top products (30d)</CardTitle></CardHeader>
          <CardContent className="text-sm space-y-2">
            {report?.topProducts.length
              ? report.topProducts.map((t) => (
                  <div key={t.productId} className="flex justify-between">
                    <Link to={`/dashboard/store/products/${t.productId}`} className="hover:underline truncate">{t.name}</Link>
                    <span className="text-muted-foreground">{t.unitsSold} • ${t.revenue.toFixed(0)}</span>
                  </div>
                ))
              : <p className="text-sm text-muted-foreground py-3">No sales yet.</p>}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle className="text-base">Daily revenue</CardTitle></CardHeader>
          <CardContent className="text-sm">
            <div className="grid grid-cols-7 gap-1 items-end h-32">
              {report?.daily.slice(-21).map((d) => {
                const max = Math.max(...(report.daily.map((x) => x.revenue) ?? [0]));
                const h = max > 0 ? Math.round((d.revenue / max) * 100) : 0;
                return (
                  <div key={d.date} title={`${d.date}: $${d.revenue.toFixed(2)}`}
                    className="bg-primary/40 rounded-sm" style={{ height: `${h}%` }} />
                );
              })}
              {(!report || report.daily.length === 0) && <p className="text-sm text-muted-foreground col-span-7">No data.</p>}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function Stat({ label, value, Icon }: { label: string; value: string; Icon: typeof Package }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
          <Icon className="h-4 w-4" /> {label}
        </CardTitle>
      </CardHeader>
      <CardContent className="text-3xl font-bold">{value}</CardContent>
    </Card>
  );
}
