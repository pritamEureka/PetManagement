/**
 * Admin V2 API surface. Maps every endpoint added under /api/v1/admin/*
 * plus the cross-module reads the dashboard pages depend on.
 *
 * Existing modules keep their own clients (storesApi, productsV2Api, etc.) —
 * this file only owns the admin-specific endpoints.
 */

import { api } from "./client";

const V1 = "/v1";

// ---------------------------------------------------------------------------
//  Types
// ---------------------------------------------------------------------------

export interface AdminOverview {
  totalUsers: number;
  activeUsers: number;
  newUsersToday: number;
  pendingDoctorApprovals: number;
  pendingStoreApprovals: number;
  pendingAdoptionListings: number;
  newFeedPostsToday: number;
  openReports: number;
  totalAppointments: number;
  appointmentsToday: number;
  totalOrders: number;
  totalRevenue: number;
  commissionEarned: number;
  activeStores: number;
  activeDoctors: number;
}

export interface DailyPoint {
  date: string;
  users: number;
  orders: number;
  revenue: number;
}

export interface TopAnimal { type: string; listings: number; adopted: number }
export interface TopProduct { productId: string; name: string; unitsSold: number; revenue: number }

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  isActive: boolean;
  isSuspended: boolean;
  emailConfirmed: boolean;
  createdAt: string;
  lastLoginAt?: string | null;
  roles: string[];
}

export interface AdminUserDetail extends AdminUser {
  phoneNumber?: string | null;
  postCount: number;
  orderCount: number;
  adoptionListingCount: number;
}

export interface AdminUserListResponse {
  items: AdminUser[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AdminAppointment {
  id: string;
  doctorId: string;
  doctorName: string;
  userId: string;
  userDisplayName: string;
  scheduledAt: string;
  status: string;
  paymentStatus: string;
  amount: number;
  createdAt: string;
}

export interface AdminNotification {
  id: string;
  userId: string;
  userDisplayName: string;
  title: string;
  body: string;
  isRead: boolean;
  createdAt: string;
}

export interface SystemSettingRow {
  key: string;
  category: string;
  valueJson: string;
  description?: string | null;
  isSecret: boolean;
  updatedAt?: string | null;
}

// ---------------------------------------------------------------------------
//  API
// ---------------------------------------------------------------------------

export const adminApi = {
  overview: () => api.get<AdminOverview>(`${V1}/admin/overview`).then((r) => r.data),
  series:   (days = 30) => api.get<DailyPoint[]>(`${V1}/admin/series`, { params: { days } }).then((r) => r.data),
  top:      (days = 30) =>
    api.get<{ animals: TopAnimal[]; products: TopProduct[] }>(`${V1}/admin/top`, { params: { days } })
       .then((r) => r.data),

  users: {
    list: (params: { q?: string; role?: string; suspended?: boolean; active?: boolean; page?: number; pageSize?: number } = {}) =>
      api.get<AdminUserListResponse>(`${V1}/admin/users`, { params }).then((r) => r.data),
    get: (id: string) => api.get<AdminUserDetail>(`${V1}/admin/users/${id}`).then((r) => r.data),
    grantRole: (id: string, roleName: string) => api.post(`${V1}/admin/users/${id}/roles`, { roleName }),
    revokeRole: (id: string, roleName: string) => api.delete(`${V1}/admin/users/${id}/roles/${roleName}`),
    forceLogout: (id: string) => api.post(`${V1}/admin/users/${id}/force-logout`)
  },

  appointments: (params: {
    status?: string; from?: string; to?: string; q?: string; page?: number; pageSize?: number;
  } = {}) =>
    api.get<{ items: AdminAppointment[]; total: number }>(`${V1}/admin/appointments`, { params }).then((r) => r.data),

  notifications: {
    list: (params: { userId?: string; unreadOnly?: boolean; page?: number; pageSize?: number } = {}) =>
      api.get<AdminNotification[]>(`${V1}/admin/notifications`, { params }).then((r) => r.data),
    broadcast: (input: { title: string; body: string; payload?: unknown }) =>
      api.post(`${V1}/admin/notifications/broadcast`, input),
    send: (input: { userId: string; title: string; body: string; payload?: unknown }) =>
      api.post(`${V1}/admin/notifications/send`, input)
  },

  settings: {
    list: () => api.get<SystemSettingRow[]>(`${V1}/admin/settings`).then((r) => r.data),
    upsert: (key: string, body: { valueJson: string; category?: string; description?: string; isSecret?: boolean }) =>
      api.put(`${V1}/admin/settings/${encodeURIComponent(key)}`, body),
    remove: (key: string) => api.delete(`${V1}/admin/settings/${encodeURIComponent(key)}`)
  }
};
