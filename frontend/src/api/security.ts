import { api } from "./client";

// ---------------------------------------------------------------------------
//  Types
// ---------------------------------------------------------------------------

export type ReportTargetType =
  | "Post" | "Comment" | "Message" | "User"
  | "AdoptionListing" | "Product" | "Doctor" | "Store";

export type ReportStatus = "Open" | "UnderReview" | "Resolved" | "Dismissed";

export type ModerationActionType =
  | "Warn" | "Suspend" | "Ban" | "Hide" | "Restore"
  | "Approve" | "Reject" | "MarkSuspicious" | "Escalate" | "Unhide";

export type ModerationTargetType =
  | "Post" | "Comment" | "Message" | "User"
  | "AdoptionListing" | "Product" | "Doctor" | "Store" | "Review";

export type WarningSeverity = "Info" | "Minor" | "Major" | "Final";
export type SuspensionStatus = "Active" | "Lifted" | "Expired";
export type OtpPurpose = "EmailVerification" | "PhoneVerification" | "PasswordReset" | "TwoFactor";

export interface ContentReport {
  id: string;
  targetType: ReportTargetType;
  targetId: string;
  reporterId: string;
  reporterDisplayName: string;
  reason: string;
  details?: string | null;
  status: ReportStatus;
  resolvedById?: string | null;
  resolutionNotes?: string | null;
  resolvedAt?: string | null;
  createdAt: string;
}

export interface ModerationAction {
  id: string;
  action: ModerationActionType;
  targetType: ModerationTargetType;
  targetId: string;
  moderatorId: string;
  moderatorName: string;
  notes?: string | null;
  relatedSuspensionId?: string | null;
  relatedWarningId?: string | null;
  createdAt: string;
}

export interface UserWarning {
  id: string;
  userId: string;
  severity: WarningSeverity;
  reason: string;
  message?: string | null;
  acknowledgedByUser: boolean;
  createdAt: string;
}

export interface UserSuspension {
  id: string;
  userId: string;
  reason: string;
  details?: string | null;
  isBan: boolean;
  expiresAt?: string | null;
  status: SuspensionStatus;
  issuedById: string;
  createdAt: string;
}

export interface UserDevice {
  id: string;
  fingerprint: string;
  label?: string | null;
  userAgent?: string | null;
  ipAddress?: string | null;
  ipCity?: string | null;
  ipCountry?: string | null;
  firstSeenAt: string;
  lastSeenAt: string;
  isTrusted: boolean;
  isRevoked: boolean;
}

export interface SecuritySelf {
  userId: string;
  email: string;
  permissions: string[];
  twoFactorEnabled: boolean;
  activeSuspension: UserSuspension | null;
  pendingWarnings: UserWarning[];
}

const V1 = "/v1";

// ---------------------------------------------------------------------------
//  API surface
// ---------------------------------------------------------------------------

export const reportsApi = {
  create: (input: {
    targetType: ReportTargetType;
    targetId: string;
    reason: string;
    details?: string;
  }) => api.post<{ id: string }>(`${V1}/reports`, input).then((r) => r.data),

  list: (params: { status?: ReportStatus; targetType?: ReportTargetType; page?: number; pageSize?: number } = {}) =>
    api.get<ContentReport[]>(`${V1}/reports`, { params }).then((r) => r.data),

  get: (id: string) => api.get<ContentReport>(`${V1}/reports/${id}`).then((r) => r.data),

  setStatus: (id: string, status: ReportStatus, notes?: string) =>
    api.put(`${V1}/reports/${id}/status`, { status, notes })
};

export const moderationApi = {
  act: (input: {
    action: ModerationActionType;
    targetType: ModerationTargetType;
    targetId: string;
    reportId?: string;
    notes?: string;
    suspendUntil?: string;
    isBan?: boolean;
    warningSeverity?: WarningSeverity;
  }) => api.post<ModerationAction>(`${V1}/moderation/actions`, input).then((r) => r.data),

  history: (targetType: ModerationTargetType, targetId: string) =>
    api.get<ModerationAction[]>(`${V1}/moderation/targets/${targetType}/${targetId}/history`).then((r) => r.data),

  suspend: (userId: string, body: { reason: string; details?: string; expiresAt?: string; isBan?: boolean }) =>
    api.post<{ suspensionId: string }>(`${V1}/moderation/users/${userId}/suspend`, body).then((r) => r.data),
  lift: (suspensionId: string, notes?: string) =>
    api.post(`${V1}/moderation/suspensions/${suspensionId}/lift`, { notes }),
  warn: (userId: string, body: {
    severity: WarningSeverity; reason: string; message?: string;
    relatedContentType?: string; relatedContentId?: string;
  }) => api.post<{ warningId: string }>(`${V1}/moderation/users/${userId}/warn`, body).then((r) => r.data),
  userWarnings: (userId: string) =>
    api.get<UserWarning[]>(`${V1}/moderation/users/${userId}/warnings`).then((r) => r.data),
  activeSuspension: (userId: string) =>
    api.get<UserSuspension>(`${V1}/moderation/users/${userId}/active-suspension`).then((r) => r.data),

  adminActions: (page = 1, pageSize = 50) =>
    api.get(`${V1}/moderation/admin-actions`, { params: { page, pageSize } }).then((r) => r.data)
};

export const securityApi = {
  me: () => api.get<SecuritySelf>(`${V1}/security/me`).then((r) => r.data),

  devices: () => api.get<UserDevice[]>(`${V1}/security/devices`).then((r) => r.data),
  revokeDevice: (id: string) => api.post(`${V1}/security/devices/${id}/revoke`),
  trustDevice: (id: string, label?: string) => api.post(`${V1}/security/devices/${id}/trust`, { label }),

  myWarnings: () => api.get<UserWarning[]>(`${V1}/security/warnings`).then((r) => r.data),
  ackWarning: (id: string) => api.post(`${V1}/security/warnings/${id}/ack`),

  issueOtp: (purpose: OtpPurpose, destination: string) =>
    api.post(`${V1}/security/otp/issue`, { purpose, destination }),
  verifyOtp: (purpose: OtpPurpose, destination: string, code: string) =>
    api.post<{ verified: boolean }>(`${V1}/security/otp/verify`, { purpose, destination, code }).then((r) => r.data),

  begin2FA: () => api.post<{ secretBase32: string; otpAuthUri: string; recoveryCodes: string[] }>(`${V1}/security/2fa/setup`).then((r) => r.data),
  enable2FA: (code: string) => api.post(`${V1}/security/2fa/enable`, { code }),
  disable2FA: (code: string) => api.post(`${V1}/security/2fa/disable`, { code })
};
