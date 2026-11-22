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
  /** Km from the viewer; only when the search sent a location and the listing has one. */
  distanceKm?: number | null;
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
  /** Approximate (~1 km) point the lister shared. */
  latitude?: number | null;
  longitude?: number | null;
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

export type AdoptionSort = "nearest" | "newest" | "oldest" | "fee_asc" | "fee_desc";

interface SearchParams {
  cursor?: string; pageSize?: number;
  animalType?: string; breed?: string; size?: string; gender?: string;
  location?: string; maxFee?: number;
  vaccinatedOnly?: boolean; neuteredOnly?: boolean;
  goodWithChildren?: boolean; goodWithOtherPets?: boolean;
  sort?: AdoptionSort;
  lat?: number; lng?: number; radiusKm?: number; postedWithinDays?: number;
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
  markAdopted: (id: string, adoptedByUserId?: string, opts?: { transferPetProfile?: boolean; includeHealthHistory?: boolean }) =>
    api.post(`/v1/adoption/listings/${id}/adopted`, { adoptedByUserId, transferPetProfile: opts?.transferPetProfile ?? false, includeHealthHistory: opts?.includeHealthHistory ?? false }),

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

// ---------- Adoption pipeline / matching / follow-ups ----------
export type AdoptionStage =
  "Submitted" | "ReferenceCheck" | "HomeVisit" | "Decision" | "Adopted" | "FollowUp" | "Declined" | "Withdrawn";

export interface StageEvent { stage: AdoptionStage; enteredAt: string; byUserId?: string | null; note?: string | null; dueAt?: string | null }
export interface PipelineItem {
  requestId: string; listingId: string; listingTitle: string; requesterId: string; requesterName: string;
  message: string; status: AdoptionRequestStatus; stage: AdoptionStage; stageEnteredAt: string;
  dueAt?: string | null; isOverdue: boolean; matchPercent?: number | null; createdAt: string;
}
export interface MyApplication {
  requestId: string; listingId: string; listingTitle: string; status: AdoptionRequestStatus;
  stage: AdoptionStage; stageEnteredAt: string; dueAt?: string | null; history: StageEvent[];
}
export interface AdoptionReference { id: string; requestId: string; name: string; contact: string; relationship?: string | null; status: "Pending" | "Contacted" | "Verified" | "Failed"; notes?: string | null }
export interface AdoptionHomeVisit { id: string; requestId: string; scheduledAt: string; status: "Scheduled" | "Completed" | "Cancelled" | "NoShow"; notes?: string | null; outcome?: string | null }
export interface AdoptionFollowUp { id: string; listingId: string; listingTitle: string; dayOffset: number; dueAt: string; status: "Due" | "Completed" | "Skipped"; notes?: string | null; welfareScore?: number | null; completedAt?: string | null }
export interface AdopterProfile {
  householdSize: number; childrenAges?: string | null; existingPetsJson?: string | null;
  experience: "None" | "Some" | "Experienced"; homeType?: string | null; hasYard: boolean;
  travelDistanceKm: number; activity: "Low" | "Moderate" | "High"; latitude?: number | null; longitude?: number | null;
}
export interface FosterPlacement { id: string; listingId: string; petId?: string | null; fosterUserId: string; fosterName: string; startedAt: string; endedAt?: string | null; outcome: "Ongoing" | "FosterFailed" | "ConvertedToAdoption" | "Returned" | number; notes?: string | null }
export interface MatchResult { listingId: string; percent: number; explanations: string[] }
export interface RiskFlag { listingId: string; kind: string; detail: string; severity: number; createdAt: string }

const P = "/v1/adoption-plus";
export const adoptionPlusApi = {
  pipeline: (listingId?: string) => api.get<PipelineItem[]>(`${P}/pipeline`, { params: { listingId } }).then((r) => r.data),
  myApplications: () => api.get<MyApplication[]>(`${P}/my-applications`).then((r) => r.data),
  advance: (requestId: string, stage: AdoptionStage, note?: string) => api.post(`${P}/requests/${requestId}/stage`, { stage, note }),
  references: (requestId: string) => api.get<AdoptionReference[]>(`${P}/requests/${requestId}/references`).then((r) => r.data),
  addReference: (requestId: string, body: { name: string; contact: string; relationship?: string }) =>
    api.post<AdoptionReference>(`${P}/requests/${requestId}/references`, body).then((r) => r.data),
  updateReference: (id: string, status: AdoptionReference["status"], notes?: string) =>
    api.put<AdoptionReference>(`${P}/references/${id}`, { status, notes }).then((r) => r.data),
  homeVisits: (requestId: string) => api.get<AdoptionHomeVisit[]>(`${P}/requests/${requestId}/home-visits`).then((r) => r.data),
  scheduleVisit: (requestId: string, scheduledAt: string, notes?: string) =>
    api.post<AdoptionHomeVisit>(`${P}/requests/${requestId}/home-visits`, { scheduledAt, notes }).then((r) => r.data),
  updateVisit: (id: string, status: AdoptionHomeVisit["status"], notes?: string, outcome?: string) =>
    api.put<AdoptionHomeVisit>(`${P}/home-visits/${id}`, { status, notes, outcome }).then((r) => r.data),
  myFollowUps: () => api.get<AdoptionFollowUp[]>(`${P}/follow-ups/mine`).then((r) => r.data),
  listingFollowUps: (listingId: string) => api.get<AdoptionFollowUp[]>(`${P}/listings/${listingId}/follow-ups`).then((r) => r.data),
  completeFollowUp: (id: string, welfareScore: number, notes?: string) => api.post(`${P}/follow-ups/${id}/complete`, { welfareScore, notes }),
  startFoster: (listingId: string, fosterUserId: string, notes?: string) => api.post(`${P}/listings/${listingId}/fosters`, { fosterUserId, notes }),
  fosters: (listingId: string) => api.get<FosterPlacement[]>(`${P}/listings/${listingId}/fosters`).then((r) => r.data),
  endFoster: (id: string, outcome: "FosterFailed" | "ConvertedToAdoption" | "Returned", notes?: string) => api.post(`${P}/fosters/${id}/end`, { outcome, notes }),
  profile: () => api.get<AdopterProfile | "">(`${P}/profile`).then((r) => (r.data ? (r.data as AdopterProfile) : null)),
  saveProfile: (body: AdopterProfile) => api.put<AdopterProfile>(`${P}/profile`, body).then((r) => r.data),
  match: (listingIds: string[]) => api.post<MatchResult[]>(`${P}/match`, { listingIds }).then((r) => r.data),
  riskFlags: (listingIds: string[]) => api.post<RiskFlag[]>(`${P}/risk-flags`, { listingIds }).then((r) => r.data),
};
