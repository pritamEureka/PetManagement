import { api } from "./client";
import type { IntelligenceDataset } from "./intelligence";

const BASE = "/v1/admin/intelligence/schedules";

export type ReportFrequency = "Daily" | "Weekly" | "Monthly";

export interface ScheduledReport {
  id: string; name: string; dataset: IntelligenceDataset; frequency: ReportFrequency; recipients: string[];
  isActive: boolean; lastRunAt: string | null; nextRunAt: string; ownerUserId: string; createdAt: string;
  lastRunSuccess: boolean | null;
}
export interface SaveScheduledReport {
  name: string; dataset: IntelligenceDataset; frequency: ReportFrequency; recipients: string[]; isActive: boolean;
}
export interface ReportRun {
  id: string; ranAt: string; success: boolean; error: string | null; rowCount: number; recipientCount: number;
  emailsSent: number; csvTruncated: boolean; manual: boolean; hasCsv: boolean;
}

export const scheduledReportsApi = {
  list: () => api.get<ScheduledReport[]>(BASE).then(r => r.data),
  create: (body: SaveScheduledReport) => api.post<ScheduledReport>(BASE, body).then(r => r.data),
  update: (id: string, body: SaveScheduledReport) => api.put<ScheduledReport>(`${BASE}/${id}`, body).then(r => r.data),
  remove: (id: string) => api.delete(`${BASE}/${id}`).then(() => undefined),
  runNow: (id: string) => api.post<ReportRun>(`${BASE}/${id}/run`).then(r => r.data),
  runs: (id: string) => api.get<ReportRun[]>(`${BASE}/${id}/runs`).then(r => r.data),
  async downloadRun(id: string, runId: string, name: string) {
    const res = await api.get<Blob>(`${BASE}/${id}/runs/${runId}/csv`, { responseType: "blob" });
    const url = URL.createObjectURL(res.data);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${name}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }
};
