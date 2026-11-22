import { api, apiBase } from "./client";

const V1 = "/v1";
const data = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

// ---------- Care plans ----------

export const carePlanItemTypes = ["Vaccination", "Medication", "Appointment", "Grooming", "ParasitePrevention", "LivestockTreatment"] as const;
export type CarePlanItemType = (typeof carePlanItemTypes)[number];
export type CarePlanItemStatus = "Pending" | "Done" | "Missed" | "Rescheduled";

export interface CarePlanTemplateItemInput { type: CarePlanItemType; title: string; offsetDays: number; repeatEveryDays?: number | null; repeatCount?: number | null; notes?: string | null }
export interface CarePlanTemplateItem extends CarePlanTemplateItemInput { id: string }
export interface CarePlanTemplateInput { title: string; species: string; breed?: string | null; description?: string | null; isPublished: boolean; items: CarePlanTemplateItemInput[] }
export interface CarePlanTemplate {
  id: string; title: string; species: string; breed?: string | null; description?: string | null; isPublished: boolean;
  publishedByUserId: string; isMine: boolean; createdAt: string; items: CarePlanTemplateItem[];
}
export interface CarePlanItem {
  id: string; planId: string; type: CarePlanItemType; title: string; dueDate: string; originalDueDate?: string | null;
  status: CarePlanItemStatus; isMissed: boolean; completedAt?: string | null; notes?: string | null;
  linkedRecordType?: string | null; linkedRecordId?: string | null; assignedUserId?: string | null; assignedUserName?: string | null;
}
export interface CarePlan {
  id: string; petId: string; templateId?: string | null; isCustom: boolean; title: string; status: "Active" | "Completed" | "Archived";
  startDate: string; pending: number; done: number; missed: number; items: CarePlanItem[];
}
export interface MissedCareSummary { missedCount: number; dueSoonCount: number; missed: CarePlanItem[]; dueSoon: CarePlanItem[] }
export interface CustomPlanItemInput { type: CarePlanItemType; title: string; dueDate: string; notes?: string | null; assignedUserId?: string | null }

export const carePlansApi = {
  templates: (species?: string, mine = false) => data<CarePlanTemplate[]>(api.get(`${V1}/care-plan-templates`, { params: { species: species || undefined, mine } })),
  createTemplate: (i: CarePlanTemplateInput) => data<CarePlanTemplate>(api.post(`${V1}/care-plan-templates`, i)),
  updateTemplate: (id: string, i: CarePlanTemplateInput) => data<CarePlanTemplate>(api.put(`${V1}/care-plan-templates/${id}`, i)),
  publish: (id: string, published: boolean) => api.post(`${V1}/care-plan-templates/${id}/publish`, null, { params: { published } }),
  deleteTemplate: (id: string) => api.delete(`${V1}/care-plan-templates/${id}`),

  plans: (petId: string) => data<CarePlan[]>(api.get(`${V1}/pets/${petId}/care-plans`)),
  summary: (petId: string) => data<MissedCareSummary>(api.get(`${V1}/pets/${petId}/care-plans/summary`)),
  createCustom: (petId: string, i: { title: string; startDate?: string | null; items: CustomPlanItemInput[] }) =>
    data<CarePlan>(api.post(`${V1}/pets/${petId}/care-plans`, i)),
  adopt: (petId: string, i: { templateId: string; startDate?: string | null; assignedUserId?: string | null }) =>
    data<CarePlan>(api.post(`${V1}/pets/${petId}/care-plans/adopt`, i)),
  addItem: (petId: string, planId: string, i: CustomPlanItemInput) => data<CarePlanItem>(api.post(`${V1}/pets/${petId}/care-plans/${planId}/items`, i)),
  archive: (petId: string, planId: string) => api.delete(`${V1}/pets/${petId}/care-plans/${planId}`),
  complete: (petId: string, itemId: string, i: { notes?: string | null; createRecord?: boolean }) =>
    data<CarePlanItem>(api.post(`${V1}/pets/${petId}/care-plan-items/${itemId}/complete`, { notes: i.notes ?? null, createRecord: !!i.createRecord, linkedRecordId: null, linkedRecordType: null })),
  reschedule: (petId: string, itemId: string, newDueDate: string, reason?: string) =>
    data<CarePlanItem>(api.post(`${V1}/pets/${petId}/care-plan-items/${itemId}/reschedule`, { newDueDate, reason: reason ?? null })),
  assign: (petId: string, itemId: string, userId: string | null) =>
    data<CarePlanItem>(api.post(`${V1}/pets/${petId}/care-plan-items/${itemId}/assign`, { userId })),
};

// ---------- Exposure / emergency ----------

