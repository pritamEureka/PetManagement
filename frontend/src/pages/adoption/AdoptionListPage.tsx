import { useEffect, useMemo, useRef, useState } from "react";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { Plus, HeartHandshake, MapPin, Search, ArrowUpDown, Clock, LocateFixed, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Checkbox } from "@/components/ui/checkbox";
import { Can } from "@/components/auth/Can";
import { PageHeader } from "@/components/common/PageHeader";
import { EmptyState } from "@/components/common/EmptyState";
import { adoptionApi, type AdoptionSort } from "@/api/adoption";
import { useGeolocation, formatDistance, type GeoStatus } from "@/hooks/useGeolocation";
import { animalTypes, animalSizes } from "@/lib/schemas";
import { CreateAdoptionDialog } from "./CreateAdoptionDialog";

// Sentinel for "no filter" Select items — Radix Select disallows empty-string values.
const ANY = "any";
// Sort = server default: nearest when we know where the viewer is, otherwise newest.
const AUTO = "auto";
const RADII = [5, 10, 25, 50, 100];
const POSTED = [
  { days: 1, label: "Last 24 hours" },
  { days: 7, label: "Last 7 days" },
  { days: 30, label: "Last 30 days" },
  { days: 90, label: "Last 3 months" },
];

export function AdoptionListPage() {
  const qc = useQueryClient();
  const [animal, setAnimal] = useState<string>(ANY);
  const [size, setSize] = useState<string>(ANY);
  const [location, setLocation] = useState("");
  const [vaccinated, setVaccinated] = useState(false);
  const [neutered, setNeutered] = useState(false);
  const [withChildren, setWithChildren] = useState(false);
  const [withPets, setWithPets] = useState(false);
  const [open, setOpen] = useState(false);

  // Sort / distance / time live in the URL so back-navigation and shared links keep them.
  const [search, setSearch] = useSearchParams();
  const sort = search.get("sort") ?? AUTO;
  const radius = search.get("radius") ?? ANY;
  const posted = search.get("posted") ?? ANY;
  const setParam = (key: string, value: string, fallback: string) => {
    const next = new URLSearchParams(search);
    if (value === fallback) next.delete(key); else next.set(key, value);
    setSearch(next, { replace: true });
  };

  // Asks once; the browser remembers the answer. Results fall back to newest-first without it.
  const geo = useGeolocation({ auto: true });
  const near = geo.status === "ready" && geo.coords
    // ~110 m precision is plenty for ranking and keeps the query key stable.
    ? { lat: Math.round(geo.coords.lat * 1000) / 1000, lng: Math.round(geo.coords.lng * 1000) / 1000 }
    : null;
  const effectiveSort = sort === AUTO ? (near ? "nearest" : "newest") : sort;

  const params = useMemo(() => ({
    animalType: animal === ANY ? undefined : animal,
    size: size === ANY ? undefined : size,
    location: location || undefined,
    vaccinatedOnly: vaccinated || undefined,
    neuteredOnly: neutered || undefined,
    goodWithChildren: withChildren || undefined,
    goodWithOtherPets: withPets || undefined,
    sort: effectiveSort as AdoptionSort,
    lat: near?.lat,
    lng: near?.lng,
    radiusKm: near && radius !== ANY ? Number(radius) : undefined,
    postedWithinDays: posted === ANY ? undefined : Number(posted),
  }), [animal, size, location, vaccinated, neutered, withChildren, withPets, effectiveSort, near?.lat, near?.lng, radius, posted]);

  const queryKey = ["adoption", "list", params];
  const {
    data, fetchNextPage, hasNextPage, isFetchingNextPage, isLoading
  } = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => adoptionApi.list({ ...params, cursor: pageParam, pageSize: 24 }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined
  });
  const items = useMemo(() => (data?.pages ?? []).flatMap((p) => p.items), [data]);

  const sentinel = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    if (!sentinel.current) return;
    const io = new IntersectionObserver((e) => {
      if (e[0]?.isIntersecting && hasNextPage && !isFetchingNextPage) fetchNextPage();
    }, { rootMargin: "200px" });
    io.observe(sentinel.current);
    return () => io.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  return (
    <div className="space-y-4">
      <PageHeader title="Adoption" icon={HeartHandshake}
        description="Find your next best friend, or list a pet that needs a home."
        actions={
          <Can permission="adoption.create">
            <Button onClick={() => setOpen(true)}><Plus className="h-4 w-4 mr-2" /> List a pet</Button>
          </Can>
        } />

      <Card>
        <CardContent className="pt-6 flex flex-wrap gap-3 items-center">
          <div className="w-full sm:w-44">
            <Select value={animal} onValueChange={setAnimal}>
              <SelectTrigger><SelectValue placeholder="Any animal" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any animal</SelectItem>
                {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="w-full sm:w-40">
            <Select value={size} onValueChange={setSize}>
              <SelectTrigger><SelectValue placeholder="Any size" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any size</SelectItem>
                {animalSizes.map((s) => <SelectItem key={s} value={s}>{s.replace("ExtraLarge", "Extra large")}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="relative flex-1 min-w-[140px]">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input className="pl-8" placeholder="Place (area or city)" aria-label="Filter by place"
              value={location} onChange={(e) => setLocation(e.target.value)} />
          </div>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={vaccinated} onChange={(e) => setVaccinated(e.currentTarget.checked)} /> Vaccinated</label>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={neutered} onChange={(e) => setNeutered(e.currentTarget.checked)} /> Neutered</label>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={withChildren} onChange={(e) => setWithChildren(e.currentTarget.checked)} /> Good with kids</label>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={withPets} onChange={(e) => setWithPets(e.currentTarget.checked)} /> Good with pets</label>

          <div className="w-full border-t pt-3 flex flex-wrap gap-3 items-center">
            <div className="w-full sm:w-48">
              <Select value={sort === AUTO ? effectiveSort : sort} onValueChange={(v) => setParam("sort", v, AUTO)}>
                <SelectTrigger aria-label="Sort"><ArrowUpDown className="h-4 w-4 mr-2 opacity-60" /><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="nearest" disabled={!near}>Nearest first</SelectItem>
                  <SelectItem value="newest">Newest first</SelectItem>
                  <SelectItem value="oldest">Oldest first</SelectItem>
                  <SelectItem value="fee_asc">Fee: low to high</SelectItem>
                  <SelectItem value="fee_desc">Fee: high to low</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="w-full sm:w-44">
              <Select value={radius} onValueChange={(v) => setParam("radius", v, ANY)} disabled={!near}>
                <SelectTrigger aria-label="Distance"><MapPin className="h-4 w-4 mr-2 opacity-60" /><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ANY}>Any distance</SelectItem>
                  {RADII.map((r) => <SelectItem key={r} value={String(r)}>Within {r} km</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="w-full sm:w-44">
              <Select value={posted} onValueChange={(v) => setParam("posted", v, ANY)}>
                <SelectTrigger aria-label="Posted"><Clock className="h-4 w-4 mr-2 opacity-60" /><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ANY}>Any time</SelectItem>
                  {POSTED.map((p) => <SelectItem key={p.days} value={String(p.days)}>{p.label}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <NearMeStatus status={geo.status} onRetry={geo.request} />
          </div>
        </CardContent>
      </Card>

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
          {[...Array(6)].map((_, i) => <Skeleton key={i} className="h-72 rounded-lg" />)}
        </div>
      ) : items.length === 0 ? (
        <EmptyState icon={HeartHandshake} title="No listings match your filters."
          description="Try widening your search, or check back later." />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
          {items.map((l) => (
            <Link key={l.id} to={`/adoption/${l.id}`}>
              <Card className="overflow-hidden hover:shadow-md transition-shadow h-full">
                <div className="aspect-square bg-muted relative">
                  {l.photoUrls?.[0]
                    ? <img src={l.photoUrls[0]} className="object-cover w-full h-full" />
                    : <div className="flex items-center justify-center h-full text-muted-foreground"><HeartHandshake className="h-10 w-10" /></div>}
                  <div className="absolute top-2 right-2 flex gap-1">
                    {l.vaccinated && <Badge variant="secondary" className="text-[10px]">Vaccinated</Badge>}
                    {l.neuteredSpayed && <Badge variant="secondary" className="text-[10px]">Neutered</Badge>}
                  </div>
                </div>
                <CardContent className="pt-4 space-y-2">
                  <p className="font-semibold line-clamp-1">{l.petName ?? l.title}</p>
                  <p className="text-xs text-muted-foreground">{l.animalType}{l.breed ? ` · ${l.breed}` : ""}{l.size ? ` · ${l.size.replace("ExtraLarge", "XL")}` : ""}</p>
                  <div className="flex items-center justify-between text-xs">
                    {l.location || l.distanceKm != null ? (
                      <span className="flex items-center gap-1 text-muted-foreground min-w-0">
                        <MapPin className="h-3 w-3 shrink-0" />
                        <span className="truncate">
                          {l.distanceKm != null && <span className="font-medium text-foreground">{formatDistance(l.distanceKm)}</span>}
                          {l.distanceKm != null && l.location ? " · " : ""}{l.location}
                        </span>
                      </span>
                    ) : <span />}
                    <span className="font-semibold">{l.adoptionFee > 0 ? `$${l.adoptionFee.toFixed(2)}` : "Free"}</span>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}

      {hasNextPage && (
        <div ref={sentinel} className="py-4 text-center text-sm text-muted-foreground">
          {isFetchingNextPage ? "Loading more..." : "Scroll for more"}
        </div>
      )}

      <CreateAdoptionDialog open={open} onOpenChange={setOpen}
        onCreated={() => qc.invalidateQueries({ queryKey: ["adoption", "list"] })} />
    </div>
  );
}

function NearMeStatus({ status, onRetry }: { status: GeoStatus; onRetry: () => void }) {
  if (status === "ready") {
    return <span className="text-xs text-emerald-700 dark:text-emerald-400 flex items-center gap-1"><LocateFixed className="h-3.5 w-3.5" /> Showing pets near you first</span>;
  }
  if (status === "locating") {
    return <span className="text-xs text-muted-foreground flex items-center gap-1"><Loader2 className="h-3.5 w-3.5 animate-spin" /> Finding your location…</span>;
  }
  return (
    <span className="text-xs text-muted-foreground flex items-center gap-2 flex-wrap">
      {status === "denied" ? "Location is blocked — allow it in your browser to see nearby pets first." : "See pets near you first."}
      <Button size="sm" variant="outline" className="h-7 text-xs" onClick={onRetry}><LocateFixed className="h-3.5 w-3.5 mr-1" /> Use my location</Button>
    </span>
  );
}
