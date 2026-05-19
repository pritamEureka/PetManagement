import { api } from "./client";

// ---------- Shared types --------------------------------------------------

export type ApprovalStatus = "Pending" | "Approved" | "Rejected" | "Suspended";
export type OrderStatus = "Created" | "Confirmed" | "Packed" | "Shipped" | "Delivered" | "Cancelled" | "Returned";
export type PaymentStatusT = "Unpaid" | "Pending" | "Paid" | "Refunded" | "Failed";
export type ShipmentStatus = "NotShipped" | "Processing" | "InTransit" | "OutForDelivery" | "Delivered" | "Failed";
export type InventoryReason = "Purchase" | "Sale" | "Return" | "Adjustment" | "Damage" | "Restock";
export type CommissionScope = "Global" | "Category" | "Store";
export type StoreDocumentType = "TradeLicense" | "NationalId" | "AddressProof" | "TaxCertificate" | "BankStatement" | "Other";

export interface PageResult<T> { items: T[]; total: number; page: number; pageSize: number; totalPages: number }

// ---------- Stores --------------------------------------------------------

export interface StoreOwnerProfile {
  id: string; userId: string;
  legalName: string; businessName?: string | null;
  tradeLicenseNumber?: string | null; nationalIdNumber?: string | null; taxId?: string | null;
  tradeLicenseDocUrl?: string | null; nationalIdDocUrl?: string | null; addressProofDocUrl?: string | null;
  kycStatus: ApprovalStatus; adminNotes?: string | null;
  submittedAt?: string | null; decidedAt?: string | null;
}

export interface StoreOwnerProfileInput {
  legalName: string; businessName?: string;
  tradeLicenseNumber?: string; nationalIdNumber?: string; taxId?: string;
  tradeLicenseDocUrl?: string; nationalIdDocUrl?: string; addressProofDocUrl?: string;
  additionalDocuments?: { type: StoreDocumentType; fileName: string; url: string; notes?: string }[];
}

export interface Store {
  id: string; ownerUserId: string; name: string; description?: string | null;
  logoUrl?: string | null; bannerUrl?: string | null;
  address?: string | null; city?: string | null; country?: string | null;
  phoneNumber?: string | null; email?: string | null;
  approvalStatus: ApprovalStatus; commissionPercent: number;
  avgRating: number; reviewCount: number; productCount: number;
  createdAt: string;
}

export interface StoreInput {
  name: string; description?: string;
  logoUrl?: string; bannerUrl?: string;
  address?: string; city?: string; country?: string;
  phoneNumber?: string; email?: string;
}

// ---------- Catalog --------------------------------------------------------

export interface ProductCategory { id: string; name: string; slug: string; parentCategoryId?: string | null; productCount: number }

export interface ProductSummary {
  id: string; name: string; sku: string;
  price: number; discountPrice?: number | null;
  stockQuantity: number; isActive: boolean; isFeatured: boolean;
  ratingAverage: number; ratingCount: number;
  storeId: string; storeName: string;
  categoryId?: string | null; categoryName?: string | null;
  brandId?: string | null; brandName?: string | null;
  imageUrls: string[];
  createdAt: string;
}

export interface ProductReview {
  id: string; productId: string; userId: string;
  userDisplayName: string; userAvatarUrl?: string | null;
  rating: number; comment?: string | null; createdAt: string;
}

export interface ProductDetail extends Omit<ProductSummary, "imageUrls"> {
  description?: string | null;
  storeApprovalStatus: ApprovalStatus;
  imageUrls: string[];
  recentReviews: ProductReview[];
}

export interface ProductInput {
  name: string; sku: string; description?: string;
  price: number; discountPrice?: number | null;
  stockQuantity: number;
  categoryId?: string | null; brandId?: string | null;
  imageUrls?: string[];
}

export interface ProductUpdateInput {
  name: string; description?: string;
  price: number; discountPrice?: number | null;
  categoryId?: string | null; brandId?: string | null;
  isActive: boolean; imageUrls?: string[];
}

