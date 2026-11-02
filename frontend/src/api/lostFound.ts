import { api } from "./client";

const V1 = "/v1/lost-found";
const data = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

export type LostFoundKind = "Lost" | "Found";
export type LostFoundStatus = "Open" | "Reunited" | "Closed";

export interface LostFoundSummary {
  id: string; kind: LostFoundKind; status: LostFoundStatus; animalType: string; breed?: string | null;
  color?: string | null; description: string; photoUrl?: string | null; lastSeenAt: string; city: string;
  area?: string | null; reporterId: string; reporterName: string; petId?: string | null; petName?: string | null;
  createdAt: string;
}

export interface LostFoundDetail {
  report: LostFoundSummary;
  latitude?: number | null; longitude?: number | null;
  contactPreference: "Chat" | "Phone" | "Email"; contactPhone?: string | null;
  microchipNumber?: string | null; tagNumber?: string | null; resolvedAt?: string | null;
  feedPostId?: string | null; isMine: boolean;
}

export interface LostFoundMatch { report: LostFoundSummary; score: number; reasons: string[]; }

export interface CreateLostFoundInput {
  kind: LostFoundKind; petId?: string | null; animalType?: string | null; breed?: string | null; color?: string | null;
  description: string; photoUrl?: string | null; lastSeenAt: string; city: string; area?: string | null;
  latitude?: number | null; longitude?: number | null; contactPreference: "Chat" | "Phone" | "Email";
  contactPhone?: string | null; shareToFeed: boolean;
}

export const lostFoundApi = {
  search: (params: { kind?: LostFoundKind; animalType?: string; city?: string; status?: LostFoundStatus; q?: string; page?: number; pageSize?: number }) =>
    data<{ items: LostFoundSummary[]; total: number; page: number; pageSize: number }>(api.get(V1, { params })),
  mine: () => data<LostFoundSummary[]>(api.get(`${V1}/mine`)),
  get: (id: string) => data<LostFoundDetail>(api.get(`${V1}/${id}`)),
  matches: (id: string) => data<LostFoundMatch[]>(api.get(`${V1}/${id}/matches`)),
  create: (input: CreateLostFoundInput) => data<LostFoundDetail>(api.post(V1, input)),
  resolve: (id: string, reunited: boolean, note?: string) => api.post(`${V1}/${id}/resolve`, { reunited, note }),
  remove: (id: string) => api.delete(`${V1}/${id}`),
};
