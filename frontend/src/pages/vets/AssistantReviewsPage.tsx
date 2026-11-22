import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/sonner";
import { PageHeader } from "@/components/common/PageHeader";
import { useAuthStore } from "@/store/authStore";
import { assistantTriageApi, type ReviewRequest, type TriageUrgency } from "@/api/assistant";

const VARIANT: Record<TriageUrgency, "destructive" | "warning" | "info" | "success"> = {
  Emergency: "destructive", Urgent: "warning", Soon: "info", Routine: "success"
};

function ReviewCard({ r }: { r: ReviewRequest }) {
  const qc = useQueryClient();
  const canAnswer = useAuthStore(s => s.hasPermission("vets.edit"));
  const [notes, setNotes] = useState(r.vetNotes ?? "");
  const answer = useMutation({
    mutationFn: () => assistantTriageApi.answer(r.id, notes.trim()),
    onSuccess: () => { toast.success("Answer sent to the pet owner."); qc.invalidateQueries({ queryKey: ["assistant-reviews"] }); }
  });
  const c = r.checklist;
  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base flex items-center gap-2">
          <Badge variant={VARIANT[r.urgency]}>{r.urgency}</Badge>
          <span className="text-sm font-normal text-muted-foreground">{new Date(r.createdAt).toLocaleString()}</span>
        </CardTitle>
        <Badge variant={r.status === "Pending" ? "warning" : "success"}>{r.status}</Badge>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <p>{r.summary}</p>
        {c && (
          <dl className="grid grid-cols-2 sm:grid-cols-3 gap-x-4 gap-y-1 text-xs">
            <div><dt className="text-muted-foreground inline">Species: </dt><dd className="inline">{c.species}</dd></div>
            <div><dt className="text-muted-foreground inline">Duration: </dt><dd className="inline">{c.duration}</dd></div>
            <div><dt className="text-muted-foreground inline">Appetite: </dt><dd className="inline">{c.appetite}</dd></div>
            <div><dt className="text-muted-foreground inline">Breathing: </dt><dd className="inline">{c.breathing}</dd></div>
            <div><dt className="text-muted-foreground inline">Gums: </dt><dd className="inline">{c.gumColour}</dd></div>
            <div><dt className="text-muted-foreground inline">Bleeding: </dt><dd className="inline">{c.bleeding}</dd></div>
            <div><dt className="text-muted-foreground inline">Vomiting: </dt><dd className="inline">{c.vomitingCount}</dd></div>
            <div><dt className="text-muted-foreground inline">Diarrhoea: </dt><dd className="inline">{c.diarrhoeaCount}</dd></div>
            <div><dt className="text-muted-foreground inline">Energy: </dt><dd className="inline">{c.lethargy}</dd></div>
          </dl>
        )}
        {c?.notes && <p className="text-xs italic">Owner notes: {c.notes}</p>}
        {r.imageUrls.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {r.imageUrls.map((u, i) => (
              <a key={u} href={u} target="_blank" rel="noreferrer">
                <img src={u} alt={`Owner photo ${i + 1}`} className="h-24 w-24 rounded object-cover border" />
              </a>
            ))}
          </div>
        )}
        {canAnswer ? (
          <div className="space-y-2">
            <Textarea rows={3} maxLength={4000} value={notes} onChange={e => setNotes(e.target.value)}
              placeholder="Your notes for the pet owner (not a diagnosis; advise a visit where needed)" aria-label="Vet notes" />
            <Button size="sm" disabled={!notes.trim() || answer.isPending} onClick={() => answer.mutate()}>
              {r.status === "Reviewed" ? "Update answer" : "Send answer"}
            </Button>
          </div>
        ) : r.vetNotes ? <p className="rounded-md bg-muted p-2">{r.vetNotes}</p> : null}
      </CardContent>
    </Card>
  );
}

/** Vet queue for assistant triage / photo review requests. Needs vets.view (answering needs vets.edit). */
export function AssistantReviewsPage() {
  const [status, setStatus] = useState<"Pending" | "Reviewed">("Pending");
  const q = useQuery({ queryKey: ["assistant-reviews", status], queryFn: () => assistantTriageApi.reviews(status) });
  return (
    <div className="space-y-4">
      <PageHeader title="Assistant review requests" description="Symptom checklists and photos that need a human vet's eyes." />
      <div className="flex gap-2">
        {(["Pending", "Reviewed"] as const).map(s => (
          <Button key={s} size="sm" variant={status === s ? "default" : "outline"} onClick={() => setStatus(s)}>{s}</Button>
        ))}
      </div>
      {q.isLoading ? <p className="text-sm text-muted-foreground">Loading...</p>
        : !q.data?.length ? <p className="text-sm text-muted-foreground">Nothing here.</p>
        : <div className="space-y-3">{q.data.map(r => <ReviewCard key={r.id} r={r} />)}</div>}
    </div>
  );
}
