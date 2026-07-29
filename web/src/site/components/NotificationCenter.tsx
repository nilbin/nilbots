import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';
import {
  type FocusEvent,
  type ReactNode,
  useCallback,
  useEffect,
  useRef,
  useState,
} from 'react';
import { Link, useLocation } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import {
  botLook,
  projectileLook,
  type BotLook,
} from '../../render/arenaThemes';
import { type UserNotification, type EntitlementEarnedPayload } from '../api';
import { useAuth } from '../auth';
import { useNotifications, useReadNotification } from '../queries';
import ResultToast from './ResultToast';
import ToastFrame, { ToastArtwork } from './ToastFrame';

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
  return (
    payload.kind === 'match-challenged' ||
    payload.kind === 'match-settled' ||
    payload.kind === 'set-settled'
  );
}

export default function NotificationCenter() {
  const { user } = useAuth();
  const { pathname } = useLocation();
  const [pending, setPending] = useState<UserNotification[]>([]);
  const [hovered, setHovered] = useState(false);
  const [focusWithin, setFocusWithin] = useState(false);
  const seen = useRef(new Set<string>());
  const dismissClock = useRef<{
    notificationId: string;
    remainingMs: number;
  } | null>(null);
  const acknowledge = useReadNotification();
  const acknowledgeRef = useRef(acknowledge.mutate);
  acknowledgeRef.current = acknowledge.mutate;

  const receive = useCallback((notification: UserNotification) => {
    // Narrowed on the payload's own discriminator, not the outer `kind`: they carry the
    // same string, but TypeScript cannot use one property to narrow a sibling.
    if (seen.current.has(notification.id)) return;
    seen.current.add(notification.id);
    if (!isShowable(notification)) {
      acknowledgeRef.current(notification.id);
      return;
    }
    setPending((current) => [...current, notification]);
  }, []);

  // Delivery is the hub; the query is the catch-up for whatever arrived while the socket
  // was down or the tab asleep. Both funnel through `receive`, which dedupes by id.
  const { data: unread } = useNotifications(Boolean(user));
  useEffect(() => {
    unread?.forEach(receive);
  }, [unread, receive]);

  useEffect(() => {
    seen.current.clear();
    setPending([]);
    // A site-review build has a typed HTTP fixture boundary but intentionally no fake
    // realtime server. The normal build folds this branch away and keeps hub delivery.
    if (!user || import.meta.env.VITE_SITE_REVIEW === '1') return;

    let disposed = false;
    let restartTimer: number | undefined;

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

    void start();
    return () => {
      disposed = true;
      window.clearTimeout(restartTimer);
      connection.off('notification', receive);
      void stop(connection);
    };
  }, [receive, user]);

  const dismiss = useCallback(
    (notificationId: string) => {
      setPending((current) =>
        current.filter((notification) => notification.id !== notificationId),
      );
      acknowledgeRef.current(notificationId);
    },
    [],
  );

  const active = pending[0];
  const activeId = active?.id;
  // A result toast over a live arena can reveal another outcome and covers the playback
  // controls on a phone. Keep it queued, and resume its remaining display time after the
  // viewer is left.
  const watchingMatch = pathname.startsWith('/matches/');
  const interacting = hovered || focusWithin || watchingMatch;

  useEffect(() => {
    if (!activeId) {
      dismissClock.current = null;
      return;
    }
    let clock = dismissClock.current;
    if (clock?.notificationId !== activeId) {
      clock = {
        notificationId: activeId,
        remainingMs: VISIBLE_MS,
      };
      dismissClock.current = clock;
    }

    let timer: number | undefined;
    let startedAt: number | undefined;

    const pause = () => {
      window.clearTimeout(timer);
      timer = undefined;
      if (startedAt === undefined) return;
      clock.remainingMs = Math.max(
        0,
        clock.remainingMs - (performance.now() - startedAt),
      );
      startedAt = undefined;
    };
    const schedule = () => {
      if (interacting || document.hidden || startedAt !== undefined) return;
      if (clock.remainingMs <= 0) {
        dismiss(activeId);
        return;
      }
      startedAt = performance.now();
      timer = window.setTimeout(() => {
        timer = undefined;
        startedAt = undefined;
        clock.remainingMs = 0;
        dismiss(activeId);
      }, clock.remainingMs);
    };
    const syncVisibility = () => {
      if (document.hidden) pause();
      else schedule();
    };

    document.addEventListener('visibilitychange', syncVisibility);
    schedule();
    return () => {
      pause();
      document.removeEventListener('visibilitychange', syncVisibility);
    };
  }, [activeId, dismiss, interacting]);

  useEffect(() => {
    if (!activeId) {
      setHovered(false);
      setFocusWithin(false);
    }
  }, [activeId]);

  const handleToastBlur = useCallback((event: FocusEvent<HTMLDivElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null))
      setFocusWithin(false);
  }, []);

  if (!active || watchingMatch) return null;
  // Narrowed on the payload's own discriminator, not the notification's — they carry the
  // same string, but TypeScript cannot narrow a sibling property from it.
  let toast: ReactNode;
  if (isUnlock(active))
    toast = (
      <UnlockToast
        key={active.id}
        notification={active}
        queued={pending.length - 1}
        onDismiss={() => dismiss(active.id)}
      />
    );
  else if (
    active.payload.kind === 'match-challenged' ||
    active.payload.kind === 'match-settled' ||
    active.payload.kind === 'set-settled'
  )
    toast = (
      <ResultToast
        key={active.id}
        payload={active.payload}
        queued={pending.length - 1}
        onDismiss={() => dismiss(active.id)}
      />
    );
  else return null;

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onFocusCapture={() => setFocusWithin(true)}
      onBlurCapture={handleToastBlur}
    >
      {toast}
    </div>
  );
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
    <ToastFrame
      tone="neutral"
      eyebrow="Achievement unlocked"
      title={title}
      artwork={
        <UnlockArtwork
          chassis={chassis}
          projectileId={projectileItem?.id}
        />
      }
      action={
        <Link
          to="/garage"
          onClick={onDismiss}
          className="btn mt-3 inline-flex items-center gap-1"
        >
          Choose a bot to equip <span aria-hidden>→</span>
        </Link>
      }
      queuedLabel={
        queued > 0
          ? `+${queued} more unlock${queued === 1 ? '' : 's'}`
          : undefined
      }
      dismissLabel="Dismiss unlock notification"
      onDismiss={onDismiss}
    >
      {items.length > 1 && (
        <p className="t-body mt-1 text-arena-text">
          {items.map((item) => item.label).join(' + ')}
        </p>
      )}
      {reason && <p className="t-meta mt-1">{reason}</p>}
    </ToastFrame>
  );
}

function UnlockArtwork({
  chassis,
  projectileId,
}: {
  chassis: BotLook | null;
  projectileId?: string;
}) {
  const accent = chassis?.suggestedAccent ?? botLook().suggestedAccent;
  const badge =
    chassis && projectileId ? (
      <ProjectilePreview
        look={projectileLook(projectileId)}
        accent={accent}
        className="h-5 w-7"
      />
    ) : undefined;

  return (
    <ToastArtwork accent={accent} badge={badge}>
      {chassis ? (
        <img
          src={chassis.imageUrl}
          alt=""
          className="size-16 object-contain sm:size-20"
        />
      ) : projectileId ? (
        <ProjectilePreview
          look={projectileLook(projectileId)}
          accent={accent}
          className="h-16 w-20"
        />
      ) : (
        <span className="text-3xl text-arena-text" aria-hidden>
          ✦
        </span>
      )}
    </ToastArtwork>
  );
}
