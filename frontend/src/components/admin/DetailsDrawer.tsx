import type { ReactNode } from "react";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription } from "@/components/ui/sheet";

interface Props {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  title: string;
  description?: string;
  children: ReactNode;
  width?: string; // tailwind class — e.g. "max-w-lg" / "max-w-2xl"
}

/**
 * Slide-in details panel. Used to show row detail without leaving the table.
 */
export function DetailsDrawer({ open, onOpenChange, title, description, children, width = "max-w-2xl" }: Props) {
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className={`${width} w-full`}>
        <SheetHeader>
          <SheetTitle>{title}</SheetTitle>
          {description && <SheetDescription>{description}</SheetDescription>}
        </SheetHeader>
        <div className="py-4 space-y-4">{children}</div>
      </SheetContent>
    </Sheet>
  );
}
