import { Badge } from "@/components/ui/badge";
import type { AppointmentStatus } from "@/api/vets";

const MAP: Record<AppointmentStatus, { label: string; variant: "default"|"secondary"|"muted"|"destructive"|"outline" }> = {
  Draft:               { label: "Draft",               variant: "muted" },
  PendingPayment:      { label: "Pending payment",     variant: "outline" },
  PendingConfirmation: { label: "Pending confirmation",variant: "secondary" },
  Confirmed:           { label: "Confirmed",           variant: "default" },
  Rescheduled:         { label: "Rescheduled",         variant: "secondary" },
  CancelledByUser:     { label: "Cancelled (you)",     variant: "destructive" },
  CancelledByDoctor:   { label: "Cancelled (doctor)",  variant: "destructive" },
  Completed:           { label: "Completed",           variant: "default" },
  NoShow:              { label: "No-show",             variant: "destructive" },
  Refunded:            { label: "Refunded",            variant: "muted" }
};

export function AppointmentStatusBadge({ status }: { status: AppointmentStatus }) {
  const m = MAP[status];
  return <Badge variant={m.variant}>{m.label}</Badge>;
}
