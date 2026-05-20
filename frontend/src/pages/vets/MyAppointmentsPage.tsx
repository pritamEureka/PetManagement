import { useEffect, useMemo, useRef, useState } from "react";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Calendar, Video, Building2, X, Pill, RotateCcw, CreditCard, Star } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { appointmentsApi, type Appointment, type AppointmentStatus } from "@/api/vets";
import { AppointmentStatusBadge } from "@/components/vet/AppointmentStatusBadge";
import { ReviewDialog } from "@/components/vet/ReviewDialog";
import { toast } from "@/components/ui/sonner";
import { PromptDialog } from "@/components/common/PromptDialog";

// Radix Select disallows empty-string item values (it reserves "" to mean "cleared").
// "all" is the sentinel for the no-filter case, converted to `undefined` at the API.
type StatusFilter = AppointmentStatus | "all";

const FILTER_STATUSES: { label: string; value: StatusFilter }[] = [
  { label: "All",                  value: "all" },
  { label: "Pending payment",      value: "PendingPayment" },
  { label: "Pending confirmation", value: "PendingConfirmation" },
  { label: "Confirmed",            value: "Confirmed" },
  { label: "Completed",            value: "Completed" },
  { label: "Cancelled (you)",      value: "CancelledByUser" },
  { label: "Cancelled (doctor)",   value: "CancelledByDoctor" },
];

