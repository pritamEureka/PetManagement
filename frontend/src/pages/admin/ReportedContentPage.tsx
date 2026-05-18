import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Flag, Gavel, Filter, Inbox } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  reportsApi, type ContentReport, type ReportStatus, type ReportTargetType,
  type ModerationTargetType
} from "@/api/security";
import { ModerationActionModal } from "@/components/security/ModerationActionModal";
import { toast } from "@/components/ui/sonner";

const STATUSES: ReportStatus[] = ["Open", "UnderReview", "Resolved", "Dismissed"];
const TYPES: ReportTargetType[] = [
  "Post", "Comment", "Message", "User", "AdoptionListing", "Product", "Doctor", "Store"
];

const STATUS_VARIANT: Record<ReportStatus, "default" | "secondary" | "outline" | "destructive"> = {
  Open: "destructive",
  UnderReview: "secondary",
  Resolved: "default",
  Dismissed: "outline"
};

export function ReportedContentPage() {
  const qc = useQueryClient();
  const [status, setStatus] = useState<ReportStatus | "all">("Open");
  const [targetType, setTargetType] = useState<ReportTargetType | "all">("all");
  const [modAction, setModAction] = useState<{ id: string; targetType: ReportTargetType; targetId: string } | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["admin-reports", status, targetType],
    queryFn: () => reportsApi.list({
      status: status === "all" ? undefined : status,
      targetType: targetType === "all" ? undefined : targetType,
      page: 1, pageSize: 100
    })
  });

  const setStatusMutation = useMutation({
    mutationFn: ({ id, s }: { id: string; s: ReportStatus }) => reportsApi.setStatus(id, s, "Resolved from admin queue"),
    onSuccess: () => { toast.success("Report updated"); qc.invalidateQueries({ queryKey: ["admin-reports"] }); }
  });

  const groups = useMemo(() => groupByTarget(data ?? []), [data]);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Flag className="h-6 w-6 text-primary" /> Reported content
        </h1>
        <div className="flex items-center gap-2 flex-wrap">
          <Filter className="h-4 w-4 text-muted-foreground" />
          <Select value={status} onValueChange={(v) => setStatus(v as any)}>
            <SelectTrigger className="w-full sm:w-40"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
            </SelectContent>
          </Select>
          <Select value={targetType} onValueChange={(v) => setTargetType(v as any)}>
            <SelectTrigger className="w-full sm:w-44"><SelectValue placeholder="Target" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All targets</SelectItem>
              {TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-3">{[...Array(4)].map((_, i) => <Skeleton key={i} className="h-32" />)}</div>
      ) : !data || data.length === 0 ? (
        <Card><CardContent className="py-16 text-center text-muted-foreground flex flex-col items-center gap-2">
          <Inbox className="h-8 w-8" /> Nothing to review right now.
        </CardContent></Card>
      ) : (
        <div className="space-y-3">
          {groups.map((g) => (
            <Card key={`${g.targetType}-${g.targetId}`}>
              <CardHeader className="flex flex-row justify-between items-start space-y-0 pb-2">
                <div>
                  <CardTitle className="text-base">
                    {g.targetType} <span className="font-mono text-xs text-muted-foreground">{g.targetId}</span>
                  </CardTitle>
                  <p className="text-xs text-muted-foreground">{g.reports.length} report(s)</p>
                </div>
                <Button size="sm" onClick={() =>
                  setModAction({ id: g.reports[0].id, targetType: g.targetType, targetId: g.targetId })}>
                  <Gavel className="h-3 w-3 mr-1" /> Take action
                </Button>
              </CardHeader>
              <CardContent className="space-y-2 text-sm divide-y">
                {g.reports.map((r) => (
                  <div key={r.id} className="pt-2 first:pt-0">
                    <div className="flex items-center justify-between">
                      <p className="font-medium">{r.reason}</p>
                      <div className="flex items-center gap-2">
                        <Badge variant={STATUS_VARIANT[r.status]}>{r.status}</Badge>
                        {r.status === "Open" && (
                          <Button size="sm" variant="outline"
                            onClick={() => setStatusMutation.mutate({ id: r.id, s: "Dismissed" })}>
                            Dismiss
                          </Button>
                        )}
                      </div>
                    </div>
                    {r.details && <p className="text-muted-foreground">{r.details}</p>}
                    <p className="text-xs text-muted-foreground">
                      By {r.reporterDisplayName} · {new Date(r.createdAt).toLocaleString()}
                    </p>
                  </div>
                ))}
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {modAction && (
        <ModerationActionModal
          open={!!modAction}
          onOpenChange={(o) => !o && setModAction(null)}
          targetType={modAction.targetType as ModerationTargetType}
          targetId={modAction.targetId}
          reportId={modAction.id}
          onDone={() => qc.invalidateQueries({ queryKey: ["admin-reports"] })}
        />
      )}
    </div>
  );
}

// Reports stack on the same target — show one card per target with all related reports.
function groupByTarget(rows: ContentReport[]) {
  const map = new Map<string, { targetType: ReportTargetType; targetId: string; reports: ContentReport[] }>();
  for (const r of rows) {
    const key = `${r.targetType}|${r.targetId}`;
    const entry = map.get(key) ?? { targetType: r.targetType, targetId: r.targetId, reports: [] };
    entry.reports.push(r);
    map.set(key, entry);
  }
  // Newest first within each group; groups sorted by latest report timestamp.
  for (const g of map.values()) g.reports.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  return Array.from(map.values()).sort((a, b) =>
    b.reports[0].createdAt.localeCompare(a.reports[0].createdAt));
}
