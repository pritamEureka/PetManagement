import { useEffect } from "react";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { adoptionListingSchema, type AdoptionListingInput } from "@/lib/schemas";
import { adoptionApi } from "@/api/adoption";
import { toast } from "@/components/ui/sonner";
import { AdoptionListingFormFields } from "@/components/adoption/AdoptionListingForm";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onCreated: (id: string) => void;
}

const DEFAULT: AdoptionListingInput = {
  title: "", petName: "", description: "",
  animalType: "Dog", breed: "", gender: "Unknown",
  size: undefined, color: "",
  vaccinated: false, vaccinationDetails: "",
  neuteredSpayed: false, healthCondition: "",
  behaviorNotes: "", goodWithChildren: null, goodWithOtherPets: null,
  location: "", adoptionFee: 0, reasonForListing: "",
  contactPreference: "Chat",
  petId: "", photoUrls: [],
  submitForApproval: true
};

export function CreateAdoptionDialog({ open, onOpenChange, onCreated }: Props) {
  const methods = useForm<AdoptionListingInput>({
    resolver: zodResolver(adoptionListingSchema),
    defaultValues: DEFAULT
  });
  const { handleSubmit, watch, setValue, reset, formState: { isSubmitting } } = methods;

  useEffect(() => { if (open) reset(DEFAULT); }, [open, reset]);

  async function onSubmit(values: AdoptionListingInput) {
    try {
      const res = await adoptionApi.create(values);
      toast.success(values.submitForApproval ? "Listing submitted for review." : "Saved as draft.");
      onCreated(res.id);
      onOpenChange(false);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't create listing.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>List a pet for adoption</DialogTitle>
          <DialogDescription>
            Your listing is reviewed by our team before going live. You can save as a draft and submit later.
          </DialogDescription>
        </DialogHeader>

        <FormProvider {...methods}>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="max-h-[60vh] overflow-y-auto pr-2">
              <AdoptionListingFormFields />
            </div>

            <label className="flex items-center gap-2 text-sm border-t pt-3">
              <Checkbox
                checked={!!watch("submitForApproval")}
                onChange={(e) => setValue("submitForApproval", e.currentTarget.checked)}
              />
              Submit for approval now (otherwise saved as Draft)
            </label>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? "Saving..." : watch("submitForApproval") ? "Submit listing" : "Save draft"}
              </Button>
            </DialogFooter>
          </form>
        </FormProvider>
      </DialogContent>
    </Dialog>
  );
}
