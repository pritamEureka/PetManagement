import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { QRCodeSVG } from "qrcode.react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { withStepUp } from "@/lib/withStepUp";
import { emergencyCardUrl, exposureApi, type EmergencyCardLink } from "@/api/carePlans";
import { Muted } from "./shared";

/** Emergency health card preview + short-lived public QR link (re-authentication required to create the link). */
export function EmergencyCardDialog({ petId, open, onOpenChange, canShare }: {
  petId: string; open: boolean; onOpenChange: (v: boolean) => void; canShare: boolean;
}) {
  const { data: card, isLoading } = useQuery({ queryKey: ["pets", petId, "emergency-card"], queryFn: () => exposureApi.card(petId), enabled: open });
  const [link, setLink] = useState<EmergencyCardLink | null>(null);
  const [hours, setHours] = useState("24");

  async function create() {
    try { setLink(await withStepUp(() => exposureApi.createCardLink(petId, Number(hours)))); }
    catch { /* cancelled or toasted */ }
  }
  const url = link ? emergencyCardUrl(link.viewPath) : "";
  const List = ({ title, items }: { title: string; items: string[] }) => (
    <div><p className="font-medium text-sm">{title}</p>{items.length ? <ul className="list-disc pl-5 text-sm">{items.map((i) => <li key={i}>{i}</li>)}</ul> : <p className="text-sm text-muted-foreground">None recorded</p>}</div>
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader><DialogTitle>Emergency health card</DialogTitle></DialogHeader>
        {isLoading || !card ? <Muted>Loading…</Muted> : (
          <div className="space-y-3 max-h-[60vh] overflow-y-auto pr-1">
            <p className="text-sm">{card.petName} · {card.species}{card.breed ? ` · ${card.breed}` : ""}{card.weightKg ? ` · ${card.weightKg} kg` : ""}</p>
            <List title="Allergies" items={card.allergies} />
            <List title="Current medication" items={card.activeMedications} />
            <List title="Recent conditions" items={card.conditions} />
            <p className="text-sm"><span className="font-medium">Vet:</span> {[card.vetName, card.vetClinic, card.vetPhone].filter(Boolean).join(" · ") || "—"}</p>
            <p className="text-sm"><span className="font-medium">Owner:</span> {[card.ownerName, card.ownerPhone].filter(Boolean).join(" · ") || "—"}</p>
            {canShare && (
              <div className="rounded-md border p-3 space-y-2">
                <p className="text-sm font-medium">Share with a vet / first responder</p>
                {link ? (
                  <div className="flex flex-col items-center gap-2">
                    <QRCodeSVG value={url} size={160} />
                    <p className="text-xs break-all text-muted-foreground">{url}</p>
                    <p className="text-xs">Expires {new Date(link.expiresAt).toLocaleString()}</p>
                    <div className="flex gap-2">
                      <Button size="sm" variant="outline" onClick={() => { navigator.clipboard?.writeText(url); toast.success("Link copied."); }}>Copy link</Button>
                      <Button size="sm" variant="outline" onClick={async () => { await exposureApi.revokeCardLinks(petId); setLink(null); toast.success("Links revoked."); }}>Revoke all</Button>
                    </div>
                  </div>
                ) : (
                  <div className="flex items-center gap-2">
                    <select aria-label="Link validity" className="h-9 rounded-md border bg-background px-2 text-sm" value={hours} onChange={(e) => setHours(e.target.value)}>
                      <option value="1">1 hour</option><option value="24">24 hours</option><option value="72">3 days</option><option value="168">7 days</option>
                    </select>
                    <Button size="sm" onClick={create}>Create QR link</Button>
                  </div>
                )}
                <p className="text-xs text-muted-foreground">Anyone with the link can see this card (including the owner's phone) until it expires.</p>
              </div>
            )}
          </div>
        )}
        <DialogFooter><Button variant="ghost" onClick={() => onOpenChange(false)}>Close</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
