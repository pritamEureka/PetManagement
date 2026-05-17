import { useEffect } from "react";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle, DialogDescription
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { adoptionListingSchema, type AdoptionListingInput } from "@/lib/schemas";
import { adoptionApi, type AdoptionListingDetail } from "@/api/adoption";
import { toast } from "@/components/ui/sonner";
import { AdoptionListingFormFields } from "@/components/adoption/AdoptionListingForm";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  listing: AdoptionListingDetail;
  onSaved: () => void;
}

export function EditAdoptionDialog({ open, onOpenChange, listing, onSaved }: Props) {
  const methods = useForm<AdoptionListingInput>({
    resolver: zodResolver(adoptionListingSchema),
    defaultValues: toFormInput(listing)
  });
  const { handleSubmit, reset, formState: { isSubmitting } } = methods;

  useEffect(() => { if (open) reset(toFormInput(listing)); }, [open, listing, reset]);

  async function onSubmit(values: AdoptionListingInput) {
    try {
      await adoptionApi.update(listing.id, values);
      toast.success("Listing updated");
      onSaved(); onOpenChange(false);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't save.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>Edit adoption listing</DialogTitle>
          <DialogDescription>
            Changes to an approved listing don't require re-approval unless flagged by moderators.
          </DialogDescription>
        </DialogHeader>
        <FormProvider {...methods}>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="max-h-[60vh] overflow-y-auto pr-2">
              <AdoptionListingFormFields />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
              <Button type="submit" disabled={isSubmitting}>{isSubmitting ? "Saving..." : "Save changes"}</Button>
            </DialogFooter>
          </form>
        </FormProvider>
      </DialogContent>
    </Dialog>
  );
}

function toFormInput(l: AdoptionListingDetail): AdoptionListingInput {
  return {
    title: l.title,
    petName: l.petName ?? "",
    description: l.description ?? "",
    animalType: l.animalType as any,
    breed: l.breed ?? "",
    ageMonths: l.ageMonths ?? undefined,
    gender: l.gender as any,
    size: (l.size as any) ?? undefined,
    color: l.color ?? "",
    vaccinated: l.vaccinated,
    vaccinationDetails: l.vaccinationDetails ?? "",
    neuteredSpayed: l.neuteredSpayed,
    healthCondition: l.healthCondition ?? "",
    behaviorNotes: l.behaviorNotes ?? "",
    goodWithChildren: l.goodWithChildren ?? null,
    goodWithOtherPets: l.goodWithOtherPets ?? null,
    location: l.location ?? "",
    adoptionFee: l.adoptionFee,
    reasonForListing: l.reasonForListing ?? "",
    contactPreference: l.contactPreference as any,
    petId: "",
    photoUrls: l.photoUrls,
    submitForApproval: false
  };
}
