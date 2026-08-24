import type { TacticalSheetCatalog } from '../api';
import { possessive } from './summary';

/**
 * The player-owned `arc-relay-tactics-v2` document, mirrored from the server
 * grammar in `ArcRelayTacticsDsl.cs`. The server compile is the authority;
 * this file gives the editor the same structural and bounds checks so most
 * problems surface while drawing, not on save. A Tactic is one committed
 * whole-team operation: groups are membership, and ordered named phases say
 * what every group does and when the operation moves forward — transitions
 * are latched and forward-only. Saved v1 documents migrate on load.
 */

export type Facing = 'n' | 'e' | 's' | 'w';
export type JobKind =
  | 'posts' | 'area' | 'line' | 'approach'
  | 'converge' | 'advance' | 'patrol' | 'return' | 'escort' | 'none'
  | 'train';
export type Posture = 'wait' | 'hold' | 'aggressive' | 'steal' | 'escort' | 'observe';
export type Recovery = 'fight-on' | 'patch' | 'heal-tile' | 'heal-up';
export type CoreRule = 'ignore' | 'grab' | 'hunt';
export type CoreBankValueOperator = 'equals' | 'at-least' | 'at-most';

export interface PhaseCoreIntent {
  rule: CoreRule;
  /** Optional subset of this Group. Absent means every member. This lets a
   * specialist keep Group custody/formation membership without inheriting a
   * narrower Core objective. */
  slots?: number[];
  /** Seat-relative source Well. */
  well?: 'centre' | 'own' | 'enemy';
  coreBankValue?: { operator: CoreBankValueOperator; value: 1 | 2 | 3 | 4 };
  /** Leave the ordinary job and return to this (independent) Well shortly
   * before its next Core birth. The loose-Core objective still wins. */
  preposition?: {
    well: 'centre' | 'own' | 'enemy';
    ticks: number;
  };
}
export type DeliveryPlan = 'none' | 'solo' | 'escort';
export type MaxDuration = 'none' | 'brief' | 'standard' | 'long';
export type StartMode = 'always' | 'when';
export type Build = 'balanced' | 'durable' | 'aggressive' | 'scout';
export type RecipeId =
  | 'hold-area' | 'centre-rush' | 'pincer' | 'core-raid' | 'patrol'
  | 'deliver-core' | 'blank';
export type WellChoice = 'centre' | 'own' | 'enemy';
/** Player-authored Workshop choices are seat-relative. Replays retain the
 * authoritative global north/south room ids after resolution. */
export type WorkshopChoice = 'own' | 'enemy' | 'either';
/** Whose channel a `workshopTraining` row reads. */
export type WorkshopTeam = 'self' | 'enemy' | 'any';

export type Point = [number, number];
export type Rect = [number, number, number, number];

export interface ZoneRef {
  name?: string;
  rect: Rect;
}

export interface ZoneSetRef {
  /** New documents use zones. Singular zone remains a load-only compatibility
   * spelling and is normalized before the next intentional save. */
  zones: ZoneRef[];
  zone?: never;
}

/** Every condition row any case context can carry. Which rows a given
 * context accepts is the ROWS_* lists below, mirroring the server. */
export interface TacticConds {
  /** Every selected Group (all when omitted) is at its ground this phase —
   * the dead excuse themselves. */
  groupsSet?: true | { allowedLate: 0 | 1 | 2; groups?: string[] };
  /** We have been at this phase for N ticks. */
  held?: { ticks: number };
  /** Every LIVING bot is at ≥N% health — the dead excuse themselves. */
  healed?: { percent: number };
  /** At least count of the selected, currently living roster slots have
   * reached level or higher. */
  veterancy?: { level: number; slots: number[]; count: number };
  enemiesAt?: { count: number; zones: ZoneRef[]; zone?: never };
  contact?: true;
  coreLoose?: { well: WellChoice };
  /** That Well's next Core births within N ticks — its one effective clock:
   * the initial opening, then the bank-triggered recharge. */
  coreSoon?: { well: WellChoice; ticks: number };
  /** At least N enemy bots are down right now (team-wide fact). */
  enemiesDown?: { count: number };
  carrier?: true;
  losses?: { count: number };
  /** We lost N bots within the last T ticks — the remembered-casualty row
   * for aborts and emergency starts. */
  lossesWithin?: { count: number; ticks: number };
  killsWithin?: { count: number; ticks: number };
  kills?: { count: number };
  cleared?: ZoneSetRef;
  notFighting?: ZoneSetRef;
  /** true = any Core taken since this phase began; a well narrows it to
   * that source's Core — the claim a Claim phase means. */
  coreAcquired?: true | { well: WellChoice };
  /** true = any Core banked since this phase began; the object form narrows
   * the evidence to one source Well and, optionally, its scoring value. */
  coreBanked?: true | {
    well: WellChoice;
    coreBankValue?: { operator: CoreBankValueOperator; value: 1 | 2 | 3 | 4 };
  };
  coreLost?: true;
  reached?: true;
  observed?: true;
  /** Workshop rows read the PUBLIC neutral-objective signal both seats can
   * see. They never reveal a fogged position — only Workshop state. */
  workshopReady?: { workshop: WorkshopChoice };
  /** A body of that team is publicly channeling there; team defaults to any. */
  workshopTraining?: { workshop: WorkshopChoice; team?: WorkshopTeam };
  workshopContested?: { workshop: WorkshopChoice };
  workshopDormant?: { workshop: WorkshopChoice };
  /** ≥N friendly completions at that Workshop since THIS phase began. */
  workshopDone?: { workshop: WorkshopChoice; count?: number };
}

export type CondKind = keyof TacticConds;

export const START_ROWS: CondKind[] = [
  'enemiesAt', 'contact', 'cleared', 'notFighting', 'coreLoose', 'coreSoon', 'enemiesDown',
  'carrier', 'losses', 'lossesWithin', 'killsWithin', 'healed',
  'workshopReady', 'workshopTraining', 'workshopContested', 'workshopDormant',
];

export const TRANSITION_ROWS: CondKind[] = [
  'groupsSet', 'held', 'healed', 'veterancy', 'contact', 'enemiesAt', 'coreLoose',
  'coreSoon', 'enemiesDown', 'kills', 'killsWithin', 'losses',
  'lossesWithin', 'cleared', 'notFighting', 'coreAcquired', 'coreBanked', 'coreLost',
  'reached', 'observed',
  'workshopReady', 'workshopTraining', 'workshopContested', 'workshopDormant',
  'workshopDone',
];

export const ABORT_ROWS: CondKind[] = [
  'losses', 'lossesWithin', 'coreLost', 'enemiesAt', 'contact', 'cleared', 'notFighting',
  'carrier', 'enemiesDown', 'coreLoose', 'coreSoon', 'killsWithin', 'healed',
  'workshopTraining', 'workshopContested', 'workshopDormant',
];

export const WORKSHOP_CHOICES: WorkshopChoice[] = [
  'own', 'enemy', 'either',
];
export const WORKSHOP_TEAMS: WorkshopTeam[] = ['self', 'enemy', 'any'];

/** Abort-to-Tactic is editor sugar, not a saved scheduler edge. Only rows
 * legal in both contexts can be copied into the target Tactic's Start. */
export function abortCasesCanStart(cases: TacticCase[] | undefined): boolean {
  if (!cases?.length) return false;
  const startRows = new Set<CondKind>(START_ROWS);
  let active = 0;
  for (const tacticCase of cases) {
    for (const conds of [tacticCase.required, tacticCase.triggers, tacticCase.unless]) {
      for (const kind of Object.keys(conds ?? {}) as CondKind[]) {
        if (!startRows.has(kind)) return false;
        if (conds?.[kind] !== undefined) active += 1;
      }
    }
  }
  return active > 0;
}

function stableConditionJson(value: unknown): string {
  if (Array.isArray(value)) {
    return `[${value.map(stableConditionJson).join(',')}]`;
  }
  if (value && typeof value === 'object') {
    const record = value as Record<string, unknown>;
    return `{${Object.keys(record).sort().map((key) =>
      `${JSON.stringify(key)}:${stableConditionJson(record[key])}`).join(',')}}`;
  }
  return JSON.stringify(value) ?? 'undefined';
}

/** Recover the editor's linked-fallback affordance without adding metadata
 * to the player document. The nearest earlier Tactic with the same Start
 * cases is the one the authoring sugar most recently prepared. */
export function inferAbortSwitchTarget(
  document: TacticsDocument,
  sourceId: string,
): string | null {
  const sourceIndex = document.tactics.findIndex((tactic) => tactic.id === sourceId);
  const cases = document.tactics[sourceIndex]?.abort?.cases;
  if (sourceIndex < 1 || !abortCasesCanStart(cases)) return null;
  const wanted = stableConditionJson(cases);
  for (let index = sourceIndex - 1; index >= 0; index -= 1) {
    const tactic = document.tactics[index];
    if (tactic.start.mode === 'when'
      && stableConditionJson(tactic.start.cases ?? []) === wanted) {
      return tactic.id;
    }
  }
  return null;
}

/** Copy an active Tactic's abort promise into another Tactic's Start and put
 * that target immediately above it. Runtime still does its one ordinary
 * top-to-bottom Sheet reread; this merely authors the two matching halves. */
export function applyAbortSwitchTarget(
  document: TacticsDocument,
  sourceId: string,
  targetId: string,
): boolean {
  if (sourceId === targetId) return false;
  const source = document.tactics.find((tactic) => tactic.id === sourceId);
  const targetIndex = document.tactics.findIndex((tactic) => tactic.id === targetId);
  if (!source || targetIndex < 0 || !abortCasesCanStart(source.abort?.cases)) return false;

  const [target] = document.tactics.splice(targetIndex, 1);
  target.start = { mode: 'when', cases: structuredClone(source.abort!.cases) };
  const sourceIndex = document.tactics.findIndex((tactic) => tactic.id === sourceId);
  if (sourceIndex < 0) return false;
  document.tactics.splice(sourceIndex, 0, target);
  return true;
}

