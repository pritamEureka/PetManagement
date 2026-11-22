import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardList } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "@/components/ui/sonner";
import { questionnaireApi } from "@/api/clinicPlus";

/**
 * Pre-visit questionnaire. Owner mode (default) fills and submits; with `readOnly` (vet view) it shows the submitted answers.
 */
export function QuestionnaireForm({ appointmentId, readOnly = false }: { appointmentId: string; readOnly?: boolean }) {
  const qc = useQueryClient();
  const key = ["appointments", appointmentId, "questionnaire"];
  const { data } = useQuery({ queryKey: key, queryFn: () => questionnaireApi.forAppointment(appointmentId) });
  const [answers, setAnswers] = useState<Record<string, string>>({});
  useEffect(() => { if (data?.answers) setAnswers(data.answers); }, [data?.answers]);

  const submit = useMutation({
    mutationFn: () => questionnaireApi.submit(appointmentId, answers),
    onSuccess: () => { toast.success("Questionnaire sent to your vet."); qc.invalidateQueries({ queryKey: key }); }
  });

  if (!data?.template) return readOnly ? <p className="text-sm text-muted-foreground">No pre-visit questionnaire for this appointment.</p> : null;
  if (readOnly && !data.submittedAt) return <p className="text-sm text-muted-foreground">The patient has not submitted the questionnaire yet.</p>;

  const set = (id: string, v: string) => setAnswers((a) => ({ ...a, [id]: v }));

  return (
    <Card>
      <CardHeader><CardTitle className="flex items-center gap-2 text-base"><ClipboardList className="h-4 w-4" /> {data.template.title}
        {data.required && !data.submittedAt && <span className="text-xs text-destructive font-normal">(required before check-in)</span>}</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        {data.template.questions.map((q) => (
          <div key={q.id} className="space-y-1">
            <Label htmlFor={`q-${q.id}`}>{q.label}{q.required ? " *" : ""}</Label>
            {readOnly ? <p className="rounded border bg-muted/30 px-2 py-1">{answers[q.id] || "—"}</p>
              : q.type === "yesno" ? (
                <div className="flex gap-3" role="radiogroup" aria-label={q.label}>
                  {["yes", "no"].map((v) => <label key={v} className="flex items-center gap-1"><input type="radio" name={`q-${q.id}`} checked={answers[q.id] === v} onChange={() => set(q.id, v)} />{v === "yes" ? "Yes" : "No"}</label>)}
                </div>
              ) : q.type === "choice" ? (
                <select id={`q-${q.id}`} className="w-full rounded-md border bg-background px-2 py-2" value={answers[q.id] ?? ""} onChange={(e) => set(q.id, e.target.value)}>
                  <option value="">Select…</option>
                  {(q.options ?? []).map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              ) : <Input id={`q-${q.id}`} type={q.type === "number" ? "number" : "text"} value={answers[q.id] ?? ""} onChange={(e) => set(q.id, e.target.value)} />}
          </div>
        ))}
        {!readOnly && <Button onClick={() => submit.mutate()} disabled={submit.isPending}>{data.submittedAt ? "Update answers" : "Submit answers"}</Button>}
        {data.submittedAt && <p className="text-xs text-muted-foreground">Submitted {new Date(data.submittedAt).toLocaleString()}</p>}
      </CardContent>
    </Card>
  );
}
