import { api } from "./client";

const data = <T,>(p: Promise<{ data: T }>) => p.then((r) => r.data);

export interface CategoryPreference { category: string; inApp: boolean; push: boolean; email: boolean; }
export interface QuietHours { enabled: boolean; startMinute: number; endMinute: number; timezoneOffsetMinutes: number; }
export interface NotificationPreferences { categories: CategoryPreference[]; quietHours: QuietHours; }
export interface PushSubscriptionInfo { id: string; endpoint: string; userAgent?: string | null; createdAt: string; }

export const pushSettingsApi = {
  vapidPublicKey: () => data<{ publicKey?: string | null }>(api.get("/v1/push/vapid-public-key")),
  subscriptions: () => data<PushSubscriptionInfo[]>(api.get("/v1/push/subscriptions")),
  subscribe: (body: { endpoint: string; p256dh: string; auth: string; userAgent?: string }) =>
    data<PushSubscriptionInfo>(api.post("/v1/push/subscriptions", body)),
  unsubscribe: (endpoint: string) =>
    api.delete("/v1/push/subscriptions", { params: { endpoint } }).then(() => undefined),
  getPreferences: () => data<NotificationPreferences>(api.get("/v1/notification-preferences")),
  putPreferences: (body: NotificationPreferences) =>
    data<NotificationPreferences>(api.put("/v1/notification-preferences", body)),
};
