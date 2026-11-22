import { api } from "./client";
import { withIdempotency } from "@/lib/idempotency";

// Responses may or may not be wrapped in { data }; accept both (same convention as api/vets.ts).
const unwrap = async <T,>(p: Promise<{ data: any }>): Promise<T> => {
  const r = (await p).data;
  return (r && typeof r === "object" && "data" in r && Object.keys(r).length <= 2 ? r.data : r) as T;
};

// ---------- Emergency ----------
export interface EmergencyProfile {
  is24x7: boolean; emergencyPhone?: string | null; clinicName?: string | null; clinicAddress?: string | null; city?: string | null;
  latitude?: number | null; longitude?: number | null; acceptsWalkInEmergency: boolean; updatedAt?: string | null;
}
export type EmergencyProfileInput = Omit<EmergencyProfile, "updatedAt">;
export interface EmergencyVet {
  doctorId: string; doctorName: string; clinicName?: string | null; clinicAddress?: string | null; city?: string | null;
  is24x7: boolean; acceptsWalkInEmergency: boolean; emergencyPhone?: string | null; telUri?: string | null;
  distanceKm?: number | null; cityMatch: boolean; isOnline: boolean;
}
export const emergencyApi = {
  mine: () => unwrap<EmergencyProfile>(api.get("/v1/vets/me/emergency-profile", { skipErrorToast: true })).catch(() => null),
  save: (i: EmergencyProfileInput) => unwrap<EmergencyProfile>(api.put("/v1/vets/me/emergency-profile", i)),
  search: (p: { lat?: number; lng?: number; city?: string; animalType?: string }) =>
    unwrap<EmergencyVet[]>(api.get("/v1/vets/emergency", { params: p }))
};

// ---------- Booking policy / deposits ----------
export interface BookingPolicy {
  depositPercent?: number | null; depositFixed?: number | null; cancellationWindowHours: number;
  lateCancelFeePercent: number; noShowFeePercent: number; allowRescheduleHours: number; requiresQuestionnaire: boolean;
}
export interface CancellationQuote {
  appointmentId: string; isLate: boolean; windowHours: number; feePercent: number; fee: number; forfeited: number;
  refund: number; outstanding: number; scheduledAt: string;
}
export interface CancellationOutcome {
  appointmentId: string; isLate: boolean; fee: number; forfeited: number; refund: number; outstanding: number; depositStatus?: string | null;
}
export interface Deposit {
  appointmentId: string; amount: number; status: "Pending" | "Paid" | "Refunded" | "Forfeited"; feeAmount: number;
  refundAmount: number; note?: string | null; resolvedAt?: string | null;
}
export const policyApi = {
  mine: () => unwrap<BookingPolicy>(api.get("/v1/vets/me/booking-policy", { skipErrorToast: true })).catch(() => null),
  save: (i: BookingPolicy) => unwrap<BookingPolicy>(api.put("/v1/vets/me/booking-policy", i)),
  forDoctor: (id: string) => unwrap<BookingPolicy>(api.get(`/v1/vets/${id}/booking-policy`, { skipErrorToast: true })).catch(() => null),
  deposit: (appointmentId: string) => unwrap<Deposit>(api.get(`/v1/appointments/${appointmentId}/deposit`, { skipErrorToast: true })).catch(() => null),
  quote: (appointmentId: string) => unwrap<CancellationQuote>(api.get(`/v1/appointments/${appointmentId}/cancellation-quote`)),
  outcome: (appointmentId: string) => unwrap<CancellationOutcome>(api.get(`/v1/appointments/${appointmentId}/cancellation-outcome`, { skipErrorToast: true })).catch(() => null)
};

