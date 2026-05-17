import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Stethoscope, Video, Building2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { vetsApi, appointmentsApi, type TimeSlot } from "@/api/vets";
import { petsApi } from "@/api/pets";
import { toast } from "@/components/ui/sonner";
import { SlotPicker } from "@/components/vet/SlotPicker";

type ConsultType = "Online" | "Offline";

export function AppointmentBookingPage() {
  const { doctorId = "" } = useParams();
  const nav = useNavigate();

  const { data: doctor, isLoading } = useQuery({
    queryKey: ["vet", doctorId],
    queryFn: () => vetsApi.get(doctorId),
    enabled: !!doctorId
  });
  const { data: myPets } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });

  const [petId, setPetId] = useState<string>("");
  const [type, setType]   = useState<ConsultType>("Online");
  const [symptoms, setSymptoms] = useState("");
  const [slot, setSlot]   = useState<TimeSlot | null>(null);
  const [busy, setBusy]   = useState(false);

  async function book() {
    if (!slot) { toast.error("Pick a time slot."); return; }
    setBusy(true);
    try {
      const appt = await appointmentsApi.book({
        doctorId, timeSlotId: slot.id,
        petId: petId || undefined,
        type, symptoms: symptoms || undefined
      });
      toast.success("Appointment requested.");
      nav(`/appointments?highlight=${appt.id}`);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Booking failed.");
    } finally { setBusy(false); }
  }

  if (isLoading) {
    return <div className="max-w-3xl mx-auto space-y-3"><Skeleton className="h-32" /><Skeleton className="h-64" /></div>;
  }
  if (!doctor) {
    return <div className="max-w-3xl mx-auto"><p className="text-muted-foreground">Doctor not found.</p></div>;
  }

  const ConsultIcon = type === "Online" ? Video : Building2;

  return (
    <div className="max-w-3xl mx-auto space-y-4">
      <Button variant="ghost" size="sm" asChild>
        <Link to={`/vets/${doctorId}`}><ArrowLeft className="h-4 w-4 mr-1" /> Back to doctor</Link>
      </Button>

      <Card>
        <CardHeader className="flex flex-row gap-3 items-center space-y-0">
          <Avatar><AvatarImage src={doctor.avatarUrl ?? undefined} /><AvatarFallback>{doctor.name[0]}</AvatarFallback></Avatar>
          <div className="flex-1">
            <CardTitle className="text-base">Dr. {doctor.name}</CardTitle>
            <p className="text-xs text-muted-foreground">{doctor.specialty ?? "General practice"}{doctor.city ? ` · ${doctor.city}` : ""}</p>
          </div>
          <Badge variant="muted">${doctor.consultationFee.toFixed(2)}</Badge>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label>Pet (optional)</Label>
              <Select value={petId} onValueChange={setPetId}>
                <SelectTrigger><SelectValue placeholder="Choose a pet" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="">No specific pet</SelectItem>
                  {(myPets ?? []).map((p) => <SelectItem key={p.id} value={p.id}>{p.name} ({p.animalType})</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Consultation type</Label>
              <Select value={type} onValueChange={(v) => { setType(v as ConsultType); setSlot(null); }}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {doctor.onlineAvailable && <SelectItem value="Online">Online</SelectItem>}
                  {doctor.offlineAvailable && <SelectItem value="Offline">In clinic</SelectItem>}
                </SelectContent>
              </Select>
            </div>
          </div>

          <Separator />

          <div>
            <Label className="mb-2 flex items-center gap-1"><ConsultIcon className="h-4 w-4" /> Pick a time</Label>
            <SlotPicker
              doctorId={doctorId}
              selectedSlotId={slot?.id ?? null}
              consultationType={type}
              onSelect={setSlot}
            />
          </div>

          <Separator />

          <div>
            <Label htmlFor="symptoms">Symptoms / reason (optional)</Label>
            <Textarea id="symptoms" rows={3} value={symptoms} onChange={(e) => setSymptoms(e.target.value)} />
          </div>

          <div className="rounded-md border bg-muted/40 p-3 text-sm space-y-1">
            <p className="font-medium flex items-center gap-1"><Stethoscope className="h-4 w-4 text-primary" /> Summary</p>
            <p className="text-muted-foreground">Doctor: <span className="text-foreground">Dr. {doctor.name}</span></p>
            <p className="text-muted-foreground">Type: <span className="text-foreground">{type}</span></p>
            <p className="text-muted-foreground">Time: <span className="text-foreground">{slot ? new Date(slot.startUtc).toLocaleString() : "—"}</span></p>
            <p className="text-muted-foreground">Fee: <span className="text-foreground">${doctor.consultationFee.toFixed(2)}</span></p>
          </div>

          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => nav(-1)}>Cancel</Button>
            <Button onClick={book} disabled={!slot || busy}>{busy ? "Booking..." : "Confirm booking"}</Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
