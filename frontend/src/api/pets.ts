import { api } from "./client";
import type { PetInput } from "@/lib/schemas";

const V1 = "/v1/pets";
const data = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

// ---------- Types ----------

export type PetAccessLevel = "None" | "Vet" | "Viewer" | "Caretaker" | "CoOwner" | "Owner";
export const accessRank: Record<PetAccessLevel, number> = { None: 0, Vet: 1, Viewer: 2, Caretaker: 3, CoOwner: 4, Owner: 5 };
export const canLogCare = (a?: PetAccessLevel) => !!a && accessRank[a] >= accessRank.Caretaker;
export const canEditPet = (a?: PetAccessLevel) => !!a && accessRank[a] >= accessRank.CoOwner;
export const canEditClinical = (a?: PetAccessLevel) => a === "Vet" || canEditPet(a);

export interface Pet {
  id: string;
  ownerId: string;
  name: string;
  animalType: string;
  breed?: string | null;
  gender: string;
  birthDate?: string | null;
  weightKg?: number | null;
  color?: string | null;
  tagNumber?: string | null;
  primaryPhotoUrl?: string | null;
  allergies?: string | null;
  dietNotes?: string | null;
  isAvailableForAdoption: boolean;
  size?: string | null;
  microchipNumber?: string | null;
  isLost: boolean;
  groupId?: string | null;
  groupName?: string | null;
  myAccess: PetAccessLevel;
  overdueVaccinations: number;
  nextVaccinationDue?: string | null;
  createdAt: string;
}

export interface PetRef { id: string; name: string; breed?: string | null; primaryPhotoUrl?: string | null; }
export interface PetPhoto { id: string; url: string; caption?: string | null; uploadedAt: string; }
export interface Vaccination {
  id: string; vaccineName: string; administeredOn: string; nextDueOn?: string | null;
  administeredByVet?: string | null; notes?: string | null; certificateUrl?: string | null;
}
export interface MedicalRecord {
  id: string; appointmentId?: string | null; visitDate: string; diagnosis?: string | null; treatment?: string | null;
  prescriptionFileUrl?: string | null; notes?: string | null; vetName?: string | null; attachmentUrl?: string | null;
}
export interface GroomingRecord {
  id: string; performedOn: string; serviceType: string; provider?: string | null; notes?: string | null;
  cost?: number | null; serviceBookingId?: string | null;
}

export interface PetDetail extends Omit<Pet, "overdueVaccinations" | "nextVaccinationDue"> {
  ownerName: string;
  publicCode?: string | null;
  publicProfileEnabled: boolean;
  sire?: PetRef | null;
  dam?: PetRef | null;
  photos: PetPhoto[];
  vaccinations: Vaccination[];
  medicalRecords: MedicalRecord[];
  groomingRecords: GroomingRecord[];
}

export interface VaccinationInput {
  vaccineName: string; administeredOn: string; nextDueOn?: string | null;
  administeredByVet?: string | null; notes?: string | null; certificateUrl?: string | null;
}
export interface MedicalRecordInput {
  visitDate: string; diagnosis?: string | null; treatment?: string | null; notes?: string | null;
  vetName?: string | null; attachmentUrl?: string | null; prescriptionFileUrl?: string | null;
}
export interface GroomingRecordInput {
  performedOn: string; serviceType: string; provider?: string | null; notes?: string | null; cost?: number | null;
}

export interface VaccinePlanItem { vaccineName: string; ageWeeks: number; notes?: string | null; }
export interface VaccineScheduleItem {
  vaccineName: string; ageWeeks: number; dueOn?: string | null;
  status: "given" | "overdue" | "due" | "upcoming" | "unknown"; givenOn?: string | null; notes?: string | null;
}

export interface WeightLog { id: string; recordedOn: string; weightKg: number; notes?: string | null; }
export interface WeightHistory { entries: WeightLog[]; healthyRange?: { minKg: number; maxKg: number; basis: string } | null; }

