import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Pencil, Trash2, PawPrint, Wallet, Tractor } from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Can } from "@/components/auth/Can";
import { petsApi, canEditPet, type Pet, type PetAccessLevel } from "@/api/pets";
import { toast } from "@/components/ui/sonner";
import { PetFormDialog } from "./PetFormDialog";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";

export function PetsPage() {
  const qc = useQueryClient();
  const { data: pets, isLoading } = useQuery({ queryKey: ["pets", "mine"], queryFn: petsApi.mine });
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Pet | undefined>();
  const [deleting, setDeleting] = useState<Pet | null>(null);

  function openCreate() { setEditing(undefined); setOpen(true); }
  function openEdit(p: Pet) { setEditing(p); setOpen(true); }

  function onDelete(p: Pet) { setDeleting(p); }

  async function confirmDelete() {
    if (!deleting) return;
    try {
      await petsApi.remove(deleting.id);
      toast.success(`${deleting.name} removed.`);
      qc.invalidateQueries({ queryKey: ["pets", "mine"] });
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Delete failed.");
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2"><PawPrint className="h-6 w-6 text-primary" /> My pets</h1>
          <p className="text-sm text-muted-foreground">Health records, vaccinations, photos — one card per pet.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" asChild><Link to="/pets/expenses"><Wallet className="h-4 w-4 mr-2" /> Expenses</Link></Button>
          <Button variant="outline" asChild><Link to="/livestock"><Tractor className="h-4 w-4 mr-2" /> Herds &amp; breeding</Link></Button>
          <Can permission="pets.create">
            <Button onClick={openCreate}><Plus className="h-4 w-4 mr-2" /> Add pet</Button>
          </Can>
        </div>
      </div>

      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {[...Array(3)].map((_, i) => (
            <Card key={i}><CardContent className="pt-6 space-y-2"><Skeleton className="h-40 w-full" /><Skeleton className="h-4 w-1/2" /></CardContent></Card>
          ))}
        </div>
      ) : !pets || pets.length === 0 ? (
        <EmptyState onAdd={openCreate} />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {pets.map((p) => (
            <Card key={p.id} className="overflow-hidden">
              <Link to={`/pets/${p.id}`} className="block aspect-video bg-muted relative">
                {p.primaryPhotoUrl
                  ? <img src={p.primaryPhotoUrl} alt={p.name} className="object-cover w-full h-full" />
                  : <div className="flex items-center justify-center h-full text-muted-foreground"><PawPrint className="h-10 w-10" /></div>}
                <div className="absolute top-2 right-2 flex flex-col items-end gap-1">
                  {p.isLost && <Badge variant="destructive">Lost</Badge>}
                  {p.isAvailableForAdoption && <Badge>Adoption</Badge>}
                  {p.overdueVaccinations > 0 && <Badge variant="warning">{p.overdueVaccinations} vaccine overdue</Badge>}
                </div>
              </Link>
              <CardContent className="pt-4 space-y-3">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-semibold text-lg">{p.name}</p>
                    <p className="text-xs text-muted-foreground">
                      {p.animalType}{p.breed ? ` · ${p.breed}` : ""}{p.gender !== "Unknown" ? ` · ${p.gender}` : ""}
                      {p.groupName ? ` · ${p.groupName}` : ""}
                    </p>
                    {p.nextVaccinationDue && (
                      <p className="text-xs text-muted-foreground mt-1">
                        Next vaccine {new Date(p.nextVaccinationDue).toLocaleDateString()}
                      </p>
                    )}
                  </div>
                  {p.myAccess !== "Owner" && <Badge variant="info">Shared · {accessLabel(p.myAccess)}</Badge>}
                </div>
                <div className="flex gap-2">
                  {canEditPet(p.myAccess) ? (
                    <Button size="sm" variant="outline" className="flex-1" onClick={() => openEdit(p)}>
                      <Pencil className="h-3 w-3 mr-1" /> Edit
                    </Button>
                  ) : (
                    <Button size="sm" variant="outline" className="flex-1" asChild>
                      <Link to={`/pets/${p.id}`}>Open</Link>
                    </Button>
                  )}
                  {p.myAccess === "Owner" && (
                    <Button size="sm" variant="destructive" onClick={() => onDelete(p)} aria-label={`Remove ${p.name}`}>
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <PetFormDialog
        open={open}
        onOpenChange={setOpen}
        editing={editing}
        onSaved={() => qc.invalidateQueries({ queryKey: ["pets", "mine"] })}
      />

      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={`Remove ${deleting?.name ?? "pet"}?`}
        description="This removes the pet from your account. Health records linked to this pet are removed too."
        confirmLabel="Remove"
        destructive
        onConfirm={confirmDelete}
      />
    </div>
  );
}

export function accessLabel(a: PetAccessLevel) {
  return a === "CoOwner" ? "co-owner" : a === "Vet" ? "treating vet" : a.toLowerCase();
}

function EmptyState({ onAdd }: { onAdd: () => void }) {
  return (
    <Card>
      <CardContent className="py-16 text-center space-y-3">
        <PawPrint className="h-12 w-12 text-muted-foreground mx-auto" />
        <p className="font-medium">No pets yet</p>
        <p className="text-sm text-muted-foreground">Add your first pet to start tracking health records.</p>
        <Button onClick={onAdd}><Plus className="h-4 w-4 mr-2" /> Add your first pet</Button>
      </CardContent>
    </Card>
  );
}
