import { useState } from "react";
import { Bookmark, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useI18n } from "@/lib/i18n";
import { useSavedFilters } from "@/hooks/useSavedFilters";

interface Props {
  /** Unique bucket for the list page, e.g. "admin-users". */
  scope: string;
  /** Current filters as URLSearchParams (typically from useSearchParams()). */
  params: URLSearchParams;
  /** Apply a saved filter: receives the deserialized params. */
  onApply: (params: URLSearchParams) => void;
}

/** Name + save the current query string; chips re-apply it. Stored per user in localStorage (max 20). */
export function SavedFiltersBar({ scope, params, onApply }: Props) {
  const { t } = useI18n();
  const { filters, save, remove } = useSavedFilters(scope);
  const [naming, setNaming] = useState(false);
  const [name, setName] = useState("");
  const canSave = params.toString().length > 0;

  function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    save(name, params);
    setName("");
    setNaming(false);
  }

  return (
    <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t("filters.saved")}>
      {filters.map((f) => (
        <span key={f.id} className="inline-flex items-center rounded-full border bg-card text-sm">
          <button
            type="button"
            className="min-h-9 rounded-s-full ps-3 pe-2 hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
            onClick={() => onApply(new URLSearchParams(f.query))}
          >
            {f.name}
          </button>
          <button
            type="button"
            aria-label={t("filters.delete", { name: f.name })}
            className="grid min-h-9 min-w-9 place-items-center rounded-e-full hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
            onClick={() => remove(f.id)}
          >
            <X aria-hidden="true" className="h-3 w-3" />
          </button>
        </span>
      ))}
      {naming ? (
        <form onSubmit={submit} className="flex items-center gap-2">
          <Input autoFocus aria-label={t("filters.name")} placeholder={t("filters.name")} value={name} maxLength={60} onChange={(e) => setName(e.target.value)} className="h-9 w-44" />
          <Button type="submit" size="sm" disabled={!name.trim()}>{t("common.save")}</Button>
          <Button type="button" size="sm" variant="ghost" onClick={() => setNaming(false)}>{t("common.cancel")}</Button>
        </form>
      ) : (
        <Button type="button" size="sm" variant="outline" className="min-h-9" disabled={!canSave} onClick={() => setNaming(true)}>
          <Bookmark aria-hidden="true" className="h-3.5 w-3.5 me-1" /> {t("filters.saveCurrent")}
        </Button>
      )}
    </div>
  );
}
