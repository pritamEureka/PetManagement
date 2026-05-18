import { useState } from "react";
import { Link } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  PawPrint, MailCheck, ShieldCheck, Hourglass, ArrowLeft, ArrowRight,
  Store, Stethoscope, Tag, Sparkles, HeartHandshake, Truck, Cat
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { useAuthStore } from "@/store/authStore";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/ui/password-input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { AuthPawTrail } from "@/components/auth/AuthPawTrail";
import { registerSchema, selfRegisterRoles, type RegisterInput, type SelfRegisterRole } from "@/lib/schemas";
import { toast } from "@/components/ui/sonner";
import { cn } from "@/lib/utils";

// Per-role icon. Kept here instead of in schemas.ts because that file is
// data-only and shouldn't import React component types.
const ROLE_ICON: Record<SelfRegisterRole, LucideIcon> = {
  User:            Cat,
  StoreOwner:      Store,
  Veterinarian:    Stethoscope,
  Seller:          Tag,
  ServiceProvider: Sparkles,
  Breeder:         PawPrint,
  AdoptionCenter:  HeartHandshake,
  DeliveryUser:    Truck,
};

type Step = "details" | "role" | "done";

export function RegisterPage() {
  const register = useAuthStore((s) => s.register);
  const [step, setStep] = useState<Step>("details");
  const [submittedEmail, setSubmittedEmail] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const { register: r, handleSubmit, setValue, trigger, getValues, formState: { errors } } =
    useForm<RegisterInput>({
      resolver: zodResolver(registerSchema),
      // Default the role to Pet Owner so schema validation passes even before
      // the user lands on the role-picker step. The picker still requires an
      // explicit click before we submit, so the default is never used silently.
      defaultValues: { requestedRole: "User" }
    });

  // Step 1: validate the visible fields only, then advance to the role picker.
  async function continueToRoles() {
    const ok = await trigger(["displayName", "email", "password", "phoneNumber"]);
    if (ok) setStep("role");
  }

  // Step 2: a card click commits the role and submits everything.
  async function pickRole(role: SelfRegisterRole) {
    setValue("requestedRole", role, { shouldValidate: true });
    await handleSubmit(onSubmit)();
  }

  async function onSubmit(v: RegisterInput) {
    setSubmitting(true);
    try {
      const result = await register(v.email, v.password, v.displayName, v.phoneNumber || undefined, v.requestedRole);
      setSubmittedEmail(result.email);
      setStep("done");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Registration failed");
    } finally {
      setSubmitting(false);
    }
  }

  const heading = step === "done"
    ? "Request received"
    : step === "role"
      ? "Tell us who you are"
      : "Create your account";

  const subheading = step === "done"
    ? "Every new account is reviewed by an administrator before you can sign in."
    : step === "role"
      ? "Pick the role that best describes how you'll use Pawzaroo. An administrator will confirm it during approval."
      : "Every new account is approved by an administrator before sign-in.";

  return (
    <div className="relative min-h-screen flex items-center justify-center overflow-hidden p-4 bg-gradient-to-b from-primary/5 to-background">
      <AuthPawTrail />
      <div className="absolute top-4 right-4 z-10"><ThemeToggle /></div>
      <Card className={cn("relative z-10 w-full", step === "role" ? "max-w-2xl" : "max-w-md")} data-auth-card="true">
        <CardHeader className="space-y-1">
          <Link to="/" className="flex items-center gap-2 text-primary font-bold">
            <PawPrint className="h-5 w-5" /> Pawzaroo
          </Link>
          <CardTitle>{heading}</CardTitle>
          <p className="text-sm text-muted-foreground">{subheading}</p>
        </CardHeader>
        <CardContent>
          {step === "details" && (
            <DetailsForm
              register={r}
              errors={errors}
              onContinue={continueToRoles}
            />
          )}
          {step === "role" && (
            <RolePicker
              email={getValues("email")}
              submitting={submitting}
              onBack={() => setStep("details")}
              onPick={pickRole}
            />
          )}
          {step === "done" && submittedEmail && <PendingScreen email={submittedEmail} />}
        </CardContent>
      </Card>
    </div>
  );
}

