/**
 * Stable-ish per-browser fingerprint:
 *   1. read from localStorage if present
 *   2. otherwise generate a random UUID and store it
 *
 * This is a *hint* — the backend combines it with the User-Agent to derive the
 * actual device record. Clearing storage rolls the fingerprint forward, which
 * is fine: a new device row appears and the user can flag it from /account/devices.
 */
const KEY = "pawzaroo:device-id";

export function getDeviceFingerprint(): string {
  try {
    const existing = localStorage.getItem(KEY);
    if (existing) return existing;
    const id = crypto.randomUUID();
    localStorage.setItem(KEY, id);
    return id;
  } catch {
    // SSR / Safari private mode — generate a per-call value.
    return "ephemeral-" + Math.random().toString(36).slice(2);
  }
}
