import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Lightbulb, Star, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { toast } from "@/components/ui/sonner";
import { petsApi, canEditPet, type LineageNode, type PetDetail } from "@/api/pets";
import { Section, Muted, fmtDate, fmtMoney } from "./shared";

export function OverviewTab({ pet }: { pet: PetDetail }) {
  const qc = useQueryClient();
  const editor = canEditPet(pet.myAccess);
  const { data: rec } = useQuery({ queryKey: ["pets", pet.id, "recommendations"], queryFn: () => petsApi.recommendations(pet.id) });
  const { data: lineage } = useQuery({ queryKey: ["pets", pet.id, "lineage"], queryFn: () => petsApi.lineage(pet.id, 3) });
  const refresh = () => { qc.invalidateQueries({ queryKey: ["pets", pet.id] }); qc.invalidateQueries({ queryKey: ["pets", "mine"] }); };

  const hasAncestors = !!(lineage?.root.sire || lineage?.root.dam);

  return (
    <div className="space-y-4">
      <Section title="Profile notes">
        <div className="grid sm:grid-cols-2 gap-4 text-sm">
          <div><p className="font-medium">Allergies</p><p className="text-muted-foreground">{pet.allergies || "None recorded"}</p></div>
          <div><p className="font-medium">Diet</p><p className="text-muted-foreground">{pet.dietNotes || "—"}</p></div>
          {pet.microchipNumber && <div><p className="font-medium">Microchip</p><p className="text-muted-foreground font-mono">{pet.microchipNumber}</p></div>}
          {pet.birthDate && <div><p className="font-medium">Born</p><p className="text-muted-foreground">{fmtDate(pet.birthDate)}</p></div>}
        </div>
      </Section>

      {rec && (
        <Section title={`Care tips · ${rec.lifeStage}`}>
          <ul className="space-y-1.5 text-sm">
            {rec.careTips.map((t, i) => (
              <li key={i} className="flex gap-2"><Lightbulb className="h-4 w-4 text-amber-500 shrink-0 mt-0.5" />{t}</li>
            ))}
          </ul>
          {rec.products.length > 0 && (
            <>
              <p className="font-medium text-sm mt-5 mb-2">Recommended for {pet.name}</p>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                {rec.products.map((p) => (
                  <Link key={p.id} to={`/store/products/${p.id}`} className="rounded-lg border hover:bg-muted/50 transition-colors overflow-hidden">
                    <div className="aspect-square bg-muted">{p.imageUrl && <img src={p.imageUrl} alt="" className="h-full w-full object-cover" />}</div>
                    <div className="p-2">
                      <p className="text-xs font-medium line-clamp-2">{p.name}</p>
                      <p className="text-xs mt-1">{fmtMoney(p.discountPrice ?? p.price)}</p>
                      <p className="text-[11px] text-muted-foreground">{p.reason}</p>
                    </div>
                  </Link>
                ))}
              </div>
            </>
          )}
        </Section>
      )}

      <Section
        title="Photos"
        action={editor && (
          <FileUploadButton label="Add photo" onUploaded={async (url) => {
            await petsApi.photos.add(pet.id, { url });
            toast.success("Photo added.");
            refresh();
          }} />
        )}
      >
        {pet.photos.length === 0 ? <Muted>No photos yet.</Muted> : (
          <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
            {pet.photos.map((ph) => {
              const primary = ph.url === pet.primaryPhotoUrl;
              return (
                <div key={ph.id} className="relative group aspect-square rounded-md overflow-hidden bg-muted">
                  <img src={ph.url} alt={ph.caption ?? pet.name} className="h-full w-full object-cover" />
                  {primary && <Badge className="absolute top-1 left-1">Profile</Badge>}
                  {editor && (
                    <div className="absolute bottom-1 right-1 flex gap-1 opacity-0 group-hover:opacity-100 focus-within:opacity-100 transition-opacity">
                      {!primary && (
                        <Button size="icon" variant="secondary" className="h-7 w-7" aria-label="Use as profile photo"
                          onClick={async () => { await petsApi.photos.setPrimary(pet.id, ph.id); refresh(); }}>
                          <Star className="h-3.5 w-3.5" />
                        </Button>
                      )}
                      <Button size="icon" variant="secondary" className="h-7 w-7" aria-label="Delete photo"
                        onClick={async () => { await petsApi.photos.remove(pet.id, ph.id); refresh(); }}>
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </Section>

      {lineage && (hasAncestors || lineage.offspring.length > 0) && (
        <Section title="Family tree">
          {hasAncestors && <Tree node={lineage.root} />}
          {lineage.offspring.length > 0 && (
            <div className="mt-4">
              <p className="text-sm font-medium mb-2">Offspring</p>
              <div className="flex flex-wrap gap-2">
                {lineage.offspring.map((o) => (
                  <Link key={o.id} to={`/pets/${o.id}`} className="text-sm rounded-full border px-3 py-1 hover:bg-muted">{o.name}</Link>
                ))}
              </div>
            </div>
          )}
        </Section>
      )}
    </div>
  );
}

function Tree({ node, depth = 0 }: { node: LineageNode; depth?: number }) {
  return (
    <div className={depth ? "pl-4 border-l ml-2" : ""}>
      <p className="text-sm py-1">
        {depth > 0 && <span className="text-muted-foreground">{node.gender === "Female" ? "Dam · " : node.gender === "Male" ? "Sire · " : ""}</span>}
        <Link to={`/pets/${node.id}`} className="font-medium hover:underline">{node.name}</Link>
        {node.breed && <span className="text-muted-foreground"> · {node.breed}</span>}
      </p>
      {node.sire && <Tree node={node.sire} depth={depth + 1} />}
      {node.dam && <Tree node={node.dam} depth={depth + 1} />}
    </div>
  );
}
