import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Crosshair, MapPin, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/sonner";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { useGeolocation } from "@/hooks/useGeolocation";
import { trackingApi, type TrackingStatus } from "@/api/commercePlus";

// Statuses a courier can report from the road.
const STATUSES: { value: TrackingStatus; label: string }[] = [
  { value: "PickedUp", label: "Picked up" },
  { value: "InTransit", label: "In transit" },
  { value: "OutForDelivery", label: "Out for delivery" },
  { value: "Delivered", label: "Delivered" },
  { value: "DeliveryFailed", label: "Delivery attempt failed" },
  { value: "Exception", label: "Delay / exception" },
];

const errMsg = (e: any, fallback: string) => e?.response?.data?.error?.message ?? fallback;

interface Props {
  orderId: string;
}

/** Courier tools for one assigned order: add a tracking checkpoint and record proof of delivery. */
export function CourierUpdatePanel({ orderId }: Props) {
  const qc = useQueryClient();
  const geo = useGeolocation();

  const [status, setStatus] = useState<TrackingStatus>("InTransit");
  const [locationText, setLocationText] = useState("");
  const [notes, setNotes] = useState("");
  const [useGps, setUseGps] = useState(false);

  const [recipientName, setRecipientName] = useState("");
  const [otp, setOtp] = useState("");
  const [photoUrl, setPhotoUrl] = useState<string | null>(null);

  const refresh = () => qc.invalidateQueries({ queryKey: ["order-tracking", orderId] });

  const checkpoint = useMutation({
    mutationFn: () =>
      trackingApi.addCheckpoint(orderId, {
        status,
        locationText: locationText.trim() || null,
        description: notes.trim() || null,
        latitude: useGps && geo.coords ? geo.coords.lat : null,
        longitude: useGps && geo.coords ? geo.coords.lng : null,
      }),
    onSuccess: () => {
      toast.success("Checkpoint added");
      setLocationText("");
      setNotes("");
      refresh();
      qc.invalidateQueries({ queryKey: ["deliveries-mine-active"] });
    },
    onError: (e) => toast.error(errMsg(e, "Could not add checkpoint.")),
  });

  const proof = useMutation({
    mutationFn: () =>
      trackingApi.addProof(orderId, {
        recipientName: recipientName.trim() || null,
        signatureName: recipientName.trim() || null,
        otpCode: otp.trim() || null,
        photoUrl,
      }),
    onSuccess: () => {
      toast.success("Proof of delivery saved");
      setOtp("");
      refresh();
    },
    onError: (e) => toast.error(errMsg(e, "Could not save proof.")),
  });

  function toggleGps() {
    if (!useGps) geo.request();
    setUseGps((v) => !v);
  }

  const gpsHint =
    !useGps ? null
    : geo.status === "locating" ? "Locating..."
    : geo.status === "denied" ? "Location permission denied."
    : geo.status === "unavailable" ? "Location unavailable on this device."
    : geo.coords ? `${geo.coords.lat.toFixed(5)}, ${geo.coords.lng.toFixed(5)}` : null;

  const canProof = !!(recipientName.trim() || otp.trim() || photoUrl);

  return (
    <div className="space-y-4 rounded-md border p-3 text-sm">
      <div className="space-y-2">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground flex items-center gap-1">
          <MapPin className="h-3 w-3" /> Add checkpoint
        </p>
        <div className="grid gap-2 sm:grid-cols-2">
          <div className="space-y-1">
            <Label htmlFor={`cp-status-${orderId}`}>Status</Label>
            <Select value={status} onValueChange={(v) => setStatus(v as TrackingStatus)}>
              <SelectTrigger id={`cp-status-${orderId}`}><SelectValue /></SelectTrigger>
              <SelectContent>
                {STATUSES.map((s) => <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor={`cp-loc-${orderId}`}>Location</Label>
            <Input
              id={`cp-loc-${orderId}`}
              value={locationText}
              maxLength={200}
              placeholder="e.g. Dhaka sorting hub"
              onChange={(e) => setLocationText(e.target.value)}
            />
          </div>
        </div>
        <div className="space-y-1">
          <Label htmlFor={`cp-notes-${orderId}`}>Notes (optional)</Label>
          <Textarea
            id={`cp-notes-${orderId}`}
            rows={2}
            maxLength={500}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
          />
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" size="sm" variant={useGps ? "secondary" : "outline"} onClick={toggleGps}>
            <Crosshair className="h-3.5 w-3.5 mr-1" /> {useGps ? "GPS attached" : "Attach GPS"}
          </Button>
          {gpsHint && <span className="text-xs text-muted-foreground">{gpsHint}</span>}
          <Button
            type="button"
            size="sm"
            className="ml-auto"
            disabled={checkpoint.isPending}
            onClick={() => checkpoint.mutate()}
          >
            {checkpoint.isPending ? "Saving..." : "Add checkpoint"}
          </Button>
        </div>
      </div>

      <div className="space-y-2 border-t pt-3">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground flex items-center gap-1">
          <ShieldCheck className="h-3 w-3" /> Proof of delivery
        </p>
        <div className="grid gap-2 sm:grid-cols-2">
          <div className="space-y-1">
            <Label htmlFor={`pf-name-${orderId}`}>Recipient name</Label>
            <Input
              id={`pf-name-${orderId}`}
              value={recipientName}
              maxLength={120}
              onChange={(e) => setRecipientName(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor={`pf-otp-${orderId}`}>Delivery OTP</Label>
            <Input
              id={`pf-otp-${orderId}`}
              value={otp}
              inputMode="numeric"
              maxLength={12}
              autoComplete="off"
              onChange={(e) => setOtp(e.target.value)}
            />
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <FileUploadButton label={photoUrl ? "Replace photo" : "Upload photo"} onUploaded={(url) => setPhotoUrl(url)} />
          {photoUrl && (
            <a href={photoUrl} target="_blank" rel="noreferrer" className="text-xs text-primary hover:underline">
              View photo
            </a>
          )}
          <Button
            type="button"
            size="sm"
            className="ml-auto"
            disabled={proof.isPending || !canProof}
            onClick={() => proof.mutate()}
          >
            {proof.isPending ? "Saving..." : "Save proof"}
          </Button>
        </div>
      </div>
    </div>
  );
}