export type ExposureStatus = "Reported" | "VetContacted" | "UnderObservation" | "Resolved";
export interface CommonSubstance { key: string; label: string; note: string }
export interface ExposureReport {
  id: string; petId: string; petName: string; substance: string; amountEstimate?: string | null; onsetAt?: string | null; exposedAt: string;
  symptoms?: string | null; status: ExposureStatus; escalated: boolean; createdAt: string; timeline: { at: string; kind: string; note?: string | null }[];
}
export interface Escalation {
  emergency: boolean; headline: string; message: string; callNowText: string; emergencyVetsLink: string; genericGuidance: string;
  disclaimer: string; doNow: string[]; doNot: string[]; report: ExposureReport;
}
export interface EmergencyCard {
  petName: string; species: string; breed?: string | null; weightKg?: number | null; birthDate?: string | null; microchipNumber?: string | null;
  allergies: string[]; activeMedications: string[]; conditions: string[]; vetName?: string | null; vetClinic?: string | null; vetPhone?: string | null;
  ownerName?: string | null; ownerPhone?: string | null; generatedAt: string; expiresAt?: string | null;
}
export interface EmergencyCardLink { id: string; token: string; viewPath: string; expiresAt: string }
/** Absolute public URL of the printable emergency card (works without login — encode as QR). */
export const emergencyCardUrl = (viewPath: string) => new URL(`${apiBase}${viewPath}`, window.location.origin).toString();

export const exposureApi = {
  substances: () => data<CommonSubstance[]>(api.get(`${V1}/pets/exposure-substances`)),
  list: (petId: string) => data<ExposureReport[]>(api.get(`${V1}/pets/${petId}/exposures`)),
  report: (petId: string, i: { substance: string; amountEstimate?: string | null; onsetAt?: string | null; exposedAt?: string | null; symptoms?: string | null }) =>
    data<Escalation>(api.post(`${V1}/pets/${petId}/exposures`, i)),
  update: (petId: string, id: string, i: { status?: ExposureStatus | null; note?: string | null }) =>
    data<ExposureReport>(api.post(`${V1}/pets/${petId}/exposures/${id}/update`, i)),
  card: (petId: string) => data<EmergencyCard>(api.get(`${V1}/pets/${petId}/emergency-card`)),
  createCardLink: (petId: string, validHours = 24) => data<EmergencyCardLink>(api.post(`${V1}/pets/${petId}/emergency-card/links`, null, { params: { validHours } })),
  revokeCardLinks: (petId: string) => api.delete(`${V1}/pets/${petId}/emergency-card/links`),
};

// ---------- Typed health records, history, labs, insights ----------

export type AllergyCategory = "Food" | "Drug" | "Environmental" | "Other";
export type AllergySeverity = "Mild" | "Moderate" | "Severe" | "LifeThreatening";
export interface Allergy { id: string; petId: string; petName?: string | null; allergen: string; category: AllergyCategory; severity: AllergySeverity; reaction?: string | null; notedAt: string; active: boolean }
export interface AllergyInput { allergen: string; category: AllergyCategory; severity: AllergySeverity; reaction?: string | null; notedAt?: string | null; active: boolean }
export interface DietEntry { id: string; petId: string; food: string; amount?: string | null; frequency?: string | null; notes?: string | null; active: boolean }
export interface DietEntryInput { food: string; amount?: string | null; frequency?: string | null; notes?: string | null; active: boolean }
export interface ImportFromTextResult { allergiesCreated: number; dietEntriesCreated: number; skipped: number }

export type LabFlag = "Unknown" | "Normal" | "Low" | "High" | "Critical";
export interface LabResult {
  id: string; petId: string; testName: string; value?: string | null; unit?: string | null; referenceRange?: string | null; flag: LabFlag;
  takenAt: string; documentUrl?: string | null; ocrText?: string | null; status: "Entered" | "OcrUnavailable" | "OcrExtracted" | "Transcribed";
  needsTranscription: boolean; statusNote?: string | null;
}
export interface LabResultInput { testName: string; value?: string | null; unit?: string | null; referenceRange?: string | null; flag?: LabFlag | null; takenAt: string; documentUrl?: string | null }

export type HealthRecordType = "Vaccination" | "Medical";
export interface RecordVersion { version: number; changedAt: string; changedByUserId: string; changedByName?: string | null; changeReason?: string | null; snapshot: Record<string, string | null> }
export interface RecordSeal { recordType: HealthRecordType; recordId: string; sealedByName?: string | null; sealedAt: string; signatureHash: string; signatureValid: boolean; changedSinceSeal: boolean }
export interface RecordHistory { recordType: HealthRecordType; recordId: string; sealed: boolean; seal?: RecordSeal | null; versions: RecordVersion[] }
export interface SealState { recordId: string; recordType: HealthRecordType; sealedAt: string; sealedByName?: string | null }

export interface PetInsight { kind: string; severity: "Info" | "Watch" | "Alert"; title: string; detail: string; metric?: string | null }
export interface PetInsights { petId: string; generatedAt: string; insights: PetInsight[]; disclaimer: string }

export interface CertificateIssue { id: string; token: string; verifyPath: string; certificatePath: string; issuedAt: string }

