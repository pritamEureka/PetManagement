import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { CheckCircle2, Clock, Package, ArrowRight, Home } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { ordersV2Api } from "@/api/marketplace";

export function CheckoutSuccessPage() {
  const [params] = useSearchParams();
  const orderId = params.get("orderId") ?? "";

  // SSLCommerz hits the backend success URL first; by the time the browser
  // lands here, the backend has already validated and stamped paymentStatus.
  // We still poll briefly (3 attempts, 2s gap) for the rare race where the
  // IPN beats the redirect-validate ordering and the row hasn't flipped yet.
  const { data: order, isLoading, refetch } = useQuery({
    queryKey: ["order", orderId],
    queryFn: () => ordersV2Api.get(orderId),
    enabled: !!orderId,
    refetchInterval: (q) => {
      const o = q.state.data;
      return o && (o.paymentStatus === "Paid" || o.paymentStatus === "Failed") ? false : 2000;
    }
  });

  if (!orderId) {
    return (
      <div className="max-w-xl mx-auto">
        <Card>
          <CardContent className="py-10 text-center space-y-3">
            <p className="text-muted-foreground">No order specified.</p>
            <Button asChild><Link to="/marketplace"><Home className="h-4 w-4 mr-1" /> Back to marketplace</Link></Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (isLoading || !order) {
    return <div className="max-w-xl mx-auto"><Skeleton className="h-72" /></div>;
  }

  const paid = order.paymentStatus === "Paid";

  return (
    <div className="max-w-xl mx-auto space-y-4">
      <Card>
        <CardHeader className="text-center pb-2">
          {paid ? (
            <CheckCircle2 className="h-14 w-14 text-emerald-500 mx-auto mb-2" />
          ) : (
            <Clock className="h-14 w-14 text-amber-500 mx-auto mb-2" />
          )}
          <CardTitle className="text-2xl">
            {paid ? "Payment successful" : "Payment processing"}
          </CardTitle>
          <p className="text-sm text-muted-foreground mt-1">
            {paid
              ? `Thanks — your order ${order.orderNumber} is confirmed.`
              : `We're waiting for the gateway to confirm ${order.orderNumber}. This usually takes a few seconds.`}
          </p>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-center gap-2">
            <Badge variant={statusBadgeVariant(order.status)}>{order.status}</Badge>
            <Badge variant={statusBadgeVariant(order.paymentStatus)}>{order.paymentStatus}</Badge>
          </div>

          <Separator />

          <div className="space-y-2 text-sm">
            <div className="flex justify-between text-muted-foreground">
              <span>Subtotal</span><span>{order.subtotal.toFixed(2)}</span>
            </div>
            {order.shippingFee > 0 && (
              <div className="flex justify-between text-muted-foreground">
                <span>Shipping</span><span>{order.shippingFee.toFixed(2)}</span>
              </div>
            )}
            {order.tax > 0 && (
              <div className="flex justify-between text-muted-foreground">
                <span>Tax</span><span>{order.tax.toFixed(2)}</span>
              </div>
            )}
            <div className="flex justify-between font-semibold">
              <span>Total</span><span>{order.total.toFixed(2)}</span>
            </div>
          </div>

          <Separator />

          <div className="space-y-2">
            <p className="text-xs uppercase tracking-wide text-muted-foreground">Items</p>
            <ul className="space-y-1 text-sm">
              {order.items.map((it) => (
                <li key={it.id} className="flex justify-between">
                  <span className="truncate">{it.quantity}× {it.productName}</span>
                  <span className="font-medium">{it.total.toFixed(2)}</span>
                </li>
              ))}
            </ul>
          </div>

          <div className="flex flex-col sm:flex-row gap-2 pt-2">
            <Button asChild className="flex-1">
              <Link to={`/orders/${order.id}`}><Package className="h-4 w-4 mr-1" /> View order</Link>
            </Button>
            <Button variant="outline" asChild className="flex-1">
              <Link to="/marketplace">Continue shopping <ArrowRight className="h-4 w-4 ml-1" /></Link>
            </Button>
          </div>

          {!paid && (
            <Button variant="ghost" size="sm" onClick={() => refetch()} className="w-full">
              Refresh status
            </Button>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
