import * as React from "react";
import { Check } from "lucide-react";
import { cn } from "@/lib/utils";

export interface CheckboxProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, "type"> {
  indeterminate?: boolean;
}

/**
 * Lightweight checkbox. Avoids @radix-ui/react-checkbox so we can support
 * indeterminate state (used by the PermissionMatrix module-header rows).
 */
export const Checkbox = React.forwardRef<HTMLInputElement, CheckboxProps>(
  ({ className, indeterminate, checked, ...props }, ref) => {
    const inner = React.useRef<HTMLInputElement | null>(null);
    React.useImperativeHandle(ref, () => inner.current as HTMLInputElement);
    React.useEffect(() => {
      if (inner.current) inner.current.indeterminate = !!indeterminate;
    }, [indeterminate]);

    return (
      <label className={cn("relative inline-flex h-4 w-4 cursor-pointer", className)}>
        <input
          ref={inner}
          type="checkbox"
          checked={checked}
          className="peer absolute inset-0 h-4 w-4 cursor-pointer appearance-none rounded border border-input bg-background checked:bg-primary checked:border-primary indeterminate:bg-primary/60 disabled:cursor-not-allowed disabled:opacity-50"
          {...props}
        />
        {(checked || indeterminate) && (
          <Check className="pointer-events-none absolute inset-0 h-4 w-4 text-primary-foreground" strokeWidth={3} />
        )}
      </label>
    );
  }
);
Checkbox.displayName = "Checkbox";
