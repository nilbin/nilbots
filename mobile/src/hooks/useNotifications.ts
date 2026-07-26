import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState } from 'react-native';

import { API_BASE_URL } from '@/api/config';
import { api, type UserNotification } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';

/**
 * The player's notifications, live.
 *
 * SignalR is the delivery channel, not the inbox (DECISIONS #108). The durable records
 * are the truth, so this does both: it reloads unread from the API whenever the app comes
 * to the foreground, and listens on the hub while it is there. Either alone loses events —
 * a socket misses everything that happened while the app was backgrounded, and polling
 * alone makes a reward arrive late enough to stop feeling like a reward.
 *
 * Dedupe by id is what makes that safe: the same notification arriving from both paths is
 * shown once.
 */
export function useNotifications() {
  const { status } = useAuth();
  const [queue, setQueue] = useState<UserNotification[]>([]);
  const seen = useRef(new Set<string>());
  const connection = useRef<HubConnection | null>(null);

  const receive = useCallback((notification: UserNotification) => {
    if (seen.current.has(notification.id)) return;
    seen.current.add(notification.id);
    setQueue((current) => [...current, notification]);
  }, []);

  /** Drop the front of the queue once its toast has been shown and acknowledged. */
  const acknowledge = useCallback((id: string) => {
    setQueue((current) => current.filter((notification) => notification.id !== id));
    // Best effort: an unacknowledged notification simply arrives again next launch, which
    // is the failure mode this whole design prefers over losing one.
    void api.readNotification(id).catch(() => undefined);
  }, []);

  const reload = useCallback(async () => {
    try {
      for (const notification of await api.notifications()) receive(notification);
    } catch {
      // Offline, or signed out mid-flight. The hub or the next resume will catch up.
    }
  }, [receive]);

  useEffect(() => {
    if (status !== 'signed-in') {
      // Signing out clears what was queued: the next person to open the app must not see
      // the last one's rewards.
      seen.current.clear();
      setQueue([]);
      return;
    }

    void reload();
    const subscription = AppState.addEventListener('change', (next) => {
      if (next === 'active') void reload();
    });

    const hub = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/notifications`, {
        // The hub authenticates with the same token every request uses; the provider
        // renews it, so this is asked for per connection attempt rather than captured.
        accessTokenFactory: async () => (await api.accessToken()) ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    hub.on('notification', (notification: UserNotification) => receive(notification));
    // A reconnect means time passed unheard, so treat it like a resume.
    hub.onreconnected(() => void reload());
    connection.current = hub;
    hub.start().catch(() => undefined);

    return () => {
      subscription.remove();
      connection.current = null;
      if (hub.state !== HubConnectionState.Disconnected) void hub.stop();
    };
  }, [status, reload, receive]);

  return { current: queue[0] ?? null, pending: queue.length, acknowledge };
}
