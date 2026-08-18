import type { TacticalSheetCatalog } from '../api';
import { possessive } from './summary';

/**
 * The player-owned `arc-relay-tactics-v1` document, mirrored from the server
 * grammar in `ArcRelayTacticsDsl.cs`. The server compile is the authority;
 * this file gives the editor the same structural and bounds checks so most
 * problems surface while drawing, not on save.
 */

export type Facing = 'n' | 'e' | 's' | 'w';
export type StageKind = 'posts' | 'area' | 'line' | 'approach' | 'none';
export type ActKind = 'none' | 'converge' | 'advance' | 'patrol' | 'return' | 'escort';
export type Posture = 'wait' | 'hold' | 'aggressive' | 'steal' | 'escort' | 'observe';
export type Recovery = 'fight-on' | 'patch' | 'heal-tile';
export type CoreRule = 'ignore' | 'grab' | 'hunt';
export type DeliveryPlan = 'none' | 'solo' | 'escort';
export type MaxAttempt = 'none' | 'brief' | 'standard' | 'long';
export type ReleaseMode = 'immediate' | 'ready' | 'ready-when';
export type StartMode = 'always' | 'when';
export type Build = 'balanced' | 'durable' | 'aggressive' | 'scout';
export type RecipeId =
  | 'hold-area' | 'centre-rush' | 'pincer' | 'core-raid' | 'patrol'
  | 'deliver-core' | 'blank';
export type WellChoice = 'north' | 'centre' | 'south' | 'own' | 'enemy';

export type Point = [number, number];
export type Rect = [number, number, number, number];

export interface ZoneRef {
  name?: string;
  rect: Rect;
}

export interface TacticConds {
  enemiesAt?: { count: number; zone: ZoneRef };
  contact?: true;
  coreLoose?: { well: WellChoice };
  carrier?: true;
  losses?: { count: number };
}

export interface TacticOutcomes {
  kills?: { count: number };
  losses?: { count: number };
  cleared?: { zone: ZoneRef };
  coreAcquired?: true;
  coreBanked?: true;
  coreLost?: true;
  reached?: true;
  observed?: true;
  persistent?: true;
}

export interface TacticPost {
  at: Point;
  facing: Facing;
}

export interface TacticPhase {
  kind: StageKind | ActKind;
  posture: Posture;
  recovery: Recovery;
  posts?: TacticPost[];
  rect?: Rect;
  path?: Point[];
  point?: Point;
  /** Optional drawn approach route; the group walks it in, then takes the floor. */
  via?: Point[];
}

export type SignatureMark = { points: Point[] } | { line: Point[] };

export interface TacticGroup {
  id: string;
  name: string;
  slots: number[];
  coreRule: CoreRule;
  stage: TacticPhase;
  act: TacticPhase;
  sigs?: {
    stage?: Record<string, SignatureMark>;
    act?: Record<string, SignatureMark>;
  };
}

export interface Tactic {
  id: string;
  name: string;
  recipe: RecipeId;
  start: { mode: StartMode; conds?: TacticConds };
  release: { mode: ReleaseMode; conds?: TacticConds };
  finish: { outcomes: TacticOutcomes; maxAttempt: MaxAttempt };
  delivery: { plan: DeliveryPlan; preferredSlots?: number[] };
  groups: TacticGroup[];
}

export interface SquadBot {
  slot: number;
  class: string;
  build: Build;
  name: string;
}

export interface TacticsDocument {
  schema: 'arc-relay-tactics-v1';
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
  { id: 'wait', label: 'Wait', blurb: 'Keep quiet, keep out of fights.' },
  { id: 'hold', label: 'Hold', blurb: 'Fight for this ground, don’t chase.' },
  { id: 'aggressive', label: 'Aggressive', blurb: 'Chase, commit, finish kills.' },
  { id: 'steal', label: 'Steal', blurb: 'Get the Core; fight only to take it.' },
  { id: 'escort', label: 'Escort', blurb: 'Screen the carrier; peel attackers.' },
  { id: 'observe', label: 'Observe', blurb: 'Watch and report; break contact.' },
];

