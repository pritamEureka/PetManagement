import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Wallet } from "lucide-react";
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/common/PageHeader";
import { StatTile } from "@/components/common/StatTile";
import { EmptyState } from "@/components/common/EmptyState";
import { petsApi, expenseCategories } from "@/api/pets";
import { fmtDate, fmtMoney } from "./detail/shared";

// Categorical palette — distinct hues that hold up in light and dark themes.
const colors: Record<string, string> = {
  Food: "#2563eb", Vet: "#dc2626", Grooming: "#db2777", Medicine: "#7c3aed", Accessories: "#0891b2",
  Insurance: "#65a30d", Services: "#ea580c", Training: "#ca8a04", Other: "#64748b",
};

export function PetExpensesPage() {
  const [months, setMonths] = useState("6");
  const [petId, setPetId] = useState<string>("all");
  const { data: pets = [] } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const { data, isLoading } = useQuery({
    queryKey: ["pets", "expense-summary", months, petId],
    queryFn: () => petsApi.expenses.summary(Number(months), petId === "all" ? undefined : petId),
  });

  const editablePets = pets.filter((p) => p.myAccess === "Owner" || p.myAccess === "CoOwner");
  const chart = (data?.months ?? []).map((m) => ({
    month: new Date(`${m.month}-01T00:00:00`).toLocaleDateString(undefined, { month: "short", year: "2-digit" }),
    ...m.byCategory,
  }));
  const used = expenseCategories.filter((c) => (data?.byCategory[c] ?? 0) > 0);
  const avg = data && data.months.length ? data.total / data.months.length : 0;
  const top = data ? Object.entries(data.byCategory).sort((a, b) => b[1] - a[1])[0] : undefined;

  return (
    <div className="space-y-4">
      <Button variant="ghost" size="sm" asChild><Link to="/pets"><ArrowLeft className="h-4 w-4 mr-1" /> My pets</Link></Button>
      <PageHeader
        title="Pet expenses"
        icon={Wallet}
        description="Manual entries plus paid vet visits, service bookings and grooming costs."
        actions={
          <>
            <Select value={petId} onValueChange={setPetId}>
              <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All pets</SelectItem>
                {editablePets.map((p) => <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={months} onValueChange={setMonths}>
              <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
              <SelectContent>
                {["3", "6", "12", "24"].map((m) => <SelectItem key={m} value={m}>Last {m} months</SelectItem>)}
              </SelectContent>
            </Select>
          </>
        }
      />

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <StatTile label="Total spent" value={fmtMoney(data?.total ?? 0)} />
        <StatTile label="Monthly average" value={fmtMoney(avg)} />
        <StatTile label="Biggest category" value={top ? top[0] : "—"} hint={top ? fmtMoney(top[1]) : undefined} />
      </div>

      {!isLoading && data && data.total === 0 ? (
        <EmptyState icon={Wallet} title="No spending recorded yet"
          description="Add expenses from a pet's Expenses tab. Paid vet and service bookings show up automatically." />
      ) : (
        <>
          <Card>
            <CardHeader><CardTitle className="text-base">Monthly spend by category</CardTitle></CardHeader>
            <CardContent className="h-72">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chart} margin={{ top: 4, right: 8, bottom: 0, left: -8 }}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} className="stroke-muted" />
                  <XAxis dataKey="month" fontSize={11} tickLine={false} />
                  <YAxis fontSize={11} tickLine={false} />
                  <Tooltip formatter={(v: number) => fmtMoney(v)} />
                  <Legend wrapperStyle={{ fontSize: 12 }} />
                  {used.map((c) => <Bar key={c} dataKey={c} stackId="a" fill={colors[c]} />)}
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <Card>
              <CardHeader><CardTitle className="text-base">By pet</CardTitle></CardHeader>
              <CardContent className="space-y-2">
                {Object.entries(data?.byPet ?? {}).sort((a, b) => b[1] - a[1]).map(([name, amt]) => (
                  <div key={name} className="space-y-1">
                    <div className="flex justify-between text-sm"><span>{name}</span><span className="font-medium">{fmtMoney(amt)}</span></div>
                    <div className="h-1.5 rounded-full bg-muted overflow-hidden">
                      <div className="h-full bg-primary" style={{ width: `${data!.total ? (amt / data!.total) * 100 : 0}%` }} />
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle className="text-base">Recent</CardTitle></CardHeader>
              <CardContent className="divide-y">
                {(data?.recent ?? []).map((e, i) => (
                  <div key={e.id ?? i} className="py-2 flex justify-between gap-3 text-sm">
                    <div className="min-w-0">
                      <p className="font-medium truncate">{e.petName} · {e.category}</p>
                      <p className="text-xs text-muted-foreground truncate">{fmtDate(e.spentOn)}{e.description ? ` · ${e.description}` : ""}</p>
                    </div>
                    <div className="text-right shrink-0">
                      <p className="font-medium">{fmtMoney(e.amount)}</p>
                      {e.source !== "manual" && <Badge variant="muted" className="text-[10px]">auto</Badge>}
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>
        </>
      )}
    </div>
  );
}
