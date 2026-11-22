import { useEffect, useState } from "react";
import { reputationBadgesApi, type ReputationBadgeInfo } from "@/api/socialPlus";

/** Module-level cache + micro-batcher: many PostCards asking at once produce a single request. */
const TTL_MS = 5 * 60_000;
const cache = new Map<string, { at: number; badge: ReputationBadgeInfo | null }>();
const pending = new Set<string>();
const listeners = new Set<() => void>();
let timer: ReturnType<typeof setTimeout> | null = null;

function flush() {
  timer = null;
  const ids = [...pending].slice(0, 100);
  ids.forEach((i) => pending.delete(i));
  if (ids.length === 0) return;
  reputationBadgesApi.batch(ids)
    .then((res) => {
      const now = Date.now();
      for (const id of ids) {
        const key = Object.keys(res ?? {}).find((k) => k.toLowerCase() === id.toLowerCase());
        cache.set(id, { at: now, badge: key ? res[key] : null });
      }
    })
    .catch(() => { const now = Date.now(); ids.forEach((id) => cache.set(id, { at: now, badge: null })); })
    .finally(() => { listeners.forEach((l) => l()); if (pending.size > 0 && !timer) timer = setTimeout(flush, 30); });
}

function request(ids: string[]) {
  const now = Date.now();
  let added = false;
  for (const id of ids) {
    const hit = cache.get(id);
    if ((!hit || now - hit.at > TTL_MS) && !pending.has(id)) { pending.add(id); added = true; }
  }
  if (added && !timer) timer = setTimeout(flush, 30);
}

/** Returns a map userId -> badge for users with reputation level >= 1 (others are omitted). */
export function useReputationBadges(userIds: (string | null | undefined)[]): Record<string, ReputationBadgeInfo> {
  const key = [...new Set(userIds.filter((x): x is string => !!x))].sort().join(",");
  const [, bump] = useState(0);
  useEffect(() => {
    const l = () => bump((n) => n + 1);
    listeners.add(l);
    return () => { listeners.delete(l); };
  }, []);
  useEffect(() => { if (key) request(key.split(",")); }, [key]);
  const out: Record<string, ReputationBadgeInfo> = {};
  if (key) for (const id of key.split(",")) {
    const b = cache.get(id)?.badge;
    if (b && b.level >= 1) out[id] = b;
  }
  return out;
}
