import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, Circle, Flame, Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/sonner";
import { petsApi, canEditPet, canLogCare, careActivityTypes, type CareActivityType, type CareRoutine, type PetDetail } from "@/api/pets";
import { RecordDialog, useSave } from "./HealthTab";
import { Section, Field, Muted } from "./shared";

const emoji: Record<CareActivityType, string> = {
  Feeding: "🍖", Walk: "🦮", Medication: "💊", Litter: "🧹", Bath: "🛁", Play: "🎾", Training: "🎓", Other: "📝"
};

export function CareTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const [adding, setAdding] = useState(false);
  const caretaker = canLogCare(pet.myAccess);
  const editor = canEditPet(pet.myAccess);

  const { data, isLoading } = useQuery({ queryKey: ["pets", pet.id, "care-today"], queryFn: () => petsApi.care.today(pet.id) });
  const { data: routines = [] } = useQuery({ queryKey: ["pets", pet.id, "routines"], queryFn: () => petsApi.care.routines(pet.id) });
  const refresh = () => qc.invalidateQueries({ queryKey: ["pets", pet.id] });

  async function tick(r: CareRoutine) {
    await petsApi.care.log(pet.id, { activityType: r.activityType, routineId: r.id });
    toast.success(`${r.title} done.`);
    refresh();
  }

  async function quickLog(type: CareActivityType) {
    await petsApi.care.log(pet.id, { activityType: type });
    toast.success(`${type} logged.`);
    refresh();
  }

  return (
    <div className="space-y-4">
      <Section
        title="Today"
        action={data && data.streakDays > 0 && (
          <Badge variant="warning" className="gap-1"><Flame className="h-3.5 w-3.5" /> {data.streakDays}-day streak</Badge>
        )}
      >
        {isLoading ? <Muted>Loading…</Muted> : !data || data.checklist.length === 0 ? (
          <Muted>{editor ? "Add routines below to get a daily checklist." : "No routines set up yet."}</Muted>
        ) : (
          <div className="divide-y">
            {data.checklist.map(({ routine: r, done, doneAt, doneBy }) => (
              <div key={r.id} className="py-2 flex items-center justify-between gap-3">
                <div className="flex items-center gap-3 min-w-0">
                  {done ? <CheckCircle2 className="h-5 w-5 text-emerald-600 shrink-0" /> : <Circle className="h-5 w-5 text-muted-foreground shrink-0" />}
                  <div className="min-w-0">
                    <p className={done ? "text-sm line-through text-muted-foreground" : "text-sm font-medium"}>
                      {emoji[r.activityType]} {r.title}{r.timeOfDay ? <span className="text-muted-foreground font-normal"> · {r.timeOfDay.slice(0, 5)}</span> : null}
                    </p>
                    {done ? (
                      <p className="text-xs text-muted-foreground">
                        {doneBy} at {doneAt ? new Date(doneAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) : ""}
                      </p>
                    ) : r.details ? <p className="text-xs text-muted-foreground">{r.details}</p> : null}
                  </div>
                </div>
                {!done && caretaker && <Button size="sm" variant="outline" onClick={() => tick(r)}>Done</Button>}
              </div>
            ))}
          </div>
        )}
        {caretaker && (
          <div className="flex flex-wrap gap-2 mt-4 pt-3 border-t">
            <span className="text-xs text-muted-foreground self-center mr-1">Quick log:</span>
            {(["Feeding", "Walk", "Play", "Bath", "Litter"] as CareActivityType[]).map((t) => (
              <Button key={t} size="sm" variant="secondary" onClick={() => quickLog(t)}>{emoji[t]} {t}</Button>
            ))}
          </div>
        )}
      </Section>

      <Section title="Routines" action={editor && <Button size="sm" onClick={() => setAdding(true)}><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>}>
        {routines.length === 0 ? <Muted>No routines yet — e.g. "Breakfast 08:00", "Evening walk 18:30".</Muted> : (
          <div className="divide-y">
            {routines.map((r) => (
              <div key={r.id} className="py-2 flex items-center justify-between text-sm">
                <span>
                  {emoji[r.activityType]} {r.title}
                  <span className="text-muted-foreground">{r.timeOfDay ? ` · ${r.timeOfDay.slice(0, 5)}` : ""}{r.details ? ` · ${r.details}` : ""}</span>
                </span>
                {editor && (
                  <Button size="icon" variant="ghost" aria-label={`Delete ${r.title}`}
                    onClick={async () => { await petsApi.care.removeRoutine(pet.id, r.id); refresh(); }}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                )}
              </div>
            ))}
          </div>
        )}
      </Section>

      {data && data.recentLogs.length > 0 && (
        <Section title="Recent activity">
          <div className="divide-y">
            {data.recentLogs.map((l) => (
              <div key={l.id} className="py-2 text-sm flex justify-between gap-3">
                <span>{emoji[l.activityType]} {l.activityType}{l.notes ? <span className="text-muted-foreground"> · {l.notes}</span> : null}</span>
                <span className="text-xs text-muted-foreground shrink-0">{l.performedByName} · {new Date(l.performedAt).toLocaleString()}</span>
              </div>
            ))}
          </div>
        </Section>
      )}

      {adding && <RoutineDialog petId={pet.id} onClose={() => setAdding(false)} onSaved={refresh} />}
    </div>
  );
}

function RoutineDialog({ petId, onClose, onSaved }: { petId: string; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState<{ activityType: CareActivityType; title: string; timeOfDay: string; details: string }>({
    activityType: "Feeding", title: "", timeOfDay: "", details: ""
  });
  const { busy, run } = useSave(onSaved, onClose);

  const submit = () => {
    if (!f.title.trim()) return toast.error("Give the routine a name.");
    run(() => petsApi.care.addRoutine(petId, {
      activityType: f.activityType, title: f.title.trim(),
      timeOfDay: f.timeOfDay ? `${f.timeOfDay}:00` : null, details: f.details.trim() || null, isActive: true
    }), "Routine added.");
  };

  return (
    <RecordDialog title="Add routine" busy={busy} onClose={onClose} onSubmit={submit}>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Type">
          <Select value={f.activityType} onValueChange={(v) => setF({ ...f, activityType: v as CareActivityType })}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>{careActivityTypes.map((t) => <SelectItem key={t} value={t}>{emoji[t]} {t}</SelectItem>)}</SelectContent>
          </Select>
        </Field>
        <Field label="Time (optional)" htmlFor="rt"><Input id="rt" type="time" value={f.timeOfDay} onChange={(e) => setF({ ...f, timeOfDay: e.target.value })} /></Field>
      </div>
      <Field label="Name" htmlFor="rn"><Input id="rn" placeholder="Breakfast" value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} /></Field>
      <Field label="Details" htmlFor="rd"><Input id="rd" placeholder="1 cup dry food" value={f.details} onChange={(e) => setF({ ...f, details: e.target.value })} /></Field>
    </RecordDialog>
  );
}
