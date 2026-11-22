import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, X, PackagePlus } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { shopPlusApi, type UpsertVariantInput, type Variant } from "@/api/shopPlus";

interface AttrRow { key: string; value: string }

interface FormState {
  id: string | null;
  sku: string;
  priceOverride: string;
  stockQuantity: string;
  isActive: boolean;
  attrs: AttrRow[];
}

const EMPTY: FormState = { id: null, sku: "", priceOverride: "", stockQuantity: "0", isActive: true, attrs: [{ key: "", value: "" }] };

const errMsg = (e: any, fallback: string) => e?.response?.data?.error?.message ?? fallback;

function toForm(v: Variant): FormState {
  const attrs = Object.entries(v.attributes ?? {}).map(([key, value]) => ({ key, value }));
  return {
    id: v.id, sku: v.sku,
    priceOverride: v.priceOverride == null ? "" : String(v.priceOverride),
    stockQuantity: String(v.stockQuantity),
    isActive: v.isActive,
    attrs: attrs.length ? attrs : [{ key: "", value: "" }],
  };
}

/** Store-owner management of a product's variants: list, create, edit, delete, adjust stock. */
export function VariantManager({ productId }: { productId: string }) {
  const qc = useQueryClient();
  const queryKey = ["variants-manage", productId];
  const { data, isLoading } = useQuery({ queryKey, queryFn: () => shopPlusApi.variantsManage(productId) });

  const [form, setForm] = useState<FormState | null>(null);
  const [deleting, setDeleting] = useState<Variant | null>(null);
  const [adjusting, setAdjusting] = useState<{ v: Variant; change: string; notes: string } | null>(null);

  const refresh = () => {
    qc.invalidateQueries({ queryKey });
    qc.invalidateQueries({ queryKey: ["product", productId] });
    qc.invalidateQueries({ queryKey: ["variants", productId] });
  };

  const save = useMutation({
    mutationFn: (f: FormState) => {
      const attributes: Record<string, string> = {};
      for (const r of f.attrs) {
        const k = r.key.trim();
        if (k) attributes[k] = r.value.trim();
      }
      const input: UpsertVariantInput = {
        sku: f.sku.trim(),
        attributes,
        priceOverride: f.priceOverride.trim() === "" ? null : Number(f.priceOverride),
        isActive: f.isActive,
      };
      if (!f.id) {
        input.stockQuantity = Math.max(0, Math.trunc(Number(f.stockQuantity) || 0));
        return shopPlusApi.createVariant(productId, input);
      }
      return shopPlusApi.updateVariant(f.id, input);
    },
    onSuccess: () => { toast.success("Variant saved"); setForm(null); refresh(); },
    onError: (e) => toast.error(errMsg(e, "Could not save variant.")),
  });

  const remove = useMutation({
    mutationFn: (id: string) => shopPlusApi.deleteVariant(id),
    onSuccess: () => { toast.success("Variant deleted"); refresh(); },
    onError: (e) => toast.error(errMsg(e, "Could not delete variant.")),
  });

  const adjust = useMutation({
    mutationFn: ({ id, change, notes }: { id: string; change: number; notes?: string }) =>
      shopPlusApi.adjustVariantStock(id, change, notes),
    onSuccess: () => { toast.success("Stock adjusted"); setAdjusting(null); refresh(); },
    onError: (e) => toast.error(errMsg(e, "Could not adjust stock.")),
  });

  function setAttr(i: number, patch: Partial<AttrRow>) {
    setForm((f) => f && { ...f, attrs: f.attrs.map((r, idx) => (idx === i ? { ...r, ...patch } : r)) });
  }

  function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form) return;
    if (!form.sku.trim()) { toast.error("SKU is required."); return; }
    const price = form.priceOverride.trim();
    if (price !== "" && (!Number.isFinite(Number(price)) || Number(price) < 0)) {
      toast.error("Price override must be a non-negative number."); return;
    }
    save.mutate(form);
  }

  const adjustChange = adjusting ? Math.trunc(Number(adjusting.change)) : 0;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0">
        <CardTitle>Variants</CardTitle>
        {!form && (
          <Button size="sm" onClick={() => setForm({ ...EMPTY, attrs: [{ key: "", value: "" }] })}>
            <Plus className="h-4 w-4 mr-1" /> Add variant
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-xs text-muted-foreground">
          Variants (size, colour...) can carry their own price and stock. Product stock always equals the sum of variant stock.
        </p>

        {form && (
          <form onSubmit={submit} className="space-y-3 rounded-md border p-3">
            <div className="grid sm:grid-cols-3 gap-3">
              <div>
                <Label htmlFor="var-sku">SKU</Label>
                <Input id="var-sku" value={form.sku} maxLength={64} onChange={(e) => setForm({ ...form, sku: e.target.value })} />
              </div>
              <div>
                <Label htmlFor="var-price">Price override</Label>
                <Input id="var-price" type="number" step="0.01" min="0" placeholder="Product price"
                  value={form.priceOverride} onChange={(e) => setForm({ ...form, priceOverride: e.target.value })} />
              </div>
              <div>
                <Label htmlFor="var-stock">Initial stock</Label>
                <Input id="var-stock" type="number" min="0" disabled={!!form.id}
                  value={form.stockQuantity} onChange={(e) => setForm({ ...form, stockQuantity: e.target.value })} />
                {form.id && <p className="text-xs text-muted-foreground">Use "Adjust stock" to change it.</p>}
              </div>
            </div>

            <div className="space-y-2">
              <Label>Attributes</Label>
              {form.attrs.map((r, i) => (
                <div key={i} className="flex gap-2">
                  <Input aria-label="Attribute name" placeholder="e.g. size" value={r.key} onChange={(e) => setAttr(i, { key: e.target.value })} />
                  <Input aria-label="Attribute value" placeholder="e.g. L" value={r.value} onChange={(e) => setAttr(i, { value: e.target.value })} />
                  <Button type="button" size="icon" variant="ghost" aria-label="Remove attribute"
                    onClick={() => setForm({ ...form, attrs: form.attrs.filter((_, idx) => idx !== i) })}>
                    <X className="h-4 w-4" />
                  </Button>
                </div>
              ))}
              <Button type="button" size="sm" variant="outline"
                onClick={() => setForm({ ...form, attrs: [...form.attrs, { key: "", value: "" }] })}>
                <Plus className="h-3.5 w-3.5 mr-1" /> Add attribute
              </Button>
            </div>

            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
              Active (visible to buyers)
            </label>

            <div className="flex gap-2">
              <Button type="submit" size="sm" disabled={save.isPending}>
                {save.isPending ? "Saving..." : form.id ? "Save variant" : "Create variant"}
              </Button>
              <Button type="button" size="sm" variant="ghost" onClick={() => setForm(null)}>Cancel</Button>
            </div>
          </form>
        )}

        {isLoading ? (
          <Skeleton className="h-20" />
        ) : !data?.length ? (
          <p className="text-sm text-muted-foreground">No variants yet. The product is sold as a single item.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b text-muted-foreground text-left">
                <tr>
                  <th className="py-2 pr-3 font-medium">SKU</th>
                  <th className="py-2 pr-3 font-medium">Attributes</th>
                  <th className="py-2 pr-3 font-medium text-right">Price</th>
                  <th className="py-2 pr-3 font-medium text-right">Stock</th>
                  <th className="py-2 pr-3 font-medium text-right">Reserved</th>
                  <th className="py-2 pr-3 font-medium">Status</th>
                  <th className="py-2 font-medium text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.map((v) => (
                  <tr key={v.id} className="border-b last:border-0">
                    <td className="py-2 pr-3 font-mono text-xs">{v.sku}</td>
                    <td className="py-2 pr-3">
                      {Object.entries(v.attributes ?? {}).map(([k, val]) => (
                        <Badge key={k} variant="outline" className="mr-1">{k}: {val}</Badge>
                      ))}
                    </td>
                    <td className="py-2 pr-3 text-right">
                      ${v.effectivePrice.toFixed(2)}
                      {v.priceOverride != null && <span className="block text-[10px] text-muted-foreground">override</span>}
                    </td>
                    <td className="py-2 pr-3 text-right">{v.stockQuantity}</td>
                    <td className="py-2 pr-3 text-right">{v.reserved}</td>
                    <td className="py-2 pr-3">
                      <Badge variant={v.isActive ? "default" : "secondary"}>{v.isActive ? "Active" : "Hidden"}</Badge>
                    </td>
                    <td className="py-2 text-right whitespace-nowrap">
                      <Button size="icon" variant="ghost" aria-label="Adjust stock" title="Adjust stock"
                        onClick={() => setAdjusting({ v, change: "", notes: "" })}>
                        <PackagePlus className="h-4 w-4" />
                      </Button>
                      <Button size="icon" variant="ghost" aria-label="Edit variant" title="Edit"
                        onClick={() => setForm(toForm(v))}>
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button size="icon" variant="ghost" aria-label="Delete variant" title="Delete"
                        onClick={() => setDeleting(v)}>
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {adjusting && (
          <div className="rounded-md border p-3 space-y-2">
            <p className="text-sm font-medium">Adjust stock for {adjusting.v.sku} (now {adjusting.v.stockQuantity})</p>
            <div className="grid sm:grid-cols-2 gap-3">
              <div>
                <Label htmlFor="adj-change">Change (negative to remove)</Label>
                <Input id="adj-change" type="number" step="1" value={adjusting.change}
                  onChange={(e) => setAdjusting({ ...adjusting, change: e.target.value })} />
              </div>
              <div>
                <Label htmlFor="adj-notes">Note (optional)</Label>
                <Input id="adj-notes" value={adjusting.notes}
                  onChange={(e) => setAdjusting({ ...adjusting, notes: e.target.value })} />
              </div>
            </div>
            <div className="flex gap-2">
              <Button size="sm" disabled={adjust.isPending || !adjustChange}
                onClick={() => adjust.mutate({ id: adjusting.v.id, change: adjustChange, notes: adjusting.notes.trim() || undefined })}>
                Apply
              </Button>
              <Button size="sm" variant="ghost" onClick={() => setAdjusting(null)}>Cancel</Button>
            </div>
          </div>
        )}
      </CardContent>

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(o) => !o && setDeleting(null)}
        title="Delete variant?"
        description={deleting ? `Variant ${deleting.sku} will be removed and its stock written off.` : ""}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => { if (deleting) await remove.mutateAsync(deleting.id); }}
      />
    </Card>
  );
}
