import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { adoptionPlusApi, type AdoptionHomeVisit, type AdoptionReference } from "@/api/adoption";
import { useI18n } from "@/lib/i18n";

const REF_STATUSES: AdoptionReference["status"][] = ["Pending", "Contacted", "Verified", "Failed"];
const VISIT_STATUSES: AdoptionHomeVisit["status"][] = ["Scheduled", "Completed", "Cancelled", "NoShow"];
const selectCls = "h-8 rounded-md border border-input bg-background px-2 text-xs";

function errMsg(err: any) { return err?.response?.data?.error?.message ?? err?.response?.data?.detail ?? "Something went wrong."; }

interface Props { requestId: string | null; title?: string; onOpenChange: (open: boolean) => void }

/** Listing-owner dialog for one application: reference checks + home visits. */
export function ApplicationChecksDialog({ requestId, title, onOpenChange }: Props) {
  const { t } = useI18n();
  const qc = useQueryClient();
  const open = !!requestId;
  const { data: refs = [] } = useQuery({ queryKey: ["adoption", "refs", requestId], queryFn: () => adoptionPlusApi.references(requestId!), enabled: open });
  const { data: visits = [] } = useQuery({ queryKey: ["adoption", "visits", requestId], queryFn: () => adoptionPlusApi.homeVisits(requestId!), enabled: open });

  const [name, setName] = useState(""); const [contact, setContact] = useState(""); const [rel, setRel] = useState("");
  const [when, setWhen] = useState(""); const [visitNotes, setVisitNotes] = useState("");
  const [refNotes, setRefNotes] = useState<Record<string, string>>({});
  const [outcomes, setOutcomes] = useState<Record<string, string>>({});

  const reload = () => { qc.invalidateQueries({ queryKey: ["adoption", "refs", requestId] }); qc.invalidateQueries({ queryKey: ["adoption", "visits", requestId] }); };

  async function addRef() {
    if (!requestId || !name.trim() || !contact.trim()) return;
    try { await adoptionPlusApi.addReference(requestId, { name: name.trim(), contact: contact.trim(), relationship: rel.trim() || undefined }); setName(""); setContact(""); setRel(""); reload(); }
    catch (e) { toast.error(errMsg(e)); }
  }
  async function setRefStatus(r: AdoptionReference, status: AdoptionReference["status"]) {
    try { await adoptionPlusApi.updateReference(r.id, status, refNotes[r.id] ?? r.notes ?? undefined); reload(); }
    catch (e) { toast.error(errMsg(e)); }
  }
  async function schedule() {
    if (!requestId || !when) return;
    try { await adoptionPlusApi.scheduleVisit(requestId, new Date(when).toISOString(), visitNotes.trim() || undefined); setWhen(""); setVisitNotes(""); reload(); }
    catch (e) { toast.error(errMsg(e)); }
  }
  async function setVisit(v: AdoptionHomeVisit, status: AdoptionHomeVisit["status"]) {
    try { await adoptionPlusApi.updateVisit(v.id, status, v.notes ?? undefined, outcomes[v.id] ?? v.outcome ?? undefined); reload(); }
    catch (e) { toast.error(errMsg(e)); }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t("adoptmsg.checks.title")}</DialogTitle>
          {title && <DialogDescription>{title}</DialogDescription>}
        </DialogHeader>

        <section className="space-y-2">
          <h3 className="font-semibold text-sm">{t("adoptmsg.refs.title")}</h3>
          {refs.length === 0 && <p className="text-xs text-muted-foreground">{t("adoptmsg.refs.empty")}</p>}
          {refs.map((r) => (
            <div key={r.id} className="rounded-md border p-2 space-y-1 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="font-medium">{r.name} <span className="text-xs text-muted-foreground">{r.contact}{r.relationship ? ` · ${r.relationship}` : ""}</span></span>
                <select className={selectCls} value={r.status} onChange={(e) => setRefStatus(r, e.target.value as AdoptionReference["status"])} aria-label={t("adoptmsg.status")}>
                  {REF_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
                </select>
              </div>
              <Input className="h-8 text-xs" placeholder={t("adoptmsg.notes")} defaultValue={r.notes ?? ""}
                     onChange={(e) => setRefNotes((n) => ({ ...n, [r.id]: e.target.value }))}
                     onBlur={() => { if (refNotes[r.id] !== undefined && refNotes[r.id] !== (r.notes ?? "")) setRefStatus(r, r.status); }} />
            </div>
          ))}
          <div className="grid gap-2 sm:grid-cols-4">
            <Input placeholder={t("adoptmsg.refs.name")} value={name} onChange={(e) => setName(e.target.value)} maxLength={120} />
            <Input placeholder={t("adoptmsg.refs.contact")} value={contact} onChange={(e) => setContact(e.target.value)} maxLength={200} />
            <Input placeholder={t("adoptmsg.refs.relationship")} value={rel} onChange={(e) => setRel(e.target.value)} maxLength={80} />
            <Button size="sm" onClick={addRef} disabled={!name.trim() || !contact.trim()}>{t("adoptmsg.refs.add")}</Button>
          </div>
        </section>

        <section className="space-y-2">
          <h3 className="font-semibold text-sm">{t("adoptmsg.visits.title")}</h3>
          {visits.length === 0 && <p className="text-xs text-muted-foreground">{t("adoptmsg.visits.empty")}</p>}
          {visits.map((v) => (
            <div key={v.id} className="rounded-md border p-2 space-y-1 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="font-medium">{new Date(v.scheduledAt).toLocaleString()} <Badge variant="secondary">{v.status}</Badge></span>
                <select className={selectCls} value={v.status} onChange={(e) => setVisit(v, e.target.value as AdoptionHomeVisit["status"])} aria-label={t("adoptmsg.status")}>
                  {VISIT_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
                </select>
              </div>
              {v.notes && <p className="text-xs text-muted-foreground">{v.notes}</p>}
              <Input className="h-8 text-xs" placeholder={t("adoptmsg.visits.outcome")} defaultValue={v.outcome ?? ""}
                     onChange={(e) => setOutcomes((o) => ({ ...o, [v.id]: e.target.value }))}
                     onBlur={() => { if (outcomes[v.id] !== undefined && outcomes[v.id] !== (v.outcome ?? "")) setVisit(v, v.status); }} />
            </div>
          ))}
          <div className="grid gap-2 sm:grid-cols-3">
            <Input type="datetime-local" value={when} onChange={(e) => setWhen(e.target.value)} aria-label={t("adoptmsg.visits.when")} />
            <Input placeholder={t("adoptmsg.notes")} value={visitNotes} onChange={(e) => setVisitNotes(e.target.value)} maxLength={500} />
            <Button size="sm" onClick={schedule} disabled={!when}>{t("adoptmsg.visits.schedule")}</Button>
          </div>
        </section>
      </DialogContent>
    </Dialog>
  );
}
