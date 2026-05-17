import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Boxes, Plus, Minus, History } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { productsV2Api, type InventoryReason, type ProductSummary } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

const REASONS: InventoryReason[] = ["Purchase", "Sale", "Return", "Adjustment", "Damage", "Restock"];

export function InventoryManagementPage() {
  const qc = useQueryClient();
  const [selected, setSelected] = useState<ProductSummary | null>(null);
  const [qty, setQty] = useState(1);
  const [reason, setReason] = useState<InventoryReason>("Restock");
  const [notes, setNotes] = useState("");

  const { data: products, isLoading } = useQuery({
    queryKey: ["my-products", { search: "" }],
    queryFn: () => productsV2Api.mine({ page: 1, pageSize: 200 })
  });

  const { data: log } = useQuery({
    queryKey: ["inventory-log", selected?.id],
    queryFn: () => productsV2Api.listInventory(selected!.id, 1, 50),
    enabled: !!selected
  });

  const adjust = useMutation({
    mutationFn: () => productsV2Api.adjustInventory(selected!.id, { quantityChange: qty, reason, notes: notes || undefined }),
    onSuccess: () => {
      toast.success("Inventory adjusted");
      setQty(1); setNotes("");
      qc.invalidateQueries({ queryKey: ["my-products"] });
      qc.invalidateQueries({ queryKey: ["inventory-log", selected!.id] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Adjustment failed.")
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2"><Boxes className="h-6 w-6 text-primary" /> Inventory</h1>

      <div className="grid lg:grid-cols-[1fr_22rem] gap-4">
        <Card>
          <CardHeader><CardTitle className="text-base">Products & stock</CardTitle></CardHeader>
          <CardContent className="space-y-2 max-h-[36rem] overflow-y-auto">
            {isLoading ? Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-12" />)
              : products?.items.map((p) => (
                <button key={p.id}
                  onClick={() => setSelected(p)}
                  className={`w-full flex items-center gap-3 p-2 rounded-md text-left hover:bg-accent
                    ${selected?.id === p.id ? "bg-accent" : ""}`}>
                  <div className="h-10 w-10 bg-muted rounded overflow-hidden flex-shrink-0">
                    {p.imageUrls[0] && <img src={p.imageUrls[0]} className="object-cover w-full h-full" />}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="font-medium truncate">{p.name}</p>
                    <p className="text-xs text-muted-foreground font-mono">{p.sku}</p>
                  </div>
                  <Badge variant={p.stockQuantity <= 0 ? "destructive" : p.stockQuantity < 5 ? "outline" : "secondary"}>
                    {p.stockQuantity}
                  </Badge>
                </button>
              ))}
          </CardContent>
        </Card>

        <div className="space-y-3">
          <Card>
            <CardHeader><CardTitle className="text-base">Adjust</CardTitle></CardHeader>
            <CardContent className="space-y-3">
              {!selected ? (
                <p className="text-sm text-muted-foreground">Select a product on the left to adjust its stock.</p>
              ) : (
                <>
                  <p className="text-sm">
                    Product: <span className="font-medium">{selected.name}</span> — current stock {selected.stockQuantity}
                  </p>
                  <div className="flex items-center gap-2">
                    <Button size="icon" variant="outline" onClick={() => setQty((q) => q - 1)}><Minus className="h-3 w-3" /></Button>
                    <Input type="number" value={qty} onChange={(e) => setQty(Number(e.target.value))} className="w-24" />
                    <Button size="icon" variant="outline" onClick={() => setQty((q) => q + 1)}><Plus className="h-3 w-3" /></Button>
                  </div>
                  <div>
                    <Label>Reason</Label>
                    <Select value={reason} onValueChange={(v) => setReason(v as InventoryReason)}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {REASONS.map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div><Label>Notes</Label><Input value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
                  <Button onClick={() => adjust.mutate()} disabled={adjust.isPending || qty === 0} className="w-full">
                    {adjust.isPending ? "Applying..." : "Apply adjustment"}
                  </Button>
                </>
              )}
            </CardContent>
          </Card>

          {selected && (
            <Card>
              <CardHeader><CardTitle className="text-base flex items-center gap-2"><History className="h-4 w-4" /> Recent movements</CardTitle></CardHeader>
              <CardContent className="text-sm divide-y max-h-72 overflow-y-auto">
                {log?.items.length
                  ? log.items.map((m) => (
                      <div key={m.id} className="py-2 flex items-center justify-between">
                        <div>
                          <p className="font-medium">{m.reason} {m.notes && <span className="text-xs text-muted-foreground">— {m.notes}</span>}</p>
                          <p className="text-xs text-muted-foreground">{new Date(m.createdAt).toLocaleString()}</p>
                        </div>
                        <div className="text-right">
                          <p className={m.quantityChange < 0 ? "text-destructive" : "text-emerald-500"}>
                            {m.quantityChange > 0 ? "+" : ""}{m.quantityChange}
                          </p>
                          <p className="text-xs text-muted-foreground">→ {m.quantityAfter}</p>
                        </div>
                      </div>
                    ))
                  : <p className="text-sm text-muted-foreground py-3">No movements yet.</p>}
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
