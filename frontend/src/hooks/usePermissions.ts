import { useMemo } from "react";
import { useAuthStore } from "@/store/authStore";

/**
 * Single source of truth for "can the current user do X?".
 * SuperAdmin short-circuits to true. Otherwise we check the permissions
 * embedded in the JWT (already loaded into the auth store on login).
 */
export function usePermissions() {
  const user = useAuthStore((s) => s.user);

  return useMemo(() => {
    const isSuperAdmin = !!user?.roles.includes("SuperAdmin");
    const set = new Set(user?.permissions ?? []);

    const can = (perm: string | string[]) => {
      if (isSuperAdmin) return true;
      if (typeof perm === "string") return set.has(perm);
      return perm.every((p) => set.has(p));
    };
    const canAny = (perms: string[]) => isSuperAdmin || perms.some((p) => set.has(p));
    const hasRole = (...roles: string[]) => !!user?.roles.some((r) => roles.includes(r));
    return { can, canAny, hasRole, isSuperAdmin, roles: user?.roles ?? [], permissions: user?.permissions ?? [] };
  }, [user]);
}
