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

// ---------- Messaging plus: search, pins, folders, disappearing, edit/recall, E2EE keys ----------
export interface MessageSearchHit { messageId: string; conversationId: string; senderId: string; senderName: string; content?: string | null; createdAt: string }
export interface PinnedMessage { messageId: string; conversationId: string; content?: string | null; senderId: string; senderName: string; pinnedBy: string; pinnedAt: string }
export interface ConversationFolder { id: string; name: string; conversationIds: string[] }
export interface ConversationSettings { conversationId: string; disappearAfterSeconds?: number | null; mutedUntil?: string | null; e2eeEnabled: boolean }
export interface E2eeKeyInfo { id: string; userId: string; publicKeyJwk: string; createdAt: string }

const MP = "/v1/messaging-plus";
export const messagesPlusApi = {
  search: (q: string, conversationId?: string, cursor?: string) =>
    api.get<{ items: MessageSearchHit[]; nextCursor?: string | null }>(`${MP}/search`, { params: { q, conversationId, cursor } }).then((r) => r.data),
  pins: (conversationId: string) => api.get<PinnedMessage[]>(`${MP}/conversations/${conversationId}/pins`).then((r) => r.data),
  pin: (messageId: string) => api.post(`${MP}/messages/${messageId}/pin`),
  unpin: (messageId: string) => api.delete(`${MP}/messages/${messageId}/pin`),
  folders: () => api.get<ConversationFolder[]>(`${MP}/folders`).then((r) => r.data),
  createFolder: (name: string) => api.post<ConversationFolder>(`${MP}/folders`, { name }).then((r) => r.data),
  renameFolder: (id: string, name: string) => api.put<ConversationFolder>(`${MP}/folders/${id}`, { name }).then((r) => r.data),
  deleteFolder: (id: string) => api.delete(`${MP}/folders/${id}`),
  setFolderItem: (folderId: string, conversationId: string, inFolder: boolean) => api.put(`${MP}/folders/${folderId}/items`, { conversationId, inFolder }),
  settings: (conversationId: string) => api.get<ConversationSettings>(`${MP}/conversations/${conversationId}/settings`).then((r) => r.data),
  setDisappear: (conversationId: string, seconds: number | null) => api.put<ConversationSettings>(`${MP}/conversations/${conversationId}/disappear`, { seconds }).then((r) => r.data),
  setE2ee: (conversationId: string, enabled: boolean) => api.put<ConversationSettings>(`${MP}/conversations/${conversationId}/e2ee`, { enabled }).then((r) => r.data),
  edit: (messageId: string, content: string) => api.put(`${MP}/messages/${messageId}`, { content }),
  recall: (messageId: string) => api.post(`${MP}/messages/${messageId}/recall`),
  registerKey: (publicKeyJwk: string) => api.post<E2eeKeyInfo>(`${MP}/e2ee/keys`, { publicKeyJwk }).then((r) => r.data),
  userKeys: (userId: string) => api.get<E2eeKeyInfo[]>(`${MP}/e2ee/users/${userId}/keys`).then((r) => r.data),
};
