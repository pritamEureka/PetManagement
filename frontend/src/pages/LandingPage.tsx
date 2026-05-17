import { Link } from "react-router-dom";
import { PawPrint, HeartHandshake, Stethoscope, ShoppingBag, MessageSquare, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

const FEATURES = [
  { icon: HeartHandshake, title: "Adopt with confidence",
    body: "Verified shelters, breeders, and pet owners. Every listing reviewed before it goes live." },
  { icon: Stethoscope,    title: "Book vets in seconds",
    body: "Search by animal type, specialty, online or in-clinic. Real-time slot booking." },
  { icon: MessageSquare,  title: "Real-time chat",
    body: "Talk to vets, shelters, and sellers without leaving the app. Read receipts + media." },
  { icon: ShoppingBag,    title: "Multi-vendor marketplace",
    body: "Food, accessories, medicine, livestock supplies — all from approved local stores." },
  { icon: PawPrint,       title: "Digital pet health records",
    body: "Vaccinations, prescriptions, vet visits — one tap to share with your next vet." },
  { icon: ShieldCheck,    title: "Privacy by design",
    body: "Dynamic role-based access. Your records stay yours. Block, report, control." }
];

export function LandingPage() {
  return (
    <>
      {/* Hero */}
      <section className="relative overflow-hidden">
        <div className="container py-20 md:py-28 grid md:grid-cols-2 gap-10 items-center">
          <div className="space-y-6">
            <span className="inline-flex items-center gap-2 rounded-full bg-primary/10 text-primary px-3 py-1 text-xs font-semibold">
              <PawPrint className="h-3.5 w-3.5" /> Pet life, organized
            </span>
            <h1 className="text-4xl md:text-6xl font-bold tracking-tight">
              The home for everything <span className="text-primary">your pets need</span>.
            </h1>
            <p className="text-lg text-muted-foreground max-w-prose">
              Pawzaroo is a single, friendly ecosystem for pet owners, vets, adoption centers, and sellers.
              Adopt, book, shop, chat — all in one place.
            </p>
            <div className="flex flex-wrap gap-3">
              <Button size="lg" asChild><Link to="/register">Create your account</Link></Button>
              <Button size="lg" variant="outline" asChild><Link to="/login">Sign in</Link></Button>
            </div>
            <p className="text-xs text-muted-foreground">Free for pet owners. Vets & stores apply for approval.</p>
          </div>
          <div className="relative">
            <div className="aspect-square rounded-3xl bg-gradient-to-br from-primary/20 via-primary/5 to-transparent border flex items-center justify-center">
              <PawPrint className="h-40 w-40 text-primary/60" />
            </div>
          </div>
        </div>
      </section>

      {/* Features */}
      <section id="features" className="container py-16 md:py-24 space-y-12">
        <div className="text-center max-w-2xl mx-auto space-y-2">
          <h2 className="text-3xl md:text-4xl font-bold">Everything pets, in one place</h2>
          <p className="text-muted-foreground">A modular platform that grows with your family.</p>
        </div>
        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
          {FEATURES.map(({ icon: Icon, title, body }) => (
            <Card key={title} className="hover:shadow-md transition-shadow">
              <CardContent className="pt-6 space-y-3">
                <div className="h-10 w-10 rounded-lg bg-primary/10 flex items-center justify-center">
                  <Icon className="h-5 w-5 text-primary" />
                </div>
                <h3 className="font-semibold text-lg">{title}</h3>
                <p className="text-sm text-muted-foreground">{body}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>

      {/* CTA */}
      <section className="container pb-24">
        <div className="rounded-3xl bg-primary text-primary-foreground p-10 md:p-16 text-center space-y-4">
          <h3 className="text-3xl md:text-4xl font-bold">Ready to give your pet the Pawzaroo treatment?</h3>
          <p className="opacity-90">Join thousands of pet parents already on the platform.</p>
          <Button size="lg" variant="secondary" asChild>
            <Link to="/register">Get started — it's free</Link>
          </Button>
        </div>
      </section>
    </>
  );
}