export interface InventoryAdjustment {
  id: string; productId: string;
  quantityChange: number; quantityAfter: number;
  reason: InventoryReason; notes?: string | null;
  performedById?: string | null; createdAt: string;
}

// ---------- Cart -----------------------------------------------------------

export interface ServerCartItem {
  id: string; productId: string; productName: string; imageUrl?: string | null;
  storeId: string; storeName: string;
  quantity: number; unitPrice: number; total: number; stockAvailable: number;
}
export interface ServerCart { id: string; currency: string; items: ServerCartItem[]; subtotal: number; totalItems: number }

// ---------- Orders ---------------------------------------------------------

export interface OrderItem {
  id: string; productId: string; productName: string; imageUrl?: string | null;
  storeId: string; storeName: string;
  quantity: number; unitPrice: number; total: number; commissionAmount: number;
}

export interface Order {
  id: string; orderNumber: string; userId: string;
  subtotal: number; shippingFee: number; tax: number; total: number;
  status: OrderStatus; paymentStatus: PaymentStatusT; shipmentStatus: ShipmentStatus;
  shippingAddress: string; shippingCity?: string | null; shippingCountry?: string | null;
  trackingNumber?: string | null;
  items: OrderItem[]; createdAt: string;
  /**
   * When set (only on the response from POST /v1/orders/checkout for hosted-payment
   * methods like sslcommerz), the client should redirect the browser here to
   * complete payment. Successful payment lands the user on /checkout/success.
   */
  paymentCheckoutUrl?: string | null;
}

export type PaymentMethod = "sslcommerz" | "cod";

export interface CheckoutPayload {
  shippingAddressId?: string;
  shippingAddress?: string; shippingCity?: string; shippingCountry?: string;
  paymentMethod?: string;
}

// ---------- Shipping address ----------------------------------------------

export interface ShippingAddress {
  id: string; label: string; recipientName: string; phoneNumber: string;
  addressLine1: string; addressLine2?: string | null;
  city?: string | null; state?: string | null; country?: string | null; postalCode?: string | null;
  isDefault: boolean;
}

// ---------- Commission ----------------------------------------------------

export interface CommissionConfiguration {
  id: string; scope: CommissionScope;
  storeId?: string | null; storeName?: string | null;
  categoryId?: string | null; categoryName?: string | null;
  commissionPercent: number; flatFee?: number | null;
  minCommission?: number | null; maxCommission?: number | null;
  effectiveFrom: string; effectiveTo?: string | null;
  notes?: string | null;
}

// ---------- Sales report --------------------------------------------------

export interface DailySalesPoint { date: string; revenue: number; orders: number }
export interface TopProduct { productId: string; name: string; unitsSold: number; revenue: number }
export interface StoreSalesReport {
  storeId: string; from: string; to: string;
  grossRevenue: number; commission: number; netRevenue: number;
  ordersCount: number; itemsSold: number;
  daily: DailySalesPoint[]; topProducts: TopProduct[];
}

// ---------- Helper --------------------------------------------------------

const V1 = "/v1";

// ==========================================================================
//   API surface
// ==========================================================================

