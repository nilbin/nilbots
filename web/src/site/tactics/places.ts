import type { Rect, WellChoice, WorkshopChoice } from './tacticsDocument';

/**
 * Named places are an editor concept: the document always stores the resolved
 * rectangle, and these names exist for dropdowns and plain-language readback.
 * First hit wins in `describePlace`, so order runs specific to general.
 *
 * Every rectangle here is Broadloom (`arc-relay-broadloom-01`, 37x27) ground,
 * anchored on the map's own landmarks: reactors at (3,13)/(33,13), Wells at
 * (10,19)/(18,13)/(26,7), heal tiles at (8,6)/(28,20) and the two neutral
 * veterancy Workshops at (18,4)/(18,22). West is always "your" side.
 */

export interface NamedZone {
  id: string;
  label: string;
  rect: Rect;
}

export const ZONE_OPTIONS: NamedZone[] = [
  { id: 'mid', label: 'Mid', rect: [14, 11, 19, 15] },
  { id: 'your-well', label: 'your Well', rect: [8, 17, 12, 21] },
  { id: 'their-well', label: 'their Well', rect: [24, 5, 28, 9] },
  { id: 'north-workshop', label: 'the north Workshop', rect: [15, 3, 21, 6] },
  { id: 'south-workshop', label: 'the south Workshop', rect: [15, 20, 21, 23] },
  { id: 'your-approach', label: 'your approach', rect: [6, 10, 13, 16] },
  { id: 'their-approach', label: 'their approach', rect: [22, 10, 29, 16] },
  { id: 'behind', label: 'their backfield', rect: [27, 14, 34, 22] },
];

export const WELL_OPTIONS: { id: WellChoice; label: string }[] = [
  { id: 'centre', label: 'the Centre Well' },
  { id: 'own', label: 'your Well' },
  { id: 'enemy', label: 'their Well' },
];

/** Seat-relative strategic choices come first. North/south remain available
 * when the physical room itself matters; either lets the executor choose. */
export const WORKSHOP_OPTIONS: { id: WorkshopChoice; label: string; blurb: string }[] = [
  {
    id: 'own',
    label: 'Your side',
    blurb: 'The Workshop nearest your own Well; this choice mirrors with your seat.',
  },
  {
    id: 'enemy',
    label: 'Their side',
    blurb: 'The Workshop nearest the enemy Well; this choice mirrors with your seat.',
  },
  {
    id: 'north',
    label: 'North Workshop',
    blurb: 'The northern training room, the same room for both sides.',
  },
  {
    id: 'south',
    label: 'South Workshop',
    blurb: 'The southern training room, the same room for both sides.',
  },
  {
    id: 'either',
    label: 'Either',
    blurb: 'Keep the channel you already hold, else take the nearest open room.',
  },
];

export function workshopLabel(workshop: WorkshopChoice | undefined): string {
  if (workshop === 'own') return 'your-side Workshop';
  if (workshop === 'enemy') return 'their-side Workshop';
  if (workshop === 'north') return 'the north Workshop';
  if (workshop === 'south') return 'the south Workshop';
  return 'either Workshop';
}

const PLACES: { label: string; rect: Rect }[] = [
  { label: 'Mid', rect: [14, 11, 19, 15] },
  { label: 'your reactor', rect: [1, 11, 5, 15] },
  { label: 'their reactor', rect: [31, 11, 35, 15] },
  { label: 'your Well', rect: [8, 17, 12, 21] },
  { label: 'their Well', rect: [24, 5, 28, 9] },
  { label: 'the north Workshop', rect: [15, 2, 21, 7] },
  { label: 'the south Workshop', rect: [15, 19, 21, 24] },
  { label: 'the north corridor', rect: [1, 1, 35, 2] },
  { label: 'the south corridor', rect: [1, 24, 35, 25] },
  { label: 'behind their lines', rect: [27, 1, 35, 25] },
  { label: 'your backfield', rect: [1, 1, 9, 25] },
  { label: 'their approach', rect: [20, 10, 26, 16] },
  { label: 'your approach', rect: [6, 10, 13, 16] },
  { label: 'the north wing', rect: [10, 1, 26, 10] },
  { label: 'the south wing', rect: [10, 16, 26, 25] },
];

export function describePlace(x: number, y: number): string {
  for (const place of PLACES) {
    const [x0, y0, x1, y1] = place.rect;
    if (x >= x0 && x <= x1 && y >= y0 && y <= y1) return place.label;
  }
  return x < 18 ? 'your half' : 'their half';
}

export function namedZoneForRect(rect: Rect): NamedZone | null {
  return ZONE_OPTIONS.find((zone) =>
    zone.rect.every((value, index) => value === rect[index])) ?? null;
}
