import { useMutation, useQuery } from "@tanstack/react-query";
import { Package } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { marketplacePlusApi, type Bundle } from "@/api/marketplacePlus";
import { useCartStore } from "@/store/cartStore";

const money = (n: number) => `$${n.toFixed(2)}`;

/** A store bundle: component list, bundle price vs list price, one-click add (discount is applied at checkout). */
export function BundleCard({ bundle }: { bundle: Bundle }) {
  const hydrate = useCartStore((s) => s.hydrate);
  const add = useMutation({
    mutationFn: () => marketplacePlusApi.addBundleToCart(bundle.id, 1),
    onSuccess: async () => { await hydrate(); toast.success("Bundle added to cart"); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not add bundle."),
  });
  const soldOut = bundle.maxAvailable < 1 || !bundle.isActive;

  return (
    <Card>
      <CardContent className="p-3 space-y-2">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <div className="font-semibold flex items-center gap-1.5"><Package className="h-4 w-4 text-primary" />{bundle.name}</div>
            <div className="text-xs text-muted-foreground">{bundle.storeName}</div>
          </div>
          {bundle.savings > 0 && <Badge variant="success">Save {money(bundle.savings)}</Badge>}
        </div>
        <ul className="text-sm space-y-0.5">
          {bundle.items.map((i, idx) => (
            <li key={idx} className="flex justify-between gap-2">
              <span className="line-clamp-1">{i.quantity} x {i.productName}{i.variantLabel ? ` (${i.variantLabel})` : ""}</span>
              <span className="text-muted-foreground">{money(i.unitPrice * i.quantity)}</span>
            </li>
          ))}
        </ul>
        <div className="flex items-center justify-between pt-1">
          <div>
            <span className="font-bold">{money(bundle.bundlePrice)}</span>{" "}
            {bundle.savings > 0 && <span className="text-xs text-muted-foreground line-through">{money(bundle.listPrice)}</span>}
          </div>
          <Button size="sm" disabled={soldOut || add.isPending} onClick={() => add.mutate()}>
            {soldOut ? "Unavailable" : "Add bundle"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

/** Bundles that include a given product (shown on the product detail page). */
export function ProductBundles({ productId }: { productId: string }) {
  const { data } = useQuery({ queryKey: ["product-bundles", productId], queryFn: () => marketplacePlusApi.bundlesForProduct(productId) });
  if (!data?.length) return null;
  return (
    <div className="space-y-2">
      <h3 className="font-semibold text-sm">Frequently bought together</h3>
      {data.slice(0, 2).map((b) => <BundleCard key={b.id} bundle={b} />)}
    </div>
  );
}

/** Estimated delivery range for a store (history based, with a default fallback). */
export function DeliveryPromiseBadge({ storeId, city }: { storeId: string; city?: string }) {
  const { data } = useQuery({ queryKey: ["delivery-promise", storeId, city], queryFn: () => marketplacePlusApi.deliveryForStore(storeId, city) });
  if (!data) return null;
  const f = (d: string) => new Date(d).toLocaleDateString(undefined, { month: "short", day: "numeric" });
  return (
    <p className="text-xs text-muted-foreground" title={data.notes.join(" ")}>
      Estimated delivery {f(data.earliestDate)} - {f(data.latestDate)}
    </p>
  );
}

/** Estimated delivery window for the whole cart (latest store wins); pass the destination city when known. */
export function CartDeliveryPromise({ city }: { city?: string }) {
  const { data } = useQuery({
    queryKey: ["delivery-promise-cart", city],
    queryFn: () => marketplacePlusApi.deliveryForCart(city)
  });
  if (!data) return null;
  const f = (d: string) => new Date(d).toLocaleDateString(undefined, { month: "short", day: "numeric" });
  const notes = Array.from(new Set(data.perStore.flatMap((s) => s.notes)));
  return (
    <p className="text-xs text-muted-foreground" title={notes.join(" ")} data-testid="cart-delivery-promise">
      Estimated delivery {f(data.earliestDate)} - {f(data.latestDate)}
    </p>
  );
}
