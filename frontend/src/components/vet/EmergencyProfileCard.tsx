import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Siren } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "@/components/ui/sonner";
import { emergencyApi, type EmergencyProfileInput } from "@/api/clinicPlus";

const EMPTY: EmergencyProfileInput = {
  is24x7: false, acceptsWalkInEmergency: false, emergencyPhone: "", clinicName: "", clinicAddress: "", city: "", latitude: null, longitude: null
};

/** Vet self-service: emergency / 24-7 availability shown in the public emergency directory. */
export function EmergencyProfileCard() {
  const qc = useQueryClient();
  const { data } = useQuery({ queryKey: ["vets", "me", "emergency-profile"], queryFn: emergencyApi.mine });
  const [f, setF] = useState<EmergencyProfileInput>(EMPTY);
  useEffect(() => { if (data) setF({ ...EMPTY, ...data }); }, [data]);

  const save = useMutation({
    mutationFn: () => emergencyApi.save({
      ...f,
      emergencyPhone: f.emergencyPhone?.trim() || null,
      latitude: f.latitude === null || (f.latitude as unknown) === "" ? null : Number(f.latitude),
      longitude: f.longitude === null || (f.longitude as unknown) === "" ? null : Number(f.longitude)
    }),
    onSuccess: () => { toast.success("Emergency profile saved."); qc.invalidateQueries({ queryKey: ["vets", "me", "emergency-profile"] }); }
  });

  const set = <K extends keyof EmergencyProfileInput>(k: K, v: EmergencyProfileInput[K]) => setF((p) => ({ ...p, [k]: v }));

  return (
    <Card>
      <CardHeader><CardTitle className="flex items-center gap-2 text-base"><Siren className="h-4 w-4 text-destructive" /> Emergency availability</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        <div className="flex flex-wrap gap-4">
          <label className="flex items-center gap-2"><input type="checkbox" checked={f.is24x7} onChange={(e) => set("is24x7", e.target.checked)} /> Open 24/7</label>
          <label className="flex items-center gap-2"><input type="checkbox" checked={f.acceptsWalkInEmergency} onChange={(e) => set("acceptsWalkInEmergency", e.target.checked)} /> Accepts walk-in emergencies</label>
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <div><Label htmlFor="ep-phone">Emergency phone</Label><Input id="ep-phone" inputMode="tel" placeholder="+91 98765 43210" value={f.emergencyPhone ?? ""} onChange={(e) => set("emergencyPhone", e.target.value)} /></div>
          <div><Label htmlFor="ep-clinic">Clinic name</Label><Input id="ep-clinic" value={f.clinicName ?? ""} onChange={(e) => set("clinicName", e.target.value)} /></div>
          <div><Label htmlFor="ep-addr">Clinic address</Label><Input id="ep-addr" value={f.clinicAddress ?? ""} onChange={(e) => set("clinicAddress", e.target.value)} /></div>
          <div><Label htmlFor="ep-city">City</Label><Input id="ep-city" value={f.city ?? ""} onChange={(e) => set("city", e.target.value)} /></div>
          <div><Label htmlFor="ep-lat">Latitude (optional)</Label><Input id="ep-lat" inputMode="decimal" value={f.latitude ?? ""} onChange={(e) => set("latitude", e.target.value === "" ? null : (e.target.value as unknown as number))} /></div>
          <div><Label htmlFor="ep-lng">Longitude (optional)</Label><Input id="ep-lng" inputMode="decimal" value={f.longitude ?? ""} onChange={(e) => set("longitude", e.target.value === "" ? null : (e.target.value as unknown as number))} /></div>
        </div>
        <p className="text-xs text-muted-foreground">A phone number is required to be listed. Coordinates enable "nearest first" ordering; without them we match by city.</p>
        <Button onClick={() => save.mutate()} disabled={save.isPending}>Save</Button>
      </CardContent>
    </Card>
  );
}
