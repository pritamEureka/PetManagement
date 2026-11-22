import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Stethoscope, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { EmptyState } from "@/components/common/EmptyState";
import { toast } from "@/components/ui/sonner";
import { petsApi } from "@/api/pets";
import { livestockApi } from "@/api/livestock";
import { livestockHealthApi } from "@/api/carePlans";
import { Field, Muted, dateInput, fmtDate } from "@/pages/pets/detail/shared";

const sel = "h-9 rounded-md border bg-background px-2 text-sm w-full";

/** Herd-level diagnosis & treatment with meat/milk withdrawal tracking (bulk vaccination lives on the Groups tab). */
export function HerdHealthTab() {
  const qc = useQueryClient();
  const { data: withdrawal = [] } = useQuery({ queryKey: ["livestock", "withdrawal"], queryFn: () => livestockHealthApi.withdrawal() });
  const { data: treatments = [], isLoading } = useQuery({ queryKey: ["livestock", "treatments"], queryFn: () => livestockHealthApi.list() });
  const [open, setOpen] = useState(false);
  const refresh = () => qc.invalidateQueries({ queryKey: ["livestock"] });

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-2 flex-wrap">
        <p className="text-sm text-muted-foreground max-w-xl">
          Record a diagnosis and treatment for one animal or a whole group, with meat and milk withdrawal periods. Withdrawal dates are a record-keeping aid — always follow the product label and your vet.
        </p>
        <Button onClick={() => setOpen(true)}><Plus className="h-4 w-4 mr-1" /> Record treatment</Button>
      </div>

      {withdrawal.length > 0 && (
        <div className="rounded-md border border-amber-400 bg-amber-50 dark:bg-amber-950/40 p-3 space-y-1" role="status">
          <p className="font-medium text-sm">Animals currently in withdrawal ({withdrawal.length})</p>
          <ul className="text-sm space-y-0.5">
            {withdrawal.map((w) => (
              <li key={w.petId}><span className="font-medium">{w.petName}</span>
                {w.meatBlocked && <> · <Badge variant="destructive">No meat until {fmtDate(w.meatSafeOn)}</Badge></>}
                {w.milkBlocked && <> · <Badge variant="destructive">No milk until {fmtDate(w.milkSafeOn)}</Badge></>}
              </li>
            ))}
          </ul>
        </div>
      )}

      {isLoading ? <Muted>Loading…</Muted> : treatments.length === 0 ? (
        <EmptyState icon={Stethoscope} title="No treatments recorded" description="Record the first diagnosis and treatment." />
      ) : (
        <div className="overflow-x-auto"><table className="w-full text-sm">
          <thead><tr className="text-left text-xs text-muted-foreground"><th className="py-1">Animal</th><th>Diagnosis</th><th>Treatment</th><th>Date</th><th>Meat safe</th><th>Milk safe</th><th /></tr></thead>
          <tbody>
            {treatments.map((t) => (
              <tr key={t.id} className="border-t">
                <td className="py-1.5 font-medium">{t.petName}</td><td>{t.diagnosis}</td><td>{t.treatment}</td><td>{fmtDate(t.administeredOn)}</td>
                <td>{t.meatSafeOn ? fmtDate(t.meatSafeOn) : "—"}</td><td>{t.milkSafeOn ? fmtDate(t.milkSafeOn) : "—"}</td>
                <td><Button size="icon" variant="ghost" aria-label="Delete treatment" onClick={async () => { await livestockHealthApi.remove(t.id); refresh(); }}><Trash2 className="h-4 w-4" /></Button></td>
              </tr>
            ))}
          </tbody>
        </table></div>
      )}
      {open && <TreatmentDialog onClose={() => setOpen(false)} onSaved={refresh} />}
    </div>
  );
}

function TreatmentDialog({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const { data: groups = [] } = useQuery({ queryKey: ["livestock", "groups"], queryFn: livestockApi.groups });
  const { data: pets = [] } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const [target, setTarget] = useState("");   // "g:<id>" | "p:<id>"
  const [f, setF] = useState({ diagnosis: "", treatment: "", administeredOn: dateInput(), meat: "0", milk: "0", notes: "" });
  const [busy, setBusy] = useState(false);

  async function save() {
    if (!target) return toast.error("Choose an animal or group.");
    if (!f.diagnosis.trim() || !f.treatment.trim()) return toast.error("Diagnosis and treatment are required.");
    setBusy(true);
    try {
      const r = await livestockHealthApi.add({
        petGroupId: target.startsWith("g:") ? target.slice(2) : null, petId: target.startsWith("p:") ? target.slice(2) : null,
        diagnosis: f.diagnosis.trim(), treatment: f.treatment.trim(), administeredOn: f.administeredOn,
        meatWithdrawalDays: Math.max(0, Number(f.meat) || 0), milkWithdrawalDays: Math.max(0, Number(f.milk) || 0), notes: f.notes.trim() || null,
      });
      toast.success(`Treatment recorded for ${r.animals} animal${r.animals === 1 ? "" : "s"}.`);
      onSaved(); onClose();
    } catch { /* toasted */ } finally { setBusy(false); }
  }
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value });

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>Record treatment</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <Field label="Animal or group" htmlFor="ht-target">
            <select id="ht-target" className={sel} value={target} onChange={(e) => setTarget(e.target.value)}>
              <option value="">Choose…</option>
              <optgroup label="Groups">{groups.map((g) => <option key={g.id} value={`g:${g.id}`}>{g.name} ({g.memberCount} animals)</option>)}</optgroup>
              <optgroup label="Single animal">{pets.map((p) => <option key={p.id} value={`p:${p.id}`}>{p.name}</option>)}</optgroup>
            </select>
          </Field>
          <Field label="Diagnosis" htmlFor="ht-dx"><Input id="ht-dx" value={f.diagnosis} onChange={set("diagnosis")} /></Field>
          <Field label="Treatment / drug" htmlFor="ht-tx"><Input id="ht-tx" value={f.treatment} onChange={set("treatment")} /></Field>
          <div className="grid grid-cols-3 gap-3">
            <Field label="Date" htmlFor="ht-date"><Input id="ht-date" type="date" value={f.administeredOn} onChange={set("administeredOn")} /></Field>
            <Field label="Meat withdrawal (days)" htmlFor="ht-meat"><Input id="ht-meat" type="number" min={0} value={f.meat} onChange={set("meat")} /></Field>
            <Field label="Milk withdrawal (days)" htmlFor="ht-milk"><Input id="ht-milk" type="number" min={0} value={f.milk} onChange={set("milk")} /></Field>
          </div>
          <Field label="Notes" htmlFor="ht-notes"><Input id="ht-notes" value={f.notes} onChange={set("notes")} /></Field>
        </div>
        <DialogFooter><Button variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={busy} onClick={save}>Save</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
