import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { XCircle, AlertTriangle, RotateCcw, ShoppingCart, Package } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ordersV2Api } from "@/api/marketplace";

export function CheckoutCancelPage() {
  const [params] = useSearchParams();
  const orderId = params.get("orderId") ?? "";
  const reason = (params.get("reason") ?? "cancelled").toLowerCase();
  const failed = reason === "failed";

  const { data: order, isLoading } = useQuery({
    queryKey: ["order", orderId],
    queryFn: () => ordersV2Api.get(orderId),
    enabled: !!orderId
  });

  return (
    <div className="max-w-xl mx-auto space-y-4">
      <Card>
        <CardHeader className="text-center pb-2">
          {failed ? (
            <AlertTriangle className="h-14 w-14 text-amber-500 mx-auto mb-2" />
          ) : (
            <XCircle className="h-14 w-14 text-muted-foreground mx-auto mb-2" />
          )}
          <CardTitle className="text-2xl">
            {failed ? "Payment failed" : "Payment cancelled"}
          </CardTitle>
          <p className="text-sm text-muted-foreground mt-1">
            {failed
              ? "The gateway reported the payment did not complete. Your order is on hold — try again or pick a different method."
              : "You cancelled the payment. Your order is still saved and you can retry whenever you're ready."}
          </p>
        </CardHeader>
        <CardContent className="space-y-3">
          {isLoading ? <Skeleton className="h-16" /> : order ? (
            <p className="text-sm text-center">
              Order <span className="font-mono">{order.orderNumber}</span> — total{" "}
              <span className="font-semibold">{order.total.toFixed(2)}</span>
            </p>
          ) : null}

          <div className="flex flex-col sm:flex-row gap-2 pt-2">
            <Button asChild className="flex-1">
              <Link to="/cart"><RotateCcw className="h-4 w-4 mr-1" /> Retry checkout</Link>
            </Button>
            {orderId && (
              <Button variant="outline" asChild className="flex-1">
                <Link to={`/orders/${orderId}`}><Package className="h-4 w-4 mr-1" /> View order</Link>
              </Button>
            )}
            <Button variant="ghost" asChild className="flex-1">
              <Link to="/marketplace"><ShoppingCart className="h-4 w-4 mr-1" /> Keep shopping</Link>
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
