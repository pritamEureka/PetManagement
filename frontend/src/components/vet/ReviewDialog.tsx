import { useEffect, useState } from "react";
import { Star } from "lucide-react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title?: string;
  description?: React.ReactNode;
  /** Async-friendly. Dialog stays open with submit disabled until the promise resolves. */
  onSubmit: (rating: number, comment: string) => void | Promise<void>;
}

/**
 * Single-step review collector: 1–5 star rating plus an optional comment.
 * Used by the appointments flow after a visit completes.
 */
export function ReviewDialog({ open, onOpenChange, title = "Leave a review", description, onSubmit }: Props) {
  const [rating, setRating] = useState<number>(0);
  const [hover, setHover] = useState<number>(0);
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) { setRating(0); setHover(0); setComment(""); }
  }, [open]);

  const canSubmit = !busy && rating >= 1 && rating <= 5;

  async function handle() {
    if (!canSubmit) return;
    try {
      setBusy(true);
      await onSubmit(rating, comment.trim());
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
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label>Rating</Label>
            <div className="flex items-center gap-1">
              {[1, 2, 3, 4, 5].map((n) => {
                const filled = (hover || rating) >= n;
                return (
                  <button
                    key={n}
                    type="button"
                    className="p-1"
                    onClick={() => setRating(n)}
                    onMouseEnter={() => setHover(n)}
                    onMouseLeave={() => setHover(0)}
                    aria-label={`${n} star${n > 1 ? "s" : ""}`}
                  >
                    <Star className={`h-6 w-6 ${filled ? "fill-amber-400 text-amber-400" : "text-muted-foreground"}`} />
                  </button>
                );
              })}
              <span className="ml-2 text-sm text-muted-foreground">{rating > 0 ? `${rating}/5` : "Pick a rating"}</span>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="review-comment">Comment (optional)</Label>
            <Textarea
              id="review-comment"
              rows={4}
              placeholder="How was your visit?"
              value={comment}
              onChange={(e) => setComment(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>Cancel</Button>
          <Button onClick={handle} disabled={!canSubmit}>
            {busy ? "Submitting..." : "Submit review"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
