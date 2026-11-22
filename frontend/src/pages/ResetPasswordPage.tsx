import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { PawPrint } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PasswordInput } from "@/components/ui/password-input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { toast } from "@/components/ui/sonner";
import { recoveryApi } from "@/api/securityPlus";

/** Completes recovery with the emailed token (?token=...) or with email + a backup recovery code. */
export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const nav = useNavigate();
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (password !== confirm) return setError("Passwords do not match.");
    setBusy(true);
    try {
      await recoveryApi.complete(token
        ? { token, newPassword: password }
        : { email: email.trim(), recoveryCode: code.trim(), newPassword: password });
      toast.success("Password updated. Sign in with your new password.");
      nav("/login", { replace: true });
    } catch (err: any) {
      const details = err?.response?.data?.error?.details?.newPassword as string[] | undefined;
      setError(details?.[0] ?? err?.response?.data?.error?.message ?? "Could not reset the password.");
    } finally { setBusy(false); }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-b from-primary/5 to-background">
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-1">
          <Link to="/" className="flex items-center gap-2 text-primary font-bold">
            <PawPrint className="h-5 w-5" /> Pawzaroo
          </Link>
          <CardTitle>Choose a new password</CardTitle>
          {!token && (
            <p className="text-sm text-muted-foreground">Enter your email and one of your backup recovery codes.</p>
          )}
        </CardHeader>
        <CardContent>
          <form onSubmit={submit} className="space-y-3">
            {!token && (
              <>
                <div>
                  <Label htmlFor="rp-email">Email</Label>
                  <Input id="rp-email" type="email" autoComplete="email" required value={email}
                    onChange={(e) => setEmail(e.target.value)} />
                </div>
                <div>
                  <Label htmlFor="rp-code">Recovery code</Label>
                  <Input id="rp-code" required autoComplete="off" placeholder="abcde-12345" value={code}
                    onChange={(e) => setCode(e.target.value)} />
                </div>
              </>
            )}
            <div>
              <Label htmlFor="rp-pw">New password</Label>
              <PasswordInput id="rp-pw" autoComplete="new-password" required value={password}
                onChange={(e) => setPassword(e.target.value)} />
              <p className="text-xs text-muted-foreground mt-1">At least 8 characters with upper case, lower case and a digit.</p>
            </div>
            <div>
              <Label htmlFor="rp-confirm">Confirm password</Label>
              <PasswordInput id="rp-confirm" autoComplete="new-password" required value={confirm}
                onChange={(e) => setConfirm(e.target.value)} />
            </div>
            {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
            <Button type="submit" className="w-full" disabled={busy}>{busy ? "Saving..." : "Reset password"}</Button>
            <p className="text-sm text-muted-foreground text-center">
              <Link className="underline" to="/forgot-password">Request a new link</Link>
            </p>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
