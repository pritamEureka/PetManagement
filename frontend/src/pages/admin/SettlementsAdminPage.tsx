import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Banknote, Download, Plus, RefreshCw, CheckCircle2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { settlementsApi, type StorePayout } from "@/api/commercePlus";
import { toast } from "@/components/ui/sonner";
import { adminApi } from "@/api/adminV2";

const HOLD_KEY = "settlement.hold_days";

/** Accepts 7, "7" or {"value":7} (the shapes LedgerService understands). */
function parseHoldDays(valueJson?: string): number | null {
  if (!valueJson) return null;
  try {
    let v: unknown = JSON.parse(valueJson);
    if (v && typeof v === "object" && "value" in v) v = (v as { value: unknown }).value;
    const n = typeof v === "string" ? Number(v) : v;
    return typeof n === "number" && Number.isFinite(n) ? Math.min(365, Math.max(0, Math.trunc(n))) : null;
  } catch { return null; }
}

function HoldDaysCard() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ["admin-settings"], queryFn: () => adminApi.settings.list() });
  const current = parseHoldDays(data?.find((r) => r.key === HOLD_KEY)?.valueJson);
  const [value, setValue] = useState("");
  useEffect(() => { if (!isLoading) setValue(current == null ? "" : String(current)); }, [current, isLoading]);

  const n = Number(value);
  const valid = value.trim() !== "" && Number.isInteger(n) && n >= 0 && n <= 365;
  const save = useMutation({
    mutationFn: () => adminApi.settings.upsert(HOLD_KEY, {
      valueJson: JSON.stringify(n), category: "settlement",
      description: "Days seller earnings stay on hold before they can be paid out."
    }),
    onSuccess: () => { toast.success("Hold period saved"); qc.invalidateQueries({ queryKey: ["admin-settings"] }); },
    onError: (e) => toast.error(errMsg(e, "Could not save the hold period."))
  });

  return (
    <Card>
      <CardHeader className="pb-2"><CardTitle className="text-base">Hold period</CardTitle></CardHeader>
      <CardContent className="flex flex-wrap items-end gap-2">
        <div className="space-y-1">
          <label htmlFor="hold-days" className="text-xs text-muted-foreground">Days on hold (0 to 365)</label>
          <Input id="hold-days" type="number" min={0} max={365} step={1} className="w-32" disabled={isLoading}
            placeholder={current == null ? "default" : undefined}
            value={value} onChange={(e) => setValue(e.target.value)} />
        </div>
        <Button onClick={() => save.mutate()} disabled={!valid || save.isPending}>{save.isPending ? "Saving..." : "Save"}</Button>
        <p className="text-xs text-muted-foreground w-full">
          Applies to earnings recorded from now on. {current == null ? "No override set; the configured default is used." : ""}
        </p>
      </CardContent>
    </Card>
  );
}

const errMsg = (e: any, fallback: string) => e?.response?.data?.error?.message ?? fallback;

