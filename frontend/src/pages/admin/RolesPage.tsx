import { useEffect, useState } from "react";
import { Plus, Pencil, Trash2, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Can } from "@/components/auth/Can";
import { rbacApi, type PermissionDto, type RoleDto } from "@/api/rbac";
import { RoleFormDialog } from "@/components/admin/RoleFormDialog";

export function RolesPage() {
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [allPerms, setAllPerms] = useState<PermissionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<RoleDto | undefined>();
  const [open, setOpen] = useState(false);

  async function load() {
    setLoading(true);
    try {
      const [r, p] = await Promise.all([rbacApi.listRoles(), rbacApi.listPermissions()]);
      setRoles(r); setAllPerms(p);
    } finally { setLoading(false); }
  }
  useEffect(() => { load(); }, []);

  function openCreate() { setEditing(undefined); setOpen(true); }
  function openEdit(role: RoleDto) { setEditing(role); setOpen(true); }

  async function onDelete(role: RoleDto) {
    if (role.isSystem) return;
    if (!confirm(`Delete role "${role.name}"? Users currently holding it will lose its permissions.`)) return;
    try {
      await rbacApi.deleteRole(role.id);
      await load();
    } catch (err: any) {
      alert(err?.response?.data?.error?.message ?? "Delete failed.");
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <ShieldCheck className="h-6 w-6 text-primary" /> Roles & permissions
          </h1>
          <p className="text-sm text-muted-foreground">
            Dynamic RBAC — {allPerms.length} permissions across {new Set(allPerms.map((p) => p.module)).size} modules.
          </p>
        </div>
        <Can permission="roles.create">
          <Button onClick={openCreate}><Plus className="h-4 w-4 mr-2" /> New role</Button>
        </Can>
      </div>

      {loading && <p className="text-muted-foreground">Loading...</p>}

      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {roles.map((r) => (
          <Card key={r.id}>
            <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-2">
              <div className="space-y-1">
                <CardTitle className="text-base">{r.name}</CardTitle>
                {r.description && <p className="text-xs text-muted-foreground">{r.description}</p>}
              </div>
              {r.isSystem && <Badge variant="muted">system</Badge>}
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex flex-wrap gap-1.5 max-h-32 overflow-y-auto">
                {r.permissions.slice(0, 12).map((p) => (
                  <Badge key={p} variant="outline" className="font-mono text-[10px]">{p}</Badge>
                ))}
                {r.permissions.length > 12 && (
                  <Badge variant="muted">+{r.permissions.length - 12} more</Badge>
                )}
                {r.permissions.length === 0 && (
                  <span className="text-xs text-muted-foreground">No permissions assigned.</span>
                )}
              </div>
              <div className="flex justify-end gap-2">
                <Can permission="roles.edit">
                  <Button size="sm" variant="outline" onClick={() => openEdit(r)}>
                    <Pencil className="h-3 w-3 mr-1" /> Edit
                  </Button>
                </Can>
                <Can permission="roles.delete">
                  <Button
                    size="sm" variant="destructive" onClick={() => onDelete(r)}
                    disabled={r.isSystem}
                  >
                    <Trash2 className="h-3 w-3 mr-1" /> Delete
                  </Button>
                </Can>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      <RoleFormDialog
        open={open}
        onOpenChange={setOpen}
        role={editing}
        allPermissions={allPerms}
        onSaved={load}
      />
    </div>
  );
}
