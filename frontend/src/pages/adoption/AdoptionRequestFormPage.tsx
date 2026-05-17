import { useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { HeartHandshake } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { PageHeader } from "@/components/common/PageHeader";
import {
  adoptionWantedSchema, type AdoptionWantedInput,
  animalTypes, animalSizes, contactPreferences, homeEnvironments
} from "@/lib/schemas";
import { wantedPostsApi } from "@/api/adoption";
import { toast } from "@/components/ui/sonner";

export function AdoptionRequestFormPage() {
  const nav = useNavigate();
  const { register, handleSubmit, watch, setValue, formState: { errors, isSubmitting } } =
    useForm<AdoptionWantedInput>({
      resolver: zodResolver(adoptionWantedSchema),
      defaultValues: { animalType: "Dog", contactPreference: "Chat" }
    });

  async function onSubmit(v: AdoptionWantedInput) {
    try {
      await wantedPostsApi.create(v);
      toast.success("Your wanted-adoption post is live.");
      nav("/adoption");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't post.");
    }
  }

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <PageHeader
        title="Looking to adopt"
        icon={HeartHandshake}
        description="Tell shelters and breeders what kind of pet you're looking for and a bit about your home."
      />

      <Card>
        <CardContent className="pt-6">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <section className="space-y-3">
              <h3 className="text-sm font-semibold">What you're looking for</h3>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>Animal type</Label>
                  <Select value={watch("animalType")} onValueChange={(v) => setValue("animalType", v as any)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div>
                  <Label htmlFor="breed">Breed preference</Label>
                  <Input id="breed" placeholder="Any" {...register("breed")} />
                </div>
                <div>
                  <Label htmlFor="minAge">Min age (months)</Label>
                  <Input id="minAge" type="number" {...register("preferredAgeMonthsMin")} />
                </div>
                <div>
                  <Label htmlFor="maxAge">Max age (months)</Label>
                  <Input id="maxAge" type="number" {...register("preferredAgeMonthsMax")} />
                </div>
                <div>
                  <Label>Preferred size</Label>
                  <Select value={watch("preferredSize") ?? ""} onValueChange={(v) => setValue("preferredSize", v as any)}>
                    <SelectTrigger><SelectValue placeholder="Any" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="">Any</SelectItem>
                      {animalSizes.map((s) => <SelectItem key={s} value={s}>{s.replace("ExtraLarge", "Extra large")}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label htmlFor="loc">Preferred location</Label>
                  <Input id="loc" placeholder="City / region" {...register("preferredLocation")} />
                </div>
              </div>
            </section>

            <Separator />

            <section className="space-y-3">
              <h3 className="text-sm font-semibold">About your home</h3>
              <div>
                <Label htmlFor="experience">Experience with pets</Label>
                <Textarea id="experience" rows={2} {...register("experienceWithPets")} />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>Home environment</Label>
                  <Select value={watch("homeEnvironment") ?? ""} onValueChange={(v) => setValue("homeEnvironment", v as any)}>
                    <SelectTrigger><SelectValue placeholder="Pick one" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="">Unspecified</SelectItem>
                      {homeEnvironments.map((h) =>
                        <SelectItem key={h} value={h}>
                          {h === "HouseWithYard" ? "House with yard" : h}
                        </SelectItem>)}
                    </SelectContent>
                  </Select>
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
                <Label htmlFor="other">Other pets at home</Label>
                <Textarea id="other" rows={2} {...register("otherPetsAtHome")} />
              </div>
              <div>
                <Label htmlFor="reason">Reason for adoption</Label>
                <Textarea id="reason" rows={2} {...register("reasonForAdoption")} />
              </div>
              <div>
                <Label htmlFor="desc">Anything else</Label>
                <Textarea id="desc" rows={3} {...register("description")} />
              </div>
            </section>

            {Object.keys(errors).length > 0 && (
              <p className="text-xs text-destructive">Please fix the highlighted fields.</p>
            )}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => nav("/adoption")}>Cancel</Button>
              <Button type="submit" disabled={isSubmitting}>{isSubmitting ? "Posting..." : "Publish post"}</Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
