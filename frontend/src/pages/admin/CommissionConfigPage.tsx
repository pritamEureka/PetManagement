import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Percent, Plus, Trash2 } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { commissionsApi, type CommissionScope, type CommissionConfiguration } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { QueryBoundary } from "@/components/common/QueryBoundary";
import { EmptyState } from "@/components/common/EmptyState";
import { DraftBanner } from "@/components/common/DraftBanner";
import { PageHeader } from "@/components/common/PageHeader";
import { useDraft } from "@/hooks/useDraft";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatNumber } from "@/lib/format";

interface RuleDraft {
  scope: CommissionScope;
  storeId: string;
  categoryId: string;
  percent: string;
  effectiveFrom: string;
  notes: string;
}

export function CommissionConfigPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [scope, setScope] = useState<CommissionScope>("Global");
  const [storeId, setStoreId] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [percent, setPercent] = useState("10");
  const [effectiveFrom, setEffectiveFrom] = useState(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState("");
  const [deleting, setDeleting] = useState<CommissionConfiguration | null>(null);
  const draftValue: RuleDraft = { scope, storeId, categoryId, percent, effectiveFrom, notes };
  const draft = useDraft("admin-commission-rule", draftValue, { enabled: open });

  const query = useQuery({
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
      toast.success(t("adm.commission.saved"));
      qc.invalidateQueries({ queryKey: ["commission-configs"] });
      draft.clear();
      setOpen(false);
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? t("adm.saveFailed"))
  });

  const remove = useMutation({
    mutationFn: (id: string) => commissionsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["commission-configs"] })
  });

  async function confirmDelete() {
    if (!deleting) return;
    try {
      await remove.mutateAsync(deleting.id);
      toast.success(t("adm.commission.deleted"));
    } catch (e: any) {
      toast.error(e?.response?.data?.error?.message ?? t("adm.deleteFailed"));
    }
  }

  const newRule = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild><Button><Plus aria-hidden="true" className="h-4 w-4 me-1" /> {t("adm.commission.newRule")}</Button></DialogTrigger>
      <DialogContent>
        <DialogHeader><DialogTitle>{t("adm.commission.newTitle")}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <DraftBanner<RuleDraft>
            draft={draft}
            onRestore={(d) => {
              if (d.scope) setScope(d.scope);
              setStoreId(d.storeId ?? "");
              setCategoryId(d.categoryId ?? "");
              if (d.percent) setPercent(d.percent);
              if (d.effectiveFrom) setEffectiveFrom(d.effectiveFrom);
              setNotes(d.notes ?? "");
            }}
          />
          <div>
            <Label htmlFor="cc-scope">{t("adm.commission.scope")}</Label>
            <Select value={scope} onValueChange={(v) => setScope(v as CommissionScope)}>
              <SelectTrigger id="cc-scope"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="Global">{t("adm.commission.scopeGlobal")}</SelectItem>
                <SelectItem value="Category">{t("adm.commission.scopeCategory")}</SelectItem>
                <SelectItem value="Store">{t("adm.commission.scopeStore")}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          {scope === "Store" && <div><Label htmlFor="cc-store">{t("adm.commission.storeId")}</Label><Input id="cc-store" value={storeId} onChange={(e) => setStoreId(e.target.value)} /></div>}
          {scope === "Category" && <div><Label htmlFor="cc-cat">{t("adm.commission.categoryId")}</Label><Input id="cc-cat" value={categoryId} onChange={(e) => setCategoryId(e.target.value)} /></div>}
          <div><Label htmlFor="cc-pct">{t("adm.commission.percentLabel")}</Label><Input id="cc-pct" type="number" step="0.1" value={percent} onChange={(e) => setPercent(e.target.value)} /></div>
          <div><Label htmlFor="cc-from">{t("adm.commission.effectiveFrom")}</Label><Input id="cc-from" type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} /></div>
          <div><Label htmlFor="cc-notes">{t("adm.commission.notes")}</Label><Input id="cc-notes" value={notes} onChange={(e) => setNotes(e.target.value)} /></div>
          <Button className="w-full" onClick={() => upsert.mutate()} disabled={upsert.isPending}>
            {upsert.isPending ? t("adm.saving") : t("adm.commission.saveRule")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );

  return (
    <div className="space-y-4">
      <PageHeader title={t("adm.commission.title")} icon={Percent} actions={newRule} />

      <p className="text-sm text-muted-foreground">{t("adm.commission.precedence")}</p>

      <QueryBoundary
        query={query}
        empty={
          <EmptyState
            icon={Percent}
            title={t("adm.commission.emptyTitle")}
            description={t("adm.commission.emptyBody")}
            action={<Button size="sm" onClick={() => setOpen(true)}>{t("adm.commission.newRule")}</Button>}
          />
        }
      >
        {(configs) => (
          <Card>
            <CardContent className="p-0 overflow-x-auto">
              <table className="w-full text-sm">
                <caption className="sr-only">{t("adm.commission.title")}</caption>
                <thead className="border-b text-muted-foreground">
                  <tr className="text-start">
                    <th scope="col" className="p-3 text-start">{t("adm.commission.scope")}</th>
                    <th scope="col" className="p-3 text-start">{t("adm.commission.target")}</th>
                    <th scope="col" className="p-3 text-start">{t("adm.commission.percent")}</th>
                    <th scope="col" className="p-3 text-start">{t("adm.commission.effective")}</th>
                    <th scope="col" className="p-3 text-end">{t("adm.actions")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {configs.map((c) => (
                    <tr key={c.id}>
                      <td className="p-3"><Badge variant="outline">{c.scope}</Badge></td>
                      <td className="p-3">{c.storeName ?? c.categoryName ?? <span className="text-muted-foreground">{t("adm.commission.all")}</span>}</td>
                      <td className="p-3 font-semibold">{formatNumber(c.commissionPercent)}%</td>
                      <td className="p-3 text-xs text-muted-foreground">
                        {formatDate(c.effectiveFrom)}
                        {c.effectiveTo && <> → {formatDate(c.effectiveTo)}</>}
                      </td>
                      <td className="p-3 text-end">
                        <Button size="icon" variant="ghost" aria-label={t("common.delete")} onClick={() => setDeleting(c)}>
                          <Trash2 aria-hidden="true" className="h-4 w-4 text-destructive" />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </CardContent>
          </Card>
        )}
      </QueryBoundary>

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={t("adm.commission.deleteTitle")}
        description={t("adm.commission.deleteBody")}
        confirmLabel={t("common.delete")}
        destructive
        onConfirm={confirmDelete}
      />
    </div>
  );
}
