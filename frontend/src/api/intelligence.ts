import { api } from "./client";

const BASE = "/v1/admin/intelligence";

export interface DemandPoint { date: string; actual: number | null; movingAvg7: number | null; projected: number | null }
export interface DemandForecast { points: DemandPoint[]; trendPerDay: number; projectedTotalNext14Days: number }
export interface DoctorWeekRate {
  weekStart: string; doctorId: string; doctorName: string; total: number; noShows: number; cancelled: number;
  noShowRate: number; cancellationRate: number;
}
export interface CapacityPoint { date: string; booked: number; capacity: number; utilisationPct: number }
export interface VaccineCoverage { species: string; totalPets: number; upToDate: number; coveragePct: number }
export interface LostPetWeek { weekStart: string; reported: number; reunited: number; stillOpen: number; recoveryRate: number }
export interface StoreTrendWeek {
  weekStart: string; orders: number; cartUsers: number; orderingUsers: number; conversionPct: number;
  returnRequests: number; returnRatePct: number;
}
export interface InventoryRunout {
  productId: string; name: string; sku: string; stock: number; avgDailySales: number;
  daysRemaining: number | null; runOutDate: string | null;
}
export interface Anomaly { metric: string; date: string; value: number; zScore: number; direction: "spike" | "drop" }

export type IntelligenceDataset =
  "demand" | "no-show" | "capacity" | "vaccine-coverage" | "lost-pets" | "store" | "inventory" | "anomalies";

export const intelligenceApi = {
  demand: (historyDays = 60) => api.get<DemandForecast>(`${BASE}/demand-forecast`, { params: { historyDays } }).then(r => r.data),
  noShow: (weeks = 8) => api.get<DoctorWeekRate[]>(`${BASE}/no-show-rates`, { params: { weeks } }).then(r => r.data),
  capacity: () => api.get<CapacityPoint[]>(`${BASE}/capacity`).then(r => r.data),
  vaccines: () => api.get<VaccineCoverage[]>(`${BASE}/vaccine-coverage`).then(r => r.data),
  lostPets: (weeks = 12) => api.get<LostPetWeek[]>(`${BASE}/lost-pets`, { params: { weeks } }).then(r => r.data),
  store: (weeks = 12) => api.get<StoreTrendWeek[]>(`${BASE}/store-trends`, { params: { weeks } }).then(r => r.data),
  inventory: () => api.get<InventoryRunout[]>(`${BASE}/inventory-runout`).then(r => r.data),
  anomalies: () => api.get<Anomaly[]>(`${BASE}/anomalies`).then(r => r.data),
  /** Downloads a dataset as CSV through the authenticated client (a plain link would miss the bearer token). */
  async exportCsv(dataset: IntelligenceDataset) {
    const res = await api.get<Blob>(`${BASE}/export/${dataset}`, { responseType: "blob" });
    const url = URL.createObjectURL(res.data);
    const a = document.createElement("a");
    a.href = url;
    a.download = `intelligence-${dataset}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }
};
