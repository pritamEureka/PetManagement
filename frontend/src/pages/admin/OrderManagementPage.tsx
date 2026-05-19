import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Package, Check, Ban, Printer, Truck, User as UserIcon, Mail, Phone } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Separator } from "@/components/ui/separator";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { PageHeader } from "@/components/common/PageHeader";
import { FilterBar } from "@/components/admin/FilterBar";
import { DetailsDrawer } from "@/components/admin/DetailsDrawer";
import { ordersV2Api, deliveriesApi, type Order, type OrderStatus } from "@/api/marketplace";
import { toast } from "@/components/ui/sonner";

const STATUSES: OrderStatus[] = ["Created", "Confirmed", "Packed", "Shipped", "Delivered", "Cancelled", "Returned", "Denied"];
const STATUS_VARIANT: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
  Created: "outline", Confirmed: "secondary", Packed: "secondary",
  Shipped: "default", Delivered: "default",
  Cancelled: "destructive", Returned: "outline", Denied: "destructive"
};

export function AdminOrderManagementPage() {
  const qc = useQueryClient();
  const [status, setStatus] = useState<OrderStatus | "all">("all");
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [deliveryUserId, setDeliveryUserId] = useState<string>("");
  const [deliveryNotes, setDeliveryNotes] = useState<string>("");

  const { data, isLoading } = useQuery({
    queryKey: ["admin-orders", status, page],
    queryFn: () => ordersV2Api.adminAll(status === "all" ? undefined : status, page, 50)
  });

  // Re-fetch the selected order so the drawer reflects mutations immediately.
  const { data: selected } = useQuery({
    queryKey: ["admin-order-detail", selectedId],
    queryFn: () => ordersV2Api.get(selectedId!),
    enabled: !!selectedId
  });

  const { data: deliveryUsers } = useQuery({
    queryKey: ["delivery-users"],
    queryFn: () => deliveriesApi.listDeliveryUsers(),
    enabled: !!selectedId
  });

  function invalidateAll() {
    qc.invalidateQueries({ queryKey: ["admin-orders"] });
    if (selectedId) qc.invalidateQueries({ queryKey: ["admin-order-detail", selectedId] });
  }

  const approve = useMutation({
    mutationFn: (id: string) => ordersV2Api.updateStatus(id, "Confirmed"),
    onSuccess: () => { toast.success("Order approved"); invalidateAll(); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not approve.")
  });
  const deny = useMutation({
    mutationFn: (id: string) => ordersV2Api.deny(id, "Denied by admin"),
    onSuccess: () => { toast.success("Order denied"); invalidateAll(); },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not deny.")
  });
  const assignDelivery = useMutation({
    mutationFn: (orderId: string) => deliveriesApi.assign(orderId, deliveryUserId, deliveryNotes || undefined),
    onSuccess: () => {
      toast.success("Delivery assigned");
      setDeliveryUserId("");
      setDeliveryNotes("");
      invalidateAll();
      qc.invalidateQueries({ queryKey: ["delivery-assignments"] });
    },
    onError: (e: any) => toast.error(e?.response?.data?.error?.message ?? "Could not assign.")
  });

  const columns: Column<Order>[] = [
    { key: "no", header: "Order #", render: (o) => <span className="font-mono text-xs">{o.orderNumber}</span> },
    { key: "buyer", header: "Buyer",
      render: (o) => <span className="text-xs">{o.customerName ?? <span className="text-muted-foreground">{o.userId.slice(0, 8)}…</span>}</span> },
    { key: "items", header: "Items", render: (o) => o.items.length, className: "text-right w-16" },
    { key: "total", header: "Total", className: "text-right w-24",
      render: (o) => <span className="font-semibold">${o.total.toFixed(2)}</span> },
    { key: "status", header: "Status",
      render: (o) => <Badge variant={STATUS_VARIANT[o.status] ?? "outline"}>{o.status}</Badge> },
    { key: "ship", header: "Shipment", render: (o) => <Badge variant="outline">{o.shipmentStatus}</Badge> },
    { key: "pay", header: "Payment", render: (o) => <Badge variant="outline">{o.paymentStatus}</Badge> },
    { key: "courier", header: "Courier",
      render: (o) => o.deliveryUserName
        ? <span className="text-xs">{o.deliveryUserName}</span>
        : <span className="text-xs text-muted-foreground">—</span>
    },
    { key: "created", header: "Placed",
      render: (o) => <span className="text-xs text-muted-foreground">{new Date(o.createdAt).toLocaleString()}</span> }
  ];

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  const canApprove = selected?.status === "Created";
  const canDeny = selected && selected.status !== "Cancelled" && selected.status !== "Denied"
                          && selected.status !== "Shipped" && selected.status !== "Delivered";
  const hasActiveDelivery = selected?.deliveryUserId != null
    && selected.deliveryStatus !== "Delivered"
    && selected.deliveryStatus !== "Failed";

  return (
    <div className="space-y-4">
      <PageHeader title="Orders" icon={Package} description={data ? `${data.total} total` : ""} />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <FilterBar onReset={() => { setStatus("all"); setPage(1); }}>
            <Select value={status} onValueChange={(v) => { setStatus(v as any); setPage(1); }}>
              <SelectTrigger className="w-full sm:w-40"><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
              </SelectContent>
            </Select>
          </FilterBar>

          <DataTable
            data={data?.items ?? []}
            columns={columns}
            rowKey={(o) => o.id}
            loading={isLoading}
            onRowClick={(o) => setSelectedId(o.id)}
            empty={<EmptyState icon={Package} title="No orders" description="Order history will appear here." />}
          />

          {totalPages > 1 && (
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Page {page} of {totalPages}</span>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <DetailsDrawer
        open={!!selectedId}
        onOpenChange={(v) => !v && setSelectedId(null)}
        title={selected ? `Order ${selected.orderNumber}` : ""}
        description={selected ? new Date(selected.createdAt).toLocaleString() : ""}
      >
        {selected && (
          <div className="space-y-4 text-sm">
            <div className="flex flex-wrap gap-2">
              <Badge variant={STATUS_VARIANT[selected.status] ?? "outline"}>{selected.status}</Badge>
              <Badge variant="outline">{selected.shipmentStatus}</Badge>
              <Badge variant="outline">{selected.paymentStatus}</Badge>
              {selected.deliveryStatus && <Badge variant="secondary">Delivery: {selected.deliveryStatus}</Badge>}
            </div>

            <div className="flex flex-wrap gap-2">
              <Button size="sm" disabled={!canApprove || approve.isPending}
                onClick={() => approve.mutate(selected.id)}>
                <Check className="h-4 w-4 mr-1" /> Approve
              </Button>
              <Button size="sm" variant="destructive" disabled={!canDeny || deny.isPending}
                onClick={() => deny.mutate(selected.id)}>
                <Ban className="h-4 w-4 mr-1" /> Deny
              </Button>
              <Button size="sm" variant="outline" asChild>
                <a href={ordersV2Api.invoiceUrl(selected.id)} target="_blank" rel="noreferrer">
                  <Printer className="h-4 w-4 mr-1" /> Print invoice
                </a>
              </Button>
            </div>

            <Separator />

            <div>
              <p className="text-xs uppercase tracking-wide text-muted-foreground mb-1">Customer</p>
              <div className="space-y-0.5">
                <p className="flex items-center gap-2"><UserIcon className="h-3.5 w-3.5 text-muted-foreground" /> {selected.customerName ?? "—"}</p>
                {selected.customerEmail && <p className="flex items-center gap-2 text-muted-foreground"><Mail className="h-3.5 w-3.5" /> {selected.customerEmail}</p>}
                {selected.customerPhone && <p className="flex items-center gap-2 text-muted-foreground"><Phone className="h-3.5 w-3.5" /> {selected.customerPhone}</p>}
              </div>
            </div>

            <div>
              <p className="text-xs uppercase tracking-wide text-muted-foreground mb-1">Ship to</p>
              <p className="whitespace-pre-line">{selected.shippingAddress}</p>
              <p className="text-muted-foreground">{[selected.shippingCity, selected.shippingCountry].filter(Boolean).join(", ")}</p>
            </div>

            <Separator />

            <div>
              <p className="text-xs uppercase tracking-wide text-muted-foreground mb-2 flex items-center gap-1">
                <Truck className="h-3.5 w-3.5" /> Delivery assignment
              </p>
              {selected.deliveryUserName ? (
                <div className="space-y-1">
                  <p>Courier: <span className="font-medium">{selected.deliveryUserName}</span></p>
                  <p className="text-xs text-muted-foreground">Status: {selected.deliveryStatus ?? "—"}</p>
                  {!hasActiveDelivery && (
                    <p className="text-xs text-muted-foreground">Closed assignment — reassign below if needed.</p>
                  )}
                </div>
              ) : (
                <p className="text-xs text-muted-foreground">No delivery person assigned yet.</p>
              )}

              <div className="space-y-2 pt-3">
                <Label>{hasActiveDelivery ? "Reassign to…" : "Assign delivery person"}</Label>
                <Select value={deliveryUserId} onValueChange={setDeliveryUserId}>
                  <SelectTrigger><SelectValue placeholder="Choose a courier…" /></SelectTrigger>
                  <SelectContent>
                    {(deliveryUsers ?? []).map((u) => (
                      <SelectItem key={u.userId} value={u.userId}>
                        {u.displayName} ({u.activeAssignmentsCount} active)
                      </SelectItem>
                    ))}
                    {deliveryUsers?.length === 0 && (
                      <SelectItem value="__none" disabled>No delivery users yet</SelectItem>
                    )}
                  </SelectContent>
                </Select>
                <Input
                  placeholder="Notes for the courier (optional)"
                  value={deliveryNotes}
                  onChange={(e) => setDeliveryNotes(e.target.value)}
                />
                <Button
                  size="sm"
                  disabled={!deliveryUserId || assignDelivery.isPending}
                  onClick={() => assignDelivery.mutate(selected.id)}
                >
                  {assignDelivery.isPending ? "Assigning…" : (hasActiveDelivery ? "Reassign" : "Assign")}
                </Button>
              </div>
            </div>

            <Separator />

            <div>
              <p className="font-semibold mb-1">Items</p>
              <div className="space-y-2 divide-y">
                {selected.items.map((i) => (
                  <div key={i.id} className="pt-2 first:pt-0 flex justify-between">
                    <div>
                      <p className="font-medium">{i.productName}</p>
                      <p className="text-xs text-muted-foreground">{i.storeName} · {i.quantity} × ${i.unitPrice.toFixed(2)}</p>
                    </div>
                    <div className="text-right">
                      <p className="font-semibold">${i.total.toFixed(2)}</p>
                      <p className="text-xs text-muted-foreground">commission ${i.commissionAmount.toFixed(2)}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div className="space-y-1 text-sm">
              <div className="flex justify-between text-muted-foreground">
                <span>Subtotal</span><span>${selected.subtotal.toFixed(2)}</span>
              </div>
              {selected.discountAmount != null && selected.discountAmount > 0 && (
                <div className="flex justify-between text-emerald-600">
                  <span>Coupon {selected.couponCode}</span><span>−${selected.discountAmount.toFixed(2)}</span>
                </div>
              )}
              <div className="flex justify-between text-muted-foreground">
                <span>Shipping</span><span>${selected.shippingFee.toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-muted-foreground">
                <span>Tax</span><span>${selected.tax.toFixed(2)}</span>
              </div>
              <Separator />
              <div className="flex justify-between font-semibold">
                <span>Total</span><span>${selected.total.toFixed(2)}</span>
              </div>
            </div>
          </div>
        )}
      </DetailsDrawer>
    </div>
  );
}
