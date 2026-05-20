import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Truck, MapPin, Phone, MessageSquare, Package, CheckCircle2, AlertCircle } from "lucide-react";
import { Link } from "react-router-dom";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { deliveriesApi, type DeliveryAssignment, type DeliveryAssignmentStatus } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

// Possible next transitions per current status. Couriers can only move the
// assignment forward; admin can use the admin pages for harder fixes.
const NEXT: Record<DeliveryAssignmentStatus, DeliveryAssignmentStatus[]> = {
  Assigned:       ["PickedUp", "Failed"],
  PickedUp:       ["InTransit", "OutForDelivery", "Failed"],
  InTransit:      ["OutForDelivery", "Failed"],
  OutForDelivery: ["Delivered", "Failed"],
  Delivered:      [],
  Failed:         []
};

const LABEL: Record<DeliveryAssignmentStatus, string> = {
  Assigned: "Mark picked up",
  PickedUp: "Picked up",
  InTransit: "In transit",
  OutForDelivery: "Out for delivery",
  Delivered: "Mark delivered",
  Failed: "Mark failed"
};

export function MyDeliveriesPage() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ["deliveries-mine-active"],
    queryFn: () => deliveriesApi.mineActive()
  });

  const update = useMutation({
    mutationFn: ({ id, status }: { id: string; status: DeliveryAssignmentStatus }) =>
      deliveriesApi.updateStatus(id, status),
    onSuccess: (_, vars) => {
      toast.success(vars.status === "Delivered" ? "Marked delivered" :
                    vars.status === "Failed"    ? "Marked failed"    : "Status updated");
      qc.invalidateQueries({ queryKey: ["deliveries-mine-active"] });
      qc.invalidateQueries({ queryKey: ["deliveries-mine-history"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not update.")
  });

  if (isLoading) {
    return (
      <div className="max-w-5xl mx-auto space-y-3">
        {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-40" />)}
      </div>
    );
  }

  if (!data || data.length === 0) {
    return (
      <div className="max-w-2xl mx-auto">
        <Card>
          <CardContent className="py-16 text-center space-y-2">
            <Truck className="h-10 w-10 mx-auto text-muted-foreground" />
            <p className="font-medium">No active deliveries</p>
            <p className="text-sm text-muted-foreground">You're all caught up. New assignments will appear here.</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="max-w-5xl mx-auto space-y-3">
      <h1 className="text-xl font-semibold flex items-center gap-2">
        <Truck className="h-5 w-5 text-primary" /> Active deliveries
        <Badge variant="outline" className="ml-2">{data.length}</Badge>
      </h1>

      {data.map((a) => <DeliveryCard key={a.id} a={a} onAct={(s) => update.mutate({ id: a.id, status: s })} disabled={update.isPending} />)}
    </div>
  );
}

function DeliveryCard({ a, onAct, disabled }: {
  a: DeliveryAssignment;
  onAct: (s: DeliveryAssignmentStatus) => void;
  disabled: boolean;
}) {
  const next = NEXT[a.status];
  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between space-y-0">
        <div>
          <CardTitle className="flex items-center gap-2 text-lg">
            <Package className="h-5 w-5 text-primary" /> {a.orderNumber}
          </CardTitle>
          <p className="text-xs text-muted-foreground">Assigned {new Date(a.assignedAt).toLocaleString()}</p>
        </div>
        <div className="flex flex-col items-end gap-1">
          <Badge variant={statusBadgeVariant(a.status)}>{a.status}</Badge>
          <p className="text-sm font-semibold">${a.orderTotal.toFixed(2)}</p>
        </div>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <div className="grid sm:grid-cols-2 gap-3">
          <div className="space-y-1">
            <p className="text-xs uppercase tracking-wide text-muted-foreground">Buyer</p>
            <p className="font-medium">{a.customerName}</p>
            {a.customerPhone && (
              <a href={`tel:${a.customerPhone}`} className="text-xs text-primary hover:underline flex items-center gap-1">
                <Phone className="h-3 w-3" /> {a.customerPhone}
              </a>
            )}
            <Link to="/messages" className="text-xs text-primary hover:underline flex items-center gap-1">
              <MessageSquare className="h-3 w-3" /> Message
            </Link>
          </div>
          <div className="space-y-1">
            <p className="text-xs uppercase tracking-wide text-muted-foreground flex items-center gap-1">
              <MapPin className="h-3 w-3" /> Ship to
            </p>
            <p className="whitespace-pre-line">{a.shippingAddress}</p>
            <p className="text-xs text-muted-foreground">
              {[a.shippingCity, a.shippingCountry].filter(Boolean).join(", ")}
            </p>
          </div>
        </div>

        {a.notes && (
          <div className="bg-muted/40 rounded p-2 text-xs">
            <span className="font-medium">Notes:</span> {a.notes}
          </div>
        )}

        {next.length > 0 && (
          <>
            <Separator />
            <div className="flex flex-wrap gap-2">
              {next.map((s) => (
                <Button
                  key={s}
                  size="sm"
                  variant={s === "Failed" ? "destructive" : s === "Delivered" ? "default" : "outline"}
                  disabled={disabled}
                  onClick={() => onAct(s)}
                >
                  {s === "Delivered" ? <CheckCircle2 className="h-4 w-4 mr-1" /> :
                   s === "Failed"    ? <AlertCircle  className="h-4 w-4 mr-1" /> : null}
                  {LABEL[s]}
                </Button>
              ))}
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