// ---------- Follow-ups ----------
export interface FollowUp {
  id: string; sourceAppointmentId: string; doctorId: string; doctorName: string; petId?: string | null; petName?: string | null;
  inDays: number; dueDate: string; note?: string | null; status: string; followUpAppointmentId?: string | null;
}
export const followUpApi = {
  request: (appointmentId: string, inDays: number, note?: string) =>
    unwrap<FollowUp>(api.post(`/v1/appointments/${appointmentId}/follow-ups/request`, { inDays, note })),
  mine: () => unwrap<FollowUp[]>(api.get("/v1/follow-ups/mine")),
  prefill: (appointmentId: string) => unwrap<FollowUp>(api.get(`/v1/appointments/${appointmentId}/follow-ups/prefill`)),
  book: (appointmentId: string, timeSlotId: string, symptoms?: string) =>
    unwrap<unknown>(api.post(`/v1/appointments/${appointmentId}/follow-ups/book`, { timeSlotId, symptoms }, withIdempotency())),
  dismiss: (id: string) => api.post(`/v1/follow-ups/${id}/dismiss`)
};

// ---------- Doctor switching ----------
export interface SwitchCandidate {
  doctorId: string; doctorName: string; specialty?: string | null; fee: number; ratingAverage: number;
  timeSlotId: string; startUtc: string; endUtc: string;
}
export const switchApi = {
  candidates: (appointmentId: string) => unwrap<SwitchCandidate[]>(api.get(`/v1/appointments/${appointmentId}/switch-candidates`)),
  switch: (appointmentId: string, newDoctorId: string, newTimeSlotId: string) =>
    unwrap<unknown>(api.post(`/v1/appointments/${appointmentId}/switch-doctor`, { newDoctorId, newTimeSlotId }, withIdempotency()))
};

// ---------- Calendar shares ----------
export interface CalendarShare {
  id: string; scope: "Patient" | "Doctor"; label?: string | null; tokenHint: string; createdAt: string;
  expiresAt?: string | null; revokedAt?: string | null; lastAccessedAt?: string | null; feedPath?: string | null;
}
export const calendarShareApi = {
  list: () => unwrap<CalendarShare[]>(api.get("/v1/calendar/shares")),
  create: (scope: "Patient" | "Doctor", label?: string, expiresInDays?: number) =>
    unwrap<CalendarShare>(api.post("/v1/calendar/shares", { scope, label, expiresInDays })),
  revoke: (id: string) => api.delete(`/v1/calendar/shares/${id}`)
};

// ---------- Questionnaires ----------
export type QuestionType = "text" | "yesno" | "choice" | "number";
export interface Question { id: string; type: QuestionType; label: string; required: boolean; options?: string[] | null; }
export interface QuestionnaireTemplate { id: string; title: string; questions: Question[]; isActive: boolean; }
export interface AppointmentQuestionnaire {
  appointmentId: string; template?: QuestionnaireTemplate | null; answers?: Record<string, string> | null;
  submittedAt?: string | null; required: boolean;
}
export const questionnaireApi = {
  mine: () => unwrap<QuestionnaireTemplate>(api.get("/v1/vets/me/questionnaire", { skipErrorToast: true })).catch(() => null),
  save: (i: { title: string; questions: Question[]; isActive: boolean }) => unwrap<QuestionnaireTemplate>(api.put("/v1/vets/me/questionnaire", i)),
  forAppointment: (id: string) => unwrap<AppointmentQuestionnaire>(api.get(`/v1/appointments/${id}/questionnaire`)),
  submit: (id: string, answers: Record<string, string>) => unwrap<AppointmentQuestionnaire>(api.post(`/v1/appointments/${id}/questionnaire`, { answers }))
};