export const storesApi = {
  // Catalog of stores
  search: (params: { search?: string; status?: ApprovalStatus; page?: number; pageSize?: number } = {}) =>
    api.get<PageResult<Store>>(`${V1}/stores`, { params }).then((r) => r.data),
  get: (id: string) => api.get<Store>(`${V1}/stores/${id}`).then((r) => r.data),
  mine: () => api.get<Store | null>(`${V1}/stores/mine`).then((r) => r.data),

  register: (input: StoreInput) => api.post<{ id: string }>(`${V1}/stores/register`, input).then((r) => r.data),
  update: (id: string, input: StoreInput) => api.put<void>(`${V1}/stores/${id}`, input).then((r) => r.data),

  // KYC
  myKyc: () => api.get<StoreOwnerProfile | null>(`${V1}/stores/owner/profile`).then((r) => r.data),
  submitKyc: (input: StoreOwnerProfileInput) =>
    api.post<StoreOwnerProfile>(`${V1}/stores/owner/profile`, input).then((r) => r.data),

  // Admin moderation
  adminListKyc: (status?: ApprovalStatus, page = 1, pageSize = 20) =>
    api.get<PageResult<StoreOwnerProfile>>(`${V1}/stores/admin/owner-profiles`, { params: { status, page, pageSize } })
       .then((r) => r.data),
  adminApproveKyc: (id: string, notes?: string) =>
    api.post(`${V1}/stores/admin/owner-profiles/${id}/approve`, { notes }),
  adminRejectKyc: (id: string, notes?: string) =>
    api.post(`${V1}/stores/admin/owner-profiles/${id}/reject`, { notes }),

  approve: (id: string, notes?: string) => api.post(`${V1}/stores/${id}/approve`, { notes }),
  reject: (id: string, notes?: string) => api.post(`${V1}/stores/${id}/reject`, { notes }),
  suspend: (id: string, notes?: string) => api.post(`${V1}/stores/${id}/suspend`, { notes }),
  restore: (id: string) => api.post(`${V1}/stores/${id}/restore`),
  feature: (id: string, value: boolean) => api.post(`${V1}/stores/${id}/feature`, null, { params: { value } }),
  setCommission: (id: string, percent: number) =>
    api.put(`${V1}/stores/${id}/commission`, { percent }),

  // Store reviews
  listReviews: (id: string, page = 1, pageSize = 20) =>
    api.get(`${V1}/stores/${id}/reviews`, { params: { page, pageSize } }).then((r) => r.data),
  createReview: (id: string, payload: { rating: number; comment?: string; orderId?: string }) =>
    api.post(`${V1}/stores/${id}/reviews`, payload).then((r) => r.data),

  report: (id: string, from?: string, to?: string) =>
    api.get<StoreSalesReport>(`${V1}/stores/${id}/report`, { params: { from, to } }).then((r) => r.data),
};

export const productsV2Api = {
  list: (params: {
    search?: string; categoryId?: string; brandId?: string; storeId?: string;
    minPrice?: number; maxPrice?: number; featured?: boolean; inStockOnly?: boolean;
    sort?: string; page?: number; pageSize?: number;
  } = {}) =>
    api.get<PageResult<ProductSummary>>(`${V1}/products`, { params }).then((r) => r.data),

  mine: (params: { search?: string; page?: number; pageSize?: number } = {}) =>
    api.get<PageResult<ProductSummary>>(`${V1}/products/mine`, { params }).then((r) => r.data),

  adminAll: (params: { search?: string; page?: number; pageSize?: number } = {}) =>
    api.get<PageResult<ProductSummary>>(`${V1}/products/admin/all`, { params }).then((r) => r.data),

  get: (id: string) => api.get<ProductDetail>(`${V1}/products/${id}`).then((r) => r.data),

  create: (input: ProductInput) => api.post<{ id: string }>(`${V1}/products`, input).then((r) => r.data),
  update: (id: string, input: ProductUpdateInput) => api.put(`${V1}/products/${id}`, input).then((r) => r.data),
  remove: (id: string) => api.delete(`${V1}/products/${id}`).then((r) => r.data),

  feature: (id: string, value: boolean) => api.post(`${V1}/products/${id}/feature`, null, { params: { value } }),
  publish: (id: string, active: boolean) => api.post(`${V1}/products/${id}/publish`, null, { params: { active } }),

  adjustInventory: (id: string, input: { quantityChange: number; reason: InventoryReason; notes?: string }) =>
    api.post<InventoryAdjustment>(`${V1}/products/${id}/inventory`, input).then((r) => r.data),
  listInventory: (id: string, page = 1, pageSize = 50) =>
    api.get<PageResult<InventoryAdjustment>>(`${V1}/products/${id}/inventory`, { params: { page, pageSize } })
       .then((r) => r.data),

  listReviews: (id: string, page = 1, pageSize = 20) =>
    api.get<PageResult<ProductReview>>(`${V1}/products/${id}/reviews`, { params: { page, pageSize } }).then((r) => r.data),
  createReview: (id: string, payload: { rating: number; comment?: string }) =>
    api.post<ProductReview>(`${V1}/products/${id}/reviews`, payload).then((r) => r.data),

  categories: () => api.get<ProductCategory[]>(`${V1}/products/categories`).then((r) => r.data),
  createCategory: (input: { name: string; slug: string; parentCategoryId?: string }) =>
    api.post<{ id: string }>(`${V1}/products/categories`, input).then((r) => r.data),
};

