import { useEffect, useMemo, useState } from "react";
import { Plus, Pencil, Trash2, ShieldCheck, Eye } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Can } from "@/components/auth/Can";
import { rbacApi, type PermissionDto, type RoleDto } from "@/api/rbac";
import { RoleFormDialog } from "@/components/admin/RoleFormDialog";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { toast } from "@/components/ui/sonner";

export function RolesPage() {
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [allPerms, setAllPerms] = useState<PermissionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<RoleDto | undefined>();
  const [open, setOpen] = useState(false);
  const [viewing, setViewing] = useState<RoleDto | null>(null);
  const [deleting, setDeleting] = useState<RoleDto | null>(null);

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

  function onDelete(role: RoleDto) {
    if (role.isSystem) return;
    setDeleting(role);
  }

  async function confirmDeleteRole() {
    if (!deleting) return;
    try {
      await rbacApi.deleteRole(deleting.id);
      toast.success(`Role "${deleting.name}" deleted.`);
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Delete failed.");
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

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
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
                <Button size="sm" variant="outline" onClick={() => setViewing(r)}>
                  <Eye className="h-3 w-3 mr-1" /> View
                </Button>
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

      <RoleViewDialog
        role={viewing}
        allPermissions={allPerms}
        onClose={() => setViewing(null)}
      />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={`Delete role "${deleting?.name ?? ""}"?`}
        description="Users currently holding this role will lose its permissions immediately. This cannot be undone."
        confirmLabel="Delete role"
        destructive
        onConfirm={confirmDeleteRole}
      />
    </div>
  );
}

/**
 * Read-only dialog. Groups the role's permissions by module so the reviewer can
 * see at a glance what surfaces the role unlocks, and uses each permission's
 * description (from the /v1/permissions endpoint) when available.
 */
function RoleViewDialog({
  role, allPermissions, onClose
}: {
  role: RoleDto | null;
  allPermissions: PermissionDto[];
  onClose: () => void;
}) {
  // Index full permission metadata by `module.action` code so we can pull the
  // friendly description for each permission the role carries.
  const permByCode = useMemo(() => {
    const map = new Map<string, PermissionDto>();
    for (const p of allPermissions) map.set(`${p.module}.${p.action}`, p);
    return map;
  }, [allPermissions]);

  // Group role permissions by module → keep sorted alphabetically inside each group.
  const grouped = useMemo(() => {
    if (!role) return [] as { module: string; items: { code: string; action: string; description?: string | null }[] }[];
    const byModule = new Map<string, { code: string; action: string; description?: string | null }[]>();
    for (const code of role.permissions) {
      const meta = permByCode.get(code);
      const [module, action] = meta ? [meta.module, meta.action] : code.split(".", 2);
      const list = byModule.get(module) ?? [];
      list.push({ code, action, description: meta?.description });
      byModule.set(module, list);
    }
    return Array.from(byModule.entries())
      .map(([module, items]) => ({ module, items: items.sort((a, b) => a.action.localeCompare(b.action)) }))
      .sort((a, b) => a.module.localeCompare(b.module));
  }, [role, permByCode]);

  if (!role) return null;

  return (
    <Dialog open={!!role} onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5 text-primary" />
            {role.name}
            {role.isSystem && <Badge variant="muted">system</Badge>}
          </DialogTitle>
          <DialogDescription>
            {role.description ?? "No description."}
          </DialogDescription>
        </DialogHeader>

        <div className="text-xs text-muted-foreground">
          {role.permissions.length} permission{role.permissions.length === 1 ? "" : "s"}
          {grouped.length > 0 && <> across {grouped.length} module{grouped.length === 1 ? "" : "s"}</>}.
        </div>

        {role.permissions.length === 0 ? (
          <p className="text-sm text-muted-foreground py-6 text-center">
            This role has no permissions assigned.
          </p>
        ) : (
          <ScrollArea className="max-h-96 -mx-1 pr-2">
            <div className="space-y-4 px-1">
              {grouped.map((g) => (
                <div key={g.module} className="space-y-1.5">
                  <div className="flex items-center gap-2">
                    <h3 className="text-sm font-semibold capitalize">{g.module}</h3>
                    <Badge variant="muted" className="text-[10px]">{g.items.length}</Badge>
                  </div>
                  <ul className="space-y-1">
                    {g.items.map((p) => (
                      <li key={p.code} className="flex items-start gap-2 text-xs">
                        <Badge variant="outline" className="font-mono shrink-0">{p.code}</Badge>
                        {p.description && p.description !== p.code && (
                          <span className="text-muted-foreground leading-relaxed">{p.description}</span>
                        )}
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          </ScrollArea>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Close</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
