import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { LogOut, Trash2, UserPlus } from "lucide-react";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { toast } from "@/components/ui/sonner";
import { useAuthStore } from "@/store/authStore";
import { petsApi, type Caretaker, type CaretakerRole, type PetDetail } from "@/api/pets";
import { Section, Field, Muted, fmtDate } from "./shared";

const roleHelp: Record<CaretakerRole, string> = {
  Viewer: "Can see the profile and health records.",
  Caretaker: "Can also log care, doses and weight.",
  CoOwner: "Can also edit the profile, records, expenses and sharing.",
};

export function FamilyTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const me = useAuthStore((s) => s.user?.id);
  const isOwner = pet.myAccess === "Owner";
  const { data: people = [], isLoading } = useQuery({ queryKey: ["pets", pet.id, "caretakers"], queryFn: () => petsApi.caretakers.list(pet.id) });
  const [f, setF] = useState<{ email: string; role: CaretakerRole; expiresAt: string }>({ email: "", role: "Caretaker", expiresAt: "" });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [removing, setRemoving] = useState<Caretaker | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: ["pets", pet.id, "caretakers"] });

  async function invite(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!/^\S+@\S+\.\S+$/.test(f.email.trim())) return setError("Enter a valid email address.");
    setBusy(true);
    try {
      await petsApi.caretakers.invite(pet.id, {
        email: f.email.trim(), role: f.role, expiresAt: f.expiresAt ? new Date(`${f.expiresAt}T23:59:59`).toISOString() : null
      });
      toast.success("Access granted — they've been notified.");
      setF({ email: "", role: "Caretaker", expiresAt: "" });
      refresh();
    } catch (err: any) {
      setError(err?.response?.status === 404
        ? "No Pawzaroo account uses that email. Ask them to sign up first."
        : err?.response?.data?.error?.message ?? "Couldn't add that person.");
    } finally { setBusy(false); }
  }

  async function confirmRemove() {
    if (!removing) return;
    await petsApi.caretakers.remove(pet.id, removing.id);
    if (removing.userId === me) {
      toast.success(`You left ${pet.name}'s care team.`);
      qc.invalidateQueries({ queryKey: ["pets", "mine"] });
      navigate("/pets");
      return;
    }
    toast.success("Access removed.");
    refresh();
  }

  return (
    <div className="space-y-4">
      <Section title="Care team">
        <div className="divide-y">
          <div className="py-2.5 flex items-center justify-between">
            <span className="text-sm font-medium">{pet.ownerName}</span>
            <Badge>Owner</Badge>
          </div>
          {isLoading ? <Muted>Loading…</Muted> : people.map((c) => (
            <div key={c.id} className="py-2.5 flex items-center justify-between gap-3">
              <div className="flex items-center gap-3 min-w-0">
                <Avatar className="h-8 w-8">
                  {c.avatarUrl && <AvatarImage src={c.avatarUrl} alt="" />}
                  <AvatarFallback>{c.displayName.slice(0, 1).toUpperCase()}</AvatarFallback>
                </Avatar>
                <div className="min-w-0">
                  <p className="text-sm font-medium truncate">{c.displayName}</p>
                  <p className="text-xs text-muted-foreground truncate">{c.email}{c.expiresAt ? ` · until ${fmtDate(c.expiresAt)}` : ""}</p>
                </div>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                {isOwner ? (
                  <Select value={c.role} onValueChange={async (v) => {
                    await petsApi.caretakers.update(pet.id, c.id, { role: v as CaretakerRole, expiresAt: c.expiresAt });
                    refresh();
                  }}>
                    <SelectTrigger className="h-8 w-32"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Viewer">Viewer</SelectItem>
                      <SelectItem value="Caretaker">Caretaker</SelectItem>
                      <SelectItem value="CoOwner">Co-owner</SelectItem>
                    </SelectContent>
                  </Select>
                ) : <Badge variant="secondary">{c.role === "CoOwner" ? "Co-owner" : c.role}</Badge>}
                {(isOwner || c.userId === me) && (
                  <Button size="icon" variant="ghost" onClick={() => setRemoving(c)} aria-label={c.userId === me ? "Leave" : "Remove"}>
                    {c.userId === me ? <LogOut className="h-4 w-4" /> : <Trash2 className="h-4 w-4" />}
                  </Button>
                )}
              </div>
            </div>
          ))}
        </div>
      </Section>

      {isOwner && (
        <Section title="Add someone">
          <form onSubmit={invite} className="space-y-3">
            <div className="grid sm:grid-cols-3 gap-3">
              <Field label="Their Pawzaroo email" htmlFor="ce">
                <Input id="ce" type="email" value={f.email} onChange={(e) => setF({ ...f, email: e.target.value })} />
              </Field>
              <Field label="Role">
                <Select value={f.role} onValueChange={(v) => setF({ ...f, role: v as CaretakerRole })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Viewer">Viewer</SelectItem>
                    <SelectItem value="Caretaker">Caretaker</SelectItem>
                    <SelectItem value="CoOwner">Co-owner</SelectItem>
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Access until (optional)" htmlFor="cx">
                <Input id="cx" type="date" min={new Date().toISOString().slice(0, 10)} value={f.expiresAt}
                  onChange={(e) => setF({ ...f, expiresAt: e.target.value })} />
              </Field>
            </div>
            <p className="text-xs text-muted-foreground">{roleHelp[f.role]} Set an end date for pet sitters.</p>
            {error && <p className="text-sm text-destructive">{error}</p>}
            <Button type="submit" disabled={busy}><UserPlus className="h-4 w-4 mr-1" /> {busy ? "Adding..." : "Give access"}</Button>
          </form>
        </Section>
      )}

      <ConfirmDialog
        open={!!removing}
        onOpenChange={(v) => !v && setRemoving(null)}
        title={removing?.userId === me ? `Leave ${pet.name}'s care team?` : `Remove ${removing?.displayName}?`}
        description={removing?.userId === me ? "You'll lose access to this pet." : "They will immediately lose access to this pet."}
        confirmLabel={removing?.userId === me ? "Leave" : "Remove"}
        destructive
        onConfirm={confirmRemove}
      />
    </div>
  );
}
