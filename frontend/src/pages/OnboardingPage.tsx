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
import { animalTypes } from "@/lib/schemas";

type Intent = "owner" | "vet" | "store" | "shelter";

const INTENTS: { id: Intent; icon: typeof PawPrint; title: string; body: string }[] = [
  { id: "owner",   icon: PawPrint,       title: "I'm a pet parent",      body: "Track records, find vets, adopt." },
  { id: "vet",     icon: Stethoscope,    title: "I'm a veterinarian",    body: "Manage clinic & appointments." },
  { id: "store",   icon: ShoppingBag,    title: "I sell pet products",   body: "Open a marketplace store." },
  { id: "shelter", icon: HeartHandshake, title: "I run a shelter / center", body: "List pets for adoption." }
];

export function OnboardingPage() {
  const nav = useNavigate();
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [intent, setIntent] = useState<Intent>("owner");
  const [pet, setPet] = useState({ name: "", animalType: "Dog", breed: "" });
  const [busy, setBusy] = useState(false);

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
        toast.success(`Welcome ${pet.name}! 🐾`);
      }
      nav("/home", { replace: true });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't save pet.");
    } finally { setBusy(false); }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-gradient-to-b from-primary/5 to-background">
      <div className="w-full max-w-2xl space-y-4">
        <div className="flex items-center gap-2 justify-center text-muted-foreground text-sm">
          {[1, 2, 3].map((n) => (
            <Badge key={n} variant={step === n ? "default" : step > n ? "muted" : "outline"}>
              Step {n}
            </Badge>
          ))}
        </div>

        {step === 1 && (
          <Card>
            <CardContent className="pt-6 space-y-6">
              <div className="text-center space-y-1">
                <h1 className="text-2xl font-bold">Welcome to Pawzaroo!</h1>
                <p className="text-sm text-muted-foreground">Tell us how you'd like to use the platform.</p>
              </div>
              <div className="grid sm:grid-cols-2 gap-3">
                {INTENTS.map(({ id, icon: Icon, title, body }) => (
                  <button
                    key={id}
                    onClick={() => setIntent(id)}
                    className={`text-left rounded-lg border p-4 transition-colors ${intent === id ? "border-primary bg-primary/5 ring-1 ring-primary hover:bg-primary/10" : "hover:bg-muted"}`}
                  >
                    <Icon className="h-5 w-5 text-primary mb-2" />
                    <p className="font-semibold">{title}</p>
                    <p className="text-xs text-muted-foreground">{body}</p>
                  </button>
                ))}
              </div>
              <div className="flex justify-end gap-2">
                <Button onClick={() => setStep(2)}>Continue</Button>
              </div>
            </CardContent>
          </Card>
        )}

        {step === 2 && (
          <Card>
            <CardContent className="pt-6 space-y-4">
              <div className="text-center space-y-1">
                <h1 className="text-2xl font-bold">
                  {intent === "owner" ? "Add your first pet" : "Almost there"}
                </h1>
                <p className="text-sm text-muted-foreground">
                  {intent === "owner"
                    ? "You can add more later. Skip if you'd rather explore first."
                    : "Application reviews take 24–72h. We'll email you when you're approved."}
                </p>
              </div>
              {intent === "owner" ? (
                <div className="space-y-3">
                  <div>
                    <Label htmlFor="pname">Pet name</Label>
                    <Input id="pname" value={pet.name} onChange={(e) => setPet({ ...pet, name: e.target.value })} />
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label htmlFor="ptype">Type</Label>
                      <Select value={pet.animalType} onValueChange={(v) => setPet({ ...pet, animalType: v })}>
                        <SelectTrigger id="ptype"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div>
                      <Label htmlFor="pbreed">Breed</Label>
                      <Input id="pbreed" value={pet.breed} onChange={(e) => setPet({ ...pet, breed: e.target.value })} />
                    </div>
                  </div>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground text-center py-4">
                  We'll redirect you to your dashboard where you can submit your application.
                </p>
              )}
              <div className="flex justify-between">
                <Button variant="ghost" onClick={() => setStep(1)}>Back</Button>
                <div className="flex gap-2">
                  {intent === "owner" && <Button variant="outline" onClick={() => setStep(3)}>Skip</Button>}
                  <Button onClick={() => setStep(3)} disabled={intent === "owner" && !pet.name.trim()}>Continue</Button>
                </div>
              </div>
            </CardContent>
          </Card>
        )}

        {step === 3 && (
          <Card>
            <CardContent className="pt-6 space-y-6 text-center">
              <Sparkles className="h-12 w-12 mx-auto text-primary" />
              <h1 className="text-2xl font-bold">You're all set!</h1>
              <p className="text-sm text-muted-foreground">
                Jump in and start exploring. You can change everything from settings later.
              </p>
              <Button size="lg" onClick={() => finish(intent !== "owner" || !pet.name.trim())} disabled={busy}>
                {busy ? "Setting things up..." : "Go to my dashboard"}
              </Button>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
}
