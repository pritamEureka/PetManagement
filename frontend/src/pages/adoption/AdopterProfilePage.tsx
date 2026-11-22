import { useEffect, useState } from "react";
import { UserRound } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { PageHeader } from "@/components/common/PageHeader";
import { toast } from "@/components/ui/sonner";
import { adoptionPlusApi, type AdopterProfile } from "@/api/adoption";
import { useI18n } from "@/lib/i18n";

const EMPTY: AdopterProfile = {
  householdSize: 1, childrenAges: "", existingPetsJson: "", experience: "Some", homeType: "House",
  hasYard: false, travelDistanceKm: 25, activity: "Moderate", latitude: null, longitude: null
};

const selectCls = "h-9 w-full rounded-md border bg-background px-2 text-sm";

export function AdopterProfilePage() {
  const { t } = useI18n();
  const [p, setP] = useState<AdopterProfile>(EMPTY);
  const [pets, setPets] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    adoptionPlusApi.profile().then((x) => {
      if (!x) return;
      setP(x);
      try { setPets(x.existingPetsJson ? (JSON.parse(x.existingPetsJson) as { type: string }[]).map((e) => e.type).join(", ") : ""); } catch { setPets(""); }
    }).catch(() => {});
  }, []);

  async function save() {
    setBusy(true);
    try {
      const list = pets.split(",").map((s) => s.trim()).filter(Boolean).map((type) => ({ type, count: 1 }));
      await adoptionPlusApi.saveProfile({ ...p, existingPetsJson: list.length ? JSON.stringify(list) : null });
      toast.success(t("adoptmsg.profile.saved"));
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't save profile.");
    } finally { setBusy(false); }
  }

  const set = <K extends keyof AdopterProfile>(k: K, v: AdopterProfile[K]) => setP((s) => ({ ...s, [k]: v }));

  return (
    <div className="space-y-4 max-w-2xl">
      <PageHeader title={t("adoptmsg.profile.title")} icon={UserRound} />
      <Card><CardContent className="pt-6 grid gap-4 sm:grid-cols-2">
        <div><Label htmlFor="hh">Household size</Label>
          <Input id="hh" type="number" min={1} max={30} value={p.householdSize} onChange={(e) => set("householdSize", Number(e.target.value) || 1)} /></div>
        <div><Label htmlFor="kids">Ages of children (comma separated)</Label>
          <Input id="kids" value={p.childrenAges ?? ""} onChange={(e) => set("childrenAges", e.target.value)} placeholder="3, 7" /></div>
        <div className="sm:col-span-2"><Label htmlFor="pets">Existing pets (types, comma separated)</Label>
          <Input id="pets" value={pets} onChange={(e) => setPets(e.target.value)} placeholder="Dog, Cat" /></div>
        <div><Label htmlFor="exp">Experience</Label>
          <select id="exp" className={selectCls} value={p.experience} onChange={(e) => set("experience", e.target.value as AdopterProfile["experience"])}>
            <option>None</option><option>Some</option><option>Experienced</option></select></div>
        <div><Label htmlFor="home">Home type</Label>
          <select id="home" className={selectCls} value={p.homeType ?? "House"} onChange={(e) => set("homeType", e.target.value)}>
            <option>Apartment</option><option>House</option><option>HouseWithYard</option><option>Farm</option><option>Other</option></select></div>
        <div><Label htmlFor="act">Activity level</Label>
          <select id="act" className={selectCls} value={p.activity} onChange={(e) => set("activity", e.target.value as AdopterProfile["activity"])}>
            <option>Low</option><option>Moderate</option><option>High</option></select></div>
        <div><Label htmlFor="km">Willing to travel (km)</Label>
          <Input id="km" type="number" min={1} max={2000} value={p.travelDistanceKm} onChange={(e) => set("travelDistanceKm", Number(e.target.value) || 1)} /></div>
        <div><Label htmlFor="lat">Latitude (optional)</Label>
          <Input id="lat" type="number" step="0.01" value={p.latitude ?? ""} onChange={(e) => set("latitude", e.target.value === "" ? null : Number(e.target.value))} /></div>
        <div><Label htmlFor="lng">Longitude (optional)</Label>
          <Input id="lng" type="number" step="0.01" value={p.longitude ?? ""} onChange={(e) => set("longitude", e.target.value === "" ? null : Number(e.target.value))} /></div>
        <label className="flex items-center gap-2 text-sm sm:col-span-2">
          <Checkbox checked={p.hasYard} onChange={(e) => set("hasYard", e.currentTarget.checked)} /> I have a yard
        </label>
        <div className="sm:col-span-2"><Button onClick={save} disabled={busy}>{t("adoptmsg.profile.save")}</Button></div>
      </CardContent></Card>
    </div>
  );
}
