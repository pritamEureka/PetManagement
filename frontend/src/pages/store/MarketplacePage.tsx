import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Search, ShoppingBag, ShoppingCart, Star, SlidersHorizontal, Plus, Minus, Trash2 } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { productsV2Api, storesApi, type ProductSummary } from "@/api/marketplace";
import { useCartStore } from "@/store/cartStore";
import { toast } from "@/components/ui/sonner";
import { cn } from "@/lib/utils";

export function MarketplacePage() {
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState<string | undefined>();
  const [storeId, setStoreId] = useState<string | undefined>();
  const [sort, setSort] = useState("newest");
  const [minPrice, setMinPrice] = useState("");
  const [maxPrice, setMaxPrice] = useState("");
  const [page, setPage] = useState(1);

  const { data, isLoading } = useQuery({
    queryKey: ["marketplace-products", { search, categoryId, storeId, sort, minPrice, maxPrice, page }],
    queryFn: () => productsV2Api.list({
      search: search || undefined,
      categoryId: categoryId || undefined,
      storeId: storeId || undefined,
      sort,
      minPrice: minPrice ? Number(minPrice) : undefined,
      maxPrice: maxPrice ? Number(maxPrice) : undefined,
      page, pageSize: 24
    })
  });

  const { data: categories } = useQuery({
    queryKey: ["product-categories"],
    queryFn: () => productsV2Api.categories()
  });

  // Approved stores only — buyer-facing filter shouldn't expose pending/rejected.
  const { data: stores } = useQuery({
    queryKey: ["marketplace-stores"],
    queryFn: () => storesApi.search({ status: "Approved", pageSize: 200 })
  });

  const addToCart = useCartStore((s) => s.add);
  const setQty = useCartStore((s) => s.setQty);
  const removeFromCart = useCartStore((s) => s.remove);
  const cartLines = useCartStore((s) => s.lines);
  const cartCount = useCartStore((s) => s.count());

  // Map productId → quantity already in cart, so each ProductCard can show
  // its own counter without each one subscribing to the whole array.
  const inCart = new Map(cartLines.map((l) => [l.productId, l.quantity] as const));

  const onAdd = async (p: ProductSummary) => {
    try {
      await addToCart(p);
      toast.success(`${p.name} added to cart`);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Could not add to cart.");
    }
  };
  const onIncrement = async (p: ProductSummary, currentQty: number) => {
    try { await setQty(p.id, Math.min(p.stockQuantity, currentQty + 1)); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Cart update failed."); }
  };
  const onDecrement = async (p: ProductSummary, currentQty: number) => {
    try {
      if (currentQty <= 1) await removeFromCart(p.id);
      else await setQty(p.id, currentQty - 1);
    } catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Cart update failed."); }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <ShoppingBag className="h-6 w-6 text-primary" /> Marketplace
          </h1>
          <p className="text-sm text-muted-foreground">Food, accessories, medicine — from approved local stores.</p>
        </div>
        <Button variant="outline" asChild className="relative">
          <Link to="/cart">
            <ShoppingCart className="h-4 w-4 mr-2" /> Cart
            {cartCount > 0 && (
              <span className="ml-2 inline-flex items-center justify-center h-5 min-w-[1.25rem] px-1.5 rounded-full bg-primary text-primary-foreground text-xs font-semibold">
                {cartCount}
              </span>
            )}
          </Link>
        </Button>
      </div>

      <div className="flex flex-wrap gap-3 items-end">
        <div className="relative flex-1 min-w-[10rem]">
          <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input className="pl-8" placeholder="Search products..." value={search}
                 onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
        </div>

        <Select value={categoryId ?? "all"} onValueChange={(v) => { setCategoryId(v === "all" ? undefined : v); setPage(1); }}>
          <SelectTrigger className="w-full sm:w-44"><SelectValue placeholder="Category" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All categories</SelectItem>
            {categories?.map((c) => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
          </SelectContent>
        </Select>

        <Select value={storeId ?? "all"} onValueChange={(v) => { setStoreId(v === "all" ? undefined : v); setPage(1); }}>
          <SelectTrigger className="w-full sm:w-44"><SelectValue placeholder="Store" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All stores</SelectItem>
            {stores?.items.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
          </SelectContent>
        </Select>

        <Select value={sort} onValueChange={(v) => { setSort(v); setPage(1); }}>
          <SelectTrigger className="w-full sm:w-44"><SelectValue placeholder="Sort" /></SelectTrigger>
          <SelectContent>
            <SelectItem value="newest">Newest</SelectItem>
            <SelectItem value="price_asc">Price: low → high</SelectItem>
            <SelectItem value="price_desc">Price: high → low</SelectItem>
            <SelectItem value="rating_desc">Top rated</SelectItem>
            <SelectItem value="best_selling">Best selling</SelectItem>
          </SelectContent>
        </Select>

        <div className="flex items-center gap-1 text-sm">
          <SlidersHorizontal className="h-4 w-4 text-muted-foreground" />
          <Input className="w-16 sm:w-20" placeholder="Min $" value={minPrice} onChange={(e) => { setMinPrice(e.target.value); setPage(1); }} />
          <span className="text-muted-foreground">–</span>
          <Input className="w-16 sm:w-20" placeholder="Max $" value={maxPrice} onChange={(e) => { setMaxPrice(e.target.value); setPage(1); }} />
        </div>
      </div>

      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {[...Array(8)].map((_, i) => <Skeleton key={i} className="h-72" />)}
        </div>
      ) : !data || data.items.length === 0 ? (
        <Card><CardContent className="py-16 text-center text-muted-foreground">No products match these filters.</CardContent></Card>
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {data.items.map((p) => (
              <ProductCard
                key={p.id}
                p={p}
                cartQty={inCart.get(p.id) ?? 0}
                onAdd={onAdd}
                onIncrement={onIncrement}
                onDecrement={onDecrement}
              />
            ))}
          </div>
          <div className="flex items-center justify-between pt-2">
            <p className="text-sm text-muted-foreground">{data.total} products • page {data.page} of {data.totalPages}</p>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</Button>
              <Button variant="outline" size="sm" disabled={page >= data.totalPages} onClick={() => setPage(p => p + 1)}>Next</Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

interface ProductCardProps {
  p: ProductSummary;
  cartQty: number;
  onAdd: (p: ProductSummary) => void;
  onIncrement: (p: ProductSummary, currentQty: number) => void;
  onDecrement: (p: ProductSummary, currentQty: number) => void;
}

function ProductCard({ p, cartQty, onAdd, onIncrement, onDecrement }: ProductCardProps) {
  const price = p.discountPrice ?? p.price;
  const discounted = p.discountPrice != null && p.discountPrice < p.price;
  const inCart = cartQty > 0;
  const atStockCap = cartQty >= p.stockQuantity;

  return (
    <Card className={cn("overflow-hidden flex flex-col", inCart && "ring-2 ring-primary/40")}>
      <Link to={`/store/products/${p.id}`} className="aspect-square bg-muted relative">
        {p.imageUrls[0]
          ? <img src={p.imageUrls[0]} className="object-cover w-full h-full" />
          : <div className="flex items-center justify-center h-full text-muted-foreground"><ShoppingBag className="h-10 w-10" /></div>}
        {inCart && (
          <span className="absolute top-2 right-2 inline-flex items-center justify-center h-6 min-w-[1.5rem] px-2 rounded-full bg-primary text-primary-foreground text-xs font-semibold shadow">
            {cartQty} in cart
          </span>
        )}
      </Link>
      <CardContent className="pt-4 space-y-2 flex-1 flex flex-col">
        <Link to={`/store/products/${p.id}`} className="flex-1">
          <p className="font-medium line-clamp-2">{p.name}</p>
          <p className="text-xs text-muted-foreground mt-0.5">{p.storeName}</p>
        </Link>
        <div className="flex items-center justify-between">
          <div>
            <span className="text-lg font-bold">${price.toFixed(2)}</span>
            {discounted && <span className="ml-1 text-xs text-muted-foreground line-through">${p.price.toFixed(2)}</span>}
          </div>
          <span className="flex items-center gap-1 text-xs text-amber-500">
            <Star className="h-3 w-3 fill-current" /> {p.ratingAverage.toFixed(1)}
          </span>
        </div>
        <div className="flex items-center justify-between gap-2">
          {p.isFeatured && <Badge variant="secondary" className="text-[10px]">Featured</Badge>}

          {p.stockQuantity <= 0 ? (
            <Button size="sm" className="ml-auto" disabled>Sold out</Button>
          ) : !inCart ? (
            <Button size="sm" className="ml-auto" onClick={() => onAdd(p)}>
              <ShoppingCart className="h-3.5 w-3.5 mr-1" /> Add
            </Button>
          ) : (
            <div className="ml-auto flex items-center gap-1 rounded-md border bg-background">
              <Button size="icon" variant="ghost" className="h-8 w-8"
                onClick={() => onDecrement(p, cartQty)}
                title={cartQty === 1 ? "Remove from cart" : "Decrease quantity"}
              >
                {cartQty === 1 ? <Trash2 className="h-3.5 w-3.5 text-destructive" /> : <Minus className="h-3.5 w-3.5" />}
              </Button>
              <span className="w-6 text-center text-sm font-semibold tabular-nums">{cartQty}</span>
              <Button size="icon" variant="ghost" className="h-8 w-8"
                onClick={() => onIncrement(p, cartQty)}
                disabled={atStockCap}
                title={atStockCap ? "Stock limit reached" : "Increase quantity"}
              >
                <Plus className="h-3.5 w-3.5" />
              </Button>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
