import { useEffect } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/store/authStore";
import { notificationsApi } from "@/api/notifications";
import { useNotificationHub } from "@/hooks/useNotificationHub";

export const UNREAD_NOTIFICATIONS_QUERY_KEY = ["notifications", "unread-count"] as const;

/**
 * Shared source of truth for the unread-notification count rendered in the
 * sidebar and the top navbar bell icon. Listens to the notification hub so the
 * badge updates the instant a new notification arrives, and falls back to a
 * periodic refetch + window-focus refresh so the count stays correct even if a
 * SignalR event is missed (e.g. dropped connection during a reconnect).
 */
export function useNotifications() {
  const qc = useQueryClient();
  const isAuthed = useAuthStore((s) => !!s.accessToken);

  const query = useQuery({
    queryKey: UNREAD_NOTIFICATIONS_QUERY_KEY,
    queryFn: () => notificationsApi.unreadCount().then((r) => r.count),
    enabled: isAuthed,
    refetchOnWindowFocus: true,
    refetchInterval: 30_000,
    staleTime: 5_000
  });

  useNotificationHub((e) => {
    if (e.kind === "notify") {
      qc.invalidateQueries({ queryKey: UNREAD_NOTIFICATIONS_QUERY_KEY });
    }
  });

  useEffect(() => {
    if (!isAuthed) qc.setQueryData(UNREAD_NOTIFICATIONS_QUERY_KEY, 0);
  }, [isAuthed, qc]);

  return query.data ?? 0;
}
