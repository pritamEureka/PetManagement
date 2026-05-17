import { useEffect, useMemo, useRef, useState } from "react";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Plus, HeartHandshake, MapPin, Search } from "lucide-react";
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
import { adoptionApi } from "@/api/adoption";
import { animalTypes, animalSizes } from "@/lib/schemas";
import { CreateAdoptionDialog } from "./CreateAdoptionDialog";

export function AdoptionListPage() {
  const qc = useQueryClient();
  const [animal, setAnimal] = useState("");
  const [size, setSize] = useState("");
  const [location, setLocation] = useState("");
  const [vaccinated, setVaccinated] = useState(false);
  const [neutered, setNeutered] = useState(false);
  const [withChildren, setWithChildren] = useState(false);
  const [withPets, setWithPets] = useState(false);
  const [open, setOpen] = useState(false);

  const params = useMemo(() => ({
    animalType: animal || undefined,
    size: size || undefined,
    location: location || undefined,
    vaccinatedOnly: vaccinated || undefined,
    neuteredOnly: neutered || undefined,
    goodWithChildren: withChildren || undefined,
    goodWithOtherPets: withPets || undefined,
  }), [animal, size, location, vaccinated, neutered, withChildren, withPets]);

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
          <div className="w-44">
            <Select value={animal} onValueChange={setAnimal}>
              <SelectTrigger><SelectValue placeholder="Any animal" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="">Any animal</SelectItem>
                {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="w-40">
            <Select value={size} onValueChange={setSize}>
              <SelectTrigger><SelectValue placeholder="Any size" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="">Any size</SelectItem>
                {animalSizes.map((s) => <SelectItem key={s} value={s}>{s.replace("ExtraLarge", "Extra large")}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="relative flex-1 min-w-[180px] max-w-xs">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input className="pl-8" placeholder="Location" value={location} onChange={(e) => setLocation(e.target.value)} />
          </div>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={vaccinated} onChange={(e) => setVaccinated(e.currentTarget.checked)} /> Vaccinated</label>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={neutered} onChange={(e) => setNeutered(e.currentTarget.checked)} /> Neutered</label>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={withChildren} onChange={(e) => setWithChildren(e.currentTarget.checked)} /> Good with kids</label>
          <label className="flex items-center gap-2 text-sm"><Checkbox checked={withPets} onChange={(e) => setWithPets(e.currentTarget.checked)} /> Good with pets</label>
        </CardContent>
      </Card>

      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {[...Array(6)].map((_, i) => <Skeleton key={i} className="h-72 rounded-lg" />)}
        </div>
      ) : items.length === 0 ? (
        <EmptyState icon={HeartHandshake} title="No listings match your filters."
          description="Try widening your search, or check back later." />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
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
                    {l.location ? <span className="flex items-center gap-1 text-muted-foreground"><MapPin className="h-3 w-3" /> {l.location}</span> : <span />}
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
