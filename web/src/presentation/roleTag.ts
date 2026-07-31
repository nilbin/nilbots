/**
 * Role-tag presentation (docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md §12.3).
 *
 * A role tag is the mind's own word for what a body is doing — free
 * vocabulary, non-authoritative, and published on visible enemies as well as
 * own bodies. Rendering it is the single highest watchability-per-line item in
 * the whole mind design: a spectator reading `channeler / screen / screen /
 * courier` understands the escorted channel without being taught the rules.
 *
 * Two rules the memo states and this module exists to keep:
 *
 * - **Colour by a stable hash of the tag**, so `channeler` is the same colour
 *   all match and across matches. Colouring by body or by team would make the
 *   label a decoration; colouring by the word makes it readable at a glance.
 * - **An absent tag renders nothing** — never the string "none". An unlabelled
 *   body should look unlabelled, not broken.
 */

/**
 * The house palette's readable-on-near-black band. Deliberately not the bot
 * accents: an accent says *whose* body this is, and a role tag says *what it
 * is doing*, so sharing the scale would collapse two facts into one colour.
 */
const ROLE_TAG_HUES = [
  '#7dd3fc',
  '#fca5a5',
  '#a7f3d0',
  '#fcd34d',
  '#c4b5fd',
  '#f9a8d4',
  '#93c5fd',
  '#fdba74',
];

/** FNV-1a over the tag's code units: stable, cheap, and order-sensitive. */
function hashTag(tag: string): number {
  let hash = 0x811c9dc5;
  for (let index = 0; index < tag.length; index += 1) {
    hash ^= tag.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash;
}

export function roleTagColor(tag: string): string {
  return ROLE_TAG_HUES[hashTag(tag) % ROLE_TAG_HUES.length];
}

/**
 * The caption drawn under a body. Tags are capped at 24 bytes by the contract,
 * which is already short enough to read at gameplay scale, so this only guards
 * against a pathological label rather than truncating normal ones.
 */
export function roleTagCaption(tag: string): string {
  return tag.length <= 14 ? tag : `${tag.slice(0, 13)}…`;
}
