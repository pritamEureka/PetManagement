import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { petSchema, type PetInput, animalTypes, genders, animalSizes } from "@/lib/schemas";
import { petsApi, type Pet, type PetDetail } from "@/api/pets";
import { livestockApi } from "@/api/livestock";
import { toast } from "@/components/ui/sonner";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  editing?: Pet | PetDetail;
  onSaved: () => void;
}

const NONE = "__none";

const empty: Partial<PetInput> = { name: "", animalType: "Dog", gender: "Unknown", isAvailableForAdoption: false };

export function PetFormDialog({ open, onOpenChange, editing, onSaved }: Props) {
  const form = useForm<PetInput>({ resolver: zodResolver(petSchema), defaultValues: empty });
  const { register, handleSubmit, watch, setValue, reset, formState: { errors, isSubmitting } } = form;

  const { data: myPets = [] } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine, enabled: open });
  const { data: groups = [] } = useQuery({ queryKey: ["livestock", "groups"], queryFn: livestockApi.groups, enabled: open });

  useEffect(() => {
    if (!open) return;
    if (!editing) { reset(empty as PetInput); return; }
    const e = editing as Pet & Partial<PetDetail>;
    reset({
      name: e.name,
      animalType: e.animalType as any,
      gender: e.gender as any,
      breed: e.breed ?? "",
      color: e.color ?? "",
      tagNumber: e.tagNumber ?? "",
      primaryPhotoUrl: e.primaryPhotoUrl ?? "",
      allergies: e.allergies ?? "",
      dietNotes: e.dietNotes ?? "",
      birthDate: e.birthDate?.slice(0, 10) ?? null,
      weightKg: e.weightKg ?? null,
      isAvailableForAdoption: e.isAvailableForAdoption,
      size: (e.size as any) ?? null,
      microchipNumber: e.microchipNumber ?? "",
      sireId: e.sire?.id ?? null,
      damId: e.dam?.id ?? null,
      groupId: e.groupId ?? null,
    });
  }, [editing, open, reset]);

  const animalType = watch("animalType");
  const sameSpecies = myPets.filter((p) => p.animalType === animalType && p.id !== editing?.id);
  const sires = sameSpecies.filter((p) => p.gender !== "Female");
  const dams = sameSpecies.filter((p) => p.gender !== "Male");
  const photo = watch("primaryPhotoUrl");

  async function onSubmit(values: PetInput) {
    try {
      if (editing) await petsApi.update(editing.id, values);
      else await petsApi.create(values);
      toast.success(editing ? "Pet updated" : "Pet added");
      onSaved();
      onOpenChange(false);
    } catch {
      // Global interceptor already surfaced the API error.
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{editing ? "Edit pet" : "Add a pet"}</DialogTitle>
          <DialogDescription>You can update or remove this any time from your pets list.</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
          <div className="flex items-start gap-3">
            <div className="h-20 w-20 rounded-lg bg-muted overflow-hidden shrink-0">
              {photo ? <img src={photo} alt="" className="h-full w-full object-cover" /> : null}
            </div>
            <div className="flex-1 space-y-2">
              <div>
                <Label htmlFor="name">Name</Label>
                <Input id="name" {...register("name")} />
                {errors.name && <p className="text-xs text-destructive mt-1">{errors.name.message}</p>}
              </div>
              <FileUploadButton label={photo ? "Change photo" : "Upload photo"}
                onUploaded={(url) => setValue("primaryPhotoUrl", url, { shouldDirty: true })} />
            </div>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
            <div>
              <Label>Animal type</Label>
              <Select value={animalType} onValueChange={(v) => setValue("animalType", v as any)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div>
              <Label>Gender</Label>
              <Select value={watch("gender")} onValueChange={(v) => setValue("gender", v as any)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{genders.map((g) => <SelectItem key={g} value={g}>{g}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div>
              <Label>Size</Label>
              <Select value={watch("size") ?? NONE} onValueChange={(v) => setValue("size", v === NONE ? null : (v as any))}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>Not set</SelectItem>
                  {animalSizes.map((s) => <SelectItem key={s} value={s}>{s === "ExtraLarge" ? "Extra large" : s}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <Label htmlFor="breed">Breed</Label>
              <Input id="breed" {...register("breed")} />
            </div>
            <div>
              <Label htmlFor="birth">Birth date</Label>
              <Input id="birth" type="date" max={new Date().toISOString().slice(0, 10)} {...register("birthDate")} />
              {errors.birthDate && <p className="text-xs text-destructive mt-1">{errors.birthDate.message}</p>}
            </div>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div>
              <Label htmlFor="weight">Weight (kg)</Label>
              <Input id="weight" type="number" step="0.1" min="0" {...register("weightKg")} />
              {errors.weightKg && <p className="text-xs text-destructive mt-1">{errors.weightKg.message}</p>}
            </div>
            <div>
              <Label htmlFor="color">Color</Label>
              <Input id="color" {...register("color")} />
            </div>
            <div>
              <Label htmlFor="tag">Tag #</Label>
              <Input id="tag" {...register("tagNumber")} />
            </div>
            <div>
              <Label htmlFor="chip">Microchip #</Label>
              <Input id="chip" {...register("microchipNumber")} />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div>
              <Label>Sire (father)</Label>
              <Select value={watch("sireId") ?? NONE} onValueChange={(v) => setValue("sireId", v === NONE ? null : v)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>Unknown</SelectItem>
                  {sires.map((p) => <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Dam (mother)</Label>
              <Select value={watch("damId") ?? NONE} onValueChange={(v) => setValue("damId", v === NONE ? null : v)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>Unknown</SelectItem>
                  {dams.map((p) => <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Herd / flock</Label>
              <Select value={watch("groupId") ?? NONE} onValueChange={(v) => setValue("groupId", v === NONE ? null : v)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {groups.map((g) => <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div>
            <Label htmlFor="allergies">Allergies</Label>
            <Textarea id="allergies" rows={2} {...register("allergies")} />
          </div>
          <div>
            <Label htmlFor="diet">Diet notes</Label>
            <Textarea id="diet" rows={2} {...register("dietNotes")} />
          </div>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox
              checked={watch("isAvailableForAdoption")}
              onChange={(e) => setValue("isAvailableForAdoption", e.currentTarget.checked)}
            />
            Available for adoption
          </label>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Saving..." : editing ? "Save changes" : "Add pet"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
