import { useState } from "react";
import { Link } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PawPrint, MailCheck, ShieldCheck, Hourglass } from "lucide-react";
import { useAuthStore } from "@/store/authStore";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { registerSchema, type RegisterInput } from "@/lib/schemas";
import { toast } from "@/components/ui/sonner";

export function RegisterPage() {
  const register = useAuthStore((s) => s.register);
  // Once submission succeeds the backend returns Pending — we don't navigate
  // anywhere because there's nothing for the user to do until an admin acts.
  const [submittedEmail, setSubmittedEmail] = useState<string | null>(null);

  const { register: r, handleSubmit, formState: { errors, isSubmitting } } =
    useForm<RegisterInput>({ resolver: zodResolver(registerSchema) });

  async function onSubmit(v: RegisterInput) {
    try {
      const result = await register(v.email, v.password, v.displayName, v.phoneNumber || undefined);
      setSubmittedEmail(result.email);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Registration failed");
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
          <CardTitle>{submittedEmail ? "Request received" : "Create your account"}</CardTitle>
          <p className="text-sm text-muted-foreground">
            {submittedEmail
              ? "Every new account is reviewed by an administrator before you can sign in."
              : "Free for pet owners. Every new account is approved by an administrator before sign-in."}
          </p>
        </CardHeader>
        <CardContent>
          {submittedEmail ? <PendingScreen email={submittedEmail} /> : (
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
              <div>
                <Label htmlFor="name">Display name</Label>
                <Input id="name" {...r("displayName")} />
                {errors.displayName && <p className="text-xs text-destructive mt-1">{errors.displayName.message}</p>}
              </div>
              <div>
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" autoComplete="email" {...r("email")} />
                {errors.email && <p className="text-xs text-destructive mt-1">{errors.email.message}</p>}
              </div>
              <div>
                <Label htmlFor="password">Password</Label>
                <Input id="password" type="password" autoComplete="new-password" {...r("password")} />
                {errors.password && <p className="text-xs text-destructive mt-1">{errors.password.message}</p>}
              </div>
              <div>
                <Label htmlFor="phone">Phone (optional)</Label>
                <Input id="phone" {...r("phoneNumber")} />
              </div>
              <Button type="submit" className="w-full" disabled={isSubmitting}>
                {isSubmitting ? "Submitting..." : "Request account"}
              </Button>
              <p className="text-sm text-muted-foreground text-center">
                Already have an account? <Link className="underline" to="/login">Sign in</Link>
              </p>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function PendingScreen({ email }: { email: string }) {
  return (
    <div className="space-y-5">
      <Step icon={MailCheck} title="1. Request submitted"
        body={<>Your registration for <span className="font-medium">{email}</span> has been recorded.</>} />
      <Step icon={Hourglass} title="2. Awaiting admin review"
        body="An administrator will review your request shortly. There's nothing you need to do right now." />
      <Step icon={ShieldCheck} title="3. Approval email"
        body="Once approved, we'll send a confirmation to your email and you can sign in." />
      <div className="pt-2">
        <Button asChild className="w-full"><Link to="/login">Back to sign in</Link></Button>
      </div>
    </div>
  );
}

function Step({ icon: Icon, title, body }: { icon: any; title: string; body: React.ReactNode }) {
  return (
    <div className="flex gap-3">
      <div className="h-8 w-8 rounded-full bg-primary/10 text-primary flex items-center justify-center shrink-0">
        <Icon className="h-4 w-4" />
      </div>
      <div className="space-y-0.5">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-xs text-muted-foreground">{body}</p>
      </div>
    </div>
  );
}
