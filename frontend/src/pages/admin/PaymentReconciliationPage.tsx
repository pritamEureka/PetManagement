import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Scale } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/components/ui/sonner";
import { paymentReconciliationApi, type ReconciliationReport } from "@/api/reliability";

const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString().slice(0, 10);

function fmt(n: number | null) {
  return n == null ? "-" : n.toFixed(2);
}

export function PaymentReconciliationPage() {
  const [from, setFrom] = useState(day(-7));
  const [to, setTo] = useState(day(0));
  const [report, setReport] = useState<ReconciliationReport | null>(null);

  const range = () => ({
    from: new Date(`${from}T00:00:00Z`).toISOString(),
    to: new Date(`${to}T23:59:59Z`).toISOString(),
  });

  const run = useMutation({
    mutationFn: (record: boolean) => {
      const r = range();
      return record
        ? paymentReconciliationApi.runAndRecord(r.from, r.to)
        : paymentReconciliationApi.run(r.from, r.to);
    },
    onSuccess: (data, record) => {
      setReport(data);
      toast.success(record ? "Reconciled and recorded critical items." : "Reconciliation complete.");
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Reconciliation failed."),
  });

  const sevVariant = (s: string) => (s === "critical" ? "destructive" : s === "warning" ? "secondary" : "outline");

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2">
        <Scale className="h-6 w-6" />
        <h1 className="text-2xl font-semibold">Payment reconciliation</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Range</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap items-end gap-4">
          <div className="space-y-1">
            <Label htmlFor="rec-from">From</Label>
            <Input id="rec-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="rec-to">To</Label>
            <Input id="rec-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
          <Button onClick={() => run.mutate(false)} disabled={run.isPending}>
            {run.isPending ? "Running..." : "Run reconciliation"}
          </Button>
          <Button variant="outline" onClick={() => run.mutate(true)} disabled={run.isPending}>
            Run and record critical items
          </Button>
        </CardContent>
      </Card>

      {report && (
        <Card>
          <CardHeader>
            <CardTitle className="flex flex-wrap items-center gap-3">
              Result
              <Badge variant="outline">{report.paymentsChecked} checked</Badge>
              <Badge variant="outline">{report.matched} matched</Badge>
              <Badge variant={report.discrepancies ? "destructive" : "outline"}>
                {report.discrepancies} discrepancies
              </Badge>
              {!report.gatewayQuerySupported && (
                <Badge variant="secondary">Gateway query unavailable - local checks only</Badge>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            {report.items.length === 0 ? (
              <p className="text-sm text-muted-foreground">No discrepancies found.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="py-2 pr-3">Severity</th>
                      <th className="py-2 pr-3">Kind</th>
                      <th className="py-2 pr-3">Order</th>
                      <th className="py-2 pr-3">Local</th>
                      <th className="py-2 pr-3">Gateway</th>
                      <th className="py-2">Detail</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.items.map((i, idx) => (
                      <tr key={`${i.orderId}-${i.kind}-${idx}`} className="border-b align-top">
                        <td className="py-2 pr-3">
                          <Badge variant={sevVariant(i.severity) as any}>{i.severity}</Badge>
                        </td>
                        <td className="py-2 pr-3 font-medium">{i.kind}</td>
                        <td className="py-2 pr-3 font-mono text-xs">{i.orderId.slice(0, 8)}</td>
                        <td className="py-2 pr-3">
                          {fmt(i.localAmount)} {i.localStatus ?? ""}
                        </td>
                        <td className="py-2 pr-3">
                          {fmt(i.gatewayAmount)} {i.gatewayStatus ?? ""}
                        </td>
                        <td className="py-2">{i.detail}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

export default PaymentReconciliationPage;
