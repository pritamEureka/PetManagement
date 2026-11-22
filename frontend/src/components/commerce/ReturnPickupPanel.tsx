import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/components/ui/sonner";
import { logisticsApi } from "@/api/logisticsPlus";

type Role = "buyer" | "store";
const toIso = (local: string) => new Date(local).toISOString();
const errMsg = (e: unknown) => (e as any)?.response?.data?.error?.message ?? "Request failed.";

/**
 * Pickup scheduling / tracking for an APPROVED return.
 *   <ReturnPickupPanel returnRequestId={r.id} role="buyer" />   (order detail, buyer)
 *   <ReturnPickupPanel returnRequestId={r.id} role="store" />   (store order view: confirm receipt)
 */
export function ReturnPickupPanel({ returnRequestId, role }: { returnRequestId: string; role: Role }) {
  const qc = useQueryClient();
  const key = ["return-pickup", returnRequestId];
  const { data: pickup, isLoading } = useQuery({ queryKey: key, queryFn: () => logisticsApi.pickupForReturn(returnRequestId), retry: false });
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [notes, setNotes] = useState("");
  const done = () => qc.invalidateQueries({ queryKey: key });
  const fail = (e: unknown) => toast.error(errMsg(e));

  const save = useMutation({
    mutationFn: () => {
      const b = { windowStart: toIso(start), windowEnd: toIso(end), notes: notes || undefined };
      return pickup ? logisticsApi.reschedulePickup(pickup.id, b) : logisticsApi.requestPickup(returnRequestId, b);
    },
    onSuccess: () => { toast.success("Pickup saved"); done(); }, onError: fail
  });
  const confirm = useMutation({
    mutationFn: () => logisticsApi.confirmReceipt(pickup!.id),
    onSuccess: () => { toast.success("Receipt confirmed"); done(); }, onError: fail
  });

  if (isLoading) return null;
  const canSchedule = role === "buyer" && (!pickup || ["Requested", "Scheduled", "Failed"].includes(pickup.status));
  return (
    <div className="rounded-md border p-3 text-sm space-y-2">
      <div className="flex items-center gap-2 font-medium">
        Return pickup {pickup && <Badge variant="outline">{pickup.status}</Badge>}
      </div>
      {pickup && (
        <div className="space-y-1">
          <p>Window: {new Date(pickup.windowStart).toLocaleString()} to {new Date(pickup.windowEnd).toLocaleString()}</p>
          <p className="text-muted-foreground">From: {pickup.address}</p>
          {pickup.deliveryUserName && <p>Courier: {pickup.deliveryUserName}</p>}
          <ol className="text-xs text-muted-foreground list-disc pl-4">
            {pickup.checkpoints.map((c) => <li key={c.id}>{c.status}{c.note ? ` - ${c.note}` : ""} ({new Date(c.occurredAt).toLocaleString()})</li>)}
          </ol>
        </div>
      )}
      {canSchedule && (
        <div className="flex flex-wrap items-end gap-2">
          <label className="text-xs">From<input type="datetime-local" className="block border rounded px-2 py-1 bg-background" value={start} onChange={(e) => setStart(e.target.value)} /></label>
          <label className="text-xs">To<input type="datetime-local" className="block border rounded px-2 py-1 bg-background" value={end} onChange={(e) => setEnd(e.target.value)} /></label>
          <input placeholder="Notes (optional)" className="border rounded px-2 py-1 bg-background" value={notes} onChange={(e) => setNotes(e.target.value)} />
          <Button size="sm" disabled={!start || !end || save.isPending} onClick={() => save.mutate()}>{pickup ? "Reschedule" : "Request pickup"}</Button>
        </div>
      )}
      {role === "store" && pickup && ["PickedUp", "InTransit"].includes(pickup.status) && (
        <Button size="sm" disabled={confirm.isPending} onClick={() => confirm.mutate()}>Confirm receipt</Button>
      )}
      {role === "store" && !pickup && <p className="text-muted-foreground">The buyer has not scheduled a pickup yet.</p>}
    </div>
  );
}
