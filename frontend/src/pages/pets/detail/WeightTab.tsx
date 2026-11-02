import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2 } from "lucide-react";
import { CartesianGrid, Line, LineChart, ReferenceArea, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/sonner";
import { petsApi, canEditPet, canLogCare, type PetDetail } from "@/api/pets";
import { Section, Field, Muted, dateInput, fmtDate } from "./shared";

export function WeightTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ["pets", pet.id, "weights"], queryFn: () => petsApi.weights.list(pet.id) });
  const [f, setF] = useState({ recordedOn: dateInput(), weightKg: "", notes: "" });
  const [busy, setBusy] = useState(false);

  const entries = data?.entries ?? [];
  const range = data?.healthyRange;
  const chart = entries.map((e) => ({ date: new Date(e.recordedOn).toLocaleDateString(), kg: e.weightKg }));
  const latest = entries.at(-1);
  const outOfRange = latest && range && (latest.weightKg < range.minKg || latest.weightKg > range.maxKg);

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["pets", pet.id] });
    qc.invalidateQueries({ queryKey: ["pets", "mine"] });
  };

  async function add(e: React.FormEvent) {
    e.preventDefault();
    const kg = Number(f.weightKg);
    if (!kg || kg <= 0) return toast.error("Enter a weight above 0.");
    setBusy(true);
    try {
      await petsApi.weights.add(pet.id, { recordedOn: f.recordedOn, weightKg: kg, notes: f.notes || undefined });
      toast.success("Weight logged.");
      setF({ recordedOn: dateInput(), weightKg: "", notes: "" });
      refresh();
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  return (
    <div className="space-y-4">
      <Section title="Weight trend">
        {range && (
          <p className="text-xs text-muted-foreground mb-2">
            Typical healthy range: <strong>{range.minKg}–{range.maxKg} kg</strong> ({range.basis}). Set the pet's size for a better estimate.
          </p>
        )}
        {outOfRange && (
          <p className="text-sm rounded-md bg-amber-100 dark:bg-amber-950 text-amber-900 dark:text-amber-100 px-3 py-2 mb-3">
            The latest weight ({latest!.weightKg} kg) is outside the typical range — worth mentioning at your next vet visit.
          </p>
        )}
        {isLoading ? <Muted>Loading…</Muted> : chart.length < 2 ? (
          <Muted>Log at least two weights to see a trend.</Muted>
        ) : (
          <div className="h-64">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={chart} margin={{ top: 8, right: 12, bottom: 0, left: -12 }}>
                <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                <XAxis dataKey="date" fontSize={11} tickLine={false} />
                <YAxis fontSize={11} tickLine={false} unit=" kg" domain={["auto", "auto"]} />
                {range && <ReferenceArea y1={range.minKg} y2={range.maxKg} fill="hsl(var(--primary))" fillOpacity={0.08} />}
                <Tooltip formatter={(v: number) => [`${v} kg`, "Weight"]} />
                <Line type="monotone" dataKey="kg" stroke="hsl(var(--primary))" strokeWidth={2} dot={{ r: 3 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </Section>

      {canLogCare(pet.myAccess) && (
        <Section title="Log weight">
          <form onSubmit={add} className="grid grid-cols-2 sm:grid-cols-4 gap-3 items-end">
            <Field label="Date" htmlFor="wd"><Input id="wd" type="date" max={dateInput()} value={f.recordedOn} onChange={(e) => setF({ ...f, recordedOn: e.target.value })} /></Field>
            <Field label="Weight (kg)" htmlFor="wk"><Input id="wk" type="number" min="0" step="0.01" value={f.weightKg} onChange={(e) => setF({ ...f, weightKg: e.target.value })} /></Field>
            <Field label="Note" htmlFor="wn"><Input id="wn" value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
            <Button type="submit" disabled={busy}><Plus className="h-4 w-4 mr-1" /> Add</Button>
          </form>
        </Section>
      )}

      {entries.length > 0 && (
        <Section title="History">
          <div className="divide-y">
            {[...entries].reverse().map((w) => (
              <div key={w.id} className="py-2 flex items-center justify-between text-sm">
                <span>{fmtDate(w.recordedOn)}{w.notes ? <span className="text-muted-foreground"> · {w.notes}</span> : null}</span>
                <span className="flex items-center gap-2 font-medium">
                  {w.weightKg} kg
                  {canEditPet(pet.myAccess) && (
                    <Button size="icon" variant="ghost" aria-label="Delete entry"
                      onClick={async () => { await petsApi.weights.remove(pet.id, w.id); refresh(); }}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </span>
              </div>
            ))}
          </div>
        </Section>
      )}
    </div>
  );
}