export interface Medication {
  id: string; petId: string; prescriptionId?: string | null; drugName: string; dose?: string | null; doseTimes: string;
  startDate: string; endDate?: string | null; instructions?: string | null; isActive: boolean;
  dosesTaken: number; dosesSkipped: number;
}
export interface MedicationInput {
  drugName: string; dose?: string | null; doseTimes: string; startDate: string; endDate?: string | null;
  instructions?: string | null; isActive: boolean;
}
export interface TodayDose {
  medicationId: string; petId: string; petName: string; drugName: string; dose?: string | null;
  scheduledFor: string; status: "taken" | "skipped" | "due" | "upcoming";
}

export type CaretakerRole = "Viewer" | "Caretaker" | "CoOwner";
export interface Caretaker {
  id: string; userId: string; displayName: string; email: string; avatarUrl?: string | null;
  role: CaretakerRole; expiresAt?: string | null; createdAt: string;
}

export const careActivityTypes = ["Feeding", "Walk", "Medication", "Litter", "Bath", "Play", "Training", "Other"] as const;
export type CareActivityType = (typeof careActivityTypes)[number];
export interface CareRoutine { id: string; activityType: CareActivityType; title: string; timeOfDay?: string | null; details?: string | null; isActive: boolean; }
export interface CareRoutineInput { activityType: CareActivityType; title: string; timeOfDay?: string | null; details?: string | null; isActive: boolean; }
export interface CareLog {
  id: string; activityType: CareActivityType; routineId?: string | null; performedAt: string;
  performedByUserId: string; performedByName: string; notes?: string | null;
}
export interface CareToday {
  checklist: { routine: CareRoutine; done: boolean; doneAt?: string | null; doneBy?: string | null }[];
  streakDays: number;
  recentLogs: CareLog[];
}

export const expenseCategories = ["Food", "Vet", "Grooming", "Medicine", "Accessories", "Insurance", "Services", "Training", "Other"] as const;
export type ExpenseCategory = (typeof expenseCategories)[number];
export interface Expense {
  id?: string | null; petId: string; petName: string; category: ExpenseCategory; amount: number;
  spentOn: string; description?: string | null; source: "manual" | "appointment" | "service" | "grooming";
}
export interface ExpenseSummary {
  total: number;
  months: { month: string; total: number; byCategory: Record<string, number> }[];
  byCategory: Record<string, number>;
  byPet: Record<string, number>;
  recent: Expense[];
}
export interface InsurancePolicy {
  id: string; provider: string; policyNumber: string; coverageSummary?: string | null; coverageLimit?: number | null;
  premium?: number | null; startDate: string; endDate?: string | null; documentUrl?: string | null; isActive: boolean;
}
export type InsurancePolicyInput = Omit<InsurancePolicy, "id" | "isActive">;

export interface ShareLink { id: string; token: string; label?: string | null; expiresAt: string; revokedAt?: string | null; createdAt: string; }

export interface LineageNode {
  id: string; name: string; breed?: string | null; gender: string; birthDate?: string | null;
  sire?: LineageNode | null; dam?: LineageNode | null;
}
export interface Lineage { root: LineageNode; offspring: PetRef[]; }

export interface UpcomingCareItem {
  kind: "vaccination" | "medication" | "birthday" | "breeding";
  petId: string; petName: string; title: string; dueOn: string; overdue: boolean; url: string;
}

export interface Recommendations {
  lifeStage: string;
  careTips: string[];
  suggestedVaccines: VaccinePlanItem[];
  products: { id: string; name: string; price: number; discountPrice?: number | null; imageUrl?: string | null; reason: string }[];
}

export interface PublicPet {
  code: string; name: string; animalType: string; breed?: string | null; gender: string; color?: string | null;
  primaryPhotoUrl?: string | null; allergies?: string | null; isLost: boolean; ownerFirstName: string; openLostReportId?: string | null;
}

/** Browser's offset in minutes east of UTC — the API's "today" math uses it. */
export const tzOffset = () => -new Date().getTimezoneOffset();

/** Absolute URL of a public, token-protected health record (opens without login). */
export const sharedHealthUrl = (token: string) => `${window.location.origin}/api/v1/public/health/${token}/report`;
export const publicPetUrl = (code: string) => `${window.location.origin}/p/${code}`;

