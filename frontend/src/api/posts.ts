import { api } from "./client";

// ---------- Domain types ----------
export interface FeedAuthor { id: string; displayName: string; avatarUrl?: string | null; }
export interface FeedMedia  { url: string; mediaType: string; }

export interface FeedItem {
  id: string;
  content: string | null;
  animalType: string | null;
  location: string | null;
  createdAt: string;
  updatedAt?: string | null;
  author: FeedAuthor;
  media: FeedMedia[];
  hashtags: string[];
  reactionCount: number;
  commentCount: number;
  shareCount: number;
  myReaction?: string | null;
  isSaved: boolean;
  isOwn: boolean;
  isHidden: boolean;
}

export interface Comment {
  id: string;
  postId: string;
  parentCommentId?: string | null;
  authorId: string;
  authorName: string;
  authorAvatarUrl?: string | null;
  content: string;
  createdAt: string;
  updatedAt?: string | null;
  isOwn: boolean;
}

export interface CursorPage<T> { items: T[]; nextCursor?: string | null; }

export interface CreatePostBody {
  content?: string;
  animalType?: string;
  location?: string;
  mediaUrls?: string[];
  hashtags?: string[];
  petTagIds?: string[];
}
export interface UpdatePostBody {
  content?: string;
  animalType?: string;
  location?: string;
  hashtags?: string[];
}

// ---------- Helpers ----------
const unwrap = <T,>(p: Promise<{ data: { data: T } | T }>) =>
  p.then((r) => {
    const body: any = r.data;
    return (body && typeof body === "object" && "data" in body ? body.data : body) as T;
  });

interface FeedParams { cursor?: string; pageSize?: number; animalType?: string; hashtag?: string; }

// ---------- Public surface ----------
export const postsApi = {
  // Feeds
  feed:       (p: FeedParams = {}) => unwrap<CursorPage<FeedItem>>(api.get("/v1/posts", { params: p })),
  following:  (p: FeedParams = {}) => unwrap<CursorPage<FeedItem>>(api.get("/v1/posts/following", { params: p })),
  mine:       (p: FeedParams = {}) => unwrap<CursorPage<FeedItem>>(api.get("/v1/posts/mine", { params: p })),
  byUser:     (userId: string, p: FeedParams = {}) =>
    unwrap<CursorPage<FeedItem>>(api.get(`/v1/posts/user/${userId}`, { params: p })),
  getById:    (id: string) => unwrap<FeedItem>(api.get(`/v1/posts/${id}`)),

  // Mutations
  create:     (body: CreatePostBody) => unwrap<{ id: string }>(api.post("/v1/posts", body)),
  update:     (id: string, body: UpdatePostBody) => api.put(`/v1/posts/${id}`, body),
  remove:     (id: string) => api.delete(`/v1/posts/${id}`),
  hide:       (id: string, hidden: boolean, reason?: string) => api.post(`/v1/posts/${id}/hide`, { hidden, reason }),

  // Reactions
  react:      (id: string, type: string) => api.post(`/v1/posts/${id}/reactions`, { type }),
  unreact:    (id: string) => api.delete(`/v1/posts/${id}/reactions`),

  // Share + report
  share:      (id: string, note?: string) => api.post(`/v1/posts/${id}/shares`, { note }),
  report:     (id: string, reason: string, details?: string) => api.post(`/v1/posts/${id}/reports`, { reason, details }),

  // Saved
  toggleSave: (id: string) => unwrap<{ saved: boolean }>(api.post(`/v1/saved-posts/${id}`)),
  saved:      (p: FeedParams = {}) => unwrap<CursorPage<FeedItem>>(api.get("/v1/saved-posts", { params: p }))
};

export const commentsApi = {
  list:   (postId: string, p: { cursor?: string; pageSize?: number } = {}) =>
    unwrap<CursorPage<Comment>>(api.get(`/v1/posts/${postId}/comments`, { params: p })),
  add:    (postId: string, content: string, parentCommentId?: string) =>
    unwrap<Comment>(api.post(`/v1/posts/${postId}/comments`, { content, parentCommentId })),
  edit:   (id: string, content: string) =>
    unwrap<Comment>(api.put(`/v1/comments/${id}`, { content })),
  remove: (id: string) => api.delete(`/v1/comments/${id}`),
  report: (id: string, reason: string, details?: string) =>
    api.post(`/v1/comments/${id}/reports`, { reason, details })
};
