import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Download, Wallet } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { settlementsApi } from "@/api/commercePlus";
import { toast } from "@/components/ui/sonner";

function money(n: number, currency: string) {
  return `${n < 0 ? "-" : ""}${Math.abs(n).toFixed(2)} ${currency}`;
}

/** Store-side balance, hold status, recent ledger entries, payouts and CSV statement. */
export function StoreLedgerPanel() {
  const [page, setPage] = useState(1);
  const [downloading, setDownloading] = useState(false);

  const { data: ledger, isLoading } = useQuery({
    queryKey: ["store-ledger", page], queryFn: () => settlementsApi.mine(page, 10), retry: false
  });
  const { data: payouts } = useQuery({
    queryKey: ["store-payouts"], queryFn: () => settlementsApi.myPayouts(), retry: false
  });

  async function download() {
    setDownloading(true);
    try { await settlementsApi.downloadMyStatement(); }
    catch { toast.error("Could not download the statement."); }
    finally { setDownloading(false); }
  }

  if (isLoading) return <Skeleton className="h-48" />;
  if (!ledger) return null;
  const cur = ledger.currency;
  const totalPages = Math.max(1, Math.ceil(ledger.entries.total / ledger.entries.pageSize));

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base flex items-center gap-2"><Wallet className="h-4 w-4" /> Earnings and payouts</CardTitle>
        <Button size="sm" variant="outline" onClick={download} disabled={downloading}>
          <Download className="h-4 w-4 mr-1" /> {downloading ? "Preparing..." : "Statement CSV"}
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
          <Tile label="Available to pay out" value={money(ledger.availableBalance, cur)} />
          <Tile label={`On hold (${ledger.holdDays}d)`} value={money(ledger.onHoldBalance, cur)} />
          <Tile label="Paid out" value={money(ledger.paidOut, cur)} />
          <Tile label="Net balance" value={money(ledger.totalBalance, cur)} />
        </div>

        <div>
          <p className="text-sm font-medium mb-2">Ledger</p>
          {ledger.entries.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No ledger entries yet. Earnings appear once orders are paid or delivered.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-xs text-muted-foreground border-b">
                    <th className="py-1 pr-2">Date</th><th className="pr-2">Type</th>
                    <th className="pr-2">Note</th><th className="pr-2">Payable from</th><th className="text-right">Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {ledger.entries.items.map((e) => (
                    <tr key={e.id} className="border-b last:border-0">
                      <td className="py-1.5 pr-2 whitespace-nowrap">{new Date(e.createdAt).toLocaleDateString()}</td>
                      <td className="pr-2"><Badge variant="outline">{e.type}</Badge></td>
                      <td className="pr-2 text-muted-foreground">{e.memo ?? ""}</td>
                      <td className="pr-2 whitespace-nowrap text-muted-foreground">
                        {e.type === "Payout" ? "-" : new Date(e.availableAt).toLocaleDateString()}
                      </td>
                      <td className={`text-right font-mono ${e.amount < 0 ? "text-red-600" : "text-emerald-700"}`}>
                        {e.amount.toFixed(2)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {totalPages > 1 && (
            <div className="flex items-center justify-end gap-2 pt-2 text-xs">
              <Button size="sm" variant="ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</Button>
              <span>{page} / {totalPages}</span>
              <Button size="sm" variant="ghost" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </div>
          )}
        </div>

        {payouts && payouts.length > 0 && (
          <div>
            <p className="text-sm font-medium mb-2">Recent payouts</p>
            <div className="space-y-1 text-sm">
              {payouts.slice(0, 5).map((p) => (
                <div key={p.id} className="flex items-center justify-between gap-2">
                  <span className="text-muted-foreground truncate">{p.batchReference}{p.gatewayReference ? ` - ${p.gatewayReference}` : ""}</span>
                  <span className="flex items-center gap-2">
                    <Badge variant="secondary">{p.status}</Badge>
                    <span className="font-mono">{money(p.amount, p.currency)}</span>
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function Tile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-lg font-semibold font-mono">{value}</p>
    </div>
  );
}
