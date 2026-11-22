import { api } from "./client";
import type { ServerCart } from "./marketplace";

const MP = "/v1/marketplace-plus";

export interface BundleItem {
  productId: string; productName: string; imageUrl?: string | null; variantId?: string | null; variantLabel?: string | null;
  quantity: number; unitPrice: number; stock: number;
}
export interface Bundle {
  id: string; storeId: string; storeName: string; name: string; description?: string | null;
  price?: number | null; discountPercent?: number | null; isActive: boolean;
  listPrice: number; bundlePrice: number; savings: number; maxAvailable: number; items: BundleItem[];
}
export interface UpsertBundleInput {
  storeId?: string; name: string; description?: string; price?: number | null; discountPercent?: number | null;
  isActive: boolean; items: { productId: string; variantId?: string | null; quantity: number }[];
}

export interface ReplenishmentReminder {
  id: string; productId: string; productName: string; imageUrl?: string | null; price: number; inStock: boolean;
  expectedRunOutAt: string; daysLeft: number;
}

export interface StoreComparisonItem {
  storeId: string; name: string; city?: string | null; logoUrl?: string | null;
  rating?: number | null; reviewCount: number; avgFulfilmentDays?: number | null; deliveredOrders: number;
  returnRatePercent?: number | null; priceIndex?: number | null; comparableProducts: number;
  shippingFee: number; freeShippingThreshold: number; activeProducts: number;
}

export interface DeliveryPromise {
  storeId: string; minDays: number; maxDays: number; earliestDate: string; latestDate: string;
  basis: "history" | "default"; sampleSize: number; notes: string[];
}
export interface CartDeliveryPromise { earliestDate: string; latestDate: string; perStore: DeliveryPromise[] }

export interface AvailabilityProblem { cartItemId: string; productId: string; productName: string; code: string; message: string; available: number }
export interface CartAvailability { ok: boolean; problems: AvailabilityProblem[] }

export type LoyaltyTier = "Bronze" | "Silver" | "Gold" | "Platinum";
export interface LoyaltyAccount {
  balance: number; lifetimePoints: number; tier: LoyaltyTier; nextTier?: LoyaltyTier | null; pointsToNextTier: number;
  valuePerPoint: number; balanceValue: number; referralCode: string; referrals: number; rewardedReferrals: number;
}
export interface LoyaltyTransaction {
  id: string; type: "Earn" | "Redeem" | "Expire" | "Adjust" | "Reverse"; points: number; orderId?: string | null;
  note?: string | null; createdAt: string; expiresAt?: string | null;
}
export interface LoyaltyRedeemPreview { maxPoints: number; maxValue: number; balance: number; valuePerPoint: number }

export interface GiftCardBalance { maskedCode: string; balance: number; currency: string; expiresAt?: string | null; usable: boolean }
export interface GiftCard {
  id: string; code: string; initialAmount: number; balance: number; currency: string;
  recipientEmail?: string | null; message?: string | null; expiresAt?: string | null; status: string; createdAt: string;
}

export type PromotionType = "PercentOff" | "FixedOff" | "FreeShipping" | "BuyXGetY";
export type PromotionScope = "Storewide" | "Category" | "Store" | "Product";
export interface Promotion {
  id: string; name: string; type: PromotionType; scope: PromotionScope; scopeRefId?: string | null; value: number;
  buyQuantity: number; getQuantity: number; minSubtotal: number; startsAt?: string | null; endsAt?: string | null;
  usageLimit?: number | null; usedCount: number; stackable: boolean; isActive: boolean; createdAt: string;
}
export type UpsertPromotionInput = Omit<Promotion, "id" | "usedCount" | "createdAt">;

export interface PricingPreview {
  subtotal: number; couponDiscount: number; merchandiseDiscount: number; shippingFee: number; tax: number;
  creditsApplied: number; total: number; adjustments: { kind: string; description: string; amount: number; points?: number | null }[];
}

export interface SponsoredPlacement {
  id: string; productId: string; productName: string; storeId: string; storeName: string;
  startsAt: string; endsAt: string; budget?: number | null; spent: number; bidWeight: number; isActive: boolean;
  status: "Pending" | "Approved" | "Rejected"; impressions: number; createdAt: string;
}

export interface StockThreshold {
  productId: string; variantId?: string | null; productName: string; variantSku?: string | null;
  stock: number; threshold: number; lastAlertedAt?: string | null; isLow: boolean;
}

