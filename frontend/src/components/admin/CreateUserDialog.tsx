import { useEffect, useState } from "react";
import { UserPlus } from "lucide-react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Badge } from "@/components/ui/badge";
import { PasswordInput } from "@/components/ui/password-input";
import { adminApi, type CreateUserInput } from "@/api/admin";
import { rbacApi, type RoleDto } from "@/api/rbac";
import { toast } from "@/components/ui/sonner";
import { useAuthStore } from "@/store/authStore";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  /** Fires after a successful create so the parent can refresh the list. */
  onCreated: () => void;
}

const PRIVILEGED = new Set(["Admin", "SuperAdmin"]);

/**
 * Admin-driven user creation. Accepts an arbitrary set of role assignments;
 * the backend enforces "SuperAdmin-only" for Admin / SuperAdmin grants, but
 * we also hide those role checkboxes for non-SuperAdmin callers so they can't
 * even pick them.
 */
export function CreateUserDialog({ open, onOpenChange, onCreated }: Props) {
  const isSuperAdmin = useAuthStore((s) => s.user?.roles.includes("SuperAdmin")) ?? false;

  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [selectedRoles, setSelectedRoles] = useState<Set<string>>(new Set(["User"]));
  const [form, setForm] = useState<CreateUserInput>({
    email: "", password: "", displayName: "", phoneNumber: "", roles: ["User"]
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setForm({ email: "", password: "", displayName: "", phoneNumber: "", roles: ["User"] });
    setSelectedRoles(new Set(["User"]));
    setError(null);
    rbacApi.listRoles().then(setRoles).catch(() => setRoles([]));
  }, [open]);

  function toggle(name: string) {
    const next = new Set(selectedRoles);
    next.has(name) ? next.delete(name) : next.add(name);
    setSelectedRoles(next);
  }

  async function submit() {
    setError(null);
    if (!form.email.trim() || !form.password || !form.displayName.trim()) {
      setError("Email, password and display name are required.");
      return;
    }
    if (form.password.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }
    if (selectedRoles.size === 0) {
      setError("Pick at least one role.");
      return;
    }
    setBusy(true);
    try {
      await adminApi.createUser({
        email: form.email.trim(),
        password: form.password,
        displayName: form.displayName.trim(),
        phoneNumber: form.phoneNumber || undefined,
        roles: Array.from(selectedRoles)
      });
      toast.success(`Created ${form.displayName}.`);
      onCreated();
      onOpenChange(false);
    } catch (err: any) {
      setError(err?.response?.data?.error?.message ?? "Create failed.");
    } finally {
      setBusy(false);
    }
  }

  // Roles a non-SuperAdmin caller is not allowed to grant.
  const visibleRoles = roles.filter((r) => isSuperAdmin || !PRIVILEGED.has(r.name));

  return (
    <Dialog open={open} onOpenChange={(v) => !busy && onOpenChange(v)}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <UserPlus className="h-5 w-5 text-primary" /> Add a new user
          </DialogTitle>
          <DialogDescription>
            Creates an approved account. The user gets an email; tell them the password out-of-band.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div>
            <Label htmlFor="cu-name">Display name</Label>
            <Input id="cu-name" value={form.displayName}
              onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
          </div>
          <div>
            <Label htmlFor="cu-email">Email</Label>
            <Input id="cu-email" type="email" value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })} />
          </div>
          <div>
            <Label htmlFor="cu-pwd">Password</Label>
            <PasswordInput id="cu-pwd" autoComplete="new-password" value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })} />
            <p className="text-xs text-muted-foreground mt-1">Min 8 chars, upper, lower and digit.</p>
          </div>
          <div>
            <Label htmlFor="cu-phone">Phone (optional)</Label>
            <Input id="cu-phone" value={form.phoneNumber ?? ""}
              onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })} />
          </div>

          <div>
            <Label>Roles</Label>
            <div className="space-y-1.5 max-h-48 overflow-y-auto border rounded-md p-2 mt-1">
              {visibleRoles.map((r) => (
                <label key={r.id} className="flex items-start gap-2 text-sm cursor-pointer">
                  <Checkbox
                    checked={selectedRoles.has(r.name)}
                    onChange={() => toggle(r.name)}
                  />
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-1.5">
                      <span className="font-medium">{r.name}</span>
                      {PRIVILEGED.has(r.name) && <Badge variant="destructive" className="text-[10px]">privileged</Badge>}
                      {r.isSystem && <Badge variant="muted" className="text-[10px]">system</Badge>}
                    </div>
                    {r.description && <p className="text-xs text-muted-foreground">{r.description}</p>}
                  </div>
                </label>
              ))}
              {visibleRoles.length === 0 && <p className="text-xs text-muted-foreground">No roles available.</p>}
            </div>
          </div>
        </div>

        {error && <p className="text-sm text-destructive">{error}</p>}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>Cancel</Button>
          <Button onClick={submit} disabled={busy}>{busy ? "Creating..." : "Create user"}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
