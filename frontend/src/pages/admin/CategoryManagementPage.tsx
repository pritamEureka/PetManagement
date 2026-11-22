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
import { QueryBoundary } from "@/components/common/QueryBoundary";
import { EmptyState } from "@/components/common/EmptyState";
import { DraftBanner } from "@/components/common/DraftBanner";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { productsV2Api, type ProductCategory } from "@/api/marketplace";
import { api } from "@/api/client";
import { ConfirmDialog } from "@/components/admin/ConfirmDialog";
import { toast } from "@/components/ui/sonner";
import { useDraft } from "@/hooks/useDraft";
import { useI18n } from "@/lib/i18n";
import { formatNumber } from "@/lib/format";

export function AdminCategoryManagementPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [toDelete, setToDelete] = useState<ProductCategory | null>(null);
  const draft = useDraft("admin-category-new", { name, slug }, { enabled: open });

  const query = useQuery({
    queryKey: ["product-categories"],
    queryFn: () => productsV2Api.categories()
  });
  const { data } = query;

  const create = useMutation({
    mutationFn: () => productsV2Api.createCategory({ name, slug: slug || slugify(name) }),
    onSuccess: () => {
      toast.success(t("adm.cat.created"));
      qc.invalidateQueries({ queryKey: ["product-categories"] });
      draft.clear();
      setName(""); setSlug(""); setOpen(false);
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? t("adm.createFailed"))
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.delete(`/v1/products/categories/${id}`),
    onSuccess: () => {
      toast.success(t("adm.deleted"));
      qc.invalidateQueries({ queryKey: ["product-categories"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? e?.message ?? t("adm.deleteFailed"))
  });

  const columns: Column<ProductCategory>[] = [
    { key: "name", header: t("adm.name"), render: (c) => <span className="font-medium">{c.name}</span> },
    { key: "slug", header: t("adm.slug"), render: (c) => <span className="font-mono text-xs text-muted-foreground">{c.slug}</span> },
    { key: "count", header: t("adm.cat.products"), className: "text-end w-24",
      render: (c) => <Badge variant="secondary">{formatNumber(c.productCount)}</Badge> },
    { key: "actions", header: "", className: "w-12",
      render: (c) => (
        <Button variant="ghost" size="icon" aria-label={t("common.delete")} onClick={() => setToDelete(c)}>
          <Trash2 aria-hidden="true" className="h-4 w-4 text-destructive" />
        </Button>
      )
    }
  ];

  const newButton = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button><Plus aria-hidden="true" className="h-4 w-4 me-1" /> {t("adm.cat.new")}</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader><DialogTitle>{t("adm.cat.new")}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <DraftBanner<{ name: string; slug: string }>
            draft={draft}
            onRestore={(d) => { setName(d.name ?? ""); setSlug(d.slug ?? ""); }}
          />
          <div>
            <Label htmlFor="cat-name">{t("adm.name")}</Label>
            <Input id="cat-name" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="cat-slug">{t("adm.cat.slugHint")}</Label>
            <Input id="cat-slug" value={slug} onChange={(e) => setSlug(e.target.value)} placeholder={slugify(name)} />
          </div>
          <Button className="w-full" disabled={!name.trim() || create.isPending}
                  onClick={() => create.mutate()}>
            {create.isPending ? t("adm.creating") : t("adm.create")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );

  return (
    <div className="space-y-4">
      <PageHeader title={t("adm.cat.title")} icon={ShoppingBag}
        description={data ? t("adm.cat.count", { count: formatNumber(data.length) }) : ""}
        actions={newButton} />

      <QueryBoundary
        query={query}
        empty={<EmptyState icon={ShoppingBag} title={t("adm.cat.emptyTitle")} description={t("adm.cat.emptyBody")} />}
      >
        {(rows) => (
          <Card>
            <CardContent className="pt-6">
              <DataTable data={rows} columns={columns} rowKey={(c) => c.id} caption={t("adm.cat.title")} />
            </CardContent>
          </Card>
        )}
      </QueryBoundary>

      <ConfirmDialog
        open={!!toDelete}
        onOpenChange={(v) => !v && setToDelete(null)}
        destructive
        title={toDelete ? t("adm.cat.deleteTitle", { name: toDelete.name }) : ""}
        description={t("adm.cat.deleteBody")}
        confirmLabel={t("common.delete")}
        isLoading={remove.isPending}
        onConfirm={async () => { if (toDelete) await remove.mutateAsync(toDelete.id); }}
      />
    </div>
  );
}

function slugify(s: string) {
  return s.toLowerCase().trim().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
}
