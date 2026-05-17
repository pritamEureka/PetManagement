import { api } from "./client";

export interface AdminUser {
  id: string; email: string; displayName: string;
  isActive: boolean; isSuspended: boolean;
  createdAt: string;
  roles: string[];
}

interface ListResponse { total: number; items: AdminUser[]; }

const unwrap = <T,>(p: Promise<{ data: { data: T } | T }>) =>
  p.then((r) => {
    const body: any = r.data;
    return (body && typeof body === "object" && "data" in body ? body.data : body) as T;
  });

export const adminApi = {
  dashboard: () => unwrap<Record<string, number>>(api.get("/admin/dashboard")),
  users: (params: { q?: string; page?: number; pageSize?: number } = {}) =>
    unwrap<ListResponse>(api.get("/admin/users", { params })),
  suspend: (id: string) => api.post(`/admin/users/${id}/suspend`),
  restore: (id: string) => api.post(`/admin/users/${id}/restore`)
};
