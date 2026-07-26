import { useNotifications } from '@/hooks/useNotifications';
import { NotificationToast } from '@/components/NotificationToast';

/**
 * Drives one toast at a time from the notification queue.
 *
 * Mounted once at the root, above the navigator, so a reward reaches the player whatever
 * screen they are on. One at a time on purpose: a set settling can grant a cosmetic in
 * the same instant, and three stacked banners over the arena is noise, not a reward.
 */
export function NotificationHost() {
  const { current, pending, acknowledge } = useNotifications();
  if (!current) return null;
  return (
    <NotificationToast
      notification={current}
      queued={pending}
      onDismiss={() => acknowledge(current.id)}
    />
  );
}
