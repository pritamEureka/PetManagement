import { useEffect, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Crosshair, MapPin } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "@/components/ui/sonner";
import { useGeolocation } from "@/hooks/useGeolocation";
import { discoveryApi } from "@/api/shopPlus";

/** Lets a store owner set the store's coordinates so "near me" discovery can rank it. */
export function StoreLocationCard() {
  const geo = useGeolocation();
  const [lat, setLat] = useState("");
  const [lng, setLng] = useState("");

  useEffect(() => {
    if (geo.coords) {
      setLat(geo.coords.lat.toFixed(6));
      setLng(geo.coords.lng.toFixed(6));
    }
  }, [geo.coords]);

  const latN = Number(lat);
  const lngN = Number(lng);
  const filled = lat.trim() !== "" && lng.trim() !== "";
  const valid = filled
    && Number.isFinite(latN) && latN >= -90 && latN <= 90
    && Number.isFinite(lngN) && lngN >= -180 && lngN <= 180;

  const save = useMutation({
    mutationFn: () => discoveryApi.setStoreLocation(latN, lngN),
    onSuccess: () => toast.success("Store location saved"),
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not save location."),
  });

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base flex items-center gap-2"><MapPin className="h-4 w-4" /> Store location</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-xs text-muted-foreground">Buyers can sort products by distance from your store.</p>
        <div className="grid sm:grid-cols-[1fr_1fr_auto_auto] gap-2 items-end">
          <div>
            <Label htmlFor="store-lat">Latitude</Label>
            <Input id="store-lat" inputMode="decimal" placeholder="-90 to 90" value={lat} onChange={(e) => setLat(e.target.value)} />
          </div>
          <div>
            <Label htmlFor="store-lng">Longitude</Label>
            <Input id="store-lng" inputMode="decimal" placeholder="-180 to 180" value={lng} onChange={(e) => setLng(e.target.value)} />
          </div>
          <Button type="button" variant="outline" onClick={geo.request} disabled={geo.status === "locating"}>
            <Crosshair className="h-4 w-4 mr-1" /> {geo.status === "locating" ? "Locating..." : "Use my location"}
          </Button>
          <Button type="button" disabled={!valid || save.isPending} onClick={() => save.mutate()}>
            {save.isPending ? "Saving..." : "Save"}
          </Button>
        </div>
        {geo.status === "denied" && <p className="text-xs text-destructive">Location permission denied. Enter coordinates manually.</p>}
        {filled && !valid && <p className="text-xs text-destructive">Enter a valid latitude and longitude.</p>}
      </CardContent>
    </Card>
  );
}