export const healthPlusApi = {
  allergies: (petId: string, q?: string, active?: boolean) => data<Allergy[]>(api.get(`${V1}/pets/${petId}/allergies`, { params: { q: q || undefined, active } })),
  searchAllergies: (q: string) => data<Allergy[]>(api.get(`${V1}/pets/allergies/search`, { params: { q } })),
  addAllergy: (petId: string, i: AllergyInput) => data<Allergy>(api.post(`${V1}/pets/${petId}/allergies`, i)),
  updateAllergy: (petId: string, id: string, i: AllergyInput) => api.put(`${V1}/pets/${petId}/allergies/${id}`, i),
  deleteAllergy: (petId: string, id: string) => api.delete(`${V1}/pets/${petId}/allergies/${id}`),
  diet: (petId: string, q?: string) => data<DietEntry[]>(api.get(`${V1}/pets/${petId}/diet`, { params: { q: q || undefined } })),
  addDiet: (petId: string, i: DietEntryInput) => data<DietEntry>(api.post(`${V1}/pets/${petId}/diet`, i)),
  updateDiet: (petId: string, id: string, i: DietEntryInput) => api.put(`${V1}/pets/${petId}/diet/${id}`, i),
  deleteDiet: (petId: string, id: string) => api.delete(`${V1}/pets/${petId}/diet/${id}`),
  importText: (petId: string) => data<ImportFromTextResult>(api.post(`${V1}/pets/${petId}/health-text/import`)),

  labs: (petId: string) => data<LabResult[]>(api.get(`${V1}/pets/${petId}/labs`)),
  addLab: (petId: string, i: LabResultInput) => data<LabResult>(api.post(`${V1}/pets/${petId}/labs`, i)),
  updateLab: (petId: string, id: string, i: LabResultInput) => data<LabResult>(api.put(`${V1}/pets/${petId}/labs/${id}`, i)),
  deleteLab: (petId: string, id: string) => api.delete(`${V1}/pets/${petId}/labs/${id}`),

  seals: (petId: string) => data<SealState[]>(api.get(`${V1}/pets/${petId}/record-seals`)),
  history: (petId: string, type: HealthRecordType, id: string) => data<RecordHistory>(api.get(`${V1}/pets/${petId}/records/${type}/${id}/history`)),
  seal: (petId: string, type: HealthRecordType, id: string) => data<RecordSeal>(api.post(`${V1}/pets/${petId}/records/${type}/${id}/seal`)),
  amend: (petId: string, type: HealthRecordType, id: string, body: { reason: string; vaccination?: unknown; medical?: unknown }) =>
    api.post(`${V1}/pets/${petId}/records/${type}/${id}/amend`, body),

  insights: (petId: string) => data<PetInsights>(api.get(`${V1}/pets/${petId}/insights`)),
  issueCertificate: (petId: string) => data<CertificateIssue>(api.post(`${V1}/pets/${petId}/vaccination-certificates`)),
  fhirExportUrl: (petId: string) => `${apiBase}${V1}/pets/${petId}/export/fhir`,
  /** Downloads the FHIR Bundle with the auth header and saves it as a file. */
  downloadFhir: async (petId: string, name: string) => {
    const r = await api.get(`${V1}/pets/${petId}/export/fhir`, { responseType: "blob" });
    const url = URL.createObjectURL(r.data as Blob);
    const a = document.createElement("a");
    a.href = url; a.download = `${name}-fhir.json`; a.click();
    URL.revokeObjectURL(url);
  },
};

// ---------- Herd treatments ----------

export interface LivestockTreatment {
  id: string; batchId: string; petId: string; petName: string; petGroupId?: string | null; diagnosis: string; treatment: string;
  administeredOn: string; meatWithdrawalDays: number; milkWithdrawalDays: number; meatSafeOn?: string | null; milkSafeOn?: string | null; notes?: string | null;
}
export interface WithdrawalStatus { petId: string; petName: string; groupId?: string | null; meatSafeOn?: string | null; milkSafeOn?: string | null; meatBlocked: boolean; milkBlocked: boolean }
export interface LivestockTreatmentInput {
  petId?: string | null; petGroupId?: string | null; diagnosis: string; treatment: string; administeredOn: string;
  meatWithdrawalDays: number; milkWithdrawalDays: number; notes?: string | null;
}
export const livestockHealthApi = {
  list: (groupId?: string) => data<LivestockTreatment[]>(api.get(`${V1}/livestock/treatments`, { params: { groupId } })),
  withdrawal: (groupId?: string) => data<WithdrawalStatus[]>(api.get(`${V1}/livestock/treatments/withdrawal`, { params: { groupId } })),
  add: (i: LivestockTreatmentInput) => data<{ animals: number; batchId: string }>(api.post(`${V1}/livestock/treatments`, i)),
  remove: (id: string) => api.delete(`${V1}/livestock/treatments/${id}`),
};
