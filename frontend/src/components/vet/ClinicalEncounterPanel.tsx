import { useEffect, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { FileSignature, Plus, Trash2, ShieldCheck } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { PromptDialog } from "@/components/common/PromptDialog";
import { toast } from "@/components/ui/sonner";
import { encounterApi, followUpApi, type DiagnosisCode, type Encounter, type EncounterDraft, type LabResult } from "@/api/clinicPlus";

const EMPTY: EncounterDraft = { chiefComplaint: "", findings: "", diagnosis: "", treatmentPlan: "", diagnosisCodes: [], labResults: [] };
const errMsg = (e: any) => e?.response?.data?.error?.message ?? "Something went wrong.";

function toDraft(e: Encounter): EncounterDraft {
  return {
    chiefComplaint: e.chiefComplaint ?? "", findings: e.findings ?? "", diagnosis: e.diagnosis ?? "", treatmentPlan: e.treatmentPlan ?? "",
    diagnosisCodes: e.diagnosisCodes, labResults: e.labResults
  };
}

/**
 * Clinical record for an appointment. Vet: autosaving draft (debounced), finalize (signed + prescriptions sealed) and
 * amend. Owner: read-only view of the finalized record. Drafts are never visible to the owner.
 */
export function ClinicalEncounterPanel({ appointmentId }: { appointmentId: string }) {
  const qc = useQueryClient();
  const key = ["appointments", appointmentId, "encounter"];
  const { data, isLoading } = useQuery({ queryKey: key, queryFn: () => encounterApi.get(appointmentId) });

  const [draft, setDraft] = useState<EncounterDraft>(EMPTY);
  const [dirty, setDirty] = useState(false);
  const [saveState, setSaveState] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [finalizeOpen, setFinalizeOpen] = useState(false);
  const [amendOpen, setAmendOpen] = useState(false);
  const [amending, setAmending] = useState(false);
  const loadedFor = useRef<string | null>(null);

  useEffect(() => {
    if (data && (loadedFor.current !== `${data.version}:${data.status}` || !dirty)) {
      if (!dirty) setDraft(toDraft(data));
      loadedFor.current = `${data.version}:${data.status}`;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data]);

  const isDoctor = !!data?.isDoctor;
  const isDraft = data?.status === "Draft";
  const editable = isDoctor && (isDraft || amending);

  // Local debounce autosave (draft status only).
  useEffect(() => {
    if (!dirty || !isDoctor || !isDraft) return;
    const t = setTimeout(async () => {
      setSaveState("saving");
      try { await encounterApi.saveDraft(appointmentId, draft); setSaveState("saved"); setDirty(false); }
      catch { setSaveState("error"); }
    }, 1500);
    return () => clearTimeout(t);
  }, [draft, dirty, isDoctor, isDraft, appointmentId]);

  const change = (patch: Partial<EncounterDraft>) => { setDraft((d) => ({ ...d, ...patch })); setDirty(true); setSaveState("idle"); };

  async function finalize() {
    try {
      await encounterApi.saveDraft(appointmentId, draft);
      const e = await encounterApi.finalize(appointmentId);
      qc.setQueryData(key, e); setDirty(false); setFinalizeOpen(false);
      toast.success("Record finalized and signed.");
    } catch (e) { toast.error(errMsg(e)); setFinalizeOpen(false); }
  }
  async function amend(reason: string) {
    try {
      const e = await encounterApi.amend(appointmentId, reason, draft);
      qc.setQueryData(key, e); setAmending(false); setDirty(false);
      toast.success("Amendment recorded.");
    } catch (e) { toast.error(errMsg(e)); }
  }

  if (isLoading || !data) return <Skeleton className="h-40" />;
  if (!isDoctor && data.id == null) return null; // owner: nothing finalized yet

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="flex items-center gap-2 text-base"><FileSignature className="h-4 w-4" /> Clinical record</CardTitle>
        <div className="flex items-center gap-2">
          {isDoctor && isDraft && <span className="text-xs text-muted-foreground" aria-live="polite">{saveState === "saving" ? "Saving…" : saveState === "saved" ? "Draft saved" : saveState === "error" ? "Autosave failed" : dirty ? "Unsaved changes" : ""}</span>}
          <Badge variant={isDraft ? "secondary" : "default"}>{data.status}{data.version > 1 ? ` v${data.version}` : ""}</Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <Field label="Chief complaint" value={draft.chiefComplaint} editable={editable} onChange={(v) => change({ chiefComplaint: v })} />
        <Field label="Findings" value={draft.findings} editable={editable} onChange={(v) => change({ findings: v })} rows={4} />
        <Field label="Diagnosis *" value={draft.diagnosis} editable={editable} onChange={(v) => change({ diagnosis: v })} />
        <CodesEditor codes={draft.diagnosisCodes} editable={editable} onChange={(c) => change({ diagnosisCodes: c })} />
        <Field label="Treatment plan *" value={draft.treatmentPlan} editable={editable} onChange={(v) => change({ treatmentPlan: v })} rows={4} />
        <LabsEditor labs={draft.labResults} editable={editable} onChange={(l) => change({ labResults: l })} />

        {data.signatureHash && (
          <p className="flex items-center gap-1 text-xs text-muted-foreground break-all">
            <ShieldCheck className="h-3.5 w-3.5 shrink-0" /> Signed {data.finalizedAt ? new Date(data.finalizedAt).toLocaleString() : ""} · SHA-256 {data.signatureHash}
          </p>
        )}
        {data.amendments.length > 0 && (
          <div className="text-xs"><p className="font-medium">Amendment history</p>
            <ul className="list-disc pl-5">{data.amendments.map((a) => <li key={a.version}>v{a.version} · {new Date(a.amendedAt).toLocaleString()} · {a.reason}</li>)}</ul></div>
        )}
        {data.referrals.length > 0 && (
          <div className="text-xs"><p className="font-medium">Referrals</p>
            <ul className="list-disc pl-5">{data.referrals.map((r) => <li key={r.id}>{r.toDoctorName ? `Dr. ${r.toDoctorName}` : r.externalClinic} · {r.status} · {r.reason}</li>)}</ul></div>
        )}

        {isDoctor && (
          <div className="flex flex-wrap gap-2 pt-1">
            {isDraft && <Button onClick={() => setFinalizeOpen(true)} disabled={!draft.diagnosis?.trim() || !draft.treatmentPlan?.trim()}>Finalize and sign</Button>}
            {!isDraft && !amending && <Button variant="outline" onClick={() => setAmending(true)}>Amend</Button>}
            {amending && <><Button onClick={() => setAmendOpen(true)}>Save amendment</Button><Button variant="ghost" onClick={() => { setAmending(false); setDraft(toDraft(data)); setDirty(false); }}>Cancel</Button></>}
            <FollowUpRequest appointmentId={appointmentId} />
          </div>
        )}
      </CardContent>

      <Dialog open={finalizeOpen} onOpenChange={setFinalizeOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Finalize clinical record?</DialogTitle>
            <DialogDescription>The record is signed with your identity and a SHA-256 hash, and this visit's prescriptions are sealed. Later changes need a documented amendment.</DialogDescription>
          </DialogHeader>
          <div className="flex gap-2 justify-end"><Button variant="ghost" onClick={() => setFinalizeOpen(false)}>Back</Button><Button onClick={finalize}>Finalize</Button></div>
        </DialogContent>
      </Dialog>
      <PromptDialog open={amendOpen} onOpenChange={setAmendOpen} title="Reason for amendment" description="Recorded permanently with the superseded version."
        label="Reason" multiline required confirmLabel="Record amendment" onSubmit={amend} />
    </Card>
  );
}

function Field({ label, value, editable, onChange, rows = 2 }: { label: string; value?: string | null; editable: boolean; onChange: (v: string) => void; rows?: number }) {
  const id = `enc-${label.replace(/\W+/g, "-")}`;
  return (
    <div className="space-y-1">
      <Label htmlFor={id}>{label}</Label>
      {editable ? <Textarea id={id} rows={rows} value={value ?? ""} onChange={(e) => onChange(e.target.value)} />
        : <p className="whitespace-pre-wrap rounded border bg-muted/30 px-2 py-1 min-h-[2rem]">{value || "—"}</p>}
    </div>
  );
}

function CodesEditor({ codes, editable, onChange }: { codes: DiagnosisCode[]; editable: boolean; onChange: (c: DiagnosisCode[]) => void }) {
  if (!editable && codes.length === 0) return null;
  return (
    <div className="space-y-1">
      <Label>Diagnosis codes (VeNom / SNOMED)</Label>
      {codes.map((c, i) => (
        <div key={i} className="flex gap-2 items-center">
          {editable ? <>
            <Input className="w-32" value={c.code} placeholder="Code" aria-label="Code" onChange={(e) => onChange(codes.map((x, j) => (j === i ? { ...x, code: e.target.value } : x)))} />
            <select className="rounded-md border bg-background px-2 py-2" value={c.system} aria-label="System" onChange={(e) => onChange(codes.map((x, j) => (j === i ? { ...x, system: e.target.value as DiagnosisCode["system"] } : x)))}>
              <option>VeNom</option><option>SNOMED</option><option>Other</option></select>
            <Input className="flex-1" value={c.label ?? ""} placeholder="Label" aria-label="Label" onChange={(e) => onChange(codes.map((x, j) => (j === i ? { ...x, label: e.target.value } : x)))} />
            <Button type="button" size="sm" variant="ghost" aria-label="Remove code" onClick={() => onChange(codes.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
          </> : <Badge variant="outline">{c.system}: {c.code}{c.label ? ` · ${c.label}` : ""}</Badge>}
        </div>
      ))}
      {editable && <Button type="button" size="sm" variant="outline" onClick={() => onChange([...codes, { code: "", system: "VeNom", label: "" }])}><Plus className="h-4 w-4 mr-1" /> Add code</Button>}
    </div>
  );
}

function LabsEditor({ labs, editable, onChange }: { labs: LabResult[]; editable: boolean; onChange: (l: LabResult[]) => void }) {
  if (!editable && labs.length === 0) return null;
  const upd = (i: number, p: Partial<LabResult>) => onChange(labs.map((x, j) => (j === i ? { ...x, ...p } : x)));
  return (
    <div className="space-y-1">
      <Label>Lab results</Label>
      {labs.map((l, i) => (
        <div key={i} className="flex flex-wrap gap-2 items-center">
          {editable ? <>
            <Input className="w-40" placeholder="Test" aria-label="Test" value={l.test} onChange={(e) => upd(i, { test: e.target.value })} />
            <Input className="w-24" placeholder="Value" aria-label="Value" value={l.value ?? ""} onChange={(e) => upd(i, { value: e.target.value })} />
            <Input className="w-20" placeholder="Unit" aria-label="Unit" value={l.unit ?? ""} onChange={(e) => upd(i, { unit: e.target.value })} />
            <Input className="w-28" placeholder="Ref. range" aria-label="Reference range" value={l.referenceRange ?? ""} onChange={(e) => upd(i, { referenceRange: e.target.value })} />
            <select className="rounded-md border bg-background px-2 py-2" aria-label="Flag" value={l.flag ?? ""} onChange={(e) => upd(i, { flag: e.target.value || null })}>
              <option value="">Flag</option><option>Normal</option><option>Low</option><option>High</option><option>Critical</option></select>
            <Button type="button" size="sm" variant="ghost" aria-label="Remove lab result" onClick={() => onChange(labs.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
          </> : <p>{l.test}: {l.value} {l.unit} {l.referenceRange ? `(ref ${l.referenceRange})` : ""} {l.flag && <Badge variant={l.flag === "Normal" ? "secondary" : "destructive"}>{l.flag}</Badge>}
            {l.documentUrl && <a className="underline ml-1" href={l.documentUrl} target="_blank" rel="noreferrer">report</a>}</p>}
        </div>
      ))}
      {editable && <Button type="button" size="sm" variant="outline" onClick={() => onChange([...labs, { test: "", value: "", unit: "", referenceRange: "", flag: null }])}><Plus className="h-4 w-4 mr-1" /> Add lab result</Button>}
    </div>
  );
}

function FollowUpRequest({ appointmentId }: { appointmentId: string }) {
  const [open, setOpen] = useState(false);
  const [days, setDays] = useState("14");
  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>Request follow-up</Button>
      <PromptDialog open={open} onOpenChange={setOpen} title="Request a follow-up visit" description="The owner is notified and can book directly."
        label="Days from now" inputType="number" min={1} max={365} initialValue={days} confirmLabel="Send request"
        onSubmit={async (v) => {
          setDays(v);
          try { await followUpApi.request(appointmentId, Number(v)); toast.success("Follow-up requested."); }
          catch (e) { toast.error(errMsg(e)); }
        }} />
    </>
  );
}
