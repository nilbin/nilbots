/**
 * A mind's per-body diagnostic, read as an order.
 *
 * Debug text is free-form author vocabulary and the viewer must not pretend
 * otherwise — but the one clause a spectator is asking about while watching is
 * inside it: *what is this body doing right now*. A selected body that answers
 * that with nothing reads as a broken panel, not as a private thought.
 *
 * The tactical playbook writes `tp:<phase>:<group>:<order>:<action>`, often
 * behind a qualifier (`idle-break:tp:…`, `turn for tp:…`) and usually with a
 * ` via <Direction>` suffix the arena already shows by pointing the body that
 * way. **Anything that does not match is carried whole as the action**: a
 * reader must not decide that a vocabulary it has not been taught is not worth
 * showing.
 *
 * This is the exact twin of `command_reason` in `scripts/arc-relay-broadcast.py`,
 * which does the same split at projection time so a broadcast can publish the
 * clause without publishing the thought. Canonical replays keep their whole
 * debug text and are split here instead — the two must agree, or the same match
 * reads differently depending on which document the viewer was handed.
 */

/** The longest reason text worth carrying; the panel showing it has one line. */
export const REASON_MAX_CHARS = 48;

export interface CommandReason {
  /** The mind's name for the standing job, when the reason names one. */
  orderId: string | null;
  /** What the body is doing about it this tick. */
  action: string;
}

export function parseCommandReason(
  message: string | null | undefined,
): CommandReason | null {
  if (typeof message !== 'string') return null;
  const text = message.split(' via ')[0]!.trim();
  if (text.length === 0) return null;
  const marker = text.indexOf('tp:');
  if (marker < 0) {
    return { orderId: null, action: text.slice(0, REASON_MAX_CHARS) };
  }
  const parts = text.slice(marker).split(':');
  if (parts.length < 5) {
    return { orderId: null, action: text.slice(0, REASON_MAX_CHARS) };
  }
  // Stacked qualifiers and a preposition: keep the words, drop the grammar,
  // and join them the way the tail already reads.
  const qualifier = text
    .slice(0, marker)
    .split(/[\s:]+/)
    .filter((word) => word.length > 0 && word !== 'for')
    .join('/');
  const tail = parts.slice(4).join(':');
  const action = qualifier.length > 0 ? `${qualifier}/${tail}` : tail;
  return {
    orderId: parts[3]!.length > 0 ? parts[3]! : null,
    action: action.slice(0, REASON_MAX_CHARS),
  };
}
