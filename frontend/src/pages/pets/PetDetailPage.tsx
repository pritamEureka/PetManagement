import { useQuery } from "@tanstack/react-query";
import { useParams, Link } from "react-router-dom";
import { ArrowLeft, PawPrint, Syringe, Stethoscope, Sparkles } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { petsApi } from "@/api/pets";
import { api } from "@/api/client";

export function PetDetailPage() {
  const { id = "" } = useParams();
  const { data: pet, isLoading } = useQuery({
    queryKey: ["pets", id],
    queryFn: () => api.get(`/pets/${id}`).then((r) => r.data.data ?? r.data),
    enabled: !!id
  });

  if (isLoading) return <div className="space-y-3 max-w-3xl mx-auto"><Skeleton className="h-56" /><Skeleton className="h-32" /></div>;
  if (!pet) return <p className="text-muted-foreground">Pet not found.</p>;

  const vacs = pet.vaccinations ?? [];
  const med = pet.medicalRecords ?? [];
  const groom = pet.groomingRecords ?? [];

  return (
    <div className="space-y-4 max-w-4xl mx-auto">
      <Button variant="ghost" size="sm" asChild><Link to="/pets"><ArrowLeft className="h-4 w-4 mr-1" /> All pets</Link></Button>

      <Card className="overflow-hidden">
        <div className="aspect-[3/1] bg-muted relative">
          {pet.primaryPhotoUrl
            ? <img src={pet.primaryPhotoUrl} alt={pet.name} className="object-cover w-full h-full" />
            : <div className="flex items-center justify-center h-full text-muted-foreground"><PawPrint className="h-16 w-16" /></div>}
        </div>
        <CardContent className="pt-6 space-y-2">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 className="text-2xl font-bold">{pet.name}</h1>
              <p className="text-sm text-muted-foreground">
                {pet.animalType}{pet.breed ? ` · ${pet.breed}` : ""}{pet.gender !== "Unknown" ? ` · ${pet.gender}` : ""}
                {pet.weightKg ? ` · ${pet.weightKg} kg` : ""}
              </p>
            </div>
            {pet.isAvailableForAdoption && <Badge>Available for adoption</Badge>}
          </div>
          {pet.tagNumber && <p className="text-xs">Tag #{pet.tagNumber}</p>}
        </CardContent>
      </Card>

      <Tabs defaultValue="vaccinations">
        <TabsList>
          <TabsTrigger value="vaccinations"><Syringe className="h-3.5 w-3.5 mr-1" /> Vaccinations</TabsTrigger>
          <TabsTrigger value="medical"><Stethoscope className="h-3.5 w-3.5 mr-1" /> Medical</TabsTrigger>
          <TabsTrigger value="grooming"><Sparkles className="h-3.5 w-3.5 mr-1" /> Grooming</TabsTrigger>
          <TabsTrigger value="notes">Notes</TabsTrigger>
        </TabsList>

        <TabsContent value="vaccinations">
          {vacs.length === 0
            ? <Empty title="No vaccinations recorded" />
            : <RecordList rows={vacs.map((v: any) => ({ id: v.id, title: v.vaccineName, date: v.administeredOn, note: v.notes }))} />}
        </TabsContent>
        <TabsContent value="medical">
          {med.length === 0
            ? <Empty title="No medical records" />
            : <RecordList rows={med.map((m: any) => ({ id: m.id, title: m.diagnosis ?? "Visit", date: m.visitDate, note: m.treatment ?? m.notes }))} />}
        </TabsContent>
        <TabsContent value="grooming">
          {groom.length === 0
            ? <Empty title="No grooming visits" />
            : <RecordList rows={groom.map((g: any) => ({ id: g.id, title: g.serviceType, date: g.performedOn, note: g.notes }))} />}
        </TabsContent>
        <TabsContent value="notes">
          <Card><CardContent className="pt-6 space-y-3 text-sm">
            <div><p className="font-medium">Allergies</p><p className="text-muted-foreground">{pet.allergies || "—"}</p></div>
            <div><p className="font-medium">Diet</p><p className="text-muted-foreground">{pet.dietNotes || "—"}</p></div>
          </CardContent></Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}

function Empty({ title }: { title: string }) {
  return <Card><CardContent className="py-10 text-center text-muted-foreground text-sm">{title}</CardContent></Card>;
}

function RecordList({ rows }: { rows: { id: string; title: string; date: string; note?: string | null }[] }) {
  return (
    <div className="space-y-2">
      {rows.map((r) => (
        <Card key={r.id}>
          <CardContent className="py-3 flex items-start justify-between gap-3">
            <div>
              <p className="font-medium">{r.title}</p>
              {r.note && <p className="text-xs text-muted-foreground mt-0.5">{r.note}</p>}
            </div>
            <Badge variant="muted">{new Date(r.date).toLocaleDateString()}</Badge>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
