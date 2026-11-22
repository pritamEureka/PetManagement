import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown, ChevronUp, PawPrint } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { petSocialApi } from "@/api/socialPlus";
import { useCareT } from "@/lib/careT";
import { safeRead, safeWrite } from "@/lib/safeStorage";

const KEY = "feed.followedPets.open";

/** Collapsible strip with recent photos from pets the user follows. Hidden entirely when there is nothing to show. */
export function FollowedPetsStrip() {
  const ct = useCareT();
  const [open, setOpen] = useState(() => safeRead<boolean>(KEY, true));
  const { data = [] } = useQuery({ queryKey: ["pet-social", "followed-photos"], queryFn: () => petSocialApi.followedPhotos(12), staleTime: 60_000 });
  if (data.length === 0) return null;

  function toggle() {
    const next = !open;
    setOpen(next);
    safeWrite(KEY, next);
  }

  return (
    <Card>
      <CardContent className="py-3 space-y-2">
        <div className="flex items-center justify-between">
          <p className="text-sm font-medium flex items-center gap-1.5"><PawPrint className="h-4 w-4" /> {ct("feed.followedPets", "From pets you follow")}</p>
          <Button size="sm" variant="ghost" onClick={toggle} aria-expanded={open} aria-label={open ? ct("feed.collapse", "Collapse") : ct("feed.expand", "Expand")}>
            {open ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
          </Button>
        </div>
        {open && (
          <ul className="flex gap-2 overflow-x-auto pb-1" aria-label={ct("feed.followedPets", "From pets you follow")}>
            {data.map((p) => (
              <li key={p.photo.id} className="shrink-0 w-24">
                <div className="block" title={`${p.petName} · ${p.albumTitle}`}>
                  <img src={p.photo.url} alt={p.photo.caption ?? `${p.petName} - ${p.albumTitle}`} loading="lazy"
                    className="h-24 w-24 rounded-md object-cover bg-muted" />
                  <span className="block text-xs mt-1 truncate">{p.petName}</span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
