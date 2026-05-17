import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Users as UsersIcon, Search, MoreHorizontal, KeyRound, Ban, RotateCcw } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";
import { PageHeader } from "@/components/common/PageHeader";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { Can } from "@/components/auth/Can";
import { adminApi, type AdminUser } from "@/api/admin";
import { rbacApi } from "@/api/rbac";
import { AssignRoleDialog } from "@/components/admin/AssignRoleDialog";
import { toast } from "@/components/ui/sonner";

export function UsersPage() {
  const qc = useQueryClient();
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const [roleUser, setRoleUser] = useState<AdminUser | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["admin", "users", q, page],
    queryFn: () => adminApi.users({ q: q || undefined, page, pageSize: 20 })
  });

  const { data: roles } = useQuery({ queryKey: ["rbac", "roles"], queryFn: rbacApi.listRoles });

  async function suspend(u: AdminUser) {
    if (!confirm(`Suspend ${u.displayName}?`)) return;
    try { await adminApi.suspend(u.id); toast.success("Suspended."); qc.invalidateQueries({ queryKey: ["admin", "users"] }); }
    catch (e: any) { toast.error(e?.response?.data?.error?.message ?? "Failed."); }
  }
  async function restore(u: AdminUser) {
    try { await adminApi.restore(u.id); toast.success("Restored."); qc.invalidateQueries({ queryKey: ["admin", "users"] }); }
    catch (e: any) { toast.error(e?.response?.data?.error?.message ?? "Failed."); }
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
            : u.roles.map((r) => <Badge key={r} variant="outline" className="text-[10px]">{r}</Badge>)}
        </div>
      )
    },
    {
      key: "status", header: "Status", className: "w-32",
      render: (u) =>
        u.isSuspended ? <Badge variant="destructive">Suspended</Badge>
                       : u.isActive ? <Badge variant="secondary">Active</Badge>
                                    : <Badge variant="muted">Inactive</Badge>
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
            <Can permission="roles.assign">
              <DropdownMenuItem onClick={() => setRoleUser(u)}>
                <KeyRound className="h-3.5 w-3.5 mr-2" /> Manage roles
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </Can>
            {u.isSuspended ? (
              <Can permission="users.restore">
                <DropdownMenuItem onClick={() => restore(u)}>
                  <RotateCcw className="h-3.5 w-3.5 mr-2" /> Restore
                </DropdownMenuItem>
              </Can>
            ) : (
              <Can permission="users.suspend">
                <DropdownMenuItem destructive onClick={() => suspend(u)}>
                  <Ban className="h-3.5 w-3.5 mr-2" /> Suspend
                </DropdownMenuItem>
              </Can>
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
        description={data ? `${data.total} total` : "Manage accounts and roles."} />

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

      {roleUser && (
        <AssignRoleDialog
          open={!!roleUser}
          onOpenChange={(v) => !v && setRoleUser(null)}
          userId={roleUser.id}
          userDisplayName={roleUser.displayName}
          allRoles={roles ?? []}
        />
      )}
    </div>
  );
}
