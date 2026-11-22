import { Link } from "react-router-dom";
import { Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { useI18n } from "@/lib/i18n";
import { useRecentItems } from "@/hooks/useRecentItems";

/**
 * Header dropdown listing recently viewed entities. Radix DropdownMenu provides
 * roving focus (arrow keys / typeahead / Esc). Pages feed it via
 * `useRecentItems().track({ id, kind, title, to })`.
 */
export function RecentItemsMenu() {
  const { t } = useI18n();
  const { items, clear } = useRecentItems();
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" aria-label={t("recents.title")} title={t("recents.title")} className="min-h-10 min-w-10">
          <Clock aria-hidden="true" className="h-5 w-5" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-72">
        <DropdownMenuLabel>{t("recents.title")}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {items.length === 0 ? (
          <p className="px-2 py-3 text-sm text-muted-foreground">{t("recents.empty")}</p>
        ) : (
          items.map((i) => (
            <DropdownMenuItem key={i.id} asChild className="min-h-10">
              <Link to={i.to}>
                <span className="min-w-0">
                  <span className="block truncate text-sm">{i.title}</span>
                  {i.subtitle && <span className="block truncate text-xs text-muted-foreground">{i.subtitle}</span>}
                </span>
              </Link>
            </DropdownMenuItem>
          ))
        )}
        {items.length > 0 && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={clear} className="text-xs text-muted-foreground">
              {t("recents.clear")}
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
