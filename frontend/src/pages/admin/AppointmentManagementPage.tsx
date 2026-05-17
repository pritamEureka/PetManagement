import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Calendar } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { DataTable, type Column } from "@/components/common/DataTable";
import { EmptyState } from "@/components/common/EmptyState";
import { PageHeader } from "@/components/common/PageHeader";
import { FilterBar } from "@/components/admin/FilterBar";
import { adminApi, type AdminAppointment } from "@/api/adminV2";

// Status set lifted from Domain.Common.AppointmentStatus. Keep in sync if the
// enum gets new entries; the API will silently ignore unknown filters.
const STATUSES = [
  "Draft", "PendingPayment", "PendingConfirmation", "Confirmed",
  "Rescheduled", "CancelledByUser", "CancelledByDoctor",
  "Completed", "NoShow", "Refunded"
];

const STATUS_VARIANT: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
  Completed: "default",
  Confirmed: "secondary",
  PendingConfirmation: "secondary",
  PendingPayment: "outline",
  Draft: "outline",
  Refunded: "outline",
  CancelledByUser: "destructive",
  CancelledByDoctor: "destructive",
  NoShow: "destructive"
};

export function AdminAppointmentManagementPage() {
  const [status, setStatus] = useState<string>("all");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 50;

  const { data, isLoading } = useQuery({
    queryKey: ["admin-appointments", { status, q, page }],
    queryFn: () => adminApi.appointments({
      status: status === "all" ? undefined : status,
      q: q || undefined,
      page,
      pageSize
    })
  });

  const columns: Column<AdminAppointment>[] = [
    {
      key: "when", header: "When",
      render: (a) => (
        <div>
          <p className="text-sm font-medium">{new Date(a.scheduledAt).toLocaleString()}</p>
          <p className="text-[10px] text-muted-foreground">created {new Date(a.createdAt).toLocaleDateString()}</p>
        </div>
      )
    },
    { key: "doctor", header: "Doctor", render: (a) => <span className="truncate">{a.doctorName}</span> },
    { key: "user",   header: "Patient", render: (a) => <span className="truncate">{a.userDisplayName}</span> },
    {
      key: "status", header: "Status",
      render: (a) => <Badge variant={STATUS_VARIANT[a.status] ?? "outline"}>{a.status}</Badge>
    },
    { key: "pay",  header: "Payment", render: (a) => <Badge variant="outline">{a.paymentStatus}</Badge> },
    { key: "amount", header: "Amount", className: "text-right w-24",
      render: (a) => <span className="font-semibold">${a.amount.toFixed(2)}</span> }
  ];

  const totalPages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1;

  return (
    <div className="space-y-4">
      <PageHeader title="Appointments" icon={Calendar} description={data ? `${data.total} total` : ""} />

      <Card>
        <CardContent className="pt-6 space-y-4">
          <FilterBar
            search={q}
            onSearch={(s) => { setQ(s); setPage(1); }}
            placeholder="Search by doctor or patient"
            onReset={() => { setQ(""); setStatus("all"); setPage(1); }}
          >
            <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
              <SelectTrigger className="w-52"><SelectValue placeholder="Status" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {STATUSES.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
              </SelectContent>
            </Select>
          </FilterBar>

          <DataTable
            data={data?.items ?? []}
            columns={columns}
            rowKey={(a) => a.id}
            loading={isLoading}
            empty={<EmptyState icon={Calendar} title="No appointments" description="Bookings will show up here." />}
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
    </div>
  );
}
