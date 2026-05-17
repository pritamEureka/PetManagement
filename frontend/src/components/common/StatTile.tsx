import type { LucideIcon } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

interface Props {
  label: string;
  value: string | number;
  icon?: LucideIcon;
  trend?: { direction: "up" | "down"; label: string };
  hint?: string;
  className?: string;
}

export function StatTile({ label, value, icon: Icon, trend, hint, className }: Props) {
  const trendColor = trend?.direction === "up" ? "text-emerald-600" : "text-rose-600";
  return (
    <Card className={cn("", className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
          {Icon && <Icon className="h-4 w-4" />} {label}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-3xl font-bold">{value}</p>
        {trend && <p className={cn("text-xs mt-1", trendColor)}>{trend.direction === "up" ? "▲" : "▼"} {trend.label}</p>}
        {hint && !trend && <p className="text-xs text-muted-foreground mt-1">{hint}</p>}
      </CardContent>
    </Card>
  );
}
