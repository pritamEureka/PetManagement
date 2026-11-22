import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { DoorOpen, UserCheck, Video } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { telemedicineApi } from "@/api/vetPlus";

/** Vet dashboard widget: patients who checked in to the online waiting room, polled every 10s. */
export function WaitingRoomWidget() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ["telemedicine", "waiting-room"],
    queryFn: telemedicineApi.waiting,
    refetchInterval: 10_000
  });

  const admit = useMutation({
    mutationFn: (appointmentId: string) => telemedicineApi.admit(appointmentId),
    onSuccess: () => {
      toast.success("Patient admitted.");
      qc.invalidateQueries({ queryKey: ["telemedicine", "waiting-room"] });
    },
    onError: (err: any) => toast.error(err?.response?.data?.error?.message ?? "Couldn't admit patient.")
  });

  const patients = data ?? [];

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base flex items-center gap-2">
          <DoorOpen className="h-4 w-4 text-primary" /> Waiting room
          {patients.length > 0 && <span className="text-xs font-normal text-muted-foreground">({patients.length})</span>}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {isLoading ? (
          <p className="text-sm text-muted-foreground">Loading…</p>
        ) : patients.length === 0 ? (
          <p className="text-sm text-muted-foreground">No patients are waiting right now.</p>
        ) : (
          patients.map((p) => (
            <div key={p.appointmentId} className="flex flex-col sm:flex-row sm:items-center gap-2 rounded-md border p-2">
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium truncate">
                  {p.patientName}{p.petName ? ` · ${p.petName}` : ""}
                </p>
                <p className="text-xs text-muted-foreground">
                  Appointment {new Date(p.scheduledAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                  {" · "}waiting since {new Date(p.checkedInAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                </p>
                {p.preConsultNote && <p className="text-xs mt-1 line-clamp-2">{p.preConsultNote}</p>}
              </div>
              <div className="flex items-center gap-2">
                <Button size="sm" onClick={() => admit.mutate(p.appointmentId)}
                  disabled={admit.isPending && admit.variables === p.appointmentId}>
                  <UserCheck className="h-3.5 w-3.5 mr-1" /> Admit
                </Button>
                <Button size="sm" variant="outline" asChild>
                  <Link to={`/appointments/${p.appointmentId}/room`}><Video className="h-3.5 w-3.5 mr-1" /> Room</Link>
                </Button>
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
