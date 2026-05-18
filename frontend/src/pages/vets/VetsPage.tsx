import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Search, Stethoscope, Star, MapPin } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { vetsApi } from "@/api/vets";
import { animalTypes, consultationTypes } from "@/lib/schemas";

// "any" is the sentinel for no-filter — Radix Select disallows empty-string values.
const ANY = "any";

export function VetsPage() {
  const [city, setCity] = useState("");
  const [animal, setAnimal] = useState<string>(ANY);
  const [type, setType] = useState<string>(ANY);

  const { data, isLoading } = useQuery({
    queryKey: ["vets", { city, animal, type }],
    queryFn: () => vetsApi.search({
      city: city || undefined,
      animalType: animal === ANY ? undefined : animal,
      type: type === ANY ? undefined : type,
      pageSize: 24
    })
  });

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Stethoscope className="h-6 w-6 text-primary" /> Find a veterinarian
        </h1>
        <p className="text-sm text-muted-foreground">Approved doctors only. Book online or in-clinic visits.</p>
      </div>

      <Card>
        <CardContent className="pt-6 flex flex-wrap gap-3">
          <div className="relative flex-1 min-w-[140px]">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input className="pl-8" placeholder="City" value={city} onChange={(e) => setCity(e.target.value)} />
          </div>
          <div className="w-full sm:w-44">
            <Select value={animal} onValueChange={setAnimal}>
              <SelectTrigger><SelectValue placeholder="Animal type" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any</SelectItem>
                {animalTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="w-full sm:w-44">
            <Select value={type} onValueChange={setType}>
              <SelectTrigger><SelectValue placeholder="Consultation" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any</SelectItem>
                {consultationTypes.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      {isLoading ? (
        <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
          {[...Array(6)].map((_, i) => <Skeleton key={i} className="h-32" />)}
        </div>
      ) : !data || data.items.length === 0 ? (
        <Card><CardContent className="py-16 text-center text-muted-foreground">No vets match those filters.</CardContent></Card>
      ) : (
        <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
          {data.items.map((d) => (
            <Link key={d.id} to={`/vets/${d.id}`}>
              <Card className="hover:shadow-md transition-shadow h-full">
                <CardContent className="pt-6 flex gap-3">
                  <Avatar className="h-14 w-14">
                    <AvatarImage src={d.avatarUrl ?? undefined} />
                    <AvatarFallback>{d.name?.[0] ?? "D"}</AvatarFallback>
                  </Avatar>
                  <div className="flex-1 min-w-0">
                    <p className="font-semibold truncate">Dr. {d.name}</p>
                    <p className="text-xs text-muted-foreground">{d.primarySpecialty ?? "General practice"}</p>
                    <p className="text-xs text-muted-foreground flex items-center gap-1 mt-1">
                      <MapPin className="h-3 w-3" /> {d.city ?? "—"}{d.country ? `, ${d.country}` : ""}
                    </p>
                    <div className="flex items-center gap-3 text-xs mt-2">
                      <span className="flex items-center gap-1 text-amber-500">
                        <Star className="h-3 w-3 fill-current" /> {d.ratingAverage.toFixed(1)}
                        <span className="text-muted-foreground">({d.ratingCount})</span>
                      </span>
                      <Badge variant="muted">{d.consultationType}</Badge>
                      <span className="ml-auto font-semibold text-foreground">${d.consultationFee}</span>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
