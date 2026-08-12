/**
 * A mind's per-body diagnostic, read as an order.
 *
 * Debug text is free-form author vocabulary and the viewer must not pretend
 * otherwise — but the one clause a spectator is asking about while watching is
 * inside it: *what is this body doing right now*. A selected body that answers
 * that with nothing reads as a broken panel, not as a private thought.
 *
 * The current orders executor writes
 * `orders:<role> > <order> [> <child>…]:<action>`, while frozen tactical
 * artifacts write `tp:<phase>:<group>:<order>:<action>`. Both may sit behind a
 * qualifier (`idle-break:tp:…`, `turn for orders:…`) and usually carry a
 * ` via <Direction>` suffix the arena already shows by pointing the body that
 * way. **Anything that does not match is carried whole as the action**: a
 * reader must not decide that a vocabulary it has not been taught is not worth
 * showing.
 *
 * A movement action additionally carries the resolved destination as an `@x,y`
 * tail. It is split off rather than shown: "where is it going" is a place on
 * the map and belongs on the map.
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
  /**
   * The tile this body is walking toward, when the action names one.
   *
   * Only the route/march movement plane publishes it (`march@12,7`)
   * — a body closing on a fight is walking at something the standing order's
   * target is not. Null everywhere else, and the arena simply draws nothing.
   */
  destination: { x: number; y: number } | null;
}

/** The `@x,y` tail a movement diagnostic carries; see `MovementProvenance`. */
const DESTINATION_TAIL = /@(\d+),(\d+)$/;

function finishReason(
  text: string,
  marker: number,
  orderId: string | null,
  tail: string,
): CommandReason {
  // Stacked qualifiers and a preposition: keep the words, drop the grammar,
  // and join them the way the tail already reads.
  const qualifier = text
    .slice(0, marker)
    .split(/[\s:]+/)
    .filter((word) => word.length > 0 && word !== 'for')
    .join('/');
  // Split the destination off BEFORE the length cap: a long action tail must
  // never silently eat the coordinates off the end of the lens.
  const found = DESTINATION_TAIL.exec(tail);
  const destination = found
    ? { x: Number(found[1]), y: Number(found[2]) }
    : null;
  if (found) tail = tail.slice(0, found.index);
  const action = qualifier.length > 0 ? `${qualifier}/${tail}` : tail;
  return {
    orderId,
    action: action.slice(0, REASON_MAX_CHARS),
    destination,
  };
}

export function parseCommandReason(
  message: string | null | undefined,
): CommandReason | null {
  if (typeof message !== 'string') return null;
  const text = message.split(' via ')[0]!.trim();
  if (text.length === 0) return null;

  const ordersMarker = text.indexOf('orders:');
  if (ordersMarker >= 0) {
    const sentence = text.slice(ordersMarker + 'orders:'.length);
    const actionMarker = sentence.indexOf(':');
    if (actionMarker >= 0) {
      const path = sentence
        .slice(0, actionMarker)
        .split('>')
        .map((part) => part.trim())
        .filter((part) => part.length > 0);
      // A path containing only the role means no authored order was selected
      // (upgrade/no-eligible-order). The presenter deliberately falls back to
      // the role in that case. With an order, the leaf is the body's current
      // objective; parent orders remain visible in the selected-body panel.
      const orderId = path.length > 1 ? path.at(-1)! : null;
      return finishReason(
        text,
        ordersMarker,
        orderId,
        sentence.slice(actionMarker + 1),
      );
    }
  }

  const marker = text.indexOf('tp:');
  if (marker < 0) {
    return {
      orderId: null,
      action: text.slice(0, REASON_MAX_CHARS),
      destination: null,
    };
  }
  const parts = text.slice(marker).split(':');
  if (parts.length < 5) {
    return {
      orderId: null,
      action: text.slice(0, REASON_MAX_CHARS),
      destination: null,
    };
  }
  return finishReason(
    text,
    marker,
    parts[3]!.length > 0 ? parts[3]! : null,
    parts.slice(4).join(':'),
  );
}

/**
 * Whether an action tail means *this body has a live fight*.
 *
 * The combat-state cue is always on, for every body, so it has to be derivable
 * from what a broadcast already carries — and it is: the mind names the fight
 * in the action. `duel-stand`, `close-on-focus` and `flank-approach` are the
 * `tp:` movement/stand tails; `focus …` and `prepare focus …` are the free-form
 * shooting reasons carried whole, which is why this matches a prefix rather
 * than an exact set. A qualifier folds in front (`turn/close-on-focus`), so the
 * match is on the LAST segment.
 *
 * Deliberately narrow. `signature`, `repair`, `withdraw` and `scan` are things
 * a body does near a fight without being in one, and a pip that lit for them
 * would mean "something is happening", which the arena already shows.
 */
const ENGAGED_ACTIONS = [
  'focus',
  'prepare focus',
  'duel-stand',
  'close-on-focus',
  'flank-approach',
  'flush-hidden',
];

export function isEngagedAction(action: string | null | undefined): boolean {
  if (typeof action !== 'string') return false;
  const tail = action.slice(action.lastIndexOf('/') + 1);
  return ENGAGED_ACTIONS.some(
    (verb) => tail === verb || tail.startsWith(`${verb} `),
  );
}
