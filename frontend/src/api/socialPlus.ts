import { api } from "./client";

const data = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

// ---------- community polls + reputation ----------
export interface PollOptionResult { id: string; text: string; votes: number; percent: number; voted: boolean; }
export interface Poll {
  id: string; communityId: string; question: string; multiChoice: boolean; closesAt?: string | null; isClosed: boolean;
  createdByUserId: string; createdByName: string; createdAt: string; totalVoters: number; hasVoted: boolean;
  canManage: boolean; options: PollOptionResult[];
}
export interface ReputationEntry { userId: string; displayName: string; avatarUrl?: string | null; total: number; level: number; levelName: string; rank: number; }
export interface MyReputation { total: number; level: number; levelName: string; nextLevelAt: number; }

const CP = "/v1/community-plus";
export const communityPlusApi = {
  polls: (communityId: string) => data<Poll[]>(api.get(`${CP}/${communityId}/polls`)),
  createPoll: (communityId: string, body: { question: string; options: string[]; multiChoice: boolean; closesAt?: string | null }) =>
    data<Poll>(api.post(`${CP}/${communityId}/polls`, body)),
  vote: (pollId: string, optionIds: string[]) => data<Poll>(api.post(`${CP}/polls/${pollId}/vote`, { optionIds })),
  close: (pollId: string) => data<Poll>(api.post(`${CP}/polls/${pollId}/close`)),
  remove: (pollId: string) => api.delete(`${CP}/polls/${pollId}`),
  leaderboard: (communityId: string, take = 10) => data<ReputationEntry[]>(api.get(`${CP}/${communityId}/leaderboard`, { params: { take } })),
  myReputation: (communityId?: string) => data<MyReputation>(api.get(`${CP}/reputation/me`, { params: { communityId } })),
};

// ---------- nearby feed ----------
export interface NearbyItem { post: import("./posts").FeedItem; distanceKm?: number | null; city?: string | null; }
export const nearbyApi = {
  list: (p: { lat?: number; lng?: number; radiusKm?: number; city?: string; cursor?: string; pageSize?: number }) =>
    data<{ items: NearbyItem[]; nextCursor?: string | null }>(api.get("/v1/posts/nearby", { params: p })),
};

// ---------- pet albums / follows ----------
export type AlbumVisibility = "Public" | "Followers" | "Private";
export interface PetAlbumPhoto { id: string; url: string; caption?: string | null; takenAt?: string | null; sortOrder: number; }
export interface PetAlbum { id: string; petId: string; title: string; visibility: AlbumVisibility; coverUrl?: string | null; photoCount: number; photos: PetAlbumPhoto[]; }
export interface PetSocialProfile {
  petId: string; name: string; photoUrl?: string | null; animalType: string; breed?: string | null;
  followerCount: number; isFollowing: boolean; isOwner: boolean; albums: PetAlbum[];
}
export interface FollowedPetPhoto { petId: string; petName: string; albumId: string; albumTitle: string; photo: PetAlbumPhoto; }

const PS = "/v1/pet-social";
export const petSocialApi = {
  byCode: (code: string) => data<PetSocialProfile>(api.get(`/v1/public/pets/${encodeURIComponent(code)}/social`)),
  profile: (petId: string) => data<PetSocialProfile>(api.get(`${PS}/${petId}`)),
  follow: (petId: string) => api.post(`${PS}/${petId}/follow`),
  unfollow: (petId: string) => api.delete(`${PS}/${petId}/follow`),
  followedPhotos: (take = 20) => data<FollowedPetPhoto[]>(api.get(`${PS}/followed/photos`, { params: { take } })),
  createAlbum: (petId: string, body: { title: string; visibility: AlbumVisibility; coverUrl?: string | null }) =>
    data<PetAlbum>(api.post(`${PS}/${petId}/albums`, body)),
  updateAlbum: (albumId: string, body: { title?: string; visibility?: AlbumVisibility; coverUrl?: string }) =>
    data<PetAlbum>(api.put(`${PS}/albums/${albumId}`, body)),
  deleteAlbum: (albumId: string) => api.delete(`${PS}/albums/${albumId}`),
  addPhoto: (albumId: string, body: { url: string; caption?: string | null; takenAt?: string | null }) =>
    data<PetAlbumPhoto>(api.post(`${PS}/albums/${albumId}/photos`, body)),
  deletePhoto: (albumId: string, photoId: string) => api.delete(`${PS}/albums/${albumId}/photos/${photoId}`),
};

// ---------- lost & found extras ----------
export type SightingStatus = "Pending" | "Verified" | "Rejected";
export interface Sighting {
  id: string; reportId: string; reporterUserId: string; reporterName: string; seenAt: string; latitude?: number | null;
  longitude?: number | null; description: string; photoUrl?: string | null; status: SightingStatus; verifiedAt?: string | null;
  createdAt: string; isMine: boolean;
}
export interface TimelineEvent { kind: string; at: string; title: string; detail?: string | null; refId?: string | null; }
export interface Evidence { id: string; reportId: string; url: string; kind: "Photo" | "Document"; uploadedBy: string; createdAt: string; }
export interface AlertSubscription { id: string; latitude: number; longitude: number; radiusKm: number; animalType?: string | null; active: boolean; createdAt: string; }

const LF = "/v1/lost-found";
export const lostFoundPlusApi = {
  sightings: (id: string) => data<Sighting[]>(api.get(`${LF}/${id}/sightings`)),
  addSighting: (id: string, body: { seenAt: string; latitude?: number | null; longitude?: number | null; description: string; photoUrl?: string | null }) =>
    data<Sighting>(api.post(`${LF}/${id}/sightings`, body)),
  verify: (id: string, sid: string) => data<Sighting>(api.post(`${LF}/${id}/sightings/${sid}/verify`)),
  reject: (id: string, sid: string) => data<Sighting>(api.post(`${LF}/${id}/sightings/${sid}/reject`)),
  timeline: (id: string) => data<TimelineEvent[]>(api.get(`${LF}/${id}/timeline`)),
  evidence: (id: string) => data<Evidence[]>(api.get(`${LF}/${id}/evidence`)),
  addEvidence: (id: string, url: string, kind: "Photo" | "Document") => data<Evidence>(api.post(`${LF}/${id}/evidence`, { url, kind })),
  alerts: () => data<AlertSubscription[]>(api.get(`${LF}/alerts`)),
  subscribe: (body: { latitude: number; longitude: number; radiusKm: number; animalType?: string | null }) =>
    data<AlertSubscription>(api.post(`${LF}/alerts`, body)),
  unsubscribe: (id: string) => api.delete(`${LF}/alerts/${id}`),
};

// ---------- reputation badges (batched) ----------
export interface ReputationBadgeInfo { total: number; level: number; levelName: string; }
export const reputationBadgesApi = {
  batch: (userIds: string[]) =>
    data<Record<string, ReputationBadgeInfo>>(api.post(`${CP}/reputation/badges`, userIds, { skipErrorToast: true } as any)),
};
