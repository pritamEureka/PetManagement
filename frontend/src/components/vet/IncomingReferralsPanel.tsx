import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Inbox } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { encounterApi, type Referral } from "@/api/clinicPlus";

const statusVariant = { Pending: "warning", Accepted: "success", Declined: "muted" } as const;

function apiMessage(e: any): { code?: string; message: string } {
  const err = e?.response?.data?.error;
  return { code: err?.code, message: err?.message ?? e?.message ?? "Something went wrong." };
}

/**
 * Referrals other vets sent to the signed-in doctor. Accepting a referral that asks for record sharing needs an active
 * owner consent; the API's explanation is shown inline on the row. (The respond endpoint takes no free-text note.)
 */
export function IncomingReferralsPanel() {
  const qc = useQueryClient();
  const { data = [], isLoading, isError } = useQuery({ queryKey: ["referrals", "incoming"], queryFn: encounterApi.incoming, retry: false });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const respond = useMutation({
    mutationFn: ({ id, accept }: { id: string; accept: boolean }) => encounterApi.respond(id, accept, true),
    onSuccess: (_r, v) => {
      setErrors((e) => { const { [v.id]: _drop, ...rest } = e; return rest; });
      toast.success(v.accept ? "Referral accepted." : "Referral declined.");
      qc.invalidateQueries({ queryKey: ["referrals", "incoming"] });
    },
    onError: (e, v) => {
      const m = apiMessage(e);
      setErrors((x) => ({ ...x, [v.id]: m.code === "consent_required"
        ? `${m.message} Ask the pet owner to grant you access (Pet > Share & ID > Vet record sharing), then accept again.`
        : m.message }));
    },
  });

  const pending = data.filter((r) => r.status === "Pending");
  const answered = data.filter((r) => r.status !== "Pending");

  return (
    <Card>
      <CardHeader className="pb-3 flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base flex items-center gap-2"><Inbox className="h-4 w-4" /> Incoming referrals {pending.length > 0 && <Badge variant="warning">{pending.length} new</Badge>}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? <Skeleton className="h-16" /> : isError ? (
          <p className="text-sm text-muted-foreground">Couldn't load referrals.</p>
        ) : data.length === 0 ? (
          <p className="text-sm text-muted-foreground">No referrals sent to you yet.</p>
        ) : (
          <ul className="divide-y">
            {[...pending, ...answered].map((r) => (
              <ReferralRow key={r.id} r={r} error={errors[r.id]} busy={respond.isPending && respond.variables?.id === r.id}
                onRespond={(accept) => respond.mutate({ id: r.id, accept })} />
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function ReferralRow({ r, error, busy, onRespond }: { r: Referral; error?: string; busy: boolean; onRespond: (accept: boolean) => void }) {
  return (
    <li className="py-3 space-y-1 text-sm">
      <div className="flex items-start justify-between gap-2 flex-wrap">
        <div className="min-w-0">
          <p className="font-medium">
            {r.petName ?? "Patient"} <span className="font-normal text-muted-foreground">from {r.fromDoctorName ?? "a colleague"}</span>{" "}
            <Badge variant={(statusVariant as Record<string, any>)[r.status] ?? "outline"}>{r.status}</Badge>
          </p>
          <p className="text-muted-foreground">{r.reason}</p>
          <p className="text-xs text-muted-foreground">
            {new Date(r.createdAt).toLocaleDateString()}{r.consentToShareRecords ? " · requests record sharing (needs owner consent to accept)" : ""}
          </p>
        </div>
        {r.status === "Pending" && (
          <div className="flex gap-2 shrink-0">
            <Button size="sm" disabled={busy} onClick={() => onRespond(true)}>Accept</Button>
            <Button size="sm" variant="outline" disabled={busy} onClick={() => { if (window.confirm("Decline this referral?")) onRespond(false); }}>Decline</Button>
          </div>
        )}
      </div>
      {error && <p role="alert" className="text-xs text-destructive rounded-md border border-destructive/30 bg-destructive/5 px-2 py-1">{error}</p>}
    </li>
  );
}