/** Player-authored cases use all(required) AND none(unless). `triggers` is a
 * legacy read shape normalized into required by the editor; keeping it here
 * allows old saved documents to open without a schema fork. */
export interface TacticCase {
  required?: TacticConds;
  triggers?: TacticConds;
  unless?: TacticConds;
}

/** One forward-only latched exit from a phase. */
export interface TacticTransition extends TacticCase {
  /** Loaded only for pre-pruning documents. New authoring is strictly
   * sequential and omits this field. */
  to?: string;
}

export interface TacticPost {
  at: Point;
  facing: Facing;
  /** The squad slot this post belongs to. Explicit so a bot moved to another
   * Group takes its post (and way in) along instead of the pairing silently
   * shifting onto its neighbours. Absent in older documents, where posts
   * paired with the group's slots by position — normalizeDocument stamps it. */
  slot?: number;
  /** This post's own drawn way in — overrides the group route for its bot. */
  via?: Point[];
}

export type SignaturePolicy = 'smart' | 'prefer' | 'only' | 'off';
export type SignatureMark = { points: Point[] } | { line: Point[] };
export type SignaturePlan = {
  policy: SignaturePolicy;
  points?: Point[];
  line?: Point[];
};

export function signaturePlanPoints(plan: SignaturePlan | undefined): Point[] {
  return plan?.points ?? plan?.line ?? [];
}

/** One group's assignment for one phase. Kind 'none' carries the group's
 * nearest earlier geometric job forward under this phase's posture. */
export interface TacticJob {
  kind: JobKind;
  posture: Posture;
  /** Optional on-the-way posture: how the group behaves while MOVING to
   * this job's ground; the main posture takes over once set. */
  travel?: Posture;
  recovery: Recovery;
  posts?: TacticPost[];
  rect?: Rect;
  path?: Point[];
  point?: Point;
  /** Optional drawn approach route; the group walks it in, then takes the floor. */
  via?: Point[];
  /** Explicit per-bot plans keyed by stable squad slot. Missing = Smart. */
  sigs?: Record<string, SignaturePlan>;
  /** A `train` job's Workshop, instead of drawn geometry. The training tile
   * itself is rules-owned and resolved by the executor from the match
   * contract, so the editor never draws it and the document stays honest on
   * any later map revision. Refused on every other kind. */
  workshop?: WorkshopChoice;
  /** Optional phase-local replacement for the Group's tactic-wide Core
   * habit. Absent means use group.coreRule. */
  core?: PhaseCoreIntent;
}

export type CustodyPlan = 'solo' | 'escort' | 'keep';

export interface TacticPhaseDef {
  id: string;
  name: string;
  jobs: Record<string, TacticJob>;
  /** Absent/empty on the final phase = the operation holds there forever. */
  continue?: TacticTransition[];
  /** Policy bound to a Core first acquired in this phase; absent = the
   * Tactic's delivery plan. The Core keeps this policy through later phase
   * changes. 'keep' holds it on this phase's job. Via is the drawn carrier
   * route home. */
  custody?: { plan: CustodyPlan; via?: Point[] };
}

/** Membership only — what a group does lives in each phase's job for it. */
export interface TacticGroup {
  id: string;
  name: string;
  slots: number[];
  coreRule: CoreRule;
}

export interface Tactic {
  id: string;
  name: string;
  recipe: RecipeId;
  start: { mode: StartMode; cases?: TacticCase[] };
  abort?: { cases: TacticCase[] };
  /** Deliberately minor: absent means no limit. */
  maxDuration?: MaxDuration;
  delivery: { plan: DeliveryPlan; preferredSlots?: number[] };
  groups: TacticGroup[];
  phases: TacticPhaseDef[];
}

export interface SquadBot {
  slot: number;
  class: string;
  build: Build;
  name: string;
}

export interface TacticsDocument {
  schema: 'arc-relay-tactics-v2';
  sheetId: string;
  mapId: string;
  mapWidth: number;
  mapHeight: number;
  squad: SquadBot[];
  tactics: Tactic[];
}

export interface SheetIssue {
  path: string;
  message: string;
  severity: 'error' | 'warning';
}

/** One sheet under edit; the document is the only saved content. */
export interface TacticsDraft {
  localId: string;
  name: string;
  sheetId: string | null;
  revision: number | null;
  enterLadder: boolean;
  document: TacticsDocument;
}

export const POSTURES: { id: Posture; label: string; blurb: string }[] = [
  { id: 'observe', label: 'Observe', blurb: 'Stay on the assignment and do not voluntarily fight.' },
  { id: 'hold', label: 'Hold', blurb: 'Fight for this ground with a short defensive leash.' },
  { id: 'aggressive', label: 'Aggressive', blurb: 'Chase, commit, finish kills.' },
];

export const TRAVEL_BEHAVIORS: { id: Posture; label: string; blurb: string }[] = [
  { id: 'observe', label: 'Just march', blurb: 'Ignore contact on the way; destination behavior takes over on arrival.' },
  { id: 'hold', label: 'Respond to contact', blurb: 'Answer nearby contact with a short leash, then resume this route.' },
  { id: 'aggressive', label: 'Brawl through', blurb: 'Pursue nearby enemies while travelling, then resume the route.' },
];

export const RECOVERIES: { id: Recovery; label: string; blurb: string }[] = [
  { id: 'fight-on', label: 'Fight on', blurb: 'No falling back — hold the plan whatever the damage.' },
  { id: 'heal-tile', label: 'Break for the heal tile', blurb: 'At low health, run to the nearest heal tile; return after recovering.' },
  { id: 'heal-up', label: 'Heal up fully', blurb: 'Break for the heal tile at any damage and stay until completely full — pair with an “Everyone is healed up” condition.' },
];

/** The one job vocabulary every phase shares. 'none' reads differently on
 * the first phase (nothing to do) than later (carry the last plan on). */
export const JOB_KINDS: { id: JobKind; label: string; blurb: string; draw: DrawKind | null }[] = [
  { id: 'posts', label: 'Take up posts', blurb: 'Exact tiles — one per bot.', draw: 'posts' },
  { id: 'area', label: 'Occupy an area', blurb: 'Use flexible positions inside a drawn area.', draw: 'rect' },
  { id: 'converge', label: 'Converge on a spot', blurb: 'Pick the destination — the route finds itself.', draw: 'point' },
  { id: 'patrol', label: 'Patrol a loop', blurb: 'Walk the beat until the phase moves on.', draw: 'route' },
  {
    id: 'train',
    label: 'Train at Workshop',
    blurb: 'One bot holds the channel at the neutral Workshop — nothing to draw, the room picks the tile.',
    draw: null,
  },
];

export const CORE_RULES: { id: CoreRule; label: string; blurb: string }[] = [
  { id: 'ignore', label: 'Ignore', blurb: 'Leave Cores alone — the job comes first.' },
  { id: 'grab', label: 'Grab if safe', blurb: 'Pick up a loose Core when it costs nothing.' },
  { id: 'hunt', label: 'Go take one', blurb: 'Actively go for a Core — steal it if held.' },
];

export const DELIVERY_PLANS: { id: DeliveryPlan; label: string; blurb: string }[] = [
  { id: 'none', label: 'Finish the job first', blurb: 'Keep executing; banking waits until this Tactic ends.' },
  { id: 'solo', label: 'Carrier breaks off and banks', blurb: 'Whoever holds it runs it home alone; the rest keep at it.' },
  { id: 'escort', label: 'Carrier banks under escort', blurb: 'The carrier heads home with groupmates screening the run.' },
];

export const MAX_DURATIONS: { id: MaxDuration; label: string; blurb: string }[] = [
  { id: 'none', label: 'No limit', blurb: 'Runs until it finishes or aborts.' },
  { id: 'brief', label: 'Brief', blurb: 'A quick probe, then move on.' },
  { id: 'standard', label: 'Standard', blurb: 'One committed push.' },
  { id: 'long', label: 'Long', blurb: 'A patient siege.' },
];

export const BUILDS: { id: Build; label: string; blurb: string }[] = [
  { id: 'balanced', label: 'Balanced', blurb: 'Even spread — no weak phase.' },
  { id: 'durable', label: 'Durable', blurb: 'Holds ground longer under fire.' },
  { id: 'aggressive', label: 'Aggressive', blurb: 'Hits harder, commits sooner.' },
  { id: 'scout', label: 'Scout', blurb: 'Sees further, moves lighter.' },
];

export type DrawKind = 'posts' | 'rect' | 'route' | 'point';

export const FACING_DELTAS: Record<Facing, Point> = {
  n: [0, -1], e: [1, 0], s: [0, 1], w: [-1, 0],
};

export type TilePlacement = 'anchor' | 'route';

/** Why this tile cannot host the placement — null when legal. Mirrors the
 * server sheet compiler exactly: geometry must be legal from BOTH seats,
 * because the same plan runs mirrored when the entrant is seated east.
 * Anchors (posts, points, marks) must be open, unreserved
 * ground; route waypoints additionally avoid protected home regions. */
export function tilePlacementIssue(
  catalog: TacticalSheetCatalog,
  [x, y]: Point,
  placement: TilePlacement,
): string | null {
  const width = catalog.map.width;
  const height = catalog.map.height;
  const floor = (px: number, py: number) =>
    px >= 0 && py >= 0 && px < width && py < height
    && catalog.map.tileRows[py]?.[px] !== '#';
  const eitherSeat = (points: { x: number; y: number }[]) =>
    points.some((point) => (point.x === x && point.y === y)
      || (point.x === width - 1 - x && point.y === height - 1 - y));
  if (!floor(x, y)) return 'a wall';
  if (!floor(width - 1 - x, height - 1 - y)) {
    return 'a wall once the plan mirrors to the east seat';
  }
  if (eitherSeat(catalog.map.spawnAnchors.map((spawn) => spawn.position))) {
    return 'reserved spawn ground';
  }
  if (placement === 'route') {
    // Only the OPPONENT's protected home ground blocks a route — you may
    // route through your own pad. Mirrors the server exactly:
    // eastForbidden(tile) OR westForbidden(mirrored tile).
    const forbidden = opponentForbidden(catalog);
    if (forbidden.east.has(`${x},${y}`)
      || forbidden.west.has(`${width - 1 - x},${height - 1 - y}`)) {
      return 'inside a protected home region';
    }
  }
  return null;
}

