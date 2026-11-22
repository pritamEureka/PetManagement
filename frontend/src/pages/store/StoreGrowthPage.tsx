import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Megaphone, Package, Pencil, Plus, Trash2, TrendingUp, Bell } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "@/components/ui/sonner";
import { productsV2Api, storesApi, type ProductSummary } from "@/api/marketplace";
import { marketplacePlusApi, type Bundle, type SponsoredPlacement, type UpsertBundleInput } from "@/api/marketplacePlus";
import { useCommerceT } from "@/components/store/useCommerceT";

const money = (n: number) => `$${n.toFixed(2)}`;
const selectCls = "h-9 w-full rounded-md border border-input bg-background px-3 text-sm";

function errMsg(e: any, fallback: string) {
  return e?.response?.data?.error?.message ?? fallback;
}

/** Store-owner growth tools: bundles, low-stock thresholds and sponsored placement requests. Route: /dashboard/store/growth. */
export function StoreGrowthPage() {
  const c = useCommerceT();
  const { data: store, isLoading } = useQuery({ queryKey: ["my-store"], queryFn: () => storesApi.mine() });
  const { data: products } = useQuery({
    queryKey: ["growth-products"],
    queryFn: () => productsV2Api.mine({ pageSize: 100 }),
    enabled: !!store
  });
  const list = products?.items ?? [];

  if (isLoading) return <Skeleton className="h-48 max-w-4xl mx-auto" />;
  if (!store) {
    return (
      <Card className="max-w-4xl mx-auto"><CardContent className="py-12 text-center text-sm text-muted-foreground">{c("growth.noStore")}</CardContent></Card>
    );
  }

  return (
    <div className="max-w-4xl mx-auto space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2"><TrendingUp className="h-6 w-6 text-primary" /> {c("growth.title")}</h1>
          <p className="text-sm text-muted-foreground">{c("growth.subtitle")}</p>
        </div>
        <Button asChild variant="outline" size="sm"><Link to="/dashboard/store"><ArrowLeft className="h-4 w-4 mr-1" /> {c("growth.back")}</Link></Button>
      </div>

      <Tabs defaultValue="bundles">
        <TabsList>
          <TabsTrigger value="bundles"><Package className="h-3.5 w-3.5 mr-1" /> {c("growth.tab.bundles")}</TabsTrigger>
          <TabsTrigger value="stock"><Bell className="h-3.5 w-3.5 mr-1" /> {c("growth.tab.stock")}</TabsTrigger>
          <TabsTrigger value="sponsored"><Megaphone className="h-3.5 w-3.5 mr-1" /> {c("growth.tab.sponsored")}</TabsTrigger>
        </TabsList>
        <TabsContent value="bundles"><BundlesTab storeId={store.id} products={list} /></TabsContent>
        <TabsContent value="stock"><StockTab storeId={store.id} /></TabsContent>
        <TabsContent value="sponsored"><SponsoredTab storeId={store.id} products={list} /></TabsContent>
      </Tabs>
    </div>
  );
}

// ---------------------------------------------------------------- bundles

type PriceMode = "price" | "percent";
interface BundleDraft {
  id?: string; name: string; description: string; mode: PriceMode; value: string; isActive: boolean;
  items: Record<string, number>; // productId -> quantity (selected products only)
}
const emptyDraft: BundleDraft = { name: "", description: "", mode: "percent", value: "", isActive: true, items: {} };

function toDraft(b: Bundle): BundleDraft {
  return {
    id: b.id, name: b.name, description: b.description ?? "", isActive: b.isActive,
    mode: b.price != null ? "price" : "percent", value: String(b.price ?? b.discountPercent ?? ""),
    items: Object.fromEntries(b.items.map((i) => [i.productId, i.quantity]))
  };
}

