import { Fragment, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, FileCheck2, History, Plus, ShieldCheck, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { toast } from "@/components/ui/sonner";
import { canEditClinical, type PetDetail } from "@/api/pets";
import {
  healthPlusApi, type AllergyCategory, type AllergySeverity, type HealthRecordType, type LabResult,
} from "@/api/carePlans";
import { apiBase } from "@/api/client";
import { Section, Field, Muted, dateInput, fmtDate } from "./shared";
import { AmendRecordDialog } from "./AmendRecordDialog";

const sel = "h-9 rounded-md border bg-background px-2 text-sm";
const categories: AllergyCategory[] = ["Food", "Drug", "Environmental", "Other"];
const severities: AllergySeverity[] = ["Mild", "Moderate", "Severe", "LifeThreatening"];

/** Typed allergies / diet, lab results, record history + sealing, certificates and FHIR export. Mounted at the bottom of HealthTab. */
export function TypedHealthPanel({ pet }: { pet: PetDetail }) {
  return (
    <div className="space-y-4">
      <AllergiesDiet pet={pet} />
      <Labs pet={pet} />
      <RecordIntegrity pet={pet} />
    </div>
  );
}

function AllergiesDiet({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const clinical = canEditClinical(pet.myAccess);
  const [q, setQ] = useState("");
  const { data: allergies = [] } = useQuery({ queryKey: ["pets", pet.id, "allergies", q], queryFn: () => healthPlusApi.allergies(pet.id, q) });
  const { data: diet = [] } = useQuery({ queryKey: ["pets", pet.id, "diet"], queryFn: () => healthPlusApi.diet(pet.id) });
  const [a, setA] = useState({ allergen: "", category: "Food" as AllergyCategory, severity: "Mild" as AllergySeverity, reaction: "" });
  const [d, setD] = useState({ food: "", amount: "", frequency: "" });
  const refresh = () => { qc.invalidateQueries({ queryKey: ["pets", pet.id, "allergies"] }); qc.invalidateQueries({ queryKey: ["pets", pet.id, "diet"] }); };

  return (
    <Section title="Allergies & diet (searchable)" action={clinical && (pet.allergies || pet.dietNotes) && (
      <Button size="sm" variant="outline" onClick={async () => {
        const r = await healthPlusApi.importText(pet.id);
        toast.success(`Imported ${r.allergiesCreated} allergies and ${r.dietEntriesCreated} diet entries (${r.skipped} already present).`);
        refresh();
      }}>Import from notes</Button>
    )}>
      <div className="grid md:grid-cols-2 gap-6">
        <div className="space-y-2">
          <Input aria-label="Search allergies" placeholder="Search allergies…" value={q} onChange={(e) => setQ(e.target.value)} />
          {allergies.length === 0 ? <Muted>No typed allergies.</Muted> : (
            <ul className="divide-y text-sm">
              {allergies.map((x) => (
                <li key={x.id} className="py-1.5 flex items-center justify-between gap-2">
                  <div className={x.active ? "" : "text-muted-foreground line-through"}>
                    <span className="font-medium">{x.allergen}</span> <Badge variant="outline">{x.category}</Badge>{" "}
                    <Badge variant={x.severity === "Mild" ? "muted" : "destructive"}>{x.severity}</Badge>
                    {x.reaction && <p className="text-xs text-muted-foreground">{x.reaction}</p>}
                  </div>
                  {clinical && (
                    <div className="flex">
                      <Button size="sm" variant="ghost" onClick={async () => { await healthPlusApi.updateAllergy(pet.id, x.id, { ...x, active: !x.active }); refresh(); }}>{x.active ? "Resolve" : "Reactivate"}</Button>
                      <Button size="icon" variant="ghost" aria-label={`Delete ${x.allergen}`} onClick={async () => { await healthPlusApi.deleteAllergy(pet.id, x.id); refresh(); }}><Trash2 className="h-4 w-4" /></Button>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
          {clinical && (
            <form className="flex flex-wrap gap-2" onSubmit={async (e) => {
              e.preventDefault();
              if (!a.allergen.trim()) return;
              await healthPlusApi.addAllergy(pet.id, { ...a, reaction: a.reaction || null, active: true });
              setA({ ...a, allergen: "", reaction: "" }); refresh();
            }}>
              <Input className="w-40" aria-label="Allergen" placeholder="Allergen" value={a.allergen} onChange={(e) => setA({ ...a, allergen: e.target.value })} />
              <select aria-label="Category" className={sel} value={a.category} onChange={(e) => setA({ ...a, category: e.target.value as AllergyCategory })}>{categories.map((c) => <option key={c}>{c}</option>)}</select>
              <select aria-label="Severity" className={sel} value={a.severity} onChange={(e) => setA({ ...a, severity: e.target.value as AllergySeverity })}>{severities.map((c) => <option key={c}>{c}</option>)}</select>
              <Input className="w-40" aria-label="Reaction" placeholder="Reaction" value={a.reaction} onChange={(e) => setA({ ...a, reaction: e.target.value })} />
              <Button size="sm" type="submit"><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>
            </form>
          )}
        </div>
        <div className="space-y-2">
          {diet.length === 0 ? <Muted>No typed diet entries.</Muted> : (
            <ul className="divide-y text-sm">
              {diet.map((x) => (
                <li key={x.id} className="py-1.5 flex items-center justify-between gap-2">
                  <span><span className="font-medium">{x.food}</span>{x.amount ? ` · ${x.amount}` : ""}{x.frequency ? ` · ${x.frequency}` : ""}</span>
                  {clinical && <Button size="icon" variant="ghost" aria-label={`Delete ${x.food}`} onClick={async () => { await healthPlusApi.deleteDiet(pet.id, x.id); refresh(); }}><Trash2 className="h-4 w-4" /></Button>}
                </li>
              ))}
            </ul>
          )}
          {clinical && (
            <form className="flex flex-wrap gap-2" onSubmit={async (e) => {
              e.preventDefault();
              if (!d.food.trim()) return;
              await healthPlusApi.addDiet(pet.id, { food: d.food, amount: d.amount || null, frequency: d.frequency || null, active: true });
              setD({ food: "", amount: "", frequency: "" }); refresh();
            }}>
              <Input className="w-40" aria-label="Food or brand" placeholder="Food / brand" value={d.food} onChange={(e) => setD({ ...d, food: e.target.value })} />
              <Input className="w-24" aria-label="Amount" placeholder="Amount" value={d.amount} onChange={(e) => setD({ ...d, amount: e.target.value })} />
              <Input className="w-28" aria-label="Frequency" placeholder="Frequency" value={d.frequency} onChange={(e) => setD({ ...d, frequency: e.target.value })} />
              <Button size="sm" type="submit"><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>
            </form>
          )}
        </div>
      </div>
    </Section>
  );
}

const flagVariant = { Normal: "success", Low: "warning", High: "warning", Critical: "destructive", Unknown: "muted" } as const;

function Labs({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const clinical = canEditClinical(pet.myAccess);
  const { data: labs = [] } = useQuery({ queryKey: ["pets", pet.id, "labs"], queryFn: () => healthPlusApi.labs(pet.id) });
  const [editing, setEditing] = useState<LabResult | "new" | null>(null);
  const [docUrl, setDocUrl] = useState<string | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: ["pets", pet.id, "labs"] });

  return (
    <Section title="Lab results" action={clinical && (
      <div className="flex gap-2">
        <FileUploadButton label="Upload report" size="sm" variant="outline" accept="image/*,application/pdf" onUploaded={(url) => { setDocUrl(url); setEditing("new"); }} />
        <Button size="sm" onClick={() => { setDocUrl(null); setEditing("new"); }}><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>
      </div>
    )}>
      {labs.length === 0 ? <Muted>No lab results.</Muted> : (
        <div className="overflow-x-auto"><table className="w-full text-sm">
          <thead><tr className="text-left text-xs text-muted-foreground"><th className="py-1">Test</th><th>Value</th><th>Reference</th><th>Date</th><th /></tr></thead>
          <tbody>
            {labs.map((l) => (
              <tr key={l.id} className="border-t align-top">
                <td className="py-1.5 font-medium">{l.testName}</td>
                <td>{l.value ? `${l.value} ${l.unit ?? ""}` : "—"} {l.value && <Badge variant={flagVariant[l.flag]}>{l.flag}</Badge>}</td>
                <td>{l.referenceRange ?? "—"}</td>
                <td>{fmtDate(l.takenAt)}</td>
                <td className="text-right space-x-1">
                  {l.documentUrl && <a className="text-xs underline" href={l.documentUrl} target="_blank" rel="noreferrer">Document</a>}
                  {l.needsTranscription && <Badge variant="warning" title={l.statusNote ?? ""}>OCR unavailable — transcribe</Badge>}
                  {clinical && <Button size="sm" variant="ghost" onClick={() => setEditing(l)}>{l.needsTranscription ? "Transcribe" : "Edit"}</Button>}
                  {clinical && <Button size="icon" variant="ghost" aria-label="Delete lab result" onClick={async () => { await healthPlusApi.deleteLab(pet.id, l.id); refresh(); }}><Trash2 className="h-4 w-4" /></Button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table></div>
      )}
      <p className="text-xs text-muted-foreground mt-2">Automatic text extraction (OCR) is not available on this deployment — uploaded reports are kept as documents and you enter the values manually.</p>
      {editing && <LabDialog petId={pet.id} lab={editing === "new" ? undefined : editing} docUrl={docUrl} onClose={() => setEditing(null)} onSaved={refresh} />}
    </Section>
  );
}

function LabDialog({ petId, lab, docUrl, onClose, onSaved }: { petId: string; lab?: LabResult; docUrl: string | null; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState({
    testName: lab?.testName ?? "", value: lab?.value ?? "", unit: lab?.unit ?? "", referenceRange: lab?.referenceRange ?? "",
    takenAt: dateInput(lab?.takenAt), documentUrl: lab?.documentUrl ?? docUrl ?? "",
  });
  const [busy, setBusy] = useState(false);
  async function save() {
    if (!f.testName.trim()) return toast.error("Test name is required.");
    setBusy(true);
    const body = { testName: f.testName, value: f.value || null, unit: f.unit || null, referenceRange: f.referenceRange || null, takenAt: f.takenAt, documentUrl: f.documentUrl || null };
    try { if (lab) await healthPlusApi.updateLab(petId, lab.id, body); else await healthPlusApi.addLab(petId, body); onSaved(); onClose(); }
    catch { /* toasted */ } finally { setBusy(false); }
  }
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value });
  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>{lab ? "Lab result" : "Add lab result"}</DialogTitle></DialogHeader>
        {f.documentUrl && <a className="text-sm underline" href={f.documentUrl} target="_blank" rel="noreferrer">Open attached document</a>}
        <div className="grid grid-cols-2 gap-3">
          <Field label="Test" htmlFor="lab-test"><Input id="lab-test" value={f.testName} onChange={set("testName")} /></Field>
          <Field label="Date" htmlFor="lab-date"><Input id="lab-date" type="date" value={f.takenAt} onChange={set("takenAt")} /></Field>
          <Field label="Value" htmlFor="lab-val"><Input id="lab-val" value={f.value} onChange={set("value")} /></Field>
          <Field label="Unit" htmlFor="lab-unit"><Input id="lab-unit" value={f.unit} onChange={set("unit")} /></Field>
          <Field label="Reference range (e.g. 10-20)" htmlFor="lab-ref"><Input id="lab-ref" value={f.referenceRange} onChange={set("referenceRange")} /></Field>
        </div>
        <DialogFooter><Button variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={busy} onClick={save}>Save</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function RecordIntegrity({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const { data: seals = [] } = useQuery({ queryKey: ["pets", pet.id, "seals"], queryFn: () => healthPlusApi.seals(pet.id) });
  const [hist, setHist] = useState<{ type: HealthRecordType; id: string; label: string } | null>(null);
  const sealed = new Set(seals.map((s) => `${s.recordType}:${s.recordId}`));
  const rows: { type: HealthRecordType; id: string; label: string }[] = [
    ...pet.vaccinations.map((v) => ({ type: "Vaccination" as const, id: v.id, label: `${v.vaccineName} · ${fmtDate(v.administeredOn)}` })),
    ...pet.medicalRecords.map((m) => ({ type: "Medical" as const, id: m.id, label: `${m.diagnosis ?? "Visit"} · ${fmtDate(m.visitDate)}` })),
  ];

  return (
    <Section title="Record history, vet seals & exports" action={
      <div className="flex gap-2">
        <Button size="sm" variant="outline" onClick={async () => {
          try {
            const c = await healthPlusApi.issueCertificate(pet.id);
            window.open(new URL(`${apiBase}${c.certificatePath}`, window.location.origin).toString(), "_blank");
          } catch { /* toasted */ }
        }}><FileCheck2 className="h-3.5 w-3.5 mr-1" /> Vaccination certificate</Button>
        <Button size="sm" variant="outline" onClick={() => healthPlusApi.downloadFhir(pet.id, pet.name).catch(() => undefined)}><Download className="h-3.5 w-3.5 mr-1" /> FHIR export</Button>
      </div>
    }>
      {rows.length === 0 ? <Muted>No records yet.</Muted> : (
        <ul className="divide-y text-sm">
          {rows.map((r) => (
            <li key={`${r.type}${r.id}`} className="py-1.5 flex items-center justify-between gap-2">
              <span>{r.label} <Badge variant="outline">{r.type}</Badge> {sealed.has(`${r.type}:${r.id}`) && <Badge variant="success"><ShieldCheck className="h-3 w-3 mr-0.5 inline" />Vet-sealed</Badge>}</span>
              <Button size="sm" variant="ghost" onClick={() => setHist(r)}><History className="h-3.5 w-3.5 mr-1" /> History</Button>
            </li>
          ))}
        </ul>
      )}
      <p className="text-xs text-muted-foreground mt-2">The certificate opens a printable page (use Print → Save as PDF). It is not a PDF/A archival document.</p>
      {hist && <HistoryDialog pet={pet} target={hist} onClose={() => setHist(null)} onChanged={() => qc.invalidateQueries({ queryKey: ["pets", pet.id, "seals"] })} />}
    </Section>
  );
}

function HistoryDialog({ pet, target, onClose, onChanged }: {
  pet: PetDetail; target: { type: HealthRecordType; id: string; label: string }; onClose: () => void; onChanged: () => void;
}) {
  const { data, refetch } = useQuery({ queryKey: ["pets", pet.id, "history", target.type, target.id], queryFn: () => healthPlusApi.history(pet.id, target.type, target.id) });
  const [busy, setBusy] = useState(false);
  const canSeal = pet.myAccess === "Vet" && !data?.sealed;
  const canAmend = canEditClinical(pet.myAccess) && !!data?.sealed;
  const [amending, setAmending] = useState(false);

  async function seal() {
    if (!window.confirm("Seal this record? It can no longer be edited, only amended with a reason.")) return;
    setBusy(true);
    try { await healthPlusApi.seal(pet.id, target.type, target.id); toast.success("Record sealed."); await refetch(); onChanged(); }
    catch { /* toasted */ } finally { setBusy(false); }
  }

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader><DialogTitle>History — {target.label}</DialogTitle></DialogHeader>
        <div className="space-y-3 max-h-[60vh] overflow-y-auto pr-1 text-sm">
          {data?.seal && (
            <div className="rounded-md border p-2">
              <p><ShieldCheck className="h-4 w-4 inline mr-1 text-green-600" />Sealed by {data.seal.sealedByName ?? "a vet"} on {new Date(data.seal.sealedAt).toLocaleString()}</p>
              <p className="text-xs text-muted-foreground break-all">SHA-256 {data.seal.signatureHash} · signature {data.seal.signatureValid ? "valid" : "INVALID"}{data.seal.changedSinceSeal ? " · amended since sealing" : ""}</p>
            </div>
          )}
          {!data || data.versions.length === 0 ? <Muted>No earlier versions — this record has not been changed.</Muted> : data.versions.map((v) => (
            <div key={v.version} className="rounded-md border p-2">
              <p className="font-medium">Version {v.version} <span className="font-normal text-muted-foreground">— replaced {new Date(v.changedAt).toLocaleString()} by {v.changedByName ?? "unknown"}</span></p>
              {v.changeReason && <p className="text-xs">Reason: {v.changeReason}</p>}
              <dl className="mt-1 grid grid-cols-[8rem_1fr] gap-x-2 text-xs">
                {Object.entries(v.snapshot).filter(([k, val]) => val && k !== "petId").map(([k, val]) => (<Fragment key={k}><dt className="text-muted-foreground">{k}</dt><dd className="break-words">{val}</dd></Fragment>))}
              </dl>
            </div>
          ))}
        </div>
        <DialogFooter>
          {canAmend && <Button variant="outline" onClick={() => setAmending(true)}>Amend (reason required)</Button>}
          {canSeal && <Button variant="outline" disabled={busy} onClick={seal}><ShieldCheck className="h-4 w-4 mr-1" /> Seal as treating vet</Button>}
          <Button onClick={onClose}>Close</Button>
        </DialogFooter>
      </DialogContent>
      {amending && <AmendRecordDialog pet={pet} type={target.type} recordId={target.id} onClose={() => setAmending(false)} onAmended={() => { refetch(); onChanged(); }} />}
    </Dialog>
  );
}
