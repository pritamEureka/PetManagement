import { useState, type ReactNode } from "react";
import { ArrowDown, ArrowUp, ChevronsUpDown, Rows3 } from "lucide-react";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { safeRead, safeWrite } from "@/lib/safeStorage";

export interface Column<T> {
  key: string;
  header: string;
  className?: string;
  render: (row: T) => ReactNode;
  /** Opt-in client sorting; value used for comparison. */
  sortValue?: (row: T) => string | number | null | undefined;
}

export type Density = "comfortable" | "compact";

interface Props<T> {
  data: T[];
  columns: Column<T>[];
  rowKey: (row: T) => string;
  empty?: ReactNode;
  loading?: boolean;
  onRowClick?: (row: T) => void;
  /** Row density. Omit to use the user's saved preference (see `showDensityToggle`). */
  density?: Density;
  /** Render a compact/comfortable toggle above the table (persisted per user device). */
  showDensityToggle?: boolean;
  /** Accessible table caption (visually hidden). Strongly recommended. */
  caption?: string;
}

const DENSITY_KEY = "pawzaroo:table-density";

export function DataTable<T>({ data, columns, rowKey, empty, loading, onRowClick, density, showDensityToggle, caption }: Props<T>) {
  const { t } = useI18n();
  const [stored, setStored] = useState<Density>(() => safeRead<Density>(DENSITY_KEY, "comfortable"));
  const [sort, setSort] = useState<{ key: string; dir: "asc" | "desc" } | null>(null);
  const effective: Density = density ?? stored;

  if (loading) {
    return <div className="space-y-2" aria-busy="true" role="status" aria-label={t("common.loading")}>{[...Array(4)].map((_, i) => <div key={i} className="h-10 bg-muted/50 rounded animate-pulse" />)}</div>;
  }
  if (data.length === 0) return <>{empty}</>;

  const col = sort ? columns.find((c) => c.key === sort.key) : undefined;
  const rows =
    col?.sortValue && sort
      ? [...data].sort((a, b) => {
          const av = col.sortValue!(a) ?? "";
          const bv = col.sortValue!(b) ?? "";
          const r = typeof av === "number" && typeof bv === "number" ? av - bv : String(av).localeCompare(String(bv));
          return sort.dir === "asc" ? r : -r;
        })
      : data;

  const cell = effective === "compact" ? "px-2 py-1 text-xs" : "px-3 py-2";

  return (
    <div className="space-y-2">
      {showDensityToggle && density === undefined && (
        <div className="flex justify-end">
          <button
            type="button"
            aria-pressed={stored === "compact"}
            onClick={() => {
              const next: Density = stored === "compact" ? "comfortable" : "compact";
              setStored(next);
              safeWrite(DENSITY_KEY, next);
            }}
            className="inline-flex min-h-8 items-center gap-1 rounded-md border px-2 text-xs hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring"
          >
            <Rows3 aria-hidden="true" className="h-3.5 w-3.5" />
            {stored === "compact" ? t("table.comfortable") : t("table.compact")}
          </button>
        </div>
      )}
      <div className="overflow-x-auto rounded-md border">
        <table className="w-full text-sm">
          {caption && <caption className="sr-only">{caption}</caption>}
          <thead className="bg-muted/40">
            <tr className="text-start">
              {columns.map((c) => {
                const active = sort?.key === c.key;
                const ariaSort = c.sortValue ? (active ? (sort!.dir === "asc" ? "ascending" : "descending") : "none") : undefined;
                return (
                  <th key={c.key} scope="col" aria-sort={ariaSort} className={cn("text-start font-medium text-muted-foreground", cell, c.className)}>
                    {c.sortValue ? (
                      <button
                        type="button"
                        className="inline-flex items-center gap-1 rounded focus-visible:ring-2 focus-visible:ring-ring"
                        onClick={() => setSort(active && sort!.dir === "asc" ? { key: c.key, dir: "desc" } : { key: c.key, dir: "asc" })}
                      >
                        {c.header}
                        {active ? (sort!.dir === "asc" ? <ArrowUp aria-hidden="true" className="h-3 w-3" /> : <ArrowDown aria-hidden="true" className="h-3 w-3" />) : <ChevronsUpDown aria-hidden="true" className="h-3 w-3 opacity-50" />}
                      </button>
                    ) : (
                      c.header
                    )}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={rowKey(row)}
                className={cn("border-t hover:bg-background/80 dark:hover:bg-background/60", onRowClick && "cursor-pointer")}
                onClick={() => onRowClick?.(row)}
                tabIndex={onRowClick ? 0 : undefined}
                onKeyDown={
                  onRowClick
                    ? (e) => {
                        if (e.target === e.currentTarget && (e.key === "Enter" || e.key === " ")) {
                          e.preventDefault();
                          onRowClick(row);
                        }
                      }
                    : undefined
                }
              >
                {columns.map((c) => (
                  <td key={c.key} className={cn(cell, c.className)}>{c.render(row)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
