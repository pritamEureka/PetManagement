import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Package } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { PageHeader } from "@/components/common/PageHeader";
import { FilterBar } from "@/components/admin/FilterBar";
import { DetailsDrawer } from "@/components/admin/DetailsDrawer";
import { ordersV2Api, type Order, type OrderStatus } from "@/api/marketplace";

const STATUSES: OrderStatus[] = ["Created", "Confirmed", "Packed", "Shipped", "Delivered", "Cancelled", "Returned"];
const STATUS_VARIANT: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
  Created: "outline", Confirmed: "secondary", Packed: "secondary",
  Shipped: "default", Delivered: "default", Cancelled: "destructive", Returned: "outline"
};

export function AdminOrderManagementPage() {
  const [status, setStatus] = useState<OrderStatus | "all">("all");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<Order | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["admin-orders", status, page],
    queryFn: () => ordersV2Api.adminAll(status === "all" ? undefined : status, page, 50)
  });

  const columns: Column<Order>[] = [
    { key: "no", header: "Order #", render: (o) => <span className="font-mono text-xs">{o.orderNumber}</span> },
    { key: "buyer", header: "Buyer", render: (o) => <span className="text-xs text-muted-foreground">{o.userId.slice(0, 8)}…</span> },
    { key: "items", header: "Items", render: (o) => o.items.length, className: "text-right w-16" },
    { key: "total", header: "Total", className: "text-right w-24",
      render: (o) => <span className="font-semibold">${o.total.toFixed(2)}</span> },
    { key: "status", header: "Status",
      render: (o) => <Badge variant={STATUS_VARIANT[o.status] ?? "outline"}>{o.status}</Badge> },
    { key: "ship", header: "Shipment", render: (o) => <Badge variant="outline">{o.shipmentStatus}</Badge> },
    { key: "pay", header: "Payment", render: (o) => <Badge variant="outline">{o.paymentStatus}</Badge> },
    { key: "created", header: "Placed",
      render: (o) => <span className="text-xs text-muted-foreground">{new Date(o.createdAt).toLocaleString()}</span> }
  ];

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div className="space-y-4">
      <PageHeader title="Orders" icon={Package} description={data ? `${data.total} total` : ""} />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <FilterBar onReset={() => { setStatus("all"); setPage(1); }}>
            <Select value={status} onValueChange={(v) => { setStatus(v as any); setPage(1); }}>
              <SelectTrigger className="w-40"><SelectValue placeholder="Status" /></SelectTrigger>
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
            onRowClick={(o) => setSelected(o)}
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
        open={!!selected}
        onOpenChange={(v) => !v && setSelected(null)}
        title={selected ? `Order ${selected.orderNumber}` : ""}
        description={selected ? new Date(selected.createdAt).toLocaleString() : ""}
      >
        {selected && (
          <div className="space-y-4 text-sm">
            <div className="grid grid-cols-2 gap-3">
              <Field label="Status"   value={selected.status} />
              <Field label="Shipment" value={selected.shipmentStatus} />
              <Field label="Payment"  value={selected.paymentStatus} />
              <Field label="Total"    value={`$${selected.total.toFixed(2)}`} />
            </div>
            <div>
              <p className="text-xs text-muted-foreground mb-1">Ship to</p>
              <p className="whitespace-pre-line">{selected.shippingAddress}</p>
              <p className="text-muted-foreground">{[selected.shippingCity, selected.shippingCountry].filter(Boolean).join(", ")}</p>
            </div>
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
          </div>
        )}
      </DetailsDrawer>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string | number }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="font-medium">{value}</p>
    </div>
  );
}
