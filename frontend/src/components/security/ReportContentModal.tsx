import { useState } from "react";
import { Flag } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { reportsApi, type ReportTargetType } from "@/api/security";
import { toast } from "@/components/ui/sonner";

interface Props {
  targetType: ReportTargetType;
  targetId: string;
  trigger?: React.ReactNode;          // optional custom button
  className?: string;
}

const COMMON_REASONS = [
  "Spam or scam",
  "Harassment / hate speech",
  "Inappropriate or sexual content",
  "Animal abuse / cruelty",
  "Misinformation",
  "Copyright violation",
  "Other"
];

export function ReportContentModal({ targetType, targetId, trigger, className }: Props) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState(COMMON_REASONS[0]);
  const [details, setDetails] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function submit() {
    if (!reason.trim()) return;
    setSubmitting(true);
    try {
      await reportsApi.create({
        targetType,
        targetId,
        reason: reason.trim(),
        details: details.trim() || undefined
      });
      toast.success("Report submitted. Our team will review it.");
      setOpen(false);
      setReason(COMMON_REASONS[0]);
      setDetails("");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Could not submit report.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {trigger ?? (
          <Button variant="ghost" size="sm" className={className}>
            <Flag className="h-4 w-4 mr-1" /> Report
          </Button>
        )}
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Report this {targetType.toLowerCase()}</DialogTitle>
        </DialogHeader>
        <div className="space-y-3">
          <div>
            <Label>Reason</Label>
            <Select value={reason} onValueChange={setReason}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {COMMON_REASONS.map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          {reason === "Other" && (
            <div>
              <Label>Custom reason</Label>
              <Input value={reason === "Other" ? "" : reason}
                     onChange={(e) => setReason(e.target.value)}
                     placeholder="Briefly describe the issue" />
            </div>
          )}
          <div>
            <Label>Additional details (optional)</Label>
            <Textarea rows={4} value={details} maxLength={2000}
                      onChange={(e) => setDetails(e.target.value)}
                      placeholder="Add context if it helps the reviewer." />
            <p className="text-xs text-muted-foreground mt-1">{details.length}/2000</p>
          </div>
          <Button className="w-full" disabled={submitting || !reason.trim()} onClick={submit}>
            {submitting ? "Submitting..." : "Submit report"}
          </Button>
          <p className="text-xs text-muted-foreground text-center">
            Reports are reviewed by moderators. Abusing this feature may lead to a warning.
          </p>
        </div>
      </DialogContent>
    </Dialog>
  );
}
