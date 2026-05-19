import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { ProductSummary } from "@/api/marketplace";

export interface CartLine {
  productId: string; name: string; price: number;
  image?: string | null; quantity: number; storeName: string;
  stockAvailable?: number;
}

export interface AppliedCoupon { code: string; discount: number }

interface CartState {
  lines: CartLine[];
  appliedCoupon: AppliedCoupon | null;
  add: (p: ProductSummary, qty?: number) => void;
  setQty: (productId: string, qty: number) => void;
  remove: (productId: string) => void;
  clear: () => void;
  setCoupon: (coupon: AppliedCoupon | null) => void;
  count: () => number;
  subtotal: () => number;
}

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      lines: [],
      appliedCoupon: null,
      add: (p, qty = 1) => set((s) => {
        const existing = s.lines.find((l) => l.productId === p.id);
        if (existing) return {
          lines: s.lines.map((l) =>
            l.productId === p.id ? { ...l, quantity: l.quantity + qty, stockAvailable: p.stockQuantity } : l)
        };
        return {
          lines: [...s.lines, {
            productId: p.id, name: p.name,
            price: p.discountPrice ?? p.price,
            image: p.imageUrls[0], quantity: qty,
            storeName: p.storeName,
            stockAvailable: p.stockQuantity
          }]
        };
      }),
      setQty: (id, qty) => set((s) => ({
        lines: qty <= 0 ? s.lines.filter((l) => l.productId !== id)
                        : s.lines.map((l) => l.productId === id ? { ...l, quantity: qty } : l)
      })),
      remove: (id) => set((s) => ({ lines: s.lines.filter((l) => l.productId !== id) })),
      clear: () => set({ lines: [], appliedCoupon: null }),
      setCoupon: (coupon) => set({ appliedCoupon: coupon }),
      count: () => get().lines.reduce((n, l) => n + l.quantity, 0),
      subtotal: () => get().lines.reduce((n, l) => n + l.quantity * l.price, 0)
    }),
    { name: "pawzaroo-cart" }
  )
);
