import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MapPin } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/components/ui/sonner";
import { logisticsApi, type DeliveryZone, type PeakHourRule } from "@/api/logisticsPlus";

const DAYS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
const errMsg = (e: unknown) => (e as any)?.response?.data?.error?.message ?? "Request failed.";

function ZonesCard() {
  const qc = useQueryClient();
  const { data: zones = [] } = useQuery({ queryKey: ["dz-zones"], queryFn: logisticsApi.zones });
  const [f, setF] = useState({ name: "", cities: "", baseFee: "60", free: "", min: "24", max: "72" });
  const refresh = () => qc.invalidateQueries({ queryKey: ["dz-zones"] });
  const add = useMutation({
    mutationFn: () => logisticsApi.saveZone(null, {
      storeId: null, name: f.name, cities: f.cities.split(",").map((c) => c.trim()).filter(Boolean),
      baseFee: Number(f.baseFee), freeShippingThreshold: f.free ? Number(f.free) : null,
      estimatedMinHours: Number(f.min), estimatedMaxHours: Number(f.max), isActive: true
    }),
    onSuccess: () => { toast.success("Zone saved"); setF({ ...f, name: "", cities: "" }); refresh(); }, onError: (e) => toast.error(errMsg(e))
  });
  const toggle = useMutation({
    mutationFn: (z: DeliveryZone) => logisticsApi.saveZone(z.id, { ...z, isActive: !z.isActive }),
    onSuccess: refresh, onError: (e) => toast.error(errMsg(e))
  });
  const del = useMutation({ mutationFn: (id: string) => logisticsApi.deleteZone(id), onSuccess: refresh, onError: (e) => toast.error(errMsg(e)) });

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Delivery zones</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        <p className="text-muted-foreground">Checkout uses a zone only when every store in the order has a matching zone for the destination city; otherwise the flat fee applies.</p>
        {zones.map((z) => (
          <div key={z.id} className="flex items-center gap-2 border rounded p-2">
            <div className="flex-1">
              <div className="font-medium">{z.name} {!z.isActive && <Badge variant="outline">inactive</Badge>}</div>
              <div className="text-xs text-muted-foreground">{z.cities.join(", ")} | fee {z.baseFee}{z.freeShippingThreshold ? `, free over ${z.freeShippingThreshold}` : ""} | {z.estimatedMinHours}-{z.estimatedMaxHours}h</div>
            </div>
            <Button size="sm" variant="outline" onClick={() => toggle.mutate(z)}>{z.isActive ? "Disable" : "Enable"}</Button>
            <Button size="sm" variant="outline" onClick={() => del.mutate(z.id)}>Delete</Button>
          </div>
        ))}
        <div className="grid grid-cols-2 md:grid-cols-6 gap-2 items-end">
          <Input placeholder="Name" value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} />
          <Input placeholder="Cities, comma separated" className="md:col-span-2" value={f.cities} onChange={(e) => setF({ ...f, cities: e.target.value })} />
          <Input type="number" placeholder="Base fee" value={f.baseFee} onChange={(e) => setF({ ...f, baseFee: e.target.value })} />
          <Input type="number" placeholder="Free over" value={f.free} onChange={(e) => setF({ ...f, free: e.target.value })} />
          <div className="flex gap-1">
            <Input type="number" aria-label="Min hours" value={f.min} onChange={(e) => setF({ ...f, min: e.target.value })} />
            <Input type="number" aria-label="Max hours" value={f.max} onChange={(e) => setF({ ...f, max: e.target.value })} />
          </div>
        </div>
        <Button size="sm" disabled={!f.name || !f.cities || add.isPending} onClick={() => add.mutate()}>Add zone</Button>
      </CardContent>
    </Card>
  );
}

