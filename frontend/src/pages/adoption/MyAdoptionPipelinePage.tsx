import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { HeartHandshake } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { toast } from "@/components/ui/sonner";
import { adoptionPlusApi, type AdoptionStage, type PipelineItem } from "@/api/adoption";
import { AdoptionStageTracker } from "@/components/adoption/AdoptionStageTracker";
import { ApplicationChecksDialog } from "@/components/adoption/ApplicationChecksDialog";
import { useI18n } from "@/lib/i18n";

const COLUMNS: AdoptionStage[] = ["Submitted", "ReferenceCheck", "HomeVisit", "Decision", "Adopted", "Declined"];
const NEXT: Partial<Record<AdoptionStage, AdoptionStage[]>> = {
  Submitted: ["ReferenceCheck", "HomeVisit", "Decision", "Declined"],
  ReferenceCheck: ["HomeVisit", "Decision", "Declined"],
  HomeVisit: ["Decision", "Declined"],
  Decision: ["Adopted", "Declined"],
};

export function MyAdoptionPipelinePage() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const { data = [], isLoading } = useQuery({ queryKey: ["adoption", "pipeline"], queryFn: () => adoptionPlusApi.pipeline() });
  const [busy, setBusy] = useState<string | null>(null);
  const [checks, setChecks] = useState<PipelineItem | null>(null);

  async function move(item: PipelineItem, stage: AdoptionStage) {
    setBusy(item.requestId);
    try {
      await adoptionPlusApi.advance(item.requestId, stage);
      qc.invalidateQueries({ queryKey: ["adoption", "pipeline"] });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't update stage.");
    } finally { setBusy(null); }
  }

  return (
    <div className="space-y-4">
      <PageHeader title={t("adoptmsg.pipeline.title")} icon={HeartHandshake} />
      {!isLoading && data.length === 0 && <EmptyState icon={HeartHandshake} title={t("adoptmsg.pipeline.empty")} description="" />}
      <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
        {COLUMNS.map((col) => {
          const items = data.filter((i) => i.stage === col || (col === "Adopted" && i.stage === "FollowUp"));
          return (
            <div key={col} className="space-y-2">
              <h3 className="text-sm font-semibold">{col} <span className="text-muted-foreground">({items.length})</span></h3>
              {items.map((i) => (
                <Card key={i.requestId}>
                  <CardContent className="p-3 space-y-2 text-sm">
                    <Link to={`/adoption/${i.listingId}`} className="font-medium hover:underline">{i.listingTitle}</Link>
                    <p className="text-xs">{i.requesterName}</p>
                    <div className="flex flex-wrap gap-1">
                      {i.matchPercent != null && <Badge variant="secondary">{t("adoptmsg.match", { pct: i.matchPercent })}</Badge>}
                      {i.isOverdue && <Badge variant="destructive">{t("adoptmsg.pipeline.overdue")}</Badge>}
                    </div>
                    <AdoptionStageTracker stage={i.stage} />
                    <Button size="sm" variant="outline" onClick={() => setChecks(i)}>{t("adoptmsg.checks.open")}</Button>
                    <div className="flex flex-wrap gap-1">
                      {(NEXT[i.stage] ?? []).map((s) => (
                        <Button key={s} size="sm" variant={s === "Declined" ? "outline" : "secondary"}
                                disabled={busy === i.requestId} onClick={() => move(i, s)}>{s === "Declined" ? "Decline" : `→ ${s}`}</Button>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          );
        })}
      </div>
      <ApplicationChecksDialog requestId={checks?.requestId ?? null} title={checks ? `${checks.listingTitle} - ${checks.requesterName}` : undefined}
                               onOpenChange={(o) => { if (!o) setChecks(null); }} />
    </div>
  );
}
