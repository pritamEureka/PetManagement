import { api } from "./client";

export interface AdminUser {
  id: string; email: string; displayName: string;
  isActive: boolean; isSuspended: boolean;
  createdAt: string;
  roles: string[];
}

interface ListResponse { total: number; items: AdminUser[]; }

// The legacy /api/admin/* shim is bypassed: every call here points straight at
// the V1 surface. The global response interceptor already unwraps the envelope.
export const adminApi = {
  dashboard: () => api.get<Record<string, number>>("/v1/admin/overview").then((r) => r.data),

  users: (params: { q?: string; page?: number; pageSize?: number } = {}) =>
    api.get<ListResponse>("/v1/admin/users", { params }).then((r) => r.data),

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
