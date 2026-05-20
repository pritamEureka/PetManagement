import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import type { AppointmentStatus } from "@/api/vets";

const LABEL: Record<AppointmentStatus, string> = {
  Draft: "Draft",
  PendingPayment: "Pending payment",
  PendingConfirmation: "Pending confirmation",
  Confirmed: "Confirmed",
  Rescheduled: "Rescheduled",
  CancelledByUser: "Cancelled (you)",
  CancelledByDoctor: "Cancelled (doctor)",
  Completed: "Completed",
  NoShow: "No-show",
  Refunded: "Refunded"
};

export function AppointmentStatusBadge({ status }: { status: AppointmentStatus }) {
  return <Badge variant={statusBadgeVariant(status)}>{LABEL[status]}</Badge>;
}
