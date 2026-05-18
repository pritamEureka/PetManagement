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

/**
 * Public directory of users for signed-in callers. Returns only minimal identity
 * (id, displayName, avatarUrl, primaryRole) — the rest lives behind the
 * admin-only /v1/admin/users surface.
 */
export const usersApi = {
  list: (params: { q?: string; page?: number; pageSize?: number } = {}) =>
    api.get<PublicUserList>("/v1/users", { params }).then((r) => r.data),
};
