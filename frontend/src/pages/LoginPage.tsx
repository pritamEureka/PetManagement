import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PawPrint, ShieldCheck, Hourglass, XCircle } from "lucide-react";
import { useAuthStore } from "@/store/authStore";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/ui/password-input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { AuthPawTrail } from "@/components/auth/AuthPawTrail";
import { loginSchema, type LoginInput } from "@/lib/schemas";
import { toast } from "@/components/ui/sonner";

// Server-emitted status messages we render inline instead of as a transient toast.
type ApprovalStatus = { kind: "pending" | "rejected"; message: string };

export function LoginPage() {
  const login = useAuthStore((s) => s.login);
  const needsTwoFactor = useAuthStore((s) => s.needsTwoFactor);
  const nav = useNavigate();
  const loc = useLocation();
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [approval, setApproval] = useState<ApprovalStatus | null>(null);

  const { register, handleSubmit, formState: { errors, isSubmitting }, getValues } =
    useForm<LoginInput>({ resolver: zodResolver(loginSchema) });

  // Translate the server's structured error codes into an inline approval banner
  // (so the user sees what step they're on, not just a toast that disappears).
  function handleLoginError(err: any): boolean {
    const code: string | undefined = err?.response?.data?.error?.code;
    const message: string = err?.response?.data?.error?.message ?? "Login failed";
    if (code === "registration_pending") { setApproval({ kind: "pending", message }); return true; }
    if (code === "registration_rejected") { setApproval({ kind: "rejected", message }); return true; }
    if (message.includes("two_factor_required")) { toast.info("Enter your authenticator code to continue."); return true; }
    return false;
  }

  async function onSubmit(v: LoginInput) {
    setApproval(null);
    try {
      await login(v.email, v.password, needsTwoFactor ? twoFactorCode : undefined);
      const to = (loc.state as { from?: { pathname: string } })?.from?.pathname ?? "/home";
      nav(to, { replace: true });
    } catch (err: any) {
      if (handleLoginError(err)) return;
      toast.error(err?.response?.data?.error?.message ?? "Login failed");
    }
  }

  // Re-submit with the 2FA code (uses the email/password already in the form state).
  async function submit2FA() {
    const v = getValues();
    if (!twoFactorCode) { toast.error("Enter the 6-digit code."); return; }
    try {
      await login(v.email, v.password, twoFactorCode);
      const to = (loc.state as { from?: { pathname: string } })?.from?.pathname ?? "/home";
      nav(to, { replace: true });
    } catch (err: any) {
      if (handleLoginError(err)) return;
      toast.error(err?.response?.data?.error?.message ?? "Invalid code");
    }
  }

  return (
    <div className="relative min-h-screen flex items-center justify-center overflow-hidden p-4 bg-gradient-to-b from-primary/5 to-background">
      <AuthPawTrail />
      <div className="absolute top-4 right-4 z-10"><ThemeToggle /></div>
      <Card className="relative z-10 w-full max-w-md" data-auth-card="true">
        <CardHeader className="space-y-1">
          <Link to="/" className="flex items-center gap-2 text-primary font-bold">
            <PawPrint className="h-5 w-5" /> Pawzaroo
          </Link>
          <CardTitle>{needsTwoFactor ? "Two-factor verification" : "Welcome back"}</CardTitle>
          <p className="text-sm text-muted-foreground">
            {needsTwoFactor
              ? "Open your authenticator app and enter the 6-digit code."
              : "Sign in to your account."}
          </p>
        </CardHeader>
        <CardContent>
          {approval && <ApprovalBanner status={approval} />}
          {!needsTwoFactor ? (
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
              <div>
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" autoComplete="email" {...register("email")} />
                {errors.email && <p className="text-xs text-destructive mt-1">{errors.email.message}</p>}
              </div>
              <div>
                <Label htmlFor="password">Password</Label>
                <PasswordInput id="password" autoComplete="current-password" {...register("password")} />
                {errors.password && <p className="text-xs text-destructive mt-1">{errors.password.message}</p>}
              </div>
              <Button type="submit" className="w-full" disabled={isSubmitting}>
                {isSubmitting ? "Signing in..." : "Sign in"}
              </Button>
              <p className="text-sm text-muted-foreground text-center">
                No account? <Link className="underline" to="/register">Create one</Link>
              </p>
            </form>
          ) : (
            <div className="space-y-3">
              <div className="flex items-center justify-center text-primary">
                <ShieldCheck className="h-8 w-8" />
              </div>
              <Label htmlFor="otp">Authenticator code</Label>
              <Input
                id="otp"
                inputMode="numeric"
                autoComplete="one-time-code"
                pattern="[0-9]{6,8}"
                maxLength={8}
                value={twoFactorCode}
                onChange={(e) => setTwoFactorCode(e.target.value.trim())}
                placeholder="123456"
              />
              <Button className="w-full" onClick={submit2FA} disabled={isSubmitting || twoFactorCode.length < 6}>
                Verify and sign in
              </Button>
              <button type="button"
                className="w-full rounded-md py-1 text-center text-xs text-muted-foreground hover:bg-muted hover:text-foreground"
                onClick={() => useAuthStore.getState().clearTwoFactor()}>
                Use a different account
              </button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function ApprovalBanner({ status }: { status: ApprovalStatus }) {
  const pending = status.kind === "pending";
  const Icon = pending ? Hourglass : XCircle;
  const tone = pending
    ? "border-amber-500/40 bg-amber-500/10 text-amber-900 dark:text-amber-200"
    : "border-destructive/40 bg-destructive/10 text-destructive";
  const title = pending ? "Your registration is in approval" : "Registration rejected";

  return (
    <div className={`mb-4 rounded-md border p-3 text-sm ${tone}`}>
      <div className="flex items-start gap-2">
        <Icon className="h-4 w-4 mt-0.5 shrink-0" />
        <div className="space-y-1">
          <p className="font-medium">{title}</p>
          <p className="text-xs leading-relaxed">{status.message}</p>
          {pending && (
            <ol className="text-xs leading-relaxed list-decimal pl-4 pt-1 space-y-0.5 text-muted-foreground">
              <li>Request submitted.</li>
              <li>Waiting for an administrator to approve.</li>
              <li>Approval email will be sent to your address.</li>
              <li>Sign in here once approved.</li>
            </ol>
          )}
        </div>
      </div>
    </div>
  );
}
