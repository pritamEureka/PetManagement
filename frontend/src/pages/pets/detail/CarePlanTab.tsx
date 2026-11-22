import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarClock, Check, ClipboardList, Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { canEditPet, canLogCare, petsApi, type PetDetail } from "@/api/pets";
import {
  carePlansApi, carePlanItemTypes, type CarePlan, type CarePlanItem, type CarePlanItemType,
} from "@/api/carePlans";
import { Section, Field, Muted, dateInput, fmtDate } from "./shared";

const sel = "h-9 rounded-md border bg-background px-2 text-sm w-full";

export function CarePlanTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const editor = canEditPet(pet.myAccess);
  const logger = canLogCare(pet.myAccess);
  const { data: plans = [], isLoading } = useQuery({ queryKey: ["pets", pet.id, "care-plans"], queryFn: () => carePlansApi.plans(pet.id) });
  const { data: summary } = useQuery({ queryKey: ["pets", pet.id, "care-plans", "summary"], queryFn: () => carePlansApi.summary(pet.id) });
  const { data: team = [] } = useQuery({ queryKey: ["pets", pet.id, "caretakers"], queryFn: () => petsApi.caretakers.list(pet.id), enabled: editor });
  const [adopting, setAdopting] = useState(false);
  const [creating, setCreating] = useState(false);
  const [resched, setResched] = useState<CarePlanItem | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: ["pets", pet.id, "care-plans"] });

  async function complete(item: CarePlanItem) {
    const canRecord = (item.type === "Vaccination" || item.type === "Grooming") && (editor || pet.myAccess === "Vet");
    try {
      await carePlansApi.complete(pet.id, item.id, { createRecord: canRecord && window.confirm(`Also add "${item.title}" to the pet's ${item.type.toLowerCase()} records?`) });
      toast.success("Marked as done.");
      refresh();
      if (canRecord) qc.invalidateQueries({ queryKey: ["pets", pet.id] });
    } catch { /* toasted */ }
  }

  return (
    <div className="space-y-4">
      {summary && (summary.missedCount > 0 || summary.dueSoonCount > 0) && (
        <div className={`rounded-md px-3 py-2 text-sm ${summary.missedCount ? "bg-red-100 text-red-900 dark:bg-red-950 dark:text-red-100" : "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-100"}`}>
          {summary.missedCount > 0 && <p><strong>{summary.missedCount} missed</strong>: {summary.missed.slice(0, 3).map((m) => `${m.title} (${fmtDate(m.dueDate)})`).join(", ")}{summary.missedCount > 3 ? "…" : ""}</p>}
          {summary.dueSoonCount > 0 && <p>{summary.dueSoonCount} due in the next week.</p>}
        </div>
      )}

      <Section title="Preventive care plans" action={editor && (
        <div className="flex gap-2">
          <Button size="sm" variant="outline" onClick={() => setAdopting(true)}><ClipboardList className="h-3.5 w-3.5 mr-1" /> Use a vet plan</Button>
          <Button size="sm" onClick={() => setCreating(true)}><Plus className="h-3.5 w-3.5 mr-1" /> Custom plan</Button>
        </div>
      )}>
        {isLoading ? <Muted>Loading…</Muted> : plans.length === 0 ? <Muted>No care plans yet. Adopt a vet-published plan or build your own.</Muted> : (
          <div className="space-y-5">
            {plans.map((p) => (
              <PlanCard key={p.id} plan={p} petId={pet.id} editor={editor} logger={logger} team={team.map((t) => ({ id: t.userId, name: t.displayName }))}
                onComplete={complete} onReschedule={setResched} onChanged={refresh} />
            ))}
          </div>
        )}
      </Section>

      {adopting && <AdoptDialog pet={pet} onClose={() => setAdopting(false)} onDone={refresh} />}
      {creating && <CustomPlanDialog pet={pet} onClose={() => setCreating(false)} onDone={refresh} />}
      {resched && <RescheduleDialog petId={pet.id} item={resched} onClose={() => setResched(null)} onDone={refresh} />}
    </div>
  );
}

