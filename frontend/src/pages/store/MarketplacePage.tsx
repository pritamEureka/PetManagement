import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Search, ShoppingBag, ShoppingCart, Star, SlidersHorizontal } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { productsV2Api, type ProductSummary } from "@/api/marketplace";
import { useCartStore } from "@/store/cartStore";
import { toast } from "@/components/ui/sonner";

export function MarketplacePage() {
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState<string | undefined>();
  const [sort, setSort] = useState("newest");
  const [minPrice, setMinPrice] = useState("");
  const [maxPrice, setMaxPrice] = useState("");
  const [page, setPage] = useState(1);

  const { data, isLoading } = useQuery({
    queryKey: ["marketplace-products", { search, categoryId, sort, minPrice, maxPrice, page }],
    queryFn: () => productsV2Api.list({
      search: search || undefined,
      categoryId: categoryId || undefined,
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

  const addToCart = useCartStore((s) => s.add);
  const onAdd = (p: ProductSummary) => {
    addToCart(p);
    toast.success(`${p.name} added to cart`);
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
        <Button variant="outline" asChild><Link to="/cart"><ShoppingCart className="h-4 w-4 mr-2" /> Cart</Link></Button>
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
            {data.items.map((p) => <ProductCard key={p.id} p={p} onAdd={onAdd} />)}
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

function ProductCard({ p, onAdd }: { p: ProductSummary; onAdd: (p: ProductSummary) => void }) {
  const price = p.discountPrice ?? p.price;
  const discounted = p.discountPrice != null && p.discountPrice < p.price;
  return (
    <Card className="overflow-hidden flex flex-col">
      <Link to={`/store/products/${p.id}`} className="aspect-square bg-muted">
        {p.imageUrls[0]
          ? <img src={p.imageUrls[0]} className="object-cover w-full h-full" />
          : <div className="flex items-center justify-center h-full text-muted-foreground"><ShoppingBag className="h-10 w-10" /></div>}
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
        <div className="flex items-center justify-between">
          {p.isFeatured && <Badge variant="secondary" className="text-[10px]">Featured</Badge>}
          <Button size="sm" className="ml-auto" onClick={() => onAdd(p)} disabled={p.stockQuantity <= 0}>
            {p.stockQuantity > 0 ? "Add" : "Sold out"}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
