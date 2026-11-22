import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, Phone, Stethoscope, Skull } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { toast } from "@/components/ui/sonner";
import { petsApi } from "@/api/pets";
import { assistantTriageApi, type SymptomChecklist, type TriageResult, type TriageUrgency } from "@/api/assistant";
import { ExposureReportDialog } from "@/pages/pets/detail/ExposureReportDialog";

const sel = "h-9 rounded-md border bg-background px-2 text-sm w-full";
const urgencyStyle: Record<TriageUrgency, string> = {
  Routine: "bg-green-100 text-green-900 dark:bg-green-950 dark:text-green-100",
  Soon: "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-100",
  Urgent: "bg-orange-200 text-orange-950 dark:bg-orange-950 dark:text-orange-100",
  Emergency: "bg-red-600 text-white",
};

const blank = (species: string): SymptomChecklist => ({
  species, duration: "lt24h", appetite: "normal", breathing: "normal", gumColour: "unknown", bleeding: "none",
  vomitingCount: 0, diarrhoeaCount: 0, lethargy: "none", seizure: false, possibleToxinExposure: false, youngOrSenior: false, notes: "",
});

/** Emergency hub: disclaimers, symptom checker (assistant triage), poison report, link to emergency vets. */
export function EmergencyPage() {
  const { data: pets = [] } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const [petId, setPetId] = useState("");
  const pet = pets.find((p) => p.id === petId);
  const [c, setC] = useState<SymptomChecklist>(blank("Dog"));
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<TriageResult | null>(null);
  const [poisonOpen, setPoisonOpen] = useState(false);
  const set = <K extends keyof SymptomChecklist>(k: K, v: SymptomChecklist[K]) => setC((x) => ({ ...x, [k]: v }));

  function choosePet(id: string) {
    setPetId(id);
    const p = pets.find((x) => x.id === id);
    if (p) set("species", p.animalType);
  }

  async function check() {
    setBusy(true);
    try { setResult(await assistantTriageApi.submit({ petId: petId || null, checklist: { ...c, notes: c.notes?.trim() || null } })); }
    catch { /* toasted */ } finally { setBusy(false); }
  }

  const S = ({ label, k, options }: { label: string; k: keyof SymptomChecklist; options: string[] }) => (
    <label className="text-sm space-y-1 block"><span className="font-medium">{label}</span>
      <select className={sel} value={String(c[k])} onChange={(e) => set(k, e.target.value as never)}>{options.map((o) => <option key={o}>{o}</option>)}</select>
    </label>
  );

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <div role="alert" className="rounded-lg bg-red-600 text-white p-4 space-y-2">
        <p className="font-bold flex items-center gap-2 text-lg"><AlertTriangle className="h-5 w-5" /> Pet emergency?</p>
        <p className="text-sm">If your pet is struggling to breathe, collapsing, seizing, bleeding heavily, bloated, or may have eaten something toxic — <strong>call your nearest emergency vet now</strong>. Do not wait for this tool.</p>
        <Button asChild variant="secondary"><Link to="/vets?emergency=true"><Phone className="h-4 w-4 mr-1" /> Find an emergency vet</Link></Button>
        <p className="text-xs opacity-90">This page gives general guidance only. It is not a diagnosis, does not replace a veterinarian, and cannot assess your pet in person.</p>
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">Which pet?</CardTitle></CardHeader>
        <CardContent>
          <select aria-label="Select pet" className={sel} value={petId} onChange={(e) => choosePet(e.target.value)}>
            <option value="">Not listed / another animal</option>
            {pets.map((p) => <option key={p.id} value={p.id}>{p.name} ({p.animalType})</option>)}
          </select>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base flex items-center gap-2"><Skull className="h-4 w-4" /> Poison or toxin exposure</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          <p className="text-sm text-muted-foreground">Report what your pet ate or touched. You'll get immediate escalation steps and your family/caretakers will be notified.</p>
          <Button variant="destructive" disabled={!pet} onClick={() => setPoisonOpen(true)}>Report exposure{pet ? ` for ${pet.name}` : " (select a pet first)"}</Button>
        </CardContent>
      </Card>
      {pet && <ExposureReportDialog petId={pet.id} petName={pet.name} open={poisonOpen} onOpenChange={setPoisonOpen} />}

      <Card>
        <CardHeader><CardTitle className="text-base flex items-center gap-2"><Stethoscope className="h-4 w-4" /> Symptom checker</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="grid sm:grid-cols-3 gap-3">
            <label className="text-sm space-y-1 block"><span className="font-medium">Species</span><Input value={c.species} onChange={(e) => set("species", e.target.value)} /></label>
            <S label="How long?" k="duration" options={["lt24h", "1to3d", "gt3d"]} />
            <S label="Appetite" k="appetite" options={["normal", "reduced", "none"]} />
            <S label="Breathing" k="breathing" options={["normal", "fast", "laboured"]} />
            <S label="Gum colour" k="gumColour" options={["pink", "pale", "blue", "yellow", "unknown"]} />
            <S label="Bleeding" k="bleeding" options={["none", "minor", "heavy"]} />
            <S label="Lethargy" k="lethargy" options={["none", "mild", "severe"]} />
            <label className="text-sm space-y-1 block"><span className="font-medium">Vomiting episodes</span><Input type="number" min={0} value={c.vomitingCount} onChange={(e) => set("vomitingCount", Math.max(0, Number(e.target.value)))} /></label>
            <label className="text-sm space-y-1 block"><span className="font-medium">Diarrhoea episodes</span><Input type="number" min={0} value={c.diarrhoeaCount} onChange={(e) => set("diarrhoeaCount", Math.max(0, Number(e.target.value)))} /></label>
          </div>
          <div className="flex flex-wrap gap-4 text-sm">
            {([["seizure", "Seizure"], ["possibleToxinExposure", "Possible toxin exposure"], ["youngOrSenior", "Very young or senior"]] as const).map(([k, l]) => (
              <label key={k} className="flex items-center gap-2"><input type="checkbox" checked={c[k]} onChange={(e) => set(k, e.target.checked)} /> {l}</label>
            ))}
          </div>
          <Textarea aria-label="Other notes" rows={2} placeholder="Anything else? (optional)" value={c.notes ?? ""} onChange={(e) => set("notes", e.target.value)} />
          <Button disabled={busy || !c.species.trim()} onClick={() => check().catch(() => toast.error("Could not run the checker."))}>Check urgency</Button>

          {result && (
            <div className="space-y-2" role="status">
              <div className={`rounded-md p-3 ${urgencyStyle[result.assessment.urgency]}`}>
                <p className="font-semibold">Urgency: {result.assessment.urgency}</p>
                <p className="text-sm">{result.assessment.summary}</p>
              </div>
              {result.assessment.reasons.length > 0 && <ul className="list-disc pl-5 text-sm">{result.assessment.reasons.map((r) => <li key={r}>{r}</li>)}</ul>}
              <div className="flex flex-wrap gap-2">
                {(result.assessment.urgency === "Emergency" || result.assessment.urgency === "Urgent") && (
                  <Button asChild variant="destructive"><Link to="/vets?emergency=true">Find an emergency vet now</Link></Button>
                )}
                <Button asChild variant="outline"><Link to={result.bookingLink}>Book a vet</Link></Button>
                {result.assessment.needsHumanReview && <Badge variant="info">A vet review was requested</Badge>}
              </div>
              <p className="text-xs text-muted-foreground">{result.disclaimer}</p>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
