import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Users as UsersIcon, Search, MoreHorizontal, KeyRound, Ban, RotateCcw,
  Eye, Pencil, Trash2, Plus
} from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { PromptDialog } from "@/components/common/PromptDialog";
import { Can } from "@/components/auth/Can";
import { adminApi, type AdminUser } from "@/api/admin";
import { rbacApi } from "@/api/rbac";
import { AssignRoleDialog } from "@/components/admin/AssignRoleDialog";
import { CreateUserDialog } from "@/components/admin/CreateUserDialog";
import { EditUserDialog } from "@/components/admin/EditUserDialog";
import { ViewUserDialog } from "@/components/admin/ViewUserDialog";
import { toast } from "@/components/ui/sonner";
import { useAuthStore } from "@/store/authStore";

const ROLE_BADGE_CLASSES: Record<string, string> = {
  superadmin: "border-fuchsia-200 bg-fuchsia-100 text-fuchsia-800 dark:border-fuchsia-900/60 dark:bg-fuchsia-950/60 dark:text-fuchsia-200",
  admin: "border-violet-200 bg-violet-100 text-violet-800 dark:border-violet-900/60 dark:bg-violet-950/60 dark:text-violet-200",
  moderator: "border-rose-200 bg-rose-100 text-rose-800 dark:border-rose-900/60 dark:bg-rose-950/60 dark:text-rose-200",
  user: "border-slate-200 bg-slate-100 text-slate-800 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-200",
  doctor: "border-teal-200 bg-teal-100 text-teal-800 dark:border-teal-900/60 dark:bg-teal-950/60 dark:text-teal-200",
  vet: "border-teal-200 bg-teal-100 text-teal-800 dark:border-teal-900/60 dark:bg-teal-950/60 dark:text-teal-200",
  storeowner: "border-amber-200 bg-amber-100 text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/60 dark:text-amber-200",
  deliveryuser: "border-sky-200 bg-sky-100 text-sky-800 dark:border-sky-900/60 dark:bg-sky-950/60 dark:text-sky-200"
};

const FALLBACK_ROLE_BADGE_CLASSES = [
  "border-cyan-200 bg-cyan-100 text-cyan-800 dark:border-cyan-900/60 dark:bg-cyan-950/60 dark:text-cyan-200",
  "border-lime-200 bg-lime-100 text-lime-800 dark:border-lime-900/60 dark:bg-lime-950/60 dark:text-lime-200",
  "border-orange-200 bg-orange-100 text-orange-800 dark:border-orange-900/60 dark:bg-orange-950/60 dark:text-orange-200",
  "border-indigo-200 bg-indigo-100 text-indigo-800 dark:border-indigo-900/60 dark:bg-indigo-950/60 dark:text-indigo-200"
];

function roleBadgeClass(role: string) {
  const key = role.toLowerCase().replace(/[\s_-]/g, "");
  const known = ROLE_BADGE_CLASSES[key];
  if (known) return known;

  const hash = Array.from(key).reduce((total, char) => total + char.charCodeAt(0), 0);
  return FALLBACK_ROLE_BADGE_CLASSES[hash % FALLBACK_ROLE_BADGE_CLASSES.length];
}