function PlanCard({ plan, petId, editor, logger, team, onComplete, onReschedule, onChanged }: {
  plan: CarePlan; petId: string; editor: boolean; logger: boolean; team: { id: string; name: string }[];
  onComplete: (i: CarePlanItem) => void; onReschedule: (i: CarePlanItem) => void; onChanged: () => void;
}) {
  const [all, setAll] = useState(false);
  const open = plan.items.filter((i) => i.status !== "Done");
  const shown = all ? plan.items : open.slice(0, 8);
  return (
    <div className="rounded-lg border p-3 space-y-2">
      <div className="flex items-start justify-between gap-2 flex-wrap">
        <div>
          <p className="font-medium">{plan.title} {plan.isCustom && <Badge variant="outline">Custom</Badge>} {plan.status === "Completed" && <Badge variant="success">Completed</Badge>}</p>
          <p className="text-xs text-muted-foreground">Started {fmtDate(plan.startDate)} · {plan.done} done · {plan.pending} upcoming · {plan.missed} missed</p>
        </div>
        {editor && (
          <Button size="icon" variant="ghost" aria-label={`Archive ${plan.title}`}
            onClick={async () => { await carePlansApi.archive(petId, plan.id); onChanged(); }}><Trash2 className="h-4 w-4" /></Button>
        )}
      </div>
      <ul className="divide-y">
        {shown.map((i) => (
          <li key={i.id} className="py-2 flex items-center justify-between gap-2 flex-wrap text-sm">
            <div className="min-w-0">
              <p className={i.status === "Done" ? "line-through text-muted-foreground" : "font-medium"}>{i.title}</p>
              <p className="text-xs text-muted-foreground">
                {i.type} · due {fmtDate(i.dueDate)}{i.originalDueDate ? ` (was ${fmtDate(i.originalDueDate)})` : ""}
                {i.assignedUserName ? ` · ${i.assignedUserName}` : ""}
              </p>
            </div>
            <div className="flex items-center gap-1.5">
              {i.isMissed && i.status !== "Done" ? <Badge variant="destructive">Missed</Badge>
                : i.status === "Done" ? <Badge variant="success">Done</Badge>
                : i.status === "Rescheduled" ? <Badge variant="warning">Rescheduled</Badge> : <Badge variant="muted">Pending</Badge>}
              {editor && i.status !== "Done" && team.length > 0 && (
                <select aria-label="Assign caretaker" className="h-8 rounded-md border bg-background px-1 text-xs max-w-[9rem]" value={i.assignedUserId ?? ""}
                  onChange={async (e) => { await carePlansApi.assign(petId, i.id, e.target.value || null); onChanged(); }}>
                  <option value="">Anyone</option>
                  {team.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select>
              )}
              {logger && i.status !== "Done" && (
                <>
                  <Button size="sm" variant="outline" onClick={() => onReschedule(i)}><CalendarClock className="h-3.5 w-3.5 mr-1" /> Reschedule</Button>
                  <Button size="sm" onClick={() => onComplete(i)}><Check className="h-3.5 w-3.5 mr-1" /> Done</Button>
                </>
              )}
            </div>
          </li>
        ))}
      </ul>
      {plan.items.length > shown.length || all ? (
        <Button size="sm" variant="ghost" onClick={() => setAll(!all)}>{all ? "Show upcoming only" : `Show all ${plan.items.length} items`}</Button>
      ) : null}
    </div>
  );
}

function AdoptDialog({ pet, onClose, onDone }: { pet: PetDetail; onClose: () => void; onDone: () => void }) {
  const { data: templates = [], isLoading } = useQuery({ queryKey: ["care-plan-templates", pet.animalType], queryFn: () => carePlansApi.templates(pet.animalType) });
  const [templateId, setTemplateId] = useState("");
  const [start, setStart] = useState(dateInput());
  const [busy, setBusy] = useState(false);
  const picked = templates.find((t) => t.id === templateId);
  async function go() {
    if (!templateId) return toast.error("Pick a plan.");
    setBusy(true);
    try { await carePlansApi.adopt(pet.id, { templateId, startDate: start }); toast.success("Plan added."); onDone(); onClose(); }
    catch { /* toasted */ } finally { setBusy(false); }
  }
  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>Adopt a vet-published plan</DialogTitle></DialogHeader>
        {isLoading ? <Muted>Loading…</Muted> : templates.length === 0 ? <Muted>No published plans for {pet.animalType} yet.</Muted> : (
          <div className="space-y-3">
            <Field label="Plan" htmlFor="tpl">
              <select id="tpl" className={sel} value={templateId} onChange={(e) => setTemplateId(e.target.value)}>
                <option value="">Choose…</option>
                {templates.filter((t) => t.isPublished).map((t) => <option key={t.id} value={t.id}>{t.title}{t.breed ? ` · ${t.breed}` : ""}</option>)}
              </select>
            </Field>
            {picked && <p className="text-xs text-muted-foreground">{picked.description} ({picked.items.length} steps)</p>}
            <Field label="Start date" htmlFor="tpl-start"><Input id="tpl-start" type="date" value={start} onChange={(e) => setStart(e.target.value)} /></Field>
          </div>
        )}
        <DialogFooter><Button variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={busy || !templateId} onClick={go}>Add plan</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function CustomPlanDialog({ pet, onClose, onDone }: { pet: PetDetail; onClose: () => void; onDone: () => void }) {
  const [title, setTitle] = useState("");
  const [rows, setRows] = useState<{ type: CarePlanItemType; title: string; dueDate: string }[]>([{ type: "Vaccination", title: "", dueDate: dateInput() }]);
  const [busy, setBusy] = useState(false);
  const setRow = (i: number, patch: Partial<(typeof rows)[number]>) => setRows((r) => r.map((x, j) => (j === i ? { ...x, ...patch } : x)));
  async function go() {
    const items = rows.filter((r) => r.title.trim());
    if (!title.trim() || items.length === 0) return toast.error("Add a plan name and at least one item.");
    setBusy(true);
    try { await carePlansApi.createCustom(pet.id, { title: title.trim(), items: items.map((r) => ({ ...r, title: r.title.trim() })) }); toast.success("Plan created."); onDone(); onClose(); }
    catch { /* toasted */ } finally { setBusy(false); }
  }
  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader><DialogTitle>Custom care plan</DialogTitle></DialogHeader>
        <div className="space-y-3 max-h-[60vh] overflow-y-auto pr-1">
          <Field label="Plan name" htmlFor="cp-title"><Input id="cp-title" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Puppy first year" /></Field>
          {rows.map((r, i) => (
            <div key={i} className="grid grid-cols-[8rem_1fr_9rem] gap-2">
              <select aria-label="Type" className={sel} value={r.type} onChange={(e) => setRow(i, { type: e.target.value as CarePlanItemType })}>
                {carePlanItemTypes.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
              <Input aria-label="Title" value={r.title} onChange={(e) => setRow(i, { title: e.target.value })} placeholder="What needs doing" />
              <Input aria-label="Due date" type="date" value={r.dueDate} onChange={(e) => setRow(i, { dueDate: e.target.value })} />
            </div>
          ))}
          <Button size="sm" variant="outline" onClick={() => setRows((r) => [...r, { type: "Medication", title: "", dueDate: dateInput() }])}><Plus className="h-3.5 w-3.5 mr-1" /> Add item</Button>
        </div>
        <DialogFooter><Button variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={busy} onClick={go}>Create plan</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function RescheduleDialog({ petId, item, onClose, onDone }: { petId: string; item: CarePlanItem; onClose: () => void; onDone: () => void }) {
  const [date, setDate] = useState(dateInput());
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  async function go() {
    setBusy(true);
    try { await carePlansApi.reschedule(petId, item.id, date, reason || undefined); toast.success("Rescheduled."); onDone(); onClose(); }
    catch { /* toasted */ } finally { setBusy(false); }
  }
  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>Reschedule "{item.title}"</DialogTitle></DialogHeader>
        <Field label="New date" htmlFor="rs-date"><Input id="rs-date" type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label="Reason (optional)" htmlFor="rs-reason"><Input id="rs-reason" value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
        <DialogFooter><Button variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={busy} onClick={go}>Reschedule</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
