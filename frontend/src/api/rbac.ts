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

const unwrap = <T,>(p: Promise<{ data: { data: T } }>) => p.then((r) => r.data.data);

export const rbacApi = {
  listPermissions: () => unwrap<PermissionDto[]>(api.get("/v1/permissions")),
  myPermissions: () => unwrap<{ userId: string; permissions: string[] }>(api.get("/v1/permissions/mine")),

  listRoles: () => unwrap<RoleDto[]>(api.get("/v1/roles")),
  getRole: (id: string) => unwrap<RoleDto>(api.get(`/v1/roles/${id}`)),
  createRole: (data: { name: string; description?: string; permissions: string[] }) =>
    unwrap<{ id: string }>(api.post("/v1/roles", data)),
  updateRole: (id: string, data: { description?: string; permissions?: string[] }) =>
    api.put(`/v1/roles/${id}`, data),
  deleteRole: (id: string) => api.delete(`/v1/roles/${id}`),

  userRoles: (userId: string) => unwrap<UserRolesView>(api.get(`/v1/user-roles/user/${userId}`)),
  assignRole: (userId: string, roleId: string) => api.post("/v1/user-roles/assign", { userId, roleId }),
  revokeRole: (userId: string, roleId: string) => api.post("/v1/user-roles/revoke", { userId, roleId }),
};