const forbiddenCache = new WeakMap<
  TacticalSheetCatalog, { west: Set<string>; east: Set<string> }>();

/** Each side's forbidden ground: the SpawnProtected component containing
 * that side's reactor (4-neighbour flood, like the server) plus its home
 * region. Cached per catalog — validation runs on every edit. */
function opponentForbidden(
  catalog: TacticalSheetCatalog,
): { west: Set<string>; east: Set<string> } {
  const cached = forbiddenCache.get(catalog);
  if (cached) return cached;
  const tagged = new Set(catalog.map.tileTags
    .filter((tag) => tag.kind === 'SpawnProtected')
    .flatMap((tag) => tag.tiles)
    .map((tile) => `${tile.x},${tile.y}`));
  const side = (reactorId: string, homeId: string) => {
    const out = new Set<string>();
    const seed = catalog.map.regions
      .find((region) => region.id === reactorId)?.tiles[0];
    if (seed && tagged.has(`${seed.x},${seed.y}`)) {
      out.add(`${seed.x},${seed.y}`);
      const queue: Point[] = [[seed.x, seed.y]];
      while (queue.length > 0) {
        const [cx, cy] = queue.pop()!;
        for (const [dx, dy] of [[-1, 0], [1, 0], [0, -1], [0, 1]] as Point[]) {
          const key = `${cx + dx},${cy + dy}`;
          if (tagged.has(key) && !out.has(key)) {
            out.add(key);
            queue.push([cx + dx, cy + dy]);
          }
        }
      }
    }
    for (const tile of catalog.map.regions
      .find((region) => region.id === homeId)?.tiles ?? []) {
      out.add(`${tile.x},${tile.y}`);
    }
    return out;
  };
  const value = {
    west: side('reactor-west', 'home-west'),
    east: side('reactor-east', 'home-east'),
  };
  forbiddenCache.set(catalog, value);
  return value;
}

/** Facing is cardinal intent, not adjacent map geometry. A wall, boundary, or
 * reserved tile in front of a post never makes the standing tile illegal. */
export function postFacingIssue(
  _catalog: TacticalSheetCatalog,
  _post: TacticPost,
): string | null {
  return null;
}

export const GROUP_COLORS = ['#22d3ee', '#f0a35e', '#b49cff'];

export function groupColor(tactic: Tactic, groupId: string): string {
  const index = tactic.groups.findIndex((group) => group.id === groupId);
  return GROUP_COLORS[Math.max(0, index) % GROUP_COLORS.length];
}

/** Later phases of one tactic render as lighter shades of the same hue —
 * one colour family per group, one distinguishable step per phase, the
 * storyboard chips and the map agreeing. Phase 1 is the base colour. */
export function phaseShade(
  color: string,
  phaseIndex: number,
  phaseCount: number,
): string {
  if (phaseCount <= 1 || phaseIndex <= 0) return color;
  const t = phaseIndex / (phaseCount - 1);
  const mix = t * .5;
  const part = (offset: number) => {
    const value = parseInt(color.slice(offset, offset + 2), 16);
    return Math.round(value + (255 - value) * mix)
      .toString(16).padStart(2, '0');
  };
  return `#${part(1)}${part(3)}${part(5)}`;
}

/** Each spatial signature's map glyph, from the design mock's vocabulary. */
const SIGNATURE_ICONS: Record<string, string> = {
  mason: '▬', nest: '◎', minesmith: '▴', lantern: '✳', mortar: '✛',
  veil: '☁', kestrel: '》', longshot: '━', towline: '↝',
};

/** Spatial signature controls come directly from the shared authoring
 * catalog. Class-specific glyphs remain presentation only. */
export function signatureMarkGeometry(
  catalog: TacticalSheetCatalog,
  classId: string,
): { kind: 'points' | 'line'; max: number; label: string; icon: string } | null {
  const entry = catalog.classes.find((value) => value.id === classId);
  if (!entry) return null;
  const authoring = entry.signatureAuthoring;
  const icon = SIGNATURE_ICONS[classId] ?? '✦';
  if (authoring.guidanceGeometry === 'points')
    return { kind: 'points', max: authoring.guidanceMarkLimit, label: entry.signatureName, icon };
  if (authoring.guidanceGeometry === 'line')
    return { kind: 'line', max: authoring.guidanceMarkLimit, label: entry.signatureName, icon };
  return null;
}

let localCounter = 0;

export function freshLocalId(prefix: string): string {
  localCounter += 1;
  return `${prefix}-${Date.now().toString(36)}-${localCounter}`;
}

export function identifierFromName(value: string, fallback: string): string {
  const kebab = value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 24)
    .replace(/-+$/g, '');
  return kebab.length > 0 ? kebab : fallback;
}

export function blankDocument(catalog: TacticalSheetCatalog): TacticsDocument {
  return {
    schema: 'arc-relay-tactics-v2',
    sheetId: 'new-sheet',
    mapId: catalog.map.id,
    mapWidth: catalog.map.width,
    mapHeight: catalog.map.height,
    squad: [],
    tactics: [],
  };
}

/** The group's job in a phase. */
export function jobOf(
  tactic: Tactic,
  phaseIndex: number,
  groupId: string,
): TacticJob | null {
  return tactic.phases[phaseIndex]?.jobs[groupId] ?? null;
}

/** The ground a group actually holds in a phase: its own geometric job, or
 * the nearest earlier one carried forward under the current posture. Null
 * when there is nothing to stand on — mirrors the materializer. */
export function effectiveJob(
  tactic: Tactic,
  phaseIndex: number,
  groupId: string,
): TacticJob | null {
  return effectiveJobResolution(tactic, phaseIndex, groupId)?.job ?? null;
}

export interface EffectiveJobResolution {
  job: TacticJob;
  sourcePhaseIndex: number;
  inherited: boolean;
}

/** The one frontend interpretation of carry-forward. Ground and its way-in
 * come from the nearest earlier assignment; behavior and signatures belong
 * to the current phase, exactly as the server materializer resolves them. */
export function effectiveJobResolution(
  tactic: Tactic,
  phaseIndex: number,
  groupId: string,
): EffectiveJobResolution | null {
  const raw = jobOf(tactic, phaseIndex, groupId);
  if (!raw) return null;
  if (raw.kind !== 'none') {
    return { job: raw, sourcePhaseIndex: phaseIndex, inherited: false };
  }
  for (let earlier = phaseIndex - 1; earlier >= 0; earlier--) {
    const candidate = jobOf(tactic, earlier, groupId);
    if (candidate && candidate.kind !== 'none') {
      return {
        sourcePhaseIndex: earlier,
        inherited: true,
        job: {
          ...candidate,
          posture: raw.posture,
          travel: raw.travel,
          recovery: raw.recovery,
          sigs: raw.sigs,
        },
      };
    }
  }
  return null;
}

/** A way-in runs in travel order and ENDS at its destination. Drawing it
 * from the destination outward is a natural gesture, so a route whose
 * FIRST point sits on the destination and whose last does not is simply
 * reversed rather than marched backwards (Cinder walked through mid to
 * stand on the first waypoint of a backwards way-in). Returns null when
 * the route is fine as drawn. */
export function reversedWayIn(via: Point[] | undefined, at: Point): Point[] | null {
  if (!via || via.length < 2) return null;
  const near = (point: Point) =>
    Math.max(Math.abs(point[0] - at[0]), Math.abs(point[1] - at[1])) <= 2;
  if (near(via[0]) && !near(via[via.length - 1])) return [...via].reverse();
  return null;
}

export function freshJob(kind: JobKind, posture: Posture): TacticJob {
  // Training is passive by grammar: any fight resets the channel, so the
  // trainee's posture is always 'wait' and its Workshop is always named.
  if (kind === 'train') {
    return { kind, posture: 'wait', recovery: 'fight-on', workshop: 'either' };
  }
  return {
    kind,
    posture,
    recovery: 'fight-on',
  };
}

let phaseCounter = 0;

export function freshPhase(tactic: Tactic, name: string): TacticPhaseDef {
  const used = new Set(tactic.phases.map((phase) => phase.id));
  let id = identifierFromName(name, `phase-${tactic.phases.length + 1}`);
  while (used.has(id)) {
    phaseCounter += 1;
    id = `${id.slice(0, 20)}-${phaseCounter}`;
  }
  const jobs: Record<string, TacticJob> = {};
  for (const group of tactic.groups) jobs[group.id] = freshJob('none', 'hold');
  return { id, name, jobs, continue: [{ required: {} }] };
}

/** v1 documents (Stage/Release/Act/Finish) migrate to the phase model
 * exactly like the server's MigrateTactics: Stage → phase 1, Release → its
 * continue rule, Act → phase 2, Finish → the finish transition, persistent
 * → the explicit empty final continue. */
export function migrateDocument(parsed: Record<string, unknown>): TacticsDocument {
  const document = structuredClone(parsed) as unknown as {
    schema: string;
    tactics: Record<string, unknown>[];
  };
  document.schema = 'arc-relay-tactics-v2';
  for (const tactic of document.tactics ?? []) migrateTactic(tactic);
  return document as unknown as TacticsDocument;
}

type LooseJob = TacticJob & { kind: string };

