import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Package, RefreshCw, Truck, MapPin, Receipt } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { ordersV2Api } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

const STEPS = ["Created", "Confirmed", "Packed", "Shipped", "Delivered"] as const;

export function OrderDetailPage() {
  const { id = "" } = useParams();
  const qc = useQueryClient();

  const { data: order, isLoading } = useQuery({
    queryKey: ["order", id], queryFn: () => ordersV2Api.get(id), enabled: !!id
  });

  const cancel = useMutation({
    mutationFn: () => ordersV2Api.cancel(id, "Buyer requested cancel"),
    onSuccess: () => { toast.success("Order cancelled"); qc.invalidateQueries({ queryKey: ["order", id] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not cancel.")
  });

  if (isLoading || !order) {
    return <div className="max-w-3xl mx-auto"><Skeleton className="h-64" /></div>;
  }

  const stepIndex = Math.max(0, STEPS.indexOf(order.status as typeof STEPS[number]));
  const canCancel = order.status !== "Shipped" && order.status !== "Delivered" && order.status !== "Cancelled";

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild><Link to="/orders"><ArrowLeft className="h-4 w-4 mr-1" /> Back to orders</Link></Button>

      <Card>
        <CardHeader className="flex flex-row justify-between items-start space-y-0">
          <div>
            <CardTitle className="flex items-center gap-2"><Package className="h-5 w-5 text-primary" /> {order.orderNumber}</CardTitle>
            <p className="text-xs text-muted-foreground">{new Date(order.createdAt).toLocaleString()}</p>
          </div>
          <div className="flex gap-2">
            <Badge>{order.status}</Badge>
            <Badge variant="outline">{order.paymentStatus}</Badge>
            <Badge variant="outline" className="flex items-center gap-1"><RefreshCw className="h-3 w-3" />{order.shipmentStatus}</Badge>
          </div>
        </CardHeader>
        <CardContent>
          {/* Progress tracker */}
          <div className="flex items-center justify-between mb-4">
            {STEPS.map((s, i) => (
              <div key={s} className="flex-1 flex items-center">
                <div className={`h-7 w-7 rounded-full grid place-items-center text-xs font-medium
                  ${i <= stepIndex ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"}`}>
                  {i + 1}
                </div>
                <span className="ml-2 text-xs text-muted-foreground">{s}</span>
                {i < STEPS.length - 1 && <div className={`flex-1 h-0.5 mx-2 ${i < stepIndex ? "bg-primary" : "bg-muted"}`} />}
              </div>
            ))}
          </div>

          <div className="grid sm:grid-cols-2 gap-3 text-sm">
            <div className="space-y-1">
              <p className="font-semibold flex items-center gap-1"><MapPin className="h-4 w-4" /> Ship to</p>
              <p className="text-muted-foreground whitespace-pre-line">{order.shippingAddress}</p>
              <p className="text-muted-foreground">{[order.shippingCity, order.shippingCountry].filter(Boolean).join(", ")}</p>
            </div>
            <div className="space-y-1">
              <p className="font-semibold flex items-center gap-1"><Truck className="h-4 w-4" /> Shipment</p>
              <p className="text-muted-foreground">Status: {order.shipmentStatus}</p>
              {order.trackingNumber && <p className="text-muted-foreground">Tracking: <span className="font-mono">{order.trackingNumber}</span></p>}
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base flex items-center gap-2"><Receipt className="h-4 w-4" /> Items</CardTitle></CardHeader>
        <CardContent className="space-y-3 divide-y">
          {order.items.map((i) => (
            <div key={i.id} className="flex items-center gap-3 pt-3 first:pt-0">
              <div className="h-14 w-14 bg-muted rounded overflow-hidden flex-shrink-0">
                {i.imageUrl && <img src={i.imageUrl} className="object-cover w-full h-full" />}
              </div>
              <div className="flex-1">
                <Link to={`/store/products/${i.productId}`} className="font-medium hover:underline">{i.productName}</Link>
                <p className="text-xs text-muted-foreground">{i.storeName}</p>
              </div>
              <div className="text-right text-sm">
                <p>{i.quantity} × ${i.unitPrice.toFixed(2)}</p>
                <p className="font-semibold">${i.total.toFixed(2)}</p>
              </div>
            </div>
          ))}
          <div className="pt-3 space-y-1 text-sm">
            <div className="flex justify-between text-muted-foreground"><span>Subtotal</span><span>${order.subtotal.toFixed(2)}</span></div>
            <div className="flex justify-between text-muted-foreground"><span>Shipping</span><span>${order.shippingFee.toFixed(2)}</span></div>
            <div className="flex justify-between text-muted-foreground"><span>Tax</span><span>${order.tax.toFixed(2)}</span></div>
            <Separator />
            <div className="flex justify-between font-semibold pt-1"><span>Total</span><span>${order.total.toFixed(2)}</span></div>
          </div>
        </CardContent>
      </Card>

      {canCancel && (
        <Button variant="destructive" onClick={() => cancel.mutate()} disabled={cancel.isPending}>
          {cancel.isPending ? "Cancelling..." : "Cancel order"}
        </Button>
      )}
    </div>
  );
}
