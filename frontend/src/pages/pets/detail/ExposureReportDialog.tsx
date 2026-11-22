import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, Phone } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { exposureApi, type Escalation } from "@/api/carePlans";
import { Field } from "./shared";

const sel = "h-9 rounded-md border bg-background px-2 text-sm w-full";

/** Poison / exposure report. On submit the API answers with an escalation block that is shown immediately. */
export function ExposureReportDialog({ petId, petName, open, onOpenChange }: {
  petId: string; petName: string; open: boolean; onOpenChange: (v: boolean) => void;
}) {
  const { data: substances = [] } = useQuery({ queryKey: ["exposure-substances"], queryFn: exposureApi.substances, enabled: open, staleTime: Infinity });
  const [key, setKey] = useState("");
  const [other, setOther] = useState("");
  const [amount, setAmount] = useState("");
  const [onset, setOnset] = useState("");
  const [symptoms, setSymptoms] = useState("");
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<Escalation | null>(null);

  const picked = substances.find((s) => s.key === key);
  const substance = key === "other" || !picked ? other.trim() : `${picked.label}${other.trim() ? ` — ${other.trim()}` : ""}`;

  async function submit() {
    if (!substance) return toast.error("Tell us what your pet was exposed to.");
    setBusy(true);
    try {
      setResult(await exposureApi.report(petId, {
        substance, amountEstimate: amount.trim() || null, symptoms: symptoms.trim() || null,
        onsetAt: onset ? new Date(onset).toISOString() : null,
      }));
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  function close(v: boolean) {
    if (!v) { setResult(null); setKey(""); setOther(""); setAmount(""); setOnset(""); setSymptoms(""); }
    onOpenChange(v);
  }

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="max-w-lg">
        <DialogHeader><DialogTitle className="flex items-center gap-2 text-red-600"><AlertTriangle className="h-5 w-5" /> Report poison / exposure — {petName}</DialogTitle></DialogHeader>
        {result ? (
          <div className="space-y-3 max-h-[65vh] overflow-y-auto pr-1" role="alert">
            <div className="rounded-md bg-red-600 text-white p-3">
              <p className="font-semibold">{result.headline}</p>
              <p className="text-sm mt-1">{result.message}</p>
              <Button asChild variant="secondary" className="mt-3"><Link to={result.emergencyVetsLink}><Phone className="h-4 w-4 mr-1" /> {result.callNowText}</Link></Button>
            </div>
            <div className="text-sm"><p className="font-medium">Do now</p><ul className="list-disc pl-5 space-y-1">{result.doNow.map((d) => <li key={d}>{d}</li>)}</ul></div>
            <div className="text-sm"><p className="font-medium">Do not</p><ul className="list-disc pl-5 space-y-1">{result.doNot.map((d) => <li key={d}>{d}</li>)}</ul></div>
            <p className="text-sm text-muted-foreground">{result.genericGuidance}</p>
            <p className="text-xs rounded-md border p-2 text-muted-foreground">{result.disclaimer} Your family/caretakers have been notified.</p>
          </div>
        ) : (
          <div className="space-y-3">
            <p className="text-xs rounded-md bg-amber-100 dark:bg-amber-950 text-amber-900 dark:text-amber-100 p-2">
              If your pet is collapsing, seizing or struggling to breathe, call an emergency vet immediately — don't fill in this form first.
            </p>
            <Field label="What was it?" htmlFor="ex-sub">
              <select id="ex-sub" className={sel} value={key} onChange={(e) => setKey(e.target.value)}>
                <option value="">Choose…</option>
                {substances.map((s) => <option key={s.key} value={s.key}>{s.label}</option>)}
              </select>
            </Field>
            <Field label={key === "other" || !key ? "Describe it" : "Product / details (optional)"} htmlFor="ex-other">
              <Input id="ex-other" value={other} onChange={(e) => setOther(e.target.value)} maxLength={150} />
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label="How much (estimate)" htmlFor="ex-amt"><Input id="ex-amt" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="e.g. half a bar, 3 pills" /></Field>
              <Field label="Symptoms started (if any)" htmlFor="ex-onset"><Input id="ex-onset" type="datetime-local" value={onset} onChange={(e) => setOnset(e.target.value)} /></Field>
            </div>
            <Field label="Symptoms you see" htmlFor="ex-sym"><Textarea id="ex-sym" rows={2} value={symptoms} onChange={(e) => setSymptoms(e.target.value)} /></Field>
          </div>
        )}
        <DialogFooter>
          {result ? <Button onClick={() => close(false)}>Close</Button> : (
            <><Button variant="ghost" onClick={() => close(false)}>Cancel</Button><Button variant="destructive" disabled={busy} onClick={submit}>Report &amp; get guidance</Button></>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
