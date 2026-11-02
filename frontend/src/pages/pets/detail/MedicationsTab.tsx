import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Pencil, Plus, SkipForward, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { toast } from "@/components/ui/sonner";
import { petsApi, canEditClinical, canLogCare, type Medication, type PetDetail, type TodayDose } from "@/api/pets";
import { RecordDialog, useSave } from "./HealthTab";
import { Section, Field, Muted, dateInput, orNull, fmtDate } from "./shared";

const doseTimesPattern = /^([01]\d|2[0-3]):[0-5]\d(\s*,\s*([01]\d|2[0-3]):[0-5]\d)*$/;

export function MedicationsTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const [editing, setEditing] = useState<Medication | "new" | null>(null);
  const [deleting, setDeleting] = useState<Medication | null>(null);
  const clinical = canEditClinical(pet.myAccess);
  const caretaker = canLogCare(pet.myAccess);

  const { data: meds = [], isLoading } = useQuery({ queryKey: ["pets", pet.id, "medications"], queryFn: () => petsApi.medications.list(pet.id) });
  const { data: today = [] } = useQuery({
    queryKey: ["pets", "doses-today"], queryFn: petsApi.medications.today, enabled: caretaker,
  });
  const todays = today.filter((d) => d.petId === pet.id);

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["pets", pet.id, "medications"] });
    qc.invalidateQueries({ queryKey: ["pets", "doses-today"] });
  };

  async function log(d: TodayDose, skipped: boolean) {
    await petsApi.medications.logDose(pet.id, d.medicationId, { scheduledFor: d.scheduledFor, skipped });
    toast.success(skipped ? "Dose skipped." : "Dose logged.");
    refresh();
  }

  return (
    <div className="space-y-4">
      {caretaker && (
        <Section title="Today's doses">
          {todays.length === 0 ? <Muted>No doses scheduled today.</Muted> : (
            <div className="divide-y">
              {todays.map((d) => (
                <div key={`${d.medicationId}-${d.scheduledFor}`} className="py-2 flex items-center justify-between gap-3">
                  <div>
                    <p className="font-medium text-sm">{d.drugName}{d.dose ? ` · ${d.dose}` : ""}</p>
                    <p className="text-xs text-muted-foreground">{new Date(d.scheduledFor).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</p>
                  </div>
                  {d.status === "taken" ? <Badge variant="success">Given</Badge>
                    : d.status === "skipped" ? <Badge variant="muted">Skipped</Badge>
                    : (
                      <div className="flex gap-1">
                        {d.status === "due" && <Badge variant="warning" className="mr-1">Due</Badge>}
                        <Button size="sm" onClick={() => log(d, false)}><Check className="h-3.5 w-3.5 mr-1" /> Given</Button>
                        <Button size="sm" variant="ghost" onClick={() => log(d, true)} aria-label="Skip dose"><SkipForward className="h-3.5 w-3.5" /></Button>
                      </div>
                    )}
                </div>
              ))}
            </div>
          )}
        </Section>
      )}

      <Section
        title="Medication courses"
        action={clinical && <Button size="sm" onClick={() => setEditing("new")}><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>}
      >
        {isLoading ? <Muted>Loading…</Muted> : meds.length === 0 ? (
          <Muted>No medications. Prescriptions uploaded by your vet are added here automatically.</Muted>
        ) : (
          <div className="divide-y">
            {meds.map((m) => (
              <div key={m.id} className="py-2.5 flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium flex items-center gap-2 flex-wrap">
                    {m.drugName}{m.dose ? <span className="text-muted-foreground font-normal">· {m.dose}</span> : null}
                    {!m.isActive && <Badge variant="muted">Stopped</Badge>}
                    {m.prescriptionId && <Badge variant="info">Prescribed</Badge>}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {m.doseTimes.replace(/,/g, ", ")} · {fmtDate(m.startDate)} → {m.endDate ? fmtDate(m.endDate) : "ongoing"}
                    {` · ${m.dosesTaken} given${m.dosesSkipped ? `, ${m.dosesSkipped} skipped` : ""}`}
                  </p>
                  {m.instructions && <p className="text-xs text-muted-foreground mt-0.5">{m.instructions}</p>}
                </div>
                {clinical && (
                  <div className="flex gap-1 shrink-0">
                    <Button size="icon" variant="ghost" onClick={() => setEditing(m)} aria-label="Edit"><Pencil className="h-4 w-4" /></Button>
                    <Button size="icon" variant="ghost" onClick={() => setDeleting(m)} aria-label="Delete"><Trash2 className="h-4 w-4" /></Button>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </Section>

      {editing && (
        <MedicationDialog petId={pet.id} record={editing === "new" ? undefined : editing} onClose={() => setEditing(null)} onSaved={refresh} />
      )}
      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={`Delete ${deleting?.drugName ?? "medication"}?`}
        description="Dose history for this course is removed too."
        confirmLabel="Delete"
        destructive
        onConfirm={async () => { if (deleting) { await petsApi.medications.remove(pet.id, deleting.id); toast.success("Removed."); refresh(); } }}
      />
    </div>
  );
}

function MedicationDialog({ petId, record, onClose, onSaved }: { petId: string; record?: Medication; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState({
    drugName: record?.drugName ?? "",
    dose: record?.dose ?? "",
    doseTimes: record?.doseTimes.replace(/,/g, ", ") ?? "09:00",
    startDate: dateInput(record?.startDate),
    endDate: record?.endDate ? dateInput(record.endDate) : "",
    instructions: record?.instructions ?? "",
    isActive: record?.isActive ?? true,
  });
  const { busy, run } = useSave(onSaved, onClose);
  const timesOk = doseTimesPattern.test(f.doseTimes.trim());

  const submit = () => {
    if (!f.drugName.trim()) return toast.error("Medicine name is required.");
    if (!timesOk) return toast.error("Dose times must look like 08:00, 20:00.");
    if (f.endDate && f.endDate < f.startDate) return toast.error("End date must be after the start date.");
    const input = {
      drugName: f.drugName.trim(), dose: orNull(f.dose), doseTimes: f.doseTimes.trim(), startDate: f.startDate,
      endDate: orNull(f.endDate), instructions: orNull(f.instructions), isActive: f.isActive,
    };
    run(() => record ? petsApi.medications.update(petId, record.id, input) : petsApi.medications.add(petId, input),
      record ? "Medication updated." : "Medication added.");
  };

  return (
    <RecordDialog title={record ? "Edit medication" : "Add medication"} busy={busy} onClose={onClose} onSubmit={submit}>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Medicine" htmlFor="dn"><Input id="dn" value={f.drugName} onChange={(e) => setF({ ...f, drugName: e.target.value })} /></Field>
        <Field label="Dose" htmlFor="dd"><Input id="dd" placeholder="1 tablet / 5 ml" value={f.dose} onChange={(e) => setF({ ...f, dose: e.target.value })} /></Field>
      </div>
      <Field label="Times each day" htmlFor="dt" error={!timesOk ? "Use 24h times separated by commas, e.g. 08:00, 20:00" : undefined}>
        <Input id="dt" value={f.doseTimes} onChange={(e) => setF({ ...f, doseTimes: e.target.value })} />
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Start" htmlFor="ds"><Input id="ds" type="date" value={f.startDate} onChange={(e) => setF({ ...f, startDate: e.target.value })} /></Field>
        <Field label="End (optional)" htmlFor="de"><Input id="de" type="date" min={f.startDate} value={f.endDate} onChange={(e) => setF({ ...f, endDate: e.target.value })} /></Field>
      </div>
      <Field label="Instructions" htmlFor="di"><Textarea id="di" rows={2} value={f.instructions} onChange={(e) => setF({ ...f, instructions: e.target.value })} /></Field>
      <label className="flex items-center gap-2 text-sm">
        <Checkbox checked={f.isActive} onChange={(e) => setF({ ...f, isActive: e.currentTarget.checked })} /> Active (send reminders)
      </label>
    </RecordDialog>
  );
}
