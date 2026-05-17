import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { History } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { FilterBar } from "@/components/admin/FilterBar";
import { DetailsDrawer } from "@/components/admin/DetailsDrawer";
import { api } from "@/api/client";

interface AuditEntry {
  id: string;
  at: string;
  userId?: string | null;
  action: string;
  entityName: string;
  entityId?: string | null;
  module?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  oldValuesJson?: string | null;
  newValuesJson?: string | null;
}

export function AdminAuditLogPage() {
  const [entityName, setEntityName] = useState("");
  const [action, setAction] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<AuditEntry | null>(null);
  const pageSize = 50;

  const { data, isLoading } = useQuery({
    queryKey: ["admin-audit", { entityName, action, page }],
    queryFn: async () => {
      const r = await api.get<{ total: number; items: AuditEntry[] }>("/admin/audit", {
        params: {
          entityName: entityName || undefined,
          action: action || undefined,
          page, pageSize
        }
      });
      return r.data;
    }
  });

  const columns: Column<AuditEntry>[] = [
    { key: "at", header: "When",
      render: (a) => <span className="text-xs text-muted-foreground">{new Date(a.at).toLocaleString()}</span> },
    { key: "actor", header: "Actor",
      render: (a) => <span className="font-mono text-xs">{a.userId?.slice(0, 8) ?? "system"}</span> },
    { key: "action", header: "Action",
      render: (a) => <Badge variant="outline">{a.action}</Badge> },
    { key: "entity", header: "Entity", render: (a) =>
      <span><span className="font-medium">{a.entityName}</span><span className="text-xs text-muted-foreground"> {a.entityId?.slice(0, 8)}</span></span> },
    { key: "module", header: "Module",
      render: (a) => <span className="text-xs">{a.module ?? "—"}</span> },
    { key: "ip", header: "IP",
      render: (a) => <span className="font-mono text-xs text-muted-foreground">{a.ipAddress ?? "—"}</span> }
  ];

  const totalPages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1;

  return (
    <div className="space-y-4">
      <PageHeader title="Audit log" icon={History}
        description={data ? `${data.total} entries` : "Every privileged action."} />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <FilterBar onReset={() => { setEntityName(""); setAction(""); setPage(1); }}>
            <Input className="w-40" placeholder="Entity (e.g. User)" value={entityName}
                   onChange={(e) => { setEntityName(e.target.value); setPage(1); }} />
            <Input className="w-40" placeholder="Action (e.g. user.suspend)" value={action}
                   onChange={(e) => { setAction(e.target.value); setPage(1); }} />
          </FilterBar>

          <DataTable
            data={data?.items ?? []}
            columns={columns}
            rowKey={(a) => a.id}
            loading={isLoading}
            onRowClick={(a) => setSelected(a)}
            empty={<EmptyState icon={History} title="No audit entries" description="Activity will appear here." />}
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
        open={!!selected}
        onOpenChange={(v) => !v && setSelected(null)}
        title={selected ? `${selected.action} · ${selected.entityName}` : ""}
        description={selected ? new Date(selected.at).toLocaleString() : ""}
      >
        {selected && (
          <div className="space-y-3 text-sm">
            <Field label="Entity ID" value={selected.entityId ?? "—"} mono />
            <Field label="Actor"     value={selected.userId ?? "system"} mono />
            <Field label="Module"    value={selected.module ?? "—"} />
            <Field label="IP"        value={selected.ipAddress ?? "—"} mono />
            <Field label="User-Agent" value={selected.userAgent ?? "—"} />
            <div>
              <p className="text-xs text-muted-foreground mb-1">Old values</p>
              <pre className="bg-muted/50 p-2 rounded text-[11px] overflow-x-auto">{prettify(selected.oldValuesJson)}</pre>
            </div>
            <div>
              <p className="text-xs text-muted-foreground mb-1">New values</p>
              <pre className="bg-muted/50 p-2 rounded text-[11px] overflow-x-auto">{prettify(selected.newValuesJson)}</pre>
            </div>
          </div>
        )}
      </DetailsDrawer>
    </div>
  );
}

function Field({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={mono ? "font-mono text-xs" : ""}>{value}</p>
    </div>
  );
}

function prettify(s?: string | null) {
  if (!s) return "—";
  try { return JSON.stringify(JSON.parse(s), null, 2); }
  catch { return s; }
}
