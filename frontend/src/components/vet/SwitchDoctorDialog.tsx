import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { switchApi, type SwitchCandidate } from "@/api/clinicPlus";

interface Props { appointmentId: string; open: boolean; onOpenChange: (open: boolean) => void; }

/** Lets the owner move an upcoming appointment to another doctor of the same specialty (no cancellation penalty). */
export function SwitchDoctorDialog({ appointmentId, open, onOpenChange }: Props) {
  const qc = useQueryClient();
  const [picked, setPicked] = useState<SwitchCandidate | null>(null);
  const { data, isLoading } = useQuery({
    queryKey: ["appointments", appointmentId, "switch-candidates"],
    queryFn: () => switchApi.candidates(appointmentId),
    enabled: open
  });
  const doSwitch = useMutation({
    mutationFn: (c: SwitchCandidate) => switchApi.switch(appointmentId, c.doctorId, c.timeSlotId),
    onSuccess: () => {
      toast.success("Your appointment was moved to the new doctor.");
      qc.invalidateQueries({ queryKey: ["appointments"] });
      onOpenChange(false);
    }
  });

  return (
    <Dialog open={open} onOpenChange={(v) => !doSwitch.isPending && onOpenChange(v)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Switch doctor</DialogTitle>
          <DialogDescription>Pick another doctor with the same specialty and an open slot. The current booking is cancelled without a fee.</DialogDescription>
        </DialogHeader>
        {isLoading ? <Skeleton className="h-24" /> : !data || data.length === 0 ? (
          <p className="text-sm text-muted-foreground">No other doctors have a matching open slot right now.</p>
        ) : (
          <ul className="max-h-72 overflow-auto space-y-2">
            {data.map((c) => (
              <li key={c.timeSlotId}>
                <button type="button" onClick={() => setPicked(c)} aria-pressed={picked?.timeSlotId === c.timeSlotId}
                  className={`w-full text-left rounded-md border p-2 text-sm ${picked?.timeSlotId === c.timeSlotId ? "border-primary bg-primary/5" : ""}`}>
                  <span className="font-medium">Dr. {c.doctorName}</span>
                  <span className="text-muted-foreground"> · {c.specialty ?? "General"} · fee {c.fee} · rating {c.ratingAverage.toFixed(1)}</span>
                  <br /><span className="text-xs">{new Date(c.startUtc).toLocaleString()}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <Button disabled={!picked || doSwitch.isPending} onClick={() => picked && doSwitch.mutate(picked)}>Switch to this doctor</Button>
      </DialogContent>
    </Dialog>
  );
}
