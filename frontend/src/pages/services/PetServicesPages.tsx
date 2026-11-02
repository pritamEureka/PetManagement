import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, CalendarDays, MapPin, Scissors, Star, Store } from "lucide-react";
import { Badge, type BadgeVariant } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { MessageUserButton } from "@/components/common/MessageUserButton";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { PromptDialog } from "@/components/common/PromptDialog";
import { Can } from "@/components/auth/Can";
import { toast } from "@/components/ui/sonner";
import { animalTypes } from "@/lib/schemas";
import { petsApi } from "@/api/pets";
import { petServicesApi, serviceTypes, type ProviderSummary, type ServiceBooking, type ServiceType, type RegisterProviderInput } from "@/api/petServices";
import { Field, fmtMoney } from "@/pages/pets/detail/shared";

const ALL = "__all";
const typeEmoji: Record<ServiceType, string> = { Grooming: "✂️", Training: "🎓", Boarding: "🏠", Walking: "🦮" };

const statusVariant: Record<string, BadgeVariant> = {
  PendingConfirmation: "warning", Confirmed: "info", Completed: "success",
  CancelledByUser: "muted", CancelledByDoctor: "muted",
};
const statusLabel = (s: string) =>
  s === "PendingConfirmation" ? "Awaiting confirmation" : s === "CancelledByDoctor" ? "Cancelled by provider" : s === "CancelledByUser" ? "Cancelled" : s;

function Stars({ value, count }: { value: number; count?: number }) {
  return (
    <span className="inline-flex items-center gap-1 text-sm">
      <Star className="h-3.5 w-3.5 fill-amber-400 text-amber-400" /> {value.toFixed(1)}
      {count !== undefined && <span className="text-muted-foreground">({count})</span>}
    </span>
  );
}

function ProviderCard({ p }: { p: ProviderSummary }) {
  return (
    <Link to={`/services/${p.id}`}>
      <Card className="h-full hover:shadow-md transition-shadow">
        <CardContent className="pt-5 flex gap-3">
          <div className="h-14 w-14 rounded-lg bg-muted overflow-hidden shrink-0 flex items-center justify-center text-2xl">
            {p.logoUrl ? <img src={p.logoUrl} alt="" className="h-full w-full object-cover" /> : typeEmoji[p.providerType]}
          </div>
          <div className="min-w-0 space-y-1">
            <p className="font-semibold truncate">{p.businessName}</p>
            <p className="text-xs text-muted-foreground flex items-center gap-1">
              <Badge variant="secondary">{p.providerType}</Badge>
              {p.city && <><MapPin className="h-3 w-3" />{p.city}</>}
            </p>
            <div className="flex items-center gap-3">
              <Stars value={p.ratingAverage} count={p.ratingCount} />
              <span className="text-sm font-medium">from {fmtMoney(p.basePrice)}</span>
            </div>
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}

// ---------- Browse ----------

export function ServicesPage() {
  const [type, setType] = useState<ServiceType | "all">("all");
  const [city, setCity] = useState("");
  const [animalType, setAnimalType] = useState(ALL);
  const [sort, setSort] = useState("rating");
  const { data, isLoading } = useQuery({
    queryKey: ["pet-services", type, city, animalType, sort],
    queryFn: () => petServicesApi.search({
      type: type === "all" ? undefined : type, city: city || undefined,
      animalType: animalType === ALL ? undefined : animalType, sort,
    }),
  });

  return (
    <div className="space-y-4">
      <PageHeader title="Pet services" icon={Scissors} description="Groomers, trainers, boarding and dog walkers near you."
        actions={
          <>
            <Button variant="outline" asChild><Link to="/services/bookings">My bookings</Link></Button>
            <Can permission="services.edit">
              <Button variant="outline" asChild><Link to="/services/dashboard"><Store className="h-4 w-4 mr-1" /> Provider dashboard</Link></Button>
            </Can>
          </>
        } />
      <div className="flex flex-wrap gap-2 items-center">
        <Tabs value={type} onValueChange={(v) => setType(v as any)}>
          <TabsList>
            <TabsTrigger value="all">All</TabsTrigger>
            {serviceTypes.map((t) => <TabsTrigger key={t} value={t}>{typeEmoji[t]} {t}</TabsTrigger>)}
          </TabsList>
        </Tabs>
        <Input className="w-40" placeholder="City" value={city} onChange={(e) => setCity(e.target.value)} />
        <Select value={animalType} onValueChange={setAnimalType}>
          <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Any animal</SelectItem>
            {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={sort} onValueChange={setSort}>
          <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="rating">Top rated</SelectItem>
            <SelectItem value="price_asc">Price: low to high</SelectItem>
            <SelectItem value="price_desc">Price: high to low</SelectItem>
            <SelectItem value="newest">Newest</SelectItem>
          </SelectContent>
        </Select>
      </div>
      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{[...Array(6)].map((_, i) => <Skeleton key={i} className="h-28" />)}</div>
      ) : !data || data.items.length === 0 ? (
        <EmptyState icon={Scissors} title="No providers found" description="Try another city or service type." />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{data.items.map((p) => <ProviderCard key={p.id} p={p} />)}</div>
      )}
    </div>
  );
}

