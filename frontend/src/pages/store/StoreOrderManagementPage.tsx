import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Package, Truck } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { storesApi, ordersV2Api, type OrderStatus, type ShipmentStatus } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

const STATUS_OPTIONS: OrderStatus[] = ["Created", "Confirmed", "Packed", "Shipped", "Delivered", "Cancelled", "Returned"];
const SHIP_OPTIONS: ShipmentStatus[] = ["NotShipped", "Processing", "InTransit", "OutForDelivery", "Delivered", "Failed"];

export function StoreOrderManagementPage() {
  const qc = useQueryClient();
  const [statusFilter, setStatusFilter] = useState<OrderStatus | "all">("all");

  const { data: store } = useQuery({ queryKey: ["my-store"], queryFn: () => storesApi.mine() });

  const { data, isLoading } = useQuery({
    queryKey: ["store-orders", store?.id, statusFilter],
    queryFn: () => ordersV2Api.forStore(store!.id, statusFilter === "all" ? undefined : statusFilter, 1, 100),
    enabled: !!store
  });

  const updateStatus = useMutation({
    mutationFn: ({ id, status }: { id: string; status: OrderStatus }) => ordersV2Api.updateStatus(id, status),
    onSuccess: () => { toast.success("Order status updated"); qc.invalidateQueries({ queryKey: ["store-orders"] }); }
  });

  const updateShipment = useMutation({
    mutationFn: ({ id, status, trackingNumber }: { id: string; status: ShipmentStatus; trackingNumber?: string }) =>
      ordersV2Api.updateShipment(id, status, trackingNumber),
    onSuccess: () => { toast.success("Shipment updated"); qc.invalidateQueries({ queryKey: ["store-orders"] }); }
  });

  if (!store) return <p className="text-sm text-muted-foreground">Register a store first.</p>;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <h1 className="text-2xl font-bold flex items-center gap-2"><Package className="h-6 w-6 text-primary" /> Orders</h1>
        <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v as any)}>
          <SelectTrigger className="w-full sm:w-44"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {STATUS_OPTIONS.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (
        <div className="space-y-2">{[...Array(4)].map((_, i) => <Skeleton key={i} className="h-32" />)}</div>
      ) : !data?.items.length ? (
        <Card><CardContent className="py-12 text-center text-muted-foreground">No orders match.</CardContent></Card>
      ) : (
        <div className="space-y-3">
          {data.items.map((o) => (
            <Card key={o.id}>
              <CardHeader className="flex flex-row justify-between items-start space-y-0 pb-2">
                <div>
                  <CardTitle className="text-base">{o.orderNumber}</CardTitle>
                  <p className="text-xs text-muted-foreground">{new Date(o.createdAt).toLocaleString()}</p>
                </div>
                <div className="flex gap-2 flex-wrap">
                  <Badge>{o.status}</Badge>
                  <Badge variant="outline">{o.paymentStatus}</Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                <div className="space-y-1">
                  {o.items.filter((i) => i.storeId === store.id).map((i) => (
                    <div key={i.id} className="flex justify-between">
                      <span>{i.quantity}× {i.productName}</span>
                      <span>${i.total.toFixed(2)} <span className="text-xs text-muted-foreground">(cmsn ${i.commissionAmount.toFixed(2)})</span></span>
                    </div>
                  ))}
                </div>
                <Separator />
                <p className="text-xs text-muted-foreground whitespace-pre-line">Ship to: {o.shippingAddress}</p>

                <div className="grid sm:grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-muted-foreground">Order status</label>
                    <Select value={o.status} onValueChange={(v) => updateStatus.mutate({ id: o.id, status: v as OrderStatus })}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {STATUS_OPTIONS.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <ShipmentEditor
                    orderId={o.id}
                    current={o.shipmentStatus}
                    tracking={o.trackingNumber}
                    onSave={(status, tracking) => updateShipment.mutate({ id: o.id, status, trackingNumber: tracking })}
                  />
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

function ShipmentEditor({ current, tracking, onSave }: {
  orderId: string;
  current: ShipmentStatus;
  tracking?: string | null;
  onSave: (status: ShipmentStatus, tracking?: string) => void;
}) {
  const [status, setStatus] = useState<ShipmentStatus>(current);
  const [trackingNumber, setTrackingNumber] = useState(tracking ?? "");
  return (
    <div className="space-y-1">
      <label className="text-xs text-muted-foreground">Shipment</label>
      <div className="flex gap-2">
        <Select value={status} onValueChange={(v) => setStatus(v as ShipmentStatus)}>
          <SelectTrigger><SelectValue /></SelectTrigger>
          <SelectContent>
            {SHIP_OPTIONS.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
          </SelectContent>
        </Select>
        <Input placeholder="Tracking #" value={trackingNumber} onChange={(e) => setTrackingNumber(e.target.value)} />
        <Button size="sm" variant="outline" onClick={() => onSave(status, trackingNumber || undefined)}>
          <Truck className="h-3 w-3" />
        </Button>
      </div>
    </div>
  );
}
