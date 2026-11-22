import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Undo2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/components/ui/sonner";
import { useAuthStore } from "@/store/authStore";
import { deliveriesApi } from "@/api/marketplace";
import { logisticsApi, type ReturnPickup, type ReturnPickupStatus } from "@/api/logisticsPlus";

const NEXT: Partial<Record<ReturnPickupStatus, ReturnPickupStatus[]>> = {
  Scheduled: ["PickedUp", "Failed"], PickedUp: ["InTransit", "Failed"], InTransit: ["Failed"]
};

function PickupCard({ p, dispatcher }: { p: ReturnPickup; dispatcher: boolean }) {
  const qc = useQueryClient();
  const [courier, setCourier] = useState("");
  const { data: couriers } = useQuery({ queryKey: ["delivery-users"], queryFn: () => deliveriesApi.listDeliveryUsers(), enabled: dispatcher });
  const refresh = () => { qc.invalidateQueries({ queryKey: ["return-pickups-mine"] }); qc.invalidateQueries({ queryKey: ["return-pickups-admin"] }); };
  const err = (e: unknown) => toast.error((e as any)?.response?.data?.error?.message ?? "Request failed.");
  const update = useMutation({ mutationFn: (status: ReturnPickupStatus) => logisticsApi.courierUpdate(p.id, { status }), onSuccess: refresh, onError: err });
  const assign = useMutation({ mutationFn: () => logisticsApi.assignPickup(p.id, courier), onSuccess: () => { toast.success("Courier assigned"); refresh(); }, onError: err });
  return (
    <Card>
      <CardContent className="py-3 space-y-2 text-sm">
        <div className="flex items-center gap-2 font-medium">Order {p.orderNumber ?? p.orderId} <Badge variant="outline">{p.status}</Badge></div>
        <div>{p.productName}</div>
        <div className="text-muted-foreground">{new Date(p.windowStart).toLocaleString()} to {new Date(p.windowEnd).toLocaleString()}</div>
        <div className="text-muted-foreground">{p.address}</div>
        {p.deliveryUserName && <div>Courier: {p.deliveryUserName}</div>}
        <div className="flex flex-wrap gap-2">
          {!dispatcher && (NEXT[p.status] ?? []).map((s) => (
            <Button key={s} size="sm" variant={s === "Failed" ? "outline" : "default"} disabled={update.isPending} onClick={() => update.mutate(s)}>
              {s === "PickedUp" ? "Mark picked up" : s === "InTransit" ? "In transit" : "Mark failed"}
            </Button>
          ))}
          {dispatcher && ["Requested", "Scheduled", "Failed"].includes(p.status) && (
            <>
              <select className="border rounded px-2 py-1 bg-background" value={courier} onChange={(e) => setCourier(e.target.value)} aria-label="Courier">
                <option value="">Choose courier</option>
                {couriers?.map((c) => <option key={c.userId} value={c.userId}>{c.displayName}</option>)}
              </select>
              <Button size="sm" disabled={!courier || assign.isPending} onClick={() => assign.mutate()}>Assign</Button>
            </>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

/** Courier task list for return pickups; dispatchers (delivery.assign / delivery.edit) also see the whole queue and can assign. */
export function ReturnPickupsPage() {
  const has = useAuthStore((s) => s.hasPermission);
  const dispatcher = has("delivery.assign") || has("delivery.edit");
  const { data: mine = [] } = useQuery({ queryKey: ["return-pickups-mine"], queryFn: () => logisticsApi.myPickups(false), retry: false });
  const { data: admin } = useQuery({ queryKey: ["return-pickups-admin"], queryFn: () => logisticsApi.pickupsAdmin(), enabled: dispatcher, retry: false });
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2"><Undo2 className="h-6 w-6 text-primary" /> Return pickups</h1>
      <Card><CardHeader><CardTitle className="text-base">My pickups</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          {mine.length ? mine.map((p) => <PickupCard key={p.id} p={p} dispatcher={false} />) : <p className="text-sm text-muted-foreground">No pickups assigned.</p>}
        </CardContent></Card>
      {dispatcher && (
        <Card><CardHeader><CardTitle className="text-base">Dispatch queue</CardTitle></CardHeader>
          <CardContent className="space-y-2">
            {admin?.items.length ? admin.items.map((p) => <PickupCard key={p.id} p={p} dispatcher />) : <p className="text-sm text-muted-foreground">Nothing in the queue.</p>}
          </CardContent></Card>
      )}
    </div>
  );
}
