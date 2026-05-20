import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Eye, EyeOff, Star, Search, ShoppingBag } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { productsV2Api, type ProductSummary } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";

export function ProductModerationPage() {
  const qc = useQueryClient();
  const [search, setSearch] = useState("");
  const [deleting, setDeleting] = useState<ProductSummary | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["admin-products", { search }],
    queryFn: () => productsV2Api.adminAll({ search: search || undefined, page: 1, pageSize: 50 })
  });

  const feature = useMutation({
    mutationFn: ({ id, value }: { id: string; value: boolean }) => productsV2Api.feature(id, value),
    onSuccess: () => { toast.success("Updated"); qc.invalidateQueries({ queryKey: ["admin-products"] }); }
  });
  const publish = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => productsV2Api.publish(id, active),
    onSuccess: () => { toast.success("Updated"); qc.invalidateQueries({ queryKey: ["admin-products"] }); }
  });
  const remove = useMutation({
    mutationFn: (id: string) => productsV2Api.remove(id),
    onSuccess: () => { toast.success("Deleted"); qc.invalidateQueries({ queryKey: ["admin-products"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Delete failed.")
  });

  async function confirmDelete() {
    if (!deleting) return;
    await remove.mutateAsync(deleting.id);
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2">
        <ShoppingBag className="h-6 w-6 text-primary" /> Product moderation
      </h1>

      <div className="relative max-w-md">
        <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input className="pl-8" placeholder="Search by name or SKU..." value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>

      {isLoading ? <Skeleton className="h-64" />
        : !data?.items.length ? <Card><CardContent className="py-12 text-center text-muted-foreground">No products.</CardContent></Card>
        : (
          <Card>
            <CardContent className="p-0">
              <table className="w-full text-sm">
                <thead className="border-b text-muted-foreground">
                  <tr className="text-left">
                    <th className="p-3">Product</th>
                    <th className="p-3">Store</th>
                    <th className="p-3">Price</th>
                    <th className="p-3">Rating</th>
                    <th className="p-3">Status</th>
                    <th className="p-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {data.items.map((p) => (
                    <tr key={p.id}>
                      <td className="p-3">
                        <div className="flex items-center gap-2">
                          <div className="h-10 w-10 bg-muted rounded overflow-hidden flex-shrink-0">
                            {p.imageUrls[0] && <img src={p.imageUrls[0]} className="object-cover w-full h-full" />}
                          </div>
                          <Link to={`/store/products/${p.id}`} className="font-medium hover:underline">{p.name}</Link>
                        </div>
                      </td>
                      <td className="p-3 text-muted-foreground">{p.storeName}</td>
                      <td className="p-3">${(p.discountPrice ?? p.price).toFixed(2)}</td>
                      <td className="p-3 text-xs">
                        <span className="flex items-center gap-1 text-amber-500">
                          <Star className="h-3 w-3 fill-current" /> {p.ratingAverage.toFixed(1)} ({p.ratingCount})
                        </span>
                      </td>
                      <td className="p-3">
                        {p.isActive ? <Badge variant={statusBadgeVariant("Live")}>Live</Badge> : <Badge variant={statusBadgeVariant("Hidden")}>Hidden</Badge>}
                        {p.isFeatured && <Badge variant="secondary" className="ml-1">Featured</Badge>}
                      </td>
                      <td className="p-3">
                        <div className="flex justify-end gap-1">
                          <Button size="sm" variant="outline"
                            onClick={() => feature.mutate({ id: p.id, value: !p.isFeatured })}>
                            <Star className="h-3 w-3 mr-1" /> {p.isFeatured ? "Unfeature" : "Feature"}
                          </Button>
                          <Button size="icon" variant="ghost"
                            onClick={() => publish.mutate({ id: p.id, active: !p.isActive })}
                            title={p.isActive ? "Hide" : "Show"}>
                            {p.isActive ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                          </Button>
                          <Button size="sm" variant="destructive"
                            onClick={() => setDeleting(p)}>
                            Remove
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </CardContent>
          </Card>
        )}

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={`Remove ${deleting?.name ?? "product"}?`}
        description="This permanently removes the product from the marketplace."
        confirmLabel="Remove"
        destructive
        onConfirm={confirmDelete}
      />
    </div>
  );
}
