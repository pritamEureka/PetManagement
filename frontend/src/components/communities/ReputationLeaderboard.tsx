import { useQuery } from "@tanstack/react-query";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useI18n } from "@/lib/i18n";
import { communityPlusApi } from "@/api/socialPlus";
import { ReputationBadge } from "./ReputationBadge";

export function ReputationLeaderboard({ communityId }: { communityId: string }) {
  const { t } = useI18n();
  const { data, isLoading } = useQuery({
    queryKey: ["community-plus", "leaderboard", communityId],
    queryFn: () => communityPlusApi.leaderboard(communityId, 10),
  });
  return (
    <Card>
      <CardContent className="p-4 space-y-2">
        <p className="font-medium">{t("rep.leaderboard")}</p>
        {isLoading ? <Skeleton className="h-16" /> : data && data.length > 0 ? data.map((r) => (
          <div key={r.userId} className="flex items-center justify-between text-sm py-1 border-b last:border-0">
            <span>#{r.rank} {r.displayName}</span>
            <span className="flex items-center gap-2">
              <span className="text-muted-foreground">{r.total} {t("rep.points")}</span>
              <ReputationBadge level={r.level} levelName={r.levelName} />
            </span>
          </div>
        )) : <p className="text-sm text-muted-foreground">{t("rep.empty")}</p>}
      </CardContent>
    </Card>
  );
}
