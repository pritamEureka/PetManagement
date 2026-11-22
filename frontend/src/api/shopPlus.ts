import { api } from "./client";
import type { ProductSummary } from "./marketplace";

const SP = "/v1/shop-plus";
const DISC = "/v1/marketplace-discovery";

// ---------- 8.5 variants / reservations / subscriptions ---------------------

export interface Variant {
  id: string; productId: string; sku: string;
  attributes: Record<string, string>;
  priceOverride?: number | null; effectivePrice: number;
  stockQuantity: number; reserved: number; available: number; isActive: boolean;
}

export interface UpsertVariantInput {
  sku: string; attributes?: Record<string, string>; priceOverride?: number | null;
  stockQuantity?: number; isActive?: boolean;
}

export type SubscriptionStatus = "Active" | "Paused" | "Cancelled";

export interface Subscription {
  id: string; productId: string; productName: string; imageUrl?: string | null;
  variantId?: string | null; variantSku?: string | null;
  quantity: number; intervalDays: number; discountPercent: number;
  unitPrice: number; discountedUnitPrice: number;
  nextRunAt: string; status: SubscriptionStatus; failureCount: number; lastError?: string | null;
  lastOrderId?: string | null; lastRunAt?: string | null;
  shippingAddressId: string; paymentMethod: string; createdAt: string;
}

export interface CreateSubscriptionInput {
  productId: string; variantId?: string | null; quantity: number; intervalDays: number;
  shippingAddressId: string; paymentMethod?: string;
}

export interface ReorderResult {
  addedCount: number;
  skipped: { productId: string; productName: string; reason: string }[];
}

export const shopPlusApi = {
  variants: (productId: string) =>
    api.get<Variant[]>(`${SP}/products/${productId}/variants`).then((r) => r.data),
  variantsManage: (productId: string) =>
    api.get<Variant[]>(`${SP}/products/${productId}/variants/manage`).then((r) => r.data),
  createVariant: (productId: string, input: UpsertVariantInput) =>
    api.post<Variant>(`${SP}/products/${productId}/variants`, input).then((r) => r.data),
  updateVariant: (id: string, input: UpsertVariantInput) =>
    api.put<Variant>(`${SP}/variants/${id}`, input).then((r) => r.data),
  deleteVariant: (id: string) => api.delete(`${SP}/variants/${id}`),
  adjustVariantStock: (id: string, quantityChange: number, notes?: string) =>
    api.post<Variant>(`${SP}/variants/${id}/stock`, { quantityChange, reason: "Adjustment", notes }).then((r) => r.data),
  addVariantToCart: (variantId: string, quantity: number) =>
    api.post(`${SP}/cart/variant-items`, { variantId, quantity }).then((r) => r.data),

  startCheckoutReservation: () => api.post(`${SP}/reservations/start-checkout`).then((r) => r.data),
  releaseReservations: () => api.post(`${SP}/reservations/release`),

  subscriptions: () => api.get<Subscription[]>(`${SP}/subscriptions`).then((r) => r.data),
  subscribe: (input: CreateSubscriptionInput) =>
    api.post<Subscription>(`${SP}/subscriptions`, input).then((r) => r.data),
  updateSubscription: (id: string, input: { quantity?: number; intervalDays?: number; shippingAddressId?: string; paymentMethod?: string }) =>
    api.put<Subscription>(`${SP}/subscriptions/${id}`, input).then((r) => r.data),
  pauseSubscription: (id: string) => api.post<Subscription>(`${SP}/subscriptions/${id}/pause`).then((r) => r.data),
  resumeSubscription: (id: string) => api.post<Subscription>(`${SP}/subscriptions/${id}/resume`).then((r) => r.data),
  skipSubscription: (id: string) => api.post<Subscription>(`${SP}/subscriptions/${id}/skip`).then((r) => r.data),
  cancelSubscription: (id: string) => api.delete(`${SP}/subscriptions/${id}`),

  reorder: (orderId: string) => api.post<ReorderResult>(`${SP}/orders/${orderId}/reorder`).then((r) => r.data),
};

// ---------- 8.7 discovery ---------------------------------------------------

export interface FacetSearchRequest {
  q?: string; categoryId?: string; brandId?: string; storeId?: string;
  minPrice?: number; maxPrice?: number; minRating?: number; inStockOnly?: boolean;
  sort?: string; lat?: number; lng?: number; page?: number; pageSize?: number;
}

export interface FacetBucket { key: string; label: string; count: number; selected: boolean }
export interface Facets {
  categories: FacetBucket[]; brands: FacetBucket[]; stores: FacetBucket[];
  priceRanges: FacetBucket[]; ratings: FacetBucket[]; stock: FacetBucket[];
}
export interface ScoreComponent { name: string; value: number; weight: number; contribution: number; note?: string | null }
export interface ScoreBreakdown { total: number; components: ScoreComponent[]; distanceKm?: number | null }
export interface RankedProduct { product: ProductSummary; score: number; breakdown: ScoreBreakdown }
export interface FacetSearchResult {
  items: RankedProduct[]; total: number; page: number; pageSize: number;
  facets: Facets; didYouMean?: string | null; effectiveQuery?: string | null;
  /** Paid placements, always separate from organic `items` (max 2). */
  sponsored?: { product: ProductSummary; placementId: string; label: string }[];
}
export interface Suggestion { text: string; kind: "term" | "correction" | "product" }
export interface SavedSearch { id: string; name: string; query: FacetSearchRequest; alertEnabled: boolean; createdAt: string }
export interface PriceWatch {
  id: string; productId: string; productName: string; imageUrl?: string | null;
  baselinePrice: number; currentPrice: number; targetPrice?: number | null; isActive: boolean; lastNotifiedAt?: string | null;
}

export const discoveryApi = {
  search: (params: FacetSearchRequest) =>
    api.get<FacetSearchResult>(`${DISC}/search`, { params }).then((r) => r.data),
  suggest: (q: string, limit = 8) =>
    api.get<Suggestion[]>(`${DISC}/suggest`, { params: { q, limit }, skipErrorToast: true }).then((r) => r.data),

  savedSearches: () => api.get<SavedSearch[]>(`${DISC}/saved-searches`).then((r) => r.data),
  createSavedSearch: (name: string, query: FacetSearchRequest, alertEnabled: boolean) =>
    api.post<SavedSearch>(`${DISC}/saved-searches`, { name, query, alertEnabled }).then((r) => r.data),
  updateSavedSearch: (id: string, input: { name?: string; alertEnabled?: boolean }) =>
    api.put<SavedSearch>(`${DISC}/saved-searches/${id}`, input).then((r) => r.data),
  deleteSavedSearch: (id: string) => api.delete(`${DISC}/saved-searches/${id}`),

  recordView: (productId: string) =>
    api.post(`${DISC}/recently-viewed/${productId}`, null, { skipErrorToast: true }),
  recentlyViewed: (limit = 12) =>
    api.get<ProductSummary[]>(`${DISC}/recently-viewed`, { params: { limit }, skipErrorToast: true }).then((r) => r.data),

  priceWatches: () => api.get<PriceWatch[]>(`${DISC}/price-watches`).then((r) => r.data),
  createPriceWatch: (productId: string, targetPrice?: number | null) =>
    api.post<PriceWatch>(`${DISC}/price-watches`, { productId, targetPrice }).then((r) => r.data),
  deletePriceWatch: (id: string) => api.delete(`${DISC}/price-watches/${id}`),

  setStoreLocation: (latitude: number, longitude: number) =>
    api.put(`${DISC}/my-store-location`, { latitude, longitude }),
};
