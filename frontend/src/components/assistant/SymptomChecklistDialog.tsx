import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { AlertTriangle, CalendarPlus, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { assistantTriageApi, type SymptomChecklist, type TriageResult, type TriageUrgency } from "@/api/assistant";

const selectCls =
  "flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

const URGENCY_VARIANT: Record<TriageUrgency, "destructive" | "warning" | "info" | "success"> = {
  Emergency: "destructive", Urgent: "warning", Soon: "info", Routine: "success"
};

const EMPTY: SymptomChecklist = {
  species: "dog", duration: "lt24h", appetite: "normal", breathing: "normal", gumColour: "pink", bleeding: "none",
  vomitingCount: 0, diarrhoeaCount: 0, lethargy: "none", seizure: false, possibleToxinExposure: false, youngOrSenior: false, notes: ""
};

function Pick<T extends string>({ id, label, value, options, onChange }: {
  id: string; label: string; value: T; options: [T, string][]; onChange: (v: T) => void;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <select id={id} className={selectCls} value={value} onChange={e => onChange(e.target.value as T)}>
        {options.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select>
    </div>
  );
}

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  conversationId?: string | null;
  petId?: string | null;
  /** Called with the result so the parent can append the assistant message / adopt the conversation id. */
  onResult?: (result: TriageResult) => void;
}

/**
 * Structured symptom checklist. Urgency is computed server-side by deterministic rules (no AI image analysis);
 * any photo is stored for a human vet and the result always says so.
 */
export function SymptomChecklistDialog({ open, onOpenChange, conversationId, petId, onResult }: Props) {
  const [c, setC] = useState<SymptomChecklist>(EMPTY);
  const [images, setImages] = useState<string[]>([]);
  const [result, setResult] = useState<TriageResult | null>(null);
  const set = <K extends keyof SymptomChecklist>(k: K, v: SymptomChecklist[K]) => setC(prev => ({ ...prev, [k]: v }));

  const submit = useMutation({
    mutationFn: () => assistantTriageApi.submit({ conversationId, petId, checklist: c, imageUrls: images }),
    onSuccess: (r) => { setResult(r); onResult?.(r); }
  });

  const close = (o: boolean) => {
    if (!o) { setResult(null); setImages([]); setC(EMPTY); }
    onOpenChange(o);
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Symptom checklist</DialogTitle>
          <DialogDescription>
            Answer a few questions for a rule-based urgency estimate. This is not a diagnosis, and photos are not analysed by AI.
          </DialogDescription>
        </DialogHeader>

        {result ? (
          <div className="space-y-3" aria-live="polite">
            <div className="flex items-center gap-2">
              {result.assessment.urgency === "Emergency" && <AlertTriangle className="h-5 w-5 text-destructive" />}
              <Badge variant={URGENCY_VARIANT[result.assessment.urgency]}>{result.assessment.urgency}</Badge>
            </div>
            <p className="text-sm">{result.assessment.summary}</p>
            <ul className="list-disc pl-5 text-sm text-muted-foreground">
              {result.assessment.reasons.map(r => <li key={r}>{r}</li>)}
            </ul>
            {images.length > 0 && (
              <p role="status" className="rounded-md border border-amber-500 bg-amber-500/10 p-2 text-sm">
                Photo needs human vet review. A review request has been sent to our vets; their answer will appear in this conversation.
              </p>
            )}
            <div className="flex flex-wrap gap-2">
              <Button asChild>
                <Link to={result.bookingLink} onClick={() => close(false)}>
                  <CalendarPlus className="h-4 w-4 mr-1" /> Book an online vet appointment
                </Link>
              </Button>
              {result.escalationLink && (
                <Button asChild variant="destructive">
                  <Link to={result.escalationLink} onClick={() => close(false)}>Find a vet now</Link>
                </Button>
              )}
              <Button variant="outline" onClick={() => close(false)}>Close</Button>
            </div>
          </div>
        ) : (
          <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); submit.mutate(); }}>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="sc-species">Species</Label>
                <Input id="sc-species" value={c.species} maxLength={40} onChange={e => set("species", e.target.value)} />
              </div>
              <Pick id="sc-duration" label="How long?" value={c.duration} onChange={v => set("duration", v)}
                options={[["lt24h", "Less than a day"], ["1to3d", "1 to 3 days"], ["gt3d", "More than 3 days"]]} />
              <Pick id="sc-appetite" label="Appetite" value={c.appetite} onChange={v => set("appetite", v)}
                options={[["normal", "Normal"], ["reduced", "Reduced"], ["none", "Not eating"]]} />
              <Pick id="sc-breathing" label="Breathing" value={c.breathing} onChange={v => set("breathing", v)}
                options={[["normal", "Normal"], ["fast", "Fast"], ["laboured", "Laboured / struggling"]]} />
              <Pick id="sc-gums" label="Gum colour" value={c.gumColour} onChange={v => set("gumColour", v)}
                options={[["pink", "Pink (normal)"], ["pale", "Pale / white"], ["blue", "Blue / grey"], ["yellow", "Yellow"], ["unknown", "Not sure"]]} />
              <Pick id="sc-bleeding" label="Bleeding" value={c.bleeding} onChange={v => set("bleeding", v)}
                options={[["none", "None"], ["minor", "Minor"], ["heavy", "Heavy / won't stop"]]} />
              <div className="space-y-1.5">
                <Label htmlFor="sc-vomit">Vomiting (last 24h)</Label>
                <Input id="sc-vomit" type="number" min={0} max={100} value={c.vomitingCount}
                  onChange={e => set("vomitingCount", Math.max(0, Number(e.target.value) || 0))} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="sc-diar">Diarrhoea (last 24h)</Label>
                <Input id="sc-diar" type="number" min={0} max={100} value={c.diarrhoeaCount}
                  onChange={e => set("diarrhoeaCount", Math.max(0, Number(e.target.value) || 0))} />
              </div>
              <Pick id="sc-lethargy" label="Energy" value={c.lethargy} onChange={v => set("lethargy", v)}
                options={[["none", "Normal"], ["mild", "Tired / quiet"], ["severe", "Very weak / collapsed"]]} />
            </div>

            {([
              ["seizure", "Seizure or convulsions"],
              ["possibleToxinExposure", "May have eaten a poison, toxin or human medication"],
              ["youngOrSenior", "Very young (under 6 months) or senior animal"]
            ] as const).map(([k, label]) => (
              <div key={k} className="flex items-center gap-2 text-sm">
                <Checkbox checked={c[k]} aria-label={label} onChange={e => set(k, e.target.checked)} />
                <span>{label}</span>
              </div>
            ))}

            <div className="space-y-1.5">
              <Label htmlFor="sc-notes">Anything else? (optional)</Label>
              <Textarea id="sc-notes" rows={2} maxLength={1000} value={c.notes ?? ""} onChange={e => set("notes", e.target.value)} />
            </div>

            <div className="space-y-1.5">
              <div className="flex items-center gap-2 flex-wrap">
                <FileUploadButton label="Attach photo" disabled={images.length >= 4}
                  onUploaded={(url) => setImages(prev => [...prev, url].slice(0, 4))} />
                {images.map((u, i) => (
                  <span key={u} className="relative">
                    <img src={u} alt={`Attached photo ${i + 1}`} className="h-12 w-12 rounded object-cover border" />
                    <button type="button" aria-label="Remove photo" onClick={() => setImages(p => p.filter(x => x !== u))}
                      className="absolute -top-1 -right-1 rounded-full bg-background border p-0.5">
                      <X className="h-3 w-3" />
                    </button>
                  </span>
                ))}
              </div>
              <p className="text-xs text-muted-foreground">Photo needs human vet review - the assistant cannot interpret images.</p>
            </div>

            {submit.isError && <p role="alert" className="text-sm text-destructive">Could not submit the checklist. Please try again.</p>}
            <Button type="submit" disabled={!c.species.trim() || submit.isPending} className="w-full">
              {submit.isPending ? "Scoring..." : "Get urgency estimate"}
            </Button>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
