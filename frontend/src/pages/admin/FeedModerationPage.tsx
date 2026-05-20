import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Flag, Eye, EyeOff } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { FilterBar } from "@/components/admin/FilterBar";
import { ModerationActionModal } from "@/components/security/ModerationActionModal";
import { api } from "@/api/client";
import { toast } from "@/components/ui/sonner";

interface AdminPostRow {
  id: string;
  authorId: string;
  authorDisplayName: string;
  caption?: string | null;
  mediaUrls: string[];
  isDeleted: boolean;
  reportsCount: number;
  reactionCount: number;
  commentCount: number;
  createdAt: string;
}

type Mode = "all" | "reported" | "hidden";

/**
 * Feed moderation surface — lists posts, indicates how many reports each one
 * has open, and lets a moderator open the standard ModerationActionModal.
 *
 * Backend endpoint expected: /api/v1/admin/posts (mode=...). If the API isn't
 * deployed yet, the table will simply show "No posts" — fail-open.
 */
export function AdminFeedModerationPage() {
  const qc = useQueryClient();
  const [mode, setMode] = useState<Mode>("reported");
  const [target, setTarget] = useState<AdminPostRow | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["admin-posts", mode],
    queryFn: async () => {
      try {
        const r = await api.get<AdminPostRow[]>("/v1/admin/posts", { params: { mode } });
        return r.data;
      } catch {
        // Endpoint may not exist in this build; degrade gracefully.
        return [] as AdminPostRow[];
      }
    }
  });

  const toggleHidden = useMutation({
    mutationFn: ({ id, hide }: { id: string; hide: boolean }) =>
      api.post(`/v1/moderation/actions`, {
        action: hide ? "Hide" : "Restore",
        targetType: "Post",
        targetId: id,
        notes: hide ? "Hidden from admin feed queue" : "Restored from admin feed queue"
      }),
    onSuccess: () => {
      toast.success("Updated.");
      qc.invalidateQueries({ queryKey: ["admin-posts"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Failed.")
  });

  const columns: Column<AdminPostRow>[] = [
    {
      key: "post", header: "Post",
      render: (p) => (
        <div className="flex items-center gap-3 max-w-md">
          {p.mediaUrls[0] && (
            <img src={p.mediaUrls[0]} className="h-10 w-10 rounded object-cover bg-muted" />
          )}
          <div className="min-w-0">
            <p className="text-sm font-medium truncate">{p.caption || "—"}</p>
            <p className="text-xs text-muted-foreground truncate">by {p.authorDisplayName}</p>
          </div>
        </div>
      )
    },
    { key: "reports", header: "Reports", className: "text-right w-20",
      render: (p) => p.reportsCount > 0
        ? <Badge variant="destructive">{p.reportsCount}</Badge>
        : <span className="text-muted-foreground">0</span> },
    { key: "engagement", header: "Engagement", className: "hidden sm:table-cell",
      render: (p) => <span className="text-xs text-muted-foreground">{p.reactionCount}♥ · {p.commentCount}💬</span> },
    { key: "status", header: "Status", className: "hidden sm:table-cell",
      render: (p) => p.isDeleted ? <Badge variant={statusBadgeVariant("Hidden")}>Hidden</Badge> : <Badge variant={statusBadgeVariant("Live")}>Live</Badge> },
    { key: "created", header: "Created", className: "hidden md:table-cell",
      render: (p) => <span className="text-xs text-muted-foreground">{new Date(p.createdAt).toLocaleDateString()}</span> },
    { key: "actions", header: "", className: "w-24 sm:w-44 text-right",
      render: (p) => (
        <div className="flex justify-end gap-1">
          <Button size="sm" variant="ghost"
            onClick={() => toggleHidden.mutate({ id: p.id, hide: !p.isDeleted })}>
            {p.isDeleted ? <Eye className="h-3 w-3 mr-1" /> : <EyeOff className="h-3 w-3 mr-1" />}
            {p.isDeleted ? "Restore" : "Hide"}
          </Button>
          <Button size="sm" variant="outline" onClick={() => setTarget(p)}>Action</Button>
        </div>
      ) }
  ];

  return (
    <div className="space-y-4">
      <PageHeader title="Feed moderation" icon={Flag}
        description="Review reported posts and apply moderation actions." />
      <Card>
        <CardContent className="pt-6 space-y-4">
          <FilterBar onReset={() => setMode("reported")}>
            <Select value={mode} onValueChange={(v) => setMode(v as Mode)}>
              <SelectTrigger className="w-full sm:w-44"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="reported">Reported only</SelectItem>
                <SelectItem value="hidden">Hidden</SelectItem>
                <SelectItem value="all">All recent</SelectItem>
              </SelectContent>
            </Select>
          </FilterBar>

          <DataTable
            data={data ?? []}
            columns={columns}
            rowKey={(p) => p.id}
            loading={isLoading}
            empty={<EmptyState icon={Flag} title="Nothing to moderate" description="Reported posts will appear here." />}
          />
        </CardContent>
      </Card>

      {target && (
        <ModerationActionModal
          open={!!target}
          onOpenChange={(o) => !o && setTarget(null)}
          targetType="Post"
          targetId={target.id}
          defaultAction={target.isDeleted ? "Restore" : "Hide"}
          onDone={() => qc.invalidateQueries({ queryKey: ["admin-posts"] })}
        />
      )}
    </div>
  );
}