function migrateTactic(tactic: Record<string, unknown>): void {
  const start = tactic.start as { mode?: string; conds?: TacticConds; cases?: TacticCase[] } | undefined;
  if (start?.conds) {
    start.cases = [{ triggers: start.conds }];
    delete start.conds;
  }
  const release = tactic.release as { mode?: string; conds?: TacticConds } | undefined;
  delete tactic.release;
  const finish = tactic.finish as {
    outcomes?: TacticConds & { persistent?: true };
    maxAttempt?: string;
  } | undefined;
  delete tactic.finish;
  if (finish?.maxAttempt && finish.maxAttempt !== 'none') {
    tactic.maxDuration = finish.maxAttempt;
  }
  const outcomes = { ...(finish?.outcomes ?? {}) };
  delete outcomes.persistent;

  const groups = (tactic.groups ?? []) as (TacticGroup & {
    stage?: LooseJob;
    act?: LooseJob;
    sigs?: { stage?: Record<string, SignatureMark>; act?: Record<string, SignatureMark> };
  })[];
  const stageJobs: Record<string, TacticJob> = {};
  const actJobs: Record<string, TacticJob> = {};
  let anyStage = false;
  for (const group of groups) {
    const sigs = group.sigs;
    const stage = withSigs(group.stage, sigs?.stage);
    const act = withSigs(group.act, sigs?.act);
    anyStage ||= stage.kind !== 'none';
    stageJobs[group.id] = stage;
    actJobs[group.id] = act;
    delete group.stage;
    delete group.act;
    delete group.sigs;
  }

  const finishContinue: TacticTransition[] | undefined =
    Object.keys(outcomes).length > 0
      ? [{ triggers: outcomes }]
      : undefined;
  const phases: TacticPhaseDef[] = [];
  if (release?.mode === 'immediate' || !anyStage) {
    // An immediate release was a single-phase tactic: the act jobs, with act
    // 'none' groups holding their stage geometry under the act posture.
    const jobs: Record<string, TacticJob> = {};
    for (const group of groups) {
      jobs[group.id] = actOrCarriedStage(actJobs[group.id], stageJobs[group.id]);
    }
    phases.push({ id: 'act', name: 'Act', jobs, continue: finishContinue });
  } else {
    const releaseRule: TacticTransition = {
      required: { groupsSet: true },
    };
    if (release?.conds && Object.keys(release.conds).length > 0) {
      releaseRule.triggers = release.conds;
    }
    phases.push({
      id: 'stage', name: 'Stage', jobs: stageJobs, continue: [releaseRule],
    });
    phases.push({
      id: 'act', name: 'Act', jobs: actJobs, continue: finishContinue,
    });
  }
  tactic.phases = phases;
}

function withSigs(
  job: LooseJob | undefined,
  sigs: Record<string, SignatureMark> | undefined,
): TacticJob {
  const base: TacticJob = job
    ? (structuredClone(job) as TacticJob)
    : { kind: 'none', posture: 'wait', recovery: 'fight-on' };
  if (sigs && Object.keys(sigs).length > 0) {
    // v1 class-keyed marks are normalized to slots at the common load
    // boundary immediately after this migration.
    base.sigs = structuredClone(sigs) as unknown as Record<string, SignaturePlan>;
  }
  return base;
}

function actOrCarriedStage(act: TacticJob, stage: TacticJob): TacticJob {
  if (act.kind !== 'none' || stage.kind === 'none') return act;
  const carried = structuredClone(stage);
  carried.posture = act.posture;
  carried.recovery = act.recovery;
  if (act.sigs) carried.sigs = act.sigs;
  else delete carried.sigs;
  return carried;
}

/** Bring an in-memory document object — parsed server JSON or a restored
 * local draft — to the current schema: v1 migrates, v2 passes through.
 * Every path into editor state goes through this one boundary. */
export function normalizeDocument(document: unknown): TacticsDocument {
  const value = document as { schema?: string };
  let loaded: TacticsDocument;
  if (value?.schema === 'arc-relay-tactics-v1') {
    loaded = migrateDocument(value as Record<string, unknown>);
  } else if (value?.schema === 'arc-relay-tactics-v2') {
    loaded = value as TacticsDocument;
  } else {
    throw new Error(`Not a tactics document: ${value?.schema}`);
  }
  normalizeSeatRelativeSelectors(loaded);
  // Self-healing on load. Backwards way-ins flip into travel order; posts
  // saved before they carried a `slot` are stamped with their positional
  // pairing (the only pairing that existed); and leftovers from a Group
  // reshuffle — a post whose bot has left, a signature mark no member's
  // class owns, a job for a dissolved Group — are simply cleaned away.
  for (const tactic of loaded.tactics ?? []) {
    const normalizeCase = (entry: TacticCase) => {
      if (entry.triggers) {
        entry.required = { ...entry.triggers, ...(entry.required ?? {}) };
        delete entry.triggers;
      }
      normalizeZoneRows(entry.required);
      normalizeZoneRows(entry.unless);
    };
    for (const entry of tactic.start.cases ?? []) normalizeCase(entry);
    for (const entry of tactic.abort?.cases ?? []) normalizeCase(entry);
    for (const phase of tactic.phases ?? []) {
      for (const rule of phase.continue ?? []) {
        // Explicit jumps were removed from the product. Array order is the
        // only phase order, so every legacy spelling (phase id, next,
        // finish, backward, or skip) canonicalizes to the implicit edge.
        delete rule.to;
        normalizeCase(rule);
      }
      for (const [groupId, job] of Object.entries(phase.jobs ?? {})) {
        const group = tactic.groups?.find((entry) => entry.id === groupId);
        if (!group) {
          delete phase.jobs[groupId];
          continue;
        }
        if (job.posts) {
          job.posts.forEach((post, index) => {
            if (post.slot == null) post.slot = group.slots[index];
          });
          job.posts = job.posts.filter((post) =>
            post.slot != null && group.slots.includes(post.slot));
          if (job.posts.length === 0) delete job.posts;
        }
        if (job.core?.slots) {
          job.core.slots = [...new Set(job.core.slots)]
            .filter((slot) => group.slots.includes(slot));
          // Never broaden a stale selected-bot override onto the remaining
          // Group. No selected member means the override itself is gone;
          // every member selected uses the canonical unscoped spelling.
          if (job.core.slots.length === 0) {
            delete job.core;
          } else if (job.core.slots.length === group.slots.length) {
            delete job.core.slots;
          }
        }
        for (const post of job.posts ?? []) {
          const flipped = reversedWayIn(post.via, post.at);
          if (flipped) post.via = flipped;
        }
        const normalizedSigs: Record<string, SignaturePlan> = {};
        for (const [key, rawPlan] of Object.entries(job.sigs ?? {}) as
          [string, SignaturePlan | SignatureMark][]) {
          const parsed = Number(key);
          if (Number.isInteger(parsed) && group.slots.includes(parsed)) {
            normalizedSigs[String(parsed)] = {
              ...structuredClone(rawPlan),
              policy: 'policy' in rawPlan ? rawPlan.policy : 'only',
            };
            continue;
          }
          // Class-keyed guidance was shared by every duplicate. Preserve
          // that behavior by cloning its marks to each matching slot and
          // making the old marks-only meaning explicit.
          for (const slot of group.slots) {
            const bot = loaded.squad?.find((entry) => entry.slot === slot);
            if (bot?.class !== key) continue;
            normalizedSigs[String(slot)] = {
              ...structuredClone(rawPlan), policy: 'only',
            };
          }
        }
        if (Object.keys(normalizedSigs).length > 0) job.sigs = normalizedSigs;
        else delete job.sigs;
      }
      if (phase.custody?.via) {
        // The carrier's way home ENDS at your own reactor, so that tile is
        // what a backwards route is measured against. Broadloom's west
        // reactor; the catalog is not in scope at this load boundary.
        const flipped = reversedWayIn(phase.custody.via, [3, 13]);
        if (flipped) phase.custody.via = flipped;
      }
    }
  }
  return loaded;
}

/** Global north/south selectors briefly existed even though every drawn
 * coordinate is authored once from west and mirrored with the entrant.
 * Preserve those drafts by translating their west-view meaning on load;
 * new UI and validation expose only own/centre/enemy (or either Workshop). */
function normalizeSeatRelativeSelectors(node: unknown): void {
  if (Array.isArray(node)) {
    node.forEach(normalizeSeatRelativeSelectors);
    return;
  }
  if (!node || typeof node !== 'object') return;
  const record = node as Record<string, unknown>;
  for (const [key, child] of Object.entries(record)) {
    if ((key === 'well' || key === 'workshop')
      && (child === 'north' || child === 'south')) {
      record[key] = child === 'north' ? 'enemy' : 'own';
    } else {
      normalizeSeatRelativeSelectors(child);
    }
  }
}

function normalizeZoneRows(rows: TacticConds | undefined): void {
  if (!rows) return;
  const normalize = (holder: { zones?: ZoneRef[]; zone?: ZoneRef } | undefined) => {
    if (!holder || holder.zones) return;
    if (holder.zone) {
      holder.zones = [holder.zone];
      delete holder.zone;
    }
  };
  normalize(rows.enemiesAt as { zones?: ZoneRef[]; zone?: ZoneRef } | undefined);
  normalize(rows.cleared as { zones?: ZoneRef[]; zone?: ZoneRef } | undefined);
  normalize(rows.notFighting as { zones?: ZoneRef[]; zone?: ZoneRef } | undefined);
}

export function parseDocument(json: string): TacticsDocument {
  return normalizeDocument(JSON.parse(json));
}

/** Open an editable saved draft on the map the current authoring catalog
 * actually renders. This deliberately does not project or otherwise move a
 * single coordinate: the normal current-map validation below must surface any
 * route, post or target that the new terrain made invalid. A dimension change
 * is not safe to relabel and remains an explicit migration problem. */
