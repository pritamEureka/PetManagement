import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ImagePlus, Pencil, Plus, Star, Trash2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { petSocialApi, type AlbumVisibility, type PetAlbum } from "@/api/socialPlus";
import { useCareT } from "@/lib/careT";

const VIS: AlbumVisibility[] = ["Public", "Followers", "Private"];
const sel = "h-9 rounded-md border bg-background px-2 text-sm";

/** Owner tool: create/edit/delete a pet's albums, upload/remove photos, pick a cover and set who can see each album. */
export function PetAlbumManager({ petId, canManage }: { petId: string; canManage: boolean }) {
  const ct = useCareT();
  const qc = useQueryClient();
  const key = ["pet-social", petId];
  const { data, isLoading } = useQuery({ queryKey: key, queryFn: () => petSocialApi.profile(petId) });
  const [editing, setEditing] = useState<PetAlbum | "new" | null>(null);
  const [deleting, setDeleting] = useState<PetAlbum | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: key });
  const visLabel = (v: AlbumVisibility) => ct(`petAlbum.vis.${v}`, v === "Followers" ? "Followers only" : v);

  const addPhoto = useMutation({
    mutationFn: ({ albumId, url }: { albumId: string; url: string }) => petSocialApi.addPhoto(albumId, { url }),
    onSuccess: refresh,
  });
  const delPhoto = useMutation({
    mutationFn: ({ albumId, photoId }: { albumId: string; photoId: string }) => petSocialApi.deletePhoto(albumId, photoId),
    onSuccess: refresh,
  });
  const setCover = useMutation({
    mutationFn: ({ albumId, url }: { albumId: string; url: string }) => petSocialApi.updateAlbum(albumId, { coverUrl: url }),
    onSuccess: () => { toast.success(ct("petAlbum.coverSet", "Cover updated.")); refresh(); },
  });

  if (isLoading || !data) return <Skeleton className="h-40" />;
  const albums = data.albums;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <p className="text-sm text-muted-foreground">
          {ct("petAlbum.followers", "{count} followers", { count: data.followerCount })} · {ct("petAlbum.hint", "Choose who can see each album: everyone, only followers, or just you.")}
        </p>
        {canManage && <Button size="sm" onClick={() => setEditing("new")}><Plus className="h-3.5 w-3.5 mr-1" /> {ct("petAlbum.new", "New album")}</Button>}
      </div>

      {albums.length === 0 ? (
        <p className="text-sm text-muted-foreground py-6 text-center">{ct("petAlbum.none", "No albums yet.")}</p>
      ) : albums.map((a) => (
        <div key={a.id} className="rounded-lg border p-3 space-y-3">
          <div className="flex items-center justify-between gap-2 flex-wrap">
            <div className="min-w-0">
              <p className="font-medium truncate">{a.title} <Badge variant={a.visibility === "Public" ? "success" : a.visibility === "Followers" ? "info" : "muted"}>{visLabel(a.visibility)}</Badge></p>
              <p className="text-xs text-muted-foreground">{a.photoCount} {a.photoCount === 1 ? "photo" : "photos"}</p>
            </div>
            {canManage && (
              <div className="flex items-center gap-1">
                <FileUploadButton label={ct("petAlbum.addPhoto", "Add photo")} size="sm" variant="outline"
                  onUploaded={(url) => addPhoto.mutate({ albumId: a.id, url })} />
                <Button size="icon" variant="ghost" aria-label={ct("petAlbum.edit", "Edit album")} onClick={() => setEditing(a)}><Pencil className="h-4 w-4" /></Button>
                <Button size="icon" variant="ghost" aria-label={ct("petAlbum.delete", "Delete album")} onClick={() => setDeleting(a)}><Trash2 className="h-4 w-4" /></Button>
              </div>
            )}
          </div>
          {a.photos.length === 0 ? (
            <p className="text-xs text-muted-foreground flex items-center gap-1"><ImagePlus className="h-3.5 w-3.5" /> {ct("petAlbum.empty", "This album is empty.")}</p>
          ) : (
            <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
              {a.photos.map((p) => {
                const isCover = a.coverUrl === p.url;
                return (
                  <figure key={p.id} className="relative group">
                    <img src={p.url} alt={p.caption ?? ""} className="aspect-square w-full rounded object-cover bg-muted" loading="lazy" />
                    {isCover && <Badge className="absolute top-1 left-1 text-[9px]">{ct("petAlbum.cover", "Cover")}</Badge>}
                    {canManage && (
                      <div className="absolute inset-x-0 bottom-0 flex justify-end gap-1 p-1 bg-gradient-to-t from-black/50 to-transparent opacity-100 sm:opacity-0 sm:group-hover:opacity-100 sm:group-focus-within:opacity-100">
                        {!isCover && (
                          <Button size="icon" variant="secondary" className="h-6 w-6" aria-label={ct("petAlbum.makeCover", "Make cover")} title={ct("petAlbum.makeCover", "Make cover")}
                            onClick={() => setCover.mutate({ albumId: a.id, url: p.url })}><Star className="h-3 w-3" /></Button>
                        )}
                        <Button size="icon" variant="secondary" className="h-6 w-6" aria-label={ct("petAlbum.removePhoto", "Remove photo")} title={ct("petAlbum.removePhoto", "Remove photo")}
                          onClick={() => { if (window.confirm(ct("petAlbum.removeConfirm", "Remove this photo?"))) delPhoto.mutate({ albumId: a.id, photoId: p.id }); }}><Trash2 className="h-3 w-3" /></Button>
                      </div>
                    )}
                  </figure>
                );
              })}
            </div>
          )}
        </div>
      ))}

      {editing && (
        <AlbumDialog petId={petId} album={editing === "new" ? undefined : editing} onClose={() => setEditing(null)} onSaved={refresh} visLabel={visLabel} />
      )}
      <ConfirmDialog
        open={!!deleting}
        onOpenChange={(v) => !v && setDeleting(null)}
        title={ct("petAlbum.deleteTitle", "Delete this album?")}
        description={ct("petAlbum.deleteBody", "The album and its photos are removed from the pet's profile.")}
        confirmLabel={ct("petAlbum.delete", "Delete album")}
        destructive
        onConfirm={async () => { if (deleting) { await petSocialApi.deleteAlbum(deleting.id); refresh(); } }}
      />
    </div>
  );
}

