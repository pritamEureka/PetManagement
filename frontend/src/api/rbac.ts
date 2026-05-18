import { api } from "./client";

export interface PermissionDto {
  id: string;
  module: string;
  action: string;
  code: string;
  description?: string | null;
}

export interface RoleDto {
  id: string;
  name: string;
  description?: string | null;
  isSystem: boolean;
  permissions: string[];
}

export interface UserRolesView {
  userId: string;
  roles: { roleId: string; name: string; assignedAt: string }[];
  permissions: string[];
}

export const rbacApi = {
  listPermissions: () => api.get<PermissionDto[]>("/v1/permissions").then((r) => r.data),
  myPermissions: () => api.get<{ userId: string; permissions: string[] }>("/v1/permissions/mine").then((r) => r.data),

  listRoles: () => api.get<RoleDto[]>("/v1/roles").then((r) => r.data),
  getRole: (id: string) => api.get<RoleDto>(`/v1/roles/${id}`).then((r) => r.data),
  createRole: (data: { name: string; description?: string; permissions: string[] }) =>
    api.post<{ id: string }>("/v1/roles", data).then((r) => r.data),
  updateRole: (id: string, data: { description?: string; permissions?: string[] }) =>
    api.put(`/v1/roles/${id}`, data),
  deleteRole: (id: string) => api.delete(`/v1/roles/${id}`),

  userRoles: (userId: string) => api.get<UserRolesView>(`/v1/user-roles/user/${userId}`).then((r) => r.data),
  assignRole: (userId: string, roleId: string) => api.post("/v1/user-roles/assign", { userId, roleId }),
  revokeRole: (userId: string, roleId: string) => api.post("/v1/user-roles/revoke", { userId, roleId }),
};
