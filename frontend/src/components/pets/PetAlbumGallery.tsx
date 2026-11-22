import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Heart } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { toast } from "@/components/ui/sonner";
import { useI18n } from "@/lib/i18n";
import { useAuthStore } from "@/store/authStore";
import { petSocialApi, type PetSocialProfile } from "@/api/socialPlus";

/** Follow button, follower count and album gallery. Visibility is filtered server-side. */
export function PetAlbumGallery({ social, queryKey }: { social: PetSocialProfile; queryKey: unknown[] }) {
  const { t } = useI18n();
  const qc = useQueryClient();
  const signedIn = useAuthStore((s) => !!s.accessToken);
  const [open, setOpen] = useState<string | null>(null);
  const toggle = useMutation({
    mutationFn: () => (social.isFollowing ? petSocialApi.unfollow(social.petId) : petSocialApi.follow(social.petId)),
    onSuccess: () => { qc.invalidateQueries({ queryKey }); },
    onError: () => toast.error(t("petAlbum.followFailed")),
  });
  const album = social.albums.find((a) => a.id === open);

  return (
    <Card>
      <CardContent className="pt-5 space-y-3">
        <div className="flex items-center justify-between gap-2">
          <p className="text-sm text-muted-foreground">{t("petAlbum.followers", { count: social.followerCount })}</p>
          {signedIn && !social.isOwner && (
            <Button size="sm" variant={social.isFollowing ? "outline" : "default"} disabled={toggle.isPending} onClick={() => toggle.mutate()}>
              <Heart className="h-3.5 w-3.5 me-1" />{social.isFollowing ? t("petAlbum.unfollow") : t("petAlbum.follow")}
            </Button>
          )}
        </div>
        {social.albums.length > 0 && (
          <div className="grid grid-cols-3 gap-2">
            {social.albums.map((a) => (
              <button key={a.id} type="button" className="text-start" onClick={() => setOpen(a.id === open ? null : a.id)}>
                <div className="aspect-square rounded bg-muted overflow-hidden">
                  {a.coverUrl && <img src={a.coverUrl} alt="" className="h-full w-full object-cover" />}
                </div>
                <p className="text-xs mt-1 truncate">{a.title} ({a.photoCount})</p>
              </button>
            ))}
          </div>
        )}
        {album && (
          <div className="grid grid-cols-2 gap-2">
            {album.photos.map((p) => (
              <figure key={p.id}>
                <img src={p.url} alt={p.caption ?? ""} className="rounded w-full object-cover" />
                {p.caption && <figcaption className="text-xs text-muted-foreground">{p.caption}</figcaption>}
              </figure>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
