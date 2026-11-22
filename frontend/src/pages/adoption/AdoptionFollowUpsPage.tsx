import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ClipboardCheck } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { toast } from "@/components/ui/sonner";
import { adoptionPlusApi, type AdoptionFollowUp, type MyApplication } from "@/api/adoption";
import { AdoptionStageTracker } from "@/components/adoption/AdoptionStageTracker";
import { useI18n } from "@/lib/i18n";

/** Adopter-facing page: application tracker + post-adoption check-ins (day 7/30/90). */
export function AdoptionFollowUpsPage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const { data: followUps = [] } = useQuery({ queryKey: ["adoption", "followups"], queryFn: () => adoptionPlusApi.myFollowUps() });
  const { data: apps = [] } = useQuery({ queryKey: ["adoption", "my-apps"], queryFn: () => adoptionPlusApi.myApplications() });

  return (
    <div className="space-y-6">
      <PageHeader title={t("adoptmsg.followups.title")} icon={ClipboardCheck} />
      <section className="space-y-2">
        <h2 className="font-semibold">{t("adoptmsg.tracker.title")}</h2>
        {apps.map((a: MyApplication) => (
          <Card key={a.requestId}><CardContent className="p-3 space-y-2">
            <p className="text-sm font-medium">{a.listingTitle}</p>
            <AdoptionStageTracker stage={a.stage} history={a.history} />
            {a.dueAt && a.stage === "Decision" && <p className="text-xs text-muted-foreground">Decision expected by {new Date(a.dueAt).toLocaleDateString()}</p>}
          </CardContent></Card>
        ))}
      </section>
      <section className="space-y-2">
        {followUps.length === 0 && <EmptyState icon={ClipboardCheck} title={t("adoptmsg.followups.empty")} description="" />}
        {followUps.map((f) => <FollowUpCard key={f.id} f={f} onDone={() => qc.invalidateQueries({ queryKey: ["adoption", "followups"] })} />)}
      </section>
    </div>
  );
}

function FollowUpCard({ f, onDone }: { f: AdoptionFollowUp; onDone: () => void }) {
  const { t } = useI18n();
  const [score, setScore] = useState(4);
  const [notes, setNotes] = useState("");
  const due = new Date(f.dueAt) <= new Date();
  async function submit() {
    try { await adoptionPlusApi.completeFollowUp(f.id, score, notes || undefined); onDone(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Couldn't submit."); }
  }
  return (
    <Card><CardContent className="p-3 space-y-2 text-sm">
      <div className="flex items-center gap-2">
        <span className="font-medium">{f.listingTitle} · day {f.dayOffset}</span>
        <Badge variant={f.status === "Completed" ? "secondary" : due ? "destructive" : "outline"}>
          {f.status === "Completed" ? `Welfare ${f.welfareScore}/5` : due ? "Due" : `Due ${new Date(f.dueAt).toLocaleDateString()}`}
        </Badge>
      </div>
      {f.status === "Due" && due && (
        <div className="flex flex-wrap items-center gap-2">
          <label className="text-xs">Welfare (1-5)</label>
          <Input type="number" min={1} max={5} className="w-20" value={score} onChange={(e) => setScore(Math.min(5, Math.max(1, Number(e.target.value) || 1)))} />
          <Input className="flex-1 min-w-40" placeholder="Notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          <Button size="sm" onClick={submit}>{t("adoptmsg.followups.submit")}</Button>
        </div>
      )}
    </CardContent></Card>
  );
}
