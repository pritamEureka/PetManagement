import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { PromptDialog } from "@/components/common/PromptDialog";
import { toast } from "@/components/ui/sonner";
import { ClinicalEncounterPanel } from "@/components/vet/ClinicalEncounterPanel";
import { QuestionnaireForm } from "@/components/vet/QuestionnaireForm";
import { encounterApi } from "@/api/clinicPlus";

const errMsg = (e: any) => e?.response?.data?.error?.message ?? "Something went wrong.";

/** Vet-side encounter workspace for one appointment: questionnaire answers, clinical record, referral and completion. */
export function EncounterPage() {
  const { id = "" } = useParams();
  const qc = useQueryClient();
  const [overrideOpen, setOverrideOpen] = useState(false);
  const [clinic, setClinic] = useState("");
  const [reason, setReason] = useState("");
  const [consent, setConsent] = useState(true);

  async function complete(override?: string) {
    try {
      await encounterApi.complete(id, override);
      toast.success("Appointment completed.");
      qc.invalidateQueries({ queryKey: ["appointments"] });
    } catch (e: any) {
      // Backend answers 409 "clinical_incomplete..." when the record is not finalized and enforcement is on.
      const msg = errMsg(e);
      if (/clinical_incomplete/.test(msg)) setOverrideOpen(true); else toast.error(msg);
    }
  }
  async function refer() {
    try {
      await encounterApi.refer(id, { externalClinic: clinic.trim() || null, reason: reason.trim(), consentToShareRecords: consent });
      toast.success("Referral created.");
      setClinic(""); setReason("");
      qc.invalidateQueries({ queryKey: ["appointments", id, "encounter"] });
    } catch (e) { toast.error(errMsg(e)); }
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4 p-4">
      <Link to="/dashboard/vet" className="inline-flex items-center gap-1 text-sm text-muted-foreground"><ArrowLeft className="h-4 w-4" /> Back to dashboard</Link>
      <h1 className="text-xl font-bold">Encounter</h1>
      <QuestionnaireForm appointmentId={id} readOnly />
      <ClinicalEncounterPanel appointmentId={id} />

      <Card>
        <CardHeader><CardTitle className="text-base">Refer to an external clinic</CardTitle></CardHeader>
        <CardContent className="space-y-2 text-sm">
          <div><Label htmlFor="ref-clinic">Clinic</Label><Input id="ref-clinic" value={clinic} onChange={(e) => setClinic(e.target.value)} /></div>
          <div><Label htmlFor="ref-reason">Reason</Label><Textarea id="ref-reason" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} /></div>
          <label className="flex items-center gap-2"><input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} /> Ask the owner to share records with the receiving vet</label>
          <Button variant="outline" onClick={refer} disabled={!clinic.trim() || !reason.trim()}>Create referral</Button>
        </CardContent>
      </Card>

      <div className="flex justify-end"><Button onClick={() => complete()}>Complete appointment</Button></div>
      <PromptDialog open={overrideOpen} onOpenChange={setOverrideOpen} title="Complete without a finalized record?"
        description="The clinical record is not finalized. Provide a reason (at least 10 characters); the override is audited."
        label="Override reason" multiline confirmLabel="Complete anyway" destructive onSubmit={(v) => complete(v)} />
    </div>
  );
}
