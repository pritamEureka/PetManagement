import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { seriesApi, type BookSeriesResult, type Recurrence } from "@/api/vetPlus";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  doctorId: string;
  /** The first (already chosen) slot; later occurrences repeat its start time. */
  timeSlotId: string | null;
  petId?: string | null;
  type: "Online" | "Offline";
  symptoms?: string | null;
}

const fmt = (iso: string) => new Date(iso).toLocaleString();

/** Books N repeat visits at the same time of day and reports which could not be booked. */
export function RecurringBookingDialog({ open, onOpenChange, doctorId, timeSlotId, petId, type, symptoms }: Props) {
  const qc = useQueryClient();
  const [frequency, setFrequency] = useState<Recurrence>("Weekly");
  const [occurrences, setOccurrences] = useState(4);
  const [result, setResult] = useState<BookSeriesResult | null>(null);

  useEffect(() => {
    if (open) setResult(null);
  }, [open]);

  const book = useMutation({
    mutationFn: () =>
      seriesApi.book({
        doctorId, timeSlotId: timeSlotId!, petId: petId ?? null, type,
        symptoms: symptoms ?? null, frequency, occurrences
      }),
    onSuccess: (r) => {
      setResult(r);
      qc.invalidateQueries({ queryKey: ["appointments"] });
    }
  });

  const valid = !!timeSlotId && occurrences >= 2 && occurrences <= 12;

  return (
    <Dialog open={open} onOpenChange={(v) => !book.isPending && onOpenChange(v)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Book a recurring visit</DialogTitle>
          <DialogDescription>
            Repeats your selected slot at the same time of day. Occurrences with no free slot are skipped and listed below.
          </DialogDescription>
        </DialogHeader>

        {!result ? (
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label htmlFor="rec-freq">Repeat</Label>
              <select
                id="rec-freq"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                value={frequency}
                onChange={(e) => setFrequency(e.target.value as Recurrence)}
              >
                <option value="Weekly">Weekly</option>
                <option value="Biweekly">Every 2 weeks</option>
                <option value="Monthly">Monthly</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="rec-n">Number of visits (2 to 12, including the first)</Label>
              <Input
                id="rec-n" type="number" min={2} max={12} value={occurrences}
                onChange={(e) => setOccurrences(Math.max(0, Math.floor(Number(e.target.value) || 0)))}
              />
            </div>
            {!timeSlotId && <p className="text-sm text-destructive">Pick a first time slot before booking a series.</p>}
          </div>
        ) : (
          <div className="space-y-3 text-sm">
            <div>
              <div className="font-medium">Booked ({result.booked.length})</div>
              <ul className="list-disc pl-5">
                {result.booked.map((b) => <li key={b.appointmentId}>#{b.sequence}: {fmt(b.scheduledAt)}</li>)}
              </ul>
            </div>
            {result.failed.length > 0 && (
              <div>
                <div className="font-medium text-destructive">Could not book ({result.failed.length})</div>
                <ul className="list-disc pl-5">
                  {result.failed.map((f) => <li key={f.sequence}>#{f.sequence}: {fmt(f.desiredStartUtc)} - {f.reason}</li>)}
                </ul>
              </div>
            )}
          </div>
        )}

        <DialogFooter>
          {!result ? (
            <>
              <Button variant="outline" onClick={() => onOpenChange(false)} disabled={book.isPending}>Cancel</Button>
              <Button onClick={() => book.mutate()} disabled={!valid || book.isPending}>
                {book.isPending ? "Booking…" : `Book ${occurrences} visits`}
              </Button>
            </>
          ) : (
            <Button onClick={() => onOpenChange(false)}>Done</Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
