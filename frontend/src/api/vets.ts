import { api } from "./client";
import type { BookAppointmentInput, DoctorRegistrationInput, AvailabilityRuleInput } from "@/lib/schemas";

export type AppointmentStatus =
  "Draft" | "PendingPayment" | "PendingConfirmation" | "Confirmed"
  | "Rescheduled" | "CancelledByUser" | "CancelledByDoctor"
  | "Completed" | "NoShow" | "Refunded";

export type PaymentStatusFE = "Unpaid" | "Pending" | "Paid" | "Refunded" | "Failed";

export interface Specialty { id: string; slug: string; name: string; }

export interface DoctorSummary {
  id: string;
  userId: string;
  name: string;
  avatarUrl?: string | null;
  primarySpecialty?: string | null;
  specialties: string[];
  clinicName?: string | null;
  city?: string | null;
  country?: string | null;
  consultationFee: number;
  consultationType: string;
  onlineAvailable: boolean;
  offlineAvailable: boolean;
  ratingAverage: number;
  ratingCount: number;
  approvalStatus: string;
  supportedAnimalTypes: string[];
}

export interface DoctorDetail extends DoctorSummary {
  licenseNumber: string;
  specialty?: string | null;
  experienceYears?: number | null;
  about?: string | null;
  clinicAddress?: string | null;
  autoConfirmAppointments: boolean;
  defaultSlotMinutes: number;
  cancellationCutoffHours: number;
  adminNotes?: string | null;
}

export interface TimeSlot {
  id: string;
  startUtc: string;
  endUtc: string;
  consultationType: string;
  status: string;
}

export interface AvailabilityRule {
  id: string;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  slotMinutes: number;
  consultationType: string;
}

export interface Holiday { id: string; date: string; reason?: string | null; }

export interface CredentialDocument {
  id: string;
  kind: number;
  title: string;
  fileUrl: string;
  issuingAuthority?: string | null;
  documentNumber?: string | null;
  issuedOn?: string | null;
  expiresOn?: string | null;
  verified: boolean;
  verifiedAt?: string | null;
}

export interface Appointment {
  id: string;
  doctorId: string;
  doctorName: string;
  patientUserId: string;
  patientName: string;
  petId?: string | null;
  petName?: string | null;
  scheduledAt: string;
  durationMinutes: number;
  type: string;
  status: AppointmentStatus;
  paymentStatus: PaymentStatusFE;
  amount: number;
  meetingLink?: string | null;
  prescriptionFileUrl?: string | null;
  createdAt: string;
}

export interface CursorPage<T> { items: T[]; nextCursor?: string | null; }

const unwrap = <T,>(p: Promise<{ data: { data: T } | T }>) =>
  p.then((r) => {
    const body: any = r.data;
    return (body && typeof body === "object" && "data" in body ? body.data : body) as T;
  });