function RulesCard() {
  const qc = useQueryClient();
  const { data: rules = [] } = useQuery({ queryKey: ["dz-rules"], queryFn: logisticsApi.rules });
  const [f, setF] = useState({ days: [5, 6] as number[], start: "18:00", end: "22:00", fixed: "0", pct: "10", label: "" });
  const refresh = () => qc.invalidateQueries({ queryKey: ["dz-rules"] });
  const add = useMutation({
    mutationFn: () => logisticsApi.saveRule(null, {
      zoneId: null, days: f.days, startTime: f.start, endTime: f.end, surchargeFixed: Number(f.fixed),
      surchargePercent: Number(f.pct), isActive: true, label: f.label || null
    }),
    onSuccess: () => { toast.success("Rule saved"); refresh(); }, onError: (e) => toast.error(errMsg(e))
  });
  const del = useMutation({ mutationFn: (id: string) => logisticsApi.deleteRule(id), onSuccess: refresh, onError: (e) => toast.error(errMsg(e)) });
  const toggleDay = (d: number) => setF({ ...f, days: f.days.includes(d) ? f.days.filter((x) => x !== d) : [...f.days, d] });
  const describe = (r: PeakHourRule) => `${r.days.map((d) => DAYS[d]).join(" ")} ${r.startTime}-${r.endTime}: +${r.surchargeFixed} +${r.surchargePercent}%`;

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Peak-hour surcharges</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        <p className="text-muted-foreground">Times are local (Logistics:UtcOffsetMinutes, default UTC+6). A window like 22:00-02:00 runs overnight. Overlapping rules do not stack.</p>
        {rules.map((r) => (
          <div key={r.id} className="flex items-center gap-2 border rounded p-2">
            <span className="flex-1">{r.label ?? "Peak"}: {describe(r)} {!r.isActive && <Badge variant="outline">inactive</Badge>}</span>
            <Button size="sm" variant="outline" onClick={() => del.mutate(r.id)}>Delete</Button>
          </div>
        ))}
        <div className="flex flex-wrap gap-1">
          {DAYS.map((d, i) => (
            <Button key={d} size="sm" type="button" variant={f.days.includes(i) ? "default" : "outline"} onClick={() => toggleDay(i)}>{d}</Button>
          ))}
        </div>
        <div className="flex flex-wrap gap-2 items-end">
          <Input type="time" className="w-28" aria-label="Start" value={f.start} onChange={(e) => setF({ ...f, start: e.target.value })} />
          <Input type="time" className="w-28" aria-label="End" value={f.end} onChange={(e) => setF({ ...f, end: e.target.value })} />
          <Input type="number" className="w-24" placeholder="Fixed" value={f.fixed} onChange={(e) => setF({ ...f, fixed: e.target.value })} />
          <Input type="number" className="w-24" placeholder="Percent" value={f.pct} onChange={(e) => setF({ ...f, pct: e.target.value })} />
          <Input className="w-40" placeholder="Label" value={f.label} onChange={(e) => setF({ ...f, label: e.target.value })} />
          <Button size="sm" disabled={f.days.length === 0 || add.isPending} onClick={() => add.mutate()}>Add rule</Button>
        </div>
      </CardContent>
    </Card>
  );
}

function SlaCard() {
  const qc = useQueryClient();
  const { data: slas = [] } = useQuery({ queryKey: ["dz-slas"], queryFn: logisticsApi.slas });
  const { data: breaches } = useQuery({ queryKey: ["dz-breaches"], queryFn: () => logisticsApi.breaches(true) });
  const platform = slas.find((s) => s.storeId == null);
  const [ship, setShip] = useState("");
  const [deliver, setDeliver] = useState("");
  const save = useMutation({
    mutationFn: () => logisticsApi.saveSla({
      storeId: null, maxHoursToShip: Number(ship || platform?.maxHoursToShip || 48),
      maxHoursToDeliver: Number(deliver || platform?.maxHoursToDeliver || 168), penaltyNote: platform?.penaltyNote ?? null, isActive: true
    }),
    onSuccess: () => { toast.success("SLA saved"); qc.invalidateQueries({ queryKey: ["dz-slas"] }); }, onError: (e) => toast.error(errMsg(e))
  });
  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Fulfillment SLA (platform default)</CardTitle></CardHeader>
      <CardContent className="space-y-3 text-sm">
        <div className="flex flex-wrap items-end gap-2">
          <label className="text-xs">Hours to ship<Input type="number" className="w-28" placeholder={String(platform?.maxHoursToShip ?? 48)} value={ship} onChange={(e) => setShip(e.target.value)} /></label>
          <label className="text-xs">Hours to deliver<Input type="number" className="w-28" placeholder={String(platform?.maxHoursToDeliver ?? 168)} value={deliver} onChange={(e) => setDeliver(e.target.value)} /></label>
          <Button size="sm" disabled={save.isPending} onClick={() => save.mutate()}>Save</Button>
        </div>
        <div className="font-medium">Open breaches ({breaches?.total ?? 0})</div>
        {breaches?.items.map((b) => (
          <div key={b.id} className="flex gap-2"><Badge variant="destructive">{b.kind}</Badge><span>Order {b.orderNumber ?? b.orderId}</span>
            <span className="text-muted-foreground">{b.storeName} | {new Date(b.breachedAt).toLocaleString()}</span></div>
        ))}
      </CardContent>
    </Card>
  );
}

export function DeliveryZonesPage() {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2"><MapPin className="h-6 w-6 text-primary" /> Delivery zones and SLAs</h1>
      <ZonesCard />
      <RulesCard />
      <SlaCard />
    </div>
  );
}
