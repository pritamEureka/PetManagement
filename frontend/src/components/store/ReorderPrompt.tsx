import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { RefreshCw, X } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { marketplacePlusApi } from "@/api/marketplacePlus";
import { cartApi } from "@/api/marketplace";
import { useCartStore } from "@/store/cartStore";

/** Replenishment prompt for the dashboard / orders list: products the buyer is about to run out of. */
export function ReorderPrompt() {
  const qc = useQueryClient();
  const hydrate = useCartStore((s) => s.hydrate);
  const { data } = useQuery({ queryKey: ["replenishment"], queryFn: () => marketplacePlusApi.replenishment() });

  const dismiss = useMutation({
    mutationFn: (id: string) => marketplacePlusApi.dismissReplenishment(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["replenishment"] }),
  });
  const reorder = useMutation({
    mutationFn: async (r: { id: string; productId: string }) => { await cartApi.add(r.productId, 1); await hydrate(); },
    onSuccess: () => toast.success("Added to cart"),
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not add to cart."),
  });

  if (!data?.length) return null;
  return (
    <Card>
      <CardContent className="p-3 space-y-2">
        <div className="font-semibold flex items-center gap-2"><RefreshCw className="h-4 w-4 text-primary" /> Running low? Reorder</div>
        {data.map((r) => (
          <div key={r.id} className="flex items-center gap-3">
            {r.imageUrl ? <img src={r.imageUrl} alt="" className="h-10 w-10 rounded object-cover" /> : <div className="h-10 w-10 rounded bg-muted" />}
            <div className="min-w-0 flex-1">
              <Link to={`/store/products/${r.productId}`} className="text-sm font-medium hover:underline line-clamp-1">{r.productName}</Link>
              <div className="text-xs text-muted-foreground">
                {r.daysLeft <= 0 ? "You may have run out" : `About ${r.daysLeft} day(s) left`} - ${r.price.toFixed(2)}
              </div>
            </div>
            <Button size="sm" disabled={!r.inStock || reorder.isPending} onClick={() => reorder.mutate(r)}>
              {r.inStock ? "Reorder" : "Out of stock"}
            </Button>
            <Button size="icon" variant="ghost" aria-label="Dismiss" onClick={() => dismiss.mutate(r.id)}><X className="h-4 w-4" /></Button>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
