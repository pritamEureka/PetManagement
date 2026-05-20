import { useState } from "react";
import axios from "axios";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, ShoppingBag, Star, Minus, Plus, ShoppingCart, Truck, ShieldCheck, Heart, Edit2, Trash2, ImagePlus, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { productsV2Api, wishlistApi } from "@/api/marketplace";
import { api } from "@/api/client";
import { useCartStore } from "@/store/cartStore";
import { useAuthStore } from "@/store/authStore";
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
  const currentUserId = useAuthStore((s) => s.user?.id);
  const [qty, setQty] = useState(1);
  const [activeImage, setActiveImage] = useState(0);
  const [reviewRating, setReviewRating] = useState(5);
  const [reviewComment, setReviewComment] = useState("");
  const [reviewImages, setReviewImages] = useState<string[]>([]);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [editingReviewId, setEditingReviewId] = useState<string | null>(null);

  const { data: wishlistStatus } = useQuery({
    queryKey: ["wishlist-status", id],
    queryFn: () => wishlistApi.status(id),
    enabled: !!id && !!currentUserId
  });

  const toggleWishlist = useMutation({
    mutationFn: async () => {
      if (wishlistStatus?.wishlisted) return wishlistApi.remove(id);
      return wishlistApi.add(id);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["wishlist-status", id] });
      qc.invalidateQueries({ queryKey: ["wishlist"] });
      toast.success(wishlistStatus?.wishlisted ? "Removed from wishlist" : "Saved to wishlist");
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not update wishlist.")
  });

  function resetReviewForm() {
    setReviewRating(5);
    setReviewComment("");
    setReviewImages([]);
    setEditingReviewId(null);
  }

  const createReview = useMutation({
    mutationFn: () => productsV2Api.createReview(id, {
      rating: reviewRating, comment: reviewComment || undefined,
      imageUrls: reviewImages.length > 0 ? reviewImages : undefined
    }),
    onSuccess: () => {
      toast.success("Review submitted");
      resetReviewForm();
      qc.invalidateQueries({ queryKey: ["product-reviews", id] });
      qc.invalidateQueries({ queryKey: ["product", id] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not submit review.")
  });

  const updateReview = useMutation({
    mutationFn: () => productsV2Api.updateReview(editingReviewId!, {
      rating: reviewRating, comment: reviewComment || undefined,
      imageUrls: reviewImages.length > 0 ? reviewImages : undefined
    }),
    onSuccess: () => {
      toast.success("Review updated");
      resetReviewForm();
      qc.invalidateQueries({ queryKey: ["product-reviews", id] });
      qc.invalidateQueries({ queryKey: ["product", id] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not update review.")
  });

  const deleteReview = useMutation({
    mutationFn: (reviewId: string) => productsV2Api.deleteReview(reviewId),
    onSuccess: () => {
      toast.success("Review deleted");
      qc.invalidateQueries({ queryKey: ["product-reviews", id] });
      qc.invalidateQueries({ queryKey: ["product", id] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not delete review.")
  });

  async function uploadReviewImage(file: File) {
    // Same presign-then-PUT flow used by messaging attachments. The server is
    // the gatekeeper on extension/mime; we just give the user a fast UX path.
    setUploadingImage(true);
    try {
      const presign = await api.post("/media/presign", { fileName: file.name, contentType: file.type })
        .then((r) => r.data?.data ?? r.data);
      await axios.put(presign.url, file, { headers: { "Content-Type": file.type } });
      setReviewImages((prev) => [...prev, presign.publicUrl].slice(0, 8));
    } catch {
      toast.error("Image upload failed.");
    } finally {
      setUploadingImage(false);
    }
  }

  function beginEdit(r: { id: string; rating: number; comment?: string | null; imageUrls: string[] }) {
    setEditingReviewId(r.id);
    setReviewRating(r.rating);
    setReviewComment(r.comment ?? "");
    setReviewImages([...r.imageUrls]);
  }

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

  async function onAdd() {
    if (!product) return;
    try {
      await add(product, qty);
      toast.success(`${qty}× ${product.name} added to cart`);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Could not add to cart.");
    }
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
            <div className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-5 gap-2">
              {product.imageUrls.map((src, i) => (
                <button
                  key={i}
                  onClick={() => setActiveImage(i)}
                  className={`aspect-square rounded-md overflow-hidden border transition-colors hover:bg-muted ${i === activeImage ? "ring-2 ring-primary" : ""}`}
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
            <Button
              size="lg"
              variant={wishlistStatus?.wishlisted ? "default" : "outline"}
              onClick={() => toggleWishlist.mutate()}
              disabled={!currentUserId || toggleWishlist.isPending}
              title={wishlistStatus?.wishlisted ? "Remove from wishlist" : "Save to wishlist"}
            >
              <Heart className={`h-4 w-4 ${wishlistStatus?.wishlisted ? "fill-current" : ""}`} />
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
              <p className="font-semibold text-sm">{editingReviewId ? "Edit your review" : "Write a review"}</p>
              <div className="flex items-center gap-1">
                {[1, 2, 3, 4, 5].map((n) => (
                  <button key={n} type="button" onClick={() => setReviewRating(n)} className="rounded-md p-0.5 hover:bg-muted">
                    <Star className={`h-5 w-5 ${n <= reviewRating ? "fill-amber-400 text-amber-400" : "text-muted-foreground"}`} />
                  </button>
                ))}
              </div>
              <Textarea placeholder="Share what you liked or didn't..." value={reviewComment}
                        onChange={(e) => setReviewComment(e.target.value)} />

              {reviewImages.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {reviewImages.map((url, i) => (
                    <div key={url} className="relative h-16 w-16 rounded border overflow-hidden">
                      <img src={url} className="object-cover w-full h-full" />
                      <button
                        type="button"
                        className="absolute top-0 right-0 bg-background/90 rounded-bl p-0.5 hover:bg-muted"
                        onClick={() => setReviewImages(reviewImages.filter((_, idx) => idx !== i))}
                      >
                        <X className="h-3 w-3" />
                      </button>
                    </div>
                  ))}
                </div>
              )}

              <div className="flex items-center gap-2">
                <label className="inline-flex items-center gap-1 text-xs text-muted-foreground cursor-pointer hover:text-foreground">
                  <ImagePlus className="h-4 w-4" />
                  {uploadingImage ? "Uploading…" : reviewImages.length < 8 ? "Add image" : "Max 8 images"}
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp,image/gif"
                    className="hidden"
                    disabled={uploadingImage || reviewImages.length >= 8}
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) uploadReviewImage(file);
                      e.target.value = "";
                    }}
                  />
                </label>
              </div>

              <div className="flex gap-2">
                {editingReviewId ? (
                  <>
                    <Button size="sm" onClick={() => updateReview.mutate()} disabled={updateReview.isPending}>
                      {updateReview.isPending ? "Saving..." : "Save changes"}
                    </Button>
                    <Button size="sm" variant="ghost" onClick={resetReviewForm}>Cancel</Button>
                  </>
                ) : (
                  <Button size="sm" onClick={() => createReview.mutate()} disabled={createReview.isPending}>
                    {createReview.isPending ? "Submitting..." : "Submit review"}
                  </Button>
                )}
              </div>

              <p className="text-xs text-muted-foreground">Only buyers of delivered orders can review.</p>
            </div>
            <Separator />
            <div className="space-y-3">
              {reviews?.items.length
                ? reviews.items.map((r) => {
                    const isMine = currentUserId && r.userId === currentUserId;
                    return (
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
                            {isMine && (
                              <div className="ml-auto flex gap-1">
                                <Button size="icon" variant="ghost" className="h-6 w-6" onClick={() => beginEdit(r)} title="Edit">
                                  <Edit2 className="h-3 w-3" />
                                </Button>
                                <Button size="icon" variant="ghost" className="h-6 w-6" onClick={() => deleteReview.mutate(r.id)} title="Delete">
                                  <Trash2 className="h-3 w-3 text-destructive" />
                                </Button>
                              </div>
                            )}
                          </div>
                          {r.comment && <p className="text-sm text-muted-foreground mt-1">{r.comment}</p>}
                          {r.imageUrls.length > 0 && (
                            <div className="flex flex-wrap gap-1.5 mt-2">
                              {r.imageUrls.map((u) => (
                                <a key={u} href={u} target="_blank" rel="noreferrer" className="h-14 w-14 rounded border overflow-hidden">
                                  <img src={u} className="object-cover w-full h-full" />
                                </a>
                              ))}
                            </div>
                          )}
                        </div>
                      </div>
                    );
                  })
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
