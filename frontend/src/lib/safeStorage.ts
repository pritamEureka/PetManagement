import { useAuthStore } from "@/store/authStore";

/** Current user id used to scope persisted client state ("anon" when signed out). */
export function currentUserScope(): string {
  try {
    return useAuthStore.getState().user?.id ?? "anon";
  } catch {
    return "anon";
  }
}

/** Build a user-scoped storage key: `pawzaroo:<namespace>:<userId>:<key>`. */
export function scopedKey(namespace: string, key: string, userId: string = currentUserScope()): string {
  return `pawzaroo:${namespace}:${userId}:${key}`;
}

function storage(): Storage | null {
  try {
    return typeof window !== "undefined" ? window.localStorage : null;
  } catch {
    return null;
  }
}

/** Read + JSON.parse; returns fallback on any failure (blocked storage, corrupt JSON). */
export function safeRead<T>(key: string, fallback: T): T {
  try {
    const raw = storage()?.getItem(key);
    return raw == null ? fallback : (JSON.parse(raw) as T);
  } catch {
    return fallback;
  }
}

/** Write JSON; returns false on quota/security errors instead of throwing. */
export function safeWrite(key: string, value: unknown): boolean {
  try {
    const s = storage();
    if (!s) return false;
    s.setItem(key, JSON.stringify(value));
    return true;
  } catch {
    return false;
  }
}

export function safeRemove(key: string): void {
  try {
    storage()?.removeItem(key);
  } catch {
    /* ignore */
  }
}

/** List every storage key under a prefix (used to evict old drafts when quota is hit). */
export function safeKeysWithPrefix(prefix: string): string[] {
  const s = storage();
  if (!s) return [];
  const out: string[] = [];
  try {
    for (let i = 0; i < s.length; i++) {
      const k = s.key(i);
      if (k && k.startsWith(prefix)) out.push(k);
    }
  } catch {
    /* ignore */
  }
  return out;
}