export function normalizeEditableDocumentForCatalog(
  document: unknown,
  catalog: TacticalSheetCatalog,
): TacticsDocument {
  const loaded = normalizeDocument(document);
  if (loaded.mapWidth !== catalog.map.width || loaded.mapHeight !== catalog.map.height) {
    throw new Error(
      `Saved map is ${loaded.mapWidth}x${loaded.mapHeight}; current map is `
      + `${catalog.map.width}x${catalog.map.height}.`,
    );
  }
  loaded.mapId = catalog.map.id;
  loaded.mapWidth = catalog.map.width;
  loaded.mapHeight = catalog.map.height;
  // Migrate the old Longshot line override to its contextual smart behavior.
  // Existing direct compiler inputs remain backwards-compatible, while every
  // document opened in the current editor stops moving the bot away from its
  // authored phase ground.
  for (const tactic of loaded.tactics) {
    for (const phase of tactic.phases) {
      for (const job of Object.values(phase.jobs)) {
        for (const [slotKey, plan] of Object.entries(job.sigs ?? {})) {
          const bot = loaded.squad[Number(slotKey)];
          if (bot?.class === 'longshot' && plan.line) {
            job.sigs![slotKey] = { policy: 'smart' };
          }
        }
      }
    }
  }
  return loaded;
}

export function stringifyDocument(document: TacticsDocument): string {
  // Saving is a normalization boundary too. Clone so serialization cannot
  // mutate the live editor draft while still guaranteeing compatibility-only
  // fields such as legacy phase targets never return to persistence.
  return JSON.stringify(normalizeDocument(structuredClone(document)), null, 2);
}

/** The document the server accepts: eight bots, at least one tactic, every
 * slot in exactly one group, phases forward-only. The editor keeps partial
 * drafts locally, so validation reports rather than throws. */
export function validateDocument(
  document: TacticsDocument,
  catalog: TacticalSheetCatalog,
): SheetIssue[] {
  const issues: SheetIssue[] = [];
  const error = (path: string, message: string) =>
    issues.push({ path, message, severity: 'error' });
  const warn = (path: string, message: string) =>
    issues.push({ path, message, severity: 'warning' });

  if (document.squad.length !== 8) {
    error('squad', `Fill all eight squad slots (${document.squad.length}/8).`);
  }
  const classCounts = new Map<string, number>();
  document.squad.forEach((bot, index) => {
    if (bot.slot !== index) error(`squad[${index}]`, 'Squad slots must be 0..7 in order.');
    const entry = catalog.classes.find((value) => value.id === bot.class);
    if (!entry) error(`squad[${index}]`, `Unknown class '${bot.class}'.`);
    else if (!entry.unlocked) error(`squad[${index}]`, `${entry.name} is not unlocked yet.`);
    classCounts.set(bot.class, (classCounts.get(bot.class) ?? 0) + 1);
    if (bot.name.trim().length === 0 || bot.name.length > 24) {
      error(`squad[${index}]`, 'Bot names need 1 to 24 characters.');
    }
  });
  for (const [classId, count] of classCounts) {
    if (count > catalog.maximumCopiesPerClass) {
      error('squad', `At most ${catalog.maximumCopiesPerClass} bots may share a class (${classId}).`);
    }
  }
  if (document.squad.length === 8
    && classCounts.size < catalog.minimumDistinctClasses) {
    error('squad', `Field at least ${catalog.minimumDistinctClasses} distinct classes (currently ${classCounts.size}).`);
  }

  if (document.tactics.length === 0) {
    error('tactics', 'Create at least one Tactic — a Sheet is its ordered Tactic list.');
  }
  if (document.tactics.length > 8) {
    error('tactics', 'A Sheet carries at most eight Tactics.');
  }

  document.tactics.forEach((tactic, tacticIndex) => {
    const at = `tactics[${tacticIndex}]`;
    const assigned = new Set<number>();
    if (tactic.groups.length === 0 || tactic.groups.length > 3) {
      error(`${at}.groups`, `${tactic.name} needs one to three Groups.`);
    }
    tactic.groups.forEach((group, groupIndex) => {
      const groupAt = `${at}.groups[${groupIndex}]`;
      if (group.slots.length === 0) {
        error(groupAt, `${group.name} has no bots — every Group needs at least one.`);
      }
      for (const slot of group.slots) {
        if (assigned.has(slot)) error(groupAt, `A bot is assigned to two Groups in ${tactic.name}.`);
        assigned.add(slot);
      }
    });
    if (document.squad.length === 8 && assigned.size !== 8) {
      error(`${at}.groups`, `Assign all eight bots in ${tactic.name} (${assigned.size}/8 assigned).`);
    }

    if (tactic.start.mode === 'when'
      && !(tactic.start.cases ?? []).some((value) => activeCaseCount(value) > 0)) {
      error(`${at}.start`, `${tactic.name} starts “only when…” but no condition is ticked.`);
    }
    validateCases(tactic.start.cases, `${at}.start`, START_ROWS, error);
    validateCases(tactic.abort?.cases, `${at}.abort`, ABORT_ROWS, error);

    if (tactic.phases.length === 0 || tactic.phases.length > 8) {
      error(`${at}.phases`, `${tactic.name} needs one to eight phases.`);
      return;
    }
    const phaseIds = tactic.phases.map((phase) => phase.id);
    if (new Set(phaseIds).size !== phaseIds.length) {
      error(`${at}.phases`, `${tactic.name} has two phases with the same id.`);
    }
    if (!tactic.phases[0]
      || !tactic.groups.some((group) =>
        jobHasGeometry(jobOf(tactic, 0, group.id)))) {
      error(`${at}.phases[0]`,
        `${possessive(tactic.name)} first phase needs at least one Group with drawn geometry.`);
    }
    tactic.phases.forEach((phase, phaseIndex) => {
      const phaseAt = `${at}.phases[${phaseIndex}]`;
      if (phase.name.trim().length === 0 || phase.name.length > 40) {
        error(phaseAt, 'Phase names need 1 to 40 characters.');
      }
      for (const group of tactic.groups) {
        const job = phase.jobs[group.id];
        if (!job) {
          error(`${phaseAt}.jobs`, `${group.name} has no job in ${phase.name}.`);
          continue;
        }
        validateJobGeometry(
          job, phaseIndex === 0, group, document.squad,
          `${phaseAt}.jobs.${group.id}`, catalog, error);
        if (job.core) {
          const coreAt = `${phaseAt}.jobs.${group.id}.core`;
          if (!CORE_RULES.some((value) => value.id === job.core!.rule)) {
            error(coreAt, `Unknown Core intent “${job.core.rule}”.`);
          }
          if (job.core.slots) {
            const selected = new Set(job.core.slots);
            if (job.core.slots.length < 1
              || selected.size !== job.core.slots.length
              || job.core.slots.some((slot) => !group.slots.includes(slot))) {
              error(coreAt, 'Core intent bots must be distinct members of this Group.');
            }
          }
          if (job.core.well
            && !['centre', 'own', 'enemy'].includes(job.core.well)) {
            error(coreAt, `“${job.core.well}” is not a source Well.`);
          }
          if (job.core.coreBankValue
            && (!['equals', 'at-least', 'at-most'].includes(job.core.coreBankValue.operator)
              || job.core.coreBankValue.value < 1
              || job.core.coreBankValue.value > 4)) {
            error(coreAt, 'Core value needs equals/at-least/at-most and a value from 1 to 4.');
          }
          if (job.core.preposition
            && (!['centre', 'own', 'enemy']
              .includes(job.core.preposition.well)
              || !Number.isInteger(job.core.preposition.ticks)
              || job.core.preposition.ticks < 1
              || job.core.preposition.ticks > 120)) {
            error(coreAt, 'Pre-position timing needs 1 to 120 ticks.');
          }
          if (job.core.rule === 'ignore'
            && (job.core.well || job.core.coreBankValue || job.core.preposition)) {
            error(coreAt, 'Ignore does not take filters or pre-positioning.');
          }
        }
        if (job.travel) {
          // Travel needs an arrival for the main posture to take over at;
          // patrol/return/escort are all march. Mirrors the server.
          const arrivalKind = effectiveJob(tactic, phaseIndex, group.id)?.kind;
          if (!arrivalKind || ['patrol', 'return', 'escort'].includes(arrivalKind)) {
            error(`${phaseAt}.jobs.${group.id}.travel`,
              `${possessive(group.name)} job is all march — there is no arrival for its “on the way” posture to hand over at.`);
          }
        }
        for (const [slotKey, plan] of Object.entries(job.sigs ?? {})) {
          const slot = Number(slotKey);
          const bot = document.squad.find((entry) => entry.slot === slot);
          const classId = bot?.class ?? '';
          const geometry = signatureMarkGeometry(catalog, classId);
          if (!Number.isInteger(slot) || !group.slots.includes(slot) || !bot) {
            error(`${phaseAt}.jobs.${group.id}.sigs`,
              `Signature plan ${slotKey} does not name a bot in ${group.name}.`);
            continue;
          }
          if (!['smart', 'prefer', 'only', 'off'].includes(plan.policy)) {
            error(`${phaseAt}.jobs.${group.id}.sigs`,
              `${possessive(bot.name)} signature policy is not recognized.`);
            continue;
          }
          const points = signaturePlanPoints(plan);
          if ((plan.policy === 'smart' || plan.policy === 'off') && points.length > 0) {
            error(`${phaseAt}.jobs.${group.id}.sigs`,
              `${possessive(bot.name)} ${plan.policy} policy cannot carry map guidance.`);
            continue;
          }
          if ((plan.policy === 'prefer' || plan.policy === 'only') && !geometry) {
            error(`${phaseAt}.jobs.${group.id}.sigs`,
              `${possessive(bot.name)} signature has no map guidance to draw.`);
            continue;
          }
          if ((plan.policy === 'prefer' || plan.policy === 'only') && points.length === 0) {
            error(`${phaseAt}.jobs.${group.id}.sigs`,
              `${possessive(bot.name)} guided signature needs map marks.`);
          }
          for (const point of points) {
            const bad = tilePlacementIssue(catalog, point, 'anchor');
            if (bad) {
              error(`${phaseAt}.jobs.${group.id}.sigs`,
                `${possessive(classId)} mark at (${point[0]},${point[1]}) is ${bad}.`);
            }
          }
          if (plan.line) {
            const [first] = plan.line;
            const last = plan.line[plan.line.length - 1];
            if (plan.line.length >= 2 && first[0] === last[0] && first[1] === last[1]) {
              error(`${phaseAt}.jobs.${group.id}.sigs`,
                `${classId}'s lane must not loop back on itself.`);
            }
          }
        }
      }
      if (phase.custody?.via) {
        const custodyAt = `${phaseAt}.custody`;
        if (phase.custody.plan === 'keep') {
          error(`${custodyAt}`, '“Keep carrying” holds the Core — there is no run home to draw.');
        } else if (phase.custody.via.length < 2 || phase.custody.via.length > 16) {
          error(`${custodyAt}`, 'The carrier’s way home needs 2 to 16 points.');
        } else {
          for (const point of phase.custody.via) {
            const bad = tilePlacementIssue(catalog, point, 'route');
            if (bad) {
              error(`${custodyAt}`, `Way-home point (${point[0]},${point[1]}) is ${bad}.`);
            }
          }
        }
      }
      const last = phaseIndex === tactic.phases.length - 1;
      const rules = phase.continue ?? [];
      if (!last && !rules.some((rule) => activeCaseCount(rule) > 0)) {
        error(`${phaseAt}.continue`,
          `${phase.name} needs a “Continue when…” condition — only the final phase may hold forever.`);
      }
      if (rules.length > 3) {
        error(`${phaseAt}.continue`, 'A phase takes at most three continue cases.');
      }
      rules.forEach((rule, ruleIndex) => {
        const ruleAt = `${phaseAt}.continue[${ruleIndex}]`;
        const expected = last ? 'finish' : 'next';
        if (rule.to !== undefined && rule.to !== expected) {
          error(ruleAt, `Phases are sequential — this phase can only ${last ? 'finish the Tactic' : 'continue to the next phase'}.`);
        }
        validateCaseRows(rule, ruleAt, TRANSITION_ROWS, error);
        for (const rows of [rule.required, rule.triggers, rule.unless]) {
          if (!rows) continue;
          if (rows.reached && !tactic.groups.some((group) =>
            ['converge', 'advance'].includes(phase.jobs[group.id]?.kind ?? ''))) {
            error(ruleAt, '“Destination reached” needs a Group that converges or advances in this phase.');
          }
          if (rows.groupsSet) {
            const readiness = rows.groupsSet === true
              ? { allowedLate: 0 as const }
              : rows.groupsSet;
            const selectedIds = readiness.groups
              ?? tactic.groups.map((group) => group.id);
            if (readiness.groups && (readiness.groups.length < 1
              || readiness.groups.length > 3
              || new Set(readiness.groups).size !== readiness.groups.length)) {
              error(ruleAt, '“Formation is ready” needs one to three unique Groups.');
            }
            const unknown = selectedIds.find((id) =>
              !tactic.groups.some((group) => group.id === id));
            if (unknown) {
              error(ruleAt, `“Formation is ready” names unknown Group “${unknown}”.`);
            } else if (selectedIds.some((groupId) =>
              !jobHasGeometry(effectiveJob(tactic, phaseIndex, groupId)))) {
              error(ruleAt, '“Formation is ready” needs every selected Group to have drawn or carried-forward geometry in this phase.');
            }
          }
          if (rows.observed && !tactic.phases.some((candidate) =>
            tactic.groups.some((group) => jobHasGeometry(candidate.jobs[group.id] ?? null)))) {
            error(ruleAt, '“Watched long enough” needs a Group with drawn geometry.');
          }
        }
      });
    });
    const finishes = tacticCanFinish(tactic);
    if (finishes.holdsForever && tactic.delivery.plan === 'none'
      && (tactic.groups.some((group) => group.coreRule !== 'ignore')
        || tactic.phases.some((phase) => Object.values(phase.jobs)
          .some((job) => job.core?.rule !== undefined
            && job.core.rule !== 'ignore')))) {
      warn(`${at}`, `${tactic.name} holds forever and may pick up Cores with no delivery plan — carriers will break off to bank on their own.`);
    }
    if (finishes.finishCarrying && tactic.delivery.plan === 'none'
      && !document.tactics.some((other) => other.recipe === 'deliver-core')) {
      warn(`${at}`, `${tactic.name} can finish holding a Core with no delivery plan — add a Deliver Core tactic or plan the run here.`);
    }
  });
  return issues;
}

