import { pushSettingsApi } from "@/api/pushSettings";

/** Registers /sw.js in production builds only (dev-server HMR and service workers don't mix). */
export function registerPwa(): void {
  if (typeof window === "undefined" || !("serviceWorker" in navigator)) return;
  if (!import.meta.env.PROD) return;
  window.addEventListener("load", () => {
    navigator.serviceWorker.register("/sw.js").catch((err) => console.warn("Service worker registration failed", err));
  });
}

/** Asks the service worker to drop its cached API responses (call on logout so the next user never sees stale data). */
export function clearPwaCaches(): void {
  try {
    if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) return;
    navigator.serviceWorker.controller?.postMessage("clear-api-cache");
  } catch { /* best effort */ }
}

function urlBase64ToUint8Array(base64: string): Uint8Array {
  const padding = "=".repeat((4 - (base64.length % 4)) % 4);
  const raw = atob((base64 + padding).replace(/-/g, "+").replace(/_/g, "/"));
  const out = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
  return out;
}

function toBase64Url(buf: ArrayBuffer | null): string {
  if (!buf) return "";
  let s = "";
  new Uint8Array(buf).forEach((b) => (s += String.fromCharCode(b)));
  return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

export function isPushSupported(): boolean {
  return typeof window !== "undefined" && "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
}

/**
 * Asks for notification permission, subscribes via PushManager using the server's VAPID public key,
 * and registers the subscription with the API. Throws a descriptive Error on any failure.
 */
export async function subscribeToPush(): Promise<void> {
  if (!isPushSupported()) throw new Error("Push notifications are not supported in this browser.");
  const { publicKey } = await pushSettingsApi.vapidPublicKey();
  if (!publicKey) throw new Error("Push is not configured on the server.");

  const permission = await Notification.requestPermission();
  if (permission !== "granted") throw new Error("Notification permission was not granted.");

  const reg = await navigator.serviceWorker.ready;
  const existing = await reg.pushManager.getSubscription();
  const sub =
    existing ??
    (await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(publicKey) as BufferSource,
    }));

  await pushSettingsApi.subscribe({
    endpoint: sub.endpoint,
    p256dh: toBase64Url(sub.getKey("p256dh")),
    auth: toBase64Url(sub.getKey("auth")),
    userAgent: navigator.userAgent,
  });
}

/** Unsubscribes this browser and removes the subscription server-side. */
export async function unsubscribeFromPush(): Promise<void> {
  if (!isPushSupported()) return;
  const reg = await navigator.serviceWorker.ready;
  const sub = await reg.pushManager.getSubscription();
  if (!sub) return;
  await pushSettingsApi.unsubscribe(sub.endpoint);
  await sub.unsubscribe();
}

export async function currentPushEndpoint(): Promise<string | null> {
  if (!isPushSupported()) return null;
  const reg = await navigator.serviceWorker.getRegistration();
  const sub = await reg?.pushManager.getSubscription();
  return sub?.endpoint ?? null;
}
