import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardList, Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { toast } from "@/components/ui/sonner";
import { animalTypes } from "@/lib/schemas";
import { carePlansApi, carePlanItemTypes, type CarePlanItemType, type CarePlanTemplate, type CarePlanTemplateItemInput } from "@/api/carePlans";
import { Field } from "@/pages/pets/detail/shared";

const sel = "h-9 rounded-md border bg-background px-2 text-sm w-full";

/** Vets (vets.edit) publish preventive-care templates that owners can adopt for their pets. */
export function CarePlanTemplatesPage() {
  const qc = useQueryClient();
  const { data: templates = [], isLoading } = useQuery({ queryKey: ["care-plan-templates", "mine"], queryFn: () => carePlansApi.templates(undefined, true) });
  const [editing, setEditing] = useState<CarePlanTemplate | "new" | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: ["care-plan-templates"] });

  return (
    <div className="space-y-4 max-w-4xl mx-auto">
      <PageHeader title="Care plan templates" icon={ClipboardList}
        description="Publish preventive-care schedules (vaccines, parasite control, check-ups) that pet owners can adopt in one click." />
      <div className="flex justify-end"><Button onClick={() => setEditing("new")}><Plus className="h-4 w-4 mr-1" /> New template</Button></div>
      {isLoading ? <p className="text-sm text-muted-foreground">Loading…</p> : templates.length === 0 ? (
        <EmptyState icon={ClipboardList} title="No templates yet" description="Create your first care plan template." />
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {templates.map((t) => (
            <Card key={t.id}><CardContent className="pt-5 space-y-2">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="font-medium">{t.title}</p>
                  <p className="text-xs text-muted-foreground">{t.species}{t.breed ? ` · ${t.breed}` : ""} · {t.items.length} items</p>
                </div>
                <Badge variant={t.isPublished ? "success" : "muted"}>{t.isPublished ? "Published" : "Draft"}</Badge>
              </div>
              {t.description && <p className="text-sm text-muted-foreground line-clamp-2">{t.description}</p>}
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="outline" onClick={() => setEditing(t)}>Edit</Button>
                <Button size="sm" variant="outline" onClick={async () => { await carePlansApi.publish(t.id, !t.isPublished); refresh(); }}>{t.isPublished ? "Unpublish" : "Publish"}</Button>
                <Button size="icon" variant="ghost" aria-label={`Delete ${t.title}`} onClick={async () => { if (window.confirm("Delete this template? Plans already adopted keep their items.")) { await carePlansApi.deleteTemplate(t.id); refresh(); } }}><Trash2 className="h-4 w-4" /></Button>
              </div>
            </CardContent></Card>
          ))}
        </div>
      )}
      {editing && <TemplateDialog template={editing === "new" ? undefined : editing} onClose={() => setEditing(null)} onSaved={refresh} />}
    </div>
  );
}

type Row = { type: CarePlanItemType; title: string; offsetDays: string; repeatEveryDays: string; repeatCount: string; notes: string };

