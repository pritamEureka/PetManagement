import { api } from "./client";

const V1 = "/v1";

export interface ShippingQuote {
  zoneMatched: boolean; zoneId: string | null; zoneName: string | null; currency: string;
  baseFee: number; peakSurcharge: number; peakLabel: string | null; freeShippingApplied: boolean; total: number;
  etaMinHours: number | null; etaMaxHours: number | null; etaEarliest: string | null; etaLatest: string | null;
}
export interface DeliveryZone {
  id: string; storeId: string | null; name: string; cities: string[]; baseFee: number;
  freeShippingThreshold: number | null; estimatedMinHours: number; estimatedMaxHours: number; isActive: boolean;
}
export type UpsertDeliveryZone = Omit<DeliveryZone, "id">;
export interface PeakHourRule {
  id: string; zoneId: string | null; days: number[]; startTime: string; endTime: string;
  surchargeFixed: number; surchargePercent: number; isActive: boolean; label: string | null;
}
export type UpsertPeakHourRule = Omit<PeakHourRule, "id">;
export interface FulfillmentSla {
  id: string | null; storeId: string | null; maxHoursToShip: number; maxHoursToDeliver: number;
  penaltyNote: string | null; isActive: boolean;
}
export interface SlaBreach {
  id: string; orderId: string; orderNumber: string | null; storeId: string; storeName: string | null;
  kind: "Ship" | "Deliver"; breachedAt: string; resolvedAt: string | null;
}
export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number }

export type ReturnPickupStatus = "Requested" | "Scheduled" | "PickedUp" | "InTransit" | "ReceivedByStore" | "Failed";
export interface ReturnPickup {
  id: string; returnRequestId: string; orderId: string; orderNumber: string | null; productName: string | null;
  status: ReturnPickupStatus; windowStart: string; windowEnd: string; address: string; notes: string | null;
  deliveryUserId: string | null; deliveryUserName: string | null; pickedUpAt: string | null; receivedAt: string | null;
  proofPhotoUrl: string | null; proofSignatureName: string | null; rescheduleCount: number; createdAt: string;
  checkpoints: { id: string; status: ReturnPickupStatus; note: string | null; occurredAt: string }[];
}

export interface PartialRefundResult {
  orderId: string; amount: number; gatewayStatus: string;
  lines: { orderItemId: string; quantity: number; amount: number }[]; orderRefundedTotal: number;
}
export interface OrderRefundSummary {
  orderId: string; orderTotal: number; refundedAmount: number;
  refunds: { id: string; orderItemId: string; quantity: number; amount: number; reason: string; gatewayStatus: string; createdAt: string }[];
}
export interface StoreOrderSplit {
  storeId: string; storeName: string | null; grossSubtotal: number; discount: number; netSubtotal: number;
  shipping: number; tax: number; commission: number; total: number; refunded: number; payable: number;
}

export interface StoreMetrics {
  storeId: string; days: number; orders: number; cancelledOrders: number; cancellationRate: number;
  shipSlaOrders: number; lateShipments: number; lateShipmentRate: number;
  returnRequests: number; returnRate: number; rejectedReturns: number; disputeRate: number;
  productViewers: number; conversionRate: number; conversionAvailable: boolean;
  averageRating: number; reviewCount: number; revenue: number;
  dailyRevenue: { label: string; value: number }[];
  topProducts: { productId: string; name: string; units: number; revenue: number }[];
}

