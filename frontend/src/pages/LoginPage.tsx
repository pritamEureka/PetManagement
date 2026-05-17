import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PawPrint, ShieldCheck } from "lucide-react";
import { useAuthStore } from "@/store/authStore";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { loginSchema, type LoginInput } from "@/lib/schemas";
import { toast } from "@/components/ui/sonner";

export function LoginPage() {
  const login = useAuthStore((s) => s.login);
  const needsTwoFactor = useAuthStore((s) => s.needsTwoFactor);
  const nav = useNavigate();
  const loc = useLocation();
  const [twoFactorCode, setTwoFactorCode] = useState("");

  const { register, handleSubmit, formState: { errors, isSubmitting }, getValues } =
    useForm<LoginInput>({ resolver: zodResolver(loginSchema) });

  async function onSubmit(v: LoginInput) {
    try {
      await login(v.email, v.password, needsTwoFactor ? twoFactorCode : undefined);
      const to = (loc.state as { from?: { pathname: string } })?.from?.pathname ?? "/home";
      nav(to, { replace: true });
    } catch (err: any) {
      const msg = err?.response?.data?.error?.message ?? "Login failed";
      // The store has already set needsTwoFactor when the server asks for it.
      if (msg.includes("two_factor_required")) {
        toast.info("Enter your authenticator code to continue.");
        return;
      }
      toast.error(msg);
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
      toast.error(err?.response?.data?.error?.message ?? "Invalid code");
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-b from-primary/5 to-background">
      <div className="absolute top-4 right-4"><ThemeToggle /></div>
      <Card className="w-full max-w-md">
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
          {!needsTwoFactor ? (
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
              <div>
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" autoComplete="email" {...register("email")} />
                {errors.email && <p className="text-xs text-destructive mt-1">{errors.email.message}</p>}
              </div>
              <div>
                <Label htmlFor="password">Password</Label>
                <Input id="password" type="password" autoComplete="current-password" {...register("password")} />
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
                className="text-xs text-muted-foreground hover:underline w-full text-center"
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
