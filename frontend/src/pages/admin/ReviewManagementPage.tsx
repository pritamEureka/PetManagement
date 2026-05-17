import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Star } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { FilterBar } from "@/components/admin/FilterBar";
import { ModerationActionModal } from "@/components/security/ModerationActionModal";
import { api } from "@/api/client";
import type { ProductReview } from "@/api/marketplace";

/**
 * Admin review queue. Filters by product id (paste from URL or detail page).
 * Each row opens the standard moderation modal for Hide / Restore / Mark.
 */
export function AdminReviewManagementPage() {
  const [productId, setProductId] = useState("");
  const [target, setTarget] = useState<ProductReview | null>(null);

  const { data, isLoading, refetch } = useQuery({
    queryKey: ["admin-reviews", productId],
    queryFn: async () => {
      if (!productId) return [];
      const r = await api.get<{ items: ProductReview[]; total: number }>(
        `/v1/products/${productId}/reviews`, { params: { page: 1, pageSize: 200 } });
      return r.data.items;
    },
    enabled: false
  });

  const columns: Column<ProductReview>[] = [
    { key: "user", header: "Reviewer",
      render: (r) => (
        <div className="flex items-center gap-2">
          <div className="h-7 w-7 bg-muted rounded-full overflow-hidden">
            {r.userAvatarUrl && <img src={r.userAvatarUrl} className="h-full w-full object-cover" />}
          </div>
          <span className="text-sm font-medium">{r.userDisplayName}</span>
        </div>
      ) },
    { key: "rating", header: "Rating",
      render: (r) => (
        <span className="flex items-center text-amber-500">
          {Array.from({ length: r.rating }).map((_, i) => <Star key={i} className="h-3 w-3 fill-current" />)}
        </span>
      ) },
    { key: "comment", header: "Comment",
      render: (r) => <span className="text-sm text-muted-foreground line-clamp-2 max-w-md">{r.comment || "—"}</span> },
    { key: "created", header: "Posted",
      render: (r) => <span className="text-xs text-muted-foreground">{new Date(r.createdAt).toLocaleDateString()}</span> },
    { key: "actions", header: "", className: "w-32 text-right",
      render: (r) => (
        <Button size="sm" variant="outline" onClick={() => setTarget(r)}>Moderate</Button>
      ) }
  ];

  return (
    <div className="space-y-4">
      <PageHeader title="Review moderation" icon={Star}
        description="Look up a product's reviews and apply moderation actions." />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <FilterBar onReset={() => { setProductId(""); }}>
            <Input
              className="w-96"
              placeholder="Product ID (UUID)"
              value={productId}
              onChange={(e) => setProductId(e.target.value.trim())}
            />
            <Button onClick={() => refetch()} disabled={!productId}>Load reviews</Button>
            {data && <Badge variant="secondary">{data.length} reviews</Badge>}
          </FilterBar>

          <DataTable
            data={data ?? []}
            columns={columns}
            rowKey={(r) => r.id}
            loading={isLoading}
            empty={<EmptyState icon={Star} title="No reviews loaded"
                               description={productId ? "Click Load reviews." : "Paste a product ID above."} />}
          />
        </CardContent>
      </Card>

      {target && (
        <ModerationActionModal
          open={!!target}
          onOpenChange={(o) => !o && setTarget(null)}
          targetType="Review"
          targetId={target.id}
          defaultAction="Hide"
          onDone={() => refetch()}
        />
      )}
    </div>
  );
}