export const marketplacePlusApi = {
  stockForStore: (storeId: string) => api.get<StockThreshold[]>(`${MP}/stock/store/${storeId}`).then((r) => r.data),
  setStockThreshold: (p: { productId: string; variantId?: string | null; threshold: number }) =>
    api.put(`${MP}/stock/threshold`, p),
  requestGiftCard: (p: { amount: number; recipientEmail?: string; message?: string }) =>
    api.post<GiftCard>(`${MP}/giftcards/request`, p).then((r) => r.data),
  myGiftCards: () => api.get<GiftCard[]>(`${MP}/giftcards/mine`).then((r) => r.data),
  activateGiftCard: (id: string) => api.post<GiftCard>(`${MP}/giftcards/${id}/activate`).then((r) => r.data),

  bundles: (take = 12) => api.get<Bundle[]>(`${MP}/bundles`, { params: { take } }).then((r) => r.data),
  bundlesForProduct: (productId: string) => api.get<Bundle[]>(`${MP}/bundles/product/${productId}`).then((r) => r.data),
  bundlesForStore: (storeId: string, includeInactive = false) =>
    api.get<Bundle[]>(`${MP}/bundles/store/${storeId}`, { params: { includeInactive } }).then((r) => r.data),
  createBundle: (b: UpsertBundleInput) => api.post<Bundle>(`${MP}/bundles`, b).then((r) => r.data),
  updateBundle: (id: string, b: UpsertBundleInput) => api.put<Bundle>(`${MP}/bundles/${id}`, b).then((r) => r.data),
  deleteBundle: (id: string) => api.delete(`${MP}/bundles/${id}`),
  addBundleToCart: (id: string, quantity = 1) =>
    api.post<ServerCart>(`${MP}/bundles/${id}/add-to-cart`, { quantity }).then((r) => r.data),

  replenishment: () => api.get<ReplenishmentReminder[]>(`${MP}/replenishment`).then((r) => r.data),
  dismissReplenishment: (id: string) => api.post(`${MP}/replenishment/${id}/dismiss`),

  compareStores: (ids: string[]) =>
    api.get<{ stores: StoreComparisonItem[] }>(`${MP}/stores/compare`, { params: { ids }, paramsSerializer: { indexes: null } })
      .then((r) => r.data),
  deliveryForStore: (storeId: string, city?: string) =>
    api.get<DeliveryPromise>(`${MP}/delivery-promise/store/${storeId}`, { params: { city } }).then((r) => r.data),
  deliveryForCart: (city?: string) =>
    api.get<CartDeliveryPromise | null>(`${MP}/delivery-promise/cart`, { params: { city } }).then((r) => r.data),
  availability: () => api.get<CartAvailability>(`/v1/cart/availability`).then((r) => r.data),
  pricingPreview: (p: { couponCode?: string; redeemLoyaltyPoints?: number; giftCardCode?: string }) =>
    api.post<PricingPreview>(`${MP}/pricing-preview`, p, { skipErrorToast: true }).then((r) => r.data),

  loyalty: () => api.get<LoyaltyAccount>(`${MP}/loyalty`).then((r) => r.data),
  loyaltyHistory: (page = 1, pageSize = 20) =>
    api.get<{ items: LoyaltyTransaction[]; total: number; page: number; pageSize: number }>(
      `${MP}/loyalty/history`, { params: { page, pageSize } }).then((r) => r.data),
  redeemPreview: (payable: number) =>
    api.get<LoyaltyRedeemPreview>(`${MP}/loyalty/redeem-preview`, { params: { payable } }).then((r) => r.data),
  redeemReferral: (code: string) => api.post(`${MP}/loyalty/referral/redeem`, { code }),

  checkGiftCard: (code: string) =>
    api.post<GiftCardBalance>(`${MP}/giftcards/check`, { code }, { skipErrorToast: true }).then((r) => r.data),
  giftCards: () => api.get<GiftCard[]>(`${MP}/giftcards`).then((r) => r.data),
  issueGiftCard: (p: { amount: number; currency?: string; recipientEmail?: string; message?: string; expiresAt?: string | null }) =>
    api.post<GiftCard>(`${MP}/giftcards`, p).then((r) => r.data),
  cancelGiftCard: (id: string) => api.post(`${MP}/giftcards/${id}/cancel`),

  promotions: () => api.get<Promotion[]>(`${MP}/promotions`).then((r) => r.data),
  activePromotions: () => api.get<Promotion[]>(`${MP}/promotions/active`).then((r) => r.data),
  createPromotion: (p: UpsertPromotionInput) => api.post<Promotion>(`${MP}/promotions`, p).then((r) => r.data),
  updatePromotion: (id: string, p: UpsertPromotionInput) => api.put<Promotion>(`${MP}/promotions/${id}`, p).then((r) => r.data),
  deletePromotion: (id: string) => api.delete(`${MP}/promotions/${id}`),

  sponsored: (storeId?: string) => api.get<SponsoredPlacement[]>(`${MP}/sponsored`, { params: { storeId } }).then((r) => r.data),
  createSponsored: (p: { productId: string; startsAt: string; endsAt: string; budget?: number | null; bidWeight?: number | null; isActive?: boolean }) =>
    api.post<SponsoredPlacement>(`${MP}/sponsored`, p).then((r) => r.data),
  reviewSponsored: (id: string, approve: boolean) => api.post(`${MP}/sponsored/${id}/review`, { approve }),
  deleteSponsored: (id: string) => api.delete(`${MP}/sponsored/${id}`),
};