function BundlesTab({ storeId, products }: { storeId: string; products: ProductSummary[] }) {
  const c = useCommerceT();
  const qc = useQueryClient();
  const [draft, setDraft] = useState<BundleDraft | null>(null);
  const { data: bundles, isLoading } = useQuery({
    queryKey: ["growth-bundles", storeId],
    queryFn: () => marketplacePlusApi.bundlesForStore(storeId, true)
  });

  const save = useMutation({
    mutationFn: (d: BundleDraft) => {
      const num = Number(d.value);
      const input: UpsertBundleInput = {
        storeId, name: d.name.trim(), description: d.description.trim() || undefined,
        price: d.mode === "price" ? num : null, discountPercent: d.mode === "percent" ? num : null,
        isActive: d.isActive,
        items: Object.entries(d.items).map(([productId, quantity]) => ({ productId, quantity }))
      };
      return d.id ? marketplacePlusApi.updateBundle(d.id, input) : marketplacePlusApi.createBundle(input);
    },
    onSuccess: () => { toast.success(c("growth.bundle.saved")); setDraft(null); qc.invalidateQueries({ queryKey: ["growth-bundles"] }); },
    onError: (e) => toast.error(errMsg(e, c("growth.error")))
  });
  const del = useMutation({
    mutationFn: (id: string) => marketplacePlusApi.deleteBundle(id),
    onSuccess: () => { toast.success(c("growth.bundle.deleted")); qc.invalidateQueries({ queryKey: ["growth-bundles"] }); },
    onError: (e) => toast.error(errMsg(e, c("growth.error")))
  });
  const toggle = useMutation({
    mutationFn: (b: Bundle) => marketplacePlusApi.updateBundle(b.id, {
      storeId, name: b.name, description: b.description ?? undefined, price: b.price ?? null,
      discountPercent: b.discountPercent ?? null, isActive: !b.isActive,
      items: b.items.map((i) => ({ productId: i.productId, variantId: i.variantId ?? null, quantity: i.quantity }))
    }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["growth-bundles"] }),
    onError: (e) => toast.error(errMsg(e, c("growth.error")))
  });

  function submit() {
    if (!draft) return;
    if (!draft.name.trim()) return toast.error(c("growth.bundle.nameRequired"));
    if (Object.keys(draft.items).length === 0) return toast.error(c("growth.bundle.pickOne"));
    const num = Number(draft.value);
    if (!draft.value || !(num > 0) || (draft.mode === "percent" && num >= 100)) return toast.error(c("growth.bundle.priceRequired"));
    save.mutate(draft);
  }

  return (
    <div className="space-y-3">
      {!draft && <Button size="sm" onClick={() => setDraft({ ...emptyDraft })}><Plus className="h-4 w-4 mr-1" /> {c("growth.bundle.new")}</Button>}

      {draft && (
        <Card>
          <CardHeader><CardTitle className="text-base">{draft.id ? c("growth.bundle.edit") : c("growth.bundle.new")}</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            <div className="grid sm:grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label htmlFor="b-name">{c("growth.bundle.name")}</Label>
                <Input id="b-name" value={draft.name} maxLength={120} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="b-desc">{c("growth.bundle.description")}</Label>
                <Input id="b-desc" value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} />
              </div>
            </div>

            <div className="space-y-1">
              <Label>{c("growth.bundle.products")}</Label>
              <div className="max-h-56 overflow-y-auto rounded-md border divide-y">
                {products.length === 0 && <p className="p-3 text-sm text-muted-foreground">{c("growth.stock.none")}</p>}
                {products.map((p) => {
                  const qty = draft.items[p.id];
                  const on = qty !== undefined;
                  return (
                    <div key={p.id} className="flex items-center gap-3 px-3 py-2 text-sm">
                      <input type="checkbox" aria-label={p.name} checked={on}
                        onChange={(e) => {
                          const items = { ...draft.items };
                          if (e.target.checked) items[p.id] = 1; else delete items[p.id];
                          setDraft({ ...draft, items });
                        }} />
                      <span className="flex-1 truncate">{p.name}</span>
                      <span className="text-muted-foreground">{money(p.discountPrice ?? p.price)}</span>
                      {on && (
                        <Input type="number" min={1} max={99} className="w-16 h-8" aria-label={c("growth.qty")} value={qty}
                          onChange={(e) => setDraft({ ...draft, items: { ...draft.items, [p.id]: Math.max(1, Math.min(99, Number(e.target.value) || 1)) } })} />
                      )}
                    </div>
                  );
                })}
              </div>
            </div>

            <div className="grid sm:grid-cols-2 gap-3 items-end">
              <div className="space-y-1">
                <Label>{c("growth.bundle.pricing")}</Label>
                <select className={selectCls} value={draft.mode} onChange={(e) => setDraft({ ...draft, mode: e.target.value as PriceMode })}>
                  <option value="percent">{c("growth.bundle.percentOff")}</option>
                  <option value="price">{c("growth.bundle.fixedPrice")}</option>
                </select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="b-value">{draft.mode === "price" ? c("growth.bundle.price") : c("growth.bundle.percent")}</Label>
                <Input id="b-value" type="number" min={0} step="0.01" value={draft.value} onChange={(e) => setDraft({ ...draft, value: e.target.value })} />
              </div>
            </div>

            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={draft.isActive} onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })} /> {c("growth.bundle.active")}
            </label>

            <div className="flex gap-2">
              <Button onClick={submit} disabled={save.isPending}>{c("growth.save")}</Button>
              <Button variant="ghost" onClick={() => setDraft(null)}>{c("growth.cancel")}</Button>
            </div>
          </CardContent>
        </Card>
      )}

      {isLoading ? <Skeleton className="h-24" /> : !bundles?.length ? (
        <p className="text-sm text-muted-foreground py-4">{c("growth.bundle.none")}</p>
      ) : bundles.map((b) => (
        <Card key={b.id}>
          <CardContent className="py-3 flex items-start justify-between gap-3 flex-wrap">
            <div className="min-w-0 space-y-1">
              <p className="font-medium flex items-center gap-2">{b.name}
                <Badge variant={b.isActive ? "default" : "secondary"}>{b.isActive ? c("growth.bundle.active") : c("growth.bundle.inactive")}</Badge>
              </p>
              <p className="text-xs text-muted-foreground truncate">{b.items.map((i) => `${i.quantity}x ${i.productName}`).join(", ")}</p>
              <p className="text-sm">
                <span className="text-muted-foreground line-through mr-2" title={c("growth.bundle.listPrice")}>{money(b.listPrice)}</span>
                <span className="font-semibold">{money(b.bundlePrice)}</span>
                <span className="text-emerald-600 ml-2">{c("growth.bundle.savings")} {money(b.savings)}</span>
              </p>
            </div>
            <div className="flex items-center gap-1">
              <Button size="sm" variant="outline" disabled={toggle.isPending} onClick={() => toggle.mutate(b)}>
                {b.isActive ? c("growth.bundle.inactive") : c("growth.bundle.active")}
              </Button>
              <Button size="icon" variant="ghost" aria-label={c("growth.edit")} onClick={() => setDraft(toDraft(b))}><Pencil className="h-4 w-4" /></Button>
              <Button size="icon" variant="ghost" aria-label={c("growth.delete")}
                onClick={() => { if (window.confirm(c("growth.bundle.confirmDelete"))) del.mutate(b.id); }}>
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------- stock alerts

function StockTab({ storeId }: { storeId: string }) {
  const c = useCommerceT();
  const qc = useQueryClient();
  const [edits, setEdits] = useState<Record<string, string>>({});
  const { data: rows, isLoading } = useQuery({ queryKey: ["growth-stock", storeId], queryFn: () => marketplacePlusApi.stockForStore(storeId) });
  const key = (r: { productId: string; variantId?: string | null }) => `${r.productId}:${r.variantId ?? ""}`;

  const save = useMutation({
    mutationFn: (p: { productId: string; variantId?: string | null; threshold: number }) => marketplacePlusApi.setStockThreshold(p),
    onSuccess: (_d, v) => {
      toast.success(c("growth.stock.saved"));
      setEdits((e) => { const n = { ...e }; delete n[key(v)]; return n; });
      qc.invalidateQueries({ queryKey: ["growth-stock"] });
    },
    onError: (e) => toast.error(errMsg(e, c("growth.error")))
  });

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">{c("growth.tab.stock")}</CardTitle></CardHeader>
      <CardContent className="space-y-3">
        <p className="text-sm text-muted-foreground">{c("growth.stock.help")}</p>
        {isLoading ? <Skeleton className="h-24" /> : !rows?.length ? (
          <p className="text-sm text-muted-foreground">{c("growth.stock.none")}</p>
        ) : (
          <div className="divide-y">
            {rows.map((r) => {
              const k = key(r);
              const val = edits[k] ?? String(r.threshold);
              const n = Number(val);
              const valid = val !== "" && Number.isInteger(n) && n >= 0 && n <= 100000;
              const dirty = edits[k] !== undefined && n !== r.threshold;
              return (
                <div key={k} className="flex items-center gap-3 py-2 text-sm flex-wrap">
                  <div className="flex-1 min-w-[10rem]">
                    <p className="font-medium truncate">{r.productName}{r.variantSku ? ` (${r.variantSku})` : ""}</p>
                    <p className="text-xs text-muted-foreground">
                      {c("growth.stock.stock")}: {r.stock} {r.isLow && <Badge variant="destructive" className="ml-1">{c("growth.stock.low")}</Badge>}
                    </p>
                  </div>
                  <Input type="number" min={0} max={100000} className="w-24 h-8" aria-label={`${c("growth.stock.threshold")} ${r.productName}`}
                    value={val} onChange={(e) => setEdits({ ...edits, [k]: e.target.value })} />
                  <Button size="sm" disabled={!dirty || !valid || save.isPending}
                    onClick={() => save.mutate({ productId: r.productId, variantId: r.variantId ?? null, threshold: n })}>
                    {c("growth.save")}
                  </Button>
                </div>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------- sponsored

const toLocalInput = (d: Date) => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);

function SponsoredTab({ storeId, products }: { storeId: string; products: ProductSummary[] }) {
  const c = useCommerceT();
  const qc = useQueryClient();
  const defaults = useMemo(() => ({ s: toLocalInput(new Date()), e: toLocalInput(new Date(Date.now() + 7 * 864e5)) }), []);
  const [form, setForm] = useState({ productId: "", startsAt: defaults.s, endsAt: defaults.e, budget: "" });
  const { data: rows, isLoading } = useQuery({ queryKey: ["growth-sponsored", storeId], queryFn: () => marketplacePlusApi.sponsored(storeId) });

  const create = useMutation({
    mutationFn: () => marketplacePlusApi.createSponsored({
      productId: form.productId, startsAt: new Date(form.startsAt).toISOString(), endsAt: new Date(form.endsAt).toISOString(),
      budget: form.budget ? Number(form.budget) : null
    }),
    onSuccess: () => { toast.success(c("growth.sponsored.requested")); setForm({ ...form, productId: "", budget: "" }); qc.invalidateQueries({ queryKey: ["growth-sponsored"] }); },
    onError: (e) => toast.error(errMsg(e, c("growth.error")))
  });
  const del = useMutation({
    mutationFn: (id: string) => marketplacePlusApi.deleteSponsored(id),
    onSuccess: () => { toast.success(c("growth.sponsored.deleted")); qc.invalidateQueries({ queryKey: ["growth-sponsored"] }); },
    onError: (e) => toast.error(errMsg(e, c("growth.error")))
  });

  function submit() {
    if (!form.productId) return toast.error(c("growth.sponsored.pickProduct"));
    if (!(new Date(form.endsAt) > new Date(form.startsAt))) return toast.error(c("growth.sponsored.badDates"));
    if (form.budget && !(Number(form.budget) >= 0)) return toast.error(c("growth.error"));
    create.mutate();
  }

  const statusVariant = (s: SponsoredPlacement["status"]) => (s === "Approved" ? "default" : s === "Rejected" ? "destructive" : "secondary");

  return (
    <div className="space-y-3">
      <Card>
        <CardHeader><CardTitle className="text-base">{c("growth.sponsored.new")}</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">{c("growth.sponsored.help")}</p>
          <div className="grid sm:grid-cols-2 gap-3">
            <div className="space-y-1 sm:col-span-2">
              <Label htmlFor="sp-product">{c("growth.sponsored.product")}</Label>
              <select id="sp-product" className={selectCls} value={form.productId} onChange={(e) => setForm({ ...form, productId: e.target.value })}>
                <option value="">-</option>
                {products.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="sp-start">{c("growth.sponsored.starts")}</Label>
              <Input id="sp-start" type="datetime-local" value={form.startsAt} onChange={(e) => setForm({ ...form, startsAt: e.target.value })} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="sp-end">{c("growth.sponsored.ends")}</Label>
              <Input id="sp-end" type="datetime-local" value={form.endsAt} onChange={(e) => setForm({ ...form, endsAt: e.target.value })} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="sp-budget">{c("growth.sponsored.budget")}</Label>
              <Input id="sp-budget" type="number" min={0} step="0.01" value={form.budget} onChange={(e) => setForm({ ...form, budget: e.target.value })} />
            </div>
          </div>
          <Button onClick={submit} disabled={create.isPending}>{c("growth.sponsored.submit")}</Button>
        </CardContent>
      </Card>

      {isLoading ? <Skeleton className="h-24" /> : !rows?.length ? (
        <p className="text-sm text-muted-foreground py-2">{c("growth.sponsored.none")}</p>
      ) : rows.map((r) => (
        <Card key={r.id}>
          <CardContent className="py-3 flex items-center justify-between gap-3 flex-wrap">
            <div className="min-w-0">
              <p className="font-medium flex items-center gap-2 truncate">{r.productName}
                <Badge variant={statusVariant(r.status)}>{c(`growth.sponsored.status.${r.status}`)}</Badge>
              </p>
              <p className="text-xs text-muted-foreground">
                {new Date(r.startsAt).toLocaleString()} - {new Date(r.endsAt).toLocaleString()}
                {r.budget != null && <> | {c("growth.sponsored.spent")} {money(r.spent)} / {money(r.budget)}</>}
                {" | "}{c("growth.sponsored.impressions")} {r.impressions}
              </p>
            </div>
            <Button size="icon" variant="ghost" aria-label={c("growth.delete")} disabled={del.isPending} onClick={() => del.mutate(r.id)}>
              <Trash2 className="h-4 w-4 text-destructive" />
            </Button>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
