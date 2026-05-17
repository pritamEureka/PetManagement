import { api } from "./client";
import type { AdoptionListingInput, AdoptionWantedInput } from "@/lib/schemas";

export type AdoptionListingStatus =
  "Draft" | "PendingApproval" | "Approved" | "Rejected" | "Adopted" | "Closed";

export type AdoptionRequestStatus =
  "Pending" | "Approved" | "Rejected" | "Withdrawn" | "Selected";

export interface AdoptionListingSummary {
  id: string;
  title: string;
  petName?: string | null;
  animalType: string;
  breed?: string | null;
  gender: string;
  size?: string | null;
  ageMonths?: number | null;
  location?: string | null;
  adoptionFee: number;
  vaccinated: boolean;
  neuteredSpayed: boolean;
  photoUrls: string[];
  ownerId: string;
  ownerDisplayName: string;
  ownerAvatarUrl?: string | null;
  status: AdoptionListingStatus;
  isSaved: boolean;
  isOwn: boolean;
  createdAt: string;
}

export interface AdoptionListingDetail extends AdoptionListingSummary {
  description?: string | null;
  color?: string | null;
  vaccinationDetails?: string | null;
  healthCondition?: string | null;
  behaviorNotes?: string | null;
  goodWithChildren?: boolean | null;
  goodWithOtherPets?: boolean | null;
  reasonForListing?: string | null;
  contactPreference: string;
  adminNotes?: string | null;
  submittedAt?: string | null;
  decidedAt?: string | null;
  adoptedAt?: string | null;
  requestCount: number;
}

export interface AdoptionRequestRow {
  id: string;
  adoptionListingId: string;
  requesterId: string;
  requesterName: string;
  message: string;
  status: AdoptionRequestStatus;
  createdAt: string;
}

export interface CursorPage<T> { items: T[]; nextCursor?: string | null; }

const unwrap = <T,>(p: Promise<{ data: { data: T } | T }>) =>
  p.then((r) => {
    const body: any = r.data;
    return (body && typeof body === "object" && "data" in body ? body.data : body) as T;
  });

interface SearchParams {
  cursor?: string; pageSize?: number;
  animalType?: string; breed?: string; size?: string; gender?: string;
  location?: string; maxFee?: number;
  vaccinatedOnly?: boolean; neuteredOnly?: boolean;
  goodWithChildren?: boolean; goodWithOtherPets?: boolean;
  sort?: string;
}

export const adoptionApi = {
  list:        (p: SearchParams = {}) =>
                 unwrap<CursorPage<AdoptionListingSummary>>(api.get("/v1/adoption/listings", { params: p })),
  mine:        (p: { cursor?: string; pageSize?: number; status?: AdoptionListingStatus } = {}) =>
                 unwrap<CursorPage<AdoptionListingSummary>>(api.get("/v1/adoption/listings/mine", { params: p })),
  saved:       (p: { cursor?: string; pageSize?: number } = {}) =>
                 unwrap<CursorPage<AdoptionListingSummary>>(api.get("/v1/adoption/listings/saved", { params: p })),
  adminQueue:  (p: { cursor?: string; pageSize?: number; status?: AdoptionListingStatus } = {}) =>
                 unwrap<CursorPage<AdoptionListingSummary>>(api.get("/v1/adoption/admin/listings", { params: p })),

  get:         (id: string) => unwrap<AdoptionListingDetail>(api.get(`/v1/adoption/listings/${id}`)),

  create:      (data: AdoptionListingInput) => unwrap<{ id: string }>(api.post("/v1/adoption/listings", data)),
  update:      (id: string, data: AdoptionListingInput) => api.put(`/v1/adoption/listings/${id}`, data),
  remove:      (id: string) => api.delete(`/v1/adoption/listings/${id}`),
  submit:      (id: string) => api.post(`/v1/adoption/listings/${id}/submit`),
  close:       (id: string) => api.post(`/v1/adoption/listings/${id}/close`),
  markAdopted: (id: string, adoptedByUserId?: string) => api.post(`/v1/adoption/listings/${id}/adopted`, { adoptedByUserId }),

  approve:     (id: string, adminNotes?: string) => api.post(`/v1/adoption/listings/${id}/approve`, { adminNotes }),
  reject:      (id: string, adminNotes?: string) => api.post(`/v1/adoption/listings/${id}/reject`, { adminNotes }),

  toggleSaved: (id: string) => unwrap<{ saved: boolean }>(api.post(`/v1/adoption/listings/${id}/saved`)),

  listingRequests: (id: string) => unwrap<AdoptionRequestRow[]>(api.get(`/v1/adoption/listings/${id}/requests`)),
  applyToAdopt: (listingId: string, message: string) =>
    unwrap<AdoptionRequestRow>(api.post(`/v1/adoption/listings/${listingId}/requests`, { message }))
};

export const adoptionRequestsApi = {
  mine:     () => unwrap<AdoptionRequestRow[]>(api.get("/v1/adoption-requests/mine")),
  withdraw: (id: string) => api.post(`/v1/adoption-requests/${id}/withdraw`),
  setStatus: (id: string, status: AdoptionRequestStatus) =>
    api.put(`/v1/adoption-requests/${id}/status`, { status })
};

export const wantedPostsApi = {
  create: (data: AdoptionWantedInput) => unwrap<{ id: string }>(api.post("/v1/adoption-requests/wanted", data)),
  list:   (p: { cursor?: string; pageSize?: number; animalType?: string; location?: string } = {}) =>
            unwrap<CursorPage<any>>(api.get("/v1/adoption-requests/wanted", { params: p })),
  mine:   () => unwrap<any[]>(api.get("/v1/adoption-requests/wanted/mine"))
};
