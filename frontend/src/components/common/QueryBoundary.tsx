import type { ReactNode } from "react";
import type { UseQueryResult } from "@tanstack/react-query";
import { AlertTriangle, CloudOff, WifiOff } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ErrorState } from "@/components/common/ErrorState";
import { useI18n } from "@/lib/i18n";
import { useOnlineStatus } from "@/hooks/useOnlineStatus";

type Kind = "offline" | "unavailable" | "error";

function statusOf(err: unknown): number | undefined {
  return (err as { response?: { status?: number } } | null)?.response?.status;
}

/** Classify a failed query so the UI can say "offline" / "temporarily unavailable" / generic error. */
export function classifyQueryError(err: unknown, online: boolean): Kind {
  if (!online) return "offline";
  const s = statusOf(err);
  if (s === 502 || s === 503 || s === 504) return "unavailable";
  // axios network error: no response at all
  if (!(err as any)?.response && (err as any)?.code === "ERR_NETWORK") return "offline";
  return "error";
}

interface Props<T> {
  query: Pick<UseQueryResult<T>, "data" | "isLoading" | "isError" | "error" | "refetch" | "isFetching" | "isPlaceholderData">;
  /** Render when data is available and non-empty. */
  children: (data: T) => ReactNode;
  /** Custom skeleton. Default: a few skeleton rows. */
  skeleton?: ReactNode;
  skeletonRows?: number;
  /** Decide emptiness; default: empty array / null / undefined. */
  isEmpty?: (data: T) => boolean;
  /** EmptyState element (should carry a next-action CTA). */
  empty?: ReactNode;
  /** Show a "partial results" banner (true uses the default text). */
  partial?: boolean | string;
  /** Called by the retry button in addition to refetch. */
  onRetry?: () => void;
  className?: string;
}

/**
 * One consistent wrapper for react-query results:
 * skeleton while loading, retryable error / offline / 503-unavailable states,
 * empty state with CTA, optional partial-results banner, and a stale-data
 * notice (keeps showing old data when a background refetch fails).
 */
export function QueryBoundary<T>({ query, children, skeleton, skeletonRows = 4, isEmpty, empty, partial, onRetry, className }: Props<T>) {
  const { t } = useI18n();
  const online = useOnlineStatus();
  const { data, isLoading, isError, error, refetch } = query;
  const retry = () => {
    onRetry?.();
    void refetch();
  };

  if (isLoading) {
    return (
      skeleton ?? (
        <div className={className} role="status" aria-busy="true" aria-label={t("common.loading")}>
          <div className="space-y-2">
            {Array.from({ length: skeletonRows }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full" />
            ))}
          </div>
        </div>
      )
    ) as ReactNode;
  }

  if (isError && data === undefined) {
    const kind = classifyQueryError(error, online);
    if (kind === "error") return <ErrorState onRetry={retry} message={(error as any)?.response?.data?.error?.message} />;
    const Icon = kind === "offline" ? WifiOff : CloudOff;
    return (
      <Card role="alert" className={className}>
        <CardContent className="py-10 text-center space-y-3">
          <Icon aria-hidden="true" className="h-10 w-10 mx-auto text-muted-foreground" />
          <p className="font-medium">{kind === "offline" ? t("state.offlineTitle") : t("state.unavailableTitle")}</p>
          <p className="text-sm text-muted-foreground max-w-md mx-auto">
            {kind === "offline" ? t("state.offlineBody") : t("state.unavailableBody")}
          </p>
          <Button variant="outline" size="sm" onClick={retry}>
            {t("common.tryAgain")}
          </Button>
        </CardContent>
      </Card>
    );
  }

  const empty_ = data === undefined || data === null || (isEmpty ? isEmpty(data as T) : Array.isArray(data) && data.length === 0);
  if (empty_) return <>{empty ?? null}</>;

  return (
    <div className={className}>
      {!online && (
        <p role="status" className="mb-3 flex items-center gap-2 rounded-md border bg-muted px-3 py-2 text-sm">
          <WifiOff aria-hidden="true" className="h-4 w-4" /> {t("state.offlineStale")}
        </p>
      )}
      {isError && online && (
        <p role="alert" className="mb-3 flex items-center gap-2 rounded-md border border-destructive/40 px-3 py-2 text-sm">
          <AlertTriangle aria-hidden="true" className="h-4 w-4 text-destructive" />
          <span className="flex-1">{t("state.refreshFailed")}</span>
          <Button variant="ghost" size="sm" onClick={retry}>
            {t("common.tryAgain")}
          </Button>
        </p>
      )}
      {partial && (
        <p role="status" className="mb-3 flex items-center gap-2 rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-sm">
          <AlertTriangle aria-hidden="true" className="h-4 w-4" />
          {typeof partial === "string" ? partial : t("state.partial")}
        </p>
      )}
      {children(data as T)}
    </div>
  );
}
