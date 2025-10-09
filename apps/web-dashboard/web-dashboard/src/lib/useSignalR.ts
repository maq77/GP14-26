import * as signalR from "@microsoft/signalr";
import { useEffect, useRef } from "react";
export const useSignalR = (url: string, onEvent: (ev: unknown) => void) => {
  const conn = useRef<signalR.HubConnection>(null);
  useEffect(() => {
    const c = new signalR.HubConnectionBuilder()
      .withUrl(url, { withCredentials: true })
      .withAutomaticReconnect().build();
    c.on("incident", onEvent);
    c.start();
    conn.current = c;
    return () => { c.stop(); }
  }, [url, onEvent]);
};
