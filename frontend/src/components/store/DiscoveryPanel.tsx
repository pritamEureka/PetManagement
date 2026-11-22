import { Sparkles, SlidersHorizontal } from "lucide-react";
import type { Facets, FacetSearchRequest } from "@/api/shopPlus";
import { FacetSidebar } from "./FacetSidebar";
import { SavedSearches } from "./SavedSearches";

interface Props {
  facets?: Facets;
  request: FacetSearchRequest;
  onChange: (patch: Partial<FacetSearchRequest>) => void;
  /** Replace the whole request (used when applying a saved search). */
  onReplace: (query: FacetSearchRequest) => void;
  didYouMean?: string | null;
}

/** Collapsible facet filters + saved searches + "did you mean" hint for MarketplacePage. */
export function DiscoveryPanel({ facets, request, onChange, onReplace, didYouMean }: Props) {
  return (
    <div className="space-y-2">
      {didYouMean && (
        <div className="flex items-center gap-2 rounded-md bg-primary/10 px-3 py-2 text-sm">
          <Sparkles className="h-4 w-4 text-primary" />
          Showing results for <button type="button" className="font-semibold underline"
            onClick={() => onChange({ q: didYouMean })}>{didYouMean}</button>
          <span className="text-muted-foreground">(no exact matches for "{request.q}")</span>
        </div>
      )}
      <details className="rounded-md border">
        <summary className="flex cursor-pointer items-center gap-2 px-3 py-2 text-sm font-medium">
          <SlidersHorizontal className="h-4 w-4" /> Refine &amp; saved searches
        </summary>
        <div className="grid gap-4 p-3 md:grid-cols-2">
          <FacetSidebar facets={facets} request={request} onChange={onChange} />
          <SavedSearches current={request} onApply={onReplace} />
        </div>
      </details>
    </div>
  );
}
