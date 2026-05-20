import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Check, X, Stethoscope, FileText, ShieldCheck, Ban } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Avatar, AvatarImage, AvatarFallback } from "@/components/ui/avatar";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { Can } from "@/components/auth/Can";
import { vetsApi, type DoctorSummary, type CredentialDocument } from "@/api/vets";
import { toast } from "@/components/ui/sonner";
import { RejectReasonModal } from "@/components/adoption/RejectReasonModal";
import { PromptDialog } from "@/components/common/PromptDialog";

export function DoctorApprovalPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ["admin-doctors"],
    queryFn: () => vetsApi.adminList({ status: "Pending", pageSize: 50 })
  });
  const [rejectTarget, setRejectTarget] = useState<DoctorSummary | null>(null);
  const [suspendTarget, setSuspendTarget] = useState<DoctorSummary | null>(null);
  const [credsForId, setCredsForId] = useState<string | null>(null);
  const { data: creds } = useQuery({
    queryKey: ["doctor-creds", credsForId],
    queryFn: () => credsForId ? vetsApi.doctorCredentials(credsForId) : Promise.resolve([] as CredentialDocument[]),
    enabled: !!credsForId
  });

  async function approve(d: DoctorSummary) {
    try { await vetsApi.approve(d.id); toast.success(`Approved Dr. ${d.name}`); qc.invalidateQueries({ queryKey: ["admin-doctors"] }); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Failed."); }
  }
  async function doSuspend(reason: string) {
    if (!suspendTarget) return;
    const d = suspendTarget;
    try { await vetsApi.suspend(d.id, reason); toast.success(`Suspended Dr. ${d.name}`); qc.invalidateQueries({ queryKey: ["admin-doctors"] }); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Failed."); }
  }
  async function verifyCred(c: CredentialDocument, verified: boolean) {
    await vetsApi.verifyCredential(c.id, verified);
    toast.success(verified ? "Verified." : "Unverified.");
    qc.invalidateQueries({ queryKey: ["doctor-creds", credsForId] });
  }

  const items = data?.items ?? [];

  return (
    <div className="space-y-4">
      <PageHeader title="Vet approvals" icon={ShieldCheck}
        description="Review applications, verify credentials, and manage status." />

      {isLoading ? (
        <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-24" />)}</div>
      ) : items.length === 0 ? (
        <EmptyState icon={Stethoscope} title="No doctors to review." />
      ) : (
        <div className="space-y-2">
          {items.map((d) => (
            <Card key={d.id}>
              <CardContent className="py-3 flex items-center gap-3">
                <Avatar>
                  <AvatarImage src={d.avatarUrl ?? undefined} />
                  <AvatarFallback>{d.name[0]}</AvatarFallback>
                </Avatar>
                <div className="flex-1 min-w-0">
                  <Link to={`/vets/${d.id}`} className="font-medium hover:underline">Dr. {d.name}</Link>
                  <p className="text-xs text-muted-foreground truncate">
                    {d.primarySpecialty ?? "—"}{d.city ? ` · ${d.city}` : ""} · ${d.consultationFee.toFixed(2)} fee
                  </p>
                </div>
                <Badge variant={statusBadgeVariant(d.approvalStatus)}>
                  {d.approvalStatus}
                </Badge>
                <Button size="sm" variant="outline" onClick={() => setCredsForId(credsForId === d.id ? null : d.id)}>
                  <FileText className="h-3.5 w-3.5 mr-1" /> Credentials
                </Button>
                <Can permission="vets.approve">
                  <Button size="sm" onClick={() => approve(d)}><Check className="h-3.5 w-3.5 mr-1" /> Approve</Button>
                </Can>
                <Can permission="vets.reject">
                  <Button size="sm" variant="destructive" onClick={() => setRejectTarget(d)}>
                    <X className="h-3.5 w-3.5 mr-1" /> Reject
                  </Button>
                </Can>
                <Can permission="vets.suspend">
                  <Button size="sm" variant="outline" onClick={() => setSuspendTarget(d)}>
                    <Ban className="h-3.5 w-3.5 mr-1" /> Suspend
                  </Button>
                </Can>
              </CardContent>
              {credsForId === d.id && (
                <CardContent className="pt-0 pb-3 space-y-1.5 border-t bg-muted/20">
                  {(creds ?? []).length === 0 ? (
                    <p className="text-xs text-muted-foreground py-2">No documents uploaded.</p>
                  ) : (
                    (creds ?? []).map((c) => (
                      <div key={c.id} className="flex items-center justify-between text-sm">
                        <a href={c.fileUrl} target="_blank" rel="noreferrer" className="hover:underline">
                          {c.title}{c.documentNumber ? ` · #${c.documentNumber}` : ""}
                        </a>
                        <div className="flex items-center gap-2">
                          {c.verified ? <Badge variant={statusBadgeVariant("Verified")}>Verified</Badge> : <Badge variant={statusBadgeVariant("Pending")}>Pending</Badge>}
                          <Button size="sm" variant={c.verified ? "outline" : "default"} onClick={() => verifyCred(c, !c.verified)}>
                            {c.verified ? "Unverify" : "Verify"}
                          </Button>
                        </div>
                      </div>
                    ))
                  )}
                </CardContent>
              )}
            </Card>
          ))}
        </div>
      )}

      {rejectTarget && (
        <RejectReasonModal
          open={!!rejectTarget}
          onOpenChange={(v) => !v && setRejectTarget(null)}
          title={`Reject Dr. ${rejectTarget.name}?`}
          description="Tell the applicant what to fix. They'll see this in their notifications."
          onConfirm={async (reason) => {
            await vetsApi.reject(rejectTarget!.id, reason);
            qc.invalidateQueries({ queryKey: ["admin-doctors"] });
          }}
        />
      )}

      <PromptDialog
        open={!!suspendTarget}
        onOpenChange={(v) => !v && setSuspendTarget(null)}
        title={`Suspend Dr. ${suspendTarget?.name ?? ""}`}
        description="The doctor will be prevented from accepting new appointments until restored."
        label="Reason"
        placeholder="What's the reason for the suspension?"
        confirmLabel="Suspend"
        destructive
        multiline
        onSubmit={doSuspend}
      />
    </div>
  );
}
