import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import type { PetDetail } from "@/api/pets";
import { healthPlusApi, type HealthRecordType } from "@/api/carePlans";
import { Field, dateInput, orNull } from "./shared";

/**
 * Amend a vet-sealed record. The server keeps the previous version in the record history and requires a reason (>= 5 chars).
 * Only mounted for people who may edit clinical data.
 */
export function AmendRecordDialog({ pet, type, recordId, onClose, onAmended }: {
  pet: PetDetail; type: HealthRecordType; recordId: string; onClose: () => void; onAmended: () => void;
}) {
  const qc = useQueryClient();
  const vac = type === "Vaccination" ? pet.vaccinations.find((v) => v.id === recordId) : undefined;
  const med = type === "Medical" ? pet.medicalRecords.find((m) => m.id === recordId) : undefined;
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [v, setV] = useState({
    vaccineName: vac?.vaccineName ?? "", administeredOn: dateInput(vac?.administeredOn), nextDueOn: vac?.nextDueOn ? dateInput(vac.nextDueOn) : "",
    administeredByVet: vac?.administeredByVet ?? "", notes: vac?.notes ?? "",
  });
  const [m, setM] = useState({
    visitDate: dateInput(med?.visitDate), diagnosis: med?.diagnosis ?? "", treatment: med?.treatment ?? "", notes: med?.notes ?? "", vetName: med?.vetName ?? "",
  });
  const reasonOk = reason.trim().length >= 5;

  async function save() {
    if (!reasonOk) return toast.error("Please give a reason of at least 5 characters.");
    setBusy(true);
    try {
      if (type === "Vaccination") {
        if (!v.vaccineName.trim()) { toast.error("Vaccine name is required."); return; }
        await healthPlusApi.amend(pet.id, type, recordId, {
          reason: reason.trim(),
          vaccination: {
            vaccineName: v.vaccineName.trim(), administeredOn: v.administeredOn, nextDueOn: v.nextDueOn || null,
            administeredByVet: orNull(v.administeredByVet), notes: orNull(v.notes), certificateUrl: vac?.certificateUrl ?? null,
          },
        });
      } else {
        await healthPlusApi.amend(pet.id, type, recordId, {
          reason: reason.trim(),
          medical: {
            visitDate: m.visitDate, diagnosis: orNull(m.diagnosis), treatment: orNull(m.treatment), notes: orNull(m.notes), vetName: orNull(m.vetName),
            attachmentUrl: med?.attachmentUrl ?? null, prescriptionFileUrl: med?.prescriptionFileUrl ?? null,
          },
        });
      }
      toast.success("Record amended. The previous version is kept in the history.");
      qc.invalidateQueries({ queryKey: ["pets", pet.id] });
      onAmended();
      onClose();
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Amend sealed {type === "Vaccination" ? "vaccination" : "medical"} record</DialogTitle>
          <DialogDescription>Sealed records can't be edited directly. Your change is saved as a new version with your reason, and the old values stay visible in the history.</DialogDescription>
        </DialogHeader>
        {!vac && !med ? <p className="text-sm text-muted-foreground">This record could not be found.</p> : (
          <div className="space-y-3">
            {type === "Vaccination" ? (
              <div className="grid grid-cols-2 gap-3">
                <Field label="Vaccine" htmlFor="am-vn"><Input id="am-vn" value={v.vaccineName} onChange={(e) => setV({ ...v, vaccineName: e.target.value })} /></Field>
                <Field label="Given on" htmlFor="am-vd"><Input id="am-vd" type="date" value={v.administeredOn} onChange={(e) => setV({ ...v, administeredOn: e.target.value })} /></Field>
                <Field label="Next due" htmlFor="am-vnd"><Input id="am-vnd" type="date" value={v.nextDueOn} onChange={(e) => setV({ ...v, nextDueOn: e.target.value })} /></Field>
                <Field label="Given by" htmlFor="am-vv"><Input id="am-vv" value={v.administeredByVet} onChange={(e) => setV({ ...v, administeredByVet: e.target.value })} /></Field>
                <div className="col-span-2"><Field label="Notes" htmlFor="am-vno"><Textarea id="am-vno" rows={2} value={v.notes} onChange={(e) => setV({ ...v, notes: e.target.value })} /></Field></div>
              </div>
            ) : (
              <div className="grid grid-cols-2 gap-3">
                <Field label="Visit date" htmlFor="am-md"><Input id="am-md" type="date" value={m.visitDate} onChange={(e) => setM({ ...m, visitDate: e.target.value })} /></Field>
                <Field label="Vet" htmlFor="am-mv"><Input id="am-mv" value={m.vetName} onChange={(e) => setM({ ...m, vetName: e.target.value })} /></Field>
                <Field label="Diagnosis" htmlFor="am-mdx"><Input id="am-mdx" value={m.diagnosis} onChange={(e) => setM({ ...m, diagnosis: e.target.value })} /></Field>
                <Field label="Treatment" htmlFor="am-mt"><Input id="am-mt" value={m.treatment} onChange={(e) => setM({ ...m, treatment: e.target.value })} /></Field>
                <div className="col-span-2"><Field label="Notes" htmlFor="am-mn"><Textarea id="am-mn" rows={2} value={m.notes} onChange={(e) => setM({ ...m, notes: e.target.value })} /></Field></div>
              </div>
            )}
            <Field label="Reason for amendment (required)" htmlFor="am-reason" error={reason.length > 0 && !reasonOk ? "At least 5 characters." : undefined}>
              <Textarea id="am-reason" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Wrong batch date entered" />
            </Field>
          </div>
        )}
        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button disabled={busy || !reasonOk || (!vac && !med)} onClick={save}>Save amendment</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
