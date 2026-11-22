import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell, MapPin } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { toast } from "@/components/ui/sonner";
import { Field } from "@/pages/pets/detail/shared";
import { useI18n } from "@/lib/i18n";
import { lostFoundPlusApi } from "@/api/socialPlus";

function nowLocal() {
  return new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
}

/** Sightings: anyone can report; the report owner verifies or rejects. Also hosts evidence upload for the owner. */
export function SightingsPanel({ reportId, isMine, isOpen }: { reportId: string; isMine: boolean; isOpen: boolean }) {
  const { t } = useI18n();
  const qc = useQueryClient();
  const key = ["lost-found", reportId, "sightings"];
  const { data: sightings = [] } = useQuery({ queryKey: key, queryFn: () => lostFoundPlusApi.sightings(reportId) });
  const { data: evidence = [] } = useQuery({
    queryKey: ["lost-found", reportId, "evidence"], queryFn: () => lostFoundPlusApi.evidence(reportId), enabled: isMine,
  });
  const [f, setF] = useState({ seenAt: nowLocal(), description: "", photoUrl: "", lat: null as number | null, lng: null as number | null });
  const refresh = () => {
    qc.invalidateQueries({ queryKey: key });
    qc.invalidateQueries({ queryKey: ["lost-found", reportId, "timeline"] });
  };

  const add = useMutation({
    mutationFn: () => lostFoundPlusApi.addSighting(reportId, {
      seenAt: new Date(f.seenAt).toISOString(), description: f.description.trim(), photoUrl: f.photoUrl || null,
      latitude: f.lat, longitude: f.lng,
    }),
    onSuccess: () => { toast.success(t("lf.sightingSent")); setF({ ...f, description: "", photoUrl: "" }); refresh(); },
  });
  const review = useMutation({
    mutationFn: ({ id, ok }: { id: string; ok: boolean }) => (ok ? lostFoundPlusApi.verify(reportId, id) : lostFoundPlusApi.reject(reportId, id)),
    onSuccess: refresh,
  });
  const addEvidence = useMutation({
    mutationFn: (url: string) => lostFoundPlusApi.addEvidence(reportId, url, "Photo"),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["lost-found", reportId, "evidence"] }),
  });

  function locate() {
    if (!navigator.geolocation) return toast.error(t("lf.noGeo"));
    navigator.geolocation.getCurrentPosition(
      (p) => setF((s) => ({ ...s, lat: p.coords.latitude, lng: p.coords.longitude })),
      () => toast.error(t("lf.noGeo")), { timeout: 10000 });
  }

  return (
    <div className="space-y-3">
      <h2 className="font-semibold">{t("lf.sightings")}</h2>
      {sightings.length === 0 && <p className="text-sm text-muted-foreground">{t("lf.noSightings")}</p>}
      {sightings.map((s) => (
        <Card key={s.id}>
          <CardContent className="p-3 space-y-1 text-sm">
            <div className="flex items-center justify-between gap-2">
              <span className="font-medium">{s.reporterName}</span>
              <Badge variant={s.status === "Verified" ? "success" : s.status === "Rejected" ? "destructive" : "secondary"}>{t("lf.status." + s.status)}</Badge>
            </div>
            <p className="text-xs text-muted-foreground">{new Date(s.seenAt).toLocaleString()}</p>
            <p>{s.description}</p>
            {s.photoUrl && <img src={s.photoUrl} alt="" className="h-24 rounded object-cover" />}
            {isMine && s.status === "Pending" && (
              <div className="flex gap-2 pt-1">
                <Button size="sm" onClick={() => review.mutate({ id: s.id, ok: true })}>{t("lf.verify")}</Button>
                <Button size="sm" variant="outline" onClick={() => review.mutate({ id: s.id, ok: false })}>{t("lf.reject")}</Button>
              </div>
            )}
          </CardContent>
        </Card>
      ))}

      {isOpen && (
        <Card>
          <CardContent className="p-4 space-y-3">
            <p className="font-medium text-sm">{t("lf.reportSighting")}</p>
            <Field label={t("lf.seenAt")} htmlFor="ss-at"><Input id="ss-at" type="datetime-local" value={f.seenAt} onChange={(e) => setF({ ...f, seenAt: e.target.value })} /></Field>
            <Field label={t("lf.whatSaw")} htmlFor="ss-d"><Textarea id="ss-d" rows={3} maxLength={1000} value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} /></Field>
            <div className="flex flex-wrap items-center gap-2">
              <Button type="button" size="sm" variant="outline" onClick={locate}><MapPin className="h-3.5 w-3.5 me-1" />{f.lat != null ? t("lf.locationAdded") : t("lf.addLocation")}</Button>
              <FileUploadButton label={f.photoUrl ? t("lf.changePhoto") : t("lf.addPhoto")} onUploaded={(url) => setF({ ...f, photoUrl: url })} />
            </div>
            <Button disabled={!f.description.trim() || add.isPending} onClick={() => add.mutate()}>{t("lf.submitSighting")}</Button>
          </CardContent>
        </Card>
      )}

      {isMine && (
        <div className="space-y-2">
          <h3 className="font-medium text-sm">{t("lf.evidence")}</h3>
          <div className="flex flex-wrap gap-2">
            {evidence.map((e) => <a key={e.id} href={e.url} target="_blank" rel="noreferrer"><img src={e.url} alt="" className="h-16 w-16 rounded object-cover" /></a>)}
          </div>
          <FileUploadButton label={t("lf.addEvidence")} onUploaded={(url) => addEvidence.mutate(url)} />
        </div>
      )}
    </div>
  );
}

