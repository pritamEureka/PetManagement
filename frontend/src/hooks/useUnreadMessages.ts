import { useEffect } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "@/store/authStore";
import { messagesApi } from "@/api/messages";
import { useChatHub } from "@/hooks/useChatHub";

export const UNREAD_MESSAGES_QUERY_KEY = ["messages", "unread-count"] as const;

/**
 * Shared source of truth for the unread-message count rendered in the sidebar.
 * Listens to the chat hub so the badge updates the instant a new message
 * arrives, and falls back to a periodic refetch + window-focus refresh so the
 * count stays correct even if a SignalR event is missed (e.g. dropped
 * connection during a reconnect).
 */
export function useUnreadMessages() {
  const qc = useQueryClient();
  const isAuthed = useAuthStore((s) => !!s.accessToken);

  const query = useQuery({
    queryKey: UNREAD_MESSAGES_QUERY_KEY,
    queryFn: () => messagesApi.unreadCount().then((r) => r.count),
    enabled: isAuthed,
    refetchOnWindowFocus: true,
    refetchInterval: 20_000,
    staleTime: 5_000
  });

  useChatHub((e) => {
    // Any incoming chat event that could change unread state should trigger a
    // refresh — new messages bump the count, reads from another device drop it.
    if (e.kind === "message" || e.kind === "read") {
      qc.invalidateQueries({ queryKey: UNREAD_MESSAGES_QUERY_KEY });
    }
  });

  useEffect(() => {
    // Clear the count immediately on sign-out so a stale badge doesn't linger.
    if (!isAuthed) qc.setQueryData(UNREAD_MESSAGES_QUERY_KEY, 0);
  }, [isAuthed, qc]);

  return query.data ?? 0;
}
