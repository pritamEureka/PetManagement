import { useState } from "react";
import { Gavel } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  moderationApi,
  type ModerationActionType,
  type ModerationTargetType,
  type WarningSeverity
} from "@/api/security";
import { toast } from "@/components/ui/sonner";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  targetType: ModerationTargetType;
  targetId: string;
  reportId?: string;
  defaultAction?: ModerationActionType;
  onDone?: () => void;
}

const ALL_ACTIONS: ModerationActionType[] = [
  "Warn", "Suspend", "Ban", "Hide", "Restore", "Approve", "Reject", "MarkSuspicious", "Escalate"
];

const SEVERITIES: WarningSeverity[] = ["Info", "Minor", "Major", "Final"];

export function ModerationActionModal(
  { open, onOpenChange, targetType, targetId, reportId, defaultAction = "Hide", onDone }: Props
) {
  const [action, setAction] = useState<ModerationActionType>(defaultAction);
  const [notes, setNotes] = useState("");
  const [suspendUntil, setSuspendUntil] = useState("");
  const [severity, setSeverity] = useState<WarningSeverity>("Minor");
  const [submitting, setSubmitting] = useState(false);

  async function apply() {
    setSubmitting(true);
    try {
      await moderationApi.act({
        action,
        targetType,
        targetId,
        reportId,
        notes: notes.trim() || undefined,
        suspendUntil: action === "Suspend" || action === "Ban"
          ? (suspendUntil ? new Date(suspendUntil).toISOString() : undefined)
          : undefined,
        isBan: action === "Ban",
        warningSeverity: action === "Warn" ? severity : undefined
      });
      toast.success(`${action} applied.`);
      onOpenChange(false);
      onDone?.();
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Could not apply action.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Gavel className="h-5 w-5" /> Moderation action
          </DialogTitle>
        </DialogHeader>
        <div className="space-y-3 text-sm">
          <div>
            <Label>Action</Label>
            <Select value={action} onValueChange={(v) => setAction(v as ModerationActionType)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {ALL_ACTIONS.map((a) => <SelectItem key={a} value={a}>{a}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>

          <p className="text-xs text-muted-foreground">
            Target: <span className="font-mono">{targetType}</span> · <span className="font-mono">{targetId}</span>
          </p>

          {action === "Warn" && (
            <div>
              <Label>Severity</Label>
              <Select value={severity} onValueChange={(v) => setSeverity(v as WarningSeverity)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {SEVERITIES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          )}

          {(action === "Suspend" || action === "Ban") && action !== "Ban" && (
            <div>
              <Label>Expires (suspensions only)</Label>
              <Input type="datetime-local" value={suspendUntil} onChange={(e) => setSuspendUntil(e.target.value)} />
            </div>
          )}

          <div>
            <Label>Notes</Label>
            <Textarea rows={3} value={notes} maxLength={2000}
                      onChange={(e) => setNotes(e.target.value)}
                      placeholder="Reason — visible to the user when relevant." />
          </div>

          <Button className="w-full" onClick={apply} disabled={submitting}>
            {submitting ? "Applying..." : `Apply ${action}`}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
