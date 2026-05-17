import { useState } from "react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/sonner";

const REASONS = [
  "Spam or misleading",
  "Animal cruelty or abuse",
  "Harassment or hate speech",
  "Sexually explicit content",
  "Violent or graphic content",
  "False information",
  "Other"
];

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  target: { id: string; kind: "post" | "comment" };
  onReport: (reason: string, details?: string) => Promise<unknown>;
}

export function ReportPostDialog({ open, onOpenChange, target, onReport }: Props) {
  const [reason, setReason] = useState(REASONS[0]);
  const [details, setDetails] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit() {
    setBusy(true);
    try {
      await onReport(reason, details || undefined);
      toast.success("Reported. Our moderation team will review it.");
      onOpenChange(false);
      setReason(REASONS[0]); setDetails("");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't submit report.");
    } finally { setBusy(false); }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Report this {target.kind}</DialogTitle>
          <DialogDescription>Tell us what's wrong — reports are anonymous to the author.</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div>
            <Label>Reason</Label>
            <Select value={reason} onValueChange={setReason}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {REASONS.map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="details">Details (optional)</Label>
            <Textarea id="details" rows={3} value={details} onChange={(e) => setDetails(e.target.value)} maxLength={2000} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button variant="destructive" onClick={submit} disabled={busy}>
            {busy ? "Submitting..." : "Submit report"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
