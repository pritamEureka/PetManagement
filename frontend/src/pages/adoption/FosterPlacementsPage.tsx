import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Home } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { toast } from "@/components/ui/sonner";
import { UserPicker } from "@/components/pickers";
import { adoptionApi, adoptionPlusApi, type FosterPlacement } from "@/api/adoption";
import { useI18n } from "@/lib/i18n";

const selectCls = "h-9 rounded-md border border-input bg-background px-2 text-sm";
function errMsg(err: any) { return err?.response?.data?.error?.message ?? err?.response?.data?.detail ?? "Something went wrong."; }

/** Listing-owner view: start a foster placement, end it, or convert it into an adoption. */
export function FosterPlacementsPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const { data: listings } = useQuery({ queryKey: ["adoption", "mine", "foster-pick"], queryFn: () => adoptionApi.mine({ pageSize: 50 }) });
  const items = (listings?.items ?? []).filter((l) => l.status !== "Adopted");
  const [listingId, setListingId] = useState("");
  const [fosterUserId, setFosterUserId] = useState("");
  const [notes, setNotes] = useState("");
  const [endNotes, setEndNotes] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  const { data: fosters = [] } = useQuery({
    queryKey: ["adoption", "fosters", listingId], queryFn: () => adoptionPlusApi.fosters(listingId), enabled: !!listingId,
  });
  const active = fosters.some((f) => !f.endedAt);
  const reload = () => { qc.invalidateQueries({ queryKey: ["adoption", "fosters", listingId] }); qc.invalidateQueries({ queryKey: ["adoption", "mine"] }); };

  async function start() {
    if (!listingId || !fosterUserId) return;
    setBusy(true);
    try { await adoptionPlusApi.startFoster(listingId, fosterUserId, notes.trim() || undefined); setFosterUserId(""); setNotes(""); toast.success(t("adoptmsg.foster.started")); reload(); }
    catch (e) { toast.error(errMsg(e)); } finally { setBusy(false); }
  }
  async function end(f: FosterPlacement, outcome: "FosterFailed" | "ConvertedToAdoption" | "Returned") {
    if (outcome === "ConvertedToAdoption" && !window.confirm(t("adoptmsg.foster.confirmConvert"))) return;
    setBusy(true);
    try { await adoptionPlusApi.endFoster(f.id, outcome, endNotes[f.id]?.trim() || undefined); reload(); }
    catch (e) { toast.error(errMsg(e)); } finally { setBusy(false); }
  }

  return (
    <div className="space-y-4">
      <PageHeader title={t("adoptmsg.foster.title")} icon={Home} />
      <div className="flex flex-wrap items-center gap-2">
        <label className="text-sm font-medium" htmlFor="foster-listing">{t("adoptmsg.foster.listing")}</label>
        <select id="foster-listing" className={selectCls} value={listingId} onChange={(e) => setListingId(e.target.value)}>
          <option value="">{t("adoptmsg.foster.chooseListing")}</option>
          {items.map((l) => <option key={l.id} value={l.id}>{l.petName ? `${l.petName} - ${l.title}` : l.title}</option>)}
        </select>
      </div>

      {listingId && !active && (
        <Card><CardContent className="p-3 space-y-2">
          <h3 className="font-semibold text-sm">{t("adoptmsg.foster.start")}</h3>
          <div className="grid gap-2 sm:grid-cols-3">
            <UserPicker value={fosterUserId} onChange={(id) => setFosterUserId(id)} placeholder={t("adoptmsg.foster.pickUser")} clearable />
            <Input placeholder={t("adoptmsg.notes")} value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={500} />
            <Button onClick={start} disabled={busy || !fosterUserId}>{t("adoptmsg.foster.startBtn")}</Button>
          </div>
        </CardContent></Card>
      )}

      {listingId && fosters.length === 0 && <EmptyState icon={Home} title={t("adoptmsg.foster.empty")} description="" />}
      <div className="space-y-2">
        {fosters.map((f) => (
          <Card key={f.id}><CardContent className="p-3 space-y-2 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-medium">{f.fosterName}</span>
              <Badge variant={f.endedAt ? "secondary" : "default"}>{f.endedAt ? String(f.outcome) : t("adoptmsg.foster.active")}</Badge>
            </div>
            <p className="text-xs text-muted-foreground">
              {new Date(f.startedAt).toLocaleDateString()}{f.endedAt ? ` - ${new Date(f.endedAt).toLocaleDateString()}` : ""}{f.notes ? ` · ${f.notes}` : ""}
            </p>
            {!f.endedAt && (
              <div className="space-y-2">
                <Input className="h-8 text-xs" placeholder={t("adoptmsg.notes")} value={endNotes[f.id] ?? ""} onChange={(e) => setEndNotes((n) => ({ ...n, [f.id]: e.target.value }))} maxLength={500} />
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" disabled={busy} onClick={() => end(f, "ConvertedToAdoption")}>{t("adoptmsg.foster.convert")}</Button>
                  <Button size="sm" variant="secondary" disabled={busy} onClick={() => end(f, "Returned")}>{t("adoptmsg.foster.return")}</Button>
                  <Button size="sm" variant="outline" disabled={busy} onClick={() => end(f, "FosterFailed")}>{t("adoptmsg.foster.fail")}</Button>
                </div>
              </div>
            )}
          </CardContent></Card>
        ))}
      </div>
    </div>
  );
}
