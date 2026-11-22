import { api } from "./client";

const data = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

export type CommunityRole = "Member" | "Moderator" | "Owner";
export type CommunityMemberStatus = "Active" | "Pending" | "Banned";
export type CommunityEventType = "Meetup" | "VaccinationCamp" | "AdoptionDrive" | "Other";
export type RsvpStatus = "Going" | "Waitlisted" | "Cancelled";
export type CommunityKind = "Community" | "LocalClub" | "BreedClub";
export const COMMUNITY_KINDS: { value: CommunityKind; label: string }[] = [
  { value: "Community", label: "Community" },
  { value: "LocalClub", label: "Local club" },
  { value: "BreedClub", label: "Breed club" },
];
export type MemberAction = "Approve" | "Ban" | "Unban" | "Promote" | "Demote" | "Remove";

export const EVENT_TYPES: { value: CommunityEventType; label: string }[] = [
  { value: "Meetup", label: "Meetup" },
  { value: "VaccinationCamp", label: "Vaccination camp" },
  { value: "AdoptionDrive", label: "Adoption drive" },
  { value: "Other", label: "Other" },
];

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }

export interface Community {
  id: string; name: string; slug: string; species?: string | null; breed?: string | null; isPublic: boolean;
  description?: string | null; city?: string | null; ownerId: string; memberCount: number;
  myRole?: CommunityRole | null; myStatus?: CommunityMemberStatus | null; createdAt: string;
  kind?: CommunityKind;
}

export interface CreateCommunityInput {
  name: string; species?: string | null; breed?: string | null; isPublic: boolean;
  description?: string | null; city?: string | null; kind?: CommunityKind;
}

export interface CommunityMember {
  userId: string; displayName: string; avatarUrl?: string | null; role: CommunityRole;
  status: CommunityMemberStatus; joinedAt: string;
}

export interface CommunityEvent {
  id: string; communityId: string; communityName: string; organizerId: string; title: string;
  description?: string | null; type: CommunityEventType; startsAt: string; endsAt: string;
  city?: string | null; venue?: string | null; latitude?: number | null; longitude?: number | null;
  capacity?: number | null; isCancelled: boolean; goingCount: number; waitlistCount: number;
  myRsvp?: RsvpStatus | null; distanceKm?: number | null;
}

export interface CreateCommunityEventInput {
  title: string; description?: string | null; type: CommunityEventType; startsAt: string; endsAt: string;
  city?: string | null; venue?: string | null; latitude?: number | null; longitude?: number | null;
  capacity?: number | null;
}

export interface EventAttendee { userId: string; displayName: string; status: RsvpStatus; rsvpAt: string; }

export interface EventDiscoveryParams {
  lat?: number; lng?: number; radiusKm?: number; city?: string; type?: CommunityEventType;
  from?: string; to?: string; page?: number; pageSize?: number;
}

const C = "/v1/communities";
const E = "/v1/community-events";

export const communitiesApi = {
  list: (params: { q?: string; species?: string; city?: string; mine?: boolean; kind?: CommunityKind; page?: number; pageSize?: number } = {}) =>
    data<Paged<Community>>(api.get(C, { params })),
  get: (idOrSlug: string) => data<Community>(api.get(`${C}/${encodeURIComponent(idOrSlug)}`)),
  create: (input: CreateCommunityInput) => data<Community>(api.post(C, input)),
  join: (id: string) => data<Community>(api.post(`${C}/${id}/join`)),
  leave: (id: string) => api.post(`${C}/${id}/leave`).then(() => undefined),
  members: (id: string, status?: CommunityMemberStatus) =>
    data<CommunityMember[]>(api.get(`${C}/${id}/members`, { params: { status } })),
  moderate: (id: string, userId: string, action: MemberAction) =>
    api.post(`${C}/${id}/members/${userId}/${action}`).then(() => undefined),
  events: (id: string, includePast = false) =>
    data<CommunityEvent[]>(api.get(`${C}/${id}/events`, { params: { includePast } })),
  createEvent: (id: string, input: CreateCommunityEventInput) =>
    data<CommunityEvent>(api.post(`${C}/${id}/events`, input)),
};

export const communityEventsApi = {
  discover: (params: EventDiscoveryParams = {}) => data<Paged<CommunityEvent>>(api.get(E, { params })),
  feed: (page = 1, pageSize = 20) => data<Paged<CommunityEvent>>(api.get(`${E}/feed`, { params: { page, pageSize } })),
  get: (id: string) => data<CommunityEvent>(api.get(`${E}/${id}`)),
  rsvp: (id: string) => data<CommunityEvent>(api.post(`${E}/${id}/rsvp`)),
  cancelRsvp: (id: string) => data<CommunityEvent>(api.delete(`${E}/${id}/rsvp`)),
  cancel: (id: string) => api.post(`${E}/${id}/cancel`).then(() => undefined),
  attendees: (id: string) => data<EventAttendee[]>(api.get(`${E}/${id}/attendees`)),
};