// ---------- Provider detail + booking ----------

export function ServiceProviderDetailPage() {
  const { id = "" } = useParams();
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ["pet-services", "provider", id], queryFn: () => petServicesApi.get(id), enabled: !!id });
  const { data: pets = [] } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const minLocal = new Date(Date.now() + 60 * 60000 - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  const [f, setF] = useState({ petId: "", scheduledAt: "", notes: "" });
  const [busy, setBusy] = useState(false);

  if (isLoading) return <Skeleton className="h-96 max-w-3xl mx-auto" />;
  if (!data) return <p className="text-muted-foreground">Provider not found.</p>;
  const p = data.provider;
  const eligible = pets.filter((x) => (x.myAccess === "Owner" || x.myAccess === "CoOwner")
    && (p.animalTypes.length === 0 || p.animalTypes.includes(x.animalType)));

  async function book(e: React.FormEvent) {
    e.preventDefault();
    if (!f.scheduledAt) return toast.error("Pick a date and time.");
    if (new Date(f.scheduledAt).getTime() < Date.now() + 30 * 60000) return toast.error("Pick a time at least 30 minutes from now.");
    setBusy(true);
    try {
      await petServicesApi.book({ providerId: p.id, petId: f.petId || null, scheduledAt: new Date(f.scheduledAt).toISOString(), notes: f.notes || null });
      toast.success("Booking requested — you'll be notified when it's confirmed.");
      setF({ petId: "", scheduledAt: "", notes: "" });
      qc.invalidateQueries({ queryKey: ["pet-services", "bookings"] });
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  return (
    <div className="space-y-4 max-w-4xl mx-auto">
      <Button variant="ghost" size="sm" asChild><Link to="/services"><ArrowLeft className="h-4 w-4 mr-1" /> Services</Link></Button>
      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-4">
          <Card>
            <CardContent className="pt-6 space-y-3">
              <div className="flex gap-4 items-start">
                <div className="h-20 w-20 rounded-xl bg-muted overflow-hidden flex items-center justify-center text-3xl shrink-0">
                  {p.logoUrl ? <img src={p.logoUrl} alt="" className="h-full w-full object-cover" /> : typeEmoji[p.providerType]}
                </div>
                <div className="space-y-1">
                  <h1 className="text-2xl font-bold">{p.businessName}</h1>
                  <p className="text-sm text-muted-foreground">{p.providerType} · run by {data.ownerName}</p>
                  <div className="flex gap-3 items-center flex-wrap">
                    <Stars value={p.ratingAverage} count={p.ratingCount} />
                    <span className="text-sm text-muted-foreground">{data.completedBookings} completed bookings</span>
                  </div>
                </div>
              </div>
              {p.about && <p className="text-sm whitespace-pre-line">{p.about}</p>}
              <div className="text-sm space-y-1">
                {(data.address || p.city) && <p className="flex items-center gap-1"><MapPin className="h-4 w-4 text-muted-foreground" />{[data.address, p.city, p.country].filter(Boolean).join(", ")}</p>}
                {data.phoneNumber && <p>📞 <a href={`tel:${data.phoneNumber}`} className="underline">{data.phoneNumber}</a></p>}
                {p.animalTypes.length > 0 && <p className="text-muted-foreground">Serves: {p.animalTypes.join(", ")}</p>}
              </div>
              <MessageUserButton userId={p.userId} userName={p.businessName} label="Message provider" variant="outline" />
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle className="text-base">Reviews</CardTitle></CardHeader>
            <CardContent className="divide-y">
              {data.reviews.length === 0 ? <p className="text-sm text-muted-foreground">No reviews yet.</p> : data.reviews.map((r) => (
                <div key={r.id} className="py-2.5">
                  <div className="flex justify-between text-sm"><span className="font-medium">{r.userName}</span><Stars value={r.rating} /></div>
                  {r.comment && <p className="text-sm text-muted-foreground mt-1">{r.comment}</p>}
                </div>
              ))}
            </CardContent>
          </Card>
        </div>

        <Card className="h-fit lg:sticky lg:top-4">
          <CardHeader>
            <CardTitle className="text-base">Book</CardTitle>
            <p className="text-sm text-muted-foreground">{fmtMoney(p.basePrice)} · {p.defaultDurationMinutes} min · pay the provider directly</p>
          </CardHeader>
          <CardContent>
            {p.approvalStatus !== "Approved" ? <p className="text-sm text-muted-foreground">This provider isn't taking bookings yet.</p> : (
              <form onSubmit={book} className="space-y-3">
                <Field label="Pet">
                  <Select value={f.petId || ALL} onValueChange={(v) => setF({ ...f, petId: v === ALL ? "" : v })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value={ALL}>Not specified</SelectItem>
                      {eligible.map((x) => <SelectItem key={x.id} value={x.id}>{x.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </Field>
                <Field label="When" htmlFor="bw"><Input id="bw" type="datetime-local" min={minLocal} value={f.scheduledAt} onChange={(e) => setF({ ...f, scheduledAt: e.target.value })} /></Field>
                <Field label="Notes" htmlFor="bn"><Textarea id="bn" rows={3} placeholder="Temperament, special needs…" value={f.notes} onChange={(e) => setF({ ...f, notes: e.target.value })} /></Field>
                <Can permission="services.book" fallback={<p className="text-xs text-muted-foreground">Your account can't book services.</p>}>
                  <Button type="submit" className="w-full" disabled={busy}><CalendarDays className="h-4 w-4 mr-1" /> {busy ? "Requesting..." : "Request booking"}</Button>
                </Can>
              </form>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

// ---------- Customer bookings ----------

export function MyServiceBookingsPage() {
  const qc = useQueryClient();
  const { data = [], isLoading } = useQuery({ queryKey: ["pet-services", "bookings", "mine"], queryFn: petServicesApi.myBookings });
  const [cancelling, setCancelling] = useState<ServiceBooking | null>(null);
  const [reviewing, setReviewing] = useState<ServiceBooking | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: ["pet-services", "bookings"] });

  return (
    <div className="space-y-4">
      <Button variant="ghost" size="sm" asChild><Link to="/services"><ArrowLeft className="h-4 w-4 mr-1" /> Services</Link></Button>
      <PageHeader title="My service bookings" icon={CalendarDays} />
      {isLoading ? <Skeleton className="h-40" /> : data.length === 0 ? (
        <EmptyState icon={CalendarDays} title="No bookings yet" action={<Button asChild><Link to="/services">Find a provider</Link></Button>} />
      ) : (
        <div className="space-y-2">
          {data.map((b) => (
            <Card key={b.id}>
              <CardContent className="py-4 flex items-start justify-between gap-3 flex-wrap">
                <div>
                  <p className="font-medium"><Link className="hover:underline" to={`/services/${b.providerId}`}>{b.providerName}</Link> · {b.providerType}</p>
                  <p className="text-sm text-muted-foreground">
                    {new Date(b.scheduledAt).toLocaleString()}{b.petName ? ` · ${b.petName}` : ""} · {fmtMoney(b.amount)} · {b.paymentStatus}
                  </p>
                  {b.cancellationReason && <p className="text-xs text-muted-foreground">Reason: {b.cancellationReason}</p>}
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant={statusVariant[b.status] ?? "outline"}>{statusLabel(b.status)}</Badge>
                  {(b.status === "PendingConfirmation" || b.status === "Confirmed") && new Date(b.scheduledAt) > new Date() && (
                    <Button size="sm" variant="outline" onClick={() => setCancelling(b)}>Cancel</Button>
                  )}
                  {b.status === "Completed" && !b.hasReview && <Button size="sm" onClick={() => setReviewing(b)}>Review</Button>}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
      <PromptDialog
        open={!!cancelling}
        onOpenChange={(v) => !v && setCancelling(null)}
        title="Cancel booking?"
        label="Reason (optional)"
        confirmLabel="Cancel booking"
        onSubmit={async (reason: string) => { if (cancelling) { await petServicesApi.cancel(cancelling.id, reason || undefined); toast.success("Booking cancelled."); refresh(); } }}
      />
      {reviewing && <ReviewDialog booking={reviewing} onClose={() => setReviewing(null)} onDone={refresh} />}
    </div>
  );
}

function ReviewDialog({ booking, onClose, onDone }: { booking: ServiceBooking; onClose: () => void; onDone: () => void }) {
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" role="dialog" aria-modal="true" onClick={onClose}>
      <Card className="w-full max-w-md" onClick={(e) => e.stopPropagation()}>
        <CardHeader><CardTitle className="text-base">Review {booking.providerName}</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          <div className="flex gap-1" role="radiogroup" aria-label="Rating">
            {[1, 2, 3, 4, 5].map((n) => (
              <button key={n} type="button" role="radio" aria-checked={rating === n} aria-label={`${n} stars`} onClick={() => setRating(n)}>
                <Star className={`h-7 w-7 ${n <= rating ? "fill-amber-400 text-amber-400" : "text-muted-foreground"}`} />
              </button>
            ))}
          </div>
          <Textarea rows={3} placeholder="How did it go?" value={comment} onChange={(e) => setComment(e.target.value)} />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={onClose}>Cancel</Button>
            <Button disabled={busy} onClick={async () => {
              setBusy(true);
              try { await petServicesApi.review(booking.id, rating, comment || undefined); toast.success("Thanks for the review!"); onDone(); onClose(); }
              catch { /* toasted */ } finally { setBusy(false); }
            }}>Submit</Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

// ---------- Provider dashboard ----------

const emptyProfile: RegisterProviderInput = {
  providerType: "Grooming", businessName: "", about: "", address: "", city: "", country: "", phoneNumber: "",
  basePrice: 0, defaultDurationMinutes: 60, logoUrl: "", animalTypes: [],
};

export function ProviderDashboardPage() {
  const qc = useQueryClient();
  const { data: mine, isLoading, isError } = useQuery({ queryKey: ["pet-services", "me"], queryFn: petServicesApi.mine, retry: false });
  const { data: incoming = [] } = useQuery({
    queryKey: ["pet-services", "bookings", "incoming"], queryFn: () => petServicesApi.incoming(), enabled: !!mine,
  });
  const [f, setF] = useState<RegisterProviderInput>(emptyProfile);
  const [busy, setBusy] = useState(false);
  const [declining, setDeclining] = useState<ServiceBooking | null>(null);

  useEffect(() => {
    if (!mine) return;
    const p = mine.provider;
    setF({
      providerType: p.providerType, businessName: p.businessName, about: p.about ?? "", address: mine.address ?? "",
      city: p.city ?? "", country: p.country ?? "", phoneNumber: mine.phoneNumber ?? "", basePrice: p.basePrice,
      defaultDurationMinutes: p.defaultDurationMinutes, logoUrl: p.logoUrl ?? "", animalTypes: p.animalTypes,
    });
  }, [mine]);

  const refresh = () => qc.invalidateQueries({ queryKey: ["pet-services"] });

  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (!f.businessName.trim() || !f.city.trim()) return toast.error("Business name and city are required.");
    if (f.basePrice < 0) return toast.error("Price can't be negative.");
    if (f.defaultDurationMinutes < 15) return toast.error("Duration must be at least 15 minutes.");
    setBusy(true);
    const clean = { ...f, about: f.about || null, address: f.address || null, country: f.country || null, phoneNumber: f.phoneNumber || null, logoUrl: f.logoUrl || null };
    try {
      if (mine) { await petServicesApi.updateMine(clean); toast.success("Profile saved."); }
      else { await petServicesApi.register(clean); toast.success("Submitted for review — we'll notify you once approved."); }
      refresh();
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  async function act(fn: () => Promise<unknown>, msg: string) {
    try { await fn(); toast.success(msg); refresh(); } catch { /* toasted */ }
  }

  const status = mine?.provider.approvalStatus;
  const pending = incoming.filter((b) => b.status === "PendingConfirmation");
  const upcoming = incoming.filter((b) => b.status === "Confirmed");
  const past = incoming.filter((b) => !["PendingConfirmation", "Confirmed"].includes(b.status)).slice(-20).reverse();

  return (
    <div className="space-y-4 max-w-5xl mx-auto">
      <PageHeader title="Provider dashboard" icon={Store}
        description={mine ? "Manage your profile and bookings." : "Create your service profile — an admin reviews it before it goes live."}
        actions={mine && <Button variant="outline" asChild><Link to={`/services/${mine.provider.id}`}>View public page</Link></Button>} />

      {!isLoading && status && status !== "Approved" && (
        <Card className="border-amber-300">
          <CardContent className="py-3 text-sm">
            Status: <Badge variant={status === "Pending" ? "warning" : "destructive"}>{status}</Badge>
            {status === "Pending" && " — your profile is waiting for admin approval."}
            {status === "Rejected" && " — update your profile and save to resubmit."}
            {mine?.adminNotes && <p className="text-muted-foreground mt-1">Admin note: {mine.adminNotes}</p>}
          </CardContent>
        </Card>
      )}

      {mine && status === "Approved" && (
        <div className="grid lg:grid-cols-2 gap-4">
          <BookingList title={`Requests (${pending.length})`} rows={pending} empty="No pending requests." actions={(b) => (
            <>
              <Button size="sm" onClick={() => act(() => petServicesApi.confirm(b.id), "Booking confirmed.")}>Confirm</Button>
              <Button size="sm" variant="outline" onClick={() => setDeclining(b)}>Decline</Button>
            </>
          )} />
          <BookingList title={`Upcoming (${upcoming.length})`} rows={upcoming} empty="Nothing scheduled." actions={(b) => (
            <>
              {b.paymentStatus !== "Paid" && <Button size="sm" variant="outline" onClick={() => act(() => petServicesApi.markPaid(b.id), "Marked paid.")}>Paid</Button>}
              <Button size="sm" onClick={() => act(() => petServicesApi.complete(b.id), "Completed.")}>Complete</Button>
              <Button size="sm" variant="ghost" onClick={() => setDeclining(b)}>Cancel</Button>
            </>
          )} />
          {past.length > 0 && (
            <div className="lg:col-span-2">
              <BookingList title="History" rows={past} empty="" actions={(b) => b.status === "Completed" && b.paymentStatus !== "Paid"
                ? <Button size="sm" variant="outline" onClick={() => act(() => petServicesApi.markPaid(b.id), "Marked paid.")}>Mark paid</Button>
                : null} />
            </div>
          )}
        </div>
      )}

      {isError || !isLoading ? (
        <Card>
          <CardHeader><CardTitle className="text-base">{mine ? "Profile" : "Register as a provider"}</CardTitle></CardHeader>
          <CardContent>
            <form onSubmit={save} className="space-y-3">
              <div className="grid sm:grid-cols-2 gap-3">
                <Field label="Business name" htmlFor="pb"><Input id="pb" value={f.businessName} onChange={(e) => setF({ ...f, businessName: e.target.value })} /></Field>
                <Field label="Service">
                  <Select value={f.providerType} onValueChange={(v) => setF({ ...f, providerType: v as ServiceType })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{serviceTypes.map((t) => <SelectItem key={t} value={t}>{typeEmoji[t]} {t}</SelectItem>)}</SelectContent>
                  </Select>
                </Field>
                <Field label="Price per session" htmlFor="pp"><Input id="pp" type="number" min="0" value={f.basePrice} onChange={(e) => setF({ ...f, basePrice: Number(e.target.value) })} /></Field>
                <Field label="Session length (minutes)" htmlFor="pd"><Input id="pd" type="number" min="15" step="15" value={f.defaultDurationMinutes} onChange={(e) => setF({ ...f, defaultDurationMinutes: Number(e.target.value) })} /></Field>
                <Field label="City" htmlFor="pc"><Input id="pc" value={f.city} onChange={(e) => setF({ ...f, city: e.target.value })} /></Field>
                <Field label="Country" htmlFor="pco"><Input id="pco" value={f.country ?? ""} onChange={(e) => setF({ ...f, country: e.target.value })} /></Field>
                <Field label="Address" htmlFor="pa"><Input id="pa" value={f.address ?? ""} onChange={(e) => setF({ ...f, address: e.target.value })} /></Field>
                <Field label="Phone" htmlFor="pph"><Input id="pph" type="tel" value={f.phoneNumber ?? ""} onChange={(e) => setF({ ...f, phoneNumber: e.target.value })} /></Field>
              </div>
              <Field label="About" htmlFor="pab"><Textarea id="pab" rows={3} value={f.about ?? ""} onChange={(e) => setF({ ...f, about: e.target.value })} /></Field>
              <Field label="Animals you serve (none selected = all)">
                <div className="flex flex-wrap gap-1.5">
                  {animalTypes.map((t) => {
                    const on = f.animalTypes?.includes(t);
                    return (
                      <button key={t} type="button" aria-pressed={on}
                        className={`text-xs rounded-full border px-2.5 py-1 ${on ? "bg-primary text-primary-foreground border-primary" : "hover:bg-muted"}`}
                        onClick={() => setF({ ...f, animalTypes: on ? f.animalTypes!.filter((x) => x !== t) : [...(f.animalTypes ?? []), t] })}>
                        {t}
                      </button>
                    );
                  })}
                </div>
              </Field>
              <div className="flex items-center gap-3">
                {f.logoUrl && <img src={f.logoUrl} alt="" className="h-12 w-12 rounded object-cover" />}
                <FileUploadButton label={f.logoUrl ? "Change logo" : "Upload logo"} onUploaded={(url) => setF({ ...f, logoUrl: url })} />
              </div>
              <Button type="submit" disabled={busy}>{busy ? "Saving..." : mine ? "Save profile" : "Submit for approval"}</Button>
            </form>
          </CardContent>
        </Card>
      ) : <Skeleton className="h-64" />}

      <PromptDialog
        open={!!declining}
        onOpenChange={(v) => !v && setDeclining(null)}
        title="Cancel this booking?"
        label="Message to the customer (optional)"
        confirmLabel="Cancel booking"
        onSubmit={async (reason: string) => { if (declining) await act(() => petServicesApi.cancel(declining.id, reason || undefined), "Booking cancelled."); }}
      />
    </div>
  );
}

function BookingList({ title, rows, empty, actions }: {
  title: string; rows: ServiceBooking[]; empty: string; actions: (b: ServiceBooking) => React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader className="pb-2"><CardTitle className="text-base">{title}</CardTitle></CardHeader>
      <CardContent className="divide-y">
        {rows.length === 0 ? <p className="text-sm text-muted-foreground py-2">{empty}</p> : rows.map((b) => (
          <div key={b.id} className="py-2.5 flex items-start justify-between gap-3 flex-wrap">
            <div className="text-sm min-w-0">
              <p className="font-medium">{b.customerName}{b.petName ? ` · ${b.petName}` : ""}</p>
              <p className="text-xs text-muted-foreground">{new Date(b.scheduledAt).toLocaleString()} · {fmtMoney(b.amount)} · {b.paymentStatus}</p>
              {b.notes && <p className="text-xs text-muted-foreground">“{b.notes}”</p>}
              {!["PendingConfirmation", "Confirmed"].includes(b.status) && <Badge variant={statusVariant[b.status] ?? "outline"} className="mt-1">{statusLabel(b.status)}</Badge>}
            </div>
            <div className="flex gap-1.5 flex-wrap">
              <MessageUserButton userId={b.userId} userName={b.customerName} iconOnly variant="ghost" size="sm" />
              {actions(b)}
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