export function UsersPage() {
  const qc = useQueryClient();
  const isSuperAdmin = useAuthStore((s) => s.user?.roles.includes("SuperAdmin")) ?? false;

  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);

  // Dialog state — one slot per action, holds the row the action targets.
  const [roleUser, setRoleUser]       = useState<AdminUser | null>(null);
  const [viewing, setViewing]         = useState<AdminUser | null>(null);
  const [editing, setEditing]         = useState<AdminUser | null>(null);
  const [deleting, setDeleting]       = useState<AdminUser | null>(null);
  const [suspending, setSuspending]   = useState<AdminUser | null>(null);
  const [restoring, setRestoring]     = useState<AdminUser | null>(null);
  const [creating, setCreating]       = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ["admin", "users", q, page],
    queryFn: () => adminApi.users({ q: q || undefined, page, pageSize: 20 })
  });

  const { data: roles } = useQuery({ queryKey: ["rbac", "roles"], queryFn: rbacApi.listRoles });

  function refresh() { qc.invalidateQueries({ queryKey: ["admin", "users"] }); }

  async function doSuspend(reason: string) {
    if (!suspending) return;
    try {
      await adminApi.suspend(suspending.id, reason);
      toast.success(`${suspending.displayName} suspended.`);
      refresh();
    } catch (e: any) {
      toast.error(e?.response?.data?.error?.message ?? "Failed.");
    }
  }

  async function doRestore() {
    if (!restoring) return;
    try {
      await adminApi.restore(restoring.id);
      toast.success(`${restoring.displayName} restored.`);
      refresh();
    } catch (e: any) {
      toast.error(e?.response?.data?.error?.message ?? "Failed.");
    }
  }

  async function doDelete() {
    if (!deleting) return;
    try {
      await adminApi.deleteUser(deleting.id);
      toast.success(`${deleting.displayName} permanently deleted.`);
      refresh();
    } catch (e: any) {
      toast.error(e?.response?.data?.error?.message ?? "Delete failed.");
    }
  }

  const columns: Column<AdminUser>[] = [
    {
      key: "user", header: "User",
      render: (u) => (
        <div className="flex items-center gap-3">
          <Avatar className="h-8 w-8"><AvatarFallback>{u.displayName[0]}</AvatarFallback></Avatar>
          <div className="min-w-0">
            <p className="font-medium truncate">{u.displayName}</p>
            <p className="text-xs text-muted-foreground truncate">{u.email}</p>
          </div>
        </div>
      )
    },
    {
      key: "roles", header: "Roles",
      render: (u) => (
        <div className="flex flex-wrap gap-1">
          {u.roles.length === 0
            ? <span className="text-xs text-muted-foreground">—</span>
            : u.roles.map((r) => <Badge key={r} variant="outline" className={`text-[10px] ${roleBadgeClass(r)}`}>{r}</Badge>)}
        </div>
      )
    },
    {
      key: "status", header: "Status", className: "w-32",
      render: (u) =>
        u.isSuspended ? <Badge variant={statusBadgeVariant("Suspended")}>Suspended</Badge>
                       : u.approvalStatus === "Pending" ? <Badge variant={statusBadgeVariant("Pending")}>Pending</Badge>
                       : u.approvalStatus === "Rejected" ? <Badge variant={statusBadgeVariant("Rejected")}>Rejected</Badge>
                       : u.isActive ? <Badge variant={statusBadgeVariant("Active")}>Active</Badge>
                                    : <Badge variant={statusBadgeVariant("Inactive")}>Inactive</Badge>
    },
    {
      key: "joined", header: "Joined", className: "w-32",
      render: (u) => <span className="text-xs text-muted-foreground">{new Date(u.createdAt).toLocaleDateString()}</span>
    },
    {
      key: "actions", header: "", className: "w-12",
      render: (u) => (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon"><MoreHorizontal className="h-4 w-4" /></Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => setViewing(u)}>
              <Eye className="h-3.5 w-3.5 mr-2" /> View
            </DropdownMenuItem>
            <Can permission="users.edit">
              <DropdownMenuItem onClick={() => setEditing(u)}>
                <Pencil className="h-3.5 w-3.5 mr-2" /> Edit
              </DropdownMenuItem>
            </Can>
            <Can permission="roles.assign">
              <DropdownMenuItem onClick={() => setRoleUser(u)}>
                <KeyRound className="h-3.5 w-3.5 mr-2" /> Manage roles
              </DropdownMenuItem>
            </Can>
            <DropdownMenuSeparator />
            {u.isSuspended ? (
              <Can permission="users.restore">
                <DropdownMenuItem onClick={() => setRestoring(u)}>
                  <RotateCcw className="h-3.5 w-3.5 mr-2" /> Restore
                </DropdownMenuItem>
              </Can>
            ) : (
              <Can permission="users.suspend">
                <DropdownMenuItem destructive onClick={() => setSuspending(u)}>
                  <Ban className="h-3.5 w-3.5 mr-2" /> Suspend
                </DropdownMenuItem>
              </Can>
            )}
            {/* Permanent delete is SuperAdmin-only, both gated client- and server-side. */}
            {isSuperAdmin && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem destructive onClick={() => setDeleting(u)}>
                  <Trash2 className="h-3.5 w-3.5 mr-2" /> Delete permanently
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      )
    }
  ];

  const totalPages = Math.max(1, Math.ceil((data?.total ?? 0) / 20));

  return (
    <div className="space-y-4">
      <PageHeader title="Users" icon={UsersIcon}
        description={data ? `${data.total} total` : "Manage accounts and roles."}
        actions={
          <Can permission="users.create">
            <Button onClick={() => setCreating(true)}>
              <Plus className="h-4 w-4 mr-2" /> New user
            </Button>
          </Can>
        } />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <div className="relative max-w-sm">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input className="pl-8" placeholder="Search by name or email"
                   value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} />
          </div>

          <DataTable
            data={data?.items ?? []}
            columns={columns}
            rowKey={(u) => u.id}
            loading={isLoading}
            empty={<EmptyState icon={UsersIcon} title="No users found"
                               description={q ? "Try adjusting your search." : "Users will appear here as they sign up."} />}
          />

          {totalPages > 1 && (
            <div className="flex flex-col sm:flex-row items-center justify-between gap-2 text-sm">
              <span className="text-muted-foreground">Page {page} of {totalPages}</span>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Create */}
      <CreateUserDialog
        open={creating}
        onOpenChange={setCreating}
        onCreated={refresh}
      />

      {/* View */}
      <ViewUserDialog
        open={!!viewing}
        onOpenChange={(v) => !v && setViewing(null)}
        userId={viewing?.id ?? null}
      />

      {/* Edit */}
      <EditUserDialog
        open={!!editing}
        onOpenChange={(v) => !v && setEditing(null)}
        user={editing}
        onSaved={refresh}
      />

      {/* Manage roles (existing dialog) */}
      {roleUser && (
        <AssignRoleDialog
          open={!!roleUser}
          onOpenChange={(v) => !v && setRoleUser(null)}
          userId={roleUser.id}
          userDisplayName={roleUser.displayName}
          allRoles={roles ?? []}
        />
      )}

      {/* Suspend — needs a reason */}
      <PromptDialog
        open={!!suspending}
        onOpenChange={(v) => !v && setSuspending(null)}
        title={`Suspend ${suspending?.displayName ?? ""}`}
        description="The user will be signed out and prevented from accessing the platform until restored."
        label="Reason"
        placeholder="What's the reason for the suspension?"
        confirmLabel="Suspend"
        destructive
        onSubmit={doSuspend}
      />

      {/* Restore — simple yes/no */}
      <ConfirmDialog
        open={!!restoring}
        onOpenChange={(v) => !v && setRestoring(null)}
        title={`Restore ${restoring?.displayName ?? "user"}?`}
        description="They will be able to sign in again immediately."
        confirmLabel="Restore"
        onConfirm={doRestore}
      />

      {/* Delete permanently — destructive, double-confirm via title wording */}
      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={`Permanently delete ${deleting?.displayName ?? "user"}?`}
        description={
          <>
            This will remove <span className="font-medium">{deleting?.email}</span> from the database entirely.
            If the account has posts, orders, or listings linked to it the delete will fail and you'll need to suspend instead.
            This cannot be undone.
          </>
        }
        confirmLabel="Delete permanently"
        destructive
        onConfirm={doDelete}
      />
    </div>
  );
}
