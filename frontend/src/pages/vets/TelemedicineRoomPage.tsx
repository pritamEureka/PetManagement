import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Video, Users } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { telemedicineApi, type ConsultationNoteInput } from "@/api/vetPlus";
import { ClinicalEncounterPanel } from "@/components/vet/ClinicalEncounterPanel";
import { QuestionnaireForm } from "@/components/vet/QuestionnaireForm";

const fmt = (iso?: string | null) => (iso ? new Date(iso).toLocaleString() : "-");

export function TelemedicineRoomPage() {
  const { id = "" } = useParams();
  const qc = useQueryClient();

  const roomKey = ["telemedicine", "room", id];
  const { data: room, isLoading, error } = useQuery({
    queryKey: roomKey,
    queryFn: () => telemedicineApi.room(id),
    enabled: !!id,
    // Poll so the lobby and waiting list stay fresh without a socket.
    refetchInterval: (q) => (q.state.data?.state === "Closed" ? false : 5000)
  });

  const [consent, setConsent] = useState(false);
  const [note, setNote] = useState("");
  useEffect(() => {
    if (room) {
      setConsent(room.consentGiven);
      setNote(room.preConsultNote ?? "");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [room?.consentGiven, room?.preConsultNote]);

  const checkIn = useMutation({
    mutationFn: () => telemedicineApi.checkIn(id, consent, note),
    onSuccess: (r) => {
      qc.setQueryData(roomKey, r);
      toast.success("You're checked in. Waiting for your vet to admit you.");
    }
  });
  const admit = useMutation({
    mutationFn: () => telemedicineApi.admit(id),
    onSuccess: () => {
      toast.success("Patient admitted.");
      qc.invalidateQueries({ queryKey: roomKey });
    }
  });

  function join() {
    if (!room?.joinUrl) return;
    telemedicineApi.event(id, "Join").catch(() => undefined);
    window.open(room.joinUrl, "_blank", "noopener,noreferrer");
  }
  function leave() {
    telemedicineApi.event(id, "Leave").then(() => toast.success("Leave recorded.")).catch(() => undefined);
  }

  if (isLoading) return <div className="mx-auto max-w-2xl p-4"><Skeleton className="h-40 w-full" /></div>;
  if (error || !room) {
    return (
      <div className="mx-auto max-w-2xl space-y-3 p-4">
        <Link to="/appointments" className="inline-flex items-center gap-1 text-sm text-muted-foreground">
          <ArrowLeft className="h-4 w-4" /> Back to appointments
        </Link>
        <p className="text-sm text-destructive">This appointment has no telemedicine room available.</p>
      </div>
    );
  }

  const closed = room.state === "Closed";
  const checkedIn = !!room.checkedInAt;
  const admitted = !!room.admittedAt;

  return (
    <div className="mx-auto max-w-2xl space-y-4 p-4">
      <Link to="/appointments" className="inline-flex items-center gap-1 text-sm text-muted-foreground">
        <ArrowLeft className="h-4 w-4" /> Back to appointments
      </Link>

      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle className="flex items-center gap-2 text-lg">
            <Video className="h-5 w-5" /> Online consultation
          </CardTitle>
          <Badge variant={room.state === "Open" ? "default" : "secondary"}>
            {room.state === "NotOpen" ? "Not open yet" : room.state}
          </Badge>
        </CardHeader>
        <CardContent className="space-y-4 text-sm">
          <div className="text-muted-foreground">
            {room.doctorName} with {room.patientName}. Room window: {fmt(room.opensAt)} to {fmt(room.expiresAt)}.
          </div>

          {closed && <p>This room has closed.</p>}

          {/* Patient lobby */}
          {!room.isDoctor && !closed && (
            <div className="space-y-3">
              {!checkedIn ? (
                <>
                  <label className="flex items-start gap-2">
                    <input
                      type="checkbox" className="mt-1" checked={consent}
                      onChange={(e) => setConsent(e.target.checked)}
                    />
                    <span>I consent to a video consultation with my vet and understand it is not for emergencies.</span>
                  </label>
                  <div className="space-y-1.5">
                    <Label htmlFor="pre-note">Note for your vet (optional)</Label>
                    <Textarea
                      id="pre-note" rows={3} maxLength={2000} value={note}
                      onChange={(e) => setNote(e.target.value)}
                      placeholder="Symptoms, questions, or anything the vet should know before the call"
                    />
                  </div>
                  <Button
                    onClick={() => checkIn.mutate()}
                    disabled={!consent || room.state !== "Open" || checkIn.isPending}
                  >
                    {room.state === "Open" ? "Check in to waiting room" : "Check-in opens 15 minutes before the visit"}
                  </Button>
                </>
              ) : !admitted ? (
                <p className="flex items-center gap-2">
                  <Users className="h-4 w-4" /> You are in the waiting room since {fmt(room.checkedInAt)}.
                  Your vet will admit you shortly.
                </p>
              ) : (
                <div className="flex items-center gap-2">
                  <Button onClick={join} disabled={!room.joinUrl}>Join video call</Button>
                  <Button variant="outline" onClick={leave}>I left the call</Button>
                </div>
              )}
            </div>
          )}

          {/* Vet view */}
          {room.isDoctor && !closed && (
            <div className="space-y-3">
              <div className="rounded-md border p-3">
                <div className="font-medium">Patient lobby</div>
                {!checkedIn && <div className="text-muted-foreground">The patient has not checked in yet.</div>}
                {checkedIn && (
                  <div className="space-y-2">
                    <div>
                      {room.patientName} checked in at {fmt(room.checkedInAt)}
                      {room.consentGiven ? " (consent given)" : ""}.
                    </div>
                    {room.preConsultNote && (
                      <div className="rounded bg-muted p-2">Note: {room.preConsultNote}</div>
                    )}
                    {!admitted && (
                      <Button size="sm" onClick={() => admit.mutate()} disabled={admit.isPending}>Admit patient</Button>
                    )}
                    {admitted && <Badge variant="secondary">Admitted {fmt(room.admittedAt)}</Badge>}
                  </div>
                )}
              </div>
              <div className="flex items-center gap-2">
                <Button onClick={join} disabled={!room.joinUrl}>Open video call</Button>
                <Button variant="outline" onClick={leave}>I left the call</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {room.isDoctor ? <QuestionnaireForm appointmentId={id} readOnly /> : !closed && !checkedIn && <QuestionnaireForm appointmentId={id} />}
      <NotesCard appointmentId={id} isDoctor={room.isDoctor} />
      {room.isDoctor && <ClinicalEncounterPanel appointmentId={id} />}
    </div>
  );
}

function NotesCard({ appointmentId, isDoctor }: { appointmentId: string; isDoctor: boolean }) {
  const qc = useQueryClient();
  const { data } = useQuery({
    queryKey: ["telemedicine", "note", appointmentId],
    queryFn: () => telemedicineApi.getNote(appointmentId)
  });
  const [form, setForm] = useState<ConsultationNoteInput>({});
  useEffect(() => {
    if (data) setForm({
      subjective: data.subjective ?? "", objective: data.objective ?? "",
      assessment: data.assessment ?? "", plan: data.plan ?? ""
    });
  }, [data]);

  const save = useMutation({
    mutationFn: () => telemedicineApi.saveNote(appointmentId, form),
    onSuccess: (n) => {
      qc.setQueryData(["telemedicine", "note", appointmentId], n);
      toast.success("Notes saved.");
    }
  });

  const hasAny = !!(data?.subjective || data?.objective || data?.assessment || data?.plan);
  if (!isDoctor && !hasAny) return null;

  const fields: { key: keyof ConsultationNoteInput; label: string }[] = [
    { key: "subjective", label: "Subjective (history, owner's report)" },
    { key: "objective", label: "Objective (observations, findings)" },
    { key: "assessment", label: "Assessment (diagnosis)" },
    { key: "plan", label: "Plan (treatment, follow-up)" }
  ];

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Consultation notes (SOAP)</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        {fields.map((f) => (
          <div key={f.key} className="space-y-1.5">
            <Label htmlFor={`soap-${f.key}`}>{f.label}</Label>
            {isDoctor ? (
              <Textarea
                id={`soap-${f.key}`} rows={3} maxLength={4000} value={form[f.key] ?? ""}
                onChange={(e) => setForm((s) => ({ ...s, [f.key]: e.target.value }))}
              />
            ) : (
              <p className="whitespace-pre-wrap rounded bg-muted p-2">{data?.[f.key] || "-"}</p>
            )}
          </div>
        ))}
        {isDoctor && (
          <Button onClick={() => save.mutate()} disabled={save.isPending}>
            {save.isPending ? "Saving…" : "Save notes"}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
