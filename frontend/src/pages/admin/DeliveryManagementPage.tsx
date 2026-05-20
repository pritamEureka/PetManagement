import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Truck, Phone, Mail, Package, CheckCircle2, AlertCircle } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge, statusBadgeVariant } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { deliveriesApi, type DeliveryAssignmentStatus } from "@/api/marketplace";

export function DeliveryManagementPage() {
  const [statusFilter, setStatusFilter] = useState<DeliveryAssignmentStatus | "all">("all");

  const { data: users, isLoading: loadingUsers } = useQuery({
    queryKey: ["delivery-users"],
    queryFn: () => deliveriesApi.listDeliveryUsers()
  });

  const { data: assignments, isLoading: loadingAssignments } = useQuery({
    queryKey: ["delivery-assignments", statusFilter],
    queryFn: () => deliveriesApi.adminList(statusFilter === "all" ? undefined : statusFilter)
  });

  return (
    <div className="max-w-6xl mx-auto space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Truck className="h-5 w-5 text-primary" /> Delivery personnel
          </CardTitle>
          <p className="text-xs text-muted-foreground">
            Users with the <span className="font-mono">DeliveryUser</span> role. To add someone, grant the role from the Users page.
          </p>
        </CardHeader>
        <CardContent>
          {loadingUsers ? <Skeleton className="h-32" /> : !users || users.length === 0 ? (
            <p className="py-6 text-sm text-muted-foreground text-center">No delivery personnel yet. Assign the DeliveryUser role to a user on the Users page.</p>
          ) : (
            <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
              {users.map((u) => (
                <Card key={u.userId} className="hover:border-primary transition-colors">
                  <CardContent className="pt-4 space-y-2">
                    <div className="flex items-center justify-between">
                      <p className="font-semibold truncate">{u.displayName}</p>
                      {u.activeAssignmentsCount > 0 && (
                        <Badge variant="secondary">{u.activeAssignmentsCount} active</Badge>
                      )}
                    </div>
                    <div className="text-xs text-muted-foreground space-y-1">
                      <p className="flex items-center gap-1"><Mail className="h-3 w-3" /> {u.email}</p>
                      {u.phoneNumber && <p className="flex items-center gap-1"><Phone className="h-3 w-3" /> {u.phoneNumber}</p>}
                    </div>
                    <div className="flex gap-2 pt-1 text-xs">
                      <Badge variant="outline" className="flex items-center gap-1">
                        <Package className="h-3 w-3" /> {u.activeAssignmentsCount} open
                      </Badge>
                      <Badge variant="outline" className="flex items-center gap-1">
                        <CheckCircle2 className="h-3 w-3" /> {u.completedDeliveriesCount} done
                      </Badge>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Assignments</CardTitle>
          <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v as DeliveryAssignmentStatus | "all")}>
            <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              <SelectItem value="Assigned">Assigned</SelectItem>
              <SelectItem value="PickedUp">Picked up</SelectItem>
              <SelectItem value="InTransit">In transit</SelectItem>
              <SelectItem value="OutForDelivery">Out for delivery</SelectItem>
              <SelectItem value="Delivered">Delivered</SelectItem>
              <SelectItem value="Failed">Failed</SelectItem>
            </SelectContent>
          </Select>
        </CardHeader>
        <CardContent>
          {loadingAssignments ? <Skeleton className="h-40" /> : !assignments || assignments.items.length === 0 ? (
            <p className="py-6 text-sm text-muted-foreground text-center">No assignments match.</p>
          ) : (
            <div className="space-y-2">
              {assignments.items.map((a) => (
                <div key={a.id} className="flex items-start justify-between border rounded-md p-3 text-sm">
                  <div className="space-y-1 min-w-0 flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="font-mono font-semibold">{a.orderNumber}</span>
                      <Badge variant={statusBadgeVariant(a.status)}>{a.status}</Badge>
                      <span className="text-muted-foreground text-xs">${a.orderTotal.toFixed(2)}</span>
                    </div>
                    <p className="text-xs text-muted-foreground truncate">
                      Courier: <span className="font-medium text-foreground">{a.deliveryUserName}</span>
                      {a.deliveryUserPhone && <> · {a.deliveryUserPhone}</>}
                    </p>
                    <p className="text-xs text-muted-foreground truncate">
                      Buyer: {a.customerName}{a.customerPhone && <> · {a.customerPhone}</>}
                    </p>
                    <p className="text-xs text-muted-foreground truncate">
                      Ship to: {a.shippingAddress}{a.shippingCity && <>, {a.shippingCity}</>}
                    </p>
                  </div>
                  <div className="text-right text-xs text-muted-foreground space-y-0.5">
                    <p>Assigned {new Date(a.assignedAt).toLocaleString()}</p>
                    {a.deliveredAt && <p className="text-emerald-600">Delivered {new Date(a.deliveredAt).toLocaleString()}</p>}
                    {a.failedAt && <p className="text-destructive flex items-center gap-1 justify-end"><AlertCircle className="h-3 w-3" /> Failed {new Date(a.failedAt).toLocaleString()}</p>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