export function TimelinePanel({ reportId }: { reportId: string }) {
  const { t } = useI18n();
  const { data = [] } = useQuery({ queryKey: ["lost-found", reportId, "timeline"], queryFn: () => lostFoundPlusApi.timeline(reportId) });
  return (
    <div className="space-y-2">
      <h2 className="font-semibold">{t("lf.timeline")}</h2>
      <ol className="border-s ps-4 space-y-3">
        {data.map((e, i) => (
          <li key={i} className="text-sm">
            <p className="font-medium">{e.title}</p>
            <p className="text-xs text-muted-foreground">{new Date(e.at).toLocaleString()}{e.detail ? " · " + e.detail : ""}</p>
          </li>
        ))}
      </ol>
    </div>
  );
}

/** Geofenced alert subscriptions (max 5). Alerts are matched against the report's approximate public location. */
export function AlertSubscriptions() {
  const { t } = useI18n();
  const qc = useQueryClient();
  const { data = [] } = useQuery({ queryKey: ["lost-found", "alerts"], queryFn: lostFoundPlusApi.alerts });
  const [radius, setRadius] = useState("5");
  const refresh = () => qc.invalidateQueries({ queryKey: ["lost-found", "alerts"] });
  const remove = useMutation({ mutationFn: (id: string) => lostFoundPlusApi.unsubscribe(id), onSuccess: refresh });
  const add = useMutation({
    mutationFn: (pos: { latitude: number; longitude: number }) => lostFoundPlusApi.subscribe({ ...pos, radiusKm: Number(radius) || 5 }),
    onSuccess: () => { toast.success(t("lf.alertAdded")); refresh(); },
  });

  function subscribeHere() {
    if (!navigator.geolocation) return toast.error(t("lf.noGeo"));
    navigator.geolocation.getCurrentPosition(
      (p) => add.mutate({ latitude: p.coords.latitude, longitude: p.coords.longitude }),
      () => toast.error(t("lf.noGeo")), { timeout: 10000 });
  }

  return (
    <Card>
      <CardContent className="p-4 space-y-3">
        <p className="font-medium flex items-center gap-2"><Bell className="h-4 w-4" />{t("lf.alerts")}</p>
        <p className="text-xs text-muted-foreground">{t("lf.alertsHelp")}</p>
        {data.map((a) => (
          <div key={a.id} className="flex items-center justify-between text-sm">
            <span>{a.radiusKm} km · {a.latitude.toFixed(2)}, {a.longitude.toFixed(2)}</span>
            <Button size="sm" variant="ghost" onClick={() => remove.mutate(a.id)}>{t("lf.remove")}</Button>
          </div>
        ))}
        <div className="flex items-end gap-2">
          <Field label={t("lf.radiusKm")} htmlFor="al-r"><Input id="al-r" type="number" min={1} max={100} value={radius} onChange={(e) => setRadius(e.target.value)} /></Field>
          <Button onClick={subscribeHere} disabled={add.isPending}>{t("lf.alertHere")}</Button>
        </div>
      </CardContent>
    </Card>
  );
}
