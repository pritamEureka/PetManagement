import { api } from "./client";

export interface AssistantCitation { sourceType: string; id: string; title: string; url: string | null }
export interface AssistantMessage {
  id: string; role: "user" | "assistant"; content: string; citations: AssistantCitation[];
  escalated: boolean; intent: string | null; createdAt: string; attachments?: string[] | null;
}
export interface AssistantConversationSummary { id: string; title: string; lastMessageAt: string; hasEscalation: boolean }
export interface AssistantConversation extends AssistantConversationSummary { petId: string | null; messages: AssistantMessage[] }
export interface AssistantExchange {
  conversationId: string; userMessage: AssistantMessage; assistantMessage: AssistantMessage;
  disclaimer: string; escalationLink: string | null;
}

const BASE = "/v1/assistant";

export const assistantApi = {
  send: (message: string, conversationId?: string | null, petId?: string | null, imageUrls?: string[]) =>
    api.post<AssistantExchange>(`${BASE}/message`, { conversationId: conversationId ?? null, message, petId: petId ?? null, imageUrls: imageUrls?.length ? imageUrls : null },
      { skipErrorToast: false }).then(r => r.data),
  list: () => api.get<AssistantConversationSummary[]>(`${BASE}/conversations`).then(r => r.data),
  get: (id: string) => api.get<AssistantConversation>(`${BASE}/conversations/${id}`).then(r => r.data),
  remove: (id: string) => api.delete(`${BASE}/conversations/${id}`).then(() => undefined)
};

// ---- Symptom checklist triage + human vet review ----

export type TriageUrgency = "Routine" | "Soon" | "Urgent" | "Emergency";

export interface SymptomChecklist {
  species: string;
  duration: "lt24h" | "1to3d" | "gt3d";
  appetite: "normal" | "reduced" | "none";
  breathing: "normal" | "fast" | "laboured";
  gumColour: "pink" | "pale" | "blue" | "yellow" | "unknown";
  bleeding: "none" | "minor" | "heavy";
  vomitingCount: number;
  diarrhoeaCount: number;
  lethargy: "none" | "mild" | "severe";
  seizure: boolean;
  possibleToxinExposure: boolean;
  youngOrSenior: boolean;
  notes?: string | null;
}
export interface TriageAssessment { urgency: TriageUrgency; reasons: string[]; needsHumanReview: boolean; summary: string }
export interface TriageResult {
  conversationId: string; assistantMessage: AssistantMessage; assessment: TriageAssessment;
  bookingLink: string; escalationLink: string | null; reviewRequestId: string; disclaimer: string;
}
export interface ReviewRequest {
  id: string; conversationId: string; petId: string | null; userId: string; urgency: TriageUrgency; summary: string;
  imageUrls: string[]; checklist: SymptomChecklist | null; status: "Pending" | "Reviewed"; vetNotes: string | null;
  createdAt: string; reviewedAt: string | null;
}

export const assistantTriageApi = {
  submit: (body: { conversationId?: string | null; petId?: string | null; checklist: SymptomChecklist; imageUrls?: string[] }) =>
    api.post<TriageResult>(`${BASE}/triage`, body).then(r => r.data),
  mine: () => api.get<ReviewRequest[]>(`${BASE}/reviews/mine`).then(r => r.data),
  /** Vet queue (needs vets.view). */
  reviews: (status: "Pending" | "Reviewed" | "" = "Pending") =>
    api.get<ReviewRequest[]>(`${BASE}/reviews`, { params: status ? { status } : {} }).then(r => r.data),
  /** Needs vets.edit. */
  answer: (id: string, vetNotes: string) => api.post<ReviewRequest>(`${BASE}/reviews/${id}/answer`, { vetNotes }).then(r => r.data)
};
