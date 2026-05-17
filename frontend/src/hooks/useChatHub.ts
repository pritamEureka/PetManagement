import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { createHubConnection } from "@/lib/signalr";

export type ChatEvent =
  | { kind: "message"; payload: any }
  | { kind: "typing"; payload: { conversationId: string; userId: string; isTyping: boolean } }
  | { kind: "read"; payload: { conversationId: string; userId: string; lastMessageId?: string; readAt: string } }
  | { kind: "delivered"; payload: { messageId: string; userId: string; at: string } }
  | { kind: "presence"; payload: { userId: string; online: boolean } };

/**
 * Single, shared chat-hub connection per app session. Subscribers receive every
 * event and decide how to handle it. Re-connects automatically; the hook hides
 * the connection lifecycle from page components.
 */
export function useChatHub(onEvent: (e: ChatEvent) => void) {
  const connRef = useRef<signalR.HubConnection | null>(null);
  const [state, setState] = useState<"disconnected" | "connecting" | "connected">("disconnected");
  const handlerRef = useRef(onEvent);
  handlerRef.current = onEvent;

  useEffect(() => {
    const conn = createHubConnection("/hubs/chat");
    connRef.current = conn;

    conn.on("message",   (p) => handlerRef.current({ kind: "message",   payload: p }));
    conn.on("typing",    (p) => handlerRef.current({ kind: "typing",    payload: p }));
    conn.on("read",      (p) => handlerRef.current({ kind: "read",      payload: p }));
    conn.on("delivered", (p) => handlerRef.current({ kind: "delivered", payload: p }));
    conn.on("presence",  (p) => handlerRef.current({ kind: "presence",  payload: p }));

    setState("connecting");
    conn.start()
      .then(() => setState("connected"))
      .catch((err) => { console.warn("chat hub start failed", err); setState("disconnected"); });
    conn.onreconnected(() => setState("connected"));
    conn.onreconnecting(() => setState("connecting"));
    conn.onclose(() => setState("disconnected"));

    return () => { conn.stop().catch(() => { /* ignore */ }); };
  }, []);

  return {
    state,
    joinConversation: (id: string) => connRef.current?.invoke("JoinConversation", id).catch(() => {}),
    leaveConversation: (id: string) => connRef.current?.invoke("LeaveConversation", id).catch(() => {}),
    sendTyping: (id: string, on: boolean) => connRef.current?.invoke("Typing", id, on).catch(() => {}),
    markRead: (id: string, lastMessageId?: string) => connRef.current?.invoke("MarkRead", id, lastMessageId ?? null).catch(() => {}),
    ackDelivered: (messageId: string) => connRef.current?.invoke("AckDelivered", messageId).catch(() => {})
  };
}
