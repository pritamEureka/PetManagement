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

// Register no longer auto-logs in — every new account starts Pending and must
// be approved by an admin before it can sign in.
export interface RegistrationResult {
  userId: string;
  email: string;
  status: "Pending" | "Approved" | "Rejected";
  message: string;
}

// Auth flows render their own error UI (2FA prompt, inline messages),
// so opt out of the global error toast on these calls.
const noToast = { skipErrorToast: true } as const;

export const authApi = {
  login: (email: string, password: string, twoFactorCode?: string) =>
    api
      .post<AuthResponse>(
        "/v1/auth/login",
        { email, password, twoFactorCode, deviceFingerprint: getDeviceFingerprint() },
        noToast
      )
      .then((r) => r.data),

  register: (email: string, password: string, displayName: string, phoneNumber?: string, requestedRole?: string) =>
    api.post<RegistrationResult>("/v1/auth/register", { email, password, displayName, phoneNumber, requestedRole }, noToast).then((r) => r.data),

  refresh: (refreshToken: string) =>
    api.post<AuthResponse>("/v1/auth/refresh", { refreshToken }, noToast).then((r) => r.data),

  logout: (refreshToken: string) => api.post("/v1/auth/logout", { refreshToken }, noToast),

  me: () => api.get("/v1/auth/me").then((r) => r.data)
};
