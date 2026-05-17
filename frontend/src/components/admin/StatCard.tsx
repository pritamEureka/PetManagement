import type { LucideIcon } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/utils";

interface Props {
  label: string;
  value: number | string;
  hint?: string;
  /** Decimal change vs. previous period (e.g. 0.12 = +12%). */
  delta?: number;
  icon?: LucideIcon;
  tone?: "default" | "warning" | "destructive" | "success";
}

const TONE: Record<NonNullable<Props["tone"]>, string> = {
  default: "bg-primary/10 text-primary",
  warning: "bg-amber-500/10 text-amber-600",
  destructive: "bg-destructive/10 text-destructive",
  success: "bg-emerald-500/10 text-emerald-600"
};

/**
 * One stat tile for the dashboard. Keeps icon, label, value, and optional
 * delta consistent across pages so the grid reads as a unit.
 */
export function StatCard({ label, value, hint, delta, icon: Icon, tone = "default" }: Props) {
  return (
    <Card>
      <CardContent className="pt-5 pb-5 flex items-start gap-3">
        {Icon && (
          <div className={cn("h-10 w-10 rounded-md grid place-items-center", TONE[tone])}>
            <Icon className="h-5 w-5" />
          </div>
        )}
        <div className="flex-1 min-w-0">
          <p className="text-xs text-muted-foreground">{label}</p>
          <p className="text-2xl font-semibold tabular-nums">{value}</p>
          {hint && <p className="text-xs text-muted-foreground mt-0.5 truncate">{hint}</p>}
          {delta !== undefined && (
            <p className={cn(
              "text-xs mt-1 tabular-nums",
              delta >= 0 ? "text-emerald-600" : "text-destructive"
            )}>
              {delta >= 0 ? "▲" : "▼"} {Math.abs(delta * 100).toFixed(1)}%
            </p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