export const cartApi = {
  get: () => api.get<ServerCart>(`${V1}/cart`).then((r) => r.data),
  add: (productId: string, quantity = 1) =>
    api.post<ServerCart>(`${V1}/cart/items`, { productId, quantity }).then((r) => r.data),
  update: (cartItemId: string, quantity: number) =>
    api.put<ServerCart>(`${V1}/cart/items/${cartItemId}`, { quantity }).then((r) => r.data),
  remove: (cartItemId: string) =>
    api.delete<ServerCart>(`${V1}/cart/items/${cartItemId}`).then((r) => r.data),
  clear: () => api.post(`${V1}/cart/clear`),
};

export const ordersV2Api = {
  checkout: (payload: CheckoutPayload) => api.post<Order>(`${V1}/orders/checkout`, payload).then((r) => r.data),
  mine: (page = 1, pageSize = 20) =>
    api.get<PageResult<Order>>(`${V1}/orders/mine`, { params: { page, pageSize } }).then((r) => r.data),
  get: (id: string) => api.get<Order>(`${V1}/orders/${id}`).then((r) => r.data),
  forStore: (storeId: string, status?: OrderStatus, page = 1, pageSize = 50) =>
    api.get<PageResult<Order>>(`${V1}/orders/store/${storeId}`, { params: { status, page, pageSize } })
       .then((r) => r.data),
  adminAll: (status?: OrderStatus, page = 1, pageSize = 50) =>
    api.get<PageResult<Order>>(`${V1}/orders/admin/all`, { params: { status, page, pageSize } })
       .then((r) => r.data),
  updateStatus: (id: string, status: OrderStatus) =>
    api.put(`${V1}/orders/${id}/status`, { status }),
  updateShipment: (id: string, status: ShipmentStatus, trackingNumber?: string) =>
    api.put(`${V1}/orders/${id}/shipment`, { status, trackingNumber }),
  cancel: (id: string, reason?: string) => api.post(`${V1}/orders/${id}/cancel`, { reason }),
  refund: (id: string, amount?: number) => api.post(`${V1}/orders/${id}/refund`, { amount }),

  createReturn: (orderItemId: string, reason: string) =>
    api.post(`${V1}/orders/returns`, { orderItemId, reason }).then((r) => r.data),
};

export const shippingAddressesApi = {
  list: () => api.get<ShippingAddress[]>(`${V1}/shipping-addresses`).then((r) => r.data),
  create: (input: Omit<ShippingAddress, "id">) =>
    api.post<{ id: string }>(`${V1}/shipping-addresses`, input).then((r) => r.data),
  update: (id: string, input: Omit<ShippingAddress, "id">) =>
    api.put(`${V1}/shipping-addresses/${id}`, input),
  remove: (id: string) => api.delete(`${V1}/shipping-addresses/${id}`),
  setDefault: (id: string) => api.post(`${V1}/shipping-addresses/${id}/default`),
};

export const commissionsApi = {
  list: () => api.get<CommissionConfiguration[]>(`${V1}/admin/commissions`).then((r) => r.data),
  upsert: (input: Omit<CommissionConfiguration, "id" | "storeName" | "categoryName">) =>
    api.post<{ id: string }>(`${V1}/admin/commissions`, input).then((r) => r.data),
  remove: (id: string) => api.delete(`${V1}/admin/commissions/${id}`),
};
