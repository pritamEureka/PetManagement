import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { FileText, Pencil, Plus, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { toast } from "@/components/ui/sonner";
import { petsApi, expenseCategories, type ExpenseCategory, type InsurancePolicy, type PetDetail } from "@/api/pets";
import { RecordDialog, useSave } from "./HealthTab";
import { Section, Field, Muted, dateInput, orNull, fmtDate, fmtMoney } from "./shared";

const sourceLabel = { manual: "Manual", appointment: "Vet booking", service: "Service booking", grooming: "Grooming record" } as const;

export function MoneyTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const [addingExpense, setAddingExpense] = useState(false);
  const [policy, setPolicy] = useState<InsurancePolicy | "new" | null>(null);

  const { data: expenses = [], isLoading } = useQuery({ queryKey: ["pets", pet.id, "expenses"], queryFn: () => petsApi.expenses.list(pet.id) });
  const { data: policies = [] } = useQuery({ queryKey: ["pets", pet.id, "insurance"], queryFn: () => petsApi.insurance.list(pet.id) });
  const refresh = () => {
    qc.invalidateQueries({ queryKey: ["pets", pet.id] });
    qc.invalidateQueries({ queryKey: ["pets", "expense-summary"] });
  };

  const year = new Date().getFullYear().toString();
  const thisYear = expenses.filter((e) => e.spentOn.startsWith(year)).reduce((s, e) => s + e.amount, 0);

  return (
    <div className="space-y-4">
      <Section title="Expenses" action={<Button size="sm" onClick={() => setAddingExpense(true)}><Plus className="h-3.5 w-3.5 mr-1" /> Add</Button>}>
        <p className="text-sm mb-3">Spent on {pet.name} in {year}: <strong>{fmtMoney(thisYear)}</strong></p>
        {isLoading ? <Muted>Loading…</Muted> : expenses.length === 0 ? (
          <Muted>No expenses yet. Paid vet and service bookings are added automatically.</Muted>
        ) : (
          <div className="divide-y">
            {expenses.map((e, i) => (
              <div key={e.id ?? `${e.source}-${i}`} className="py-2 flex items-center justify-between gap-3 text-sm">
                <div className="min-w-0">
                  <p className="font-medium flex items-center gap-2">{e.category}<Badge variant="muted">{sourceLabel[e.source]}</Badge></p>
                  <p className="text-xs text-muted-foreground truncate">{fmtDate(e.spentOn)}{e.description ? ` · ${e.description}` : ""}</p>
                </div>
                <div className="flex items-center gap-1 shrink-0">
                  <span className="font-medium">{fmtMoney(e.amount)}</span>
                  {e.source === "manual" && e.id && (
                    <Button size="icon" variant="ghost" aria-label="Delete expense"
                      onClick={async () => { await petsApi.expenses.remove(pet.id, e.id!); refresh(); }}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </Section>

      <Section title="Insurance" action={<Button size="sm" onClick={() => setPolicy("new")}><Plus className="h-3.5 w-3.5 mr-1" /> Add policy</Button>}>
        {policies.length === 0 ? <Muted>No insurance policies on file.</Muted> : (
          <div className="divide-y">
            {policies.map((p) => (
              <div key={p.id} className="py-2.5 flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium flex items-center gap-2">
                    {p.provider}<Badge variant={p.isActive ? "success" : "muted"}>{p.isActive ? "Active" : "Inactive"}</Badge>
                  </p>
                  <p className="text-xs text-muted-foreground">
                    #{p.policyNumber} · {fmtDate(p.startDate)} → {p.endDate ? fmtDate(p.endDate) : "open"}
                    {p.coverageLimit ? ` · cover ${fmtMoney(p.coverageLimit)}` : ""}{p.premium ? ` · premium ${fmtMoney(p.premium)}` : ""}
                  </p>
                  {p.coverageSummary && <p className="text-xs text-muted-foreground mt-0.5">{p.coverageSummary}</p>}
                </div>
                <div className="flex gap-1 shrink-0">
                  {p.documentUrl && (
                    <Button size="icon" variant="ghost" asChild aria-label="Open policy document">
                      <a href={p.documentUrl} target="_blank" rel="noreferrer"><FileText className="h-4 w-4" /></a>
                    </Button>
                  )}
                  <Button size="icon" variant="ghost" onClick={() => setPolicy(p)} aria-label="Edit"><Pencil className="h-4 w-4" /></Button>
                  <Button size="icon" variant="ghost" aria-label="Delete"
                    onClick={async () => { await petsApi.insurance.remove(pet.id, p.id); refresh(); }}>
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </Section>

      {addingExpense && <ExpenseDialog petId={pet.id} onClose={() => setAddingExpense(false)} onSaved={refresh} />}
      {policy && <PolicyDialog petId={pet.id} record={policy === "new" ? undefined : policy} onClose={() => setPolicy(null)} onSaved={refresh} />}
    </div>
  );
}

function ExpenseDialog({ petId, onClose, onSaved }: { petId: string; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState<{ category: ExpenseCategory; amount: string; spentOn: string; description: string }>({
    category: "Food", amount: "", spentOn: dateInput(), description: ""
  });
  const { busy, run } = useSave(onSaved, onClose);
  const submit = () => {
    const amount = Number(f.amount);
    if (!amount || amount <= 0) return toast.error("Enter an amount above 0.");
    run(() => petsApi.expenses.add(petId, { category: f.category, amount, spentOn: f.spentOn, description: orNull(f.description) }), "Expense added.");
  };
  return (
    <RecordDialog title="Add expense" busy={busy} onClose={onClose} onSubmit={submit}>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Category">
          <Select value={f.category} onValueChange={(v) => setF({ ...f, category: v as ExpenseCategory })}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>{expenseCategories.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
          </Select>
        </Field>
        <Field label="Amount" htmlFor="ea"><Input id="ea" type="number" min="0" step="0.01" value={f.amount} onChange={(e) => setF({ ...f, amount: e.target.value })} /></Field>
      </div>
      <Field label="Date" htmlFor="es"><Input id="es" type="date" max={dateInput()} value={f.spentOn} onChange={(e) => setF({ ...f, spentOn: e.target.value })} /></Field>
      <Field label="Description" htmlFor="ed"><Input id="ed" value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} /></Field>
    </RecordDialog>
  );
}

function PolicyDialog({ petId, record, onClose, onSaved }: { petId: string; record?: InsurancePolicy; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState({
    provider: record?.provider ?? "", policyNumber: record?.policyNumber ?? "", coverageSummary: record?.coverageSummary ?? "",
    coverageLimit: record?.coverageLimit?.toString() ?? "", premium: record?.premium?.toString() ?? "",
    startDate: dateInput(record?.startDate), endDate: record?.endDate ? dateInput(record.endDate) : "", documentUrl: record?.documentUrl ?? "",
  });
  const { busy, run } = useSave(onSaved, onClose);
  const num = (s: string) => (s ? Number(s) : null);
  const submit = () => {
    if (!f.provider.trim() || !f.policyNumber.trim()) return toast.error("Provider and policy number are required.");
    if (f.endDate && f.endDate <= f.startDate) return toast.error("End date must be after the start date.");
    const input = {
      provider: f.provider.trim(), policyNumber: f.policyNumber.trim(), coverageSummary: orNull(f.coverageSummary),
      coverageLimit: num(f.coverageLimit), premium: num(f.premium), startDate: f.startDate, endDate: orNull(f.endDate),
      documentUrl: orNull(f.documentUrl),
    };
    run(() => record ? petsApi.insurance.update(petId, record.id, input) : petsApi.insurance.add(petId, input),
      record ? "Policy updated." : "Policy added.");
  };
  return (
    <RecordDialog title={record ? "Edit policy" : "Add insurance policy"} busy={busy} onClose={onClose} onSubmit={submit}>
      <div className="grid grid-cols-2 gap-3">
        <Field label="Provider" htmlFor="ip"><Input id="ip" value={f.provider} onChange={(e) => setF({ ...f, provider: e.target.value })} /></Field>
        <Field label="Policy number" htmlFor="in"><Input id="in" value={f.policyNumber} onChange={(e) => setF({ ...f, policyNumber: e.target.value })} /></Field>
        <Field label="Start" htmlFor="is"><Input id="is" type="date" value={f.startDate} onChange={(e) => setF({ ...f, startDate: e.target.value })} /></Field>
        <Field label="End (optional)" htmlFor="ie"><Input id="ie" type="date" value={f.endDate} onChange={(e) => setF({ ...f, endDate: e.target.value })} /></Field>
        <Field label="Coverage limit" htmlFor="il"><Input id="il" type="number" min="0" value={f.coverageLimit} onChange={(e) => setF({ ...f, coverageLimit: e.target.value })} /></Field>
        <Field label="Premium" htmlFor="ipr"><Input id="ipr" type="number" min="0" value={f.premium} onChange={(e) => setF({ ...f, premium: e.target.value })} /></Field>
      </div>
      <Field label="What's covered" htmlFor="ic"><Textarea id="ic" rows={2} value={f.coverageSummary} onChange={(e) => setF({ ...f, coverageSummary: e.target.value })} /></Field>
      <div className="flex items-center gap-2">
        <FileUploadButton accept="image/*,application/pdf" label={f.documentUrl ? "Replace document" : "Attach policy document"}
          onUploaded={(url) => setF({ ...f, documentUrl: url })} />
        {f.documentUrl && <span className="text-xs text-muted-foreground">Document attached</span>}
      </div>
    </RecordDialog>
  );
}
