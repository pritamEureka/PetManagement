import { api } from "./client";
import { getDeviceFingerprint } from "@/lib/deviceId";

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: {
    id: string;
    email: string;
    displayName: string;
    avatarUrl?: string | null;
    roles: string[];
    permissions: string[];
  };
}

export const authApi = {
  login: (email: string, password: string, twoFactorCode?: string) =>
    api
      .post<AuthResponse>("/v1/auth/login", {
        email,
        password,
        twoFactorCode,
        deviceFingerprint: getDeviceFingerprint()
      })
      .then((r) => r.data),

  register: (email: string, password: string, displayName: string, phoneNumber?: string) =>
    api.post<AuthResponse>("/v1/auth/register", { email, password, displayName, phoneNumber }).then((r) => r.data),

  refresh: (refreshToken: string) =>
    api.post<AuthResponse>("/v1/auth/refresh", { refreshToken }).then((r) => r.data),

  logout: (refreshToken: string) => api.post("/v1/auth/logout", { refreshToken }),

  me: () => api.get("/v1/auth/me").then((r) => r.data)
};
