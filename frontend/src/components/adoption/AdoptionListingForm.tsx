import { useFormContext, useFieldArray } from "react-hook-form";
import { ImagePlus, X } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import {
  animalTypes, genders, animalSizes, contactPreferences, type AdoptionListingInput
} from "@/lib/schemas";

/**
 * Reusable form body for create + edit listing dialogs. Caller supplies the
 * FormProvider, submit button, and any wrapper chrome.
 */
export function AdoptionListingFormFields() {
  const { register, watch, setValue, formState: { errors } } = useFormContext<AdoptionListingInput>();
  const photos = watch("photoUrls") ?? [];
  const [photoInput, setPhotoInput] = useState("");

  function addPhoto() {
    const url = photoInput.trim();
    if (!url) return;
    try { new URL(url); } catch { return; }
    setValue("photoUrls", [...photos, url]);
    setPhotoInput("");
  }
  function removePhoto(i: number) {
    setValue("photoUrls", photos.filter((_, idx) => idx !== i));
  }

  return (
    <div className="space-y-4">
      {/* Identity */}
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">About the pet</h3>
        <div className="grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <Label htmlFor="title">Title</Label>
            <Input id="title" placeholder="Adopt Bella — friendly retriever" {...register("title")} />
            {errors.title && <p className="text-xs text-destructive mt-1">{errors.title.message}</p>}
          </div>
          <div>
            <Label htmlFor="petName">Pet name</Label>
            <Input id="petName" {...register("petName")} />
          </div>
          <div>
            <Label>Animal type</Label>
            <Select value={watch("animalType")} onValueChange={(v) => setValue("animalType", v as any)}>
              <SelectTrigger><SelectValue placeholder="Choose" /></SelectTrigger>
              <SelectContent>{animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="breed">Breed</Label>
            <Input id="breed" {...register("breed")} />
          </div>
          <div>
            <Label htmlFor="age">Age (months)</Label>
            <Input id="age" type="number" {...register("ageMonths")} />
          </div>
          <div>
            <Label>Gender</Label>
            <Select value={watch("gender")} onValueChange={(v) => setValue("gender", v as any)}>
              <SelectTrigger><SelectValue placeholder="Choose" /></SelectTrigger>
              <SelectContent>{genders.map((g) => <SelectItem key={g} value={g}>{g}</SelectItem>)}</SelectContent>
            </Select>
          </div>
          <div>
            <Label>Size</Label>
            <Select value={watch("size") ?? ""} onValueChange={(v) => setValue("size", v as any)}>
              <SelectTrigger><SelectValue placeholder="Choose" /></SelectTrigger>
              <SelectContent>
                {animalSizes.map((s) => <SelectItem key={s} value={s}>{s.replace("ExtraLarge", "Extra large")}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="color">Color</Label>
            <Input id="color" {...register("color")} />
          </div>
        </div>
        <div>
          <Label htmlFor="description">Description</Label>
          <Textarea id="description" rows={4} placeholder="Personality, story, anything adopters should know..." {...register("description")} />
        </div>
      </section>

      <Separator />

      {/* Health */}
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">Health</h3>
        <div className="flex flex-wrap gap-4">
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={!!watch("vaccinated")} onChange={(e) => setValue("vaccinated", e.currentTarget.checked)} />
            Vaccinated
          </label>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={!!watch("neuteredSpayed")} onChange={(e) => setValue("neuteredSpayed", e.currentTarget.checked)} />
            Neutered / spayed
          </label>
        </div>
        <div>
          <Label htmlFor="vacDetails">Vaccination details</Label>
          <Textarea id="vacDetails" rows={2} {...register("vaccinationDetails")} />
        </div>
        <div>
          <Label htmlFor="health">Health condition</Label>
          <Textarea id="health" rows={2} {...register("healthCondition")} />
        </div>
      </section>

      <Separator />

      {/* Behavior */}
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">Behavior</h3>
        <div>
          <Label htmlFor="behavior">Behavior notes</Label>
          <Textarea id="behavior" rows={2} {...register("behaviorNotes")} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <Label>Good with children</Label>
            <TriSelect
              value={watch("goodWithChildren") ?? null}
              onChange={(v) => setValue("goodWithChildren", v)}
            />
          </div>
          <div>
            <Label>Good with other pets</Label>
            <TriSelect
              value={watch("goodWithOtherPets") ?? null}
              onChange={(v) => setValue("goodWithOtherPets", v)}
            />
          </div>
        </div>
      </section>

      <Separator />

      {/* Logistics */}
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">Logistics</h3>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <Label htmlFor="location">Location</Label>
            <Input id="location" placeholder="City, Country" {...register("location")} />
          </div>
          <div>
            <Label htmlFor="fee">Adoption fee</Label>
            <Input id="fee" type="number" step="0.01" {...register("adoptionFee")} />
          </div>
          <div>
            <Label>Contact preference</Label>
            <Select value={watch("contactPreference")} onValueChange={(v) => setValue("contactPreference", v as any)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>{contactPreferences.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
            </Select>
          </div>
        </div>
        <div>
          <Label htmlFor="reason">Reason for listing</Label>
          <Textarea id="reason" rows={2} {...register("reasonForListing")} />
        </div>
      </section>

      <Separator />

      {/* Photos */}
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">Photos</h3>
        {photos.length > 0 && (
          <div className="grid grid-cols-3 gap-2">
            {photos.map((url, i) => (
              <div key={i} className="relative aspect-square rounded-md overflow-hidden bg-muted">
                <img src={url} className="object-cover w-full h-full" />
                <button type="button" onClick={() => removePhoto(i)}
                  className="absolute top-1 right-1 rounded-full bg-black/60 text-white p-1 hover:bg-black/80">
                  <X className="h-3 w-3" />
                </button>
              </div>
            ))}
          </div>
        )}
        <div className="flex gap-2">
          <Input value={photoInput} onChange={(e) => setPhotoInput(e.target.value)} placeholder="Paste image URL" />
          <Button type="button" variant="outline" onClick={addPhoto}><ImagePlus className="h-4 w-4" /></Button>
        </div>
        <p className="text-xs text-muted-foreground">Up to 12 photos. Higher quality = more requests.</p>
      </section>
    </div>
  );
}

function TriSelect({ value, onChange }: { value: boolean | null; onChange: (v: boolean | null) => void }) {
  const s = value === true ? "yes" : value === false ? "no" : "unknown";
  return (
    <Select value={s} onValueChange={(v) => onChange(v === "yes" ? true : v === "no" ? false : null)}>
      <SelectTrigger><SelectValue /></SelectTrigger>
      <SelectContent>
        <SelectItem value="unknown">Unknown</SelectItem>
        <SelectItem value="yes">Yes</SelectItem>
        <SelectItem value="no">No</SelectItem>
      </SelectContent>
    </Select>
  );
}
