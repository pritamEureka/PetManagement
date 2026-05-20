import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { History, CheckCircle2, AlertCircle, Package } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { deliveriesApi } from "@/api/marketplace";

export function DeliveryHistoryPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useQuery({
    queryKey: ["deliveries-mine-history", page],
    queryFn: () => deliveriesApi.mineHistory(page, 30)
  });

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div className="max-w-5xl mx-auto space-y-3">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><History className="h-5 w-5 text-primary" /> Delivery history</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-2">{Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-14" />)}</div>
          ) : !data || data.items.length === 0 ? (
            <p className="py-8 text-sm text-muted-foreground text-center">No completed deliveries yet.</p>
          ) : (
            <div className="space-y-2">
              {data.items.map((a) => {
                const ok = a.status === "Delivered";
                return (
                  <div key={a.id} className="flex items-center justify-between border rounded-md p-3 text-sm">
                    <div className="flex items-center gap-3 min-w-0">
                      {ok
                        ? <CheckCircle2 className="h-5 w-5 text-emerald-500 shrink-0" />
                        : <AlertCircle className="h-5 w-5 text-destructive shrink-0" />}
                      <div className="min-w-0">
                        <p className="font-mono text-xs">{a.orderNumber}</p>
                        <p className="text-xs text-muted-foreground truncate flex items-center gap-1">
                          <Package className="h-3 w-3" /> {a.customerName} · ${a.orderTotal.toFixed(2)}
                        </p>
                      </div>
                    </div>
                    <div className="text-right text-xs text-muted-foreground space-y-0.5">
                      <Badge variant={statusBadgeVariant(a.status)}>{a.status}</Badge>
                      <p>{ok ? `Delivered ${new Date(a.deliveredAt!).toLocaleDateString()}`
                            : `Failed ${new Date(a.failedAt!).toLocaleDateString()}`}</p>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {totalPages > 1 && (
            <div className="flex items-center justify-between text-sm mt-3">
              <span className="text-muted-foreground">Page {page} of {totalPages}</span>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
