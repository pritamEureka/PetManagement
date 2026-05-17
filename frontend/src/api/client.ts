import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import { useAuthStore } from "@/store/authStore";

export const apiBase = import.meta.env.VITE_API_BASE ?? "/api";

export const api = axios.create({ baseURL: apiBase });

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = useAuthStore.getState().accessToken;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

let refreshing: Promise<string | null> | null = null;

api.interceptors.response.use(
  (r) => r,
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
    return Promise.reject(error);
  }
);
