import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { petSchema, type PetInput, animalTypes, genders } from "@/lib/schemas";
import { petsApi, type Pet } from "@/api/pets";
import { toast } from "@/components/ui/sonner";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  editing?: Pet;
  onSaved: () => void;
}

export function PetFormDialog({ open, onOpenChange, editing, onSaved }: Props) {
  const form = useForm<PetInput>({
    resolver: zodResolver(petSchema),
    defaultValues: { name: "", animalType: "Dog", gender: "Unknown", isAvailableForAdoption: false }
  });
  const { register, handleSubmit, watch, setValue, reset, formState: { errors, isSubmitting } } = form;

  useEffect(() => {
    if (open) {
      reset(editing ? {
        ...editing,
        breed: editing.breed ?? "",
        color: editing.color ?? "",
        tagNumber: editing.tagNumber ?? "",
        primaryPhotoUrl: editing.primaryPhotoUrl ?? "",
        allergies: editing.allergies ?? "",
        dietNotes: editing.dietNotes ?? "",
        birthDate: editing.birthDate?.slice(0, 10) ?? "",
        animalType: editing.animalType as any,
        gender: editing.gender as any,
        weightKg: editing.weightKg ?? undefined,
      } : { name: "", animalType: "Dog", gender: "Unknown", isAvailableForAdoption: false });
    }
  }, [editing, open, reset]);

  async function onSubmit(values: PetInput) {
    try {
      if (editing) await petsApi.update(editing.id, values);
      else await petsApi.create(values);
      toast.success(editing ? "Pet updated" : "Pet added");
      onSaved();
      onOpenChange(false);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Failed to save pet.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{editing ? "Edit pet" : "Add a pet"}</DialogTitle>
          <DialogDescription>You can update or remove this any time from your pets list.</DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
          <div>
            <Label htmlFor="name">Name</Label>
            <Input id="name" {...register("name")} />
            {errors.name && <p className="text-xs text-destructive mt-1">{errors.name.message}</p>}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label>Animal type</Label>
              <Select value={watch("animalType")} onValueChange={(v) => setValue("animalType", v as any)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Gender</Label>
              <Select value={watch("gender")} onValueChange={(v) => setValue("gender", v as any)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {genders.map((g) => <SelectItem key={g} value={g}>{g}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label htmlFor="breed">Breed</Label>
              <Input id="breed" {...register("breed")} />
            </div>
            <div>
              <Label htmlFor="birth">Birth date</Label>
              <Input id="birth" type="date" {...register("birthDate")} />
            </div>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div>
              <Label htmlFor="weight">Weight (kg)</Label>
              <Input id="weight" type="number" step="0.1" {...register("weightKg")} />
            </div>
            <div>
              <Label htmlFor="color">Color</Label>
              <Input id="color" {...register("color")} />
            </div>
            <div>
              <Label htmlFor="tag">Tag #</Label>
              <Input id="tag" {...register("tagNumber")} />
            </div>
          </div>
          <div>
            <Label htmlFor="photo">Primary photo URL</Label>
            <Input id="photo" placeholder="https://..." {...register("primaryPhotoUrl")} />
            {errors.primaryPhotoUrl && <p className="text-xs text-destructive mt-1">{errors.primaryPhotoUrl.message}</p>}
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
