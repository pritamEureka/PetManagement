import { api } from "./client";

// ---------- Domain types ----------
export interface Notification {
  id: string;
  title: string;
  body: string;
  payload: string | null;
  url: string | null;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
}

export interface NotificationPayload {
  type: "post_reaction" | "post_comment";
  postId: string;
  reactorId?: string;
  reactionType?: string;
  commentId?: string;
  commenterId?: string;
}

// ---------- Helpers ----------
const unwrap = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

// ---------- Public surface ----------
export const notificationsApi = {
  list: (params: { unreadOnly?: boolean; page?: number; pageSize?: number } = {}) =>
    unwrap<Notification[]>(api.get("/v1/notifications", { params })),

  unreadCount: () =>
    unwrap<{ count: number }>(api.get("/v1/notifications/unread-count")),

  markAsRead: (id: string) =>
    api.post(`/v1/notifications/${id}/read`),

  markAllAsRead: () =>
    api.post("/v1/notifications/read-all")
};
