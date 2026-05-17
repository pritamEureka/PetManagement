import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Pill, Plus, Trash2 } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { PageHeader } from "@/components/common/PageHeader";
import { Separator } from "@/components/ui/separator";
import { appointmentsApi } from "@/api/vets";
import { toast } from "@/components/ui/sonner";

interface RxItem { drug: string; dose: string; frequency: string; duration: string; instructions: string; }

export function PrescriptionUploadPage() {
  const { appointmentId = "" } = useParams();
  const nav = useNavigate();
  const { data: appt } = useQuery({ queryKey: ["appointment", appointmentId], queryFn: () => appointmentsApi.get(appointmentId), enabled: !!appointmentId });

  const [fileUrl, setFileUrl] = useState("");
  const [notes, setNotes] = useState("");
  const [validUntil, setValidUntil] = useState("");
  const [followUp, setFollowUp] = useState("");
  const [items, setItems] = useState<RxItem[]>([{ drug: "", dose: "", frequency: "", duration: "", instructions: "" }]);
  const [busy, setBusy] = useState(false);

  function addRow() { setItems((p) => [...p, { drug: "", dose: "", frequency: "", duration: "", instructions: "" }]); }
  function removeRow(i: number) { setItems((p) => p.filter((_, idx) => idx !== i)); }
  function updateRow(i: number, key: keyof RxItem, value: string) {
    setItems((p) => p.map((r, idx) => idx === i ? { ...r, [key]: value } : r));
  }

  async function submit() {
    if (!fileUrl.trim()) { toast.error("Upload the prescription PDF first."); return; }
    setBusy(true);
    try {
      await appointmentsApi.prescription(appointmentId, {
        fileUrl: fileUrl.trim(),
        notes: notes || undefined,
        itemsJson: items.some((r) => r.drug) ? JSON.stringify(items.filter((r) => r.drug)) : undefined,
        validUntil: validUntil || undefined
      });
      if (followUp.trim()) await appointmentsApi.followUp(appointmentId, followUp.trim());
      toast.success("Prescription sent to the patient.");
      nav("/dashboard/vet");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Failed.");
    } finally { setBusy(false); }
  }

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild>
        <Link to="/dashboard/vet"><ArrowLeft className="h-4 w-4 mr-1" /> Back to clinic</Link>
      </Button>

      <PageHeader title="Issue prescription" icon={Pill}
        description={appt ? `For ${appt.patientName}${appt.petName ? ` (pet: ${appt.petName})` : ""}` : "Loading..."} />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="col-span-2">
              <Label htmlFor="file">Prescription PDF URL</Label>
              <Input id="file" placeholder="Use /api/media/presign to upload, then paste the public URL" value={fileUrl} onChange={(e) => setFileUrl(e.target.value)} />
            </div>
            <div><Label htmlFor="until">Valid until</Label><Input id="until" type="date" value={validUntil} onChange={(e) => setValidUntil(e.target.value)} /></div>
          </div>

          <div>
            <Label htmlFor="notes">Clinical notes</Label>
            <Textarea id="notes" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>

          <Separator />

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold">Medications</h3>
              <Button type="button" variant="outline" size="sm" onClick={addRow}><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>
            </div>
            {items.map((r, i) => (
              <div key={i} className="grid grid-cols-12 gap-2 rounded-md border p-2">
                <div className="col-span-3"><Input placeholder="Drug" value={r.drug} onChange={(e) => updateRow(i, "drug", e.target.value)} /></div>
                <div className="col-span-2"><Input placeholder="Dose" value={r.dose} onChange={(e) => updateRow(i, "dose", e.target.value)} /></div>
                <div className="col-span-2"><Input placeholder="Frequency" value={r.frequency} onChange={(e) => updateRow(i, "frequency", e.target.value)} /></div>
                <div className="col-span-2"><Input placeholder="Duration" value={r.duration} onChange={(e) => updateRow(i, "duration", e.target.value)} /></div>
                <div className="col-span-2"><Input placeholder="Instructions" value={r.instructions} onChange={(e) => updateRow(i, "instructions", e.target.value)} /></div>
                <div className="col-span-1 flex items-center justify-end">
                  <Button type="button" variant="ghost" size="icon" onClick={() => removeRow(i)}><Trash2 className="h-4 w-4 text-destructive" /></Button>
                </div>
              </div>
            ))}
          </div>

          <Separator />

          <div>
            <Label htmlFor="followup">Follow-up notes</Label>
            <Textarea id="followup" rows={2} value={followUp} onChange={(e) => setFollowUp(e.target.value)} />
          </div>

          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => nav(-1)}>Cancel</Button>
            <Button onClick={submit} disabled={busy}>{busy ? "Sending..." : "Send to patient"}</Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
