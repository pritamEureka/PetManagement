import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardList, Plus, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/sonner";
import { questionnaireApi, type Question, type QuestionType } from "@/api/clinicPlus";

/** Vet self-service: define the pre-visit questionnaire owners fill in. */
export function QuestionnaireTemplateCard() {
  const qc = useQueryClient();
  const { data } = useQuery({ queryKey: ["vets", "me", "questionnaire"], queryFn: questionnaireApi.mine });
  const [title, setTitle] = useState("Pre-visit questionnaire");
  const [active, setActive] = useState(true);
  const [qs, setQs] = useState<Question[]>([]);
  useEffect(() => { if (data) { setTitle(data.title); setActive(data.isActive); setQs(data.questions); } }, [data]);

  const save = useMutation({
    mutationFn: () => questionnaireApi.save({ title, isActive: active, questions: qs.map((q) => ({ ...q, options: q.type === "choice" ? q.options : null })) }),
    onSuccess: () => { toast.success("Questionnaire saved."); qc.invalidateQueries({ queryKey: ["vets", "me", "questionnaire"] }); }
  });
  const upd = (i: number, patch: Partial<Question>) => setQs((a) => a.map((q, j) => (j === i ? { ...q, ...patch } : q)));

  return (
    <Card>
      <CardHeader><CardTitle className="flex items-center gap-2 text-base"><ClipboardList className="h-4 w-4" /> Pre-visit questionnaire</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        <Input value={title} onChange={(e) => setTitle(e.target.value)} aria-label="Title" />
        {qs.map((q, i) => (
          <div key={q.id} className="flex flex-wrap items-center gap-2">
            <Input className="flex-1 min-w-[160px]" value={q.label} placeholder="Question" onChange={(e) => upd(i, { label: e.target.value })} aria-label={`Question ${i + 1}`} />
            <select className="rounded-md border bg-background px-2 py-2" value={q.type} onChange={(e) => upd(i, { type: e.target.value as QuestionType, options: e.target.value === "choice" ? (q.options ?? ["Option 1", "Option 2"]) : null })} aria-label="Type">
              <option value="text">Text</option><option value="yesno">Yes/No</option><option value="choice">Choice</option><option value="number">Number</option>
            </select>
            {q.type === "choice" && <Input className="w-48" value={(q.options ?? []).join(", ")} onChange={(e) => upd(i, { options: e.target.value.split(",").map((s) => s.trim()).filter(Boolean) })} aria-label="Options (comma separated)" placeholder="A, B, C" />}
            <label className="flex items-center gap-1 text-xs"><input type="checkbox" checked={q.required} onChange={(e) => upd(i, { required: e.target.checked })} />Required</label>
            <Button type="button" size="sm" variant="ghost" onClick={() => setQs((a) => a.filter((_, j) => j !== i))} aria-label="Remove question"><Trash2 className="h-4 w-4" /></Button>
          </div>
        ))}
        <div className="flex flex-wrap items-center gap-3">
          <Button type="button" variant="outline" size="sm" onClick={() => setQs((a) => [...a, { id: `q${Date.now().toString(36)}`, type: "text", label: "", required: false, options: null }])}><Plus className="h-4 w-4 mr-1" /> Add question</Button>
          <label className="flex items-center gap-1"><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} /> Active</label>
          <Button onClick={() => save.mutate()} disabled={save.isPending || qs.length === 0 || qs.some((q) => !q.label.trim())}>Save</Button>
        </div>
      </CardContent>
    </Card>
  );
}
