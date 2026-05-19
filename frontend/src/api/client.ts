import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { toast } from "sonner";
import { useAuthStore } from "@/store/authStore";

export const apiBase = import.meta.env.VITE_API_BASE ?? "/api";

// CSRF posture: authentication is bearer-token via the Authorization header,
// NOT cookies. The browser doesn't auto-attach bearer tokens, so classic CSRF
// (where a third-party site triggers a cross-origin request that carries the
// victim's cookie) is structurally not exploitable for state-changing calls.
// If we ever migrate tokens to HttpOnly cookies (see vulnerabilities.md C-06)
// we MUST add CSRF token handling here (double-submit cookie pattern).
export const api = axios.create({ baseURL: apiBase });

// Set `skipErrorToast: true` on a request config to opt out of the global error toast
// (use when the caller wants to render its own error UI, e.g. inline form errors).
declare module "axios" {
  export interface AxiosRequestConfig {
    skipErrorToast?: boolean;
  }
}

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = useAuthStore.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

let refreshing: Promise<string | null> | null = null;

api.interceptors.response.use(
  (r) => {
    // Backend wraps every action result in ApiResponse<T> { success, data, error, correlationId, timestamp }.
    // Unwrap here so callers see the inner payload directly.
    const body = r.data as Record<string, unknown> | undefined;
    if (
      body && typeof body === "object" &&
      typeof body.success === "boolean" &&
      "data" in body && "timestamp" in body
    ) {
      r.data = body.data as never;
    }
    return r;
  },
  async (error: AxiosError) => {
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // Suspended-account guard: server returns 403 { error: { code: "account_suspended", message } }.
    if (error.response?.status === 403) {
      const body = error.response.data as { error?: { code?: string; message?: string } } | undefined;
      if (body?.error?.code === "account_suspended") {
        useAuthStore.getState().setSuspensionMessage(body.error.message ?? "Your account is suspended.");
        if (typeof window !== "undefined" && !window.location.pathname.startsWith("/account/suspended")) {
          window.location.assign("/account/suspended");
        }
        return Promise.reject(error);
      }
    }

    if (error.response?.status === 401 && !original._retry) {
      original._retry = true;
      try {
        if (!refreshing) refreshing = useAuthStore.getState().refresh();
        const token = await refreshing;
        refreshing = null;
        if (token) {
          original.headers.Authorization = `Bearer ${token}`;
          return api.request(original);
        }
      } catch {
        useAuthStore.getState().logout();
      }
    }

    // Global error toast: surface the backend's ApiError.message for every
    // failed request except 401 (handled above) and opt-outs (skipErrorToast).
    const status = error.response?.status;
    const shouldToast = status !== 401 && !original?.skipErrorToast;
    if (shouldToast) {
      const body = error.response?.data as { error?: { message?: string; code?: string } } | undefined;
      const msg = body?.error?.message ?? error.message ?? "Request failed.";
      toast.error(msg, { id: `api:${original?.method ?? ""}:${original?.url ?? ""}:${status ?? ""}` });
    }

    return Promise.reject(error);
  }
);
