import { History } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { formatRelativeTime } from "@/lib/format";
import type { DraftHandle } from "@/hooks/useDraft";

interface Props<T> {
  draft: DraftHandle;
  /** Called with the saved value when the user chooses Restore. */
  onRestore: (data: T) => void;
  className?: string;
}

/** Restore prompt shown above a form when an unsaved draft exists. */
export function DraftBanner<T = any>({ draft, onRestore, className }: Props<T>) {
  const { t } = useI18n();
  if (!draft.hasDraft) return null;
  return (
    <div role="status" className={`flex flex-wrap items-center gap-3 rounded-md border border-primary/40 bg-card px-3 py-2 text-sm ${className ?? ""}`}>
      <History aria-hidden="true" className="h-4 w-4 shrink-0" />
      <span className="flex-1 min-w-[12rem]">
        {t("draft.found", { when: draft.savedAt ? formatRelativeTime(draft.savedAt) : "" })}
      </span>
      <Button
        type="button"
        size="sm"
        onClick={() => {
          const d = draft.restore();
          if (d !== null) onRestore(d as T);
        }}
      >
        {t("draft.restore")}
      </Button>
      <Button type="button" size="sm" variant="ghost" onClick={draft.discard}>
        {t("draft.discard")}
      </Button>
    </div>
  );
}
