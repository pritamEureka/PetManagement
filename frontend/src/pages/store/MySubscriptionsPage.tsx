import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Pause, Play, Repeat, SkipForward, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/sonner";
import { shopPlusApi, type Subscription } from "@/api/shopPlus";

const INTERVALS = [14, 30, 45, 60, 90];

export function MySubscriptionsPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ["my-subscriptions"], queryFn: () => shopPlusApi.subscriptions() });

  const refresh = () => qc.invalidateQueries({ queryKey: ["my-subscriptions"] });
  const onError = (e: any) => toast.error(e?.response?.data?.error?.message ?? "Request failed.");

  const pause = useMutation({ mutationFn: (id: string) => shopPlusApi.pauseSubscription(id), onSuccess: refresh, onError });
  const resume = useMutation({ mutationFn: (id: string) => shopPlusApi.resumeSubscription(id), onSuccess: refresh, onError });
  const skip = useMutation({
    mutationFn: (id: string) => shopPlusApi.skipSubscription(id),
    onSuccess: () => { toast.success("Next delivery skipped"); refresh(); }, onError,
  });
  const cancel = useMutation({
    mutationFn: (id: string) => shopPlusApi.cancelSubscription(id),
    onSuccess: () => { toast.success("Subscription cancelled"); refresh(); }, onError,
  });
  const update = useMutation({
    mutationFn: (v: { id: string; quantity?: number; intervalDays?: number }) =>
      shopPlusApi.updateSubscription(v.id, { quantity: v.quantity, intervalDays: v.intervalDays }),
    onSuccess: refresh, onError,
  });

  return (
    <div className="space-y-4 max-w-4xl mx-auto">
      <h1 className="text-2xl font-bold flex items-center gap-2">
        <Repeat className="h-6 w-6 text-primary" /> My subscriptions
      </h1>

      {isLoading ? (
        <div className="space-y-3">{[...Array(2)].map((_, i) => <Skeleton key={i} className="h-32" />)}</div>
      ) : !data || data.length === 0 ? (
        <Card>
          <CardContent className="py-16 text-center text-muted-foreground">
            No subscriptions yet. Open a product and choose "Subscribe &amp; save".{" "}
            <Link to="/store" className="underline">Browse the store</Link>
          </CardContent>
        </Card>
      ) : (
        data.map((s: Subscription) => (
          <Card key={s.id}>
            <CardContent className="flex flex-col gap-3 pt-4 sm:flex-row sm:items-center">
              <div className="h-16 w-16 shrink-0 overflow-hidden rounded bg-muted">
                {s.imageUrl && <img src={s.imageUrl} alt={s.productName} className="h-full w-full object-cover" />}
              </div>
              <div className="min-w-0 flex-1 space-y-1">
                <div className="flex flex-wrap items-center gap-2">
                  <Link to={`/store/products/${s.productId}`} className="font-medium hover:underline">{s.productName}</Link>
                  {s.variantSku && <Badge variant="outline">{s.variantSku}</Badge>}
                  <Badge variant={s.status === "Active" ? "default" : "secondary"}>{s.status}</Badge>
                </div>
                <div className="text-sm text-muted-foreground">
                  ${s.discountedUnitPrice.toFixed(2)} each (save {s.discountPercent}%) -{" "}
                  {s.status === "Active"
                    ? `next on ${new Date(s.nextRunAt).toLocaleDateString()}`
                    : "not scheduled"}
                </div>
                {s.lastError && (
                  <div className="flex items-center gap-1 text-sm text-destructive">
                    <AlertTriangle className="h-4 w-4" /> {s.lastError}
                    {s.failureCount > 0 && ` (attempt ${s.failureCount})`}
                  </div>
                )}
                <div className="flex flex-wrap gap-2 pt-1">
                  <Select value={String(s.quantity)}
                    onValueChange={(v) => update.mutate({ id: s.id, quantity: Number(v) })}>
                    <SelectTrigger className="w-24"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {Array.from({ length: 10 }, (_, i) => i + 1).map((n) =>
                        <SelectItem key={n} value={String(n)}>Qty {n}</SelectItem>)}
                    </SelectContent>
                  </Select>
                  <Select value={String(s.intervalDays)}
                    onValueChange={(v) => update.mutate({ id: s.id, intervalDays: Number(v) })}>
                    <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {[...new Set([...INTERVALS, s.intervalDays])].sort((a, b) => a - b).map((d) =>
                        <SelectItem key={d} value={String(d)}>Every {d} days</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                {s.status === "Active" ? (
                  <>
                    <Button size="sm" variant="outline" onClick={() => skip.mutate(s.id)}>
                      <SkipForward className="h-4 w-4 mr-1" /> Skip next
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => pause.mutate(s.id)}>
                      <Pause className="h-4 w-4 mr-1" /> Pause
                    </Button>
                  </>
                ) : (
                  <Button size="sm" onClick={() => resume.mutate(s.id)}>
                    <Play className="h-4 w-4 mr-1" /> Resume
                  </Button>
                )}
                <Button size="sm" variant="ghost" onClick={() => {
                  if (window.confirm("Cancel this subscription?")) cancel.mutate(s.id);
                }}>
                  <X className="h-4 w-4 mr-1" /> Cancel
                </Button>
              </div>
            </CardContent>
          </Card>
        ))
      )}
    </div>
  );
}
