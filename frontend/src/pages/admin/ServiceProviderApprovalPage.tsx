import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Scissors, X, Ban } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { PromptDialog } from "@/components/common/PromptDialog";
import { Can } from "@/components/auth/Can";
import { toast } from "@/components/ui/sonner";
import { petServicesApi, type ProviderSummary } from "@/api/petServices";

type Decision = { kind: "approve" | "reject" | "suspend"; provider: ProviderSummary };

export function ServiceProviderApprovalPage() {
  const qc = useQueryClient();
  const [status, setStatus] = useState("Pending");
  const [decision, setDecision] = useState<Decision | null>(null);
  const { data, isLoading } = useQuery({ queryKey: ["admin", "service-providers", status], queryFn: () => petServicesApi.admin.list(status) });

  async function decide(notes: string) {
    if (!decision) return;
    const fn = petServicesApi.admin[decision.kind];
    await fn(decision.provider.id, notes || undefined);
    toast.success(`${decision.provider.businessName} ${decision.kind === "approve" ? "approved" : decision.kind === "reject" ? "rejected" : "suspended"}.`);
    qc.invalidateQueries({ queryKey: ["admin", "service-providers"] });
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Service provider approvals" icon={Scissors} description="Groomers, trainers, boarding and walkers waiting to go live." />
      <Tabs value={status} onValueChange={setStatus}>
        <TabsList>
          {["Pending", "Approved", "Rejected", "Suspended"].map((s) => <TabsTrigger key={s} value={s}>{s}</TabsTrigger>)}
        </TabsList>
      </Tabs>

      {isLoading ? <Skeleton className="h-40" /> : !data || data.items.length === 0 ? (
        <EmptyState icon={Scissors} title={`No ${status.toLowerCase()} providers`} />
      ) : (
        <div className="space-y-2">
          {data.items.map((p) => (
            <Card key={p.id}>
              <CardContent className="py-4 flex items-start justify-between gap-3 flex-wrap">
                <div className="min-w-0">
                  <p className="font-medium flex items-center gap-2">
                    <Link to={`/services/${p.id}`} className="hover:underline">{p.businessName}</Link>
                    <Badge variant="secondary">{p.providerType}</Badge>
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {[p.city, p.country].filter(Boolean).join(", ") || "No location"} · {p.basePrice} per {p.defaultDurationMinutes} min
                    {p.animalTypes.length ? ` · ${p.animalTypes.join(", ")}` : ""}
                  </p>
                  {p.about && <p className="text-sm mt-1 line-clamp-2">{p.about}</p>}
                </div>
                <div className="flex gap-2">
                  {status !== "Approved" && (
                    <Can permission="services.approve">
                      <Button size="sm" onClick={() => setDecision({ kind: "approve", provider: p })}><Check className="h-3.5 w-3.5 mr-1" /> Approve</Button>
                    </Can>
                  )}
                  {status === "Pending" && (
                    <Can permission="services.reject">
                      <Button size="sm" variant="outline" onClick={() => setDecision({ kind: "reject", provider: p })}><X className="h-3.5 w-3.5 mr-1" /> Reject</Button>
                    </Can>
                  )}
                  {status === "Approved" && (
                    <Can permission="services.suspend">
                      <Button size="sm" variant="destructive" onClick={() => setDecision({ kind: "suspend", provider: p })}><Ban className="h-3.5 w-3.5 mr-1" /> Suspend</Button>
                    </Can>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <PromptDialog
        open={!!decision}
        onOpenChange={(v) => !v && setDecision(null)}
        title={decision ? `${decision.kind[0].toUpperCase()}${decision.kind.slice(1)} ${decision.provider.businessName}?` : ""}
        label={decision?.kind === "approve" ? "Note to the provider (optional)" : "Reason (shown to the provider)"}
        required={decision?.kind !== "approve"}
        multiline
        confirmLabel={decision?.kind === "approve" ? "Approve" : decision?.kind === "reject" ? "Reject" : "Suspend"}
        destructive={decision?.kind !== "approve"}
        onSubmit={decide}
      />
    </div>
  );
}
