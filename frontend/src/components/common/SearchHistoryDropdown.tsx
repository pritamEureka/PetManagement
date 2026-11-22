import { useId, useState } from "react";
import { History, Search, X } from "lucide-react";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { useSearchHistory } from "@/hooks/useSearchHistory";

interface Props {
  value: string;
  onChange: (v: string) => void;
  /** History bucket, e.g. "global" or "store". */
  scope?: string;
  /** Called on Enter or when a history entry is chosen. */
  onSubmit?: (v: string) => void;
  placeholder?: string;
  "aria-label"?: string;
  className?: string;
  inputClassName?: string;
  type?: string;
}

/**
 * Search input with a recent-searches listbox (combobox pattern:
 * role=combobox, aria-activedescendant, arrow keys, Esc to close).
 * Terms are stored (user-scoped, capped at 10) when the user presses Enter.
 */
export function SearchHistoryDropdown({ value, onChange, scope = "global", onSubmit, placeholder, className, inputClassName, type = "search", ...rest }: Props) {
  const { t } = useI18n();
  const { history, add, remove, clear, refresh } = useSearchHistory(scope);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const listId = useId();
  const q = value.trim().toLowerCase();
  const shown = history.filter((h) => !q || (h.toLowerCase().includes(q) && h.toLowerCase() !== q));
  const visible = open && shown.length > 0;

  const choose = (term: string) => {
    onChange(term);
    add(term);
    onSubmit?.(term);
    setOpen(false);
    setActive(-1);
  };

  return (
    <div className={cn("relative", className)}>
      <Search aria-hidden="true" className="pointer-events-none absolute start-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
      <Input
        type={type}
        role="combobox"
        aria-expanded={visible}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={visible && active >= 0 ? `${listId}-${active}` : undefined}
        aria-label={rest["aria-label"]}
        placeholder={placeholder}
        className={cn("ps-8 h-9 text-sm", inputClassName)}
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
          setActive(-1);
        }}
        onFocus={() => {
          refresh();
          setOpen(true);
        }}
        onBlur={() => setTimeout(() => setOpen(false), 120)}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown" && shown.length) {
            e.preventDefault();
            setOpen(true);
            setActive((a) => (a + 1) % shown.length);
          } else if (e.key === "ArrowUp" && shown.length) {
            e.preventDefault();
            setActive((a) => (a <= 0 ? shown.length - 1 : a - 1));
          } else if (e.key === "Enter") {
            if (visible && active >= 0) {
              e.preventDefault();
              choose(shown[active]);
            } else if (value.trim()) {
              add(value);
              onSubmit?.(value.trim());
            }
          } else if (e.key === "Escape" && open) {
            setOpen(false);
          }
        }}
      />
      {visible && (
        <ul
          id={listId}
          role="listbox"
          aria-label={t("search.recent")}
          className="absolute inset-x-0 top-full z-50 mt-1 max-h-64 overflow-auto rounded-md border bg-popover p-1 text-popover-foreground shadow-md"
        >
          {shown.map((term, i) => (
            <li
              key={term}
              id={`${listId}-${i}`}
              role="option"
              aria-selected={i === active}
              className={cn("flex min-h-9 cursor-pointer items-center gap-2 rounded px-2 text-sm", i === active ? "bg-accent" : "hover:bg-accent/60")}
              onMouseDown={(e) => {
                e.preventDefault();
                choose(term);
              }}
            >
              <History aria-hidden="true" className="h-3.5 w-3.5 text-muted-foreground" />
              <span className="flex-1 truncate">{term}</span>
              <button
                type="button"
                tabIndex={-1}
                aria-label={t("search.removeEntry", { term })}
                className="grid h-6 w-6 place-items-center rounded hover:bg-muted"
                onMouseDown={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  remove(term);
                }}
              >
                <X aria-hidden="true" className="h-3 w-3" />
              </button>
            </li>
          ))}
          <li role="presentation" className="border-t mt-1 pt-1">
            <button
              type="button"
              tabIndex={-1}
              className="w-full rounded px-2 py-1.5 text-start text-xs text-muted-foreground hover:bg-accent/60"
              onMouseDown={(e) => {
                e.preventDefault();
                clear();
              }}
            >
              {t("search.clearHistory")}
            </button>
          </li>
        </ul>
      )}
    </div>
  );
}