export function SettlementsAdminPage() {
  const qc = useQueryClient();
  const [selected, setSelected] = useState<string | null>(null);

  const { data: batches, isLoading } = useQuery({
    queryKey: ["settlement-batches"], queryFn: () => settlementsApi.batches(1, 50)
  });

  const generate = useMutation({
    mutationFn: () => settlementsApi.generate({}),
    onSuccess: (r) => {
      if (!r.generated || !r.batch) { toast.info("Nothing eligible: no store has a payable balance past the hold period."); return; }
      toast.success(`Batch ${r.batch.batch.reference} created`);
      setSelected(r.batch.batch.id);
      qc.invalidateQueries({ queryKey: ["settlement-batches"] });
    },
    onError: (e) => toast.error(errMsg(e, "Could not generate a batch."))
  });

  return (
    <div className="max-w-6xl mx-auto space-y-4">
      <HoldDaysCard />
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 flex-wrap gap-2">
          <div>
            <CardTitle className="flex items-center gap-2"><Banknote className="h-5 w-5 text-primary" /> Settlements</CardTitle>
            <p className="text-xs text-muted-foreground">
              Payout batches for seller earnings past the hold period. Hold days come from the
              <span className="font-mono"> settlement.hold_days</span> system setting (default <span className="font-mono">Settlement:HoldDays</span> = 7).
            </p>
          </div>
          <Button onClick={() => generate.mutate()} disabled={generate.isPending}>
            <Plus className="h-4 w-4 mr-1" /> {generate.isPending ? "Generating..." : "Generate batch"}
          </Button>
        </CardHeader>
        <CardContent>
          {isLoading ? <Skeleton className="h-24" /> : !batches || batches.items.length === 0 ? (
            <p className="text-sm text-muted-foreground py-6 text-center">No settlement batches yet.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-xs text-muted-foreground border-b">
                    <th className="py-1 pr-2">Reference</th><th className="pr-2">Status</th><th className="pr-2">Payouts</th>
                    <th className="pr-2">Created</th><th className="text-right">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {batches.items.map((b) => (
                    <tr key={b.id} onClick={() => setSelected(b.id)}
                      className={`border-b last:border-0 cursor-pointer hover:bg-muted/50 ${selected === b.id ? "bg-muted/60" : ""}`}>
                      <td className="py-1.5 pr-2 font-mono">{b.reference}</td>
                      <td className="pr-2"><Badge variant="secondary">{b.status}</Badge></td>
                      <td className="pr-2">{b.payoutCount}</td>
                      <td className="pr-2 whitespace-nowrap">{new Date(b.createdAt).toLocaleString()}</td>
                      <td className="text-right font-mono">{b.totalAmount.toFixed(2)} {b.currency}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {selected && <BatchDetail batchId={selected} />}
    </div>
  );
}

function BatchDetail({ batchId }: { batchId: string }) {
  const qc = useQueryClient();
  const [refs, setRefs] = useState<Record<string, string>>({});
  const [gwAmounts, setGwAmounts] = useState<Record<string, string>>({});

  const { data, isLoading } = useQuery({
    queryKey: ["settlement-batch", batchId], queryFn: () => settlementsApi.batch(batchId)
  });

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["settlement-batch", batchId] });
    qc.invalidateQueries({ queryKey: ["settlement-batches"] });
  };

  const markPaid = useMutation({
    mutationFn: (p: StorePayout) => settlementsApi.markPaid(p.id, {
      gatewayReference: (refs[p.id] ?? "").trim(),
      gatewayAmount: gwAmounts[p.id] ? Number(gwAmounts[p.id]) : null
    }),
    onSuccess: () => { toast.success("Payout marked as paid"); refresh(); },
    onError: (e) => toast.error(errMsg(e, "Could not mark paid."))
  });

  const fail = useMutation({
    mutationFn: (p: StorePayout) => settlementsApi.failPayout(p.id, "Gateway transfer failed"),
    onSuccess: () => { toast.success("Payout failed; entries released to the next batch"); refresh(); },
    onError: (e) => toast.error(errMsg(e, "Could not fail payout."))
  });

  const reconcile = useMutation({
    mutationFn: () => {
      const lines = (data?.payouts ?? [])
        .filter((p) => p.status === "Paid" || p.status === "Reconciled")
        .map((p) => ({
          payoutId: p.id,
          // Default to the amount the gateway reported when marking paid, else the expected amount.
          gatewayAmount: gwAmounts[p.id] ? Number(gwAmounts[p.id]) : (p.gatewayAmount ?? p.amount)
        }));
      return settlementsApi.reconcile(batchId, lines);
    },
    onSuccess: (r) => {
      if (r.mismatched > 0) toast.error(`${r.matched} matched, ${r.mismatched} mismatched`);
      else toast.success(`${r.matched} payout(s) reconciled`);
      refresh();
    },
    onError: (e) => toast.error(errMsg(e, "Reconciliation failed."))
  });

  if (isLoading || !data) return <Skeleton className="h-40" />;
  const { batch, payouts } = data;
  const canReconcile = payouts.some((p) => p.status === "Paid" || p.status === "Reconciled");

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 flex-wrap gap-2">
        <CardTitle className="text-base flex items-center gap-2">
          <span className="font-mono">{batch.reference}</span> <Badge variant="secondary">{batch.status}</Badge>
        </CardTitle>
        <div className="flex gap-2">
          <Button size="sm" variant="outline" onClick={() => settlementsApi.downloadBatch(batchId).catch(() => toast.error("Download failed."))}>
            <Download className="h-4 w-4 mr-1" /> CSV
          </Button>
          <Button size="sm" variant="outline" disabled={!canReconcile || reconcile.isPending} onClick={() => reconcile.mutate()}>
            <RefreshCw className="h-4 w-4 mr-1" /> Reconcile
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        <p className="text-xs text-muted-foreground mb-3">
          Cutoff {new Date(batch.cutoffAt).toLocaleString()}. To reconcile, enter the amount your gateway settled for each
          paid payout (blank = expected amount) and press Reconcile.
        </p>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-muted-foreground border-b">
                <th className="py-1 pr-2">Store</th><th className="pr-2 text-right">Amount</th><th className="pr-2">Status</th>
                <th className="pr-2">Gateway ref</th><th className="pr-2">Gateway amount</th><th />
              </tr>
            </thead>
            <tbody>
              {payouts.map((p) => (
                <tr key={p.id} className="border-b last:border-0 align-middle">
                  <td className="py-1.5 pr-2">{p.storeName || p.storeId.slice(0, 8)}</td>
                  <td className="pr-2 text-right font-mono">{p.amount.toFixed(2)} {p.currency}</td>
                  <td className="pr-2">
                    <Badge variant="secondary" className="flex items-center gap-1 w-fit">
                      {p.status === "Reconciled" && <CheckCircle2 className="h-3 w-3" />}{p.status}
                    </Badge>
                    {p.notes && <p className="text-[11px] text-amber-600">{p.notes}</p>}
                  </td>
                  <td className="pr-2">
                    {p.status === "Pending"
                      ? <Input className="h-8 w-40" placeholder="Transfer ref" value={refs[p.id] ?? ""}
                          onChange={(e) => setRefs((s) => ({ ...s, [p.id]: e.target.value }))} />
                      : <span className="font-mono text-xs">{p.gatewayReference ?? "-"}</span>}
                  </td>
                  <td className="pr-2">
                    {p.status === "Failed" ? "-" : (
                      <Input className="h-8 w-28" inputMode="decimal" placeholder={(p.gatewayAmount ?? p.amount).toFixed(2)}
                        value={gwAmounts[p.id] ?? ""}
                        onChange={(e) => setGwAmounts((s) => ({ ...s, [p.id]: e.target.value }))} />
                    )}
                  </td>
                  <td className="text-right whitespace-nowrap">
                    {p.status === "Pending" && (
                      <>
                        <Button size="sm" disabled={!(refs[p.id] ?? "").trim() || markPaid.isPending}
                          onClick={() => markPaid.mutate(p)}>Mark paid</Button>{" "}
                        <Button size="sm" variant="ghost" disabled={fail.isPending} onClick={() => fail.mutate(p)}>Fail</Button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}
