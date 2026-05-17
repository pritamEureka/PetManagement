import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Package, RefreshCw } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
import { ordersV2Api } from "@/api/marketplace";

const STATUS_VARIANT: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
  Created: "outline", Confirmed: "secondary", Packed: "secondary",
  Shipped: "default", Delivered: "default", Cancelled: "destructive", Returned: "outline"
};

export function OrdersPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["orders", "mine"],
    queryFn: () => ordersV2Api.mine(1, 50)
  });

  return (
    <div className="space-y-4 max-w-4xl mx-auto">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold flex items-center gap-2"><Package className="h-6 w-6 text-primary" /> My orders</h1>
      </div>

      {isLoading ? (
        <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-32" />)}</div>
      ) : !data || data.items.length === 0 ? (
        <Card><CardContent className="py-16 text-center text-muted-foreground">No orders yet.</CardContent></Card>
      ) : (
        <div className="space-y-3">
          {data.items.map((o) => (
            <Card key={o.id}>
              <CardHeader className="flex flex-row justify-between items-start space-y-0">
                <div>
                  <CardTitle className="text-base">
                    <Link to={`/orders/${o.id}`} className="hover:underline">{o.orderNumber}</Link>
                  </CardTitle>
                  <p className="text-xs text-muted-foreground">{new Date(o.createdAt).toLocaleString()}</p>
                </div>
                <div className="flex gap-2 flex-wrap justify-end">
                  <Badge variant={STATUS_VARIANT[o.status] ?? "outline"}>{o.status}</Badge>
                  <Badge variant="outline">{o.paymentStatus}</Badge>
                  <Badge variant="outline" className="flex items-center gap-1">
                    <RefreshCw className="h-3 w-3" /> {o.shipmentStatus}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-2">
                <div className="text-sm space-y-1">
                  {o.items.map((i) => (
                    <div key={i.id} className="flex justify-between">
                      <span>{i.quantity}× {i.productName} <span className="text-xs text-muted-foreground">({i.storeName})</span></span>
                      <span>${i.total.toFixed(2)}</span>
                    </div>
                  ))}
                </div>
                <Separator />
                <div className="flex justify-between font-semibold">
                  <span>Total</span><span>${o.total.toFixed(2)}</span>
                </div>
                {o.trackingNumber && (
                  <p className="text-xs text-muted-foreground">
                    Tracking: <span className="font-mono">{o.trackingNumber}</span>
                  </p>
                )}
                <div className="flex justify-end pt-2">
                  <Button asChild size="sm" variant="outline"><Link to={`/orders/${o.id}`}>View details</Link></Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
