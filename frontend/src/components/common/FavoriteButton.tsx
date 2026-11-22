import { Star } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { useFavorite } from "@/hooks/useFavorite";

interface Props {
  /** Namespace, e.g. "pet", "vet", "store". */
  kind: string;
  id: string;
  title?: string;
  to?: string;
  className?: string;
}

/** Local (per-user, per-device) favorite toggle. aria-pressed + 44px target. */
export function FavoriteButton({ kind, id, title, to, className }: Props) {
  const { t } = useI18n();
  const { favorite, toggle } = useFavorite(kind, id, { title, to });
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      aria-pressed={favorite}
      aria-label={favorite ? t("fav.remove") : t("fav.add")}
      title={favorite ? t("fav.remove") : t("fav.add")}
      className={cn("min-h-11 min-w-11", className)}
      onClick={toggle}
    >
      <Star aria-hidden="true" className={cn("h-4 w-4", favorite && "fill-amber-500 text-amber-500")} />
    </Button>
  );
}
