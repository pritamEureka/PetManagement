import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Edit2, Trash2, Ticket, Power, PowerOff } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { couponsApi, type Coupon, type CouponInput, type CouponType } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";
import { QueryBoundary } from "@/components/common/QueryBoundary";
import { EmptyState } from "@/components/common/EmptyState";
import { DraftBanner } from "@/components/common/DraftBanner";
import { useDraft } from "@/hooks/useDraft";
import { useI18n } from "@/lib/i18n";
import { formatCurrency, formatDate, formatNumber } from "@/lib/format";

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
  const { t } = useI18n();
  const qc = useQueryClient();
  const query = useQuery({ queryKey: ["coupons"], queryFn: () => couponsApi.list() });

  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<FormState>(empty);
  const draft = useDraft(`admin-coupon-${form.id ?? "new"}`, form, { enabled: open });

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
    onSuccess: () => { toast.success(t("adm.coupon.created")); draft.clear(); setOpen(false); setForm(empty); qc.invalidateQueries({ queryKey: ["coupons"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? t("adm.createFailed"))
  });
  const update = useMutation({
    mutationFn: () => couponsApi.update(form.id!, toInput(form)),
    onSuccess: () => { toast.success(t("adm.coupon.updated")); draft.clear(); setOpen(false); setForm(empty); qc.invalidateQueries({ queryKey: ["coupons"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? t("adm.saveFailed"))
  });
  const remove = useMutation({
    mutationFn: (id: string) => couponsApi.remove(id),
    onSuccess: () => { toast.success(t("adm.coupon.deleted")); qc.invalidateQueries({ queryKey: ["coupons"] }); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? t("adm.deleteFailed"))
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
          <CardTitle className="flex items-center gap-2"><Ticket aria-hidden="true" className="h-5 w-5 text-primary" /> {t("adm.coupon.title")}</CardTitle>
          <Button size="sm" onClick={beginCreate}><Plus aria-hidden="true" className="h-4 w-4 me-1" /> {t("adm.coupon.new")}</Button>
        </CardHeader>
        <CardContent>
          <QueryBoundary
            query={query}
            empty={
              <EmptyState
                icon={Ticket}
                title={t("adm.coupon.emptyTitle")}
                description={t("adm.coupon.emptyBody")}
                action={<Button size="sm" onClick={beginCreate}>{t("adm.coupon.new")}</Button>}
              />
            }
          >
            {(data) => (
              <div className="space-y-2">
                {data.map((c) => {
                  const expired = c.expiresAt ? new Date(c.expiresAt) < new Date() : false;
                  const exhausted = c.maxRedemptions != null && c.redemptionsCount >= c.maxRedemptions;
                  return (
                    <div key={c.id} className="flex flex-wrap items-center justify-between gap-2 border rounded-md p-3 text-sm">
                      <div className="flex flex-wrap items-center gap-3">
                        <div className="font-mono font-semibold">{c.code}</div>
                        <Badge variant="outline">{c.type === "Percent" ? `${formatNumber(c.value)}%` : `−${formatCurrency(c.value)}`}</Badge>
                        {c.minOrderAmount > 0 && <Badge variant="outline">{t("adm.coupon.min", { amount: formatCurrency(c.minOrderAmount) })}</Badge>}
                        {c.maxRedemptions != null && (
                          <Badge variant={exhausted ? "destructive" : "outline"}>
                            {t("adm.coupon.used", { used: formatNumber(c.redemptionsCount), max: formatNumber(c.maxRedemptions) })}
                          </Badge>
                        )}
                        {c.expiresAt && (
                          <Badge variant={expired ? "destructive" : "outline"}>
                            {expired ? t("adm.coupon.expired") : t("adm.coupon.expires", { date: formatDate(c.expiresAt) })}
                          </Badge>
                        )}
                        <Badge variant={statusBadgeVariant(c.isActive ? "Active" : "Inactive")}>
                          {c.isActive ? <Power aria-hidden="true" className="h-3 w-3 me-1" /> : <PowerOff aria-hidden="true" className="h-3 w-3 me-1" />}
                          {c.isActive ? t("adm.active") : t("adm.inactive")}
                        </Badge>
                      </div>
                      <div className="flex gap-1">
                        <Button size="icon" variant="ghost" onClick={() => beginEdit(c)} title={t("common.edit")} aria-label={t("common.edit")}><Edit2 aria-hidden="true" className="h-4 w-4" /></Button>
                        <Button size="icon" variant="ghost" onClick={() => remove.mutate(c.id)} title={t("common.delete")} aria-label={t("common.delete")}><Trash2 aria-hidden="true" className="h-4 w-4 text-destructive" /></Button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </QueryBoundary>
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{form.id ? t("adm.coupon.edit") : t("adm.coupon.new")}</DialogTitle>
          </DialogHeader>

          <DraftBanner<FormState> draft={draft} onRestore={(d) => setForm({ ...empty, ...d })} />

          <div className="space-y-3 py-2">
            <div>
              <Label htmlFor="code">{t("adm.coupon.code")}</Label>
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
                <Label htmlFor="ctype">{t("adm.coupon.type")}</Label>
                <Select value={form.type} onValueChange={(v) => setForm({ ...form, type: v as CouponType })}>
                  <SelectTrigger id="ctype"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Percent">{t("adm.coupon.percentOff")}</SelectItem>
                    <SelectItem value="Fixed">{t("adm.coupon.fixedOff")}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="value">{form.type === "Percent" ? t("adm.coupon.percentLabel") : t("adm.coupon.amount")}</Label>
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
                <Label htmlFor="min">{t("adm.coupon.minOrder")}</Label>
                <Input id="min" type="number" step="0.01" min={0}
                  value={form.minOrderAmount}
                  onChange={(e) => setForm({ ...form, minOrderAmount: e.target.value })}
                />
              </div>
              <div>
                <Label htmlFor="max">{t("adm.coupon.maxRedemptions")}</Label>
                <Input id="max" type="number" min={1} placeholder="∞"
                  value={form.maxRedemptions}
                  onChange={(e) => setForm({ ...form, maxRedemptions: e.target.value })}
                />
              </div>
            </div>

            <div>
              <Label htmlFor="exp">{t("adm.coupon.expiresLabel")}</Label>
              <Input id="exp" type="datetime-local"
                value={form.expiresAt}
                onChange={(e) => setForm({ ...form, expiresAt: e.target.value })}
              />
            </div>

            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
              {t("adm.active")}
            </label>
          </div>

          <DialogFooter>
            <Button variant="ghost" onClick={() => setOpen(false)}>{t("common.cancel")}</Button>
            <Button onClick={submit} disabled={!form.code.trim() || create.isPending || update.isPending}>
              {form.id ? (update.isPending ? t("adm.saving") : t("common.save")) : (create.isPending ? t("adm.creating") : t("adm.create"))}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
