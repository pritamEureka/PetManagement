import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useQuery } from "@tanstack/react-query";
import { Stethoscope, FileText, Plus, Trash2 } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { PageHeader } from "@/components/common/PageHeader";
import {
  doctorRegistrationSchema, type DoctorRegistrationInput,
  animalTypes, consultationTypes
} from "@/lib/schemas";
import { vetsApi, type CredentialDocument } from "@/api/vets";
import { toast } from "@/components/ui/sonner";

const DEFAULTS: DoctorRegistrationInput = {
  licenseNumber: "", specialty: "", experienceYears: undefined,
  about: "", clinicName: "", clinicAddress: "", city: "", country: "",
  consultationFee: 0, consultationType: "Both",
  onlineAvailable: true, offlineAvailable: true,
  autoConfirmAppointments: false, defaultSlotMinutes: 30, cancellationCutoffHours: 12,
  supportedAnimalTypes: ["Dog"], specialtyIds: []
};

export function DoctorRegistrationPage() {
  const nav = useNavigate();
  const { data: existing } = useQuery({ queryKey: ["vet-me"], queryFn: () => vetsApi.me().catch(() => null) });
  const { data: specialties } = useQuery({ queryKey: ["vet-specialties"], queryFn: vetsApi.specialties });
  const { data: credentials, refetch: refetchCreds } = useQuery({
    queryKey: ["vet-credentials"],
    queryFn: () => vetsApi.myCredentials().catch(() => [] as CredentialDocument[]),
    enabled: !!existing
  });

  const { register, handleSubmit, watch, setValue, reset, formState: { errors, isSubmitting } } =
    useForm<DoctorRegistrationInput>({
      resolver: zodResolver(doctorRegistrationSchema),
      defaultValues: DEFAULTS
    });

  useEffect(() => {
    if (existing) {
      reset({
        licenseNumber: existing.licenseNumber,
        specialty: existing.specialty ?? "",
        experienceYears: existing.experienceYears ?? undefined,
        about: existing.about ?? "",
        clinicName: existing.clinicName ?? "",
        clinicAddress: existing.clinicAddress ?? "",
        city: existing.city ?? "",
        country: existing.country ?? "",
        consultationFee: existing.consultationFee,
        consultationType: existing.consultationType as any,
        onlineAvailable: existing.onlineAvailable,
        offlineAvailable: existing.offlineAvailable,
        autoConfirmAppointments: existing.autoConfirmAppointments,
        defaultSlotMinutes: existing.defaultSlotMinutes,
        cancellationCutoffHours: existing.cancellationCutoffHours,
        supportedAnimalTypes: existing.supportedAnimalTypes as any,
        specialtyIds: []
      });
    }
  }, [existing, reset]);

  async function onSubmit(values: DoctorRegistrationInput) {
    try {
      if (existing) {
        await vetsApi.update(values);
        toast.success("Profile updated.");
      } else {
        await vetsApi.register(values);
        toast.success("Submitted for approval — we'll email you when reviewed.");
      }
      nav("/dashboard/vet");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Save failed.");
    }
  }

  const animals = watch("supportedAnimalTypes") ?? [];
  function toggleAnimal(t: (typeof animalTypes)[number]) {
    const next = new Set(animals);
    next.has(t) ? next.delete(t) : next.add(t);
    setValue("supportedAnimalTypes", Array.from(next) as any);
  }
  const selectedSpecialtyIds = watch("specialtyIds") ?? [];
  function toggleSpecialty(id: string) {
    const next = new Set(selectedSpecialtyIds);
    next.has(id) ? next.delete(id) : next.add(id);
    setValue("specialtyIds", Array.from(next));
  }

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <PageHeader title={existing ? "Edit my vet profile" : "Become a Pawzaroo vet"} icon={Stethoscope}
        description={existing
          ? `Status: ${existing.approvalStatus}. Edits won't change your approval state.`
          : "Submit your profile and credentials — we review every application within 72h."} />

      <Card>
        <CardContent className="pt-6">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            {/* Credentials */}
            <section className="space-y-3">
              <h3 className="text-sm font-semibold">Practice credentials</h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <Label htmlFor="lic">License number</Label>
                  <Input id="lic" {...register("licenseNumber")} disabled={!!existing} />
                  {errors.licenseNumber && <p className="text-xs text-destructive mt-1">{errors.licenseNumber.message}</p>}
                </div>
                <div>
                  <Label htmlFor="exp">Years of experience</Label>
                  <Input id="exp" type="number" {...register("experienceYears")} />
                </div>
                <div>
                  <Label htmlFor="spec">Primary specialty</Label>
                  <Input id="spec" placeholder="e.g. General Practice" {...register("specialty")} />
                </div>
              </div>
              <div>
                <Label htmlFor="about">About</Label>
                <Textarea id="about" rows={3} {...register("about")} />
              </div>
            </section>

            <Separator />

            {/* Clinic */}
            <section className="space-y-3">
              <h3 className="text-sm font-semibold">Clinic / location</h3>
              <div className="grid grid-cols-2 gap-3">
                <div><Label htmlFor="clinic">Clinic name</Label><Input id="clinic" {...register("clinicName")} /></div>
                <div><Label htmlFor="addr">Address</Label><Input id="addr" {...register("clinicAddress")} /></div>
                <div><Label htmlFor="city">City</Label><Input id="city" {...register("city")} /></div>
                <div><Label htmlFor="country">Country</Label><Input id="country" {...register("country")} /></div>
              </div>
            </section>

            <Separator />

            {/* Practice settings */}
            <section className="space-y-3">
              <h3 className="text-sm font-semibold">Practice settings</h3>
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <Label>Consultation type</Label>
                  <Select value={watch("consultationType")} onValueChange={(v) => setValue("consultationType", v as any)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{consultationTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div><Label htmlFor="fee">Consultation fee</Label><Input id="fee" type="number" step="0.01" {...register("consultationFee")} /></div>
                <div><Label htmlFor="slot">Slot length (min)</Label><Input id="slot" type="number" {...register("defaultSlotMinutes")} /></div>
                <div>
                  <Label htmlFor="cutoff">Cancellation cutoff (h)</Label>
                  <Input id="cutoff" type="number" {...register("cancellationCutoffHours")} />
                </div>
              </div>
              <div className="flex flex-wrap gap-4">
                <label className="flex items-center gap-2 text-sm"><Checkbox checked={!!watch("onlineAvailable")} onChange={(e) => setValue("onlineAvailable", e.currentTarget.checked)} /> Available online</label>
                <label className="flex items-center gap-2 text-sm"><Checkbox checked={!!watch("offlineAvailable")} onChange={(e) => setValue("offlineAvailable", e.currentTarget.checked)} /> In clinic</label>
                <label className="flex items-center gap-2 text-sm"><Checkbox checked={!!watch("autoConfirmAppointments")} onChange={(e) => setValue("autoConfirmAppointments", e.currentTarget.checked)} /> Auto-confirm bookings</label>
              </div>
            </section>

            <Separator />

            {/* Animal expertise */}
            <section className="space-y-3">
              <h3 className="text-sm font-semibold">Animal types you treat</h3>
              <div className="flex flex-wrap gap-1.5">
                {animalTypes.map((t) => {
                  const on = animals.includes(t);
                  return (
                    <button key={t} type="button" onClick={() => toggleAnimal(t)}
                      className={`rounded-full border px-3 py-1 text-xs ${on ? "bg-primary text-primary-foreground border-primary" : "hover:bg-accent"}`}>
                      {t}
                    </button>
                  );
                })}
              </div>
              {errors.supportedAnimalTypes && <p className="text-xs text-destructive">{errors.supportedAnimalTypes.message}</p>}
            </section>

            {/* Specialties */}
            {(specialties?.length ?? 0) > 0 && (
              <>
                <Separator />
                <section className="space-y-3">
                  <h3 className="text-sm font-semibold">Specialties</h3>
                  <div className="flex flex-wrap gap-1.5">
                    {specialties!.map((s) => {
                      const on = selectedSpecialtyIds.includes(s.id);
                      return (
                        <button key={s.id} type="button" onClick={() => toggleSpecialty(s.id)}
                          className={`rounded-full border px-3 py-1 text-xs ${on ? "bg-primary text-primary-foreground border-primary" : "hover:bg-accent"}`}>
                          {s.name}
                        </button>
                      );
                    })}
                  </div>
                </section>
              </>
            )}

            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variant="outline" onClick={() => nav(-1)}>Cancel</Button>
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? "Saving..." : existing ? "Save changes" : "Submit application"}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {/* Credentials uploader (only post-registration) */}
      {existing && (
        <Card>
          <CardContent className="pt-6 space-y-4">
            <h3 className="text-sm font-semibold flex items-center gap-2"><FileText className="h-4 w-4" /> Credential documents</h3>
            <CredentialAdder onAdded={refetchCreds} />
            {credentials && credentials.length > 0 && (
              <div className="space-y-2">
                {credentials.map((c) => <CredentialRow key={c.id} c={c} />)}
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function CredentialAdder({ onAdded }: { onAdded: () => void }) {
  const [form, setForm] = useState({ kind: 1, title: "", fileUrl: "", issuingAuthority: "", documentNumber: "", issuedOn: "", expiresOn: "" });
  const [busy, setBusy] = useState(false);
  async function submit() {
    if (!form.title.trim() || !form.fileUrl.trim()) { toast.error("Title and file URL required."); return; }
    setBusy(true);
    try {
      await vetsApi.addCredential({
        kind: form.kind, title: form.title.trim(), fileUrl: form.fileUrl.trim(),
        issuingAuthority: form.issuingAuthority || undefined,
        documentNumber: form.documentNumber || undefined,
        issuedOn: form.issuedOn || undefined,
        expiresOn: form.expiresOn || undefined
      });
      toast.success("Document uploaded.");
      setForm({ kind: 1, title: "", fileUrl: "", issuingAuthority: "", documentNumber: "", issuedOn: "", expiresOn: "" });
      onAdded();
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Upload failed.");
    } finally { setBusy(false); }
  }
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 rounded-md border p-3">
      <div className="sm:col-span-2"><Label>Title</Label><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} /></div>
      <div className="sm:col-span-2"><Label>File URL</Label><Input placeholder="Use /api/media/presign first" value={form.fileUrl} onChange={(e) => setForm({ ...form, fileUrl: e.target.value })} /></div>
      <div>
        <Label>Kind</Label>
        <Select value={String(form.kind)} onValueChange={(v) => setForm({ ...form, kind: Number(v) })}>
          <SelectTrigger><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="1">License</SelectItem>
            <SelectItem value="2">Board certification</SelectItem>
            <SelectItem value="3">Diploma</SelectItem>
            <SelectItem value="4">Insurance</SelectItem>
            <SelectItem value="99">Other</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div><Label>Document #</Label><Input value={form.documentNumber} onChange={(e) => setForm({ ...form, documentNumber: e.target.value })} /></div>
      <div><Label>Issued on</Label><Input type="date" value={form.issuedOn} onChange={(e) => setForm({ ...form, issuedOn: e.target.value })} /></div>
      <div><Label>Expires on</Label><Input type="date" value={form.expiresOn} onChange={(e) => setForm({ ...form, expiresOn: e.target.value })} /></div>
      <div className="col-span-2 flex justify-end">
        <Button onClick={submit} disabled={busy}><Plus className="h-4 w-4 mr-1" /> Add document</Button>
      </div>
    </div>
  );
}

function CredentialRow({ c }: { c: CredentialDocument }) {
  return (
    <div className="flex items-center justify-between gap-2 rounded-md border px-3 py-2">
      <div className="flex items-center gap-2 min-w-0">
        <FileText className="h-4 w-4 text-primary flex-shrink-0" />
        <div className="min-w-0">
          <a href={c.fileUrl} target="_blank" rel="noreferrer" className="text-sm font-medium hover:underline truncate block">{c.title}</a>
          <p className="text-[10px] text-muted-foreground">{c.documentNumber ?? ""}{c.expiresOn ? ` · expires ${c.expiresOn}` : ""}</p>
        </div>
      </div>
      {c.verified ? <Badge variant={statusBadgeVariant("Verified")}>Verified</Badge> : <Badge variant={statusBadgeVariant("Pending")}>Pending verification</Badge>}
    </div>
  );
}
