import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ShoppingBag, Plus, Trash2 } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { productsV2Api, type ProductCategory } from "@/api/marketplace";
import { ConfirmDialog } from "@/components/admin/ConfirmDialog";
import { toast } from "@/components/ui/sonner";

export function AdminCategoryManagementPage() {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [toDelete, setToDelete] = useState<ProductCategory | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["product-categories"],
    queryFn: () => productsV2Api.categories()
  });

  const create = useMutation({
    mutationFn: () => productsV2Api.createCategory({ name, slug: slug || slugify(name) }),
    onSuccess: () => {
      toast.success("Category created.");
      qc.invalidateQueries({ queryKey: ["product-categories"] });
      setName(""); setSlug(""); setOpen(false);
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Create failed.")
  });

  // Delete uses the /v1/products/categories/{id} endpoint via fetch — keep this
  // page self-contained without growing the marketplace API client.
  const remove = useMutation({
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/v1/products/categories/${id}`, {
        method: "DELETE", credentials: "include",
        headers: { Authorization: `Bearer ${localStorage.getItem("pawzaroo-auth")
          ? JSON.parse(localStorage.getItem("pawzaroo-auth") || "{}")?.state?.accessToken ?? "" : ""}` }
      });
      if (!res.ok) throw new Error(await res.text());
    },
    onSuccess: () => {
      toast.success("Deleted.");
      qc.invalidateQueries({ queryKey: ["product-categories"] });
    },
    onError: (e: any) => toast.error(e?.message ?? "Delete failed.")
  });

  const columns: Column<ProductCategory>[] = [
    { key: "name", header: "Name", render: (c) => <span className="font-medium">{c.name}</span> },
    { key: "slug", header: "Slug", render: (c) => <span className="font-mono text-xs text-muted-foreground">{c.slug}</span> },
    { key: "count", header: "Products", className: "text-right w-24",
      render: (c) => <Badge variant="secondary">{c.productCount}</Badge> },
    { key: "actions", header: "", className: "w-12",
      render: (c) => (
        <Button variant="ghost" size="icon" onClick={() => setToDelete(c)}>
          <Trash2 className="h-4 w-4 text-destructive" />
        </Button>
      )
    }
  ];

  return (
    <div className="space-y-4">
      <PageHeader title="Product categories" icon={ShoppingBag}
        description={data ? `${data.length} categories` : ""}
        actions={
          <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
              <Button><Plus className="h-4 w-4 mr-1" /> New category</Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader><DialogTitle>New category</DialogTitle></DialogHeader>
              <div className="space-y-3">
                <div>
                  <Label>Name</Label>
                  <Input value={name} onChange={(e) => setName(e.target.value)} />
                </div>
                <div>
                  <Label>Slug (auto-generated if empty)</Label>
                  <Input value={slug} onChange={(e) => setSlug(e.target.value)} placeholder={slugify(name)} />
                </div>
                <Button className="w-full" disabled={!name.trim() || create.isPending}
                        onClick={() => create.mutate()}>
                  {create.isPending ? "Creating..." : "Create"}
                </Button>
              </div>
            </DialogContent>
          </Dialog>
        } />

      <Card>
        <CardContent className="pt-6">
          <DataTable data={data ?? []} columns={columns} rowKey={(c) => c.id} loading={isLoading} />
        </CardContent>
      </Card>

      <ConfirmDialog
        open={!!toDelete}
        onOpenChange={(v) => !v && setToDelete(null)}
        destructive
        title={toDelete ? `Delete "${toDelete.name}"?` : ""}
        description="Products assigned to this category will keep working but their category will be cleared."
        confirmLabel="Delete"
        isLoading={remove.isPending}
        onConfirm={() => toDelete ? remove.mutateAsync(toDelete.id) : undefined}
      />
    </div>
  );
}

function slugify(s: string) {
  return s.toLowerCase().trim().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
}
