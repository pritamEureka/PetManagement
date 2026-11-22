import { useState } from "react";
import { Link } from "react-router-dom";
import { PawPrint } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { recoveryApi } from "@/api/securityPlus";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) return;
    setBusy(true);
    try { await recoveryApi.start(email.trim()); } catch { /* same neutral message either way */ }
    setSent(true);
    setBusy(false);
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-b from-primary/5 to-background">
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-1">
          <Link to="/" className="flex items-center gap-2 text-primary font-bold">
            <PawPrint className="h-5 w-5" /> Pawzaroo
          </Link>
          <CardTitle>Reset your password</CardTitle>
        </CardHeader>
        <CardContent>
          {sent ? (
            <div className="space-y-3 text-sm">
              <p>If an account exists for that email, we have sent a reset link. It expires in 30 minutes.</p>
              <p className="text-muted-foreground">
                Have a backup recovery code instead? <Link className="underline" to="/reset-password">Use it here</Link>.
              </p>
              <Link className="underline" to="/login">Back to sign in</Link>
            </div>
          ) : (
            <form onSubmit={submit} className="space-y-3">
              <div>
                <Label htmlFor="fp-email">Email</Label>
                <Input id="fp-email" type="email" autoComplete="email" required value={email}
                  onChange={(e) => setEmail(e.target.value)} />
              </div>
              <Button type="submit" className="w-full" disabled={busy}>{busy ? "Sending..." : "Send reset link"}</Button>
              <p className="text-sm text-muted-foreground text-center">
                <Link className="underline" to="/reset-password">I have a recovery code</Link>
                {" · "}
                <Link className="underline" to="/login">Back to sign in</Link>
              </p>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
