import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Bell, Check, CheckCheck, MessageSquare, Heart, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { EmptyState } from "@/components/common/EmptyState";
import { notificationsApi, type Notification, type NotificationPayload } from "@/api/notifications";
import { UNREAD_NOTIFICATIONS_QUERY_KEY } from "@/hooks/useNotifications";
import { useNotificationHub } from "@/hooks/useNotificationHub";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { formatDate, formatNumber, formatRelativeTime } from "@/lib/format";
import { useAnnounce } from "@/components/common/a11y";

function parsePayload(payload: string | null): NotificationPayload | null {
  if (!payload) return null;
  try { return JSON.parse(payload); }
  catch { return null; }
}

function getNotificationIcon(payload: NotificationPayload | null) {
  if (!payload) return <Bell aria-hidden="true" className="h-5 w-5 text-muted-foreground" />;
  switch (payload.type) {
    case "post_reaction": return <Heart aria-hidden="true" className="h-5 w-5 text-rose-500" />;
    case "post_comment": return <MessageSquare aria-hidden="true" className="h-5 w-5 text-blue-500" />;
    default: return <Bell aria-hidden="true" className="h-5 w-5 text-muted-foreground" />;
  }
}

function timeAgo(dateStr: string, justNow: string) {
  const diff = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (diff < 60) return justNow;
  if (diff < 604800) return formatRelativeTime(dateStr);
  return formatDate(dateStr);
}

export function NotificationsPage() {
  const nav = useNavigate();
  const qc = useQueryClient();
  const { t } = useI18n();
  const announce = useAnnounce();
  const [filter, setFilter] = useState<"all" | "unread">("all");

  const { data: notifications, isLoading } = useQuery({
    queryKey: ["notifications", "list", filter],
    queryFn: () => notificationsApi.list({
      unreadOnly: filter === "unread" ? true : undefined,
      pageSize: 100
    })
  });

  // Refresh list when a new notification arrives via SignalR
  useNotificationHub((e) => {
    if (e.kind === "notify") {
      qc.invalidateQueries({ queryKey: ["notifications", "list"] });
      qc.invalidateQueries({ queryKey: UNREAD_NOTIFICATIONS_QUERY_KEY });
    }
  });

  const markReadMutation = useMutation({
    mutationFn: (id: string) => notificationsApi.markAsRead(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["notifications", "list"] });
      qc.invalidateQueries({ queryKey: UNREAD_NOTIFICATIONS_QUERY_KEY });
    }
  });

  const markAllReadMutation = useMutation({
    mutationFn: () => notificationsApi.markAllAsRead(),
    onSuccess: () => {
      announce(t("notif.allMarkedRead"));
      qc.invalidateQueries({ queryKey: ["notifications", "list"] });
      qc.invalidateQueries({ queryKey: UNREAD_NOTIFICATIONS_QUERY_KEY });
    }
  });

  function handleNotificationClick(notification: Notification) {
    // Mark as read
    if (!notification.isRead) {
      markReadMutation.mutate(notification.id);
    }

    // Navigate to the post if payload contains postId
    const payload = parsePayload(notification.payload);
    if (payload?.postId) {
      nav(`/feed/${payload.postId}`);
    }
  }

  const unreadCount = notifications?.filter((n) => !n.isRead).length ?? 0;

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{t("nav.notifications")}</h1>
          {unreadCount > 0 && (
            <p className="text-sm text-muted-foreground">{t("notif.unreadCount", { count: formatNumber(unreadCount) })}</p>
          )}
        </div>
        <div className="flex items-center gap-2">
          {unreadCount > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => markAllReadMutation.mutate()}
              disabled={markAllReadMutation.isPending}
            >
              {markAllReadMutation.isPending ? (
                <Loader2 aria-hidden="true" className="h-4 w-4 me-1 animate-spin" />
              ) : (
                <CheckCheck aria-hidden="true" className="h-4 w-4 me-1" />
              )}
              {t("notif.markAllRead")}
            </Button>
          )}
        </div>
      </div>

      {/* Filter tabs */}
      <div className="flex gap-2" role="group" aria-label={t("notif.filter")}>
        <Button
          aria-pressed={filter === "all"}
          variant={filter === "all" ? "default" : "outline"}
          size="sm"
          onClick={() => setFilter("all")}
        >
          {t("notif.all")}
        </Button>
        <Button
          aria-pressed={filter === "unread"}
          variant={filter === "unread" ? "default" : "outline"}
          size="sm"
          onClick={() => setFilter("unread")}
        >
          {t("notif.unread")}
        </Button>
      </div>

      {/* Notification list */}
      {isLoading ? (
        <div className="space-y-2" aria-busy="true" role="status" aria-label={t("common.loading")}>
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-20" />
          ))}
        </div>
      ) : !notifications || notifications.length === 0 ? (
        <EmptyState
          title={filter === "unread" ? t("notif.emptyUnread") : t("notif.empty")}
          description={filter === "unread"
            ? t("notif.caughtUp")
            : t("notif.emptyDesc")
          }
        />
      ) : (
        <Card>
          <CardContent className="p-0 divide-y">
            {notifications.map((notification) => {
              const payload = parsePayload(notification.payload);
              return (
                <button
                  key={notification.id}
                  onClick={() => handleNotificationClick(notification)}
                  className={cn(
                    "w-full flex items-start gap-3 p-3 sm:p-4 text-start transition-colors hover:bg-muted",
                    !notification.isRead && "bg-primary/5"
                  )}
                >
                  {/* Icon */}
                  <div className="shrink-0 mt-0.5">
                    {getNotificationIcon(payload)}
                  </div>

                  {/* Content */}
                  <div className="flex-1 min-w-0 space-y-1">
                    <div className="flex items-center gap-2">
                      <p className={cn(
                        "text-sm truncate",
                        !notification.isRead ? "font-semibold" : "font-medium"
                      )}>
                        {notification.title}
                      </p>
                      {!notification.isRead && (
                        <Badge variant="default" className="h-2 w-2 p-0 rounded-full shrink-0" />
                      )}
                    </div>
                    <p className="text-sm text-muted-foreground line-clamp-2">
                      {notification.body}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {timeAgo(notification.createdAt, t("notif.justNow"))}
                    </p>
                  </div>

                  {/* Mark as read button */}
                  {!notification.isRead && (
                    <Button
                      variant="ghost"
                      size="icon"
                      className="shrink-0 h-8 w-8"
                      onClick={(e) => {
                        e.stopPropagation();
                        markReadMutation.mutate(notification.id);
                      }}
                      title={t("notif.markRead")}
                      aria-label={t("notif.markRead")}
                    >
                      <Check aria-hidden="true" className="h-4 w-4" />
                    </Button>
                  )}
                </button>
              );
            })}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
