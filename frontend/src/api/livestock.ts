import { api } from "./client";
import type { PetRef, VaccinationInput } from "./pets";

const V1 = "/v1/livestock";
const data = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

export const productionTypes = ["Milk", "Eggs", "Wool", "Meat", "Other"] as const;
export type ProductionType = (typeof productionTypes)[number];

export interface PetGroup {
  id: string; name: string; animalType: string; purpose?: string | null; notes?: string | null;
  memberCount: number; members: PetRef[];
}
export interface PetGroupInput { name: string; animalType: string; purpose?: string | null; notes?: string | null; }

export interface ProductionLog {
  id: string; petGroupId?: string | null; groupName?: string | null; petId?: string | null; petName?: string | null;
  type: ProductionType; quantity: number; unit: string; recordedOn: string; notes?: string | null;
}
export interface ProductionLogInput {
  petGroupId?: string | null; petId?: string | null; type: ProductionType; quantity: number; unit: string;
  recordedOn: string; notes?: string | null;
}

export interface BreedingRecord {
  id: string; damId: string; damName: string; sireId?: string | null; sireName?: string | null;
  sireDescription?: string | null; matedOn: string; expectedDueOn?: string | null; outcomeOn?: string | null;
  offspringCount?: number | null; notes?: string | null;
}
export interface BreedingRecordInput {
  damId: string; sireId?: string | null; sireDescription?: string | null; matedOn: string;
  expectedDueOn?: string | null; outcomeOn?: string | null; offspringCount?: number | null; notes?: string | null;
}

export const livestockApi = {
  groups: () => data<PetGroup[]>(api.get(`${V1}/groups`)),
  createGroup: (input: PetGroupInput) => data<PetGroup>(api.post(`${V1}/groups`, input)),
  updateGroup: (id: string, input: PetGroupInput) => api.put(`${V1}/groups/${id}`, input),
  deleteGroup: (id: string) => api.delete(`${V1}/groups/${id}`),
  setMembers: (id: string, petIds: string[]) => api.put(`${V1}/groups/${id}/members`, { petIds }),
  bulkVaccinate: (id: string, input: VaccinationInput) => data<{ recorded: number }>(api.post(`${V1}/groups/${id}/vaccinations`, input)),

  production: (from?: string, to?: string) => data<ProductionLog[]>(api.get(`${V1}/production`, { params: { from, to } })),
  addProduction: (input: ProductionLogInput) => data<ProductionLog>(api.post(`${V1}/production`, input)),
  deleteProduction: (id: string) => api.delete(`${V1}/production/${id}`),

  breeding: () => data<BreedingRecord[]>(api.get(`${V1}/breeding`)),
  addBreeding: (input: BreedingRecordInput) => data<BreedingRecord>(api.post(`${V1}/breeding`, input)),
  updateBreeding: (id: string, input: BreedingRecordInput) => api.put(`${V1}/breeding/${id}`, input),
  deleteBreeding: (id: string) => api.delete(`${V1}/breeding/${id}`),
};
