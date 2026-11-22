import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Phone, Siren, MapPin, LocateFixed, Video } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { emergencyApi } from "@/api/clinicPlus";

export function EmergencyVetsPage() {
  const [city, setCity] = useState("");
  const [coords, setCoords] = useState<{ lat: number; lng: number } | null>(null);
  const [geoError, setGeoError] = useState<string | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["vets", "emergency", city, coords?.lat, coords?.lng],
    queryFn: () => emergencyApi.search({ city: city || undefined, lat: coords?.lat, lng: coords?.lng })
  });

  function locate() {
    setGeoError(null);
    if (!navigator.geolocation) { setGeoError("Location is not available in this browser."); return; }
    navigator.geolocation.getCurrentPosition(
      (p) => setCoords({ lat: p.coords.latitude, lng: p.coords.longitude }),
      () => setGeoError("Couldn't get your location. Enter your city instead."),
      { timeout: 8000 }
    );
  }

  return (
    <div className="space-y-4 max-w-3xl mx-auto">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-2"><Siren className="h-6 w-6 text-destructive" /> Emergency and 24/7 vets</h1>
        <p className="text-sm text-muted-foreground">24/7 clinics are listed first, then the nearest.</p>
      </div>

      <div role="note" className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm">
        <strong>If your pet is in immediate danger, call a clinic now.</strong> This directory is not a substitute for
        professional veterinary diagnosis or care. Availability is provided by the clinics themselves, so confirm by phone before travelling.
      </div>

      <Card>
        <CardContent className="pt-6 flex flex-wrap gap-3 items-center">
          <Input className="flex-1 min-w-[160px]" placeholder="Your city" value={city} onChange={(e) => setCity(e.target.value)} aria-label="City" />
          <Button type="button" variant="outline" onClick={locate}><LocateFixed className="h-4 w-4 mr-1" /> Use my location</Button>
          {coords && <Badge variant="secondary">Sorted by distance</Badge>}
          {geoError && <p className="w-full text-xs text-destructive">{geoError}</p>}
        </CardContent>
      </Card>

      {isLoading ? (
        <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-28" />)}</div>
      ) : !data || data.length === 0 ? (
        <p className="text-sm text-muted-foreground">No emergency clinics are listed yet for this search. Try another city, or <Link className="underline" to="/vets?online=true">see online vets</Link>.</p>
      ) : (
        <div className="space-y-3">
          {data.map((v) => (
            <Card key={v.doctorId}>
              <CardContent className="py-4 flex flex-col sm:flex-row sm:items-center gap-3">
                <div className="flex-1 min-w-0 space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-semibold truncate">{v.clinicName ?? `Dr. ${v.doctorName}`}</p>
                    {v.is24x7 && <Badge>24/7</Badge>}
                    {v.acceptsWalkInEmergency && <Badge variant="secondary">Walk-ins</Badge>}
                    {v.isOnline && <Badge variant="outline"><Video className="h-3 w-3 mr-1" />Online</Badge>}
                  </div>
                  <p className="text-xs text-muted-foreground flex items-center gap-1">
                    <MapPin className="h-3 w-3" /> {[v.clinicAddress, v.city].filter(Boolean).join(", ") || "Address not provided"}
                    {v.distanceKm != null && ` · ${v.distanceKm} km away`}
                  </p>
                  <Link to={`/vets/${v.doctorId}`} className="text-xs underline text-muted-foreground">Dr. {v.doctorName}</Link>
                </div>
                {v.telUri ? (
                  <Button asChild size="lg" className="bg-destructive text-destructive-foreground hover:bg-destructive/90 w-full sm:w-auto">
                    <a href={v.telUri} aria-label={`Call ${v.clinicName ?? v.doctorName} now`}><Phone className="h-5 w-5 mr-2" /> Call now{v.emergencyPhone ? ` · ${v.emergencyPhone}` : ""}</a>
                  </Button>
                ) : <span className="text-xs text-muted-foreground">No phone listed</span>}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
