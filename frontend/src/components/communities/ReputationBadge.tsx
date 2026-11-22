import { Badge } from "@/components/ui/badge";

const VARIANTS = ["secondary", "secondary", "info", "info", "success", "default"] as const;

/** Small level pill. Pass the level name from the API (Newcomer..Legend). */
export function ReputationBadge({ level, levelName, total }: { level: number; levelName: string; total?: number }) {
  return (
    <Badge variant={VARIANTS[Math.min(Math.max(level, 0), VARIANTS.length - 1)] as any} title={total != null ? total + " pts" : undefined}>
      {levelName}
    </Badge>
  );
}
