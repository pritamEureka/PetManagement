import { useState } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  title: string;
  reasons?: string[];
  onSubmit: (reason: string) => Promise<void> | void;
  isLoading?: boolean;
}

const DEFAULT_REASONS = [
  "Insufficient documentation",
  "Information mismatch",
  "Suspected fraud",
  "Violates community guidelines",
  "Duplicate / spam",
  "Other"
];

/**
 * Two-field reject modal — common pattern across every approval queue.
 * Use a structured reason from the dropdown plus an optional free-text note,
 * then call <c>onSubmit</c> with the combined string.
 */
export function RejectReasonDialog({
  open, onOpenChange, title, reasons = DEFAULT_REASONS, onSubmit, isLoading
}: Props) {
  const [reason, setReason] = useState(reasons[0]);
  const [notes, setNotes] = useState("");
  const composed = notes.trim() ? `${reason} — ${notes.trim()}` : reason;

  async function submit() {
    await onSubmit(composed);
    setNotes("");
    onOpenChange(false);
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader><DialogTitle>{title}</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div>
            <Label>Reason</Label>
            <Select value={reason} onValueChange={setReason}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {reasons.map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label>Additional notes (optional)</Label>
            <Textarea rows={3} maxLength={1000} value={notes}
                      onChange={(e) => setNotes(e.target.value)} />
          </div>
          <div className="flex justify-end gap-2 pt-1">
            <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={isLoading}>Cancel</Button>
            <Button variant="destructive" onClick={submit} disabled={isLoading}>
              {isLoading ? "Submitting..." : "Reject"}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
