import { useState } from "react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/sonner";
import { adoptionApi } from "@/api/adoption";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  listingId: string;
  onDone: () => void;
}

export function MarkAsAdoptedDialog({ open, onOpenChange, listingId, onDone }: Props) {
  const [adopterId, setAdopterId] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit() {
    setBusy(true);
    try {
      await adoptionApi.markAdopted(listingId, adopterId.trim() || undefined);
      toast.success("Marked as adopted 🎉");
      onDone();
      onOpenChange(false);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't mark adopted.");
    } finally { setBusy(false); }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Mark as adopted</DialogTitle>
          <DialogDescription>
            The listing will be closed to new requests. You can optionally record the adopter's user ID for your records.
          </DialogDescription>
        </DialogHeader>
        <div>
          <Label htmlFor="adopter">Adopter user ID (optional)</Label>
          <Input id="adopter" placeholder="UUID" value={adopterId} onChange={(e) => setAdopterId(e.target.value)} />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={submit} disabled={busy}>{busy ? "Saving..." : "Confirm adoption"}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
