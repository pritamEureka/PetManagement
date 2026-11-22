import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { PawPrint, Stethoscope, ShoppingBag, HeartHandshake, Sparkles } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/sonner";
import { petsApi } from "@/api/pets";
import { useI18n } from "@/lib/i18n";
import { animalTypes } from "@/lib/schemas";
import { useDraft } from "@/hooks/useDraft";
import { DraftBanner } from "@/components/common/DraftBanner";

type Intent = "owner" | "vet" | "store" | "shelter";

const INTENTS: { id: Intent; icon: typeof PawPrint }[] = [
  { id: "owner",   icon: PawPrint },
  { id: "vet",     icon: Stethoscope },
  { id: "store",   icon: ShoppingBag },
  { id: "shelter", icon: HeartHandshake }
];

export function OnboardingPage() {
  const nav = useNavigate();
  const { t } = useI18n();
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [intent, setIntent] = useState<Intent>("owner");
  const [pet, setPet] = useState({ name: "", animalType: "Dog", breed: "" });
  const [busy, setBusy] = useState(false);
  const draftValue = { step, intent, pet };
  const draft = useDraft("onboarding", draftValue, { enabled: !busy });

  async function finish(skipPet = false) {
    setBusy(true);
    try {
      if (!skipPet && intent === "owner" && pet.name.trim()) {
        await petsApi.create({
          name: pet.name.trim(),
          animalType: pet.animalType as any,
          breed: pet.breed || "",
          gender: "Unknown",
          isAvailableForAdoption: false
        });
        toast.success(`${t("onboarding.welcomePet", { name: pet.name })} 🐾`);
      }
      draft.clear();
      nav("/home", { replace: true });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? t("onboarding.saveError"));
    } finally { setBusy(false); }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-b from-primary/5 to-background">
      <div className="w-full max-w-2xl space-y-4">
        <DraftBanner<typeof draftValue>
          draft={draft}
          onRestore={(d) => {
            if (d.intent) setIntent(d.intent);
            if (d.pet) setPet(d.pet);
            if (d.step) setStep(d.step);
          }}
        />
        <div className="flex items-center gap-2 justify-center text-muted-foreground text-sm">
          {[1, 2, 3].map((n) => (
            <Badge key={n} variant={step === n ? "default" : step > n ? "muted" : "outline"}>
              {t("onboarding.step", { n })}
            </Badge>
          ))}
        </div>

        {step === 1 && (
          <Card>
            <CardContent className="pt-6 space-y-6">
              <div className="text-center space-y-1">
                <h1 className="text-2xl font-bold">{t("onboarding.welcome")}</h1>
                <p className="text-sm text-muted-foreground">{t("onboarding.welcomeSub")}</p>
              </div>
              <div className="grid sm:grid-cols-2 gap-3">
                {INTENTS.map(({ id, icon: Icon }) => (
                  <button
                    key={id}
                    type="button"
                    aria-pressed={intent === id}
                    onClick={() => setIntent(id)}
                    className={`text-start rounded-lg border p-4 transition-colors ${intent === id ? "border-primary bg-primary/5 ring-1 ring-primary hover:bg-primary/10" : "hover:bg-muted"}`}
                  >
                    <Icon aria-hidden="true" className="h-5 w-5 text-primary mb-2" />
                    <p className="font-semibold">{t(`onboarding.intent.${id}.title`)}</p>
                    <p className="text-xs text-muted-foreground">{t(`onboarding.intent.${id}.body`)}</p>
                  </button>
                ))}
              </div>
              <div className="flex justify-end gap-2">
                <Button onClick={() => setStep(2)}>{t("onboarding.continue")}</Button>
              </div>
            </CardContent>
          </Card>
        )}

        {step === 2 && (
          <Card>
            <CardContent className="pt-6 space-y-4">
              <div className="text-center space-y-1">
                <h1 className="text-2xl font-bold">
                  {intent === "owner" ? t("onboarding.addFirstPet") : t("onboarding.almostThere")}
                </h1>
                <p className="text-sm text-muted-foreground">
                  {intent === "owner"
                    ? t("onboarding.addMoreLater")
                    : t("onboarding.reviewTime")}
                </p>
              </div>
              {intent === "owner" ? (
                <div className="space-y-3">
                  <div>
                    <Label htmlFor="pname">{t("onboarding.petName")}</Label>
                    <Input id="pname" value={pet.name} onChange={(e) => setPet({ ...pet, name: e.target.value })} />
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label htmlFor="ptype">{t("onboarding.type")}</Label>
                      <Select value={pet.animalType} onValueChange={(v) => setPet({ ...pet, animalType: v })}>
                        <SelectTrigger id="ptype"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {animalTypes.map((a) => <SelectItem key={a} value={a}>{a}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div>
                      <Label htmlFor="pbreed">{t("onboarding.breed")}</Label>
                      <Input id="pbreed" value={pet.breed} onChange={(e) => setPet({ ...pet, breed: e.target.value })} />
                    </div>
                  </div>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground text-center py-4">
                  {t("onboarding.redirectNote")}
                </p>
              )}
              <div className="flex justify-between">
                <Button variant="ghost" onClick={() => setStep(1)}>{t("common.back")}</Button>
                <div className="flex gap-2">
                  {intent === "owner" && <Button variant="outline" onClick={() => setStep(3)}>{t("onboarding.skip")}</Button>}
                  <Button onClick={() => setStep(3)} disabled={intent === "owner" && !pet.name.trim()}>{t("onboarding.continue")}</Button>
                </div>
              </div>
            </CardContent>
          </Card>
        )}

        {step === 3 && (
          <Card>
            <CardContent className="pt-6 space-y-6 text-center">
              <Sparkles aria-hidden="true" className="h-12 w-12 mx-auto text-primary" />
              <h1 className="text-2xl font-bold">{t("onboarding.allSet")}</h1>
              <p className="text-sm text-muted-foreground">
                {t("onboarding.allSetSub")}
              </p>
              <Button size="lg" onClick={() => finish(intent !== "owner" || !pet.name.trim())} disabled={busy}>
                {busy ? t("onboarding.settingUp") : t("onboarding.goDashboard")}
              </Button>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