export const logisticsApi = {
  quote: (p: { storeId?: string; city?: string; subtotal: number }) =>
    api.get<ShippingQuote>(`${V1}/shipping/quote`, { params: p }).then((r) => r.data),

  zones: () => api.get<DeliveryZone[]>(`${V1}/admin/delivery-zones`).then((r) => r.data),
  saveZone: (id: string | null, b: UpsertDeliveryZone) =>
    (id ? api.put<DeliveryZone>(`${V1}/admin/delivery-zones/${id}`, b) : api.post<DeliveryZone>(`${V1}/admin/delivery-zones`, b)).then((r) => r.data),
  deleteZone: (id: string) => api.delete(`${V1}/admin/delivery-zones/${id}`),
  rules: () => api.get<PeakHourRule[]>(`${V1}/admin/peak-hour-rules`).then((r) => r.data),
  saveRule: (id: string | null, b: UpsertPeakHourRule) =>
    (id ? api.put<PeakHourRule>(`${V1}/admin/peak-hour-rules/${id}`, b) : api.post<PeakHourRule>(`${V1}/admin/peak-hour-rules`, b)).then((r) => r.data),
  deleteRule: (id: string) => api.delete(`${V1}/admin/peak-hour-rules/${id}`),

  slas: () => api.get<FulfillmentSla[]>(`${V1}/admin/fulfillment-slas`).then((r) => r.data),
  saveSla: (b: Omit<FulfillmentSla, "id">) => api.put<FulfillmentSla>(`${V1}/admin/fulfillment-slas`, b).then((r) => r.data),
  breaches: (openOnly = false, page = 1) =>
    api.get<Paged<SlaBreach>>(`${V1}/admin/sla-breaches`, { params: { openOnly, page } }).then((r) => r.data),
  myBreaches: (openOnly = true) =>
    api.get<Paged<SlaBreach>>(`${V1}/stores/me/sla-breaches`, { params: { openOnly } }).then((r) => r.data),

  pickupForReturn: (returnId: string) =>
    api.get<ReturnPickup | null>(`${V1}/returns/${returnId}/pickup`).then((r) => r.data ?? null),
  requestPickup: (returnId: string, b: { windowStart: string; windowEnd: string; address?: string; notes?: string }) =>
    api.post<ReturnPickup>(`${V1}/returns/${returnId}/pickup`, b).then((r) => r.data),
  reschedulePickup: (id: string, b: { windowStart: string; windowEnd: string; notes?: string }) =>
    api.put<ReturnPickup>(`${V1}/return-pickups/${id}/reschedule`, b).then((r) => r.data),
  pickupsAdmin: (status?: ReturnPickupStatus) =>
    api.get<Paged<ReturnPickup>>(`${V1}/return-pickups/admin`, { params: { status } }).then((r) => r.data),
  assignPickup: (id: string, deliveryUserId: string) =>
    api.post<ReturnPickup>(`${V1}/return-pickups/${id}/assign`, { deliveryUserId }).then((r) => r.data),
  myPickups: (includeClosed = false) =>
    api.get<ReturnPickup[]>(`${V1}/return-pickups/mine`, { params: { includeClosed } }).then((r) => r.data),
  courierUpdate: (id: string, b: { status: ReturnPickupStatus; note?: string; proofPhotoUrl?: string; proofSignatureName?: string }) =>
    api.put<ReturnPickup>(`${V1}/return-pickups/${id}/status`, b).then((r) => r.data),
  confirmReceipt: (id: string, note?: string) =>
    api.post<ReturnPickup>(`${V1}/return-pickups/${id}/confirm-receipt`, { note }).then((r) => r.data),

  partialRefund: (orderId: string, b: { items: { orderItemId: string; quantity: number }[]; reason: string }) =>
    api.post<PartialRefundResult>(`${V1}/orders/${orderId}/partial-refund`, b).then((r) => r.data),
  refunds: (orderId: string) => api.get<OrderRefundSummary>(`${V1}/orders/${orderId}/refunds`).then((r) => r.data),
  storeSplit: (orderId: string) => api.get<StoreOrderSplit[]>(`${V1}/orders/${orderId}/store-split`).then((r) => r.data),

  myMetrics: (days = 30) => api.get<StoreMetrics>(`${V1}/stores/me/metrics`, { params: { days } }).then((r) => r.data)
};
