import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { BellRing, Trash2, TrendingDown } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { discoveryApi, type PriceWatch } from "@/api/shopPlus";

const money = (n: number) => `$${n.toFixed(2)}`;

export function PriceWatchesPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ["price-watches"], queryFn: () => discoveryApi.priceWatches() });

  const remove = useMutation({
    mutationFn: (id: string) => discoveryApi.deletePriceWatch(id),
    onSuccess: () => { toast.success("Price watch removed"); qc.invalidateQueries({ queryKey: ["price-watches"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not remove price watch."),
  });

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2"><BellRing className="h-6 w-6 text-primary" /> Price watches</h1>
      <p className="text-sm text-muted-foreground">We notify you when a watched product drops in price or reaches your target.</p>

      {isLoading ? (
        <div className="space-y-2">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-20" />)}</div>
      ) : !data?.length ? (
        <Card>
          <CardContent className="py-12 text-center text-muted-foreground">
            No price watches yet. Open a product and choose "Watch price".
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-2">
          {data.map((w) => <WatchRow key={w.id} w={w} busy={remove.isPending} onRemove={() => remove.mutate(w.id)} />)}
        </div>
      )}
    </div>
  );
}

function WatchRow({ w, onRemove, busy }: { w: PriceWatch; onRemove: () => void; busy: boolean }) {
  const dropped = w.currentPrice < w.baselinePrice;
  const hitTarget = w.targetPrice != null && w.currentPrice <= w.targetPrice;
  return (
    <Card>
      <CardContent className="p-3 flex items-center gap-3">
        {w.imageUrl
          ? <img src={w.imageUrl} alt="" className="h-14 w-14 rounded object-cover" />
          : <div className="h-14 w-14 rounded bg-muted" />}
        <div className="min-w-0 flex-1">
          <Link to={`/store/products/${w.productId}`} className="font-medium hover:underline line-clamp-1">{w.productName}</Link>
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="font-semibold">{money(w.currentPrice)}</span>
            {dropped && <span className="text-xs text-muted-foreground line-through">{money(w.baselinePrice)}</span>}
            {dropped && <Badge variant="success"><TrendingDown className="h-3 w-3 mr-1" />Dropped</Badge>}
            {w.targetPrice != null && (
              <Badge variant={hitTarget ? "success" : "outline"}>Target {money(w.targetPrice)}</Badge>
            )}
            {!w.isActive && <Badge variant="muted">Paused</Badge>}
          </div>
        </div>
        <Button size="icon" variant="ghost" aria-label="Remove price watch" disabled={busy} onClick={onRemove}>
          <Trash2 className="h-4 w-4 text-destructive" />
        </Button>
      </CardContent>
    </Card>
  );
}
