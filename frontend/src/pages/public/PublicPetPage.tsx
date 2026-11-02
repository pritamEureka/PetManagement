import { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, CheckCircle2, MapPin, PawPrint } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { useAuthStore } from "@/store/authStore";
import { publicPetsApi } from "@/api/pets";
import { Field } from "@/pages/pets/detail/shared";

/**
 * Landing page behind a pet's QR tag. Anonymous — shows only owner-approved
 * fields and relays a "I found your pet" message without exposing contact info.
 */
export function PublicPetPage() {
  const { code = "" } = useParams();
  const signedIn = useAuthStore((s) => !!s.accessToken);
  const { data: pet, isLoading, isError } = useQuery({ queryKey: ["public-pet", code], queryFn: () => publicPetsApi.get(code), retry: false });
  const [f, setF] = useState({ message: "", contactInfo: "" });
  const [coords, setCoords] = useState<{ latitude: number; longitude: number } | null>(null);
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  function shareLocation() {
    if (!navigator.geolocation) return toast.error("Location isn't available in this browser.");
    navigator.geolocation.getCurrentPosition(
      (p) => { setCoords({ latitude: p.coords.latitude, longitude: p.coords.longitude }); toast.success("Location added."); },
      () => toast.error("Couldn't get your location."),
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }

  async function send(e: React.FormEvent) {
    e.preventDefault();
    if (!f.message.trim() && !f.contactInfo.trim() && !coords) return toast.error("Add a message, your contact or your location.");
    setBusy(true);
    try {
      await publicPetsApi.sighting(code, { message: f.message.trim() || undefined, contactInfo: f.contactInfo.trim() || undefined, ...coords });
      setSent(true);
    } catch (err: any) {
      toast.error(err?.response?.status === 429 ? "Too many messages — please wait a minute." : "Couldn't send. Please try again.");
    } finally { setBusy(false); }
  }

  if (isLoading) return <div className="max-w-md mx-auto p-4"><Skeleton className="h-96" /></div>;
  if (isError || !pet) {
    return (
      <div className="max-w-md mx-auto p-6 text-center space-y-2">
        <PawPrint className="h-10 w-10 mx-auto text-muted-foreground" />
        <p className="font-medium">This tag isn't active.</p>
        <p className="text-sm text-muted-foreground">The owner may have turned off their pet's public profile.</p>
      </div>
    );
  }

  return (
    <div className="max-w-md mx-auto p-4 space-y-4">
      <Card className="overflow-hidden">
        <div className="aspect-square bg-muted">
          {pet.primaryPhotoUrl
            ? <img src={pet.primaryPhotoUrl} alt={pet.name} className="h-full w-full object-cover" />
            : <div className="h-full flex items-center justify-center"><PawPrint className="h-20 w-20 text-muted-foreground" /></div>}
        </div>
        <CardContent className="pt-5 space-y-2 text-center">
          {pet.isLost && <Badge variant="destructive" className="text-sm"><AlertTriangle className="h-3.5 w-3.5 mr-1" /> I'm lost — please help me get home</Badge>}
          <h1 className="text-3xl font-bold">Hi, I'm {pet.name}!</h1>
          <p className="text-muted-foreground">
            {pet.animalType}{pet.breed ? ` · ${pet.breed}` : ""}{pet.color ? ` · ${pet.color}` : ""}{pet.gender !== "Unknown" ? ` · ${pet.gender}` : ""}
          </p>
          <p className="text-sm">My human is <strong>{pet.ownerFirstName}</strong>.</p>
          {pet.allergies && (
            <p className="text-sm rounded-md bg-amber-100 dark:bg-amber-950 text-amber-900 dark:text-amber-100 px-3 py-2">
              ⚠️ Allergies: {pet.allergies}
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardContent className="pt-5">
          {sent ? (
            <div className="text-center space-y-2 py-4">
              <CheckCircle2 className="h-10 w-10 text-emerald-600 mx-auto" />
              <p className="font-medium">Thank you! {pet.ownerFirstName} has been notified.</p>
              <p className="text-sm text-muted-foreground">If you can, keep {pet.name} somewhere safe until they reach you.</p>
            </div>
          ) : (
            <form onSubmit={send} className="space-y-3">
              <p className="font-medium">Found {pet.name}? Let {pet.ownerFirstName} know</p>
              <Field label="Message" htmlFor="sm">
                <Textarea id="sm" rows={3} maxLength={1000} placeholder={`I found ${pet.name} near…`} value={f.message} onChange={(e) => setF({ ...f, message: e.target.value })} />
              </Field>
              <Field label="How can they reach you? (optional)" htmlFor="sc">
                <Input id="sc" maxLength={200} placeholder="Phone or email" value={f.contactInfo} onChange={(e) => setF({ ...f, contactInfo: e.target.value })} />
              </Field>
              <Button type="button" variant="outline" size="sm" onClick={shareLocation}>
                <MapPin className="h-3.5 w-3.5 mr-1" /> {coords ? "Location added" : "Share my location"}
              </Button>
              <Button type="submit" className="w-full" disabled={busy}>{busy ? "Sending..." : `Notify ${pet.ownerFirstName}`}</Button>
              <p className="text-xs text-muted-foreground text-center">Your details go only to the owner.</p>
            </form>
          )}
        </CardContent>
      </Card>

      {pet.openLostReportId && signedIn && (
        <p className="text-center text-sm"><Link className="underline" to={`/lost-found/${pet.openLostReportId}`}>See the lost report</Link></p>
      )}
      <p className="text-center text-xs text-muted-foreground">Powered by <Link to="/" className="underline">Pawzaroo</Link></p>
    </div>
  );
}
