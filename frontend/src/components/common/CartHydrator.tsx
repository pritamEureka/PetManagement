import { useEffect } from "react";
import { useAuthStore } from "@/store/authStore";
import { useCartStore } from "@/store/cartStore";

/**
 * Pulls the server cart into the local Zustand store whenever the user goes
 * from anonymous → authenticated (e.g. login, refresh, switching accounts).
 * When the token is cleared we drop any stale local lines so the cart icon
 * doesn't briefly show the previous user's items.
 *
 * Renders nothing — meant to live inside the Router tree so the hooks work.
 */
export function CartHydrator() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const hydrate = useCartStore((s) => s.hydrate);
  const resetLocal = useCartStore((s) => s.resetLocal);

  useEffect(() => {
    if (accessToken) {
      hydrate().catch(() => {/* anonymous or transient — empty cart is the safe default */});
    } else {
      resetLocal();
    }
  }, [accessToken, hydrate, resetLocal]);

  return null;
}