export const RECOVERIES: { id: Recovery; label: string; blurb: string }[] = [
  { id: 'fight-on', label: 'Fight on', blurb: 'No falling back — hold the plan whatever the damage.' },
  { id: 'patch', label: 'Patch up close', blurb: 'Step back a moment, let repairs land, slot back in.' },
  { id: 'heal-tile', label: 'Break for the heal tile', blurb: 'Badly hurt bots run to the nearest heal tile, then return.' },
];

export const STAGE_KINDS: { id: StageKind; label: string; blurb: string; draw: DrawKind | null }[] = [
  { id: 'posts', label: 'Take up posts', blurb: 'Exact tiles — one per bot.', draw: 'posts' },
  { id: 'area', label: 'Assemble in an area', blurb: 'Gather anywhere inside a box.', draw: 'rect' },
  { id: 'line', label: 'Spread along a line', blurb: 'A picket between two or more points.', draw: 'route' },
  { id: 'approach', label: 'Approach a point', blurb: 'Move up quietly and wait short of it.', draw: 'point' },
  { id: 'none', label: 'No stage', blurb: 'Straight to work on release.', draw: null },
];

export const ACT_KINDS: { id: ActKind; label: string; blurb: string; draw: DrawKind | null }[] = [
  { id: 'none', label: 'Keep at it', blurb: 'Continue the Stage job after release.', draw: null },
  { id: 'converge', label: 'Converge on a spot', blurb: 'Pick the destination — the route finds itself.', draw: 'point' },
  { id: 'advance', label: 'Advance a route', blurb: 'Push waypoint to waypoint.', draw: 'route' },
  { id: 'patrol', label: 'Patrol a loop', blurb: 'Walk the beat until told otherwise.', draw: 'route' },
  { id: 'return', label: 'Return and bank', blurb: 'Run it home to your reactor. No drawing needed.', draw: null },
  { id: 'escort', label: 'Stay on the carrier', blurb: 'Shadow whoever holds the Core.', draw: null },
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

export const MAX_ATTEMPTS: { id: MaxAttempt; label: string; blurb: string }[] = [
  { id: 'none', label: 'No limit', blurb: 'Runs until an outcome lands.' },
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

export const GROUP_COLORS = ['#22d3ee', '#f0a35e', '#b49cff'];

export function groupColor(tactic: Tactic, groupId: string): string {
  const index = tactic.groups.findIndex((group) => group.id === groupId);
  return GROUP_COLORS[Math.max(0, index) % GROUP_COLORS.length];
}

/** Anchor-mark budgets, mirroring the server compile's
 * `TacticAnchorMarkBudgets` (ArcRelayTacticsDsl.cs) byte for byte — the
 * editor must not let a player place a mark the compile then rejects. */
const ANCHOR_MARK_BUDGETS: Record<string, number> = {
  lantern: 2, mortar: 2, minesmith: 3, mason: 2, veil: 2, nest: 2,
};

/** Each spatial signature's map glyph, from the design mock's vocabulary. */
const SIGNATURE_ICONS: Record<string, string> = {
  mason: '▬', nest: '◎', minesmith: '▴', lantern: '✳', mortar: '✛',
  veil: '☁', kestrel: '》', longshot: '━',
};

/** Which spatial signature geometry a class draws, derived from the
 * catalog's descriptor capabilities — never from a class-id switch. */
export function signatureMarkGeometry(
  catalog: TacticalSheetCatalog,
  classId: string,
): { kind: 'points' | 'line'; max: number; label: string; icon: string } | null {
  const entry = catalog.classes.find((value) => value.id === classId);
  if (!entry) return null;
  const authoring = entry.signatureAuthoring;
  const icon = SIGNATURE_ICONS[classId] ?? '✦';
  if (authoring.allowsExactAnchor) {
    const max = ANCHOR_MARK_BUDGETS[classId] ?? 2;
    return { kind: 'points', max, label: entry.signatureName, icon };
  }
  if (authoring.allowsRouteForward) {
    return { kind: 'line', max: 8, label: entry.signatureName, icon };
  }
  if (classId === 'longshot') {
    return { kind: 'line', max: 2, label: entry.signatureName, icon };
  }
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
    schema: 'arc-relay-tactics-v1',
    sheetId: 'new-sheet',
    mapId: catalog.map.id,
    mapWidth: catalog.map.width,
    mapHeight: catalog.map.height,
    squad: [],
    tactics: [],
  };
}

export function parseDocument(json: string): TacticsDocument {
  const parsed = JSON.parse(json) as TacticsDocument;
  if (parsed.schema !== 'arc-relay-tactics-v1') {
    throw new Error(`Not a tactics document: ${parsed.schema}`);
  }
  return parsed;
}

export function stringifyDocument(document: TacticsDocument): string {
  return JSON.stringify(document, null, 2);
}

/** The document the server accepts: eight bots, at least one tactic, every
 * slot in exactly one group. The editor keeps partial drafts locally, so
 * validation reports rather than throws. */
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
  const width = document.mapWidth;
  const height = document.mapHeight;
  const inBounds = ([x, y]: Point) => x >= 0 && y >= 0 && x < width && y < height;
  const floor = ([x, y]: Point) =>
    inBounds([x, y]) && catalog.map.tileRows[y]?.[x] !== '#';

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
      validatePhaseGeometry(group.stage, true, group, groupAt, floor, error);
      validatePhaseGeometry(group.act, false, group, `${groupAt}.act`, floor, error);
      if (group.stage.kind === 'none' && group.act.kind === 'none'
        && tactic.groups.length === 1) {
        error(groupAt, `${group.name} has no plan in either phase.`);
      }
      const sigPhases: ('stage' | 'act')[] = ['stage', 'act'];
      for (const phase of sigPhases) {
        for (const [classId, mark] of Object.entries(group.sigs?.[phase] ?? {})) {
          const geometry = signatureMarkGeometry(catalog, classId);
          const owned = group.slots.some((slot) =>
            document.squad[slot]?.class === classId);
          if (!owned) {
            error(`${groupAt}.sigs`, `No ${classId} in ${group.name} for its signature marks.`);
            continue;
          }
          if (!geometry) {
            error(`${groupAt}.sigs`, `${classId}'s signature is bundled by posture — nothing to draw.`);
            continue;
          }
          const points = 'points' in mark ? mark.points : mark.line;
          if (points.some((point) => !floor(point))) {
            error(`${groupAt}.sigs`, `${classId}'s marks must sit on open floor.`);
          }
          if ('line' in mark) {
            const [first] = mark.line;
            const last = mark.line[mark.line.length - 1];
            if (mark.line.length >= 2 && first[0] === last[0] && first[1] === last[1]) {
              error(`${groupAt}.sigs`, `${classId}'s lane must not loop back on itself.`);
            }
          }
        }
      }
    });
    if (document.squad.length === 8 && assigned.size !== 8) {
      error(`${at}.groups`, `Assign all eight bots in ${tactic.name} (${assigned.size}/8 assigned).`);
    }
    if (tactic.start.mode === 'when' && activeCondCount(tactic.start.conds) === 0) {
      error(`${at}.start`, `${tactic.name} starts “only when…” but no condition is ticked.`);
    }
    if (tactic.release.mode === 'ready-when'
      && activeCondCount(tactic.release.conds) === 0) {
      error(`${at}.release`, `${possessive(tactic.name)} release needs at least one condition ticked.`);
    }
    const outcomes = tactic.finish.outcomes;
    const anyOutcome = Object.keys(outcomes).length > 0;
    if (!anyOutcome && tactic.finish.maxAttempt === 'none') {
      error(`${at}.finish`, `Pick at least one outcome (or a time limit) so ${tactic.name} can finish.`);
    }
    if (outcomes.reached
      && !tactic.groups.some((group) => group.act.kind === 'converge' || group.act.kind === 'advance')) {
      error(`${at}.finish`, `“Destination reached” needs a Group that converges or advances somewhere.`);
    }
    if (outcomes.observed
      && !tactic.groups.some((group) => phaseHasGeometry(group.stage) || phaseHasGeometry(group.act))) {
      error(`${at}.finish`, `“Watched long enough” needs a Group with drawn geometry.`);
    }
    if (outcomes.coreAcquired && tactic.delivery.plan === 'none'
      && !document.tactics.some((other) => other.recipe === 'deliver-core')) {
      warn(`${at}.finish`, `${tactic.name} can finish holding a Core with no delivery plan — add a Deliver Core tactic or plan the run here.`);
    }
  });
  return issues;
}

