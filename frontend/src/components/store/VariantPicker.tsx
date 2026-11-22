import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ShoppingCart } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/components/ui/sonner";
import { shopPlusApi, type Variant } from "@/api/shopPlus";
import { useCartStore } from "@/store/cartStore";
import { cn } from "@/lib/utils";

interface Props {
  productId: string;
  quantity?: number;
  onSelect?: (variant: Variant | null) => void;
}

function attrLabel(v: Variant) {
  const parts = Object.values(v.attributes ?? {});
  return parts.length ? parts.join(" / ") : v.sku;
}

/**
 * Renders the variant chips for a product (size/color/...) and an "Add selected variant" button.
 * Renders nothing when the product has no variants.
 */
export function VariantPicker({ productId, quantity = 1, onSelect }: Props) {
  const qc = useQueryClient();
  const hydrateCart = useCartStore((s) => s.hydrate);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const { data: variants } = useQuery({
    queryKey: ["product-variants", productId],
    queryFn: () => shopPlusApi.variants(productId),
  });

  const selected = variants?.find((v) => v.id === selectedId) ?? null;

  useEffect(() => { onSelect?.(selected); }, [selected, onSelect]);

  const add = useMutation({
    mutationFn: () => shopPlusApi.addVariantToCart(selectedId!, quantity),
    onSuccess: () => {
      toast.success("Added to cart");
      void hydrateCart();
      qc.invalidateQueries({ queryKey: ["cart"] });
      qc.invalidateQueries({ queryKey: ["product-variants", productId] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not add this variant."),
  });

  if (!variants || variants.length === 0) return null;

  return (
    <div className="space-y-2">
      <div className="text-sm text-muted-foreground">Options</div>
      <div className="flex flex-wrap gap-2">
        {variants.map((v) => {
          const out = v.available <= 0;
          return (
            <button
              key={v.id}
              type="button"
              disabled={out}
              onClick={() => setSelectedId(v.id)}
              className={cn(
                "rounded-md border px-3 py-1.5 text-sm transition-colors",
                selectedId === v.id ? "border-primary bg-primary/10" : "hover:bg-muted",
                out && "opacity-50 line-through cursor-not-allowed",
              )}
              title={out ? "Sold out" : `${v.available} available`}
            >
              {attrLabel(v)}
            </button>
          );
        })}
      </div>
      {selected && (
        <div className="flex items-center gap-3">
          <span className="text-lg font-semibold">${selected.effectivePrice.toFixed(2)}</span>
          <Badge variant="outline">{selected.sku}</Badge>
          <span className="text-xs text-muted-foreground">{selected.available} available</span>
        </div>
      )}
      <Button
        variant="secondary"
        disabled={!selected || add.isPending || (selected?.available ?? 0) < quantity}
        onClick={() => add.mutate()}
      >
        <ShoppingCart className="h-4 w-4 mr-2" />
        {selected ? "Add selected variant" : "Select an option"}
      </Button>
    </div>
  );
}