export const vetsApi = {
  search: (params: {
    cursor?: string; pageSize?: number;
    animalType?: string; specialty?: string; specialtyId?: string;
    city?: string; type?: string; maxPrice?: number; minRating?: number;
    availableThisWeek?: boolean; sort?: string;
  } = {}) => unwrap<CursorPage<DoctorSummary>>(api.get("/v1/vets", { params })),
  adminList: (params: { cursor?: string; pageSize?: number; status?: string } = {}) =>
    unwrap<CursorPage<DoctorSummary>>(api.get("/v1/vets/admin", { params })),

  specialties: () => unwrap<Specialty[]>(api.get("/v1/vets/specialties")),
  get:         (id: string) => unwrap<DoctorDetail>(api.get(`/v1/vets/${id}`)),
  me:          () => unwrap<DoctorDetail>(api.get("/v1/vets/me")),
  reviews:     (id: string) => unwrap<any[]>(api.get(`/v1/vets/${id}/reviews`)),

  slots: (id: string, from: string, to: string, type?: string) =>
    unwrap<TimeSlot[]>(api.get(`/v1/vets/${id}/slots`, { params: { from, to, type } })),

  register: (data: DoctorRegistrationInput) => unwrap<{ id: string }>(api.post("/v1/vets/register", data)),
  update:   (data: Partial<DoctorRegistrationInput>) => api.put("/v1/vets/me", data),

  myCredentials: () => unwrap<CredentialDocument[]>(api.get("/v1/vets/me/credentials")),
  addCredential: (data: { kind: number; title: string; fileUrl: string; issuingAuthority?: string; documentNumber?: string; issuedOn?: string; expiresOn?: string }) =>
    unwrap<{ id: string }>(api.post("/v1/vets/me/credentials", data)),
  doctorCredentials: (id: string) => unwrap<CredentialDocument[]>(api.get(`/v1/vets/${id}/credentials`)),
  verifyCredential: (id: string, verified: boolean) => api.post(`/v1/vets/credentials/${id}/verify`, null, { params: { verified } }),

  approve: (id: string, adminNotes?: string) => api.post(`/v1/vets/${id}/approve`, { adminNotes }),
  reject:  (id: string, adminNotes?: string) => api.post(`/v1/vets/${id}/reject`, { adminNotes }),
  suspend: (id: string, adminNotes?: string) => api.post(`/v1/vets/${id}/suspend`, { adminNotes }),
  commissionReport: (from: string, to: string) =>
    unwrap<{ doctorId: string; doctorName: string; grossFees: number; platformShare: number; appointmentCount: number }[]>(
      api.get("/v1/vets/admin/commission-report", { params: { from, to } })),

  rules:        () => unwrap<AvailabilityRule[]>(api.get("/v1/vets/me/availability/rules")),
  addRule:      (data: AvailabilityRuleInput) => unwrap<{ id: string }>(api.post("/v1/vets/me/availability/rules", data)),
  removeRule:   (id: string) => api.delete(`/v1/vets/me/availability/rules/${id}`),
  holidays:     () => unwrap<Holiday[]>(api.get("/v1/vets/me/availability/holidays")),
  addHoliday:   (date: string, reason?: string) => unwrap<{ id: string }>(api.post("/v1/vets/me/availability/holidays", { date, reason })),
  removeHoliday:(id: string) => api.delete(`/v1/vets/me/availability/holidays/${id}`),
  generateSlots:(fromDate: string, toDate: string) =>
    unwrap<{ generated: number }>(api.post("/v1/vets/me/availability/generate-slots", { fromDate, toDate })),
  mySlots:      (from: string, to: string, availableOnly = false) =>
    unwrap<TimeSlot[]>(api.get("/v1/vets/me/slots", { params: { from, to, availableOnly } })),
  blockSlot:    (id: string) => api.post(`/v1/vets/me/slots/${id}/block`),
  unblockSlot:  (id: string) => api.post(`/v1/vets/me/slots/${id}/unblock`),
};

export const appointmentsApi = {
  mine:    (p: { cursor?: string; pageSize?: number; status?: AppointmentStatus } = {}) =>
             unwrap<CursorPage<Appointment>>(api.get("/v1/appointments/mine", { params: p })),
  clinic:  (p: { cursor?: string; pageSize?: number; status?: AppointmentStatus } = {}) =>
             unwrap<CursorPage<Appointment>>(api.get("/v1/appointments/clinic", { params: p })),
  get:     (id: string) => unwrap<Appointment>(api.get(`/v1/appointments/${id}`)),

  book:    (data: BookAppointmentInput) => unwrap<Appointment>(api.post("/v1/appointments/book", data)),
  reschedule: (id: string, newTimeSlotId: string) => api.post(`/v1/appointments/${id}/reschedule`, { newTimeSlotId }),
  cancel:  (id: string, reason?: string) => api.post(`/v1/appointments/${id}/cancel`, { reason }),
  cancelByDoctor: (id: string, reason?: string) => api.post(`/v1/appointments/${id}/cancel-by-doctor`, { reason }),
  confirm: (id: string) => api.post(`/v1/appointments/${id}/confirm`),
  complete:(id: string) => api.post(`/v1/appointments/${id}/complete`),
  noShow:  (id: string) => api.post(`/v1/appointments/${id}/no-show`),
  pay:     (id: string) => api.post(`/v1/appointments/${id}/pay`),
  refund:  (id: string) => api.post(`/v1/appointments/${id}/refund`),
  meetingLink: (id: string) => unwrap<{ url: string }>(api.post(`/v1/appointments/${id}/meeting-link`)),

  prescription: (id: string, data: { fileUrl: string; notes?: string; itemsJson?: string; validUntil?: string }) =>
    unwrap<{ id: string }>(api.post(`/v1/appointments/${id}/prescription`, data)),
  followUp: (id: string, notes: string) => api.post(`/v1/appointments/${id}/follow-up`, { notes }),
  myPrescriptions: () => unwrap<any[]>(api.get("/v1/appointments/my-prescriptions")),

  review: (id: string, rating: number, comment?: string) =>
    api.post(`/v1/appointments/${id}/review`, { rating, comment })
};
