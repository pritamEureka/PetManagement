import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { createHubConnection } from "@/lib/signalr";

export type NotificationEvent = {
  kind: "notify";
  payload: { id?: string; title: string; body: string; payload?: unknown; at: string };
};

type ConnState = "disconnected" | "connecting" | "connected";

interface SharedHub {
  conn: signalR.HubConnection;
  state: ConnState;
  subscribers: Set<(e: NotificationEvent) => void>;
  stateSubscribers: Set<(s: ConnState) => void>;
  refCount: number;
}

// Module-level singleton — keeps a single SignalR connection per tab regardless
// of how many components call useNotificationHub.
let shared: SharedHub | null = null;

function ensureHub(): SharedHub {
  if (shared) return shared;
  const conn = createHubConnection("/hubs/notifications");
  const subscribers = new Set<(e: NotificationEvent) => void>();
  const stateSubscribers = new Set<(s: ConnState) => void>();
  const h: SharedHub = { conn, state: "disconnected", subscribers, stateSubscribers, refCount: 0 };

  const fan = (e: NotificationEvent) => { subscribers.forEach((s) => s(e)); };
  conn.on("notify", (p) => fan({ kind: "notify", payload: p }));

  const setState = (s: ConnState) => { h.state = s; stateSubscribers.forEach((cb) => cb(s)); };
  conn.onreconnected(() => setState("connected"));
  conn.onreconnecting(() => setState("connecting"));
  conn.onclose(() => setState("disconnected"));

  setState("connecting");
  conn.start()
    .then(() => setState("connected"))
    .catch((err) => { console.warn("notification hub start failed", err); setState("disconnected"); });

  shared = h;
  return h;
}

function releaseHub() {
  if (!shared) return;
  shared.refCount--;
  if (shared.refCount > 0) return;
  const h = shared;
  shared = null;
  h.conn.stop().catch(() => { /* ignore */ });
}

/**
 * Subscribe to the shared notification-hub connection. Every caller receives
 * every event; callers decide what to handle. The connection is started on first
 * mount and torn down when the last subscriber unmounts.
 */
export function useNotificationHub(onEvent: (e: NotificationEvent) => void) {
  const [state, setState] = useState<ConnState>(() => shared?.state ?? "disconnected");
  const handlerRef = useRef(onEvent);
  handlerRef.current = onEvent;

  useEffect(() => {
    const h = ensureHub();
    h.refCount++;
    const handler = (e: NotificationEvent) => handlerRef.current(e);
    h.subscribers.add(handler);
    const stateHandler = (s: ConnState) => setState(s);
    h.stateSubscribers.add(stateHandler);
    setState(h.state);

    return () => {
      h.subscribers.delete(handler);
      h.stateSubscribers.delete(stateHandler);
      releaseHub();
    };
  }, []);

  return { state };
}
