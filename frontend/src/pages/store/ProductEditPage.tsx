import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Save } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Checkbox } from "@/components/ui/checkbox";
import { productSchema, type ProductFormInput } from "@/lib/schemas";
import { productsV2Api } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

export function ProductEditPage() {
  const { id } = useParams();
  const isNew = !id || id === "new";
  const nav = useNavigate();
  const qc = useQueryClient();

  const { data: existing } = useQuery({
    queryKey: ["product", id],
    queryFn: () => productsV2Api.get(id!),
    enabled: !isNew
  });

  const { data: categories } = useQuery({
    queryKey: ["product-categories"], queryFn: () => productsV2Api.categories()
  });

  const { register, handleSubmit, setValue, watch, reset, formState: { errors, isSubmitting } } =
    useForm<ProductFormInput>({ resolver: zodResolver(productSchema) });

  useEffect(() => {
    if (existing) reset({
      name: existing.name, sku: existing.sku, description: existing.description ?? "",
      price: existing.price, discountPrice: existing.discountPrice ?? undefined,
      stockQuantity: existing.stockQuantity,
      categoryId: existing.categoryId ?? "", brandId: existing.brandId ?? "",
      imageUrls: existing.imageUrls
    });
  }, [existing, reset]);

  const create = useMutation({
    mutationFn: (v: ProductFormInput) => productsV2Api.create({
      name: v.name, sku: v.sku, description: v.description,
      price: v.price, discountPrice: v.discountPrice ?? null,
      stockQuantity: v.stockQuantity,
      categoryId: v.categoryId || null, brandId: v.brandId || null,
      imageUrls: v.imageUrls
    }),
    onSuccess: ({ id }) => {
      toast.success("Product created.");
      qc.invalidateQueries({ queryKey: ["my-products"] });
      nav(`/dashboard/store/products/${id}`);
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Create failed.")
  });

  const update = useMutation({
    mutationFn: (v: ProductFormInput) => productsV2Api.update(id!, {
      name: v.name, description: v.description,
      price: v.price, discountPrice: v.discountPrice ?? null,
      categoryId: v.categoryId || null, brandId: v.brandId || null,
      isActive: existing?.isActive ?? true,
      imageUrls: v.imageUrls
    }),
    onSuccess: () => {
      toast.success("Saved.");
      qc.invalidateQueries({ queryKey: ["product", id] });
      qc.invalidateQueries({ queryKey: ["my-products"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Save failed.")
  });

  const images = watch("imageUrls") ?? [];

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild>
        <Link to="/dashboard/store/products"><ArrowLeft className="h-4 w-4 mr-1" /> Back</Link>
      </Button>

      <Card>
        <CardHeader><CardTitle>{isNew ? "New product" : "Edit product"}</CardTitle></CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit((v) => (isNew ? create : update).mutate(v))} className="space-y-3">
            <div className="grid sm:grid-cols-2 gap-3">
              <div><Label>Name</Label><Input {...register("name")} />
                {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}</div>
              <div><Label>SKU</Label><Input {...register("sku")} disabled={!isNew} />
                {errors.sku && <p className="text-xs text-destructive">{errors.sku.message}</p>}</div>
            </div>

            <div><Label>Description</Label><Textarea rows={4} {...register("description")} /></div>

            <div className="grid sm:grid-cols-3 gap-3">
              <div><Label>Price ($)</Label><Input type="number" step="0.01" {...register("price")} /></div>
              <div><Label>Discount price ($)</Label><Input type="number" step="0.01" {...register("discountPrice")} />
                {errors.discountPrice && <p className="text-xs text-destructive">{errors.discountPrice.message}</p>}</div>
              <div><Label>Stock qty</Label><Input type="number" {...register("stockQuantity")} disabled={!isNew} />
                {!isNew && <p className="text-xs text-muted-foreground">Use Inventory page to adjust stock.</p>}</div>
            </div>

            <div className="grid sm:grid-cols-2 gap-3">
              <div>
                <Label>Category</Label>
                <Select value={watch("categoryId") ?? ""} onValueChange={(v) => setValue("categoryId", v)}>
                  <SelectTrigger><SelectValue placeholder="None" /></SelectTrigger>
                  <SelectContent>
                    {categories?.map((c) => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div><Label>Brand id (optional)</Label><Input {...register("brandId")} placeholder="Brand UUID" /></div>
            </div>

            <div>
              <Label>Image URLs (one per line, up to 12)</Label>
              <Textarea rows={4}
                value={images.join("\n")}
                onChange={(e) => setValue("imageUrls", e.target.value.split("\n").map(s => s.trim()).filter(Boolean))}
                placeholder="https://cdn.example.com/p1.jpg" />
            </div>

            <Button type="submit" disabled={isSubmitting}>
              <Save className="h-4 w-4 mr-1" /> {isSubmitting ? "Saving..." : isNew ? "Create product" : "Save changes"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
