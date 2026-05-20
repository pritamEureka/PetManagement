import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

export interface Column<T> {
  key: string;
  header: string;
  className?: string;
  render: (row: T) => ReactNode;
}

interface Props<T> {
  data: T[];
  columns: Column<T>[];
  rowKey: (row: T) => string;
  empty?: ReactNode;
  loading?: boolean;
  onRowClick?: (row: T) => void;
}

export function DataTable<T>({ data, columns, rowKey, empty, loading, onRowClick }: Props<T>) {
  if (loading) {
    return <div className="space-y-2">{[...Array(4)].map((_, i) => <div key={i} className="h-10 bg-muted/50 rounded animate-pulse" />)}</div>;
  }
  if (data.length === 0) return <>{empty}</>;

  return (
    <div className="overflow-x-auto rounded-md border">
      <table className="w-full text-sm">
        <thead className="bg-muted/40">
          <tr className="text-left">
            {columns.map((c) => (
              <th key={c.key} className={cn("px-3 py-2 font-medium text-muted-foreground", c.className)}>{c.header}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((row) => (
            <tr
              key={rowKey(row)}
              className={cn("border-t hover:bg-background/80 dark:hover:bg-background/60", onRowClick && "cursor-pointer")}
              onClick={() => onRowClick?.(row)}
            >
              {columns.map((c) => (
                <td key={c.key} className={cn("px-3 py-2", c.className)}>{c.render(row)}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
