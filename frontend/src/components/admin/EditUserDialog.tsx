import { useEffect, useState } from "react";
import { Pencil } from "lucide-react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { adminApi, type AdminUser } from "@/api/admin";
import { toast } from "@/components/ui/sonner";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  user: AdminUser | null;
  onSaved: () => void;
}

/**
 * Edit a user's basic profile fields. Roles are managed in the separate
 * AssignRoleDialog; suspension is in the row action menu via the moderation API.
 */
export function EditUserDialog({ open, onOpenChange, user, onSaved }: Props) {
  const [displayName, setDisplayName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !user) return;
    setDisplayName(user.displayName);
    setPhoneNumber(""); // not on the list payload; user may fill if changing
    setIsActive(user.isActive);
    setError(null);
  }, [open, user]);

  async function submit() {
    if (!user) return;
    setError(null);
    if (!displayName.trim()) {
      setError("Display name is required.");
      return;
    }
    setBusy(true);
    try {
      await adminApi.updateUser(user.id, {
        displayName: displayName.trim(),
        // Only send the phone change if the admin actually typed something —
        // an empty string means "leave alone" here.
        phoneNumber: phoneNumber === "" ? undefined : phoneNumber,
        isActive
      });
      toast.success(`Updated ${displayName}.`);
      onSaved();
      onOpenChange(false);
    } catch (err: any) {
      setError(err?.response?.data?.error?.message ?? "Update failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !busy && onOpenChange(v)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Pencil className="h-4 w-4 text-primary" /> Edit user
          </DialogTitle>
          <DialogDescription>{user?.email}</DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div>
            <Label htmlFor="eu-name">Display name</Label>
            <Input id="eu-name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="eu-phone">Phone</Label>
            <Input id="eu-phone" placeholder="Leave blank to keep current"
              value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} />
          </div>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={isActive} onChange={(e) => setIsActive(e.currentTarget.checked)} />
            <span>Active <span className="text-xs text-muted-foreground">(inactive accounts can't sign in)</span></span>
          </label>
        </div>

        {error && <p className="text-sm text-destructive">{error}</p>}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>Cancel</Button>
          <Button onClick={submit} disabled={busy}>{busy ? "Saving..." : "Save"}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
