import { useState } from "react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { UserPicker } from "@/components/pickers";
import { Checkbox } from "@/components/ui/checkbox";
import { toast } from "@/components/ui/sonner";
import { adoptionApi } from "@/api/adoption";
import { withStepUp } from "@/lib/withStepUp";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  listingId: string;
  onDone: () => void;
}

export function MarkAsAdoptedDialog({ open, onOpenChange, listingId, onDone }: Props) {
  const [adopterId, setAdopterId] = useState("");
  const [transfer, setTransfer] = useState(true);
  const [withHistory, setWithHistory] = useState(true);
  const [busy, setBusy] = useState(false);
  const validId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(adopterId.trim());

  async function submit() {
    if (adopterId.trim() && !validId) { toast.error("The adopter ID doesn't look right — it's a UUID."); return; }
    setBusy(true);
    try {
      await withStepUp(() => adoptionApi.markAdopted(listingId, adopterId.trim() || undefined,
        { transferPetProfile: validId && transfer, includeHealthHistory: withHistory }));
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
            The listing will be closed to new requests. You can optionally pick the adopter from the user directory.
          </DialogDescription>
        </DialogHeader>
        <div>
          <Label htmlFor="adopter">Adopter (optional)</Label>
          <UserPicker id="adopter" value={adopterId} onChange={(id) => setAdopterId(id)} clearable />
        </div>
        {validId && (
          <div className="space-y-2 rounded-md border p-3">
            <label className="flex items-start gap-2 text-sm">
              <Checkbox checked={transfer} onChange={(e) => setTransfer(e.currentTarget.checked)} />
              <span>Transfer the linked pet profile to the adopter
                <span className="block text-xs text-muted-foreground">Only applies if this listing was created from one of your pets. Your expenses and insurance stay private.</span>
              </span>
            </label>
            {transfer && (
              <label className="flex items-start gap-2 text-sm pl-6">
                <Checkbox checked={withHistory} onChange={(e) => setWithHistory(e.currentTarget.checked)} />
                <span>Include vet visits, medications and weight history
                  <span className="block text-xs text-muted-foreground">Vaccination records always transfer with the animal.</span>
                </span>
              </label>
            )}
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button onClick={submit} disabled={busy}>{busy ? "Saving..." : "Confirm adoption"}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
