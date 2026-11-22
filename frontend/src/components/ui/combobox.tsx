import * as React from "react";
import { Check, ChevronsUpDown, Loader2, RefreshCw, X } from "lucide-react";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";

export interface ComboboxProps<T> {
  value: T | null;
  onChange: (value: T | null) => void;
  items: T[];
  getKey: (item: T) => string;
  getLabel: (item: T) => string;
  /** Custom row content; default renders the label. */
  renderItem?: (item: T) => React.ReactNode;
  /** Controlled search text (remote search). Omit for local filtering by label. */
  search?: string;
  onSearchChange?: (q: string) => void;
  loading?: boolean;
  error?: boolean;
  onRetry?: () => void;
  placeholder?: string;
  searchPlaceholder?: string;
  emptyText?: string;
  /** Shown instead of the empty message while the query is shorter than this. */
  minChars?: number;
  clearable?: boolean;
  disabled?: boolean;
  id?: string;
  "aria-label"?: string;
  "aria-invalid"?: boolean;
  "aria-describedby"?: string;
  className?: string;
}

/**
 * Accessible searchable select. The trigger is a button (aria-haspopup=listbox);
 * the popover contains a cmdk input (role=combobox, aria-activedescendant,
 * arrow-key navigation) and a listbox. Supports remote search with
 * loading / empty / error (+retry) states.
 */
export function Combobox<T>(props: ComboboxProps<T>) {
  const {
    value, onChange, items, getKey, getLabel, renderItem, search, onSearchChange, loading, error, onRetry,
    placeholder, searchPlaceholder, emptyText, minChars = 0, clearable = true, disabled, id, className,
  } = props;
  const { t } = useI18n();
  const [open, setOpen] = React.useState(false);
  const remote = onSearchChange !== undefined;
  const q = search ?? "";
  const tooShort = remote && q.trim().length < minChars;

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <div className={cn("relative", className)}>
        <PopoverTrigger asChild>
          <button
            type="button"
            id={id}
            role="combobox"
            aria-expanded={open}
            aria-haspopup="listbox"
            aria-label={props["aria-label"]}
            aria-invalid={props["aria-invalid"]}
            aria-describedby={props["aria-describedby"]}
            disabled={disabled}
            className={cn(
              "flex min-h-10 w-full items-center justify-between gap-2 rounded-md border border-input bg-background px-3 py-2 text-start text-sm",
              "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50",
              clearable && value && "pe-9",
              props["aria-invalid"] && "border-destructive",
            )}
          >
            <span className={cn("truncate", !value && "text-muted-foreground")}>
              {value ? getLabel(value) : placeholder ?? t("picker.select")}
            </span>
            <ChevronsUpDown aria-hidden="true" className="h-4 w-4 shrink-0 opacity-60" />
          </button>
        </PopoverTrigger>
        {clearable && value && !disabled && (
          <button
            type="button"
            aria-label={t("picker.clear")}
            className="absolute end-8 top-1/2 -translate-y-1/2 rounded p-1.5 text-muted-foreground hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
            onClick={() => onChange(null)}
          >
            <X aria-hidden="true" className="h-3.5 w-3.5" />
          </button>
        )}
      </div>
      <PopoverContent align="start" className="w-[var(--radix-popover-trigger-width)] min-w-[16rem]">
        <Command shouldFilter={!remote}>
          <CommandInput
            value={remote ? q : undefined}
            onValueChange={remote ? onSearchChange : undefined}
            placeholder={searchPlaceholder ?? t("picker.search")}
          />
          <CommandList aria-busy={loading || undefined}>
            {loading && (
              <div role="status" className="flex items-center gap-2 px-3 py-3 text-sm text-muted-foreground">
                <Loader2 aria-hidden="true" className="h-4 w-4 animate-spin" /> {t("common.loading")}
              </div>
            )}
            {!loading && error && (
              <div role="alert" className="flex items-center justify-between gap-2 px-3 py-3 text-sm">
                <span className="text-destructive">{t("picker.error")}</span>
                {onRetry && (
                  <button type="button" onClick={onRetry} className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs underline focus-visible:ring-2 focus-visible:ring-ring">
                    <RefreshCw aria-hidden="true" className="h-3 w-3" /> {t("common.tryAgain")}
                  </button>
                )}
              </div>
            )}
            {!loading && !error && tooShort && (
              <div className="px-3 py-3 text-sm text-muted-foreground">{t("picker.minChars", { count: minChars })}</div>
            )}
            {!loading && !error && !tooShort && (
              <CommandEmpty>{emptyText ?? t("picker.empty")}</CommandEmpty>
            )}
            {!error && !tooShort && (
              <CommandGroup>
                {items.map((item) => {
                  const key = getKey(item);
                  const selected = value ? getKey(value) === key : false;
                  return (
                    <CommandItem
                      key={key}
                      value={remote ? key : getLabel(item)}
                      onSelect={() => {
                        onChange(item);
                        setOpen(false);
                      }}
                      className="min-h-10"
                    >
                      <Check aria-hidden="true" className={cn("me-2 h-4 w-4", selected ? "opacity-100" : "opacity-0")} />
                      <div className="min-w-0 flex-1">{renderItem ? renderItem(item) : getLabel(item)}</div>
                    </CommandItem>
                  );
                })}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
