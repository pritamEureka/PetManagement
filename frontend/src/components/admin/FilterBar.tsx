import { Search, X } from "lucide-react";
import type { ReactNode } from "react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";

interface Props {
  search?: string;
  onSearch?: (s: string) => void;
  placeholder?: string;
  children?: ReactNode;
  onReset?: () => void;
}

/**
 * Standard filter row used above every admin table:
 *   [ search input ] [ ...child selects ] [ reset ]
 *
 * Children get the right amount of horizontal space and align nicely with the
 * search field. Use semantic shadcn Selects for each filter.
 */
export function FilterBar({ search, onSearch, placeholder = "Search...", children, onReset }: Props) {
  return (
    <div className="flex flex-wrap gap-2 items-center">
      {onSearch && (
        <div className="relative flex-1 min-w-[14rem] max-w-md">
          <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            className="pl-8"
            placeholder={placeholder}
            value={search ?? ""}
            onChange={(e) => onSearch(e.target.value)}
          />
        </div>
      )}
      {children}
      {onReset && (
        <Button variant="ghost" size="sm" onClick={onReset}>
          <X className="h-3 w-3 mr-1" /> Reset
        </Button>
      )}
    </div>
  );
}