export function MyAppointmentsPage() {
  const qc = useQueryClient();
  const [status, setStatus] = useState<StatusFilter>("all");
  const [cancelTarget, setCancelTarget] = useState<Appointment | null>(null);
  const [reviewTarget, setReviewTarget] = useState<Appointment | null>(null);
  const sentinel = useRef<HTMLDivElement | null>(null);

  const {
    data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading
  } = useInfiniteQuery({
    queryKey: ["appointments", "mine", status],
    queryFn: ({ pageParam }) => appointmentsApi.mine({
      cursor: pageParam, pageSize: 20,
      status: status === "all" ? undefined : (status as AppointmentStatus)
    }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined
  });

  const items = useMemo(() => (data?.pages ?? []).flatMap((p) => p.items), [data]);

  useEffect(() => {
    if (!sentinel.current) return;
    const io = new IntersectionObserver((e) => {
      if (e[0]?.isIntersecting && hasNextPage && !isFetchingNextPage) fetchNextPage();
    });
    io.observe(sentinel.current);
    return () => io.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  function invalidate() { qc.invalidateQueries({ queryKey: ["appointments", "mine"] }); }

  async function doCancel(reason: string) {
    if (!cancelTarget) return;
    try { await appointmentsApi.cancel(cancelTarget.id, reason); toast.success("Cancelled."); invalidate(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Failed."); }
  }
  async function pay(a: Appointment) {
    try { await appointmentsApi.pay(a.id); toast.success("Marked as paid."); invalidate(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Failed."); }
  }
  async function joinCall(a: Appointment) {
    try {
      const res = await appointmentsApi.meetingLink(a.id);
      window.open(res.url, "_blank", "noreferrer");
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Can't open meeting.");
    }
  }
  async function doReview(rating: number, comment: string) {
    if (!reviewTarget) return;
    try { await appointmentsApi.review(reviewTarget.id, rating, comment || undefined); toast.success("Thanks for the review!"); invalidate(); }
    catch (err: any) { toast.error(err?.response?.data?.error?.message ?? "Failed."); }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="My appointments" icon={Calendar}
        description="Upcoming, past, and pending vet bookings." />

      <div className="w-full sm:w-64">
        <Select value={status} onValueChange={(v) => setStatus(v as StatusFilter)}>
          <SelectTrigger><SelectValue placeholder="Filter status" /></SelectTrigger>
          <SelectContent>
            {FILTER_STATUSES.map((s) => <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (
        <div className="space-y-3">{[...Array(3)].map((_, i) => <Skeleton key={i} className="h-28" />)}</div>
      ) : items.length === 0 ? (
        <EmptyState icon={Calendar} title="No appointments yet"
          description="Find a vet and book your first consultation."
          action={<Button asChild><Link to="/vets">Find a vet</Link></Button>} />
      ) : (
        <div className="space-y-2">
          {items.map((a) => <AppointmentRow key={a.id} a={a}
            onCancel={() => setCancelTarget(a)} onPay={() => pay(a)} onJoin={() => joinCall(a)} onReview={() => setReviewTarget(a)} />)}
        </div>
      )}

      {hasNextPage && (
        <div ref={sentinel} className="py-4 text-center text-sm text-muted-foreground">
          {isFetchingNextPage ? "Loading more..." : "Scroll for more"}
        </div>
      )}

      <PromptDialog
        open={!!cancelTarget}
        onOpenChange={(v) => !v && setCancelTarget(null)}
        title={`Cancel appointment with Dr. ${cancelTarget?.doctorName ?? ""}?`}
        description="Let the doctor know why you're cancelling."
        label="Reason for cancellation"
        placeholder="Why are you cancelling?"
        confirmLabel="Cancel appointment"
        destructive
        multiline
        onSubmit={doCancel}
      />

      <ReviewDialog
        open={!!reviewTarget}
        onOpenChange={(v) => !v && setReviewTarget(null)}
        title={`Review Dr. ${reviewTarget?.doctorName ?? ""}`}
        description="Your feedback helps other pet owners find the right vet."
        onSubmit={doReview}
      />
    </div>
  );
}

function AppointmentRow({ a, onCancel, onPay, onJoin, onReview }: {
  a: Appointment; onCancel: () => void; onPay: () => void; onJoin: () => void; onReview: () => void;
}) {
  const Icon = a.type === "Online" ? Video : Building2;
  const isOnline = a.type === "Online";
  const upcoming = new Date(a.scheduledAt) > new Date();
  const canCancel = upcoming && !["Completed","NoShow","CancelledByUser","CancelledByDoctor","Refunded"].includes(a.status);
  const canPay = a.status === "PendingPayment";
  const canReview = a.status === "Completed";

  return (
    <Card>
      <CardContent className="py-3 flex flex-col sm:flex-row items-start sm:items-center gap-2 sm:gap-3">
        <div className="h-10 w-10 rounded-md bg-primary/10 flex items-center justify-center"><Icon className="h-5 w-5 text-primary" /></div>
        <div className="flex-1 min-w-0">
          <p className="font-medium truncate">Dr. {a.doctorName}</p>
          <p className="text-xs text-muted-foreground">
            {new Date(a.scheduledAt).toLocaleString()} · {a.durationMinutes}min · {a.type}
            {a.petName ? ` · ${a.petName}` : ""}
          </p>
        </div>
        <div className="flex items-center flex-wrap gap-2">
          <AppointmentStatusBadge status={a.status} />
          {canPay && <Button size="sm" onClick={onPay}><CreditCard className="h-3.5 w-3.5 mr-1" /> Pay</Button>}
          {isOnline && a.status === "Confirmed" && <Button size="sm" onClick={onJoin}><Video className="h-3.5 w-3.5 mr-1" /> Join</Button>}
          {a.prescriptionFileUrl && (
            <Button size="sm" variant="outline" asChild>
              <a href={a.prescriptionFileUrl} target="_blank" rel="noreferrer"><Pill className="h-3.5 w-3.5 mr-1" /> Rx</a>
            </Button>
          )}
          {canReview && <Button size="sm" variant="outline" onClick={onReview}><Star className="h-3.5 w-3.5 mr-1" /> Review</Button>}
          {canCancel && <Button size="sm" variant="destructive" onClick={onCancel}><X className="h-3.5 w-3.5" /></Button>}
        </div>
      </CardContent>
    </Card>
  );
}
