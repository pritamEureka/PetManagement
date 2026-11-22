import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { withStepUp } from "@/lib/withStepUp";
import { logisticsApi } from "@/api/logisticsPlus";

export interface RefundableItem { id: string; name: string; quantity: number; total: number }

/**
 * Admin partial refund for selected order lines (one refund per line) plus the per-store split.
 * Mount in the admin order detail: <PartialRefundPanel orderId={o.id} items={o.items.map(...)} />
 */
export function PartialRefundPanel({ orderId, items }: { orderId: string; items: RefundableItem[] }) {
  const qc = useQueryClient();
  const [sel, setSel] = useState<Record<string, number>>({});
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const { data: summary } = useQuery({ queryKey: ["order-refunds", orderId], queryFn: () => logisticsApi.refunds(orderId), retry: false });
  const { data: split } = useQuery({ queryKey: ["order-split", orderId], queryFn: () => logisticsApi.storeSplit(orderId), retry: false });
  const refunded = new Set(summary?.refunds.map((r) => r.orderItemId));
  const chosen = Object.entries(sel).filter(([, q]) => q > 0);

  async function submit() {
    setBusy(true);
    try {
      const res = await withStepUp(() => logisticsApi.partialRefund(orderId,
        { reason, items: chosen.map(([orderItemId, quantity]) => ({ orderItemId, quantity })) }));
      toast.success(`Refund of ${res.amount.toFixed(2)} initiated (${res.gatewayStatus})`);
      setSel({}); setReason("");
      qc.invalidateQueries({ queryKey: ["order-refunds", orderId] });
      qc.invalidateQueries({ queryKey: ["order-split", orderId] });
    } catch (e) {
      toast.error((e as any)?.response?.data?.error?.message ?? "Refund failed.");
    } finally { setBusy(false); }
  }

  return (
    <div className="rounded-md border p-3 text-sm space-y-3">
      <div className="font-medium">Partial refund</div>
      {items.map((it) => (
        <div key={it.id} className="flex items-center gap-2">
          <span className="flex-1 truncate">{it.name} ({it.total.toFixed(2)})</span>
          {refunded.has(it.id) ? <span className="text-muted-foreground">refunded</span> : (
            <input type="number" min={0} max={it.quantity} className="w-16 border rounded px-1 bg-background" aria-label={`Quantity of ${it.name}`}
              value={sel[it.id] ?? 0} onChange={(e) => setSel({ ...sel, [it.id]: Math.min(it.quantity, Math.max(0, Number(e.target.value))) })} />
          )}
        </div>
      ))}
      <input placeholder="Reason" className="w-full border rounded px-2 py-1 bg-background" value={reason} onChange={(e) => setReason(e.target.value)} />
      <Button size="sm" disabled={busy || !reason.trim() || chosen.length === 0} onClick={submit}>Refund selected items</Button>
      {summary && summary.refundedAmount > 0 && <p className="text-muted-foreground">Refunded so far: {summary.refundedAmount.toFixed(2)} of {summary.orderTotal.toFixed(2)}</p>}
      {split && split.length > 1 && (
        <table className="w-full text-xs">
          <thead><tr className="text-left"><th>Store</th><th>Net</th><th>Ship</th><th>Commission</th><th>Refunded</th><th>Payable</th></tr></thead>
          <tbody>{split.map((s) => (
            <tr key={s.storeId}><td>{s.storeName ?? s.storeId.slice(0, 8)}</td><td>{s.netSubtotal.toFixed(2)}</td><td>{s.shipping.toFixed(2)}</td>
              <td>{s.commission.toFixed(2)}</td><td>{s.refunded.toFixed(2)}</td><td>{s.payable.toFixed(2)}</td></tr>
          ))}</tbody>
        </table>
      )}
    </div>
  );
}
