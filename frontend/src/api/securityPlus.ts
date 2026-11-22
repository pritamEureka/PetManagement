import { api } from "@/api/client";
import type { AuthResponse } from "@/api/auth";
import type { CreationOptionsJson, RequestOptionsJson } from "@/lib/webauthn";
import { getDeviceFingerprint } from "@/lib/deviceId";

export interface SessionDto {
  id: string;
  createdAt: string;
  expiresAt: string;
  ipAddress?: string | null;
  deviceLabel?: string | null;
  deviceId?: string | null;
  deviceTrusted: boolean;
  lastSeenAt?: string | null;
}
export interface RecoveryStatus { remaining: number; total: number; lastGeneratedAt?: string | null }
export interface RecoveryCodes { codes: string[]; remaining: number }
export interface ConsentDto {
  id: string; consentType: string; version?: string | null; granted: boolean;
  ipAddress?: string | null; recordedAt: string;
}

const base = "/v1/security-plus";

export const securityPlusApi = {
  sessions: () => api.get<SessionDto[]>(`${base}/sessions`).then((r) => r.data),
  currentSession: (refreshToken: string) =>
    api.post<{ id: string | null }>(`${base}/sessions/current`, { refreshToken }).then((r) => r.data.id),
  revoke: (id: string) => api.post(`${base}/sessions/${id}/revoke`),
  revokeAll: () => api.post<{ revoked: number }>(`${base}/sessions/revoke-all`).then((r) => r.data),
  setTrusted: (deviceId: string, trusted: boolean) =>
    api.put(`${base}/devices/${deviceId}/trusted`, { trusted }),
  stepUp: (input: { password?: string; otpCode?: string }) =>
    api.post<{ verified: boolean; validForSeconds: number }>(`${base}/step-up`, input, { skipErrorToast: true })
      .then((r) => r.data),
  recoveryStatus: () => api.get<RecoveryStatus>(`${base}/recovery-codes`).then((r) => r.data),
  regenerateRecovery: () =>
    api.post<RecoveryCodes>(`${base}/recovery-codes/regenerate`, undefined, { skipErrorToast: true })
      .then((r) => r.data),
  consents: () => api.get<ConsentDto[]>(`${base}/consents`).then((r) => r.data),
  recordConsent: (consentType: string, granted: boolean, version?: string) =>
    api.post<ConsentDto>(`${base}/consents`, { consentType, granted, version }).then((r) => r.data),
};

// ---- Passkeys (WebAuthn) + account recovery

export interface PasskeyDto { id: string; label: string; createdAt: string; lastUsedAt?: string | null }
type Attestation = { id: string; response: { clientDataJSON: string; attestationObject: string } };
type Assertion = {
  id: string;
  response: { clientDataJSON: string; authenticatorData: string; signature: string; userHandle?: string };
};
const noToast = { skipErrorToast: true } as const;

export const passkeyApi = {
  registerOptions: () => api.post<CreationOptionsJson>(`${base}/passkeys/register/options`).then((r) => r.data),
  registerVerify: (cred: Attestation, label?: string) =>
    api.post<PasskeyDto>(`${base}/passkeys/register/verify`, { ...cred, label }, noToast).then((r) => r.data),
  list: () => api.get<PasskeyDto[]>(`${base}/passkeys`).then((r) => r.data),
  remove: (id: string) => api.delete(`${base}/passkeys/${id}`),
  loginOptions: (email: string) =>
    api.post<RequestOptionsJson>(`${base}/passkeys/login/options`, { email }, noToast).then((r) => r.data),
  loginVerify: (cred: Assertion) =>
    api.post<AuthResponse>(`${base}/passkeys/login/verify`,
      { ...cred, deviceFingerprint: getDeviceFingerprint() }, noToast).then((r) => r.data),
};

export const recoveryApi = {
  start: (email: string) => api.post(`${base}/recovery/start`, { email }, noToast),
  complete: (input: { token?: string; email?: string; recoveryCode?: string; newPassword: string }) =>
    api.post<{ reset: boolean }>(`${base}/recovery/complete`, input, noToast).then((r) => r.data),
};
