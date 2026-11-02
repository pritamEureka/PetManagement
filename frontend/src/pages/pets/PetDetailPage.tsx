import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams, Link, useSearchParams } from "react-router-dom";
import { ArrowLeft, PawPrint, Pencil } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { petsApi, canEditPet } from "@/api/pets";
import { PetFormDialog } from "./PetFormDialog";
import { accessLabel } from "./PetsPage";
import { OverviewTab } from "./detail/OverviewTab";
import { HealthTab } from "./detail/HealthTab";
import { MedicationsTab } from "./detail/MedicationsTab";
import { WeightTab } from "./detail/WeightTab";
import { CareTab } from "./detail/CareTab";
import { FamilyTab } from "./detail/FamilyTab";
import { MoneyTab } from "./detail/MoneyTab";
import { ShareTab } from "./detail/ShareTab";

function ageLabel(birthDate?: string | null) {
  if (!birthDate) return null;
  const months = Math.floor((Date.now() - new Date(birthDate).getTime()) / (30.44 * 864e5));
  if (months < 1) return "under a month";
  if (months < 24) return `${months} mo`;
  return `${Math.floor(months / 12)} yrs`;
}

export function PetDetailPage() {
  const { id = "" } = useParams();
  const qc = useQueryClient();
  const [params, setParams] = useSearchParams();
  const [editOpen, setEditOpen] = useState(false);
  const { data: pet, isLoading, isError } = useQuery({ queryKey: ["pets", id], queryFn: () => petsApi.get(id), enabled: !!id, retry: false });

  if (isLoading) return <div className="space-y-3 max-w-4xl mx-auto"><Skeleton className="h-56" /><Skeleton className="h-32" /></div>;
  if (isError || !pet) {
    return (
      <div className="max-w-4xl mx-auto space-y-3">
        <Button variant="ghost" size="sm" asChild><Link to="/pets"><ArrowLeft className="h-4 w-4 mr-1" /> All pets</Link></Button>
        <p className="text-muted-foreground">Pet not found, or you don't have access to it.</p>
      </div>
    );
  }

  const editor = canEditPet(pet.myAccess);
  const tabs = [
    { key: "overview", label: "Overview" },
    { key: "health", label: "Health" },
    { key: "medications", label: "Medications" },
    { key: "weight", label: "Weight" },
    ...(pet.myAccess !== "Vet" ? [{ key: "care", label: "Daily care" }, { key: "family", label: "Care team" }] : []),
    ...(editor ? [{ key: "money", label: "Expenses" }] : []),
    { key: "share", label: "Share & ID" },
  ];
  const active = tabs.some((t) => t.key === params.get("tab")) ? params.get("tab")! : "overview";
  const age = ageLabel(pet.birthDate);

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
          <div className="flex items-start justify-between gap-4 flex-wrap">
            <div>
              <h1 className="text-2xl font-bold">{pet.name}</h1>
              <p className="text-sm text-muted-foreground">
                {pet.animalType}{pet.breed ? ` · ${pet.breed}` : ""}{pet.gender !== "Unknown" ? ` · ${pet.gender}` : ""}
                {age ? ` · ${age}` : ""}{pet.weightKg ? ` · ${pet.weightKg} kg` : ""}{pet.groupName ? ` · ${pet.groupName}` : ""}
              </p>
              {pet.myAccess !== "Owner" && <p className="text-xs text-muted-foreground mt-1">Owned by {pet.ownerName}</p>}
            </div>
            <div className="flex items-center gap-2 flex-wrap">
              {pet.isLost && <Badge variant="destructive">Lost</Badge>}
              {pet.isAvailableForAdoption && <Badge>Available for adoption</Badge>}
              {pet.myAccess !== "Owner" && <Badge variant="info">You: {accessLabel(pet.myAccess)}</Badge>}
              {editor && <Button size="sm" variant="outline" onClick={() => setEditOpen(true)}><Pencil className="h-3.5 w-3.5 mr-1" /> Edit</Button>}
            </div>
          </div>
          {pet.tagNumber && <p className="text-xs">Tag #{pet.tagNumber}</p>}
        </CardContent>
      </Card>

      <Tabs value={active} onValueChange={(v) => setParams({ tab: v }, { replace: true })}>
        <div className="overflow-x-auto -mx-1 px-1">
          <TabsList className="w-max">
            {tabs.map((t) => <TabsTrigger key={t.key} value={t.key}>{t.label}</TabsTrigger>)}
          </TabsList>
        </div>
        <TabsContent value="overview"><OverviewTab pet={pet} /></TabsContent>
        <TabsContent value="health"><HealthTab pet={pet} /></TabsContent>
        <TabsContent value="medications"><MedicationsTab pet={pet} /></TabsContent>
        <TabsContent value="weight"><WeightTab pet={pet} /></TabsContent>
        <TabsContent value="care"><CareTab pet={pet} /></TabsContent>
        <TabsContent value="family"><FamilyTab pet={pet} /></TabsContent>
        <TabsContent value="money"><MoneyTab pet={pet} /></TabsContent>
        <TabsContent value="share"><ShareTab pet={pet} /></TabsContent>
      </Tabs>

      <PetFormDialog
        open={editOpen}
        onOpenChange={setEditOpen}
        editing={pet}
        onSaved={() => { qc.invalidateQueries({ queryKey: ["pets", id] }); qc.invalidateQueries({ queryKey: ["pets", "mine"] }); }}
      />
    </div>
  );
}
