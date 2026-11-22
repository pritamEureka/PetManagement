import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import type { FacetBucket, Facets, FacetSearchRequest } from "@/api/shopPlus";
import { cn } from "@/lib/utils";

interface Props {
  facets?: Facets;
  request: FacetSearchRequest;
  /** Merge a partial change into the request (pass undefined to clear a key). */
  onChange: (patch: Partial<FacetSearchRequest>) => void;
}

function Group({
  title, buckets, onPick,
}: { title: string; buckets: FacetBucket[]; onPick: (b: FacetBucket) => void }) {
  if (!buckets || buckets.length === 0) return null;
  return (
    <div className="space-y-1.5">
      <div className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</div>
      <div className="space-y-0.5">
        {buckets.map((b) => (
          <button
            key={b.key}
            type="button"
            onClick={() => onPick(b)}
            className={cn(
              "flex w-full items-center justify-between rounded px-2 py-1 text-left text-sm hover:bg-muted",
              b.selected && "bg-primary/10 font-medium",
            )}
          >
            <span className="truncate">{b.label}</span>
            <Badge variant="secondary" className="ml-2">{b.count}</Badge>
          </button>
        ))}
      </div>
    </div>
  );
}

/** Facet filters with live counts (category, brand, store, price range, rating, in-stock). */
export function FacetSidebar({ facets, request, onChange }: Props) {
  if (!facets) return null;
  const anyActive = !!(request.categoryId || request.brandId || request.storeId || request.minPrice != null ||
    request.maxPrice != null || request.minRating || request.inStockOnly);

  return (
    <Card>
      <CardContent className="space-y-4 pt-4">
        <div className="flex items-center justify-between">
          <span className="font-medium">Filters</span>
          {anyActive && (
            <Button size="sm" variant="ghost" onClick={() => onChange({
              categoryId: undefined, brandId: undefined, storeId: undefined, minPrice: undefined,
              maxPrice: undefined, minRating: undefined, inStockOnly: undefined,
            })}>Clear</Button>
          )}
        </div>
        <Group title="Category" buckets={facets.categories}
          onPick={(b) => onChange({ categoryId: b.selected ? undefined : b.key })} />
        <Group title="Brand" buckets={facets.brands}
          onPick={(b) => onChange({ brandId: b.selected ? undefined : b.key })} />
        <Group title="Store" buckets={facets.stores}
          onPick={(b) => onChange({ storeId: b.selected ? undefined : b.key })} />
        <Group title="Price" buckets={facets.priceRanges}
          onPick={(b) => {
            const [lo, hi] = b.key.split("-").map(Number);
            onChange(b.selected ? { minPrice: undefined, maxPrice: undefined } : { minPrice: lo, maxPrice: hi });
          }} />
        <Group title="Rating" buckets={facets.ratings}
          onPick={(b) => onChange({ minRating: b.selected ? undefined : Number(b.key) })} />
        <Group title="Availability" buckets={facets.stock}
          onPick={(b) => onChange({ inStockOnly: b.selected ? undefined : true })} />
      </CardContent>
    </Card>
  );
}