function validatePhaseGeometry(
  phase: TacticPhase,
  isStage: boolean,
  group: TacticGroup,
  at: string,
  floor: (point: Point) => boolean,
  error: (path: string, message: string) => void,
) {
  const expected = expectedGeometry(phase.kind);
  if (expected === 'posts') {
    if (!phase.posts || phase.posts.length !== group.slots.length) {
      error(at, `${group.name} needs one post per bot (${phase.posts?.length ?? 0}/${group.slots.length}).`);
    } else if (phase.posts.some((post) => !floor(post.at))) {
      error(at, `${possessive(group.name)} posts must sit on open floor.`);
    }
  } else if (expected === 'rect') {
    if (!phase.rect) error(at, `${group.name} needs its ${isStage ? 'staging area' : 'area'} drawn.`);
  } else if (expected === 'path') {
    if (!phase.path || phase.path.length < 2) {
      error(at, `${group.name} needs its ${phase.kind === 'line' ? 'line' : 'route'} drawn (two or more points).`);
    } else if (phase.path.some((point) => !floor(point))) {
      error(at, `${possessive(group.name)} ${phase.kind === 'line' ? 'line' : 'route'} points must sit on open floor.`);
    }
  } else if (expected === 'point') {
    if (!phase.point) {
      error(at, `${group.name} needs its ${phase.kind === 'approach' ? 'approach point' : 'destination'} picked.`);
    } else if (!floor(phase.point)) {
      error(at, `${possessive(group.name)} ${phase.kind === 'approach' ? 'approach point' : 'destination'} must be open floor.`);
    }
  }
  if (phase.via) {
    if (!viaLegal(phase.kind, isStage)) {
      error(at, `${possessive(group.name)} ${phase.kind} ${isStage ? 'staging' : 'act'} does not take an approach route.`);
    } else if (phase.via.length < 2) {
      error(at, `${possessive(group.name)} approach route needs two or more points.`);
    } else if (phase.via.length > 16) {
      error(at, `${possessive(group.name)} approach route takes at most 16 points.`);
    } else if (phase.via.some((point) => !floor(point))) {
      error(at, `${possessive(group.name)} approach route points must sit on open floor.`);
    }
  }
}

