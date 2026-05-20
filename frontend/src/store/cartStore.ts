import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { ProductSummary, ServerCart } from "@/api/marketplace";
import { cartApi } from "@/api/marketplace";

export interface CartLine {
  /** Server-side row id — used by setQty/remove/clear. Null only between hydration and the first sync. */
  cartItemId?: string;
  productId: string; name: string; price: number;
  image?: string | null; quantity: number; storeName: string;
  stockAvailable?: number;
}

export interface AppliedCoupon { code: string; discount: number }

interface CartState {
  lines: CartLine[];
  appliedCoupon: AppliedCoupon | null;

  /** Pull the authoritative cart from the server. Call after login/app mount. */
  hydrate: () => Promise<void>;

  add: (p: ProductSummary, qty?: number) => Promise<void>;
  setQty: (productId: string, qty: number) => Promise<void>;
  remove: (productId: string) => Promise<void>;
  clear: () => Promise<void>;
  /** Local-only reset (used after a successful checkout — server cart was already cleared in the transaction). */
  resetLocal: () => void;

  setCoupon: (coupon: AppliedCoupon | null) => void;
  count: () => number;
  subtotal: () => number;
}

function fromServer(cart: ServerCart): CartLine[] {
  return cart.items.map((it) => ({
    cartItemId: it.id,
    productId: it.productId,
    name: it.productName,
    image: it.imageUrl ?? null,
    price: it.unitPrice,
    quantity: it.quantity,
    storeName: it.storeName,
    stockAvailable: it.stockAvailable
  }));
}

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      lines: [],
      appliedCoupon: null,

      // Pull the server cart. Swallow auth errors — anonymous visitors just see
      // an empty cart, which is the correct UX. Other errors surface to callers
      // (App.tsx wraps this in a try/catch that ignores them).
      hydrate: async () => {
        const cart = await cartApi.get();
        set({ lines: fromServer(cart) });
      },

      // Server is the source of truth. We update local state from the response
      // so the cart icon / page reflects exact server state (incl. cartItemId
      // for subsequent quantity edits).
      add: async (p, qty = 1) => {
        const cart = await cartApi.add(p.id, qty);
        set({ lines: fromServer(cart) });
      },

      setQty: async (productId, qty) => {
        const line = get().lines.find((l) => l.productId === productId);
        if (!line?.cartItemId) {
          // Defensive: shouldn't happen post-hydrate, but if it does, fall back
          // to optimistic local-only change so the UI isn't frozen.
          set((s) => ({
            lines: qty <= 0 ? s.lines.filter((l) => l.productId !== productId)
                            : s.lines.map((l) => l.productId === productId ? { ...l, quantity: qty } : l)
          }));
          return;
        }
        const cart = qty <= 0
          ? await cartApi.remove(line.cartItemId)
          : await cartApi.update(line.cartItemId, qty);
        set({ lines: fromServer(cart) });
      },

      remove: async (productId) => {
        const line = get().lines.find((l) => l.productId === productId);
        if (!line?.cartItemId) {
          set((s) => ({ lines: s.lines.filter((l) => l.productId !== productId) }));
          return;
        }
        const cart = await cartApi.remove(line.cartItemId);
        set({ lines: fromServer(cart) });
      },

      clear: async () => {
        await cartApi.clear();
        set({ lines: [], appliedCoupon: null });
      },

      resetLocal: () => set({ lines: [], appliedCoupon: null }),

      setCoupon: (coupon) => set({ appliedCoupon: coupon }),
      count: () => get().lines.reduce((n, l) => n + l.quantity, 0),
      subtotal: () => get().lines.reduce((n, l) => n + l.quantity * l.price, 0)
    }),
    {
      name: "pawzaroo-cart",
      // Don't persist the line items — the server is authoritative. We only
      // persist the applied coupon so a page refresh keeps the UI in sync
      // with what will be sent to checkout.
      partialize: (s) => ({ appliedCoupon: s.appliedCoupon }) as Partial<CartState>
    }
  )
);
