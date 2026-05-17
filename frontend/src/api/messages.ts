import { api } from "./client";

export interface Participant {
  userId: string;
  displayName: string;
  avatarUrl?: string | null;
  online: boolean;
}

export interface ConversationSummary {
  id: string;
  title?: string | null;
  isGroup: boolean;
  contextType?: string | null;
  contextRefId?: string | null;
  lastMessageAt?: string | null;
  lastMessagePreview?: string | null;
  unreadCount: number;
  isArchived: boolean;
  isMuted: boolean;
  participants: Participant[];
}

export interface Attachment {
  url: string;
  mimeType: string;
  sizeBytes: number;
  fileName?: string | null;
  width?: number | null;
  height?: number | null;
}

export interface ChatMessage {
  id: string;
  conversationId: string;
  senderId: string;
  senderName: string;
  senderAvatarUrl?: string | null;
  type: string;
  content?: string | null;
  mediaUrl?: string | null;
  replyToMessageId?: string | null;
  isEdited: boolean;
  isDeletedForAll: boolean;
  createdAt: string;
  attachments: Attachment[];
  deliveredAt?: string | null;
  readAt?: string | null;
}

export interface CursorPage<T> { items: T[]; nextCursor?: string | null; }

const unwrap = <T,>(p: Promise<{ data: { data: T } | T }>) =>
  p.then((r) => {
    const body: any = r.data;
    return (body && typeof body === "object" && "data" in body ? body.data : body) as T;
  });

export const messagesApi = {
  conversations: (params: { includeArchived?: boolean; search?: string } = {}) =>
    unwrap<ConversationSummary[]>(api.get("/v1/messages/conversations", { params })),

  get: (id: string) => unwrap<ConversationSummary>(api.get(`/v1/messages/conversations/${id}`)),

  start: (otherUserId: string, contextType?: string, contextRefId?: string, initialMessage?: string) =>
    unwrap<{ id: string }>(api.post("/v1/messages/conversations", {
      otherUserId, contextType, contextRefId, initialMessage
    })),

  history: (id: string, cursor?: string, pageSize = 50) =>
    unwrap<CursorPage<ChatMessage>>(api.get(`/v1/messages/conversations/${id}/messages`, { params: { cursor, pageSize } })),

  send: (conversationId: string, body: {
    type: string; content?: string; mediaUrl?: string; replyToMessageId?: string;
    attachments?: Attachment[];
  }) => unwrap<ChatMessage>(api.post(`/v1/messages/conversations/${conversationId}/messages`, body)),

  markRead: (id: string, lastMessageId?: string) =>
    api.post(`/v1/messages/conversations/${id}/read`, { lastMessageId }),

  archive: (id: string, archived: boolean) =>
    api.post(`/v1/messages/conversations/${id}/archive`, { archived }),

  mute: (id: string, muted: boolean) =>
    api.post(`/v1/messages/conversations/${id}/mute`, { muted }),

  unreadCount: () => unwrap<{ count: number }>(api.get("/v1/messages/unread-count")),

  deleteMessage: (id: string) => api.delete(`/v1/messages/messages/${id}`),
  reportMessage: (id: string, reason: string, details?: string) =>
    api.post(`/v1/messages/messages/${id}/report`, { reason, details }),

  block: (blockedUserId: string, reason?: string) =>
    api.post("/v1/messages/block", { blockedUserId, reason }),
  unblock: (userId: string) => api.post(`/v1/messages/unblock/${userId}`),
  listBlocked: () => unwrap<string[]>(api.get("/v1/messages/blocked")),
};
