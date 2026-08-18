/**
 * Class sprite art for the Tactics editor, straight from the shared
 * class-looks library — the same sprites the arena renders, and byte-for-byte
 * the images the design mock's squad picker used. Resolved by class id so a
 * class the library does not know simply renders without art.
 */
const sprites = import.meta.glob(
  '../../assets/class-looks/arc-*/sprite.png',
  { eager: true, query: '?url', import: 'default' },
) as Record<string, string>;

export function classSprite(classId: string): string | undefined {
  return sprites[`../../assets/class-looks/arc-${classId}/sprite.png`];
}

/** Per-slot accent rail colors, from the mock's palette. */
export const SLOT_ACCENTS = [
  '#38bdf8', '#f0a35e', '#b49cff', '#62d6a5',
  '#f472b6', '#facc15', '#22d3ee', '#fb7185',
];

export function slotAccent(slot: number): string {
  return SLOT_ACCENTS[slot % SLOT_ACCENTS.length];
}
