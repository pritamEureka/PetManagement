import { api } from "./client";

// ---------- Telemedicine ----------

export type RoomState = "NotOpen" | "Open" | "Closed";

export interface TelemedicineRoom {
  appointmentId: string;
  state: RoomState;
  joinUrl?: string | null;
  opensAt: string;
  expiresAt: string;
  consentGiven: boolean;
  preConsultNote?: string | null;
  checkedInAt?: string | null;
  admittedAt?: string | null;
  isDoctor: boolean;
  patientName: string;
  doctorName: string;
}

export interface WaitingPatient {
  appointmentId: string;
  patientName: string;
  petName?: string | null;
  scheduledAt: string;
  checkedInAt: string;
  preConsultNote?: string | null;
}

export interface ConsultationNote {
  appointmentId: string;
  subjective?: string | null;
  objective?: string | null;
  assessment?: string | null;
  plan?: string | null;
  updatedAt?: string | null;
}

export type ConsultationNoteInput = Omit<ConsultationNote, "appointmentId" | "updatedAt">;

export const telemedicineApi = {
  room: async (id: string) => (await api.get<TelemedicineRoom>(`/v1/telemedicine/appointments/${id}/room`)).data,
  checkIn: async (id: string, consent: boolean, preConsultNote?: string) =>
    (await api.post<TelemedicineRoom>(`/v1/telemedicine/appointments/${id}/check-in`, { consent, preConsultNote })).data,
  admit: (id: string) => api.post(`/v1/telemedicine/appointments/${id}/admit`),
  event: (id: string, kind: "Join" | "Leave") => api.post(`/v1/telemedicine/appointments/${id}/events`, { kind }),
  waiting: async () => (await api.get<WaitingPatient[]>("/v1/telemedicine/waiting-room")).data,
  getNote: async (id: string) => (await api.get<ConsultationNote>(`/v1/telemedicine/appointments/${id}/notes`)).data,
  saveNote: async (id: string, input: ConsultationNoteInput) =>
    (await api.put<ConsultationNote>(`/v1/telemedicine/appointments/${id}/notes`, input)).data
};

// ---------- Waitlist ----------

export type WaitlistStatus = "Waiting" | "Notified" | "Fulfilled" | "Cancelled";

export interface WaitlistEntry {
  id: string;
  doctorId: string;
  doctorName: string;
  status: WaitlistStatus;
  position: number;
  preferredFrom?: string | null;
  preferredTo?: string | null;
  consultationType?: "Online" | "Offline" | "Both" | null;
  note?: string | null;
  createdAt: string;
  notifiedAt?: string | null;
}

export interface JoinWaitlistInput {
  doctorId: string;
  petId?: string | null;
  preferredFrom?: string | null;
  preferredTo?: string | null;
  consultationType?: "Online" | "Offline" | "Both" | null;
  note?: string | null;
}

export const waitlistApi = {
  join: async (input: JoinWaitlistInput) => (await api.post<WaitlistEntry>("/v1/waitlists", input)).data,
  mine: async () => (await api.get<WaitlistEntry[]>("/v1/waitlists/mine")).data,
  leave: (id: string) => api.delete(`/v1/waitlists/${id}`)
};

// ---------- Recurring ----------

export type Recurrence = "Weekly" | "Biweekly" | "Monthly";

export interface BookSeriesInput {
  doctorId: string;
  timeSlotId: string;
  petId?: string | null;
  type: "Online" | "Offline";
  symptoms?: string | null;
  frequency: Recurrence;
  occurrences: number;
}

export interface BookSeriesResult {
  seriesId?: string | null;
  booked: { sequence: number; appointmentId: string; scheduledAt: string }[];
  failed: { sequence: number; desiredStartUtc: string; reason: string }[];
}

export const seriesApi = {
  book: async (input: BookSeriesInput) => (await api.post<BookSeriesResult>("/v1/appointment-series", input)).data
};

// ---------- Calendar ----------

async function downloadIcs(url: string, filename: string) {
  const res = await api.get<Blob>(url, { responseType: "blob" });
  const blobUrl = URL.createObjectURL(new Blob([res.data], { type: "text/calendar" }));
  const a = document.createElement("a");
  a.href = blobUrl;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(blobUrl);
}

export const calendarApi = {
  downloadAppointment: (id: string) => downloadIcs(`/v1/calendar/appointments/${id}.ics`, `appointment-${id}.ics`),
  downloadUpcoming: () => downloadIcs("/v1/calendar/upcoming.ics", "pawzaroo-upcoming.ics")
};
