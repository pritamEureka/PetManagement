import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { Field } from "@/pages/pets/detail/shared";
import { useI18n } from "@/lib/i18n";
import {
  communitiesApi, EVENT_TYPES,
  type Community, type CommunityEventType,
} from "@/api/communities";

export const nativeSelectCls =
  "flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

const SPECIES = ["", "Dog", "Cat", "Bird", "Rabbit", "Fish", "Reptile", "Other"];

export function CreateCommunityDialog({
  open, onOpenChange, onCreated,
}: { open: boolean; onOpenChange: (o: boolean) => void; onCreated?: (c: Community) => void }) {
  const { t } = useI18n();
  const qc = useQueryClient();
  const [name, setName] = useState("");
  const [species, setSpecies] = useState("");
  const [breed, setBreed] = useState("");
  const [city, setCity] = useState("");
  const [description, setDescription] = useState("");
  const [isPublic, setIsPublic] = useState(true);

  const create = useMutation({
    mutationFn: () => communitiesApi.create({
      name: name.trim(), species: species || null, breed: breed.trim() || null,
      city: city.trim() || null, description: description.trim() || null, isPublic,
    }),
    onSuccess: (c) => {
      toast.success(t("cdlg.communityCreated"));
      qc.invalidateQueries({ queryKey: ["communities"] });
      setName(""); setSpecies(""); setBreed(""); setCity(""); setDescription(""); setIsPublic(true);
      onOpenChange(false);
      onCreated?.(c);
    },
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("cdlg.createCommunity")}</DialogTitle>
          <DialogDescription>{t("cdlg.createCommunityDesc")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <Field label={t("cdlg.name")} htmlFor="cc-name">
            <Input id="cc-name" value={name} maxLength={120} onChange={(e) => setName(e.target.value)} placeholder={t("cdlg.namePlaceholder")} />
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label={t("cdlg.species")} htmlFor="cc-species">
              <select id="cc-species" className={nativeSelectCls} value={species} onChange={(e) => setSpecies(e.target.value)}>
                {SPECIES.map((sp) => <option key={sp} value={sp}>{sp ? t(`cdlg.species.${sp}`) : t("cdlg.any")}</option>)}
              </select>
            </Field>
            <Field label={t("cdlg.breed")} htmlFor="cc-breed">
              <Input id="cc-breed" value={breed} maxLength={80} onChange={(e) => setBreed(e.target.value)} />
            </Field>
          </div>
          <Field label={t("cdlg.city")} htmlFor="cc-city">
            <Input id="cc-city" value={city} maxLength={100} onChange={(e) => setCity(e.target.value)} />
          </Field>
          <Field label={t("cdlg.description")} htmlFor="cc-desc">
            <Textarea id="cc-desc" value={description} maxLength={2000} onChange={(e) => setDescription(e.target.value)} />
          </Field>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={isPublic} onChange={(e) => setIsPublic(e.target.checked)} />
            {t("cdlg.publicGroup")}
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{t("cdlg.cancel")}</Button>
          <Button disabled={!name.trim() || create.isPending} onClick={() => create.mutate()}>{t("cdlg.create")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Converts a datetime-local value ("2026-01-02T10:00") to an ISO UTC string. */
function toIso(local: string) {
  return new Date(local).toISOString();
}

export function CreateEventDialog({
  communityId, open, onOpenChange,
}: { communityId: string; open: boolean; onOpenChange: (o: boolean) => void }) {
  const { t } = useI18n();
  const qc = useQueryClient();
  const [title, setTitle] = useState("");
  const [type, setType] = useState<CommunityEventType>("Meetup");
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [city, setCity] = useState("");
  const [venue, setVenue] = useState("");
  const [lat, setLat] = useState("");
  const [lng, setLng] = useState("");
  const [capacity, setCapacity] = useState("");
  const [description, setDescription] = useState("");

  const latNum = lat.trim() === "" ? null : Number(lat);
  const lngNum = lng.trim() === "" ? null : Number(lng);
  const coordsOk = (latNum === null) === (lngNum === null) && !Number.isNaN(latNum) && !Number.isNaN(lngNum);
  const valid = title.trim() && startsAt && endsAt && new Date(endsAt) > new Date(startsAt) && coordsOk;

  const create = useMutation({
    mutationFn: () => communitiesApi.createEvent(communityId, {
      title: title.trim(), type, startsAt: toIso(startsAt), endsAt: toIso(endsAt),
      city: city.trim() || null, venue: venue.trim() || null,
      latitude: latNum, longitude: lngNum,
      capacity: capacity ? Math.max(1, Number(capacity)) : null,
      description: description.trim() || null,
    }),
    onSuccess: () => {
      toast.success(t("cdlg.eventCreated"));
      qc.invalidateQueries({ queryKey: ["community-events"] });
      onOpenChange(false);
    },
  });

  const useMyLocation = () => {
    navigator.geolocation?.getCurrentPosition((p) => {
      setLat(p.coords.latitude.toFixed(5));
      setLng(p.coords.longitude.toFixed(5));
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t("cdlg.hostEvent")}</DialogTitle>
          <DialogDescription>{t("cdlg.hostEventDesc")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <Field label={t("cdlg.title")} htmlFor="ce-title">
            <Input id="ce-title" value={title} maxLength={160} onChange={(e) => setTitle(e.target.value)} />
          </Field>
          <Field label={t("cdlg.type")} htmlFor="ce-type">
            <select id="ce-type" className={nativeSelectCls} value={type} onChange={(e) => setType(e.target.value as CommunityEventType)}>
              {EVENT_TYPES.map((et) => <option key={et.value} value={et.value}>{t(`events.typeLabel.${et.value}`)}</option>)}
            </select>
          </Field>
          <div className="grid grid-cols-2 gap-3">
            <Field label={t("cdlg.starts")} htmlFor="ce-start">
              <Input id="ce-start" type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} />
            </Field>
            <Field label={t("cdlg.ends")} htmlFor="ce-end">
              <Input id="ce-end" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
            </Field>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field label={t("cdlg.city")} htmlFor="ce-city"><Input id="ce-city" value={city} onChange={(e) => setCity(e.target.value)} /></Field>
            <Field label={t("cdlg.venue")} htmlFor="ce-venue"><Input id="ce-venue" value={venue} onChange={(e) => setVenue(e.target.value)} /></Field>
          </div>
          <div className="grid grid-cols-3 gap-3 items-end">
            <Field label={t("cdlg.latitude")} htmlFor="ce-lat"><Input id="ce-lat" inputMode="decimal" value={lat} onChange={(e) => setLat(e.target.value)} /></Field>
            <Field label={t("cdlg.longitude")} htmlFor="ce-lng"><Input id="ce-lng" inputMode="decimal" value={lng} onChange={(e) => setLng(e.target.value)} /></Field>
            <Button type="button" variant="outline" onClick={useMyLocation}>{t("cdlg.useMyLocation")}</Button>
          </div>
          {!coordsOk && <p className="text-xs text-destructive">{t("cdlg.coordsBoth")}</p>}
          <Field label={t("cdlg.capacity")} htmlFor="ce-cap">
            <Input id="ce-cap" type="number" min={1} value={capacity} onChange={(e) => setCapacity(e.target.value)} />
          </Field>
          <Field label={t("cdlg.description")} htmlFor="ce-desc">
            <Textarea id="ce-desc" value={description} onChange={(e) => setDescription(e.target.value)} />
          </Field>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{t("cdlg.cancel")}</Button>
          <Button disabled={!valid || create.isPending} onClick={() => create.mutate()}>{t("cdlg.createEvent")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
