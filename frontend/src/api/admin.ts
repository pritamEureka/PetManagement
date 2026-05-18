import { api } from "./client";

export interface AdminUser {
  id: string; email: string; displayName: string;
  isActive: boolean; isSuspended: boolean;
  createdAt: string;
  roles: string[];
  approvalStatus?: "Pending" | "Approved" | "Rejected";
}

export interface AdminUserDetail extends AdminUser {
  avatarUrl?: string | null;
  phoneNumber?: string | null;
  emailConfirmed: boolean;
  rejectionReason?: string | null;
  lastLoginAt?: string | null;
  postCount: number;
  orderCount: number;
  adoptionListingCount: number;
}

interface ListResponse { total: number; items: AdminUser[]; }

export interface CreateUserInput {
  email: string;
  password: string;
  displayName: string;
  phoneNumber?: string;
  roles: string[];
}

export interface UpdateUserInput {
  displayName?: string;
  phoneNumber?: string | null;
  isActive?: boolean;
}

// The legacy /api/admin/* shim is bypassed: every call here points straight at
// the V1 surface. The global response interceptor already unwraps the envelope.
export const adminApi = {
  dashboard: () => api.get<Record<string, number>>("/v1/admin/overview").then((r) => r.data),

  users: (params: { q?: string; page?: number; pageSize?: number; approvalStatus?: "Pending" | "Approved" | "Rejected" } = {}) =>
    api.get<ListResponse>("/v1/admin/users", { params }).then((r) => r.data),

  user: (id: string) =>
    api.get<AdminUserDetail>(`/v1/admin/users/${id}`).then((r) => r.data),

  createUser: (input: CreateUserInput) =>
    api.post<AdminUserDetail>("/v1/admin/users", input).then((r) => r.data),

  updateUser: (id: string, input: UpdateUserInput) =>
    api.put(`/v1/admin/users/${id}`, input),

  // Permanent delete — backend requires the caller to hold the SuperAdmin role.
  deleteUser: (id: string) =>
    api.delete(`/v1/admin/users/${id}`),

  grantRole: (id: string, roleName: string) =>
    api.post(`/v1/admin/users/${id}/roles`, { roleName }),

  revokeRole: (id: string, roleName: string) =>
    api.delete(`/v1/admin/users/${id}/roles/${encodeURIComponent(roleName)}`),

  // Suspend via the moderation surface. UsersPage only collects a confirm() click,
  // so we send a generic reason; richer flows should call the moderation API directly.
  suspend: (id: string, reason = "Suspended by admin") =>
    api.post(`/v1/moderation/users/${id}/suspend`, { reason }),

  // Restore = lift the active suspension. We look up the suspension first since
  // the moderation API keys lifts by suspensionId, not userId. A 404 just means
  // "no active suspension" — opt out of the global error toast for that probe.
  restore: async (id: string) => {
    try {
      const active = await api
        .get<{ id: string }>(`/v1/moderation/users/${id}/active-suspension`, { skipErrorToast: true })
        .then((r) => r.data);
      if (!active?.id) return;
      await api.post(`/v1/moderation/suspensions/${active.id}/lift`, { notes: null });
    } catch (err: any) {
      if (err?.response?.status === 404) return; // not currently suspended
      throw err;
    }
  }
};
