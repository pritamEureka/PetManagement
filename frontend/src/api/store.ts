import { api } from "./client";
import type { CheckoutInput } from "@/lib/schemas";

export interface Product {
  id: string; name: string; sku: string; price: number; discountPrice?: number | null;
  stockQuantity: number; isFeatured: boolean;
  ratingAverage: number; ratingCount: number;
  store: { id: string; name: string };
  images: string[];
}

export interface Order {
  id: string; orderNumber: string; total: number;
  status: string; paymentStatus: string; shipmentStatus: string;
  createdAt: string;
  items: { productId: string; quantity: number; unitPrice: number; total: number }[];
}

export const productsApi = {
  list: (params: { q?: string; categoryId?: string; brandId?: string; featured?: boolean; page?: number; pageSize?: number } = {}) =>
    api.get<Product[]>("/products", { params }).then((r) => r.data),

  /**
   * GET /products/{id} isn't exposed yet. Falls back to filtering the list call;
   * swap to a single endpoint when it lands.
   */
  getById: async (id: string): Promise<Product | null> => {
    const items = await productsApi.list({ page: 1, pageSize: 100 });
    return items.find((p) => p.id === id) ?? null;
  }
};

export const ordersApi = {
  checkout: (data: CheckoutInput) =>
    api.post<{ id: string; orderNumber: string; total: number }>("/orders/checkout", data).then((r) => r.data),
  mine: () => api.get<Order[]>("/orders/mine").then((r) => r.data),
  refund: (id: string) => api.post(`/orders/${id}/refund`)
};
