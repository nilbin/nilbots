import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import {
  botLook,
  projectileLook,
  type BotLook,
} from '../../render/arenaThemes';
import { api, type UserNotification, type EntitlementEarnedPayload } from '../api';
import { useAuth } from '../auth';
import ResultToast from './ResultToast';

const POLL_MS = 60_000;
const VISIBLE_MS = 14_000;

type UnlockNotification = UserNotification & { payload: EntitlementEarnedPayload };

function isUnlock(notification: UserNotification): notification is UnlockNotification {
  return notification.payload?.kind === 'entitlement-earned';
}

/**
 * Whether this component has a toast for a notification.
 *
 * A kind with no toast is still acknowledged rather than dropped — ignoring one silently
 * leaves it unread forever and grows an inbox the site can never clear. An unlock with no
 * items is treated the same way: there is nothing to show.
 */
function isShowable(notification: UserNotification): boolean {
  const payload = notification.payload;
  if (!payload) return false;
  if (payload.kind === 'entitlement-earned') return payload.items.length > 0;
  return payload.kind === 'match-settled' || payload.kind === 'set-settled';
}

export default function NotificationCenter() {
  const { user } = useAuth();
  const [pending, setPending] = useState<UserNotification[]>([]);
  const seen = useRef(new Set<string>());

  const receive = useCallback((notification: UserNotification) => {
    // Narrowed on the payload's own discriminator, not the outer `kind`: they carry the
    // same string, but TypeScript cannot use one property to narrow a sibling.
    if (seen.current.has(notification.id)) return;
    seen.current.add(notification.id);
    if (!isShowable(notification)) {
      void api.post(`/api/notifications/${notification.id}/read`, {}).catch(() => undefined);
      return;
    }
    setPending((current) => [...current, notification]);
  }, []);

  useEffect(() => {
    seen.current.clear();
    setPending([]);
    if (!user) return;

    let disposed = false;
    let restartTimer: number | undefined;
    const loadUnread = async () => {
      try {
        const notifications = await api.get<UserNotification[]>(
          '/api/notifications?take=20',
        );
        if (!disposed) notifications.forEach(receive);
      } catch {
        // Realtime and the next poll are independent recovery paths.
      }
    };

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/notifications')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('notification', receive);

    const start = async () => {
      if (disposed || connection.state !== HubConnectionState.Disconnected)
        return;
      try {
        await connection.start();
      } catch {
        if (!disposed) restartTimer = window.setTimeout(start, 5_000);
      }
    };

    void loadUnread();
    void start();
    const poll = window.setInterval(() => void loadUnread(), POLL_MS);
    return () => {
      disposed = true;
      window.clearInterval(poll);
      window.clearTimeout(restartTimer);
      connection.off('notification', receive);
      void stop(connection);
    };
  }, [receive, user]);

  const dismiss = useCallback((notificationId: string) => {
    setPending((current) =>
      current.filter((notification) => notification.id !== notificationId),
    );
    void api
      .post(`/api/notifications/${notificationId}/read`)
      .catch(() => undefined);
  }, []);

  const active = pending[0];
  useEffect(() => {
    if (!active) return;
    let timer: number | undefined;
    const schedule = () => {
      window.clearTimeout(timer);
      if (!document.hidden)
        timer = window.setTimeout(() => dismiss(active.id), VISIBLE_MS);
    };
    document.addEventListener('visibilitychange', schedule);
    schedule();
    return () => {
      window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', schedule);
    };
  }, [active, dismiss]);

  if (!active) return null;
  // Narrowed on the payload's own discriminator, not the notification's — they carry the
  // same string, but TypeScript cannot narrow a sibling property from it.
  if (isUnlock(active))
    return (
      <UnlockToast
        key={active.id}
        notification={active}
        queued={pending.length - 1}
        onDismiss={() => dismiss(active.id)}
      />
    );
  if (active.payload.kind === 'match-settled' || active.payload.kind === 'set-settled')
    return (
      <ResultToast
        key={active.id}
        payload={active.payload}
        queued={pending.length - 1}
        onDismiss={() => dismiss(active.id)}
      />
    );
  return null;
}

async function stop(connection: HubConnection) {
  try {
    await connection.stop();
  } catch {
    // The connection may already have failed while React was cleaning it up.
  }
}

