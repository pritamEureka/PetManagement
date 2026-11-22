import { Link } from "react-router-dom";
import { PawPrint, HeartHandshake, Stethoscope, ShoppingBag, MessageSquare, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { useI18n } from "@/lib/i18n";

const FEATURES = [
  { icon: HeartHandshake, key: "landing.f1" },
  { icon: Stethoscope,    key: "landing.f2" },
  { icon: MessageSquare,  key: "landing.f3" },
  { icon: ShoppingBag,    key: "landing.f4" },
  { icon: PawPrint,       key: "landing.f5" },
  { icon: ShieldCheck,    key: "landing.f6" }
];

export function LandingPage() {
  const { t } = useI18n();
  return (
    <>
      {/* Hero */}
      <section className="relative overflow-hidden" aria-labelledby="landing-hero-title">
        <div className="container py-20 md:py-28 grid md:grid-cols-2 gap-10 items-center">
          <div className="space-y-6">
            <span className="inline-flex items-center gap-2 rounded-full bg-primary/10 text-primary px-3 py-1 text-xs font-semibold">
              <PawPrint aria-hidden="true" className="h-3.5 w-3.5" /> {t("landing.badge")}
            </span>
            <h1 id="landing-hero-title" className="text-4xl md:text-6xl font-bold tracking-tight">
              {t("landing.heroPre")} <span className="text-primary">{t("landing.heroHighlight")}</span>{t("landing.heroEnd")}
            </h1>
            <p className="text-lg text-muted-foreground max-w-prose">
              {t("landing.heroBody")}
            </p>
            <div className="flex flex-wrap gap-3">
              <Button size="lg" asChild><Link to="/register">{t("landing.createAccount")}</Link></Button>
              <Button size="lg" variant="outline" asChild><Link to="/login">{t("landing.signIn")}</Link></Button>
            </div>
            <p className="text-xs text-muted-foreground">{t("landing.freeNote")}</p>
          </div>
          <div className="relative">
            <div className="aspect-square rounded-3xl bg-gradient-to-br from-primary/20 via-primary/5 to-transparent border flex items-center justify-center">
              <PawPrint aria-hidden="true" className="h-40 w-40 text-primary/60" />
            </div>
          </div>
        </div>
      </section>

      {/* Features */}
      <section id="features" aria-labelledby="landing-features-title" className="container py-16 md:py-24 space-y-12">
        <div className="text-center max-w-2xl mx-auto space-y-2">
          <h2 id="landing-features-title" className="text-3xl md:text-4xl font-bold">{t("landing.featuresTitle")}</h2>
          <p className="text-muted-foreground">{t("landing.featuresSub")}</p>
        </div>
        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
          {FEATURES.map(({ icon: Icon, key }) => (
            <Card key={key} className="hover:shadow-md transition-shadow">
              <CardContent className="pt-6 space-y-3">
                <div className="h-10 w-10 rounded-lg bg-primary/10 flex items-center justify-center">
                  <Icon aria-hidden="true" className="h-5 w-5 text-primary" />
                </div>
                <h3 className="font-semibold text-lg">{t(`${key}.title`)}</h3>
                <p className="text-sm text-muted-foreground">{t(`${key}.body`)}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>

      {/* CTA */}
      <section className="container pb-24">
        <div className="rounded-3xl bg-primary text-primary-foreground p-10 md:p-16 text-center space-y-4">
          <h3 className="text-3xl md:text-4xl font-bold">{t("landing.ctaTitle")}</h3>
          <p className="opacity-90">{t("landing.ctaBody")}</p>
          <Button size="lg" variant="secondary" asChild>
            <Link to="/register">{t("landing.ctaButton")}</Link>
          </Button>
        </div>
      </section>
    </>
  );
}
