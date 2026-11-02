import { api } from "./client";
import type { AppointmentStatus, PaymentStatusFE } from "./vets";

const V1 = "/v1/pet-services";
const data = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

export const serviceTypes = ["Grooming", "Training", "Boarding", "Walking"] as const;
export type ServiceType = (typeof serviceTypes)[number];

export interface ProviderSummary {
  id: string; userId: string; businessName: string; providerType: ServiceType; about?: string | null;
  city?: string | null; country?: string | null; basePrice: number; defaultDurationMinutes: number;
  ratingAverage: number; ratingCount: number; logoUrl?: string | null; animalTypes: string[]; approvalStatus: string;
}
export interface ProviderDetail {
  provider: ProviderSummary; address?: string | null; phoneNumber?: string | null; ownerName: string;
  completedBookings: number; adminNotes?: string | null;
  reviews: { id: string; rating: number; comment?: string | null; userName: string; createdAt: string }[];
}
export interface RegisterProviderInput {
  providerType: ServiceType; businessName: string; about?: string | null; address?: string | null; city: string;
  country?: string | null; phoneNumber?: string | null; basePrice: number; defaultDurationMinutes: number;
  logoUrl?: string | null; animalTypes?: string[];
}
export interface ServiceBooking {
  id: string; providerId: string; providerName: string; providerType: ServiceType; userId: string; customerName: string;
  petId?: string | null; petName?: string | null; scheduledAt: string; durationMinutes: number;
  status: AppointmentStatus; paymentStatus: PaymentStatusFE; amount: number; notes?: string | null;
  cancellationReason?: string | null; hasReview: boolean; createdAt: string;
}
type Paged<T> = { items: T[]; total: number; page: number; pageSize: number };

export const petServicesApi = {
  search: (params: { type?: ServiceType; city?: string; animalType?: string; q?: string; sort?: string; page?: number }) =>
    data<Paged<ProviderSummary>>(api.get(`${V1}/providers`, { params })),
  get: (id: string) => data<ProviderDetail>(api.get(`${V1}/providers/${id}`)),
  mine: () => data<ProviderDetail>(api.get(`${V1}/providers/me`, { skipErrorToast: true })),
  register: (input: RegisterProviderInput) => data<{ id: string }>(api.post(`${V1}/providers`, input)),
  updateMine: (input: RegisterProviderInput) => api.put(`${V1}/providers/me`, input),

  book: (input: { providerId: string; petId?: string | null; scheduledAt: string; notes?: string | null }) =>
    data<ServiceBooking>(api.post(`${V1}/bookings`, input)),
  myBookings: () => data<ServiceBooking[]>(api.get(`${V1}/bookings/mine`)),
  incoming: (status?: AppointmentStatus) => data<ServiceBooking[]>(api.get(`${V1}/bookings/incoming`, { params: { status } })),
  confirm: (id: string) => api.post(`${V1}/bookings/${id}/confirm`),
  complete: (id: string) => api.post(`${V1}/bookings/${id}/complete`),
  markPaid: (id: string) => api.post(`${V1}/bookings/${id}/mark-paid`),
  cancel: (id: string, reason?: string) => api.post(`${V1}/bookings/${id}/cancel`, { reason }),
  review: (id: string, rating: number, comment?: string) => api.post(`${V1}/bookings/${id}/review`, { rating, comment }),

  admin: {
    list: (status: string, page = 1) => data<Paged<ProviderSummary>>(api.get(`${V1}/admin/providers`, { params: { status, page } })),
    approve: (id: string, notes?: string) => api.post(`${V1}/admin/providers/${id}/approve`, { notes }),
    reject: (id: string, notes?: string) => api.post(`${V1}/admin/providers/${id}/reject`, { notes }),
    suspend: (id: string, notes?: string) => api.post(`${V1}/admin/providers/${id}/suspend`, { notes }),
  },
};
