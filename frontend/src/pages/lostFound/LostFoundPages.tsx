import { useEffect, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, MapPin, Plus, Search, Siren, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { MessageUserButton } from "@/components/common/MessageUserButton";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { toast } from "@/components/ui/sonner";
import { animalTypes } from "@/lib/schemas";
import { petsApi } from "@/api/pets";
import { lostFoundApi, type LostFoundKind, type LostFoundSummary } from "@/api/lostFound";
import { Field } from "@/pages/pets/detail/shared";

const ALL = "__all";

function timeAgo(iso: string) {
  const mins = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (mins < 60) return `${Math.max(mins, 1)} min ago`;
  if (mins < 1440) return `${Math.round(mins / 60)} h ago`;
  return `${Math.round(mins / 1440)} d ago`;
}

function ReportCard({ r }: { r: LostFoundSummary }) {
  return (
    <Link to={`/lost-found/${r.id}`} className="block">
      <Card className="overflow-hidden h-full hover:shadow-md transition-shadow">
        <div className="aspect-video bg-muted relative">
          {r.photoUrl && <img src={r.photoUrl} alt="" className="h-full w-full object-cover" />}
          <Badge variant={r.kind === "Lost" ? "destructive" : "success"} className="absolute top-2 left-2">
            {r.kind === "Lost" ? "Lost" : "Found"}
          </Badge>
          {r.status !== "Open" && <Badge variant="secondary" className="absolute top-2 right-2">{r.status}</Badge>}
        </div>
        <CardContent className="pt-3 space-y-1">
          <p className="font-semibold">{r.petName ?? `${r.animalType}${r.breed ? ` · ${r.breed}` : ""}`}</p>
          <p className="text-xs text-muted-foreground flex items-center gap-1"><MapPin className="h-3 w-3" />{r.area ? `${r.area}, ` : ""}{r.city}</p>
          <p className="text-sm line-clamp-2">{r.description}</p>
          <p className="text-xs text-muted-foreground">Last seen {timeAgo(r.lastSeenAt)}</p>
        </CardContent>
      </Card>
    </Link>
  );
}

// ---------- Browse ----------

export function LostFoundListPage() {
  const [kind, setKind] = useState<LostFoundKind | "all">("all");
  const [animalType, setAnimalType] = useState(ALL);
  const [city, setCity] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const { data, isLoading } = useQuery({
    queryKey: ["lost-found", kind, animalType, city, q, page],
    queryFn: () => lostFoundApi.search({
      kind: kind === "all" ? undefined : kind, animalType: animalType === ALL ? undefined : animalType,
      city: city || undefined, q: q || undefined, page,
    }),
  });
  useEffect(() => setPage(1), [kind, animalType, city, q]);
  const pages = data ? Math.ceil(data.total / data.pageSize) : 1;

  return (
    <div className="space-y-4">
      <PageHeader title="Lost & found" icon={Siren} description="Help reunite pets with their families."
        actions={
          <>
            <Button variant="outline" asChild><Link to="/lost-found/mine">My reports</Link></Button>
            <Button asChild><Link to="/lost-found/new"><Plus className="h-4 w-4 mr-1" /> Report a pet</Link></Button>
          </>
        } />

      <div className="flex flex-wrap gap-2 items-center">
        <Tabs value={kind} onValueChange={(v) => setKind(v as any)}>
          <TabsList>
            <TabsTrigger value="all">All</TabsTrigger>
            <TabsTrigger value="Lost">Lost</TabsTrigger>
            <TabsTrigger value="Found">Found</TabsTrigger>
          </TabsList>
        </Tabs>
        <Select value={animalType} onValueChange={setAnimalType}>
          <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>All animals</SelectItem>
            {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
          </SelectContent>
        </Select>
        <Input className="w-40" placeholder="City" value={city} onChange={(e) => setCity(e.target.value)} />
        <div className="relative flex-1 min-w-[180px]">
          <Search className="h-4 w-4 absolute left-2.5 top-2.5 text-muted-foreground" />
          <Input className="pl-8" placeholder="Colour, breed, area…" value={q} onChange={(e) => setQ(e.target.value)} />
        </div>
      </div>

      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{[...Array(6)].map((_, i) => <Skeleton key={i} className="h-72" />)}</div>
      ) : !data || data.items.length === 0 ? (
        <EmptyState icon={Siren} title="No open reports match" description="Try a different city or clear the filters." />
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{data.items.map((r) => <ReportCard key={r.id} r={r} />)}</div>
          {pages > 1 && (
            <div className="flex justify-center gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
              <span className="text-sm self-center">Page {page} of {pages}</span>
              <Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>Next</Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ---------- Mine ----------

export function MyLostFoundPage() {
  const { data = [], isLoading } = useQuery({ queryKey: ["lost-found", "mine"], queryFn: lostFoundApi.mine });
  return (
    <div className="space-y-4">
      <Button variant="ghost" size="sm" asChild><Link to="/lost-found"><ArrowLeft className="h-4 w-4 mr-1" /> Lost & found</Link></Button>
      <PageHeader title="My reports" icon={Siren} />
      {isLoading ? <Skeleton className="h-40" /> : data.length === 0 ? (
        <EmptyState icon={Siren} title="You haven't reported anything" action={<Button asChild><Link to="/lost-found/new">Report a pet</Link></Button>} />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{data.map((r) => <ReportCard key={r.id} r={r} />)}</div>
      )}
    </div>
  );
}

// ---------- Detail ----------

export function LostFoundDetailPage() {
  const { id = "" } = useParams();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [confirm, setConfirm] = useState<"reunited" | "close" | "delete" | null>(null);
  const { data, isLoading } = useQuery({ queryKey: ["lost-found", id], queryFn: () => lostFoundApi.get(id), enabled: !!id });
  const { data: matches = [] } = useQuery({
    queryKey: ["lost-found", id, "matches"], queryFn: () => lostFoundApi.matches(id),
    enabled: !!data && data.isMine && data.report.status === "Open",
  });

  if (isLoading) return <Skeleton className="h-96 max-w-3xl mx-auto" />;
  if (!data) return <p className="text-muted-foreground">Report not found.</p>;
  const r = data.report;

  async function act() {
    if (confirm === "delete") {
      await lostFoundApi.remove(id);
      toast.success("Report deleted.");
      navigate("/lost-found/mine");
    } else if (confirm) {
      await lostFoundApi.resolve(id, confirm === "reunited");
      toast.success(confirm === "reunited" ? "Wonderful news! 🎉" : "Report closed.");
      qc.invalidateQueries({ queryKey: ["lost-found"] });
      qc.invalidateQueries({ queryKey: ["pets"] });
    }
  }

  return (
    <div className="space-y-4 max-w-3xl mx-auto">
      <Button variant="ghost" size="sm" asChild><Link to="/lost-found"><ArrowLeft className="h-4 w-4 mr-1" /> Lost & found</Link></Button>
      <Card className="overflow-hidden">
        {r.photoUrl && <img src={r.photoUrl} alt="" className="w-full max-h-96 object-cover" />}
        <CardContent className="pt-5 space-y-3">
          <div className="flex items-start justify-between gap-3 flex-wrap">
            <div>
              <div className="flex gap-2 mb-1">
                <Badge variant={r.kind === "Lost" ? "destructive" : "success"}>{r.kind}</Badge>
                {r.status !== "Open" && <Badge variant="secondary">{r.status}</Badge>}
              </div>
              <h1 className="text-2xl font-bold">{r.petName ?? r.animalType}</h1>
              <p className="text-sm text-muted-foreground">
                {r.animalType}{r.breed ? ` · ${r.breed}` : ""}{r.color ? ` · ${r.color}` : ""}
              </p>
            </div>
            {!data.isMine && r.status === "Open" && (
              <MessageUserButton userId={r.reporterId} userName={r.reporterName} label={`Message ${r.reporterName.split(" ")[0]}`} />
            )}
          </div>
          <p className="whitespace-pre-line">{r.description}</p>
          <div className="grid sm:grid-cols-2 gap-2 text-sm">
            <p><span className="text-muted-foreground">Last seen:</span> {new Date(r.lastSeenAt).toLocaleString()}</p>
            <p><span className="text-muted-foreground">Where:</span> {r.area ? `${r.area}, ` : ""}{r.city}</p>
            {data.contactPhone && <p><span className="text-muted-foreground">Phone:</span> <a className="underline" href={`tel:${data.contactPhone}`}>{data.contactPhone}</a></p>}
            {data.microchipNumber && <p><span className="text-muted-foreground">Microchip:</span> <span className="font-mono">{data.microchipNumber}</span></p>}
            {data.tagNumber && <p><span className="text-muted-foreground">Tag:</span> {data.tagNumber}</p>}
            <p><span className="text-muted-foreground">Reported by:</span> {r.reporterName}</p>
          </div>
          {data.latitude != null && data.longitude != null && (
            <a className="text-sm text-primary underline inline-flex items-center gap-1" target="_blank" rel="noreferrer"
              href={`https://maps.google.com/?q=${data.latitude},${data.longitude}`}>
              <MapPin className="h-4 w-4" /> Open location in maps
            </a>
          )}
          {data.feedPostId && <p className="text-sm"><Link className="underline" to={`/feed/${data.feedPostId}`}>View the feed post</Link></p>}

          {data.isMine && r.status === "Open" && (
            <div className="flex flex-wrap gap-2 pt-2 border-t">
              <Button onClick={() => setConfirm("reunited")}>{r.kind === "Lost" ? "We're reunited!" : "Returned to owner"}</Button>
              <Button variant="outline" onClick={() => setConfirm("close")}>Close report</Button>
              <Button variant="ghost" onClick={() => setConfirm("delete")}><Trash2 className="h-4 w-4 mr-1" /> Delete</Button>
            </div>
          )}
        </CardContent>
      </Card>

      {data.isMine && r.status === "Open" && (
        <div className="space-y-2">
          <h2 className="font-semibold">Possible matches</h2>
          {matches.length === 0 ? (
            <p className="text-sm text-muted-foreground">No matching {r.kind === "Lost" ? "found" : "lost"} reports yet — we'll notify you when one appears.</p>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              {matches.map((m) => (
                <div key={m.report.id} className="space-y-1">
                  <ReportCard r={m.report} />
                  <p className="text-xs text-muted-foreground px-1"><Badge variant="info" className="mr-1">{m.score}%</Badge>{m.reasons.join(" · ")}</p>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      <ConfirmDialog
        open={!!confirm}
        onOpenChange={(v) => !v && setConfirm(null)}
        title={confirm === "delete" ? "Delete this report?" : confirm === "reunited" ? "Mark as reunited?" : "Close this report?"}
        description={confirm === "delete" ? "The report is removed for everyone." : "The report stops showing in searches."}
        confirmLabel={confirm === "delete" ? "Delete" : "Confirm"}
        destructive={confirm === "delete"}
        onConfirm={act}
      />
    </div>
  );
}

// ---------- Create ----------

export function CreateLostFoundPage() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [params] = useSearchParams();
  const { data: pets = [] } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const ownPets = pets.filter((p) => p.myAccess === "Owner" && !p.isLost);
  const nowLocal = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);

  const [f, setF] = useState({
    kind: "Lost" as LostFoundKind,
    petId: params.get("petId") ?? "",
    animalType: "Dog", breed: "", color: "", description: "", photoUrl: "",
    lastSeenAt: nowLocal, city: "", area: "", contactPreference: "Chat" as "Chat" | "Phone" | "Email",
    contactPhone: "", shareToFeed: true, latitude: null as number | null, longitude: null as number | null,
  });
  const [busy, setBusy] = useState(false);
  const linkedPet = ownPets.find((p) => p.id === f.petId);

  function useMyLocation() {
    if (!navigator.geolocation) return toast.error("Location isn't available in this browser.");
    navigator.geolocation.getCurrentPosition(
      (pos) => { setF((s) => ({ ...s, latitude: pos.coords.latitude, longitude: pos.coords.longitude })); toast.success("Location added."); },
      () => toast.error("Couldn't get your location."),
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!f.description.trim()) return toast.error("Describe the animal and what happened.");
    if (!f.city.trim()) return toast.error("City is required.");
    if (new Date(f.lastSeenAt) > new Date()) return toast.error("Last-seen time can't be in the future.");
    if (f.contactPreference === "Phone" && !f.contactPhone.trim()) return toast.error("Add a phone number or choose chat.");
    setBusy(true);
    try {
      const created = await lostFoundApi.create({
        kind: f.kind,
        petId: f.kind === "Lost" && f.petId ? f.petId : null,
        animalType: f.kind === "Lost" && f.petId ? null : f.animalType,
        breed: f.breed || null, color: f.color || null, description: f.description.trim(), photoUrl: f.photoUrl || null,
        lastSeenAt: new Date(f.lastSeenAt).toISOString(), city: f.city.trim(), area: f.area || null,
        latitude: f.latitude, longitude: f.longitude, contactPreference: f.contactPreference,
        contactPhone: f.contactPhone || null, shareToFeed: f.shareToFeed,
      });
      toast.success("Report published. People nearby have been alerted.");
      qc.invalidateQueries({ queryKey: ["lost-found"] });
      qc.invalidateQueries({ queryKey: ["pets"] });
      navigate(`/lost-found/${created.report.id}`);
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  return (
    <div className="space-y-4 max-w-2xl mx-auto">
      <Button variant="ghost" size="sm" asChild><Link to="/lost-found"><ArrowLeft className="h-4 w-4 mr-1" /> Lost & found</Link></Button>
      <PageHeader title="Report a pet" icon={Siren} description="Reports are shown publicly to signed-in users and nearby members are notified." />
      <Card>
        <CardContent className="pt-6">
          <form onSubmit={submit} className="space-y-4">
            <Tabs value={f.kind} onValueChange={(v) => setF({ ...f, kind: v as LostFoundKind })}>
              <TabsList className="grid grid-cols-2 w-full">
                <TabsTrigger value="Lost">I lost a pet</TabsTrigger>
                <TabsTrigger value="Found">I found an animal</TabsTrigger>
              </TabsList>
            </Tabs>

            {f.kind === "Lost" && ownPets.length > 0 && (
              <Field label="Which pet?">
                <Select value={f.petId || ALL} onValueChange={(v) => setF({ ...f, petId: v === ALL ? "" : v })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL}>Not one of my registered pets</SelectItem>
                    {ownPets.map((p) => <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </Field>
            )}

            {linkedPet ? (
              <p className="text-sm text-muted-foreground">We'll use {linkedPet.name}'s profile photo, breed, colour and microchip details.</p>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <Field label="Animal">
                  <Select value={f.animalType} onValueChange={(v) => setF({ ...f, animalType: v })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
                  </Select>
                </Field>
                <Field label="Breed" htmlFor="lb"><Input id="lb" value={f.breed} onChange={(e) => setF({ ...f, breed: e.target.value })} /></Field>
                <Field label="Colour" htmlFor="lc"><Input id="lc" value={f.color} onChange={(e) => setF({ ...f, color: e.target.value })} /></Field>
              </div>
            )}

            <Field label={f.kind === "Lost" ? "What happened? Any distinguishing marks?" : "Describe the animal and where it is now"} htmlFor="ld">
              <Textarea id="ld" rows={4} value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} />
            </Field>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <Field label="Last seen" htmlFor="ll"><Input id="ll" type="datetime-local" max={nowLocal} value={f.lastSeenAt} onChange={(e) => setF({ ...f, lastSeenAt: e.target.value })} /></Field>
              <Field label="City" htmlFor="lci"><Input id="lci" value={f.city} onChange={(e) => setF({ ...f, city: e.target.value })} /></Field>
              <Field label="Area / landmark" htmlFor="la"><Input id="la" value={f.area} onChange={(e) => setF({ ...f, area: e.target.value })} /></Field>
            </div>

            <div className="flex flex-wrap gap-2 items-center">
              <Button type="button" variant="outline" size="sm" onClick={useMyLocation}><MapPin className="h-3.5 w-3.5 mr-1" /> Use my location</Button>
              {f.latitude != null && <span className="text-xs text-muted-foreground">Location attached</span>}
              {!linkedPet && (
                <>
                  <FileUploadButton label={f.photoUrl ? "Change photo" : "Add photo"} onUploaded={(url) => setF({ ...f, photoUrl: url })} />
                  {f.photoUrl && <img src={f.photoUrl} alt="" className="h-10 w-10 rounded object-cover" />}
                </>
              )}
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <Field label="How should people reach you?">
                <Select value={f.contactPreference} onValueChange={(v) => setF({ ...f, contactPreference: v as any })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Chat">In-app chat</SelectItem>
                    <SelectItem value="Phone">Phone (shown on the report)</SelectItem>
                  </SelectContent>
                </Select>
              </Field>
              {f.contactPreference === "Phone" && (
                <Field label="Phone" htmlFor="lp"><Input id="lp" type="tel" value={f.contactPhone} onChange={(e) => setF({ ...f, contactPhone: e.target.value })} /></Field>
              )}
            </div>

            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={f.shareToFeed} onChange={(e) => setF({ ...f, shareToFeed: e.currentTarget.checked })} />
              Also post to the community feed
            </label>

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => navigate(-1)}>Cancel</Button>
              <Button type="submit" variant={f.kind === "Lost" ? "destructive" : "default"} disabled={busy}>
                {busy ? "Publishing..." : "Publish report"}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
