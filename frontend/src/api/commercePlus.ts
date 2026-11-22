import { api } from "./client";

const V1 = "/v1";

// ---------------------------------------------------------------- tracking

export type TrackingStatus =
  | "LabelCreated" | "PickedUp" | "InTransit" | "OutForDelivery"
  | "Delivered" | "DeliveryFailed" | "Returned" | "Exception";
export type CheckpointSource = "Courier" | "Webhook" | "Manual";

export interface DeliveryCheckpoint {
  id: string;
  status: TrackingStatus;
  description: string | null;
  locationText: string | null;
  latitude: number | null;
  longitude: number | null;
  source: CheckpointSource;
  provider: string | null;
  occurredAt: string;
}

export interface DeliveryProof {
  id: string;
  photoUrl: string | null;
  signatureName: string | null;
  otpProvided: boolean;
  recipientName: string | null;
  capturedAt: string;
}

export interface OrderTracking {
  orderId: string;
  orderNumber: string;
  shipmentStatus: string;
  trackingNumber: string | null;
  courier: string | null;
  checkpoints: DeliveryCheckpoint[];
  proof: DeliveryProof | null;
}

export interface AddCheckpointInput {
  status: TrackingStatus;
  description?: string | null;
  locationText?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  occurredAt?: string | null;
}

export interface AddDeliveryProofInput {
  photoUrl?: string | null;
  signatureName?: string | null;
  otpCode?: string | null;
  recipientName?: string | null;
}

export const trackingApi = {
  timeline: (orderId: string) =>
    api.get<OrderTracking>(`${V1}/tracking/orders/${orderId}`).then((r) => r.data),
  addCheckpoint: (orderId: string, input: AddCheckpointInput) =>
    api.post<{ id: string }>(`${V1}/tracking/orders/${orderId}/checkpoints`, input).then((r) => r.data),
  addProof: (orderId: string, input: AddDeliveryProofInput) =>
    api.put(`${V1}/tracking/orders/${orderId}/proof`, input)
};

// ---------------------------------------------------------------- settlements

export type LedgerEntryType = "Earning" | "Commission" | "Refund" | "Reversal" | "Payout";
export type SettlementBatchStatus = "Generated" | "Paid" | "Reconciled";
export type StorePayoutStatus = "Pending" | "Paid" | "Reconciled" | "Failed";

export interface LedgerEntry {
  id: string;
  storeId: string;
  orderId: string | null;
  orderItemId: string | null;
  type: LedgerEntryType;
  amount: number;
  currency: string;
  availableAt: string;
  memo: string | null;
  createdAt: string;
}

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number }

export interface StoreLedgerSummary {
  storeId: string;
  currency: string;
  holdDays: number;
  totalBalance: number;
  availableBalance: number;
  onHoldBalance: number;
  paidOut: number;
  entries: Paged<LedgerEntry>;
}

export interface StorePayout {
  id: string;
  settlementBatchId: string;
  batchReference: string;
  storeId: string;
  storeName: string;
  amount: number;
  currency: string;
  status: StorePayoutStatus;
  gatewayReference: string | null;
  gatewayAmount: number | null;
  notes: string | null;
  createdAt: string;
  paidAt: string | null;
  reconciledAt: string | null;
}

export interface SettlementBatch {
  id: string;
  reference: string;
  status: SettlementBatchStatus;
  currency: string;
  totalAmount: number;
  payoutCount: number;
  cutoffAt: string;
  createdAt: string;
  paidAt: string | null;
  reconciledAt: string | null;
}

export interface SettlementBatchDetail { batch: SettlementBatch; payouts: StorePayout[] }

export interface ReconcileResult {
  matched: number;
  mismatched: number;
  mismatchedPayoutIds: string[];
  batch: SettlementBatch;
}

async function downloadCsv(url: string, params?: Record<string, unknown>) {
  const res = await api.get<Blob>(url, { params, responseType: "blob" });
  const cd = (res.headers["content-disposition"] as string | undefined) ?? "";
  const m = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(cd);
  const name = m ? decodeURIComponent(m[1]) : "statement.csv";
  const href = URL.createObjectURL(res.data);
  const a = document.createElement("a");
  a.href = href;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(href);
}

export const settlementsApi = {
  // store side
  mine: (page = 1, pageSize = 15) =>
    api.get<StoreLedgerSummary>(`${V1}/settlements/mine`, { params: { page, pageSize } }).then((r) => r.data),
  myPayouts: () => api.get<StorePayout[]>(`${V1}/settlements/mine/payouts`).then((r) => r.data),
  downloadMyStatement: (from?: string, to?: string) =>
    downloadCsv(`${V1}/settlements/mine/statement.csv`, { from, to }),

  // admin side
  batches: (page = 1, pageSize = 20) =>
    api.get<Paged<SettlementBatch>>(`${V1}/settlements/admin/batches`, { params: { page, pageSize } }).then((r) => r.data),
  batch: (id: string) =>
    api.get<SettlementBatchDetail>(`${V1}/settlements/admin/batches/${id}`).then((r) => r.data),
  generate: (input: { cutoffAt?: string | null; minimumPayout?: number | null } = {}) =>
    api.post<{ generated: boolean; batch: SettlementBatchDetail | null }>(
      `${V1}/settlements/admin/batches/generate`, input).then((r) => r.data),
  downloadBatch: (id: string) => downloadCsv(`${V1}/settlements/admin/batches/${id}/export.csv`),
  reconcile: (id: string, lines: { payoutId: string; gatewayAmount: number; gatewayReference?: string | null }[]) =>
    api.post<ReconcileResult>(`${V1}/settlements/admin/batches/${id}/reconcile`, { lines }).then((r) => r.data),
  markPaid: (payoutId: string, input: { gatewayReference: string; gatewayAmount?: number | null; notes?: string | null }) =>
    api.post(`${V1}/settlements/admin/payouts/${payoutId}/paid`, input),
  failPayout: (payoutId: string, reason?: string) =>
    api.post(`${V1}/settlements/admin/payouts/${payoutId}/fail`, { reason }),
  downloadStoreStatement: (storeId: string, from?: string, to?: string) =>
    downloadCsv(`${V1}/settlements/admin/stores/${storeId}/statement.csv`, { from, to })
};
