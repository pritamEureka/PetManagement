import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Plus, Edit, Trash2, Search, Eye, EyeOff, Star } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { productsV2Api, type ProductSummary } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";

export function ProductManagementPage() {
  const qc = useQueryClient();
  const [search, setSearch] = useState("");
  const [deleting, setDeleting] = useState<ProductSummary | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["my-products", { search }],
    queryFn: () => productsV2Api.mine({ search: search || undefined, page: 1, pageSize: 100 })
  });

  const togglePublish = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => productsV2Api.publish(id, active),
    onSuccess: () => { toast.success("Updated"); qc.invalidateQueries({ queryKey: ["my-products"] }); }
  });

  const remove = useMutation({
    mutationFn: (id: string) => productsV2Api.remove(id),
    onSuccess: () => { toast.success("Deleted"); qc.invalidateQueries({ queryKey: ["my-products"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Delete failed.")
  });

  async function confirmDelete() {
    if (!deleting) return;
    await remove.mutateAsync(deleting.id);
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <h1 className="text-2xl font-bold">Products</h1>
        <Button asChild><Link to="/dashboard/store/products/new"><Plus className="h-4 w-4 mr-1" /> Add product</Link></Button>
      </div>

      <div className="relative max-w-md">
        <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
        <Input className="pl-8" placeholder="Search by name or SKU..." value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>

      {isLoading ? (
        <div className="space-y-2">{[...Array(5)].map((_, i) => <Skeleton key={i} className="h-16" />)}</div>
      ) : !data?.items.length ? (
        <Card><CardContent className="py-12 text-center text-muted-foreground">No products yet.</CardContent></Card>
      ) : (
        <Card>
          <CardContent className="p-0 overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b text-muted-foreground">
                <tr className="text-left">
                  <th className="p-3">Product</th>
                  <th className="p-3 hidden sm:table-cell">SKU</th>
                  <th className="p-3">Price</th>
                  <th className="p-3 hidden sm:table-cell">Stock</th>
                  <th className="p-3 hidden md:table-cell">Rating</th>
                  <th className="p-3 hidden md:table-cell">Status</th>
                  <th className="p-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {data.items.map((p) => {
                  const price = p.discountPrice ?? p.price;
                  return (
                    <tr key={p.id}>
                      <td className="p-3">
                        <div className="flex items-center gap-2">
                          <div className="h-10 w-10 bg-muted rounded overflow-hidden flex-shrink-0">
                            {p.imageUrls[0] && <img src={p.imageUrls[0]} className="object-cover w-full h-full" />}
                          </div>
                          <Link to={`/dashboard/store/products/${p.id}`} className="font-medium hover:underline">{p.name}</Link>
                        </div>
                      </td>
                      <td className="p-3 font-mono text-xs hidden sm:table-cell">{p.sku}</td>
                      <td className="p-3">
                        ${price.toFixed(2)}
                        {p.discountPrice != null && <span className="block text-xs text-muted-foreground line-through">${p.price.toFixed(2)}</span>}
                      </td>
                      <td className="p-3 hidden sm:table-cell">
                        <Badge variant={p.stockQuantity <= 0 ? "destructive" : p.stockQuantity < 5 ? "outline" : "secondary"}>
                          {p.stockQuantity}
                        </Badge>
                      </td>
                      <td className="p-3 text-xs hidden md:table-cell">
                        <span className="flex items-center gap-1 text-amber-500">
                          <Star className="h-3 w-3 fill-current" /> {p.ratingAverage.toFixed(1)} ({p.ratingCount})
                        </span>
                      </td>
                      <td className="p-3 hidden md:table-cell">
                        {p.isActive
                          ? <Badge>Live</Badge>
                          : <Badge variant="outline">Hidden</Badge>}
                        {p.isFeatured && <Badge variant="secondary" className="ml-1">Featured</Badge>}
                      </td>
                      <td className="p-3">
                        <div className="flex justify-end gap-1">
                          <Button size="icon" variant="ghost" asChild title="Edit">
                            <Link to={`/dashboard/store/products/${p.id}`}><Edit className="h-4 w-4" /></Link>
                          </Button>
                          <Button size="icon" variant="ghost"
                            title={p.isActive ? "Unpublish" : "Publish"}
                            onClick={() => togglePublish.mutate({ id: p.id, active: !p.isActive })}>
                            {p.isActive ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                          </Button>
                          <Button size="icon" variant="ghost" title="Delete"
                            onClick={() => setDeleting(p)}>
                            <Trash2 className="h-4 w-4 text-destructive" />
                          </Button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={`Delete ${deleting?.name ?? "product"}?`}
        description="This permanently removes the product from your catalog."
        confirmLabel="Delete"
        destructive
        onConfirm={confirmDelete}
      />
    </div>
  );
}
