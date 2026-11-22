import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ShieldCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { Skeleton } from "@/components/ui/skeleton";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { consentApi, type ConsentScope } from "@/api/clinicPlus";
import type { DoctorSummary } from "@/api/vets";
import { DoctorPicker } from "./DoctorPicker";

const sel = "h-9 rounded-md border bg-background px-2 text-sm";
const EXPIRY = [{ d: 7, l: "7 days" }, { d: 30, l: "30 days" }, { d: 90, l: "90 days" }, { d: 365, l: "1 year" }];

/** Owner-side: who may read this pet's records. Granting replaces any active consent for the same doctor. Owner only. */
export function RecordConsentsPanel({ petId, petName }: { petId: string; petName: string }) {
  const qc = useQueryClient();
  const key = ["pets", petId, "record-consents"];
  const { data = [], isLoading } = useQuery({ queryKey: key, queryFn: () => consentApi.list(petId) });
  const [doctor, setDoctor] = useState<DoctorSummary | null>(null);
  const [scope, setScope] = useState<ConsentScope>("FullRecord");
  const [days, setDays] = useState(30);
  const now = Date.now();
  const active = data.filter((c) => !c.revokedAt && new Date(c.expiresAt).getTime() > now);
  const past = data.filter((c) => !active.includes(c));

  const grant = useMutation({
    mutationFn: () => consentApi.grant({ petId, granteeDoctorId: doctor!.id, scope, expiresInDays: days }),
    onSuccess: (c) => {
      toast.success(`${c.granteeName} can now view ${petName}'s records until ${new Date(c.expiresAt).toLocaleDateString()}.`);
      setDoctor(null);
      qc.invalidateQueries({ queryKey: key });
    },
  });
  const revoke = useMutation({
    mutationFn: (id: string) => consentApi.revoke(id),
    onSuccess: () => { toast.success("Access revoked."); qc.invalidateQueries({ queryKey: key }); },
  });

  return (
    <Card>
      <CardHeader className="pb-3"><CardTitle className="text-base flex items-center gap-2"><ShieldCheck className="h-4 w-4" /> Vet record sharing</CardTitle></CardHeader>
      <CardContent className="space-y-4">
        <p className="text-xs text-muted-foreground">Give a specific doctor time-limited access to {petName}'s records, for example so they can accept a referral. You can revoke at any time.</p>
        {isLoading ? <Skeleton className="h-16" /> : active.length === 0 ? (
          <p className="text-sm text-muted-foreground">No doctor currently has access.</p>
        ) : (
          <ul className="divide-y text-sm">
            {active.map((c) => (
              <li key={c.id} className="py-2 flex items-center justify-between gap-2 flex-wrap">
                <span>
                  <span className="font-medium">{c.granteeName}</span>{" "}
                  <Badge variant="outline">{c.scope === "Summary" ? "Summary" : "Full record"}</Badge>
                  <span className="text-xs text-muted-foreground block">Until {new Date(c.expiresAt).toLocaleDateString()}</span>
                </span>
                <Button size="sm" variant="outline" disabled={revoke.isPending}
                  onClick={() => { if (window.confirm(`Revoke ${c.granteeName}'s access?`)) revoke.mutate(c.id); }}>Revoke</Button>
              </li>
            ))}
          </ul>
        )}
        <div className="space-y-2 border-t pt-3">
          <p className="text-sm font-medium">Grant access</p>
          <DoctorPicker value={doctor} onChange={setDoctor} />
          <div className="flex flex-wrap items-center gap-2">
            <select aria-label="Scope" className={sel} value={scope} onChange={(e) => setScope(e.target.value as ConsentScope)}>
              <option value="FullRecord">Full record</option>
              <option value="Summary">Summary only</option>
            </select>
            <select aria-label="Expires after" className={sel} value={days} onChange={(e) => setDays(Number(e.target.value))}>
              {EXPIRY.map((x) => <option key={x.d} value={x.d}>Expires in {x.l}</option>)}
            </select>
            <Button size="sm" disabled={!doctor || grant.isPending} onClick={() => grant.mutate()}>Grant access</Button>
          </div>
        </div>
        {past.length > 0 && (
          <details className="text-xs text-muted-foreground">
            <summary className="cursor-pointer">Past access ({past.length})</summary>
            <ul className="mt-1 space-y-0.5">
              {past.map((c) => <li key={c.id}>{c.granteeName} · {c.revokedAt ? "revoked" : "expired"} {new Date(c.revokedAt ?? c.expiresAt).toLocaleDateString()}</li>)}
            </ul>
          </details>
        )}
      </CardContent>
    </Card>
  );
}
