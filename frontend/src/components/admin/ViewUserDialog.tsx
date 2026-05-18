import { useEffect, useState } from "react";
import { Eye, Mail, Phone, ShieldCheck, Calendar, ListChecks, Package, HeartHandshake } from "lucide-react";
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { adminApi, type AdminUserDetail } from "@/api/admin";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  userId: string | null;
}

export function ViewUserDialog({ open, onOpenChange, userId }: Props) {
  const [detail, setDetail] = useState<AdminUserDetail | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open || !userId) { setDetail(null); return; }
    setLoading(true);
    adminApi.user(userId)
      .then(setDetail)
      .catch(() => setDetail(null))
      .finally(() => setLoading(false));
  }, [open, userId]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Eye className="h-5 w-5 text-primary" />
            {detail?.displayName ?? "User details"}
          </DialogTitle>
          {detail && <DialogDescription>{detail.email}</DialogDescription>}
        </DialogHeader>

        {loading || !detail ? (
          <div className="space-y-2">
            <Skeleton className="h-6" />
            <Skeleton className="h-6 w-3/4" />
            <Skeleton className="h-20" />
          </div>
        ) : (
          <div className="space-y-4 text-sm">
            {/* Status badges */}
            <div className="flex flex-wrap gap-2">
              {detail.approvalStatus && (
                <Badge variant={
                  detail.approvalStatus === "Approved" ? "default"
                  : detail.approvalStatus === "Rejected" ? "destructive"
                  : "secondary"
                }>{detail.approvalStatus}</Badge>
              )}
              <Badge variant={detail.isActive ? "secondary" : "muted"}>
                {detail.isActive ? "Active" : "Inactive"}
              </Badge>
              {detail.isSuspended && <Badge variant="destructive">Suspended</Badge>}
              {detail.emailConfirmed && <Badge variant="outline">Email confirmed</Badge>}
            </div>

            {detail.rejectionReason && (
              <div className="rounded-md border border-destructive/40 bg-destructive/5 p-2 text-xs">
                <span className="font-medium">Rejection reason: </span>{detail.rejectionReason}
              </div>
            )}

            {/* Identity */}
            <div className="grid gap-2">
              <Row icon={Mail} label="Email">{detail.email}</Row>
              {detail.phoneNumber && <Row icon={Phone} label="Phone">{detail.phoneNumber}</Row>}
              <Row icon={Calendar} label="Joined">
                {new Date(detail.createdAt).toLocaleString()}
              </Row>
              {detail.lastLoginAt && (
                <Row icon={Calendar} label="Last login">
                  {new Date(detail.lastLoginAt).toLocaleString()}
                </Row>
              )}
            </div>

            {/* Roles */}
            <div>
              <p className="text-xs font-medium text-muted-foreground mb-1 flex items-center gap-1">
                <ShieldCheck className="h-3 w-3" /> Roles
              </p>
              <div className="flex flex-wrap gap-1">
                {detail.roles.length === 0
                  ? <span className="text-xs text-muted-foreground">No roles assigned.</span>
                  : detail.roles.map((r) => <Badge key={r} variant="outline">{r}</Badge>)}
              </div>
            </div>

            {/* Activity counts */}
            <div className="grid grid-cols-3 gap-2 text-center">
              <Stat icon={ListChecks} label="Posts" value={detail.postCount} />
              <Stat icon={Package} label="Orders" value={detail.orderCount} />
              <Stat icon={HeartHandshake} label="Listings" value={detail.adoptionListingCount} />
            </div>
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Close</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function Row({ icon: Icon, label, children }: { icon: any; label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-2 text-sm">
      <Icon className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
      <span className="text-muted-foreground w-20">{label}</span>
      <span className="font-medium truncate">{children}</span>
    </div>
  );
}

function Stat({ icon: Icon, label, value }: { icon: any; label: string; value: number }) {
  return (
    <div className="rounded-md border p-2">
      <Icon className="h-4 w-4 text-muted-foreground mx-auto mb-1" />
      <p className="text-lg font-semibold leading-none">{value}</p>
      <p className="text-[10px] uppercase text-muted-foreground tracking-wider mt-1">{label}</p>
    </div>
  );
}
