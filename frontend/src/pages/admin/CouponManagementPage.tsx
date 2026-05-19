import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Edit2, Trash2, Ticket, Power, PowerOff } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { couponsApi, type Coupon, type CouponInput, type CouponType } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

type FormState = {
  id?: string;
  code: string; type: CouponType; value: string;
  minOrderAmount: string;
  maxRedemptions: string;
  expiresAt: string;
  isActive: boolean;
};

const empty: FormState = {
  code: "", type: "Percent", value: "10",
  minOrderAmount: "0",
  maxRedemptions: "",
  expiresAt: "",
  isActive: true
};

export function CouponManagementPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ["coupons"], queryFn: () => couponsApi.list() });

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<FormState>(empty);

  function toInput(f: FormState): CouponInput {
    return {
      code: f.code.trim().toUpperCase(),
      type: f.type,
      value: Number(f.value) || 0,
      minOrderAmount: Number(f.minOrderAmount) || 0,
      maxRedemptions: f.maxRedemptions.trim() === "" ? null : Number(f.maxRedemptions),
      expiresAt: f.expiresAt.trim() === "" ? null : new Date(f.expiresAt).toISOString(),
      isActive: f.isActive
    };
  }

  const create = useMutation({
    mutationFn: () => couponsApi.create(toInput(form)),
    onSuccess: () => { toast.success("Coupon created"); setOpen(false); setForm(empty); qc.invalidateQueries({ queryKey: ["coupons"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Create failed.")
  });
  const update = useMutation({
    mutationFn: () => couponsApi.update(form.id!, toInput(form)),
    onSuccess: () => { toast.success("Coupon updated"); setOpen(false); setForm(empty); qc.invalidateQueries({ queryKey: ["coupons"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Update failed.")
  });
  const remove = useMutation({
    mutationFn: (id: string) => couponsApi.remove(id),
    onSuccess: () => { toast.success("Coupon deleted"); qc.invalidateQueries({ queryKey: ["coupons"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Delete failed.")
  });

  function beginCreate() { setForm(empty); setOpen(true); }
  function beginEdit(c: Coupon) {
    setForm({
      id: c.id, code: c.code, type: c.type,
      value: c.value.toString(),
      minOrderAmount: c.minOrderAmount.toString(),
      maxRedemptions: c.maxRedemptions?.toString() ?? "",
      expiresAt: c.expiresAt ? c.expiresAt.slice(0, 16) : "",
      isActive: c.isActive
    });
    setOpen(true);
  }

  function submit() { (form.id ? update : create).mutate(); }

  return (
    <div className="max-w-5xl mx-auto space-y-4">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="flex items-center gap-2"><Ticket className="h-5 w-5 text-primary" /> Coupons</CardTitle>
          <Button size="sm" onClick={beginCreate}><Plus className="h-4 w-4 mr-1" /> New coupon</Button>
        </CardHeader>
        <CardContent>
          {isLoading ? <Skeleton className="h-40" /> : !data || data.length === 0 ? (
            <div className="py-10 text-center text-sm text-muted-foreground">No coupons yet. Create one to give buyers a discount at checkout.</div>
          ) : (
            <div className="space-y-2">
              {data.map((c) => {
                const expired = c.expiresAt ? new Date(c.expiresAt) < new Date() : false;
                const exhausted = c.maxRedemptions != null && c.redemptionsCount >= c.maxRedemptions;
                return (
                  <div key={c.id} className="flex items-center justify-between border rounded-md p-3 text-sm">
                    <div className="flex items-center gap-3">
                      <div className="font-mono font-semibold">{c.code}</div>
                      <Badge variant="outline">{c.type === "Percent" ? `${c.value}%` : `−$${c.value.toFixed(2)}`}</Badge>
                      {c.minOrderAmount > 0 && <Badge variant="outline">min ${c.minOrderAmount.toFixed(2)}</Badge>}
                      {c.maxRedemptions != null && (
                        <Badge variant={exhausted ? "destructive" : "outline"}>
                          {c.redemptionsCount}/{c.maxRedemptions} used
                        </Badge>
                      )}
                      {c.expiresAt && (
                        <Badge variant={expired ? "destructive" : "outline"}>
                          {expired ? "Expired" : `Expires ${new Date(c.expiresAt).toLocaleDateString()}`}
                        </Badge>
                      )}
                      <Badge variant={c.isActive ? "default" : "outline"} className="ml-auto">
                        {c.isActive ? <Power className="h-3 w-3 mr-1" /> : <PowerOff className="h-3 w-3 mr-1" />}
                        {c.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </div>
                    <div className="flex gap-1">
                      <Button size="icon" variant="ghost" onClick={() => beginEdit(c)} title="Edit"><Edit2 className="h-4 w-4" /></Button>
                      <Button size="icon" variant="ghost" onClick={() => remove.mutate(c.id)} title="Delete"><Trash2 className="h-4 w-4 text-destructive" /></Button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{form.id ? "Edit coupon" : "New coupon"}</DialogTitle>
          </DialogHeader>

          <div className="space-y-3 py-2">
            <div>
              <Label htmlFor="code">Code</Label>
              <Input
                id="code"
                placeholder="SUMMER25"
                value={form.code}
                onChange={(e) => setForm({ ...form, code: e.target.value })}
                disabled={!!form.id}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>Type</Label>
                <Select value={form.type} onValueChange={(v) => setForm({ ...form, type: v as CouponType })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Percent">Percent off</SelectItem>
                    <SelectItem value="Fixed">Fixed amount off</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="value">{form.type === "Percent" ? "Percent (0–100)" : "Amount"}</Label>
                <Input
                  id="value" type="number" step="0.01" min={0}
                  value={form.value}
                  onChange={(e) => setForm({ ...form, value: e.target.value })}
                />
              </div>
            </div>

            <Separator />

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label htmlFor="min">Min order subtotal</Label>
                <Input id="min" type="number" step="0.01" min={0}
                  value={form.minOrderAmount}
                  onChange={(e) => setForm({ ...form, minOrderAmount: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="max">Max redemptions (blank = unlimited)</Label>
                <Input id="max" type="number" min={1} placeholder="∞"
                  value={form.maxRedemptions}
                  onChange={(e) => setForm({ ...form, maxRedemptions: e.target.value })}
                />
              </div>
            </div>

            <div>
              <Label htmlFor="exp">Expires (blank = never)</Label>
              <Input id="exp" type="datetime-local"
                value={form.expiresAt}
                onChange={(e) => setForm({ ...form, expiresAt: e.target.value })}
              />
            </div>

            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
              Active
            </label>
          </div>

          <DialogFooter>
            <Button variant="ghost" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={submit} disabled={!form.code.trim() || create.isPending || update.isPending}>
              {form.id ? (update.isPending ? "Saving…" : "Save") : (create.isPending ? "Creating…" : "Create")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