function validateCases(
  cases: TacticCase[] | undefined,
  at: string,
  allowed: CondKind[],
  error: (path: string, message: string) => void,
) {
  (cases ?? []).forEach((value, index) => {
    validateCaseRows(value, `${at}.cases[${index}]`, allowed, error);
  });
}

function validateCaseRows(
  value: TacticCase,
  at: string,
  allowed: CondKind[],
  error: (path: string, message: string) => void,
) {
  if (value.unless?.groupsSet) {
    error(`${at}.unless`, '“Every Group is set” reads one way — readiness belongs in Required.');
  }
  if (value.triggers?.groupsSet) {
    error(`${at}.triggers`, '“Every Group is set” belongs in Required — all of them, not any one.');
  }
  if (value.triggers?.healed) {
    error(`${at}.triggers`, '“Everyone is healed up” belongs in Required — all of them, not any one.');
  }
  for (const [kind, holder] of [
    ['enemiesAt', value.required?.enemiesAt ?? value.triggers?.enemiesAt ?? value.unless?.enemiesAt],
    ['cleared', value.required?.cleared ?? value.triggers?.cleared ?? value.unless?.cleared],
    ['notFighting', value.required?.notFighting ?? value.triggers?.notFighting ?? value.unless?.notFighting],
  ] as const) {
    const loose = holder as unknown as { zones?: ZoneRef[]; zone?: ZoneRef } | undefined;
    const zones = loose
      ? (loose.zones ?? (loose.zone ? [loose.zone] : []))
      : [];
    if (holder && (zones.length < 1 || zones.length > 3)) {
      error(`${at}.${kind}`, `${kind} needs one to three drawn areas.`);
    }
  }
  for (const [side, rows] of [
    ['required', value.required], ['triggers', value.triggers],
    ['unless', value.unless],
  ] as const) {
    if (!rows) continue;
    if (rows.veterancy) {
      const value = rows.veterancy;
      const unique = new Set(value.slots);
      if (value.level < 2 || value.level > 5) {
        error(`${at}.${side}.veterancy`, 'Veteran level must be from 2 to 5.');
      }
      if (value.slots.length < 1 || value.slots.length > 8
        || unique.size !== value.slots.length
        || value.slots.some((slot) => !Number.isInteger(slot) || slot < 0 || slot > 7)) {
        error(`${at}.${side}.veterancy`, 'Choose one or more distinct squad bots.');
      }
      if (!Number.isInteger(value.count) || value.count < 1 || value.count > value.slots.length) {
        error(`${at}.${side}.veterancy`, 'The required count cannot exceed the selected bots.');
      }
    }
    if (rows.coreBanked && typeof rows.coreBanked === 'object') {
      if (!(['centre', 'own', 'enemy'] as const)
        .includes(rows.coreBanked.well)) {
        error(`${at}.${side}.coreBanked`, 'Choose the Core\'s source Well.');
      }
      const bankValue = rows.coreBanked.coreBankValue;
      if (bankValue
        && (!['equals', 'at-least', 'at-most'].includes(bankValue.operator)
          || !Number.isInteger(bankValue.value)
          || bankValue.value < 1 || bankValue.value > 4)) {
        error(`${at}.${side}.coreBanked`, 'Core score must compare a value from one to four.');
      }
    }
    for (const key of Object.keys(rows) as CondKind[]) {
      if (!allowed.includes(key)) {
        error(`${at}.${side}`, `'${key}' is not a condition this step takes.`);
      }
    }
    validateWorkshopRows(rows, `${at}.${side}`, error);
  }
}

/** Bounds and vocabulary for the six Workshop/veterancy rows — the server
 * grammar, mirrored so a bad value surfaces while authoring. Which CONTEXT
 * accepts each row is the ROWS_* lists above; this is the row's own shape. */
function validateWorkshopRows(
  rows: TacticConds,
  at: string,
  error: (path: string, message: string) => void,
) {
  const workshop = (kind: CondKind, value: WorkshopChoice | undefined) => {
    if (value !== undefined && !WORKSHOP_CHOICES.includes(value)) {
      error(`${at}.${kind}`, `'${kind}' needs your-side, their-side, or either — not '${value}'.`);
    } else if (value === undefined) {
      error(`${at}.${kind}`, `'${kind}' needs a Workshop chosen.`);
    }
  };
  if (rows.workshopReady) workshop('workshopReady', rows.workshopReady.workshop);
  if (rows.workshopContested) workshop('workshopContested', rows.workshopContested.workshop);
  if (rows.workshopDormant) workshop('workshopDormant', rows.workshopDormant.workshop);
  if (rows.workshopTraining) {
    workshop('workshopTraining', rows.workshopTraining.workshop);
    const team = rows.workshopTraining.team;
    if (team !== undefined && !WORKSHOP_TEAMS.includes(team)) {
      error(`${at}.workshopTraining`, `'${team}' is not a side — choose ours, theirs, or either.`);
    }
  }
  if (rows.workshopDone) {
    workshop('workshopDone', rows.workshopDone.workshop);
    const count = rows.workshopDone.count;
    if (count !== undefined && (!Number.isInteger(count) || count < 1 || count > 8)) {
      error(`${at}.workshopDone`, 'Completed trainings count from one to eight.');
    }
  }
}

/** How the tactic can end — drives the delivery nudges. */
export function tacticCanFinish(tactic: Tactic): {
  holdsForever: boolean;
  finishCarrying: boolean;
} {
  const last = tactic.phases[tactic.phases.length - 1];
  const rules = last?.continue ?? [];
  const holdsForever = (last?.continue ?? []).length === 0
    && !tactic.maxDuration
    && !tactic.abort;
  const finishCarrying = rules.some((rule) =>
    rule.required?.coreAcquired || rule.triggers?.coreAcquired);
  return { holdsForever, finishCarrying: Boolean(finishCarrying) };
}

