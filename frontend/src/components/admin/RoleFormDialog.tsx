import { useEffect, useMemo, useState } from "react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PermissionMatrix } from "./PermissionMatrix";
import { rbacApi, type PermissionDto, type RoleDto } from "@/api/rbac";
import { usePermissions } from "@/hooks/usePermissions";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  role?: RoleDto;            // present = edit, absent = create
  allPermissions: PermissionDto[];
  onSaved: () => void;
}

export function RoleFormDialog({ open, onOpenChange, role, allPermissions, onSaved }: Props) {
  const { permissions: mine, isSuperAdmin } = usePermissions();
  const grantable = useMemo(() => (isSuperAdmin ? null : new Set(mine)), [isSuperAdmin, mine]);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setName(role?.name ?? "");
    setDescription(role?.description ?? "");
    setSelected(new Set(role?.permissions ?? []));
    setError(null);
  }, [role, open]);

  const isSuperRole = role?.name === "SuperAdmin";
  const isSystem = !!role?.isSystem;
  const editing = !!role;

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null); setSaving(true);
    try {
      if (editing) {
        await rbacApi.updateRole(role!.id, { description, permissions: Array.from(selected) });
      } else {
        await rbacApi.createRole({ name, description, permissions: Array.from(selected) });
      }
      onSaved(); onOpenChange(false);
    } catch (err: any) {
      setError(err?.response?.data?.error?.message ?? "Failed to save role.");
    } finally { setSaving(false); }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>{editing ? `Edit role: ${role!.name}` : "Create role"}</DialogTitle>
          <DialogDescription>
            {isSuperRole
              ? "SuperAdmin permissions are immutable."
              : "Tick the actions this role can perform. You can only grant permissions you hold yourself."}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="name">Name</Label>
              <Input id="name" value={name} onChange={(e) => setName(e.target.value)}
                     disabled={editing} required maxLength={64} />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="desc">Description</Label>
              <Input id="desc" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={512} />
            </div>
          </div>

          <PermissionMatrix
            allPermissions={allPermissions}
            value={selected}
            grantable={grantable}
            onChange={setSelected}
            readOnly={isSuperRole}
          />

          {error && <p className="text-sm text-destructive">{error}</p>}

          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={saving || isSuperRole || (!editing && !name.trim())}>
              {saving ? "Saving..." : editing ? "Save changes" : "Create role"}
            </Button>
          </DialogFooter>
          {isSystem && !isSuperRole && (
            <p className="text-xs text-muted-foreground">System role — cannot be deleted.</p>
          )}
        </form>
      </DialogContent>
    </Dialog>
  );
}