// ---------- API ----------

export const petsApi = {
  mine: () => data<Pet[]>(api.get(`${V1}/mine`)),
  get: (id: string) => data<PetDetail>(api.get(`${V1}/${id}`)),
  create: (input: PetInput) => data<PetDetail>(api.post(V1, input)),
  update: (id: string, input: PetInput) => api.put(`${V1}/${id}`, input),
  remove: (id: string) => api.delete(`${V1}/${id}`),

  upcomingCare: (days = 30) => data<UpcomingCareItem[]>(api.get(`${V1}/upcoming-care`, { params: { days } })),
  vaccinePlan: (animalType: string) => data<VaccinePlanItem[]>(api.get(`${V1}/vaccine-plans/${animalType}`)),
  vaccineSchedule: (id: string) => data<VaccineScheduleItem[]>(api.get(`${V1}/${id}/vaccine-schedule`)),
  recommendations: (id: string) => data<Recommendations>(api.get(`${V1}/${id}/recommendations`)),
  lineage: (id: string, generations = 3) => data<Lineage>(api.get(`${V1}/${id}/lineage`, { params: { generations } })),

  photos: {
    add: (id: string, input: { url: string; caption?: string; makePrimary?: boolean }) => data<PetPhoto>(api.post(`${V1}/${id}/photos`, input)),
    setPrimary: (id: string, photoId: string) => api.post(`${V1}/${id}/photos/${photoId}/primary`),
    remove: (id: string, photoId: string) => api.delete(`${V1}/${id}/photos/${photoId}`),
  },
  vaccinations: {
    add: (id: string, input: VaccinationInput) => data<Vaccination>(api.post(`${V1}/${id}/vaccinations`, input)),
    update: (id: string, rid: string, input: VaccinationInput) => api.put(`${V1}/${id}/vaccinations/${rid}`, input),
    remove: (id: string, rid: string) => api.delete(`${V1}/${id}/vaccinations/${rid}`),
  },
  medical: {
    add: (id: string, input: MedicalRecordInput) => data<MedicalRecord>(api.post(`${V1}/${id}/medical-records`, input)),
    update: (id: string, rid: string, input: MedicalRecordInput) => api.put(`${V1}/${id}/medical-records/${rid}`, input),
    remove: (id: string, rid: string) => api.delete(`${V1}/${id}/medical-records/${rid}`),
  },
  grooming: {
    add: (id: string, input: GroomingRecordInput) => data<GroomingRecord>(api.post(`${V1}/${id}/grooming-records`, input)),
    update: (id: string, rid: string, input: GroomingRecordInput) => api.put(`${V1}/${id}/grooming-records/${rid}`, input),
    remove: (id: string, rid: string) => api.delete(`${V1}/${id}/grooming-records/${rid}`),
  },
  weights: {
    list: (id: string) => data<WeightHistory>(api.get(`${V1}/${id}/weights`)),
    add: (id: string, input: { recordedOn: string; weightKg: number; notes?: string }) => data<WeightLog>(api.post(`${V1}/${id}/weights`, input)),
    remove: (id: string, logId: string) => api.delete(`${V1}/${id}/weights/${logId}`),
  },
  medications: {
    today: () => data<TodayDose[]>(api.get(`${V1}/medications/today`, { params: { tzOffset: tzOffset() } })),
    list: (id: string) => data<Medication[]>(api.get(`${V1}/${id}/medications`)),
    add: (id: string, input: MedicationInput) => data<Medication>(api.post(`${V1}/${id}/medications`, input)),
    update: (id: string, mid: string, input: MedicationInput) => api.put(`${V1}/${id}/medications/${mid}`, input),
    remove: (id: string, mid: string) => api.delete(`${V1}/${id}/medications/${mid}`),
    logDose: (id: string, mid: string, input: { scheduledFor: string; skipped: boolean; notes?: string }) =>
      api.post(`${V1}/${id}/medications/${mid}/doses`, input),
  },
  caretakers: {
    list: (id: string) => data<Caretaker[]>(api.get(`${V1}/${id}/caretakers`)),
    invite: (id: string, input: { email: string; role: CaretakerRole; expiresAt?: string | null }) =>
      data<Caretaker>(api.post(`${V1}/${id}/caretakers`, input, { skipErrorToast: true })),
    update: (id: string, cid: string, input: { role: CaretakerRole; expiresAt?: string | null }) => api.put(`${V1}/${id}/caretakers/${cid}`, input),
    remove: (id: string, cid: string) => api.delete(`${V1}/${id}/caretakers/${cid}`),
  },
  care: {
    today: (id: string) => data<CareToday>(api.get(`${V1}/${id}/care/today`, { params: { tzOffset: tzOffset() } })),
    routines: (id: string) => data<CareRoutine[]>(api.get(`${V1}/${id}/routines`)),
    addRoutine: (id: string, input: CareRoutineInput) => data<CareRoutine>(api.post(`${V1}/${id}/routines`, input)),
    updateRoutine: (id: string, rid: string, input: CareRoutineInput) => api.put(`${V1}/${id}/routines/${rid}`, input),
    removeRoutine: (id: string, rid: string) => api.delete(`${V1}/${id}/routines/${rid}`),
    log: (id: string, input: { activityType: CareActivityType; routineId?: string | null; performedAt?: string | null; notes?: string | null }) =>
      data<CareLog>(api.post(`${V1}/${id}/care-logs`, input)),
    removeLog: (id: string, logId: string) => api.delete(`${V1}/${id}/care-logs/${logId}`),
  },
  expenses: {
    summary: (months = 6, petId?: string) => data<ExpenseSummary>(api.get(`${V1}/expenses/summary`, { params: { months, petId } })),
    list: (id: string) => data<Expense[]>(api.get(`${V1}/${id}/expenses`)),
    add: (id: string, input: { category: ExpenseCategory; amount: number; spentOn: string; description?: string | null }) =>
      data<Expense>(api.post(`${V1}/${id}/expenses`, input)),
    remove: (id: string, eid: string) => api.delete(`${V1}/${id}/expenses/${eid}`),
  },
  insurance: {
    list: (id: string) => data<InsurancePolicy[]>(api.get(`${V1}/${id}/insurance`)),
    add: (id: string, input: InsurancePolicyInput) => data<InsurancePolicy>(api.post(`${V1}/${id}/insurance`, input)),
    update: (id: string, pid: string, input: InsurancePolicyInput) => api.put(`${V1}/${id}/insurance/${pid}`, input),
    remove: (id: string, pid: string) => api.delete(`${V1}/${id}/insurance/${pid}`),
  },
  sharing: {
    links: (id: string) => data<ShareLink[]>(api.get(`${V1}/${id}/share-links`)),
    createLink: (id: string, input: { validDays: number; label?: string }) => data<ShareLink>(api.post(`${V1}/${id}/share-links`, input)),
    revokeLink: (id: string, linkId: string) => api.delete(`${V1}/${id}/share-links/${linkId}`),
    setPublicProfile: (id: string, enabled: boolean) =>
      data<{ enabled: boolean; code?: string | null }>(api.put(`${V1}/${id}/public-profile`, { enabled })),
    /** Fetches the printable record with auth and opens it in a new tab. */
    openHealthReport: async (id: string) => {
      const win = window.open("", "_blank");
      const html = await api.get<string>(`${V1}/${id}/health-report`, { responseType: "text" }).then((r) => r.data);
      const url = URL.createObjectURL(new Blob([html], { type: "text/html" }));
      if (win) win.location.href = url; else window.open(url, "_blank");
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    },
  },
};

export const publicPetsApi = {
  get: (code: string) => data<PublicPet>(api.get(`/v1/public/pets/${code}`, { skipErrorToast: true })),
  sighting: (code: string, input: { message?: string; contactInfo?: string; latitude?: number; longitude?: number }) =>
    api.post(`/v1/public/pets/${code}/sightings`, input),
};
