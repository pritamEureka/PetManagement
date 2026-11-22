import { useState } from "react";
import { CalendarPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { calendarApi } from "@/api/vetPlus";

interface Props {
  /** Export one appointment; omit to export all of the user's upcoming appointments. */
  appointmentId?: string;
  label?: string;
  size?: "default" | "sm" | "lg" | "icon";
  variant?: "default" | "outline" | "ghost" | "secondary";
}

/** Downloads an RFC 5545 .ics file that any calendar app can import. */
export function AddToCalendarButton({ appointmentId, label, size = "sm", variant = "outline" }: Props) {
  const [busy, setBusy] = useState(false);

  async function go() {
    try {
      setBusy(true);
      if (appointmentId) await calendarApi.downloadAppointment(appointmentId);
      else await calendarApi.downloadUpcoming();
    } finally {
      setBusy(false);
    }
  }

  return (
    <Button type="button" size={size} variant={variant} disabled={busy} onClick={go}>
      <CalendarPlus className="mr-1.5 h-4 w-4" />
      {label ?? (appointmentId ? "Add to calendar" : "Export upcoming (.ics)")}
    </Button>
  );
}