// ---------- Clinical encounter ----------
export interface DiagnosisCode { code: string; system: "VeNom" | "SNOMED" | "Other"; label?: string | null; }
export interface LabResult { id?: string | null; test: string; value?: string | null; unit?: string | null; referenceRange?: string | null; flag?: string | null; documentUrl?: string | null; }
export interface Referral {
  id: string; encounterId: string; toDoctorId?: string | null; toDoctorName?: string | null; externalClinic?: string | null; reason: string;
  status: string; consentToShareRecords: boolean; createdAt: string; petName?: string | null; petId?: string | null; fromDoctorName?: string | null;
}
export interface Encounter {
  id?: string | null; appointmentId: string; petId?: string | null; status: "Draft" | "Finalized" | "Amended";
  chiefComplaint?: string | null; findings?: string | null; diagnosis?: string | null; treatmentPlan?: string | null;
  diagnosisCodes: DiagnosisCode[]; labResults: LabResult[]; version: number; finalizedAt?: string | null; signatureHash?: string | null;
  amendments: { version: number; reason: string; amendedAt: string; signatureHash: string }[];
  referrals: Referral[]; isDoctor: boolean; updatedAt?: string | null;
}
export type EncounterDraft = Pick<Encounter, "chiefComplaint" | "findings" | "diagnosis" | "treatmentPlan" | "diagnosisCodes" | "labResults">;
export const encounterApi = {
  get: (id: string) => unwrap<Encounter>(api.get(`/v1/appointments/${id}/encounter`)),
  saveDraft: (id: string, d: EncounterDraft) => unwrap<Encounter>(api.put(`/v1/appointments/${id}/encounter`, d, { skipErrorToast: true })),
  finalize: (id: string) => unwrap<Encounter>(api.post(`/v1/appointments/${id}/encounter/finalize`)),
  amend: (id: string, reason: string, content: EncounterDraft) => unwrap<Encounter>(api.post(`/v1/appointments/${id}/encounter/amend`, { reason, content })),
  refer: (id: string, i: { toDoctorId?: string | null; externalClinic?: string | null; reason: string; consentToShareRecords: boolean }) =>
    unwrap<Referral>(api.post(`/v1/appointments/${id}/encounter/referrals`, i)),
  incoming: () => unwrap<Referral[]>(api.get("/v1/referrals/incoming")),
  respond: (id: string, accept: boolean, quiet = false) =>
    unwrap<Referral>(api.post(`/v1/referrals/${id}/respond`, { accept }, quiet ? { skipErrorToast: true } : undefined)),
  complete: (id: string, overrideReason?: string) => api.post(`/v1/appointments/${id}/complete`, overrideReason ? { overrideReason } : undefined)
};

// ---------- Analytics ----------
export interface AnalyticsPoint { period: string; revenue: number; appointments: number; completed: number; noShows: number; cancelled: number; }
export interface ClinicDoctorRow { doctorId: string; doctorName: string; revenue: number; appointments: number; noShowRate: number; }
export interface ClinicAnalytics {
  from: string; to: string; granularity: string; totalRevenue: number; totalAppointments: number; completed: number; noShows: number;
  cancelled: number; noShowRate: number; capacityUtilization: number; slotsOffered: number; slotsBooked: number;
  return90Rate: number; return90Patients: number; return180Rate: number; return180Patients: number; uniquePatients: number;
  series: AnalyticsPoint[];
  clinicDoctors?: ClinicDoctorRow[] | null;
}
export const analyticsApi = {
  mine: (p: { from?: string; to?: string; granularity?: string } = {}) => unwrap<ClinicAnalytics>(api.get("/v1/vets/me/analytics", { params: p }))
};

// ---------- Record-sharing consents (owner side) ----------
export type ConsentScope = "Summary" | "FullRecord";
export interface RecordConsent {
  id: string; petId: string; granteeDoctorId: string; granteeName: string; scope: ConsentScope; expiresAt: string; revokedAt?: string | null;
}
export const consentApi = {
  list: (petId: string) => unwrap<RecordConsent[]>(api.get("/v1/record-consents", { params: { petId } })),
  grant: (i: { petId: string; granteeDoctorId: string; scope: ConsentScope; expiresInDays: number }) =>
    unwrap<RecordConsent>(api.post("/v1/record-consents", i)),
  revoke: (id: string) => api.delete(`/v1/record-consents/${id}`)
};
