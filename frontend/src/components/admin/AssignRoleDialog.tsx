import { useEffect, useState } from "react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { rbacApi, type RoleDto, type UserRolesView } from "@/api/rbac";
import { toast } from "@/components/ui/sonner";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  userId: string;
  userDisplayName?: string;
  allRoles: RoleDto[];
  onChanged?: () => void;
}

export function AssignRoleDialog({ open, onOpenChange, userId, userDisplayName, allRoles, onChanged }: Props) {
  const [view, setView] = useState<UserRolesView | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function reload() {
    setView(await rbacApi.userRoles(userId));
  }
  useEffect(() => { if (open) { setError(null); reload(); } }, [open, userId]);

  async function toggle(roleId: string, currentlyAssigned: boolean) {
    const roleName = allRoles.find((r) => r.id === roleId)?.name ?? "Role";
    setBusy(roleId); setError(null);
    try {
      currentlyAssigned
        ? await rbacApi.revokeRole(userId, roleId)
        : await rbacApi.assignRole(userId, roleId);
      toast.success(currentlyAssigned ? `Revoked ${roleName}.` : `Assigned ${roleName}.`);
      onChanged?.();
      onOpenChange(false);
    } catch (err: any) {
      setError(err?.response?.data?.error?.message ?? "Operation failed.");
    } finally { setBusy(null); }
  }

  const assignedIds = new Set(view?.roles.map((r) => r.roleId) ?? []);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Roles for {userDisplayName ?? "user"}</DialogTitle>
          <DialogDescription>
            You can only assign roles whose permissions are a subset of your own.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-2 max-h-96 overflow-y-auto pr-1">
          {allRoles.map((r) => {
            const assigned = assignedIds.has(r.id);
            return (
              <div key={r.id} className="flex items-center justify-between border rounded-md px-3 py-2">
                <div className="flex flex-col">
                  <span className="font-medium">{r.name}</span>
                  {r.description && <span className="text-xs text-muted-foreground">{r.description}</span>}
                  <span className="text-xs text-muted-foreground mt-0.5">{r.permissions.length} permissions</span>
                </div>
                <div className="flex items-center gap-2">
                  {assigned && <Badge variant="secondary">assigned</Badge>}
                  <Button
                    size="sm"
                    variant={assigned ? "outline" : "default"}
                    disabled={busy === r.id}
                    onClick={() => toggle(r.id, assigned)}
                  >
                    {busy === r.id ? "..." : assigned ? "Revoke" : "Assign"}
                  </Button>
                </div>
              </div>
            );
          })}
        </div>

        {error && <p className="text-sm text-destructive">{error}</p>}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
