import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { createHubConnection } from "@/lib/signalr";

export type ChatEvent =
  | { kind: "message"; payload: any }
  | { kind: "typing"; payload: { conversationId: string; userId: string; isTyping: boolean } }
  | { kind: "read"; payload: { conversationId: string; userId: string; lastMessageId?: string; readAt: string } }
  | { kind: "delivered"; payload: { messageId: string; userId: string; at: string } }
  | { kind: "presence"; payload: { userId: string; online: boolean } };

type ConnState = "disconnected" | "connecting" | "connected";

interface SharedHub {
  conn: signalR.HubConnection;
  state: ConnState;
  subscribers: Set<(e: ChatEvent) => void>;
  stateSubscribers: Set<(s: ConnState) => void>;
  refCount: number;
}

// Module-level singleton — keeps a single SignalR connection per tab regardless
// of how many components call useChatHub. Prevents the sidebar badge and the
// messaging page from each spinning up their own websocket.
let shared: SharedHub | null = null;

function ensureHub(): SharedHub {
  if (shared) return shared;
  const conn = createHubConnection("/hubs/chat");
  const subscribers = new Set<(e: ChatEvent) => void>();
  const stateSubscribers = new Set<(s: ConnState) => void>();
  const h: SharedHub = { conn, state: "disconnected", subscribers, stateSubscribers, refCount: 0 };

  const fan = (e: ChatEvent) => { subscribers.forEach((s) => s(e)); };
  conn.on("message",   (p) => fan({ kind: "message",   payload: p }));
  conn.on("typing",    (p) => fan({ kind: "typing",    payload: p }));
  conn.on("read",      (p) => fan({ kind: "read",      payload: p }));
  conn.on("delivered", (p) => fan({ kind: "delivered", payload: p }));
  conn.on("presence",  (p) => fan({ kind: "presence",  payload: p }));

  const setState = (s: ConnState) => { h.state = s; stateSubscribers.forEach((cb) => cb(s)); };
  conn.onreconnected(() => setState("connected"));
  conn.onreconnecting(() => setState("connecting"));
  conn.onclose(() => setState("disconnected"));

  setState("connecting");
  conn.start()
    .then(() => setState("connected"))
    .catch((err) => { console.warn("chat hub start failed", err); setState("disconnected"); });

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
 * Subscribe to the shared chat-hub connection. Every caller receives every
 * event; callers decide what to handle. The connection is started on first
 * mount and torn down when the last subscriber unmounts.
 */
export function useChatHub(onEvent: (e: ChatEvent) => void) {
  const [state, setState] = useState<ConnState>(() => shared?.state ?? "disconnected");
  const handlerRef = useRef(onEvent);
  handlerRef.current = onEvent;

  useEffect(() => {
    const h = ensureHub();
    h.refCount++;
    const handler = (e: ChatEvent) => handlerRef.current(e);
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

  return {
    state,
    joinConversation: (id: string) => shared?.conn.invoke("JoinConversation", id).catch(() => {}),
    leaveConversation: (id: string) => shared?.conn.invoke("LeaveConversation", id).catch(() => {}),
    sendTyping: (id: string, on: boolean) => shared?.conn.invoke("Typing", id, on).catch(() => {}),
    markRead: (id: string, lastMessageId?: string) => shared?.conn.invoke("MarkRead", id, lastMessageId ?? null).catch(() => {}),
    ackDelivered: (messageId: string) => shared?.conn.invoke("AckDelivered", messageId).catch(() => {})
  };
}