function AlbumDialog({ petId, album, onClose, onSaved, visLabel }: {
  petId: string; album?: PetAlbum; onClose: () => void; onSaved: () => void; visLabel: (v: AlbumVisibility) => string;
}) {
  const ct = useCareT();
  const [title, setTitle] = useState(album?.title ?? "");
  const [visibility, setVisibility] = useState<AlbumVisibility>(album?.visibility ?? "Public");
  const [busy, setBusy] = useState(false);

  async function save() {
    if (!title.trim()) return toast.error(ct("petAlbum.titleRequired", "Give the album a title."));
    setBusy(true);
    try {
      if (album) await petSocialApi.updateAlbum(album.id, { title: title.trim(), visibility });
      else await petSocialApi.createAlbum(petId, { title: title.trim(), visibility });
      onSaved();
      onClose();
    } catch { /* toasted */ } finally { setBusy(false); }
  }

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>{album ? ct("petAlbum.edit", "Edit album") : ct("petAlbum.new", "New album")}</DialogTitle></DialogHeader>
        <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); save(); }}>
          <div className="space-y-1">
            <label htmlFor="album-title" className="text-sm font-medium">{ct("petAlbum.title", "Title")}</label>
            <Input id="album-title" value={title} maxLength={80} onChange={(e) => setTitle(e.target.value)} autoFocus />
          </div>
          <div className="space-y-1">
            <label htmlFor="album-vis" className="text-sm font-medium">{ct("petAlbum.visibility", "Who can see it")}</label>
            <select id="album-vis" className={sel + " w-full"} value={visibility} onChange={(e) => setVisibility(e.target.value as AlbumVisibility)}>
              {VIS.map((v) => <option key={v} value={v}>{visLabel(v)}</option>)}
            </select>
          </div>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>{ct("common.cancel", "Cancel")}</Button>
            <Button type="submit" disabled={busy}>{ct("common.save", "Save")}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
