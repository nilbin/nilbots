import { parsePlayRoleTag } from './playAwareness';

/**
 * The short, live intent word drawn beneath a body.
 *
 * An authored order is the objective that can change from tick to tick. The
 * mind's role tag is a sticky job title and is useful only as a fallback when
 * no order is active. Coordinated-play role tags have their own in-world
 * treatment and must not be repeated as a floor caption.
 */
export function bodyIntentLabel(
  orderId: string | null | undefined,
  roleTag: string | null | undefined,
): string | null {
  const order = orderId?.trim();
  if (order) return order;
  const role = roleTag?.trim();
  if (!role || parsePlayRoleTag(role)) return null;
  return role;
}
