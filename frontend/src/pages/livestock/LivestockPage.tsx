import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Syringe, Tractor, Trash2, Users } from "lucide-react";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { toast } from "@/components/ui/sonner";
import { animalTypes } from "@/lib/schemas";
import { petsApi } from "@/api/pets";
import { livestockApi, productionTypes, type BreedingRecord, type PetGroup, type ProductionType } from "@/api/livestock";
import { RecordDialog, useSave } from "@/pages/pets/detail/HealthTab";
import { Field, Muted, dateInput, orNull, fmtDate } from "@/pages/pets/detail/shared";

const NONE = "__none";

export function LivestockPage() {
  return (
    <div className="space-y-4">
      <PageHeader title="Herds & breeding" icon={Tractor}
        description="Group livestock or litters, vaccinate a whole group at once, and track production and breeding." />
      <Tabs defaultValue="groups">
        <TabsList>
          <TabsTrigger value="groups">Groups</TabsTrigger>
          <TabsTrigger value="production">Production</TabsTrigger>
          <TabsTrigger value="breeding">Breeding</TabsTrigger>
        </TabsList>
        <TabsContent value="groups"><GroupsTab /></TabsContent>
        <TabsContent value="production"><ProductionTab /></TabsContent>
        <TabsContent value="breeding"><BreedingTab /></TabsContent>
      </Tabs>
    </div>
  );
}

// ---------- Groups ----------