function UnlockToast({
  notification,
  queued,
  onDismiss,
}: {
  notification: UnlockNotification;
  queued: number;
  onDismiss: () => void;
}) {
  const { items, reason } = notification.payload;
  const chassisItem = items.find((item) => item.kind === 'bot-look');
  const projectileItem = items.find((item) => item.kind === 'projectile-look');
  const chassis = chassisItem ? botLook(chassisItem.id) : null;
  const title =
    items.length === 1
      ? `${items[0].label} unlocked`
      : 'New loadout unlocked';

  return (
    <aside
      className="unlock-toast fixed top-4 right-4 z-50 w-[min(28rem,calc(100vw-2rem))] overflow-hidden rounded-xl border border-amber-300/45 bg-[#101720]/96 shadow-[0_22px_70px_rgba(0,0,0,0.55),0_0_35px_rgba(245,190,70,0.13)] backdrop-blur"
      role="status"
      aria-live="polite"
    >
      <div className="unlock-toast__sheen pointer-events-none absolute inset-0" />
      <div className="absolute inset-x-0 top-0 h-px bg-linear-to-r from-transparent via-amber-200 to-transparent" />
      <div className="relative flex gap-4 p-4 sm:p-5">
        <UnlockArtwork
          chassis={chassis}
          projectileId={projectileItem?.id}
        />
        <div className="min-w-0 flex-1 py-0.5">
          <p className="font-mono text-[10px] font-bold tracking-[0.22em] text-amber-300">
            ACHIEVEMENT UNLOCKED
          </p>
          <h2 className="mt-1 text-lg font-black tracking-wide text-slate-100">
            {title}
          </h2>
          {items.length > 1 && (
            <p className="mt-0.5 text-sm font-semibold text-slate-200">
              {items.map((item) => item.label).join(' + ')}
            </p>
          )}
          {reason && (
            <p className="mt-1.5 text-xs leading-relaxed text-slate-400">
              {reason}
            </p>
          )}
          <Link
            to="/garage"
            onClick={onDismiss}
            className="mt-3 inline-flex items-center gap-1 font-mono text-xs font-bold text-arena-accent transition-colors hover:text-sky-300"
          >
            Equip in my garage <span aria-hidden>→</span>
          </Link>
          {queued > 0 && (
            <p className="mt-2 font-mono text-[10px] text-slate-500">
              +{queued} more unlock{queued === 1 ? '' : 's'}
            </p>
          )}
        </div>
        <button
          type="button"
          onClick={onDismiss}
          aria-label="Dismiss unlock notification"
          className="-mt-1 -mr-1 flex size-7 shrink-0 items-center justify-center rounded-md text-slate-500 transition-colors hover:bg-white/5 hover:text-slate-200"
        >
          ×
        </button>
      </div>
    </aside>
  );
}

function UnlockArtwork({
  chassis,
  projectileId,
}: {
  chassis: BotLook | null;
  projectileId?: string;
}) {
  const accent = chassis?.suggestedAccent ?? '#38bdf8';
  return (
    <div
      className="relative flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-white/10 bg-[radial-gradient(circle_at_50%_42%,rgba(245,190,70,0.18),rgba(10,14,20,0.82)_68%)]"
      style={{ boxShadow: `inset 0 -3px 0 ${accent}55` }}
    >
      <div className="unlock-toast__halo absolute size-16 rounded-full bg-amber-300/10 blur-xl" />
      {chassis ? (
        <img
          src={chassis.imageUrl}
          alt=""
          className="relative size-20 object-contain drop-shadow-[0_7px_9px_rgba(0,0,0,0.55)]"
        />
      ) : projectileId ? (
        <ProjectilePreview
          look={projectileLook(projectileId)}
          accent={accent}
          className="relative h-16 w-20"
        />
      ) : (
        <span className="relative text-3xl text-amber-300">✦</span>
      )}
      {chassis && projectileId && (
        <span className="absolute right-1.5 bottom-1.5 flex size-9 items-center justify-center rounded-full border border-amber-200/25 bg-[#0a0e14]/90 shadow-lg">
          <ProjectilePreview
            look={projectileLook(projectileId)}
            accent={accent}
            className="h-5 w-7"
          />
        </span>
      )}
    </div>
  );
}
