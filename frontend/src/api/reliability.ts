import { api } from "./client";

const V1 = "/v1";

export interface ReconciliationItem {
  orderId: string;
  paymentId: string | null;
  kind: string;
  severity: "critical" | "warning" | "info" | string;
  detail: string;
  localAmount: number | null;
  gatewayAmount: number | null;
  localStatus: string | null;
  gatewayStatus: string | null;
}

export interface ReconciliationReport {
  from: string;
  to: string;
  generatedAt: string;
  gatewayQuerySupported: boolean;
  paymentsChecked: number;
  matched: number;
  discrepancies: number;
  items: ReconciliationItem[];
}

export const paymentReconciliationApi = {
  /** Read-only run. */
  run: (from?: string, to?: string) =>
    api.get<ReconciliationReport>(`${V1}/admin/payments/reconcile`, { params: { from, to } }).then((r) => r.data),
  /** Run and append an immutable audit entry per critical discrepancy (needs settings.edit). */
  runAndRecord: (from?: string, to?: string) =>
    api.post<ReconciliationReport>(`${V1}/admin/payments/reconcile`, { from, to }).then((r) => r.data),
};
