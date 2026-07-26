import { useAuth } from '@/auth/AuthProvider';
import { useNotifications } from '@/hooks/useNotifications';
import { usePushRegistration } from '@/hooks/usePushRegistration';
import { NotificationToast } from '@/components/NotificationToast';

/**
 * Drives one toast at a time from the notification queue.
 *
 * Mounted once at the root, above the navigator, so a reward reaches the player whatever
 * screen they are on. One at a time on purpose: a set settling can grant a cosmetic in
 * the same instant, and three stacked banners over the arena is noise, not a reward.
 *
 * It also owns push registration, because the two are the same feature seen from either
 * side of the app being open: this component is the foreground channel, and the hook keeps
 * the background one addressable for exactly as long as someone is signed in.
 */
export function NotificationHost() {
  const { status } = useAuth();
  usePushRegistration(status === 'signed-in');
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