function validateJobGeometry(
  job: TacticJob,
  firstPhase: boolean,
  group: TacticGroup,
  squad: SquadBot[],
  at: string,
  catalog: TacticalSheetCatalog,
  error: (path: string, message: string) => void,
) {
  const expected = expectedGeometry(job.kind);
  const anchorIssue = (point: Point) => tilePlacementIssue(catalog, point, 'anchor');
  const routeIssue = (point: Point) => tilePlacementIssue(catalog, point, 'route');
  validateTrainJob(job, group, at, error);
  if (expected === 'posts') {
    const posts = job.posts ?? [];
    const named = posts.filter((post) => post.slot != null);
    if (named.length > 0 && named.length < posts.length) {
      error(at, `${possessive(group.name)} posts mix named and unnamed bots — clear and redraw them.`);
    } else if (named.length === posts.length && posts.length > 0) {
      // Slot-keyed posts: exactly one per member, each naming a member.
      const botName = (slot: number) =>
        squad.find((bot) => bot.slot === slot)?.name ?? `bot ${slot + 1}`;
      const seen = new Set<number>();
      let sound = true;
      for (const post of posts) {
        if (!group.slots.includes(post.slot!)) {
          error(at, `${possessive(group.name)} post at (${post.at[0]},${post.at[1]}) belongs to ${botName(post.slot!)}, who is not in this Group.`);
          sound = false;
        } else if (seen.has(post.slot!)) {
          error(at, `${botName(post.slot!)} has two posts in ${group.name}.`);
          sound = false;
        }
        seen.add(post.slot!);
      }
      const missing = group.slots.filter((slot) => !seen.has(slot));
      if (sound && missing.length > 0) {
        error(at, `${group.name} still needs posts for ${missing.map(botName).join(', ')} (${posts.length}/${group.slots.length} placed).`);
      }
      validatePostTiles(posts, group, at, anchorIssue, routeIssue, catalog, error);
    } else if (posts.length !== group.slots.length) {
      error(at, `${group.name} needs one post per bot (${posts.length}/${group.slots.length}).`);
    } else {
      validatePostTiles(posts, group, at, anchorIssue, routeIssue, catalog, error);
    }
  } else if (expected === 'rect') {
    if (!job.rect) error(at, `${group.name} needs its area drawn.`);
  } else if (expected === 'path') {
    if (!job.path || job.path.length < 2) {
      error(at, `${group.name} needs its ${job.kind === 'line' ? 'line' : 'route'} drawn (two or more points).`);
    } else {
      for (const point of job.path) {
        const bad = routeIssue(point);
        if (bad) {
          error(at, `${possessive(group.name)} ${job.kind === 'line' ? 'line' : 'route'} point (${point[0]},${point[1]}) is ${bad}.`);
        }
      }
    }
  } else if (expected === 'point') {
    if (!job.point) {
      error(at, `${group.name} needs its ${job.kind === 'approach' ? 'approach point' : 'destination'} picked.`);
    } else {
      const bad = anchorIssue(job.point);
      if (bad) {
        error(at, `${possessive(group.name)} ${job.kind === 'approach' ? 'approach point' : 'destination'} at (${job.point[0]},${job.point[1]}) is ${bad}.`);
      }
    }
  } else if (job.kind === 'none' && firstPhase) {
    // Legal but idle: the first phase has nothing earlier to carry.
  }
  if (job.via) {
    if (!viaLegal(job.kind)) {
      error(at, `${possessive(group.name)} ${job.kind} job does not take an approach route.`);
    } else if (job.via.length < 2) {
      error(at, `${possessive(group.name)} approach route needs two or more points.`);
    } else if (job.via.length > 16) {
      error(at, `${possessive(group.name)} approach route takes at most 16 points.`);
    } else {
      for (const point of job.via) {
        const bad = routeIssue(point);
        if (bad) {
          error(at, `${possessive(group.name)} approach route point (${point[0]},${point[1]}) is ${bad}.`);
        }
      }
    }
  }
}

/** Train at Workshop, exactly as the server DSL reads it: one bot, a passive
 * posture, no signature marks, a named Workshop and no drawn geometry. Every
 * other kind refuses the Workshop key outright. */
function validateTrainJob(
  job: TacticJob,
  group: TacticGroup,
  at: string,
  error: (path: string, message: string) => void,
) {
  if (job.kind !== 'train') {
    if (job.workshop) {
      error(at, `Only a Train at Workshop job names a Workshop — ${possessive(group.name)} job is “${job.kind}”.`);
    }
    return;
  }
  if (!job.workshop) {
    error(at, `${group.name} needs a Workshop chosen — your side, their side, or either.`);
  } else if (!WORKSHOP_CHOICES.includes(job.workshop)) {
    error(at, `“${job.workshop}” is not a Workshop — choose your side, their side, or either.`);
  }
  if (group.slots.length !== 1) {
    error(at, `Training is one bot’s job — ${group.name} has ${group.slots.length} bots. Put the trainee in a Group of one and give the guards their own posted Group.`);
  }
  if (job.sigs && Object.keys(job.sigs).length > 0) {
    error(at, `A trainee casts nothing — a signature would reset the channel, so ${possessive(group.name)} marks belong to the guard Group.`);
  }
  if (job.posture !== 'wait') {
    error(at, `Training is passive — the trainee waits on the tile; any fight resets the channel. ${possessive(group.name)} posture is “${job.posture}”.`);
  }
  if (job.posts?.length || job.rect || job.path?.length || job.point) {
    error(at, `A Train at Workshop job draws nothing — the Workshop’s own training tile is the ground. Clear ${possessive(group.name)} drawn geometry.`);
  }
}

function validatePostTiles(
  posts: TacticPost[],
  group: TacticGroup,
  at: string,
  anchorIssue: (point: Point) => string | null,
  routeIssue: (point: Point) => string | null,
  catalog: TacticalSheetCatalog,
  error: (path: string, message: string) => void,
) {
  const tiles = new Set<string>();
  for (const post of posts) {
    if (!tiles.add(`${post.at[0]},${post.at[1]}`)) {
      error(at, `${possessive(group.name)} posts share the tile (${post.at[0]},${post.at[1]}) — move one.`);
    }
    const standing = anchorIssue(post.at);
    if (standing) {
      error(at, `${possessive(group.name)} post at (${post.at[0]},${post.at[1]}) stands on ${standing}.`);
      continue;
    }
    const faced = postFacingIssue(catalog, post);
    if (faced) {
      error(at, `${possessive(group.name)} post at (${post.at[0]},${post.at[1]}) faces ${faced} — turn its arrow.`);
    }
    if (post.via) {
      if (post.via.length < 2) {
        error(at, `${possessive(group.name)} post at (${post.at[0]},${post.at[1]}) has a way in with fewer than two points.`);
      } else if (post.via.length > 16) {
        error(at, `${possessive(group.name)} post at (${post.at[0]},${post.at[1]}) has a way in with more than 16 points.`);
      } else {
        for (const point of post.via) {
          const bad = routeIssue(point);
          if (bad) {
            error(at, `${possessive(group.name)} way-in point (${point[0]},${point[1]}) is ${bad}.`);
          }
        }
      }
    }
  }
}

/** Advance and patrol carry their own route; escort follows a body; none
 * carries the previous phase's plan. */
export function viaLegal(kind: JobKind): boolean {
  return kind === 'posts' || kind === 'area' || kind === 'line'
    || kind === 'approach' || kind === 'converge' || kind === 'return';
}

export function expectedGeometry(
  kind: JobKind,
): 'posts' | 'rect' | 'path' | 'point' | null {
  switch (kind) {
    case 'posts': return 'posts';
    case 'area': return 'rect';
    case 'line': case 'advance': case 'patrol': return 'path';
    case 'approach': case 'converge': return 'point';
    default: return null;
  }
}

export function jobHasGeometry(job: TacticJob | null): boolean {
  if (!job) return false;
  // A named Workshop counts as ground, exactly as the server's HasGeometry
  // does: the tile is rules-owned rather than drawn, but the group is set.
  return Boolean(job.posts?.length || job.rect || (job.path?.length ?? 0) > 1
    || job.point || (job.kind === 'train' && job.workshop));
}

/** Plans a job-kind switch without throwing away geometry the new kind can
 * still consume. Signature marks are phase-local opportunities rather than
 * job-shape geometry, so they survive every switch. The caller decides
 * whether to ask for confirmation when `removed` is non-empty. */
export function planJobKindChange(job: TacticJob, kind: JobKind): {
  next: TacticJob;
  removed: string[];
} {
  const next = structuredClone(job);
  next.kind = kind;
  const keep = expectedGeometry(kind);
  const removed: string[] = [];
  if (keep !== 'posts' && next.posts?.length) {
    removed.push(`${next.posts.length} posts and their facings`);
    delete next.posts;
  }
  if (keep !== 'rect' && next.rect) {
    removed.push('the drawn area');
    delete next.rect;
  }
  if (keep !== 'path' && next.path?.length) {
    removed.push('the patrol or travel route');
    delete next.path;
  }
  if (keep !== 'point' && next.point) {
    removed.push('the destination spot');
    delete next.point;
  }
  if (!viaLegal(kind) && next.via?.length) {
    removed.push('the drawn approach route');
    delete next.via;
  }
  // Train is the one kind whose behavior is fixed by the grammar: passive
  // posture, no marks (a signature would reset the channel) and a Workshop
  // in place of geometry. Leaving for any other kind drops the Workshop.
  if (kind === 'train') {
    if (next.sigs && Object.keys(next.sigs).length > 0) {
      removed.push('the signature marks (a trainee casts nothing)');
      delete next.sigs;
    }
    if (next.posture !== 'wait') next.posture = 'wait';
    next.workshop ??= 'either';
  } else if (next.workshop) {
    delete next.workshop;
  }
  return { next, removed };
}

/** What a bot sitting in the unassigned tray still owns: its post per phase,
 * and any signature marks whose class left a Group with it. The Groups step
 * holds one of these so a two-step move — Group to tray, tray to Group —
 * carries a bot's belongings across the stop in between. */