/** Advance and patrol carry their own route; escort follows a body; none goes nowhere. */
export function viaLegal(kind: StageKind | ActKind, isStage: boolean): boolean {
  return isStage
    ? kind === 'posts' || kind === 'area' || kind === 'line' || kind === 'approach'
    : kind === 'converge' || kind === 'return';
}

export function expectedGeometry(
  kind: StageKind | ActKind,
): 'posts' | 'rect' | 'path' | 'point' | null {
  switch (kind) {
    case 'posts': return 'posts';
    case 'area': return 'rect';
    case 'line': case 'advance': case 'patrol': return 'path';
    case 'approach': case 'converge': return 'point';
    default: return null;
  }
}

export function phaseHasGeometry(phase: TacticPhase): boolean {
  return Boolean(phase.posts?.length || phase.rect || (phase.path?.length ?? 0) > 1 || phase.point);
}

export function activeCondCount(conds: TacticConds | undefined): number {
  if (!conds) return 0;
  return (conds.enemiesAt ? 1 : 0) + (conds.contact ? 1 : 0)
    + (conds.coreLoose ? 1 : 0) + (conds.carrier ? 1 : 0)
    + (conds.losses ? 1 : 0);
}

/** Map a server compile error (`…tactics.json.tactics[2].groups[0]…`) back to
 * a document path the editor can reveal. */
export function sheetIssueFromCompilerError(message: string): SheetIssue {
  const match = /tactics\.json[.)]*[:.]?\s*(?<path>[a-zA-Z0-9_[\].]*)?[:.]?\s*(?<rest>.*)/.exec(message);
  const path = match?.groups?.path ?? '';
  return {
    path: path.replace(/^\./, ''),
    message,
    severity: 'error',
  };
}
