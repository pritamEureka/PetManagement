import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Check, X, ShieldCheck, HeartHandshake } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { DataTable, type Column } from "@/components/common/DataTable";
import { Can } from "@/components/auth/Can";
import { adoptionApi, type AdoptionListingSummary } from "@/api/adoption";
import { toast } from "@/components/ui/sonner";
import { RejectReasonModal } from "@/components/adoption/RejectReasonModal";

export function AdoptionApprovalPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ["adoption", "admin-queue"],
    queryFn: () => adoptionApi.adminQueue({ status: "PendingApproval", pageSize: 50 })
  });
  const [rejectTarget, setRejectTarget] = useState<AdoptionListingSummary | null>(null);

  async function approve(l: AdoptionListingSummary) {
    try {
      await adoptionApi.approve(l.id);
      toast.success(`Approved: ${l.petName ?? l.title}`);
      qc.invalidateQueries({ queryKey: ["adoption", "admin-queue"] });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Approve failed.");
    }
  }

  const items = data?.items ?? [];
  const columns: Column<AdoptionListingSummary>[] = [
    {
      key: "listing", header: "Listing",
      render: (l) => (
        <Link to={`/adoption/${l.id}`} className="flex items-center gap-3">
          <div className="h-10 w-10 rounded-md bg-muted overflow-hidden">
            {l.photoUrls?.[0] && <img src={l.photoUrls[0]} className="object-cover w-full h-full" />}
          </div>
          <div className="min-w-0">
            <p className="font-medium truncate">{l.petName ?? l.title}</p>
            <p className="text-xs text-muted-foreground truncate">{l.animalType}{l.breed ? ` · ${l.breed}` : ""}</p>
          </div>
        </Link>
      )
    },
    { key: "owner", header: "Owner", render: (l) => <span className="text-sm">{l.ownerDisplayName}</span> },
    { key: "location", header: "Location", render: (l) => <span className="text-xs text-muted-foreground">{l.location ?? "—"}</span> },
    { key: "fee", header: "Fee", render: (l) => <span>{l.adoptionFee > 0 ? `$${l.adoptionFee.toFixed(2)}` : "Free"}</span> },
    { key: "submitted", header: "Submitted", className: "w-32",
      render: (l) => <span className="text-xs text-muted-foreground">{new Date(l.createdAt).toLocaleDateString()}</span> },
    {
      key: "actions", header: "", className: "w-44",
      render: (l) => (
        <div className="flex gap-2 justify-end">
          <Can permission="adoption.approve">
            <Button size="sm" onClick={() => approve(l)}><Check className="h-3.5 w-3.5 mr-1" /> Approve</Button>
          </Can>
          <Can permission="adoption.reject">
            <Button size="sm" variant="destructive" onClick={() => setRejectTarget(l)}>
              <X className="h-3.5 w-3.5 mr-1" /> Reject
            </Button>
          </Can>
        </div>
      )
    }
  ];

  return (
    <div className="space-y-4">
      <PageHeader title="Adoption approvals" icon={ShieldCheck}
        description={data ? `${items.length} listing(s) awaiting review` : "Pending listings."} />

      <Card>
        <CardContent className="pt-6">
          <DataTable
            data={items}
            columns={columns}
            rowKey={(l) => l.id}
            loading={isLoading}
            empty={<EmptyState icon={HeartHandshake} title="No pending listings" description="All caught up." />}
          />
        </CardContent>
      </Card>

      {rejectTarget && (
        <RejectReasonModal
          open={!!rejectTarget}
          onOpenChange={(v) => !v && setRejectTarget(null)}
          onConfirm={async (reason) => {
            await adoptionApi.reject(rejectTarget!.id, reason);
            qc.invalidateQueries({ queryKey: ["adoption", "admin-queue"] });
          }}
        />
      )}
    </div>
  );
}
