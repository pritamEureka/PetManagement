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

const unwrap = <T,>(p: Promise<{ data: { data: T } }>) => p.then((r) => r.data.data);

export const productsApi = {
  list: (params: { q?: string; categoryId?: string; brandId?: string; featured?: boolean; page?: number; pageSize?: number } = {}) =>
    unwrap<Product[]>(api.get("/products", { params })),

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
    unwrap<{ id: string; orderNumber: string; total: number }>(api.post("/orders/checkout", data)),
  mine: () => unwrap<Order[]>(api.get("/orders/mine")),
  refund: (id: string) => api.post(`/orders/${id}/refund`)
};