function GroupsTab() {
  const qc = useQueryClient();
  const { data: groups = [], isLoading } = useQuery({ queryKey: ["livestock", "groups"], queryFn: livestockApi.groups });
  const [editing, setEditing] = useState<PetGroup | "new" | null>(null);
  const [members, setMembers] = useState<PetGroup | null>(null);
  const [vaccinating, setVaccinating] = useState<PetGroup | null>(null);
  const [deleting, setDeleting] = useState<PetGroup | null>(null);
  const refresh = () => { qc.invalidateQueries({ queryKey: ["livestock"] }); qc.invalidateQueries({ queryKey: ["pets"] }); };

  return (
    <div className="space-y-3">
      <div className="flex justify-end"><Button onClick={() => setEditing("new")}><Plus className="h-4 w-4 mr-1" /> New group</Button></div>
      {isLoading ? <Muted>Loading…</Muted> : groups.length === 0 ? (
        <EmptyState icon={Users} title="No groups yet" description="Create a herd, flock or litter, then add your animals to it." />
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {groups.map((g) => (
            <Card key={g.id}>
              <CardHeader className="pb-2 flex flex-row items-start justify-between space-y-0 gap-2">
                <div>
                  <CardTitle className="text-base">{g.name}</CardTitle>
                  <p className="text-xs text-muted-foreground">{g.animalType}{g.purpose ? ` · ${g.purpose}` : ""} · {g.memberCount} animals</p>
                </div>
                <Button size="icon" variant="ghost" aria-label={`Delete ${g.name}`} onClick={() => setDeleting(g)}><Trash2 className="h-4 w-4" /></Button>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="flex flex-wrap gap-1.5">
                  {g.members.length === 0 ? <span className="text-sm text-muted-foreground">No animals yet.</span>
                    : g.members.map((m) => <Link key={m.id} to={`/pets/${m.id}`} className="text-xs rounded-full border px-2 py-0.5 hover:bg-muted">{m.name}</Link>)}
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button size="sm" variant="outline" onClick={() => setMembers(g)}><Users className="h-3.5 w-3.5 mr-1" /> Members</Button>
                  <Button size="sm" variant="outline" disabled={g.memberCount === 0} onClick={() => setVaccinating(g)}><Syringe className="h-3.5 w-3.5 mr-1" /> Vaccinate all</Button>
                  <Button size="sm" variant="ghost" onClick={() => setEditing(g)}>Edit</Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {editing && <GroupDialog record={editing === "new" ? undefined : editing} onClose={() => setEditing(null)} onSaved={refresh} />}
      {members && <MembersDialog group={members} onClose={() => setMembers(null)} onSaved={refresh} />}
      {vaccinating && <BulkVaccinateDialog group={vaccinating} onClose={() => setVaccinating(null)} onSaved={refresh} />}
      <ConfirmDialog open={!!deleting} onOpenChange={(v) => !v && setDeleting(null)} title={`Delete ${deleting?.name}?`}
        description="The animals stay in your account; they're just removed from this group." confirmLabel="Delete" destructive
        onConfirm={async () => { if (deleting) { await livestockApi.deleteGroup(deleting.id); refresh(); } }} />
    </div>
  );
}

function GroupDialog({ record, onClose, onSaved }: { record?: PetGroup; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState({ name: record?.name ?? "", animalType: record?.animalType ?? "Cow", purpose: record?.purpose ?? "", notes: record?.notes ?? "" });
  const { busy, run } = useSave(onSaved, onClose);
  const submit = () => {
    if (!f.name.trim()) return toast.error("Name is required.");
    const input = { name: f.name.trim(), animalType: f.animalType, purpose: orNull(f.purpose), notes: orNull(f.notes) };
    run(() => record ? livestockApi.updateGroup(record.id, input) : livestockApi.createGroup(input), record ? "Group updated." : "Group created.");
  };
  return (
    <RecordDialog title={record ? "Edit group" : "New group"} busy={busy} onClose={onClose} onSubmit={submit}>
      <Field label="Name" htmlFor="gn"><Input id="gn" placeholder="Dairy herd" value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} /></Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Species">
          <Select value={f.animalType} onValueChange={(v) => setF({ ...f, animalType: v })}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>{animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
          </Select>
        </Field>
        <Field label="Purpose" htmlFor="gp"><Input id="gp" placeholder="Dairy, layers, litter…" value={f.purpose} onChange={(e) => setF({ ...f, purpose: e.target.value })} /></Field>
      </div>
      <Field label="Notes" htmlFor="gno"><Textarea id="gno" rows={2} value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
    </RecordDialog>
  );
}

function MembersDialog({ group, onClose, onSaved }: { group: PetGroup; onClose: () => void; onSaved: () => void }) {
  const { data: pets = [] } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const own = pets.filter((p) => p.myAccess === "Owner");
  const [selected, setSelected] = useState<Set<string>>(new Set(group.members.map((m) => m.id)));
  const { busy, run } = useSave(onSaved, onClose);
  const toggle = (id: string) => setSelected((s) => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; });
  return (
    <RecordDialog title={`Members of ${group.name}`} busy={busy} onClose={onClose}
      onSubmit={() => run(() => livestockApi.setMembers(group.id, [...selected]), "Members updated.")}>
      {own.length === 0 ? <Muted>Add animals in My Pets first.</Muted> : (
        <div className="max-h-72 overflow-y-auto divide-y">
          {own.map((p) => (
            <label key={p.id} className="flex items-center gap-2 py-2 text-sm cursor-pointer">
              <Checkbox checked={selected.has(p.id)} onChange={() => toggle(p.id)} />
              {p.name} <span className="text-muted-foreground">· {p.animalType}{p.groupName && p.groupId !== group.id ? ` · in ${p.groupName}` : ""}</span>
            </label>
          ))}
        </div>
      )}
    </RecordDialog>
  );
}

function BulkVaccinateDialog({ group, onClose, onSaved }: { group: PetGroup; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState({ vaccineName: "", administeredOn: dateInput(), nextDueOn: "", administeredByVet: "", notes: "" });
  const { busy, run } = useSave(onSaved, onClose);
  const submit = () => {
    if (!f.vaccineName.trim()) return toast.error("Vaccine name is required.");
    if (f.nextDueOn && f.nextDueOn <= f.administeredOn) return toast.error("Next due must be after the date given.");
    run(() => livestockApi.bulkVaccinate(group.id, {
      vaccineName: f.vaccineName.trim(), administeredOn: f.administeredOn, nextDueOn: orNull(f.nextDueOn),
      administeredByVet: orNull(f.administeredByVet), notes: orNull(f.notes),
    }), `Recorded for ${group.memberCount} animals.`);
  };
  return (
    <RecordDialog title={`Vaccinate ${group.name} (${group.memberCount})`} busy={busy} onClose={onClose} onSubmit={submit}>
      <Field label="Vaccine" htmlFor="bv"><Input id="bv" placeholder="FMD" value={f.vaccineName} onChange={(e) => setF({ ...f, vaccineName: e.target.value })} /></Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Given on" htmlFor="bg"><Input id="bg" type="date" max={dateInput()} value={f.administeredOn} onChange={(e) => setF({ ...f, administeredOn: e.target.value })} /></Field>
        <Field label="Next due" htmlFor="bd"><Input id="bd" type="date" value={f.nextDueOn} onChange={(e) => setF({ ...f, nextDueOn: e.target.value })} /></Field>
      </div>
      <Field label="Given by" htmlFor="bb"><Input id="bb" value={f.administeredByVet} onChange={(e) => setF({ ...f, administeredByVet: e.target.value })} /></Field>
      <Field label="Notes" htmlFor="bn"><Input id="bn" value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
    </RecordDialog>
  );
}

// ---------- Production ----------

function ProductionTab() {
  const qc = useQueryClient();
  const { data: logs = [], isLoading } = useQuery({ queryKey: ["livestock", "production"], queryFn: () => livestockApi.production() });
  const { data: groups = [] } = useQuery({ queryKey: ["livestock", "groups"], queryFn: livestockApi.groups });
  const [f, setF] = useState({ target: "", type: "Milk" as ProductionType, quantity: "", unit: "L", recordedOn: dateInput(), notes: "" });
  const [busy, setBusy] = useState(false);
  const refresh = () => qc.invalidateQueries({ queryKey: ["livestock", "production"] });

  // Last 14 days total per type for the chart.
  const days = [...Array(14)].map((_, i) => { const d = new Date(); d.setDate(d.getDate() - 13 + i); return d.toISOString().slice(0, 10); });
  const types = [...new Set(logs.map((l) => l.type))];
  const chart = days.map((d) => ({
    day: d.slice(5),
    ...Object.fromEntries(types.map((t) => [t, logs.filter((l) => l.type === t && l.recordedOn.slice(0, 10) === d).reduce((s, l) => s + l.quantity, 0)])),
  }));

  async function add(e: React.FormEvent) {
    e.preventDefault();
    const qty = Number(f.quantity);
    if (!f.target) return toast.error("Choose a group.");
    if (!qty || qty <= 0) return toast.error("Quantity must be above 0.");
    setBusy(true);
    try {
      await livestockApi.addProduction({ petGroupId: f.target, type: f.type, quantity: qty, unit: f.unit, recordedOn: f.recordedOn, notes: orNull(f.notes) });
      toast.success("Logged.");
      setF({ ...f, quantity: "", notes: "" });
      refresh();
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader><CardTitle className="text-base">Log production</CardTitle></CardHeader>
        <CardContent>
          {groups.length === 0 ? <Muted>Create a group first.</Muted> : (
            <form onSubmit={add} className="grid grid-cols-2 md:grid-cols-6 gap-3 items-end">
              <Field label="Group">
                <Select value={f.target || NONE} onValueChange={(v) => setF({ ...f, target: v === NONE ? "" : v })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>Choose…</SelectItem>
                    {groups.map((g) => <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Type">
                <Select value={f.type} onValueChange={(v) => setF({ ...f, type: v as ProductionType, unit: v === "Eggs" ? "pcs" : v === "Milk" ? "L" : "kg" })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>{productionTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                </Select>
              </Field>
              <Field label="Quantity" htmlFor="pq"><Input id="pq" type="number" min="0" step="0.01" value={f.quantity} onChange={(e) => setF({ ...f, quantity: e.target.value })} /></Field>
              <Field label="Unit" htmlFor="pu"><Input id="pu" value={f.unit} onChange={(e) => setF({ ...f, unit: e.target.value })} /></Field>
              <Field label="Date" htmlFor="pdt"><Input id="pdt" type="date" max={dateInput()} value={f.recordedOn} onChange={(e) => setF({ ...f, recordedOn: e.target.value })} /></Field>
              <Button type="submit" disabled={busy}><Plus className="h-4 w-4 mr-1" /> Add</Button>
            </form>
          )}
        </CardContent>
      </Card>

      {types.length > 0 && (
        <Card>
          <CardHeader><CardTitle className="text-base">Last 14 days</CardTitle></CardHeader>
          <CardContent className="h-60">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chart} margin={{ top: 4, right: 8, bottom: 0, left: -12 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} className="stroke-muted" />
                <XAxis dataKey="day" fontSize={11} tickLine={false} />
                <YAxis fontSize={11} tickLine={false} />
                <Tooltip />
                {types.map((t, i) => <Bar key={t} dataKey={t} fill={["#2563eb", "#ea580c", "#65a30d", "#db2777", "#64748b"][i % 5]} />)}
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader><CardTitle className="text-base">Entries</CardTitle></CardHeader>
        <CardContent className="divide-y">
          {isLoading ? <Muted>Loading…</Muted> : logs.length === 0 ? <Muted>No entries yet.</Muted> : logs.slice(0, 50).map((l) => (
            <div key={l.id} className="py-2 flex items-center justify-between text-sm">
              <span>{fmtDate(l.recordedOn)} · {l.groupName ?? l.petName} · <strong>{l.quantity} {l.unit}</strong> {l.type.toLowerCase()}</span>
              <Button size="icon" variant="ghost" aria-label="Delete entry" onClick={async () => { await livestockApi.deleteProduction(l.id); refresh(); }}>
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}

// ---------- Breeding ----------

function BreedingTab() {
  const qc = useQueryClient();
  const { data: rows = [], isLoading } = useQuery({ queryKey: ["livestock", "breeding"], queryFn: livestockApi.breeding });
  const [editing, setEditing] = useState<BreedingRecord | "new" | null>(null);
  const refresh = () => { qc.invalidateQueries({ queryKey: ["livestock", "breeding"] }); qc.invalidateQueries({ queryKey: ["pets", "upcoming-care"] }); };
  const now = new Date().toISOString();

  return (
    <div className="space-y-3">
      <div className="flex justify-end"><Button onClick={() => setEditing("new")}><Plus className="h-4 w-4 mr-1" /> Record mating</Button></div>
      {isLoading ? <Muted>Loading…</Muted> : rows.length === 0 ? (
        <EmptyState icon={Tractor} title="No breeding records" description="Record a mating to get an expected birth date and a reminder 3 days before." />
      ) : (
        <div className="space-y-2">
          {rows.map((b) => (
            <Card key={b.id}>
              <CardContent className="py-3 flex items-start justify-between gap-3 flex-wrap">
                <div className="text-sm">
                  <p className="font-medium">
                    <Link to={`/pets/${b.damId}`} className="hover:underline">{b.damName}</Link>
                    {" × "}{b.sireId ? <Link to={`/pets/${b.sireId}`} className="hover:underline">{b.sireName}</Link> : (b.sireDescription ?? "unknown sire")}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Mated {fmtDate(b.matedOn)}{b.expectedDueOn ? ` · due ${fmtDate(b.expectedDueOn)}` : ""}
                    {b.outcomeOn ? ` · born ${fmtDate(b.outcomeOn)}${b.offspringCount != null ? ` (${b.offspringCount})` : ""}` : ""}
                  </p>
                  {b.notes && <p className="text-xs text-muted-foreground">{b.notes}</p>}
                </div>
                <div className="flex items-center gap-2">
                  {b.outcomeOn ? <Badge variant="success">Delivered</Badge>
                    : b.expectedDueOn && b.expectedDueOn < now ? <Badge variant="warning">Due</Badge>
                    : <Badge variant="info">Expecting</Badge>}
                  <Button size="sm" variant="ghost" onClick={() => setEditing(b)}>Update</Button>
                  <Button size="icon" variant="ghost" aria-label="Delete record" onClick={async () => { await livestockApi.deleteBreeding(b.id); refresh(); }}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
      {editing && <BreedingDialog record={editing === "new" ? undefined : editing} onClose={() => setEditing(null)} onSaved={refresh} />}
    </div>
  );
}

function BreedingDialog({ record, onClose, onSaved }: { record?: BreedingRecord; onClose: () => void; onSaved: () => void }) {
  const { data: pets = [] } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const own = pets.filter((p) => p.myAccess === "Owner");
  const [f, setF] = useState({
    damId: record?.damId ?? "", sireId: record?.sireId ?? "", sireDescription: record?.sireDescription ?? "",
    matedOn: dateInput(record?.matedOn), expectedDueOn: record?.expectedDueOn ? dateInput(record.expectedDueOn) : "",
    outcomeOn: record?.outcomeOn ? dateInput(record.outcomeOn) : "", offspringCount: record?.offspringCount?.toString() ?? "", notes: record?.notes ?? "",
  });
  const dam = own.find((p) => p.id === f.damId);
  const { busy, run } = useSave(onSaved, onClose);
  const submit = () => {
    if (!f.damId) return toast.error("Choose the mother.");
    const input = {
      damId: f.damId, sireId: orNull(f.sireId), sireDescription: orNull(f.sireDescription), matedOn: f.matedOn,
      expectedDueOn: orNull(f.expectedDueOn), outcomeOn: orNull(f.outcomeOn),
      offspringCount: f.offspringCount ? Number(f.offspringCount) : null, notes: orNull(f.notes),
    };
    run(() => record ? livestockApi.updateBreeding(record.id, input) : livestockApi.addBreeding(input), "Saved.");
  };
  return (
    <RecordDialog title={record ? "Update breeding record" : "Record mating"} busy={busy} onClose={onClose} onSubmit={submit}>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Dam (mother)">
          <Select value={f.damId || NONE} onValueChange={(v) => setF({ ...f, damId: v === NONE ? "" : v, sireId: "" })}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>Choose…</SelectItem>
              {own.filter((p) => p.gender !== "Male").map((p) => <SelectItem key={p.id} value={p.id}>{p.name} · {p.animalType}</SelectItem>)}
            </SelectContent>
          </Select>
        </Field>
        <Field label="Sire (father)">
          <Select value={f.sireId || NONE} onValueChange={(v) => setF({ ...f, sireId: v === NONE ? "" : v })}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE}>Not my animal / unknown</SelectItem>
              {own.filter((p) => p.gender !== "Female" && (!dam || p.animalType === dam.animalType)).map((p) => <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>)}
            </SelectContent>
          </Select>
        </Field>
      </div>
      {!f.sireId && <Field label="Sire details" htmlFor="sd"><Input id="sd" placeholder="Neighbour's bull, AI straw #…" value={f.sireDescription} onChange={(e) => setF({ ...f, sireDescription: e.target.value })} /></Field>}
      <div className="grid grid-cols-2 gap-3">
        <Field label="Mated on" htmlFor="mo"><Input id="mo" type="date" max={dateInput()} value={f.matedOn} onChange={(e) => setF({ ...f, matedOn: e.target.value })} /></Field>
        <Field label="Expected (auto if blank)" htmlFor="ed"><Input id="ed" type="date" value={f.expectedDueOn} onChange={(e) => setF({ ...f, expectedDueOn: e.target.value })} /></Field>
        <Field label="Born on" htmlFor="bo"><Input id="bo" type="date" max={dateInput()} value={f.outcomeOn} onChange={(e) => setF({ ...f, outcomeOn: e.target.value })} /></Field>
        <Field label="Offspring" htmlFor="oc"><Input id="oc" type="number" min="0" max="100" value={f.offspringCount} onChange={(e) => setF({ ...f, offspringCount: e.target.value })} /></Field>
      </div>
      <Field label="Notes" htmlFor="bn2"><Textarea id="bn2" rows={2} value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
    </RecordDialog>
  );
}
