import { useEffect, useState } from "react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: React.ReactNode;
  /** Field label shown above the input. Defaults to "Reason". */
  label?: string;
  placeholder?: string;
  /** Pre-fill the input. */
  initialValue?: string;
  /** Show a multi-line textarea instead of an input. */
  multiline?: boolean;
  /** Disable submit when empty. */
  required?: boolean;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  inputType?: "text" | "number";
  /** Min/max for numeric input. */
  min?: number;
  max?: number;
  /** Async-friendly. Dialog stays open with submit disabled until the promise resolves. */
  onSubmit: (value: string) => void | Promise<void>;
}

/**
 * Modal replacement for window.prompt(). Useful for collecting a single value
 * (reason, notes, rating) before triggering an action. Pass `multiline` to
 * render a textarea instead.
 */
export function PromptDialog({
  open, onOpenChange, title, description,
  label = "Reason", placeholder, initialValue = "",
  multiline = false, required = true,
  confirmLabel = "Submit", cancelLabel = "Cancel",
  destructive = false, inputType = "text", min, max,
  onSubmit
}: Props) {
  const [value, setValue] = useState(initialValue);
  const [busy, setBusy] = useState(false);

  // Reset when the dialog (re-)opens so a previous run doesn't bleed in.
  useEffect(() => { if (open) setValue(initialValue); }, [open, initialValue]);

  const trimmed = value.trim();
  const canSubmit = !busy && (!required || trimmed.length > 0);

  async function handle() {
    if (!canSubmit) return;
    try {
      setBusy(true);
      await onSubmit(trimmed);
      onOpenChange(false);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !busy && onOpenChange(v)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        <div className="space-y-2">
          <Label htmlFor="prompt-input">{label}</Label>
          {multiline ? (
            <Textarea
              id="prompt-input"
              rows={4}
              placeholder={placeholder}
              value={value}
              onChange={(e) => setValue(e.target.value)}
              autoFocus
            />
          ) : (
            <Input
              id="prompt-input"
              type={inputType}
              min={min}
              max={max}
              placeholder={placeholder}
              value={value}
              onChange={(e) => setValue(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter" && canSubmit) { e.preventDefault(); void handle(); } }}
              autoFocus
            />
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            {cancelLabel}
          </Button>
          <Button
            variant={destructive ? "destructive" : "default"}
            onClick={handle}
            disabled={!canSubmit}
          >
            {busy ? "Working..." : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
