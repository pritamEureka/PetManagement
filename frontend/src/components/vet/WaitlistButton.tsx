import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Hourglass } from "lucide-react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/sonner";
import { waitlistApi } from "@/api/vetPlus";

interface Props {
  doctorId: string;
  doctorName?: string;
  petId?: string | null;
  size?: "default" | "sm" | "lg";
  variant?: "default" | "outline" | "secondary" | "ghost";
}

/** "Notify me" button for fully booked vets: collects a preferred date range and joins the waitlist. */
export function WaitlistButton({ doctorId, doctorName, petId, size = "sm", variant = "outline" }: Props) {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [mode, setMode] = useState<"" | "Online" | "Offline">("");
  const [note, setNote] = useState("");

  const join = useMutation({
    mutationFn: () =>
      waitlistApi.join({
        doctorId,
        petId: petId ?? null,
        preferredFrom: from || null,
        preferredTo: to || null,
        consultationType: mode || null,
        note: note.trim() || null
      }),
    onSuccess: (e) => {
      toast.success(`You're #${e.position} on the waitlist. We'll notify you when a slot opens.`);
      qc.invalidateQueries({ queryKey: ["waitlists", "mine"] });
      setOpen(false);
    }
  });

  const invalidRange = !!from && !!to && to < from;

  return (
    <>
      <Button type="button" size={size} variant={variant} onClick={() => setOpen(true)}>
        <Hourglass className="mr-1.5 h-4 w-4" /> Join waitlist
      </Button>
      <Dialog open={open} onOpenChange={(v) => !join.isPending && setOpen(v)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Join the waitlist</DialogTitle>
            <DialogDescription>
              {doctorName ? `We'll notify you if ${doctorName} has a cancellation.` : "We'll notify you if a slot opens up."}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="wl-from">Earliest date</Label>
                <Input id="wl-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="wl-to">Latest date</Label>
                <Input id="wl-to" type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} />
              </div>
            </div>
            {invalidRange && <p className="text-sm text-destructive">The latest date must not be before the earliest date.</p>}
            <div className="space-y-1.5">
              <Label htmlFor="wl-mode">Consultation type</Label>
              <select
                id="wl-mode"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                value={mode}
                onChange={(e) => setMode(e.target.value as "" | "Online" | "Offline")}
              >
                <option value="">Any</option>
                <option value="Online">Online</option>
                <option value="Offline">In clinic</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="wl-note">Note (optional)</Label>
              <Textarea id="wl-note" maxLength={500} rows={2} value={note} onChange={(e) => setNote(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={join.isPending}>Cancel</Button>
            <Button onClick={() => join.mutate()} disabled={join.isPending || invalidRange}>
              {join.isPending ? "Joining…" : "Join waitlist"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