function DetailsForm({
  register, errors, onContinue
}: {
  register: ReturnType<typeof useForm<RegisterInput>>["register"];
  errors: ReturnType<typeof useForm<RegisterInput>>["formState"]["errors"];
  onContinue: () => Promise<void>;
}) {
  return (
    <form
      onSubmit={(e) => { e.preventDefault(); void onContinue(); }}
      className="space-y-3"
    >
      <div>
        <Label htmlFor="name">Display name</Label>
        <Input id="name" {...register("displayName")} />
        {errors.displayName && <p className="text-xs text-destructive mt-1">{errors.displayName.message}</p>}
      </div>
      <div>
        <Label htmlFor="email">Email</Label>
        <Input id="email" type="email" autoComplete="email" {...register("email")} />
        {errors.email && <p className="text-xs text-destructive mt-1">{errors.email.message}</p>}
      </div>
      <div>
        <Label htmlFor="password">Password</Label>
        <PasswordInput id="password" autoComplete="new-password" {...register("password")} />
        {errors.password && <p className="text-xs text-destructive mt-1">{errors.password.message}</p>}
      </div>
      <div>
        <Label htmlFor="phone">Phone (optional)</Label>
        <Input id="phone" {...register("phoneNumber")} />
      </div>
      <Button type="submit" className="w-full">
        Continue <ArrowRight className="h-4 w-4 ml-1" />
      </Button>
      <p className="text-sm text-muted-foreground text-center">
        Already have an account? <Link className="underline" to="/login">Sign in</Link>
      </p>
    </form>
  );
}

function RolePicker({
  email, submitting, onBack, onPick
}: {
  email?: string;
  submitting: boolean;
  onBack: () => void;
  onPick: (role: SelfRegisterRole) => Promise<void>;
}) {
  // Track which card is mid-submit so we can disable just that one and show a
  // localised spinner state instead of dimming the whole grid.
  const [activeRole, setActiveRole] = useState<SelfRegisterRole | null>(null);

  async function choose(role: SelfRegisterRole) {
    if (submitting) return;
    setActiveRole(role);
    try { await onPick(role); }
    finally { setActiveRole(null); }
  }

  return (
    <div className="space-y-4">
      {email && (
        <p className="text-xs text-muted-foreground">
          You're signing up as <span className="font-medium text-foreground">{email}</span>.
        </p>
      )}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        {selfRegisterRoles.map((opt) => {
          const Icon = ROLE_ICON[opt.value as SelfRegisterRole];
          const busy = activeRole === opt.value;
          return (
            <button
              key={opt.value}
              type="button"
              disabled={submitting}
              onClick={() => choose(opt.value as SelfRegisterRole)}
              className={cn(
                "group text-left rounded-lg border p-3 transition-all",
                "hover:border-primary hover:bg-primary/5 hover:shadow-sm",
                "focus:outline-none focus-visible:ring-2 focus-visible:ring-primary",
                "disabled:opacity-60 disabled:cursor-not-allowed",
                busy && "border-primary bg-primary/5"
              )}
            >
              <div className="flex items-start gap-3">
                <div className="h-10 w-10 rounded-md bg-primary/10 text-primary flex items-center justify-center shrink-0 group-hover:bg-primary group-hover:text-primary-foreground transition-colors">
                  <Icon className="h-5 w-5" />
                </div>
                <div className="min-w-0 space-y-0.5">
                  <p className="text-sm font-semibold">{opt.label}</p>
                  <p className="text-xs text-muted-foreground leading-snug">{opt.hint}</p>
                </div>
              </div>
              {busy && (
                <p className="text-[10px] text-primary mt-2 font-medium">Submitting…</p>
              )}
            </button>
          );
        })}
      </div>
      <Button variant="ghost" size="sm" onClick={onBack} disabled={submitting} className="w-full">
        <ArrowLeft className="h-4 w-4 mr-1" /> Back
      </Button>
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

function Step({ icon: Icon, title, body }: { icon: LucideIcon; title: string; body: React.ReactNode }) {
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
