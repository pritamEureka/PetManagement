import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Percent, Plus, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { commissionsApi, type CommissionScope, type CommissionConfiguration } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";

export function CommissionConfigPage() {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [scope, setScope] = useState<CommissionScope>("Global");
  const [storeId, setStoreId] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [percent, setPercent] = useState("10");
  const [effectiveFrom, setEffectiveFrom] = useState(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState("");
  const [deleting, setDeleting] = useState<CommissionConfiguration | null>(null);

  const { data: configs } = useQuery({
    queryKey: ["commission-configs"], queryFn: () => commissionsApi.list()
  });

  const upsert = useMutation({
    mutationFn: () => commissionsApi.upsert({
      scope,
      storeId: scope === "Store" ? storeId : null,
      categoryId: scope === "Category" ? categoryId : null,
      commissionPercent: Number(percent),
      flatFee: null, minCommission: null, maxCommission: null,
      effectiveFrom: new Date(effectiveFrom).toISOString(),
      effectiveTo: null,
      notes: notes || null
    }),
    onSuccess: () => {
      toast.success("Commission rule saved");
      qc.invalidateQueries({ queryKey: ["commission-configs"] });
      setOpen(false);
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Save failed.")
  });

  const remove = useMutation({
    mutationFn: (id: string) => commissionsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["commission-configs"] })
  });

  async function confirmDelete() {
    if (!deleting) return;
    try {
      await remove.mutateAsync(deleting.id);
      toast.success("Rule deleted.");
    } catch (e: any) {
      toast.error(e?.response?.data?.error?.message ?? "Delete failed.");
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h1 className="text-2xl font-bold flex items-center gap-2"><Percent className="h-6 w-6 text-primary" /> Commission rules</h1>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild><Button><Plus className="h-4 w-4 mr-1" /> New rule</Button></DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>New commission rule</DialogTitle></DialogHeader>
            <div className="space-y-3">
              <div>
                <Label>Scope</Label>
                <Select value={scope} onValueChange={(v) => setScope(v as CommissionScope)}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Global">Global (fallback)</SelectItem>
                    <SelectItem value="Category">Category</SelectItem>
                    <SelectItem value="Store">Store</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              {scope === "Store" && <div><Label>Store ID</Label><Input value={storeId} onChange={(e) => setStoreId(e.target.value)} /></div>}
              {scope === "Category" && <div><Label>Category ID</Label><Input value={categoryId} onChange={(e) => setCategoryId(e.target.value)} /></div>}
              <div><Label>Commission %</Label><Input type="number" step="0.1" value={percent} onChange={(e) => setPercent(e.target.value)} /></div>
              <div><Label>Effective from</Label><Input type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} /></div>
              <div><Label>Notes</Label><Input value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
              <Button className="w-full" onClick={() => upsert.mutate()} disabled={upsert.isPending}>
                {upsert.isPending ? "Saving..." : "Save rule"}
              </Button>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      <p className="text-sm text-muted-foreground">
        Precedence: store override → category rule → global fallback. Per-store percent on the Store entity is the
        last-resort fallback if no rule matches.
      </p>

      {!configs?.length
        ? <Card><CardContent className="py-10 text-center text-muted-foreground">No rules configured.</CardContent></Card>
        : (
          <Card>
            <CardContent className="p-0">
              <table className="w-full text-sm">
                <thead className="border-b text-muted-foreground">
                  <tr className="text-left">
                    <th className="p-3">Scope</th>
                    <th className="p-3">Target</th>
                    <th className="p-3">Percent</th>
                    <th className="p-3">Effective</th>
                    <th className="p-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {configs.map((c) => (
                    <tr key={c.id}>
                      <td className="p-3"><Badge variant="outline">{c.scope}</Badge></td>
                      <td className="p-3">{c.storeName ?? c.categoryName ?? <span className="text-muted-foreground">All</span>}</td>
                      <td className="p-3 font-semibold">{c.commissionPercent}%</td>
                      <td className="p-3 text-xs text-muted-foreground">
                        {new Date(c.effectiveFrom).toLocaleDateString()}
                        {c.effectiveTo && <> → {new Date(c.effectiveTo).toLocaleDateString()}</>}
                      </td>
                      <td className="p-3 text-right">
                        <Button size="icon" variant="ghost" onClick={() => setDeleting(c)}>
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </CardContent>
          </Card>
        )}

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title="Delete rule?"
        description="This commission rule will be permanently removed."
        confirmLabel="Delete"
        destructive
        onConfirm={confirmDelete}
      />
    </div>
  );
}
