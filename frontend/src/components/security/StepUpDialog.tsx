import { useState } from "react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PasswordInput } from "@/components/ui/password-input";
import { securityPlusApi } from "@/api/securityPlus";

interface Props {
  open: boolean;
  /** Called with true after a successful verification, false when the user cancels. */
  onResolve: (verified: boolean) => void;
}

/** Re-authentication prompt (password, or authenticator / recovery code) used before sensitive actions. */
export function StepUpDialog({ open, onResolve }: Props) {
  const [mode, setMode] = useState<"password" | "code">("password");
  const [value, setValue] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e?: React.FormEvent) {
    e?.preventDefault();
    if (!value.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await securityPlusApi.stepUp(mode === "password" ? { password: value } : { otpCode: value.trim() });
      setValue("");
      onResolve(true);
    } catch {
      setError(mode === "password" ? "That password is not right." : "That code is not valid.");
    } finally { setBusy(false); }
  }

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onResolve(false); }}>
      <DialogContent className="max-w-sm">
        <form onSubmit={submit} className="space-y-4">
          <DialogHeader>
            <DialogTitle>Confirm it is you</DialogTitle>
            <DialogDescription>
              This action is sensitive. Re-enter your {mode === "password" ? "password" : "authenticator or recovery code"} to continue.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Label htmlFor="stepup-value">{mode === "password" ? "Password" : "Authenticator or recovery code"}</Label>
            {mode === "password" ? (
              <PasswordInput id="stepup-value" autoFocus autoComplete="current-password" value={value}
                onChange={(e) => setValue(e.target.value)} />
            ) : (
              <Input id="stepup-value" autoFocus autoComplete="one-time-code" value={value}
                onChange={(e) => setValue(e.target.value)} placeholder="123456 or abcde-12345" />
            )}
            {error && <p role="alert" className="text-xs text-destructive">{error}</p>}
          </div>
          <button type="button" className="text-xs text-muted-foreground underline"
            onClick={() => { setMode(mode === "password" ? "code" : "password"); setValue(""); setError(null); }}>
            {mode === "password" ? "Use an authenticator or recovery code instead" : "Use my password instead"}
          </button>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onResolve(false)}>Cancel</Button>
            <Button type="submit" disabled={busy || !value.trim()}>{busy ? "Verifying..." : "Verify"}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
