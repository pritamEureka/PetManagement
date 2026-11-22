import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Inbox } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { DetailsDrawer } from "@/components/admin/DetailsDrawer";
import { ConfirmDialog } from "@/components/admin/ConfirmDialog";
import { toast } from "@/components/ui/sonner";
import { api } from "@/api/client";

interface DeadLetterItem {
  id: string;
  topic: string;
  partition: number;
  offset: number;
  key?: string | null;
  consumerGroup: string;
  error: string;
  failureKind: string;
  attempts: number;
  firstFailedAt: string;
  lastFailedAt?: string | null;
  status: "Pending" | "Redriven" | "Discarded";
  redriveCount: number;
  resolvedAt?: string | null;
}
interface DeadLetterDetail extends DeadLetterItem {
  payload: string;
  headers: Record<string, string>;
}

const STATUSES = ["Pending", "Redriven", "Discarded"] as const;

function prettify(s?: string | null) {
  if (!s) return "";
  try { return JSON.stringify(JSON.parse(s), null, 2); } catch { return s; }
}

export function DeadLettersPage() {
  const qc = useQueryClient();
  const [status, setStatus] = useState<string>("Pending");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<{ id: string; action: "redrive" | "discard" } | null>(null);
  const pageSize = 50;

  const list = useQuery({
    queryKey: ["admin-dead-letters", { status, page }],
    queryFn: async () =>
      (await api.get<{ total: number; items: DeadLetterItem[] }>("/admin/dead-letters", {
        params: { status: status || undefined, page, pageSize }
      })).data
  });

  const detail = useQuery({
    queryKey: ["admin-dead-letter", selectedId],
    enabled: !!selectedId,
    queryFn: async () => (await api.get<DeadLetterDetail>(`/admin/dead-letters/${selectedId}`)).data
  });

  const act = useMutation({
    mutationFn: async (v: { id: string; action: "redrive" | "discard" }) =>
      api.post(`/admin/dead-letters/${v.id}/${v.action}`),
    onSuccess: (_d, v) => {
      toast.success(v.action === "redrive" ? "Message re-driven to its original topic." : "Message discarded.");
      qc.invalidateQueries({ queryKey: ["admin-dead-letters"] });
      qc.invalidateQueries({ queryKey: ["admin-dead-letter"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Action failed.")
  });

  const columns: Column<DeadLetterItem>[] = [
    { key: "when", header: "First failed",
      render: (d) => <span className="text-xs text-muted-foreground">{new Date(d.firstFailedAt).toLocaleString()}</span> },
    { key: "topic", header: "Topic",
      render: (d) => <span className="font-mono text-xs">{d.topic}[{d.partition}]@{d.offset}</span> },
    { key: "group", header: "Consumer", render: (d) => <span className="text-xs">{d.consumerGroup}</span> },
    { key: "kind", header: "Kind",
      render: (d) => <Badge variant={d.failureKind === "Permanent" ? "destructive" : "outline"}>{d.failureKind}</Badge> },
    { key: "attempts", header: "Attempts", render: (d) => d.attempts },
    { key: "error", header: "Error",
      render: (d) => <span className="text-xs line-clamp-1 max-w-[320px]">{d.error}</span> },
    { key: "status", header: "Status", render: (d) => <Badge variant="outline">{d.status}</Badge> },
    { key: "actions", header: "",
      render: (d) => d.status === "Pending" ? (
        <div className="flex gap-2" onClick={(e) => e.stopPropagation()}>
          <Button size="sm" variant="outline" onClick={() => setConfirm({ id: d.id, action: "redrive" })}>Redrive</Button>
          <Button size="sm" variant="ghost" onClick={() => setConfirm({ id: d.id, action: "discard" })}>Discard</Button>
        </div>
      ) : null }
  ];

  const total = list.data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const d = detail.data;

  return (
    <div className="space-y-4">
      <PageHeader title="Dead letters" icon={Inbox}
        description={list.data ? `${total} message(s)` : "Messages that exhausted retries or failed permanently."} />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <div className="flex gap-2">
            {STATUSES.map((s) => (
              <Button key={s} size="sm" variant={status === s ? "default" : "outline"}
                      onClick={() => { setStatus(s); setPage(1); }}>{s}</Button>
            ))}
          </div>

          <DataTable
            data={list.data?.items ?? []}
            columns={columns}
            rowKey={(r) => r.id}
            loading={list.isLoading}
            caption="Dead-lettered messages"
            onRowClick={(r) => setSelectedId(r.id)}
            empty={<EmptyState icon={Inbox} title="No dead letters" description="Nothing is parked in this state." />}
          />

          {totalPages > 1 && (
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Page {page} of {totalPages}</span>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <DetailsDrawer
        open={!!selectedId}
        onOpenChange={(v) => !v && setSelectedId(null)}
        title={d ? `${d.topic}[${d.partition}]@${d.offset}` : "Dead letter"}
        description={d ? `${d.failureKind} failure, ${d.attempts} attempt(s)` : ""}
      >
        {d && (
          <div className="space-y-3 text-sm">
            <div><p className="text-xs text-muted-foreground">Error</p><p className="break-words">{d.error}</p></div>
            <div><p className="text-xs text-muted-foreground">Key</p><p className="font-mono text-xs">{d.key ?? "-"}</p></div>
            <div>
              <p className="text-xs text-muted-foreground mb-1">Headers</p>
              <pre className="bg-muted/50 p-2 rounded text-[11px] overflow-x-auto">{JSON.stringify(d.headers, null, 2)}</pre>
            </div>
            <div>
              <p className="text-xs text-muted-foreground mb-1">Payload</p>
              <pre className="bg-muted/50 p-2 rounded text-[11px] overflow-x-auto max-h-96">{prettify(d.payload)}</pre>
            </div>
            {d.status === "Pending" && (
              <div className="flex gap-2 pt-2">
                <Button onClick={() => setConfirm({ id: d.id, action: "redrive" })}>Redrive</Button>
                <Button variant="ghost" onClick={() => setConfirm({ id: d.id, action: "discard" })}>Discard</Button>
              </div>
            )}
          </div>
        )}
      </DetailsDrawer>

      <ConfirmDialog
        open={!!confirm}
        onOpenChange={(v) => !v && setConfirm(null)}
        title={confirm?.action === "redrive" ? "Redrive message?" : "Discard message?"}
        description={confirm?.action === "redrive"
          ? "The message is republished to its original topic with an x-redrive-count header."
          : "The message is marked as discarded and will not be processed."}
        confirmLabel={confirm?.action === "redrive" ? "Redrive" : "Discard"}
        destructive={confirm?.action === "discard"}
        isLoading={act.isPending}
        onConfirm={async () => { if (confirm) await act.mutateAsync(confirm).catch(() => undefined); }}
      />
    </div>
  );
}
