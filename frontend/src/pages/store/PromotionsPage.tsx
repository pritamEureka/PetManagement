import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Megaphone, Trash2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "@/components/ui/sonner";
import {
  marketplacePlusApi, type Promotion, type PromotionScope, type PromotionType, type UpsertPromotionInput,
} from "@/api/marketplacePlus";

const blank: UpsertPromotionInput = {
  name: "", type: "PercentOff", scope: "Storewide", scopeRefId: null, value: 10, buyQuantity: 2, getQuantity: 1,
  minSubtotal: 0, startsAt: null, endsAt: null, usageLimit: null, stackable: false, isActive: true,
};
const TYPES: PromotionType[] = ["PercentOff", "FixedOff", "FreeShipping", "BuyXGetY"];
const SCOPES: PromotionScope[] = ["Storewide", "Category", "Store", "Product"];
const sel = "h-9 rounded-md border bg-background px-2 text-sm";
const err = (e: any, fallback: string) => e?.response?.data?.error?.message ?? fallback;

/** Admin (Settings.Edit): promotions CRUD, gift card issuing and sponsored placement review. */
export function PromotionsPage() {
  const qc = useQueryClient();
  const [form, setForm] = useState<UpsertPromotionInput>(blank);
  const [editing, setEditing] = useState<string | null>(null);
  const { data: promos } = useQuery({ queryKey: ["promotions"], queryFn: () => marketplacePlusApi.promotions() });
  const { data: sponsored } = useQuery({ queryKey: ["sponsored-admin"], queryFn: () => marketplacePlusApi.sponsored() });
  const { data: cards } = useQuery({ queryKey: ["giftcards"], queryFn: () => marketplacePlusApi.giftCards() });
  const [gcAmount, setGcAmount] = useState("");
  const [gcEmail, setGcEmail] = useState("");

  const refresh = () => qc.invalidateQueries({ queryKey: ["promotions"] });
  const save = useMutation({
    mutationFn: () => (editing ? marketplacePlusApi.updatePromotion(editing, form) : marketplacePlusApi.createPromotion(form)),
    onSuccess: () => { toast.success("Promotion saved"); setForm(blank); setEditing(null); refresh(); },
    onError: (e: any) => toast.error(err(e, "Could not save promotion.")),
  });
  const del = useMutation({
    mutationFn: (id: string) => marketplacePlusApi.deletePromotion(id),
    onSuccess: refresh, onError: (e: any) => toast.error(err(e, "Delete failed.")),
  });
  const review = useMutation({
    mutationFn: (v: { id: string; approve: boolean }) => marketplacePlusApi.reviewSponsored(v.id, v.approve),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["sponsored-admin"] }), onError: (e: any) => toast.error(err(e, "Review failed.")),
  });
  const issue = useMutation({
    mutationFn: () => marketplacePlusApi.issueGiftCard({ amount: Number(gcAmount), recipientEmail: gcEmail || undefined }),
    onSuccess: (c) => { toast.success(`Gift card issued: ${c.code}`); setGcAmount(""); setGcEmail(""); qc.invalidateQueries({ queryKey: ["giftcards"] }); },
    onError: (e: any) => toast.error(err(e, "Could not issue gift card.")),
  });

  const activate = useMutation({
    mutationFn: (id: string) => marketplacePlusApi.activateGiftCard(id),
    onSuccess: () => { toast.success("Gift card activated."); qc.invalidateQueries({ queryKey: ["giftcards"] }); },
    onError: (e: any) => toast.error(err(e, "Could not activate gift card.")),
  });

  const edit = (p: Promotion) => {
    setEditing(p.id);
    setForm({ name: p.name, type: p.type, scope: p.scope, scopeRefId: p.scopeRefId ?? null, value: p.value, buyQuantity: p.buyQuantity,
      getQuantity: p.getQuantity, minSubtotal: p.minSubtotal, startsAt: p.startsAt ?? null, endsAt: p.endsAt ?? null,
      usageLimit: p.usageLimit ?? null, stackable: p.stackable, isActive: p.isActive });
  };
  const set = <K extends keyof UpsertPromotionInput>(k: K, v: UpsertPromotionInput[K]) => setForm((f) => ({ ...f, [k]: v }));
  const iso = (v: string) => (v ? new Date(v).toISOString() : null);

  return (
    <div className="max-w-5xl mx-auto space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2"><Megaphone className="h-6 w-6 text-primary" /> Promotions</h1>

      <Card>
        <CardHeader><CardTitle className="text-base">{editing ? "Edit promotion" : "New promotion"} (auto-applied at checkout)</CardTitle></CardHeader>
        <CardContent className="grid sm:grid-cols-3 gap-3">
          <div className="sm:col-span-3"><Label>Name</Label><Input value={form.name} onChange={(e) => set("name", e.target.value)} /></div>
          <div><Label>Type</Label>
            <select className={sel + " w-full"} value={form.type} onChange={(e) => set("type", e.target.value as PromotionType)}>
              {TYPES.map((t) => <option key={t}>{t}</option>)}
            </select></div>
          <div><Label>Scope</Label>
            <select className={sel + " w-full"} value={form.scope} onChange={(e) => set("scope", e.target.value as PromotionScope)}>
              {SCOPES.map((t) => <option key={t}>{t}</option>)}
            </select></div>
          {form.scope !== "Storewide" && (
            <div><Label>{form.scope} id</Label><Input value={form.scopeRefId ?? ""} onChange={(e) => set("scopeRefId", e.target.value || null)} /></div>
          )}
          {(form.type === "PercentOff" || form.type === "FixedOff") && (
            <div><Label>{form.type === "PercentOff" ? "Percent" : "Amount"}</Label>
              <Input type="number" value={form.value} onChange={(e) => set("value", Number(e.target.value))} /></div>
          )}
          {form.type === "BuyXGetY" && (<>
            <div><Label>Buy</Label><Input type="number" value={form.buyQuantity} onChange={(e) => set("buyQuantity", Number(e.target.value))} /></div>
            <div><Label>Get free</Label><Input type="number" value={form.getQuantity} onChange={(e) => set("getQuantity", Number(e.target.value))} /></div>
          </>)}
          <div><Label>Min subtotal</Label><Input type="number" value={form.minSubtotal} onChange={(e) => set("minSubtotal", Number(e.target.value))} /></div>
          <div><Label>Starts</Label><Input type="datetime-local" onChange={(e) => set("startsAt", iso(e.target.value))} /></div>
          <div><Label>Ends</Label><Input type="datetime-local" onChange={(e) => set("endsAt", iso(e.target.value))} /></div>
          <div><Label>Usage limit</Label>
            <Input type="number" value={form.usageLimit ?? ""} onChange={(e) => set("usageLimit", e.target.value ? Number(e.target.value) : null)} /></div>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.stackable} onChange={(e) => set("stackable", e.target.checked)} /> Stackable</label>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(e) => set("isActive", e.target.checked)} /> Active</label>
          <div className="sm:col-span-3 flex gap-2">
            <Button disabled={!form.name.trim() || save.isPending} onClick={() => save.mutate()}>{editing ? "Update" : "Create"}</Button>
            {editing && <Button variant="outline" onClick={() => { setEditing(null); setForm(blank); }}>Cancel</Button>}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-3 space-y-1">
          {!promos?.length ? <p className="text-sm text-muted-foreground">No promotions.</p> : promos.map((p) => (
            <div key={p.id} className="flex items-center justify-between gap-2 border-b last:border-0 py-1.5 text-sm">
              <button className="text-left hover:underline" onClick={() => edit(p)}>
                <span className="font-medium">{p.name}</span>{" "}
                <span className="text-muted-foreground">{p.type} / {p.scope} / used {p.usedCount}{p.usageLimit ? `/${p.usageLimit}` : ""}</span>
              </button>
              <div className="flex items-center gap-2">
                {p.stackable && <Badge variant="outline">stackable</Badge>}
                <Badge variant={p.isActive ? "success" : "muted"}>{p.isActive ? "active" : "off"}</Badge>
                <Button size="icon" variant="ghost" aria-label="Delete" onClick={() => del.mutate(p.id)}><Trash2 className="h-4 w-4 text-destructive" /></Button>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Sponsored placement requests</CardTitle></CardHeader>
        <CardContent className="space-y-1">
          {!sponsored?.length ? <p className="text-sm text-muted-foreground">None.</p> : sponsored.map((s) => (
            <div key={s.id} className="flex items-center justify-between gap-2 text-sm border-b last:border-0 py-1.5">
              <span>{s.productName} ({s.storeName}) - {new Date(s.startsAt).toLocaleDateString()} to {new Date(s.endsAt).toLocaleDateString()} - {s.impressions} impressions</span>
              <div className="flex items-center gap-2">
                <Badge variant={s.status === "Approved" ? "success" : "outline"}>{s.status}</Badge>
                {s.status !== "Approved" && <Button size="sm" onClick={() => review.mutate({ id: s.id, approve: true })}>Approve</Button>}
                {s.status !== "Rejected" && <Button size="sm" variant="outline" onClick={() => review.mutate({ id: s.id, approve: false })}>Reject</Button>}
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Gift cards (admin-issued)</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          <div className="flex flex-wrap gap-2">
            <Input className="w-32" type="number" placeholder="Amount" value={gcAmount} onChange={(e) => setGcAmount(e.target.value)} />
            <Input className="w-64" placeholder="Recipient email (optional)" value={gcEmail} onChange={(e) => setGcEmail(e.target.value)} />
            <Button disabled={!Number(gcAmount) || issue.isPending} onClick={() => issue.mutate()}>Issue</Button>
          </div>
          {cards?.slice(0, 20).map((c) => (
            <div key={c.id} className="flex justify-between text-sm border-b last:border-0 py-1">
              <code>{c.code}</code>
              <span className="flex items-center gap-2">
                {c.balance.toFixed(2)} / {c.initialAmount.toFixed(2)} {c.currency} - {c.status}
                {c.status === "Pending" && (
                  <Button size="sm" variant="outline" disabled={activate.isPending} onClick={() => activate.mutate(c.id)}>Activate</Button>
                )}
              </span>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