function TemplateDialog({ template, onClose, onSaved }: { template?: CarePlanTemplate; onClose: () => void; onSaved: () => void }) {
  const [title, setTitle] = useState(template?.title ?? "");
  const [species, setSpecies] = useState(template?.species ?? "Dog");
  const [breed, setBreed] = useState(template?.breed ?? "");
  const [description, setDescription] = useState(template?.description ?? "");
  const [published, setPublished] = useState(template?.isPublished ?? false);
  const [rows, setRows] = useState<Row[]>(template?.items.map((i) => ({
    type: i.type, title: i.title, offsetDays: String(i.offsetDays), repeatEveryDays: i.repeatEveryDays ? String(i.repeatEveryDays) : "",
    repeatCount: i.repeatCount ? String(i.repeatCount) : "", notes: i.notes ?? "",
  })) ?? [{ type: "Vaccination", title: "", offsetDays: "0", repeatEveryDays: "", repeatCount: "", notes: "" }]);
  const [busy, setBusy] = useState(false);
  const setRow = (i: number, p: Partial<Row>) => setRows((r) => r.map((x, j) => (j === i ? { ...x, ...p } : x)));

  async function save() {
    const items: CarePlanTemplateItemInput[] = rows.filter((r) => r.title.trim()).map((r) => ({
      type: r.type, title: r.title.trim(), offsetDays: Math.max(0, Number(r.offsetDays) || 0),
      repeatEveryDays: Number(r.repeatEveryDays) > 0 ? Number(r.repeatEveryDays) : null,
      repeatCount: Number(r.repeatEveryDays) > 0 && Number(r.repeatCount) > 0 ? Number(r.repeatCount) : null, notes: r.notes.trim() || null,
    }));
    if (!title.trim() || items.length === 0) return toast.error("Add a title and at least one item.");
    setBusy(true);
    const body = { title: title.trim(), species, breed: breed.trim() || null, description: description.trim() || null, isPublished: published, items };
    try { if (template) await carePlansApi.updateTemplate(template.id, body); else await carePlansApi.createTemplate(body); toast.success("Template saved."); onSaved(); onClose(); }
    catch { /* toasted */ } finally { setBusy(false); }
  }

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader><DialogTitle>{template ? "Edit template" : "New template"}</DialogTitle></DialogHeader>
        <div className="space-y-3 max-h-[65vh] overflow-y-auto pr-1">
          <div className="grid sm:grid-cols-3 gap-3">
            <Field label="Title" htmlFor="t-title"><Input id="t-title" value={title} onChange={(e) => setTitle(e.target.value)} /></Field>
            <Field label="Species" htmlFor="t-sp"><select id="t-sp" className={sel} value={species} onChange={(e) => setSpecies(e.target.value)}>{animalTypes.map((a) => <option key={a}>{a}</option>)}</select></Field>
            <Field label="Breed (optional)" htmlFor="t-br"><Input id="t-br" value={breed} onChange={(e) => setBreed(e.target.value)} /></Field>
          </div>
          <Field label="Description" htmlFor="t-desc"><Textarea id="t-desc" rows={2} value={description} onChange={(e) => setDescription(e.target.value)} /></Field>
          <div className="space-y-2">
            <p className="text-sm font-medium">Items <span className="font-normal text-muted-foreground">(days after the owner's start date; optionally repeating)</span></p>
            {rows.map((r, i) => (
              <div key={i} className="grid grid-cols-[7rem_1fr_4.5rem_4.5rem_4.5rem_2rem] gap-2 items-center">
                <select aria-label="Type" className={sel} value={r.type} onChange={(e) => setRow(i, { type: e.target.value as CarePlanItemType })}>{carePlanItemTypes.map((t) => <option key={t}>{t}</option>)}</select>
                <Input aria-label="Title" placeholder="Title" value={r.title} onChange={(e) => setRow(i, { title: e.target.value })} />
                <Input aria-label="Offset days" type="number" min={0} title="Days after start" value={r.offsetDays} onChange={(e) => setRow(i, { offsetDays: e.target.value })} />
                <Input aria-label="Repeat every days" type="number" min={0} placeholder="every" title="Repeat every N days" value={r.repeatEveryDays} onChange={(e) => setRow(i, { repeatEveryDays: e.target.value })} />
                <Input aria-label="Repeat count" type="number" min={0} placeholder="times" title="Total occurrences" value={r.repeatCount} onChange={(e) => setRow(i, { repeatCount: e.target.value })} />
                <Button size="icon" variant="ghost" aria-label="Remove item" onClick={() => setRows((x) => x.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
              </div>
            ))}
            <Button size="sm" variant="outline" onClick={() => setRows((r) => [...r, { type: "Vaccination", title: "", offsetDays: "0", repeatEveryDays: "", repeatCount: "", notes: "" }])}><Plus className="h-3.5 w-3.5 mr-1" /> Add item</Button>
          </div>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={published} onChange={(e) => setPublished(e.target.checked)} /> Publish (visible to all pet owners)</label>
        </div>
        <DialogFooter><Button variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={busy} onClick={save}>Save</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
