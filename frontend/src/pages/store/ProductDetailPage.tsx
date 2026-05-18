import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ShoppingBag, Star, Minus, Plus, ShoppingCart, Truck, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { productsV2Api } from "@/api/marketplace";
import { useCartStore } from "@/store/cartStore";
import { toast } from "@/components/ui/sonner";
import { EmptyState } from "@/components/common/EmptyState";

export function ProductDetailPage() {
  const { id = "" } = useParams();
  const qc = useQueryClient();
  const { data: product, isLoading } = useQuery({
    queryKey: ["product", id],
    queryFn: () => productsV2Api.get(id),
    enabled: !!id
  });
  const { data: reviews } = useQuery({
    queryKey: ["product-reviews", id],
    queryFn: () => productsV2Api.listReviews(id),
    enabled: !!id
  });

  const add = useCartStore((s) => s.add);
  const [qty, setQty] = useState(1);
  const [activeImage, setActiveImage] = useState(0);
  const [reviewRating, setReviewRating] = useState(5);
  const [reviewComment, setReviewComment] = useState("");

  const createReview = useMutation({
    mutationFn: () => productsV2Api.createReview(id, { rating: reviewRating, comment: reviewComment || undefined }),
    onSuccess: () => {
      toast.success("Review submitted");
      setReviewComment("");
      qc.invalidateQueries({ queryKey: ["product-reviews", id] });
      qc.invalidateQueries({ queryKey: ["product", id] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not submit review.")
  });

  if (isLoading) {
    return (
      <div className="max-w-5xl mx-auto grid md:grid-cols-2 gap-6">
        <Skeleton className="aspect-square rounded-lg" />
        <div className="space-y-3"><Skeleton className="h-8" /><Skeleton className="h-4 w-1/2" /><Skeleton className="h-10" /></div>
      </div>
    );
  }
  if (!product) {
    return (
      <div className="max-w-3xl mx-auto space-y-3">
        <Button variant="ghost" size="sm" asChild><Link to="/store"><ArrowLeft className="h-4 w-4 mr-1" /> Back</Link></Button>
        <EmptyState title="Product not found" description="It may have been removed by the seller." />
      </div>
    );
  }

  const price = product.discountPrice ?? product.price;
  const discounted = product.discountPrice != null && product.discountPrice < product.price;
  const outOfStock = product.stockQuantity <= 0;

  function onAdd() {
    if (!product) return;
    add(product, qty);
    toast.success(`${qty}× ${product.name} added to cart`);
  }

  return (
    <div className="max-w-5xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild><Link to="/store"><ArrowLeft className="h-4 w-4 mr-1" /> Back to store</Link></Button>

      <div className="grid md:grid-cols-2 gap-6">
        <div className="space-y-2">
          <div className="aspect-square bg-muted rounded-lg overflow-hidden">
            {product.imageUrls[activeImage]
              ? <img src={product.imageUrls[activeImage]} className="object-cover w-full h-full" />
              : <div className="flex items-center justify-center h-full text-muted-foreground"><ShoppingBag className="h-16 w-16" /></div>}
          </div>
          {product.imageUrls.length > 1 && (
            <div className="grid grid-cols-5 gap-2">
              {product.imageUrls.map((src, i) => (
                <button
                  key={i}
                  onClick={() => setActiveImage(i)}
                  className={`aspect-square rounded-md overflow-hidden border ${i === activeImage ? "ring-2 ring-primary" : ""}`}
                >
                  <img src={src} className="object-cover w-full h-full" />
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="space-y-4">
          <div>
            <Link to={`/store?storeId=${product.storeId}`} className="text-xs text-muted-foreground hover:underline">
              {product.storeName}
            </Link>
            <h1 className="text-2xl font-bold">{product.name}</h1>
            <div className="flex items-center gap-3 mt-1 text-sm">
              <span className="flex items-center gap-1 text-amber-500">
                <Star className="h-4 w-4 fill-current" /> {product.ratingAverage.toFixed(1)}
                <span className="text-muted-foreground">({product.ratingCount})</span>
              </span>
              {product.isFeatured && <Badge variant="secondary">Featured</Badge>}
              {outOfStock && <Badge variant="destructive">Sold out</Badge>}
              {product.categoryName && <Badge variant="outline">{product.categoryName}</Badge>}
            </div>
          </div>

          <div className="flex items-baseline gap-2">
            <span className="text-3xl font-bold">${price.toFixed(2)}</span>
            {discounted && <span className="text-muted-foreground line-through">${product.price.toFixed(2)}</span>}
          </div>

          <Separator />

          <div className="flex items-center gap-3">
            <span className="text-sm text-muted-foreground">Quantity</span>
            <div className="flex items-center gap-1">
              <Button variant="outline" size="icon" onClick={() => setQty(Math.max(1, qty - 1))}><Minus className="h-3 w-3" /></Button>
              <span className="w-10 text-center">{qty}</span>
              <Button variant="outline" size="icon" onClick={() => setQty(Math.min(product.stockQuantity, qty + 1))}><Plus className="h-3 w-3" /></Button>
            </div>
            <span className="text-xs text-muted-foreground">{product.stockQuantity} in stock</span>
          </div>

          <div className="flex gap-2">
            <Button size="lg" onClick={onAdd} disabled={outOfStock} className="flex-1">
              <ShoppingCart className="h-4 w-4 mr-2" /> Add to cart
            </Button>
            <Button size="lg" variant="outline" asChild><Link to="/cart">View cart</Link></Button>
          </div>

          <div className="grid grid-cols-2 gap-2 pt-2 text-xs text-muted-foreground">
            <div className="flex items-center gap-2"><Truck className="h-4 w-4 text-primary" /> Fast shipping</div>
            <div className="flex items-center gap-2"><ShieldCheck className="h-4 w-4 text-primary" /> Verified seller</div>
          </div>
        </div>
      </div>

      <Tabs defaultValue="description">
        <TabsList>
          <TabsTrigger value="description">Description</TabsTrigger>
          <TabsTrigger value="reviews">Reviews ({product.ratingCount})</TabsTrigger>
          <TabsTrigger value="seller">Seller</TabsTrigger>
        </TabsList>
        <TabsContent value="description">
          <Card><CardContent className="pt-6 text-sm whitespace-pre-line">
            {product.description ?? "No description provided."}
            <p className="text-xs text-muted-foreground mt-2">SKU: <span className="font-mono">{product.sku}</span></p>
          </CardContent></Card>
        </TabsContent>
        <TabsContent value="reviews">
          <Card><CardContent className="pt-6 space-y-4">
            <div className="space-y-2">
              <p className="font-semibold text-sm">Write a review</p>
              <div className="flex items-center gap-1">
                {[1, 2, 3, 4, 5].map((n) => (
                  <button key={n} type="button" onClick={() => setReviewRating(n)}>
                    <Star className={`h-5 w-5 ${n <= reviewRating ? "fill-amber-400 text-amber-400" : "text-muted-foreground"}`} />
                  </button>
                ))}
              </div>
              <Textarea placeholder="Share what you liked or didn't..." value={reviewComment}
                        onChange={(e) => setReviewComment(e.target.value)} />
              <Button size="sm" onClick={() => createReview.mutate()} disabled={createReview.isPending}>
                {createReview.isPending ? "Submitting..." : "Submit review"}
              </Button>
              <p className="text-xs text-muted-foreground">Only buyers of delivered orders can review.</p>
            </div>
            <Separator />
            <div className="space-y-3">
              {reviews?.items.length
                ? reviews.items.map((r) => (
                    <div key={r.id} className="flex gap-3">
                      <div className="h-8 w-8 rounded-full bg-muted overflow-hidden flex-shrink-0">
                        {r.userAvatarUrl && <img src={r.userAvatarUrl} className="object-cover w-full h-full" />}
                      </div>
                      <div className="flex-1">
                        <div className="flex items-center gap-2 text-sm">
                          <span className="font-medium">{r.userDisplayName}</span>
                          <span className="flex items-center text-amber-500">
                            {Array.from({ length: r.rating }).map((_, i) => <Star key={i} className="h-3 w-3 fill-current" />)}
                          </span>
                          <span className="text-xs text-muted-foreground">{new Date(r.createdAt).toLocaleDateString()}</span>
                        </div>
                        {r.comment && <p className="text-sm text-muted-foreground mt-1">{r.comment}</p>}
                      </div>
                    </div>
                  ))
                : <p className="text-sm text-muted-foreground text-center py-4">No reviews yet.</p>}
            </div>
          </CardContent></Card>
        </TabsContent>
        <TabsContent value="seller">
          <Card><CardContent className="pt-6 space-y-1">
            <p className="font-semibold">{product.storeName}</p>
            <p className="text-xs text-muted-foreground">
              {product.storeApprovalStatus === "Approved" ? "Verified store on Pawzaroo." : "Pending verification."}
            </p>
            <Link to={`/store/sellers/${product.storeId}`} className="text-sm text-primary hover:underline">
              View store →
            </Link>
          </CardContent></Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
