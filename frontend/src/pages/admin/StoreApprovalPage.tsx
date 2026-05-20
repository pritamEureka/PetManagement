import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Store as StoreIcon, ShieldCheck, ShieldAlert, Pause, Play } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { storesApi, type ApprovalStatus } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

export function StoreApprovalPage() {
  const qc = useQueryClient();
  const [storeStatus, setStoreStatus] = useState<ApprovalStatus | "all">("Pending");
  const [search, setSearch] = useState("");

  const { data: stores, isLoading } = useQuery({
    queryKey: ["admin-stores", storeStatus, search],
    queryFn: () => storesApi.search({
      search: search || undefined,
      status: storeStatus === "all" ? undefined : storeStatus,
      page: 1, pageSize: 50
    })
  });

  const { data: kycList } = useQuery({
    queryKey: ["admin-kyc"],
    queryFn: () => storesApi.adminListKyc("Pending", 1, 50)
  });

  const approveStore = useMutation({
    mutationFn: (id: string) => storesApi.approve(id),
    onSuccess: () => { toast.success("Store approved"); qc.invalidateQueries({ queryKey: ["admin-stores"] }); }
  });
  const rejectStore = useMutation({
    mutationFn: (id: string) => storesApi.reject(id, "Insufficient information"),
    onSuccess: () => { toast.success("Store rejected"); qc.invalidateQueries({ queryKey: ["admin-stores"] }); }
  });
  const suspendStore = useMutation({
    mutationFn: (id: string) => storesApi.suspend(id, "Policy violation"),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["admin-stores"] })
  });
  const restoreStore = useMutation({
    mutationFn: (id: string) => storesApi.restore(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["admin-stores"] })
  });

  const approveKyc = useMutation({
    mutationFn: (id: string) => storesApi.adminApproveKyc(id),
    onSuccess: () => { toast.success("KYC approved"); qc.invalidateQueries({ queryKey: ["admin-kyc"] }); }
  });
  const rejectKyc = useMutation({
    mutationFn: (id: string) => storesApi.adminRejectKyc(id, "Document unclear"),
    onSuccess: () => { toast.success("KYC rejected"); qc.invalidateQueries({ queryKey: ["admin-kyc"] }); }
  });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold flex items-center gap-2">
        <ShieldCheck className="h-6 w-6 text-primary" /> Store approvals
      </h1>

      <Tabs defaultValue="kyc">
        <TabsList>
          <TabsTrigger value="kyc">KYC ({kycList?.total ?? 0})</TabsTrigger>
          <TabsTrigger value="stores">Stores</TabsTrigger>
        </TabsList>

        <TabsContent value="kyc">
          <div className="space-y-3">
            {!kycList?.items.length
              ? <Card><CardContent className="py-12 text-center text-muted-foreground">No pending KYC reviews.</CardContent></Card>
              : kycList.items.map((k) => (
                  <Card key={k.id}>
                    <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                      <CardTitle className="text-base">{k.legalName}</CardTitle>
                      <Badge variant={statusBadgeVariant(k.kycStatus)}>{k.kycStatus}</Badge>
                    </CardHeader>
                    <CardContent className="text-sm space-y-2">
                      <div className="grid grid-cols-2 gap-2">
                        <Field label="Business" value={k.businessName} />
                        <Field label="Tax ID" value={k.taxId} />
                        <Field label="Trade license" value={k.tradeLicenseNumber} />
                        <Field label="National ID" value={k.nationalIdNumber} />
                      </div>
                      <div className="flex gap-2 flex-wrap text-xs">
                        {k.tradeLicenseDocUrl && <a href={k.tradeLicenseDocUrl} target="_blank" rel="noreferrer" className="text-primary hover:underline">Trade license doc</a>}
                        {k.nationalIdDocUrl && <a href={k.nationalIdDocUrl} target="_blank" rel="noreferrer" className="text-primary hover:underline">National ID doc</a>}
                        {k.addressProofDocUrl && <a href={k.addressProofDocUrl} target="_blank" rel="noreferrer" className="text-primary hover:underline">Address proof doc</a>}
                      </div>
                      <div className="flex gap-2 pt-2">
                        <Button size="sm" onClick={() => approveKyc.mutate(k.id)}><ShieldCheck className="h-3 w-3 mr-1" /> Approve</Button>
                        <Button size="sm" variant="destructive" onClick={() => rejectKyc.mutate(k.id)}>
                          <ShieldAlert className="h-3 w-3 mr-1" /> Reject
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                ))}
          </div>
        </TabsContent>

        <TabsContent value="stores">
          <div className="space-y-3">
            <div className="flex gap-2 flex-wrap">
              <Input className="max-w-xs" placeholder="Search stores..." value={search} onChange={(e) => setSearch(e.target.value)} />
              <Select value={storeStatus} onValueChange={(v) => setStoreStatus(v as any)}>
                <SelectTrigger className="w-full sm:w-44"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All statuses</SelectItem>
                  <SelectItem value="Pending">Pending</SelectItem>
                  <SelectItem value="Approved">Approved</SelectItem>
                  <SelectItem value="Rejected">Rejected</SelectItem>
                  <SelectItem value="Suspended">Suspended</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {isLoading ? <Skeleton className="h-32" />
              : !stores?.items.length ? <Card><CardContent className="py-12 text-center text-muted-foreground">No stores.</CardContent></Card>
              : stores.items.map((s) => (
                  <Card key={s.id}>
                    <CardHeader className="flex flex-row justify-between items-start space-y-0 pb-2">
                      <CardTitle className="text-base flex items-center gap-2"><StoreIcon className="h-4 w-4" /> {s.name}</CardTitle>
                      <Badge variant={statusBadgeVariant(s.approvalStatus)}>{s.approvalStatus}</Badge>
                    </CardHeader>
                    <CardContent className="text-sm space-y-2">
                      <p className="text-muted-foreground">{s.description}</p>
                      <div className="grid grid-cols-3 gap-2 text-xs">
                        <Field label="Address" value={s.address} />
                        <Field label="City" value={s.city} />
                        <Field label="Country" value={s.country} />
                      </div>
                      <p className="text-xs">Commission: <span className="font-semibold">{s.commissionPercent}%</span> · Products: {s.productCount} · Rating: {s.avgRating.toFixed(1)}</p>
                      <div className="flex gap-2 flex-wrap pt-2">
                        {s.approvalStatus !== "Approved" && (
                          <Button size="sm" onClick={() => approveStore.mutate(s.id)}><ShieldCheck className="h-3 w-3 mr-1" /> Approve</Button>
                        )}
                        {s.approvalStatus !== "Rejected" && (
                          <Button size="sm" variant="destructive" onClick={() => rejectStore.mutate(s.id)}>
                            <ShieldAlert className="h-3 w-3 mr-1" /> Reject
                          </Button>
                        )}
                        {s.approvalStatus === "Approved" && (
                          <Button size="sm" variant="outline" onClick={() => suspendStore.mutate(s.id)}>
                            <Pause className="h-3 w-3 mr-1" /> Suspend
                          </Button>
                        )}
                        {s.approvalStatus === "Suspended" && (
                          <Button size="sm" variant="outline" onClick={() => restoreStore.mutate(s.id)}>
                            <Play className="h-3 w-3 mr-1" /> Restore
                          </Button>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                ))}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}

function Field({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p>{value || "—"}</p>
    </div>
  );
}
