import { create } from "zustand";
import { persist } from "zustand/middleware";
import { authApi, type AuthResponse, type RegistrationResult } from "@/api/auth";

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  expiresAt: string | null;
  user: AuthResponse["user"] | null;

  /** Set by login() if the API replied "two_factor_required" so the form can prompt. */
  needsTwoFactor: boolean;
  /** Suspended message captured from the server middleware (account_suspended). */
  suspensionMessage: string | null;

  login: (email: string, password: string, twoFactorCode?: string) => Promise<void>;
  register: (email: string, password: string, displayName: string, phoneNumber?: string, requestedRole?: string) => Promise<RegistrationResult>;
  refresh: () => Promise<string | null>;
  logout: () => Promise<void>;
  setSuspensionMessage: (msg: string | null) => void;
  clearTwoFactor: () => void;
  /** Merge fresh user fields after a self-service profile edit (display name, email, avatarUrl, ...). */
  patchUser: (patch: Partial<NonNullable<AuthResponse["user"]>>) => void;
  hasPermission: (perm: string) => boolean;
  hasAnyRole: (...roles: string[]) => boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      expiresAt: null,
      user: null,
      needsTwoFactor: false,
      suspensionMessage: null,

      async login(email, password, twoFactorCode) {
        try {
          const res = await authApi.login(email, password, twoFactorCode);
          set({
            accessToken: res.accessToken,
            refreshToken: res.refreshToken,
            expiresAt: res.expiresAt,
            user: res.user,
            needsTwoFactor: false,
            suspensionMessage: null
          });
        } catch (err: any) {
          const msg = err?.response?.data?.error?.message ?? "";
          if (typeof msg === "string" && msg.includes("two_factor_required")) {
            set({ needsTwoFactor: true });
          }
          throw err;
        }
      },

      async register(email, password, displayName, phoneNumber, requestedRole) {
        // Registration no longer issues tokens — the new account is Pending
        // approval. We return the server's status payload so the page can
        // route the user to a "request received" screen.
        return await authApi.register(email, password, displayName, phoneNumber, requestedRole);
      },

      async refresh() {
        const rt = get().refreshToken;
        if (!rt) return null;
        try {
          const res = await authApi.refresh(rt);
          set({
            accessToken: res.accessToken,
            refreshToken: res.refreshToken,
            expiresAt: res.expiresAt,
            user: res.user
          });
          return res.accessToken;
        } catch {
          set({ accessToken: null, refreshToken: null, expiresAt: null, user: null });
          return null;
        }
      },

      async logout() {
        const rt = get().refreshToken;
        try { if (rt) await authApi.logout(rt); } catch { /* ignore */ }
        set({
          accessToken: null,
          refreshToken: null,
          expiresAt: null,
          user: null,
          needsTwoFactor: false,
          suspensionMessage: null
        });
      },

      setSuspensionMessage: (msg) => set({ suspensionMessage: msg }),
      clearTwoFactor:       ()    => set({ needsTwoFactor: false }),
      patchUser: (patch) => {
        const u = get().user;
        if (!u) return;
        set({ user: { ...u, ...patch } });
      },

      hasPermission: (perm) => {
        const u = get().user;
        if (!u) return false;
        if (u.roles.includes("SuperAdmin")) return true;
        return u.permissions.includes(perm);
      },
      hasAnyRole: (...roles) => !!get().user?.roles.some((r) => roles.includes(r))
    }),
    {
      name: "pawzaroo-auth",
      // Don't persist transient flags across reloads — they're per-session signals.
      partialize: (s) => ({
        accessToken: s.accessToken,
        refreshToken: s.refreshToken,
        expiresAt: s.expiresAt,
        user: s.user
      })
    }
  )
);
