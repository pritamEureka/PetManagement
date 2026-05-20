import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Heart, Trash2, ShoppingCart, ImageIcon } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { wishlistApi } from "@/api/marketplace";
import { useCartStore } from "@/store/cartStore";
import { toast } from "@/components/ui/sonner";

export function WishlistPage() {
  const qc = useQueryClient();
  const cartAdd = useCartStore((s) => s.add);

  const { data, isLoading } = useQuery({ queryKey: ["wishlist"], queryFn: () => wishlistApi.list() });

  const remove = useMutation({
    mutationFn: (productId: string) => wishlistApi.remove(productId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["wishlist"] }); }
  });

  return (
    <div className="max-w-5xl mx-auto space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><Heart className="h-5 w-5 text-primary" /> Wishlist</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
              {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-44" />)}
            </div>
          ) : !data || data.length === 0 ? (
            <div className="py-12 text-center space-y-2">
              <Heart className="h-10 w-10 mx-auto text-muted-foreground" />
              <p className="font-medium">Nothing saved yet</p>
              <p className="text-sm text-muted-foreground">Tap the heart on any product to save it for later.</p>
              <Button asChild className="mt-2"><Link to="/store">Browse the marketplace</Link></Button>
            </div>
          ) : (
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
              {data.map((w) => {
                const price = w.discountPrice ?? w.price;
                const outOfStock = w.stockQuantity <= 0;
                return (
                  <Card key={w.id} className="overflow-hidden">
                    <Link to={`/store/products/${w.productId}`} className="block">
                      <div className="aspect-square bg-muted grid place-items-center">
                        {w.imageUrl ? (
                          <img src={w.imageUrl} className="object-cover w-full h-full" />
                        ) : (
                          <ImageIcon className="h-8 w-8 text-muted-foreground" />
                        )}
                      </div>
                    </Link>
                    <CardContent className="p-3 space-y-1.5">
                      <Link to={`/store/products/${w.productId}`} className="font-medium text-sm hover:underline line-clamp-1">{w.productName}</Link>
                      <p className="text-xs text-muted-foreground line-clamp-1">{w.storeName}</p>
                      <div className="flex items-center justify-between pt-1">
                        <span className="font-semibold">${price.toFixed(2)}</span>
                        {outOfStock && <Badge variant="outline" className="text-xs">Out of stock</Badge>}
                      </div>
                      <div className="flex gap-2 pt-1">
                        <Button
                          size="sm" className="flex-1"
                          disabled={outOfStock}
                          onClick={async () => {
                            try {
                              await cartAdd({
                                id: w.productId, name: w.productName, sku: "",
                                price: w.price, discountPrice: w.discountPrice ?? null,
                                stockQuantity: w.stockQuantity, isActive: true, isFeatured: false,
                                ratingAverage: 0, ratingCount: 0,
                                storeId: w.storeId, storeName: w.storeName,
                                imageUrls: w.imageUrl ? [w.imageUrl] : [],
                                createdAt: w.createdAt
                              });
                              toast.success("Added to cart");
                            } catch (err: any) {
                              toast.error(err?.response?.data?.error?.message ?? "Could not add to cart.");
                            }
                          }}
                        >
                          <ShoppingCart className="h-3.5 w-3.5 mr-1" /> Add
                        </Button>
                        <Button size="icon" variant="ghost" onClick={() => remove.mutate(w.productId)}>
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
