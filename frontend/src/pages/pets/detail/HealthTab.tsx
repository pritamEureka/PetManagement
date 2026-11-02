import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { FileText, Pencil, Plus, Trash2 } from "lucide-react";
import { Badge, type BadgeVariant } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { toast } from "@/components/ui/sonner";
import {
  petsApi, canEditClinical, canEditPet,
  type PetDetail, type Vaccination, type MedicalRecord, type GroomingRecord, type VaccineScheduleItem
} from "@/api/pets";
import { Section, Field, Muted, dateInput, orNull, fmtDate } from "./shared";

type Editing =
  | { kind: "vaccination"; record?: Vaccination; preset?: string }
  | { kind: "medical"; record?: MedicalRecord }
  | { kind: "grooming"; record?: GroomingRecord };

type Deleting = { kind: Editing["kind"]; id: string; label: string };

const statusVariant: Record<VaccineScheduleItem["status"], BadgeVariant> = {
  given: "success", overdue: "destructive", due: "warning", upcoming: "muted", unknown: "outline"
};

export function HealthTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const clinical = canEditClinical(pet.myAccess);
  const editor = canEditPet(pet.myAccess);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [deleting, setDeleting] = useState<Deleting | null>(null);

  const { data: schedule = [] } = useQuery({
    queryKey: ["pets", pet.id, "vaccine-schedule"],
    queryFn: () => petsApi.vaccineSchedule(pet.id),
  });

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["pets", pet.id] });
    qc.invalidateQueries({ queryKey: ["pets", "mine"] });
  };

  async function confirmDelete() {
    if (!deleting) return;
    const api = deleting.kind === "vaccination" ? petsApi.vaccinations : deleting.kind === "medical" ? petsApi.medical : petsApi.grooming;
    await api.remove(pet.id, deleting.id);
    toast.success("Record removed.");
    refresh();
  }

  const latestByName = new Map<string, Vaccination>();
  for (const v of pet.vaccinations) {
    const k = v.vaccineName.trim().toLowerCase();
    const prev = latestByName.get(k);
    if (!prev || prev.administeredOn < v.administeredOn) latestByName.set(k, v);
  }
  const today = new Date().toISOString();

  return (
    <div className="space-y-4">
      <Section
        title="Vaccinations"
        action={clinical && <Button size="sm" onClick={() => setEditing({ kind: "vaccination" })}><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>}
      >
        {pet.vaccinations.length === 0 ? <Muted>No vaccinations recorded yet.</Muted> : (
          <div className="divide-y">
            {pet.vaccinations.map((v) => {
              const isLatest = latestByName.get(v.vaccineName.trim().toLowerCase())?.id === v.id;
              const overdue = isLatest && v.nextDueOn && v.nextDueOn < today;
              return (
                <div key={v.id} className="py-2.5 flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="font-medium flex items-center gap-2 flex-wrap">
                      {v.vaccineName}
                      {overdue && <Badge variant="destructive">Overdue</Badge>}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Given {fmtDate(v.administeredOn)}{v.administeredByVet ? ` by ${v.administeredByVet}` : ""}
                      {v.nextDueOn ? ` · next due ${fmtDate(v.nextDueOn)}` : ""}
                    </p>
                    {v.notes && <p className="text-xs text-muted-foreground mt-0.5">{v.notes}</p>}
                  </div>
                  <RowActions
                    fileUrl={v.certificateUrl}
                    canEdit={clinical}
                    onEdit={() => setEditing({ kind: "vaccination", record: v })}
                    onDelete={() => setDeleting({ kind: "vaccination", id: v.id, label: v.vaccineName })}
                  />
                </div>
              );
            })}
          </div>
        )}
      </Section>

      {schedule.length > 0 && (
        <Section title="Recommended schedule">
          <p className="text-xs text-muted-foreground mb-3">
            General guideline for {pet.animalType.toLowerCase()}s{pet.birthDate ? " based on birth date" : " — add a birth date to see due dates"}. Always confirm with your vet.
          </p>
          <div className="divide-y">
            {schedule.map((s, i) => (
              <div key={i} className="py-2 flex items-center justify-between gap-3 text-sm">
                <div className="min-w-0">
                  <p className="font-medium">{s.vaccineName}</p>
                  <p className="text-xs text-muted-foreground">
                    {s.ageWeeks === 0 ? "At birth" : `${s.ageWeeks} weeks`}
                    {s.dueOn ? ` · ${fmtDate(s.dueOn)}` : ""}{s.givenOn ? ` · given ${fmtDate(s.givenOn)}` : ""}
                    {s.notes ? ` · ${s.notes}` : ""}
                  </p>
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <Badge variant={statusVariant[s.status]}>{s.status === "unknown" ? "No date" : s.status}</Badge>
                  {clinical && s.status !== "given" && (
                    <Button size="sm" variant="ghost" onClick={() => setEditing({ kind: "vaccination", preset: s.vaccineName })}>
                      Mark given
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </Section>
      )}

      <Section
        title="Vet visits & medical history"
        action={clinical && <Button size="sm" onClick={() => setEditing({ kind: "medical" })}><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>}
      >
        {pet.medicalRecords.length === 0 ? <Muted>No medical records. Completed vet appointments appear here automatically.</Muted> : (
          <div className="divide-y">
            {pet.medicalRecords.map((m) => (
              <div key={m.id} className="py-2.5 flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium flex items-center gap-2 flex-wrap">
                    {m.diagnosis ?? "Visit"}
                    {m.appointmentId && <Badge variant="info">From appointment</Badge>}
                  </p>
                  <p className="text-xs text-muted-foreground">{fmtDate(m.visitDate)}{m.vetName ? ` · ${m.vetName}` : ""}</p>
                  {m.treatment && <p className="text-xs mt-0.5"><span className="text-muted-foreground">Treatment:</span> {m.treatment}</p>}
                  {m.notes && <p className="text-xs text-muted-foreground mt-0.5">{m.notes}</p>}
                  {m.prescriptionFileUrl && (
                    <a className="text-xs text-primary underline" href={m.prescriptionFileUrl} target="_blank" rel="noreferrer">Prescription</a>
                  )}
                </div>
                <RowActions
                  fileUrl={m.attachmentUrl}
                  canEdit={clinical}
                  canDelete={clinical && !m.appointmentId}
                  onEdit={() => setEditing({ kind: "medical", record: m })}
                  onDelete={() => setDeleting({ kind: "medical", id: m.id, label: m.diagnosis ?? "this visit" })}
                />
              </div>
            ))}
          </div>
        )}
      </Section>

      <Section
        title="Grooming"
        action={editor && <Button size="sm" onClick={() => setEditing({ kind: "grooming" })}><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>}
      >
        {pet.groomingRecords.length === 0 ? <Muted>No grooming visits yet.</Muted> : (
          <div className="divide-y">
            {pet.groomingRecords.map((g) => (
              <div key={g.id} className="py-2.5 flex items-start justify-between gap-3">
                <div>
                  <p className="font-medium">{g.serviceType}</p>
                  <p className="text-xs text-muted-foreground">
                    {fmtDate(g.performedOn)}{g.provider ? ` · ${g.provider}` : ""}{g.cost ? ` · ${g.cost}` : ""}
                  </p>
                  {g.notes && <p className="text-xs text-muted-foreground mt-0.5">{g.notes}</p>}
                </div>
                <RowActions
                  canEdit={editor && !g.serviceBookingId}
                  onEdit={() => setEditing({ kind: "grooming", record: g })}
                  onDelete={() => setDeleting({ kind: "grooming", id: g.id, label: g.serviceType })}
                />
              </div>
            ))}
          </div>
        )}
      </Section>

      {editing?.kind === "vaccination" && (
        <VaccinationDialog petId={pet.id} record={editing.record} preset={editing.preset} onClose={() => setEditing(null)} onSaved={refresh} />
      )}
      {editing?.kind === "medical" && (
        <MedicalDialog petId={pet.id} record={editing.record} onClose={() => setEditing(null)} onSaved={refresh} />
      )}
      {editing?.kind === "grooming" && (
        <GroomingDialog petId={pet.id} record={editing.record} onClose={() => setEditing(null)} onSaved={refresh} />
      )}

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={`Delete ${deleting?.label ?? "record"}?`}
        description="This removes the record from the pet's health history."
        confirmLabel="Delete"
        destructive
        onConfirm={confirmDelete}
      />
    </div>
  );
}

function RowActions({ fileUrl, canEdit, canDelete = canEdit, onEdit, onDelete }: {
  fileUrl?: string | null; canEdit: boolean; canDelete?: boolean; onEdit: () => void; onDelete: () => void;
}) {
  return (
    <div className="flex items-center gap-1 shrink-0">
      {fileUrl && (
        <Button size="icon" variant="ghost" asChild aria-label="Open document">
          <a href={fileUrl} target="_blank" rel="noreferrer"><FileText className="h-4 w-4" /></a>
        </Button>
      )}
      {canEdit && <Button size="icon" variant="ghost" onClick={onEdit} aria-label="Edit"><Pencil className="h-4 w-4" /></Button>}
      {canDelete && <Button size="icon" variant="ghost" onClick={onDelete} aria-label="Delete"><Trash2 className="h-4 w-4" /></Button>}
    </div>
  );
}

function RecordDialog({ title, busy, onClose, onSubmit, children }: {
  title: string; busy: boolean; onClose: () => void; onSubmit: () => void; children: React.ReactNode;
}) {
  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader><DialogTitle>{title}</DialogTitle></DialogHeader>
        <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); onSubmit(); }}>
          {children}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={busy}>{busy ? "Saving..." : "Save"}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function useSave(onSaved: () => void, onClose: () => void) {
  const [busy, setBusy] = useState(false);
  return {
    busy,
    run: async (fn: () => Promise<unknown>, msg: string) => {
      setBusy(true);
      try { await fn(); toast.success(msg); onSaved(); onClose(); }
      catch { /* interceptor toasts the API error */ }
      finally { setBusy(false); }
    },
  };
}

function VaccinationDialog({ petId, record, preset, onClose, onSaved }: {
  petId: string; record?: Vaccination; preset?: string; onClose: () => void; onSaved: () => void;
}) {
  const [f, setF] = useState({
    vaccineName: record?.vaccineName ?? preset ?? "",
    administeredOn: dateInput(record?.administeredOn),
    nextDueOn: record?.nextDueOn ? dateInput(record.nextDueOn) : "",
    administeredByVet: record?.administeredByVet ?? "",
    notes: record?.notes ?? "",
    certificateUrl: record?.certificateUrl ?? "",
  });
  const { busy, run } = useSave(onSaved, onClose);
  const invalidDue = !!f.nextDueOn && f.nextDueOn <= f.administeredOn;

  const submit = () => {
    if (!f.vaccineName.trim()) return toast.error("Vaccine name is required.");
    if (invalidDue) return toast.error("Next due date must be after the date given.");
    const input = {
      vaccineName: f.vaccineName.trim(), administeredOn: f.administeredOn, nextDueOn: orNull(f.nextDueOn),
      administeredByVet: orNull(f.administeredByVet), notes: orNull(f.notes), certificateUrl: orNull(f.certificateUrl),
    };
    run(() => record ? petsApi.vaccinations.update(petId, record.id, input) : petsApi.vaccinations.add(petId, input),
      record ? "Vaccination updated." : "Vaccination recorded.");
  };

  return (
    <RecordDialog title={record ? "Edit vaccination" : "Record vaccination"} busy={busy} onClose={onClose} onSubmit={submit}>
      <Field label="Vaccine" htmlFor="vn"><Input id="vn" value={f.vaccineName} onChange={(e) => setF({ ...f, vaccineName: e.target.value })} /></Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Given on" htmlFor="va">
          <Input id="va" type="date" max={dateInput()} value={f.administeredOn} onChange={(e) => setF({ ...f, administeredOn: e.target.value })} />
        </Field>
        <Field label="Next due (optional)" htmlFor="vd" error={invalidDue ? "Must be after the date given" : undefined}>
          <Input id="vd" type="date" value={f.nextDueOn} onChange={(e) => setF({ ...f, nextDueOn: e.target.value })} />
        </Field>
      </div>
      <Field label="Given by (vet / clinic)" htmlFor="vb"><Input id="vb" value={f.administeredByVet} onChange={(e) => setF({ ...f, administeredByVet: e.target.value })} /></Field>
      <Field label="Notes" htmlFor="vno"><Textarea id="vno" rows={2} value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
      <div className="flex items-center gap-2">
        <FileUploadButton accept="image/*,application/pdf" label={f.certificateUrl ? "Replace certificate" : "Attach certificate"}
          onUploaded={(url) => setF({ ...f, certificateUrl: url })} />
        {f.certificateUrl && <span className="text-xs text-muted-foreground">Certificate attached</span>}
      </div>
    </RecordDialog>
  );
}

function MedicalDialog({ petId, record, onClose, onSaved }: {
  petId: string; record?: MedicalRecord; onClose: () => void; onSaved: () => void;
}) {
  const [f, setF] = useState({
    visitDate: dateInput(record?.visitDate),
    diagnosis: record?.diagnosis ?? "",
    treatment: record?.treatment ?? "",
    notes: record?.notes ?? "",
    vetName: record?.vetName ?? "",
    attachmentUrl: record?.attachmentUrl ?? "",
  });
  const { busy, run } = useSave(onSaved, onClose);

  const submit = () => {
    if (!f.diagnosis.trim() && !f.treatment.trim() && !f.notes.trim()) return toast.error("Add a diagnosis, treatment or notes.");
    const input = {
      visitDate: f.visitDate, diagnosis: orNull(f.diagnosis), treatment: orNull(f.treatment), notes: orNull(f.notes),
      vetName: orNull(f.vetName), attachmentUrl: orNull(f.attachmentUrl),
    };
    run(() => record ? petsApi.medical.update(petId, record.id, input) : petsApi.medical.add(petId, input),
      record ? "Record updated." : "Record added.");
  };

  return (
    <RecordDialog title={record ? "Edit medical record" : "Add medical record"} busy={busy} onClose={onClose} onSubmit={submit}>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Visit date" htmlFor="md">
          <Input id="md" type="date" max={dateInput()} value={f.visitDate} onChange={(e) => setF({ ...f, visitDate: e.target.value })} />
        </Field>
        <Field label="Vet / clinic" htmlFor="mv"><Input id="mv" value={f.vetName} onChange={(e) => setF({ ...f, vetName: e.target.value })} /></Field>
      </div>
      <Field label="Diagnosis" htmlFor="mdi"><Input id="mdi" value={f.diagnosis} onChange={(e) => setF({ ...f, diagnosis: e.target.value })} /></Field>
      <Field label="Treatment" htmlFor="mt"><Textarea id="mt" rows={2} value={f.treatment} onChange={(e) => setF({ ...f, treatment: e.target.value })} /></Field>
      <Field label="Notes" htmlFor="mn"><Textarea id="mn" rows={2} value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
      <div className="flex items-center gap-2">
        <FileUploadButton accept="image/*,application/pdf" label={f.attachmentUrl ? "Replace report" : "Attach lab report"}
          onUploaded={(url) => setF({ ...f, attachmentUrl: url })} />
        {f.attachmentUrl && <span className="text-xs text-muted-foreground">File attached</span>}
      </div>
    </RecordDialog>
  );
}

function GroomingDialog({ petId, record, onClose, onSaved }: {
  petId: string; record?: GroomingRecord; onClose: () => void; onSaved: () => void;
}) {
  const [f, setF] = useState({
    performedOn: dateInput(record?.performedOn),
    serviceType: record?.serviceType ?? "",
    provider: record?.provider ?? "",
    notes: record?.notes ?? "",
    cost: record?.cost?.toString() ?? "",
  });
  const { busy, run } = useSave(onSaved, onClose);

  const submit = () => {
    if (!f.serviceType.trim()) return toast.error("Service is required.");
    const cost = f.cost ? Number(f.cost) : null;
    if (cost !== null && (isNaN(cost) || cost < 0)) return toast.error("Cost must be a positive number.");
    const input = { performedOn: f.performedOn, serviceType: f.serviceType.trim(), provider: orNull(f.provider), notes: orNull(f.notes), cost };
    run(() => record ? petsApi.grooming.update(petId, record.id, input) : petsApi.grooming.add(petId, input),
      record ? "Grooming updated." : "Grooming recorded.");
  };

  return (
    <RecordDialog title={record ? "Edit grooming" : "Add grooming"} busy={busy} onClose={onClose} onSubmit={submit}>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Date" htmlFor="gd">
          <Input id="gd" type="date" max={dateInput()} value={f.performedOn} onChange={(e) => setF({ ...f, performedOn: e.target.value })} />
        </Field>
        <Field label="Cost (optional)" htmlFor="gc">
          <Input id="gc" type="number" min="0" step="0.01" value={f.cost} onChange={(e) => setF({ ...f, cost: e.target.value })} />
        </Field>
      </div>
      <Field label="Service" htmlFor="gs"><Input id="gs" placeholder="Bath & nail trim" value={f.serviceType} onChange={(e) => setF({ ...f, serviceType: e.target.value })} /></Field>
      <Field label="Provider" htmlFor="gp"><Input id="gp" value={f.provider} onChange={(e) => setF({ ...f, provider: e.target.value })} /></Field>
      <Field label="Notes" htmlFor="gn"><Textarea id="gn" rows={2} value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
    </RecordDialog>
  );
}

export { RecordDialog, useSave };