export interface PocketedConfig {
  /** The Group the bot left — a new Group it lands in seeds from there. */
  from?: string;
  posts: Map<string, TacticPost>;
  sigs: Map<string, SignaturePlan>;
}
export type MembershipPocket = Map<number, PocketedConfig>;

/** After Group membership changes, per-bot config follows the bots. A moved
 * bot's post — drawn way in included — leaves its old Group's job and joins
 * its new Group's posts job in every phase; a signature mark whose class no
 * longer has an owner in its old Group follows the leaver; and a Group that
 * was empty before (just created, usually) inherits a copy of its bots' one
 * source Group's job per phase, so splitting a Group changes nothing on the
 * ground until the halves are edited apart. Whatever a departing bot cannot
 * take along is cleaned away rather than left to shift silently onto the
 * old Group's remaining bots.
 *
 * `before` is the membership before the change; `pocket` keeps unassigned
 * bots' belongings across a two-step move. Returns a short summary of what
 * followed or was cleaned — null when nothing had to move. */
export function followMembership(
  tactic: Tactic,
  squad: SquadBot[],
  before: { id: string; slots: number[] }[],
  pocket?: MembershipPocket,
): string | null {
  // Retained in the public helper signature for callers that already pass
  // the sheet roster; slot-owned plans no longer need class lookup here.
  void squad;
  const prevGroupOf = new Map<number, string>();
  for (const entry of before) {
    for (const slot of entry.slots) prevGroupOf.set(slot, entry.id);
  }
  const nowGroupOf = new Map<number, string>();
  for (const group of tactic.groups) {
    for (const slot of group.slots) nowGroupOf.set(slot, group.id);
  }
  const movers = new Set<number>();
  for (const slot of new Set([...prevGroupOf.keys(), ...nowGroupOf.keys()])) {
    if (prevGroupOf.get(slot) !== nowGroupOf.get(slot)) movers.add(slot);
  }
  if (movers.size === 0) return null;

  // A Group that was empty (or absent) before the change and drew all its
  // bots from one source Group inherits that source's jobs where blank. A
  // bot arriving from the tray counts by the Group it left when pocketed,
  // so the click flow — unassign, then place — seeds just like a drag.
  const seedSource = new Map<string, string>();
  for (const group of tactic.groups) {
    const wasEmpty =
      (before.find((entry) => entry.id === group.id)?.slots.length ?? 0) === 0;
    if (!wasEmpty || group.slots.length === 0) continue;
    const sources = new Set(group.slots.map((slot) =>
      prevGroupOf.get(slot) ?? pocket?.get(slot)?.from));
    if (sources.size !== 1) continue;
    const [source] = sources;
    if (source && source !== group.id) seedSource.set(group.id, source);
  }

  const stash = (slot: number): PocketedConfig => {
    let entry = pocket!.get(slot);
    if (!entry) {
      entry = { posts: new Map(), sigs: new Map() };
      pocket!.set(slot, entry);
    }
    return entry;
  };
  let seededJobs = 0;
  let followedPosts = 0;
  let followedSigs = 0;
  let cleanedPosts = 0;
  let pocketedPosts = 0;

  for (const phase of tactic.phases) {
    // Capture every bot's post under the OLD membership (jobs are still
    // keyed to it), topped up from the pocket for bots leaving the tray.
    const postOf = new Map<number, TacticPost>();
    for (const entry of before) {
      const job = phase.jobs[entry.id];
      job?.posts?.forEach((post, index) => {
        const slot = post.slot ?? entry.slots[index];
        if (slot != null && !postOf.has(slot)) postOf.set(slot, post);
      });
    }
    for (const slot of movers) {
      if (postOf.has(slot)) continue;
      const kept = pocket?.get(slot)?.posts.get(phase.id);
      if (kept) postOf.set(slot, kept);
    }

    // Seed brand-new Groups from their source before rebuilding, so a split
    // keeps kind, posture, travel, recovery and ground identical.
    for (const [groupId, sourceId] of seedSource) {
      const current = phase.jobs[groupId];
      const blank = !current
        || (current.kind === 'none' && !jobHasGeometry(current)
          && !current.sigs && !current.via);
      const source = phase.jobs[sourceId];
      if (!blank || !source) continue;
      const seeded = structuredClone(source);
      delete seeded.posts; // rebuilt below from the movers' own posts
      delete seeded.sigs; // moved below only when a class left entirely
      phase.jobs[groupId] = seeded;
      seededJobs++;
    }

    // Rebuild every Group's posts from the captured pairing: stayers keep
    // theirs, movers' posts arrive, departed bots' posts leave. A job that
    // takes no posts, or a tile already taken, drops the incoming post
    // rather than lying about who stands where.
    for (const group of tactic.groups) {
      const job = phase.jobs[group.id];
      if (!job) continue;
      if (job.kind !== 'posts') {
        delete job.posts;
        continue;
      }
      const tiles = new Set<string>();
      const rebuilt: TacticPost[] = [];
      for (const slot of group.slots) {
        const post = postOf.get(slot);
        if (!post) continue;
        const tile = `${post.at[0]},${post.at[1]}`;
        if (tiles.has(tile)) continue;
        tiles.add(tile);
        rebuilt.push({ ...structuredClone(post), slot });
      }
      if (rebuilt.length > 0) job.posts = rebuilt;
      else delete job.posts;
    }
    for (const slot of movers) {
      const post = postOf.get(slot);
      if (!post) continue;
      const destination = nowGroupOf.get(slot);
      if (destination == null) {
        if (pocket) {
          const kept = stash(slot);
          kept.from = kept.from ?? prevGroupOf.get(slot);
          kept.posts.set(phase.id, structuredClone(post));
          pocketedPosts++;
        } else {
          cleanedPosts++;
        }
        continue;
      }
      if (phase.jobs[destination]?.posts?.some((entry) => entry.slot === slot)) {
        followedPosts++;
      } else {
        cleanedPosts++;
      }
    }

    // Signature plans are already bot-owned by stable slot, so each mover
    // simply takes its own plan. Duplicate classes never couple here.
    for (const entry of before) {
      const source = phase.jobs[entry.id];
      if (!source?.sigs) continue;
      for (const [slotKey, plan] of Object.entries(source.sigs)) {
        const slot = Number(slotKey);
        if (!movers.has(slot)) continue;
        delete source.sigs[slotKey];
        const destination = nowGroupOf.get(slot);
        if (destination == null) {
          if (pocket) {
            const pocketed = stash(slot);
            pocketed.from = pocketed.from ?? entry.id;
            pocketed.sigs.set(phase.id, structuredClone(plan));
          }
          continue;
        }
        const job = phase.jobs[destination];
        if (!job) continue;
        job.sigs = job.sigs ?? {};
        if (!job.sigs[slotKey]) {
          job.sigs[slotKey] = structuredClone(plan);
          followedSigs++;
        }
      }
      if (Object.keys(source.sigs).length === 0) delete source.sigs;
    }
    for (const slot of movers) {
      const destination = nowGroupOf.get(slot);
      if (destination == null) continue;
      const kept = pocket?.get(slot)?.sigs.get(phase.id);
      if (!kept) continue;
      const job = phase.jobs[destination];
      if (!job) continue;
      job.sigs = job.sigs ?? {};
      if (!job.sigs[String(slot)]) {
        job.sigs[String(slot)] = kept;
        followedSigs++;
      }
    }

    // A phase Core override is optional per-bot configuration inside its
    // Group. Keep only current members after a reshuffle. A freshly seeded
    // split retains the movers selected; an unrelated destination keeps its
    // own objective rather than inheriting hidden policy from the source.
    for (const group of tactic.groups) {
      const selected = phase.jobs[group.id]?.core?.slots;
      if (!selected) continue;
      const retained = selected.filter((slot) => group.slots.includes(slot));
      const job = phase.jobs[group.id]!;
      const core = job.core!;
      if (retained.length === 0) {
        delete job.core;
      } else if (retained.length === group.slots.length) {
        delete core.slots;
      } else {
        core.slots = retained;
      }
    }

    // A dissolved Group's job entries were captured above and can now go.
    for (const groupId of Object.keys(phase.jobs)) {
      if (!tactic.groups.some((group) => group.id === groupId)) {
        delete phase.jobs[groupId];
      }
    }
  }

  if (pocket) {
    for (const slot of movers) {
      if (nowGroupOf.get(slot) != null) pocket.delete(slot);
    }
  }

  const plural = (count: number) => (count === 1 ? '' : 's');
  const parts: string[] = [];
  if (seededJobs > 0) parts.push('jobs copied to the new Group');
  if (followedPosts > 0) {
    parts.push(`${followedPosts} post${plural(followedPosts)} followed`);
  }
  if (followedSigs > 0) {
    parts.push(`${followedSigs} mark${plural(followedSigs)} followed`);
  }
  if (pocketedPosts > 0) parts.push('their posts wait in the tray');
  if (cleanedPosts > 0) {
    parts.push(`${cleanedPosts} old post${plural(cleanedPosts)} cleaned away`);
  }
  return parts.length > 0 ? `${parts.join(' · ')}.` : null;
}

export function activeCondCount(conds: TacticConds | undefined): number {
  if (!conds) return 0;
  return Object.keys(conds).length;
}

export function activeCaseCount(value: TacticCase | undefined): number {
  if (!value) return 0;
  return activeCondCount(value.required) + activeCondCount(value.triggers);
}

/** Map a server compile error (`…tactics.json.tactics[2].groups[0]…`) back to
 * a document path the editor can reveal. */
export function sheetIssueFromCompilerError(message: string): SheetIssue {
  const match = /tactics\.json[.)]*[:.]?\s*(?<path>[a-zA-Z0-9_[\].-]*)?[:.]?\s*(?<rest>.*)/.exec(message);
  const path = match?.groups?.path ?? '';
  return {
    path: path.replace(/^\./, ''),
    message,
    severity: 'error',
  };
}
