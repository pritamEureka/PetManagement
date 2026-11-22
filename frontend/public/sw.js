/* Pawzaroo service worker: app shell cache, read-only API runtime cache (network-first),
 * offline fallback page, web push. Hand-written; bump VERSION to invalidate caches. */
const VERSION = "v1";
const SHELL_CACHE = `pawzaroo-shell-${VERSION}`;
const API_CACHE = `pawzaroo-api-${VERSION}`;
const SHELL_URLS = ["/", "/offline.html", "/manifest.webmanifest", "/icons/icon.svg", "/icons/icon-maskable.svg", "/favicon.ico"];

// Selected read-only GET endpoints that are safe to show stale when offline.
const CACHEABLE_API = [
  /\/api\/v\d+\/notifications(\?|$)/,
  /\/api\/v\d+\/communities(\/|\?|$)/,
  /\/api\/v\d+\/community-events(\/|\?|$)/,
  /\/api\/v\d+\/lost-found(\?|$)/,
  /\/api\/v\d+\/notification-preferences$/,
];
const API_MAX_ENTRIES = 60;

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(SHELL_CACHE).then((c) => c.addAll(SHELL_URLS)).then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== SHELL_CACHE && k !== API_CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

async function trim(cacheName, max) {
  const cache = await caches.open(cacheName);
  const keys = await cache.keys();
  for (let i = 0; i < keys.length - max; i++) await cache.delete(keys[i]);
}

self.addEventListener("fetch", (event) => {
  const req = event.request;
  if (req.method !== "GET") return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;

  // Navigations: network first, fall back to cached shell, then offline page.
  if (req.mode === "navigate") {
    event.respondWith(
      fetch(req)
        .then((res) => {
          const copy = res.clone();
          caches.open(SHELL_CACHE).then((c) => c.put("/", copy));
          return res;
        })
        .catch(async () => (await caches.match("/")) || (await caches.match("/offline.html")))
    );
    return;
  }

  // Selected read-only API: network first, cache fallback. Auth'd responses are cached per-URL;
  // the cache is cleared on logout via the "clear-api-cache" message.
  if (url.pathname.startsWith("/api/") && CACHEABLE_API.some((re) => re.test(url.pathname + url.search))) {
    event.respondWith(
      fetch(req)
        .then((res) => {
          if (res.ok) {
            const copy = res.clone();
            caches.open(API_CACHE).then((c) => c.put(req, copy)).then(() => trim(API_CACHE, API_MAX_ENTRIES));
          }
          return res;
        })
        .catch(async () => (await caches.match(req)) || new Response(JSON.stringify({ success: false, error: { code: "offline", message: "You are offline." } }), {
          status: 503, headers: { "Content-Type": "application/json" },
        }))
    );
    return;
  }

  // Static assets (hashed build files, icons): cache first, then network.
  if (url.pathname.startsWith("/assets/") || url.pathname.startsWith("/icons/")) {
    event.respondWith(
      caches.match(req).then((hit) => hit || fetch(req).then((res) => {
        if (res.ok) { const copy = res.clone(); caches.open(SHELL_CACHE).then((c) => c.put(req, copy)); }
        return res;
      }))
    );
  }
});

self.addEventListener("message", (event) => {
  if (event.data === "clear-api-cache") event.waitUntil(caches.delete(API_CACHE));
});

self.addEventListener("push", (event) => {
  let data = {};
  try { data = event.data ? event.data.json() : {}; } catch { data = { body: event.data && event.data.text() }; }
  const title = data.title || "Pawzaroo";
  event.waitUntil(
    self.registration.showNotification(title, {
      body: data.body || "",
      icon: "/icons/icon.svg",
      badge: "/icons/icon.svg",
      tag: data.category || undefined,
      data: { url: data.url || "/notifications" },
    })
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const target = (event.notification.data && event.notification.data.url) || "/";
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then((wins) => {
      for (const w of wins) {
        if ("focus" in w) { w.navigate(target); return w.focus(); }
      }
      return self.clients.openWindow(target);
    })
  );
});
