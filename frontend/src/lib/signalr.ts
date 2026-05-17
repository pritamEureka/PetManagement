import * as signalR from "@microsoft/signalr";
import { useAuthStore } from "@/store/authStore";

export function createHubConnection(path: "/hubs/chat" | "/hubs/notifications") {
  return new signalR.HubConnectionBuilder()
    .withUrl(path, {
      accessTokenFactory: () => useAuthStore.getState().accessToken ?? ""
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}
