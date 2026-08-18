import type { Rect, WellChoice } from './tacticsDocument';

/**
 * Named places are an editor concept: the document always stores the resolved
 * rectangle, and these names exist for dropdowns and plain-language readback.
 * First hit wins in `describePlace`, so order runs specific to general.
 */

export interface NamedZone {
  id: string;
  label: string;
  rect: Rect;
}

export const ZONE_OPTIONS: NamedZone[] = [
  { id: 'mid', label: 'Mid', rect: [13, 11, 17, 15] },
  { id: 'your-well', label: 'your Well', rect: [9, 4, 13, 8] },
  { id: 'their-well', label: 'their Well', rect: [17, 18, 21, 22] },
  { id: 'your-approach', label: 'your approach', rect: [6, 9, 14, 14] },
  { id: 'their-approach', label: 'their approach', rect: [16, 12, 24, 17] },
  { id: 'behind', label: 'their backfield', rect: [21, 16, 29, 23] },
];

export const WELL_OPTIONS: { id: WellChoice; label: string }[] = [
  { id: 'centre', label: 'the Centre Well' },
  { id: 'own', label: 'your Well' },
  { id: 'enemy', label: 'their Well' },
];

const PLACES: { label: string; rect: Rect }[] = [
  { label: 'Mid', rect: [13, 11, 17, 15] },
  { label: 'your reactor', rect: [1, 3, 5, 8] },
  { label: 'their reactor', rect: [25, 18, 29, 23] },
  { label: 'your Well', rect: [9, 4, 13, 8] },
  { label: 'their Well', rect: [17, 18, 21, 22] },
  { label: 'the north corridor', rect: [1, 1, 29, 2] },
  { label: 'the south corridor', rect: [1, 24, 29, 25] },
  { label: 'behind their lines', rect: [20, 1, 29, 12] },
  { label: 'behind their lines', rect: [20, 13, 29, 25] },
  { label: 'your backfield', rect: [1, 1, 10, 25] },
  { label: 'their approach', rect: [16, 11, 20, 17] },
  { label: 'your approach', rect: [6, 9, 14, 15] },
  { label: 'the north wing', rect: [11, 1, 20, 10] },
  { label: 'the south wing', rect: [11, 16, 20, 25] },
];

export function describePlace(x: number, y: number): string {
  for (const place of PLACES) {
    const [x0, y0, x1, y1] = place.rect;
    if (x >= x0 && x <= x1 && y >= y0 && y <= y1) return place.label;
  }
  return x < 15 ? 'your half' : 'their half';
}

export function namedZoneForRect(rect: Rect): NamedZone | null {
  return ZONE_OPTIONS.find((zone) =>
    zone.rect.every((value, index) => value === rect[index])) ?? null;
}
