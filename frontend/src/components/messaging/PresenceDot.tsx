import { cn } from "@/lib/utils";

interface Props { online?: boolean; className?: string; }

export function PresenceDot({ online, className }: Props) {
  return (
    <span
      className={cn(
        "inline-block h-2 w-2 rounded-full ring-2 ring-background",
        online ? "bg-emerald-500" : "bg-muted-foreground/40",
        className
      )}
      aria-label={online ? "Online" : "Offline"}
    />
  );
}
