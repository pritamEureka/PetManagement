import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { CalendarPlus } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { SlotPicker } from "@/components/vet/SlotPicker";
import { followUpApi, type FollowUp } from "@/api/clinicPlus";

/** Owner: follow-up visits your vet recommended, with doctor and pet pre-filled. */
export function FollowUpPanel() {
  const qc = useQueryClient();
  const { data } = useQuery({ queryKey: ["follow-ups", "mine"], queryFn: followUpApi.mine });
  const [target, setTarget] = useState<FollowUp | null>(null);
  const [slotId, setSlotId] = useState<string | null>(null);

  const book = useMutation({
    mutationFn: () => followUpApi.book(target!.sourceAppointmentId, slotId!),
    onSuccess: () => {
      toast.success("Follow-up booked.");
      setTarget(null); setSlotId(null);
      qc.invalidateQueries({ queryKey: ["follow-ups", "mine"] });
      qc.invalidateQueries({ queryKey: ["appointments"] });
    }
  });
  const dismiss = useMutation({
    mutationFn: (id: string) => followUpApi.dismiss(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["follow-ups", "mine"] })
  });

  if (!data || data.length === 0) return null;
  return (
    <Card>
      <CardHeader><CardTitle className="flex items-center gap-2 text-base"><CalendarPlus className="h-4 w-4" /> Recommended follow-ups</CardTitle></CardHeader>
      <CardContent className="space-y-2 text-sm">
        {data.map((f) => (
          <div key={f.id} className="flex flex-wrap items-center justify-between gap-2 border rounded-md px-2 py-2">
            <span>Dr. {f.doctorName}{f.petName ? ` for ${f.petName}` : ""} · due around {f.dueDate}{f.note ? ` · ${f.note}` : ""}</span>
            <span className="flex gap-2">
              <Button size="sm" onClick={() => setTarget(f)}>Book</Button>
              <Button size="sm" variant="ghost" onClick={() => dismiss.mutate(f.id)}>Dismiss</Button>
            </span>
          </div>
        ))}
      </CardContent>
      <Dialog open={!!target} onOpenChange={(v) => !v && !book.isPending && setTarget(null)}>
        <DialogContent>
          <DialogHeader><DialogTitle>Book follow-up</DialogTitle><DialogDescription>Dr. {target?.doctorName}{target?.petName ? ` · ${target.petName}` : ""}</DialogDescription></DialogHeader>
          {target && <SlotPicker doctorId={target.doctorId} selectedSlotId={slotId} onSelect={(s) => setSlotId(s.id)} />}
          <Button disabled={!slotId || book.isPending} onClick={() => book.mutate()}>Confirm booking</Button>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
