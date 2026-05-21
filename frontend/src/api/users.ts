import { api } from "./client";

export interface PublicUser {
  id: string;
  displayName: string;
  avatarUrl?: string | null;
  primaryRole?: string | null;
}

export interface PublicUserList {
  total: number;
  items: PublicUser[];
}

export interface MyProfile {
  id: string;
  email: string;
  displayName: string;
  phoneNumber?: string | null;
  avatarUrl?: string | null;
  bio?: string | null;
  location?: string | null;
  roles: string[];
}

export interface UpdateMyProfileBody {
  displayName?: string;
  email?: string;
  phoneNumber?: string | null;
  avatarUrl?: string | null;
  bio?: string | null;
  location?: string | null;
}

/**
 * Public directory of users for signed-in callers. Returns only minimal identity
 * (id, displayName, avatarUrl, primaryRole) — the rest lives behind the
 * admin-only /v1/admin/users surface.
 */
export const usersApi = {
  list: (params: { q?: string; page?: number; pageSize?: number } = {}) =>
    api.get<PublicUserList>("/v1/users", { params }).then((r) => r.data),

  me: () => api.get<MyProfile>("/v1/users/me").then((r) => r.data),

  // Profile edits render inline field errors, so opt out of the global toast.
  updateMe: (body: UpdateMyProfileBody) =>
    api.put<MyProfile>("/v1/users/me", body, { skipErrorToast: true }).then((r) => r.data),

  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<void>("/v1/users/me/password", { currentPassword, newPassword }, { skipErrorToast: true }),
};
