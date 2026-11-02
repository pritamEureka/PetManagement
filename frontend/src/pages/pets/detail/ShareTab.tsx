import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { QRCodeSVG } from "qrcode.react";
import { AlertTriangle, Copy, FileDown, Link2, Printer, QrCode } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/sonner";
import { petsApi, canEditPet, publicPetUrl, sharedHealthUrl, type PetDetail } from "@/api/pets";
import { Section, Field, Muted, fmtDate } from "./shared";

async function copy(text: string) {
  try { await navigator.clipboard.writeText(text); toast.success("Link copied."); }
  catch { toast.error("Couldn't copy — select and copy the link manually."); }
}

export function ShareTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const editor = canEditPet(pet.myAccess);
  const isOwner = pet.myAccess === "Owner";
  const [busy, setBusy] = useState(false);
  const [link, setLink] = useState({ validDays: "7", label: "" });

  const { data: links = [] } = useQuery({
    queryKey: ["pets", pet.id, "share-links"], queryFn: () => petsApi.sharing.links(pet.id), enabled: editor,
  });
  const refresh = () => qc.invalidateQueries({ queryKey: ["pets", pet.id] });

  async function togglePublic(enabled: boolean) {
    setBusy(true);
    try {
      await petsApi.sharing.setPublicProfile(pet.id, enabled);
      toast.success(enabled ? "Public tag profile enabled." : "Public tag profile disabled.");
      refresh();
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  async function createLink(e: React.FormEvent) {
    e.preventDefault();
    const l = await petsApi.sharing.createLink(pet.id, { validDays: Number(link.validDays), label: link.label.trim() || undefined });
    await copy(sharedHealthUrl(l.token));
    setLink({ validDays: "7", label: "" });
    refresh();
  }

  function printTag() {
    const svg = document.getElementById("pet-qr")?.outerHTML;
    if (!svg || !pet.publicCode) return;
    const w = window.open("", "_blank", "width=420,height=560");
    if (!w) return toast.error("Allow pop-ups to print the tag.");
    const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]!));
    w.document.write(`<!doctype html><title>${esc(pet.name)} tag</title>
      <body style="font-family:system-ui;text-align:center;padding:24px">
      <div style="display:inline-block;border:2px dashed #999;border-radius:16px;padding:20px">
      <h2 style="margin:0 0 4px">${esc(pet.name)}</h2><p style="margin:0 0 12px;font-size:13px">If found, please scan</p>
      ${svg}<p style="font:600 16px monospace;letter-spacing:2px;margin:12px 0 0">${pet.publicCode}</p></div>
      <script>setTimeout(()=>print(),200)</script></body>`);
    w.document.close();
  }

  return (
    <div className="space-y-4">
      <Section title="Lost pet?">
        {pet.isLost ? (
          <div className="flex items-center justify-between gap-3 flex-wrap">
            <p className="text-sm flex items-center gap-2"><AlertTriangle className="h-4 w-4 text-destructive" /> {pet.name} is marked as lost.</p>
            <Button variant="outline" asChild><Link to="/lost-found/mine">Manage report</Link></Button>
          </div>
        ) : (
          <div className="flex items-center justify-between gap-3 flex-wrap">
            <p className="text-sm text-muted-foreground">Alert people nearby, post to the feed and get matched with found-pet reports.</p>
            {isOwner && <Button variant="destructive" asChild><Link to={`/lost-found/new?petId=${pet.id}`}>Report {pet.name} lost</Link></Button>}
          </div>
        )}
      </Section>

      <Section title="QR ID tag" action={pet.publicProfileEnabled ? <Badge variant="success">Public</Badge> : <Badge variant="muted">Off</Badge>}>
        <p className="text-sm text-muted-foreground mb-3">
          A scannable tag for {pet.name}'s collar. Finders see the name, photo, allergies and a button to alert you — never your contact details.
        </p>
        {pet.publicProfileEnabled && pet.publicCode ? (
          <div className="flex flex-col sm:flex-row gap-4 items-start">
            <div className="rounded-lg border bg-white p-3">
              <QRCodeSVG id="pet-qr" value={publicPetUrl(pet.publicCode)} size={148} level="M" />
            </div>
            <div className="space-y-2 min-w-0">
              <p className="text-sm">Tag code <span className="font-mono font-semibold tracking-wider">{pet.publicCode}</span></p>
              <p className="text-xs text-muted-foreground break-all">{publicPetUrl(pet.publicCode)}</p>
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="outline" onClick={printTag}><Printer className="h-3.5 w-3.5 mr-1" /> Print tag</Button>
                <Button size="sm" variant="outline" onClick={() => copy(publicPetUrl(pet.publicCode!))}><Copy className="h-3.5 w-3.5 mr-1" /> Copy link</Button>
                {isOwner && <Button size="sm" variant="ghost" disabled={busy} onClick={() => togglePublic(false)}>Turn off</Button>}
              </div>
            </div>
          </div>
        ) : isOwner ? (
          <Button disabled={busy} onClick={() => togglePublic(true)}><QrCode className="h-4 w-4 mr-1" /> Create QR tag</Button>
        ) : <Muted>Only the owner can enable the tag.</Muted>}
      </Section>

      <Section title="Health record">
        <div className="flex flex-wrap gap-2 mb-4">
          <Button variant="outline" onClick={() => petsApi.sharing.openHealthReport(pet.id).catch(() => toast.error("Couldn't open the report."))}>
            <FileDown className="h-4 w-4 mr-1" /> Export / print PDF
          </Button>
        </div>
        {editor && (
          <>
            <p className="text-sm text-muted-foreground mb-2">
              Share a read-only link with a new vet, a boarding kennel or for travel. Links expire automatically and can be revoked.
            </p>
            <form onSubmit={createLink} className="grid sm:grid-cols-[1fr_140px_auto] gap-3 items-end">
              <Field label="For (optional)" htmlFor="sl"><Input id="sl" placeholder="Dr. Rahman's clinic" value={link.label} onChange={(e) => setLink({ ...link, label: e.target.value })} /></Field>
              <Field label="Valid for">
                <Select value={link.validDays} onValueChange={(v) => setLink({ ...link, validDays: v })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {["1", "7", "30", "90"].map((d) => <SelectItem key={d} value={d}>{d} day{d === "1" ? "" : "s"}</SelectItem>)}
                  </SelectContent>
                </Select>
              </Field>
              <Button type="submit"><Link2 className="h-4 w-4 mr-1" /> Create link</Button>
            </form>
            {links.length > 0 && (
              <div className="divide-y mt-4">
                {links.map((l) => {
                  const active = !l.revokedAt && new Date(l.expiresAt) > new Date();
                  return (
                    <div key={l.id} className="py-2 flex items-center justify-between gap-3 text-sm">
                      <div className="min-w-0">
                        <p className="font-medium">{l.label ?? "Shared link"}</p>
                        <p className="text-xs text-muted-foreground">
                          Created {fmtDate(l.createdAt)} · {l.revokedAt ? "revoked" : active ? `expires ${fmtDate(l.expiresAt)}` : "expired"}
                        </p>
                      </div>
                      {active && (
                        <div className="flex gap-1 shrink-0">
                          <Button size="sm" variant="ghost" onClick={() => copy(sharedHealthUrl(l.token))}><Copy className="h-3.5 w-3.5" /></Button>
                          <Button size="sm" variant="ghost" onClick={async () => { await petsApi.sharing.revokeLink(pet.id, l.id); refresh(); }}>Revoke</Button>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </>
        )}
      </Section>
    </div>
  );
}
