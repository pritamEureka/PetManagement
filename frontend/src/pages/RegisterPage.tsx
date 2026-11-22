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
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ThemeToggle } from "@/components/theme/ThemeToggle";
import { AuthPawTrail } from "@/components/auth/AuthPawTrail";
import { registerSchema, selfRegisterRoles, type RegisterInput, type SelfRegisterRole } from "@/lib/schemas";
import { toast } from "@/components/ui/sonner";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { FormField } from "@/components/common/a11y";

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
  const { t } = useI18n();
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
      toast.error(err?.response?.data?.error?.message ?? t("register.failed"));
    } finally {
      setSubmitting(false);
    }
  }

  const heading = step === "done"
    ? t("register.doneTitle")
    : step === "role"
      ? t("register.roleTitle")
      : t("register.title");

  const subheading = step === "done"
    ? t("register.doneSub")
    : step === "role"
      ? t("register.roleSub")
      : t("register.detailsSub");

  return (
    <div className="relative min-h-screen flex items-center justify-center overflow-hidden p-4 bg-gradient-to-b from-primary/5 to-background">
      <AuthPawTrail />
      <div className="absolute top-4 end-4 z-10"><ThemeToggle /></div>
      <Card className={cn("relative z-10 w-full", step === "role" ? "max-w-2xl" : "max-w-md")} data-auth-card="true">
        <CardHeader className="space-y-1">
          <Link to="/" className="flex items-center gap-2 text-primary font-bold">
            <PawPrint aria-hidden="true" className="h-5 w-5" /> Pawzaroo
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
  const { t } = useI18n();
  return (
    <form
      onSubmit={(e) => { e.preventDefault(); void onContinue(); }}
      className="space-y-3"
    >
      <FormField id="name" label={t("register.displayName")} error={errors.displayName?.message}>
        {(c) => <Input {...c} {...register("displayName")} />}
      </FormField>
      <FormField id="email" label={t("auth.email")} error={errors.email?.message}>
        {(c) => <Input {...c} type="email" autoComplete="email" {...register("email")} />}
      </FormField>
      <FormField id="password" label={t("auth.password")} error={errors.password?.message}>
        {(c) => <PasswordInput {...c} autoComplete="new-password" {...register("password")} />}
      </FormField>
      <FormField id="phone" label={t("register.phoneOptional")}>
        {(c) => <Input {...c} type="tel" autoComplete="tel" {...register("phoneNumber")} />}
      </FormField>
      <Button type="submit" className="w-full">
        {t("onboarding.continue")} <ArrowRight aria-hidden="true" className="h-4 w-4 ms-1 rtl:rotate-180" />
      </Button>
      <p className="text-sm text-muted-foreground text-center">
        {t("register.haveAccount")} <Link className="underline" to="/login">{t("landing.signIn")}</Link>
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
  const { t } = useI18n();
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
          {t("register.signingUpAs")} <span className="font-medium text-foreground">{email}</span>.
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
                "group text-start rounded-lg border p-3 transition-all",
                "hover:border-primary hover:bg-muted hover:shadow-sm",
                "focus:outline-none focus-visible:ring-2 focus-visible:ring-primary",
                "disabled:opacity-60 disabled:cursor-not-allowed",
                busy && "border-primary bg-primary/5 hover:bg-primary/10"
              )}
            >
              <div className="flex items-start gap-3">
                <div className="h-10 w-10 rounded-md bg-primary/10 text-primary flex items-center justify-center shrink-0 group-hover:bg-primary/15 transition-colors">
                  <Icon aria-hidden="true" className="h-5 w-5" />
                </div>
                <div className="min-w-0 space-y-0.5">
                  <p className="text-sm font-semibold">{t(`register.role.${opt.value}.label`)}</p>
                  <p className="text-xs text-muted-foreground leading-snug">{t(`register.role.${opt.value}.hint`)}</p>
                </div>
              </div>
              {busy && (
                <p role="status" className="text-[10px] text-primary mt-2 font-medium">{t("register.submitting")}</p>
              )}
            </button>
          );
        })}
      </div>
      <Button variant="ghost" size="sm" onClick={onBack} disabled={submitting} className="w-full">
        <ArrowLeft aria-hidden="true" className="h-4 w-4 me-1 rtl:rotate-180" /> {t("common.back")}
      </Button>
    </div>
  );
}

function PendingScreen({ email }: { email: string }) {
  const { t } = useI18n();
  return (
    <div className="space-y-5">
      <Step icon={MailCheck} title={t("register.step1")}
        body={<>{t("register.step1Pre")} <span className="font-medium">{email}</span> {t("register.step1Post")}</>} />
      <Step icon={Hourglass} title={t("register.step2")}
        body={t("register.step2Body")} />
      <Step icon={ShieldCheck} title={t("register.step3")}
        body={t("register.step3Body")} />
      <div className="pt-2">
        <Button asChild className="w-full"><Link to="/login">{t("register.backToSignIn")}</Link></Button>
      </div>
    </div>
  );
}

function Step({ icon: Icon, title, body }: { icon: LucideIcon; title: string; body: React.ReactNode }) {
  return (
    <div className="flex gap-3">
      <div className="h-8 w-8 rounded-full bg-primary/10 text-primary flex items-center justify-center shrink-0">
        <Icon aria-hidden="true" className="h-4 w-4" />
      </div>
      <div className="space-y-0.5">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-xs text-muted-foreground">{body}</p>
      </div>
    </div>
  );
}
