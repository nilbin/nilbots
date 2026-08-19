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
  | 'converge' | 'advance' | 'patrol' | 'return' | 'escort' | 'none';
export type Posture = 'wait' | 'hold' | 'aggressive' | 'steal' | 'escort' | 'observe';
export type Recovery = 'fight-on' | 'patch' | 'heal-tile' | 'heal-up';
export type CoreRule = 'ignore' | 'grab' | 'hunt';
export type DeliveryPlan = 'none' | 'solo' | 'escort';
export type MaxDuration = 'none' | 'brief' | 'standard' | 'long';
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

/** Every condition row any case context can carry. Which rows a given
 * context accepts is the ROWS_* lists below, mirroring the server. */
export interface TacticConds {
  /** Every Group is at its ground this phase — the dead excuse themselves. */
  groupsSet?: true;
  /** We have been at this phase for N ticks. */
  held?: { ticks: number };
  /** Every LIVING bot is at ≥N% health — the dead excuse themselves. */
  healed?: { percent: number };
  enemiesAt?: { count: number; zone: ZoneRef };
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
  kills?: { count: number };
  cleared?: { zone: ZoneRef };
  coreAcquired?: true;
  coreBanked?: true;
  coreLost?: true;
  reached?: true;
  observed?: true;
}

export type CondKind = keyof TacticConds;

export const START_ROWS: CondKind[] = [
  'enemiesAt', 'coreLoose', 'coreSoon', 'enemiesDown', 'carrier', 'losses',
  'lossesWithin',
];

export const TRANSITION_ROWS: CondKind[] = [
  'groupsSet', 'held', 'healed', 'contact', 'enemiesAt', 'coreLoose',
  'coreSoon', 'enemiesDown', 'kills', 'losses', 'cleared', 'coreAcquired',
  'coreBanked', 'coreLost', 'reached', 'observed',
];

export const ABORT_ROWS: CondKind[] = [
  'losses', 'lossesWithin', 'coreLost', 'enemiesAt',
];

/** all(required) AND any(triggers); an absent side is vacuously true. */
export interface TacticCase {
  required?: TacticConds;
  triggers?: TacticConds;
}

/** One forward-only latched exit from a phase. */
export interface TacticTransition extends TacticCase {
  /** 'next', 'finish', or a LATER phase's id. */
  to: string;
}

export interface TacticPost {
  at: Point;
  facing: Facing;
  /** This post's own drawn way in — overrides the group route for its bot. */
  via?: Point[];
}

export type SignatureMark = { points: Point[] } | { line: Point[] };

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
  sigs?: Record<string, SignatureMark>;
}

export interface TacticPhaseDef {
  id: string;
  name: string;
  jobs: Record<string, TacticJob>;
  /** Absent/empty on the final phase = the operation holds there forever. */
  continue?: TacticTransition[];
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
  { id: 'heal-up', label: 'Heal up fully', blurb: 'Break for the heal tile at any damage and stay until completely full — pair with an “Everyone is healed up” condition.' },
];

/** The one job vocabulary every phase shares. 'none' reads differently on
 * the first phase (nothing to do) than later (carry the last plan on). */
export const JOB_KINDS: { id: JobKind; label: string; blurb: string; draw: DrawKind | null }[] = [
  { id: 'posts', label: 'Take up posts', blurb: 'Exact tiles — one per bot.', draw: 'posts' },
  { id: 'area', label: 'Hold an area', blurb: 'Gather anywhere inside a box.', draw: 'rect' },
  { id: 'line', label: 'Spread along a line', blurb: 'A picket between two or more points.', draw: 'route' },
  { id: 'approach', label: 'Approach a point', blurb: 'Move up quietly and wait short of it.', draw: 'point' },
  { id: 'converge', label: 'Converge on a spot', blurb: 'Pick the destination — the route finds itself.', draw: 'point' },
  { id: 'advance', label: 'Advance a route', blurb: 'Push waypoint to waypoint.', draw: 'route' },
  { id: 'patrol', label: 'Patrol a loop', blurb: 'Walk the beat until the phase moves on.', draw: 'route' },
  { id: 'return', label: 'Return and bank', blurb: 'Run it home to your reactor. No drawing needed.', draw: null },
  { id: 'escort', label: 'Stay on the carrier', blurb: 'Shadow whoever holds the Core.', draw: null },
  { id: 'none', label: 'Carry on', blurb: 'Keep the previous phase’s job under this phase’s posture.', draw: null },
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
 * Anchors (posts, facing targets, points, marks) must be open, unreserved
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

/** The post's facing-target problem, if any: out-of-bounds targets are fine
 * (no facing anchor is minted), everything else follows the anchor rule. */
export function postFacingIssue(
  catalog: TacticalSheetCatalog,
  post: TacticPost,
): string | null {
  const [dx, dy] = FACING_DELTAS[post.facing];
  const target: Point = [post.at[0] + dx, post.at[1] + dy];
  if (target[0] < 0 || target[1] < 0
    || target[0] >= catalog.map.width || target[1] >= catalog.map.height) {
    return null;
  }
  return tilePlacementIssue(catalog, target, 'anchor');
}

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
  const raw = jobOf(tactic, phaseIndex, groupId);
  if (!raw) return null;
  if (raw.kind !== 'none') return raw;
  for (let earlier = phaseIndex - 1; earlier >= 0; earlier--) {
    const candidate = jobOf(tactic, earlier, groupId);
    if (candidate && candidate.kind !== 'none') {
      return {
        ...candidate,
        posture: raw.posture,
        travel: raw.travel,
        recovery: raw.recovery,
      };
    }
  }
  return null;
}

export function freshJob(kind: JobKind, posture: Posture): TacticJob {
  return {
    kind,
    posture,
    recovery: ['aggressive', 'steal'].includes(posture) ? 'fight-on' : 'patch',
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
  return { id, name, jobs, continue: [{ triggers: {}, to: 'next' }] };
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
      ? [{ triggers: outcomes, to: 'finish' }]
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
      to: 'next',
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
  if (sigs && Object.keys(sigs).length > 0) base.sigs = structuredClone(sigs);
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
  if (value?.schema === 'arc-relay-tactics-v1') {
    return migrateDocument(value as Record<string, unknown>);
  }
  if (value?.schema !== 'arc-relay-tactics-v2') {
    throw new Error(`Not a tactics document: ${value?.schema}`);
  }
  return value as TacticsDocument;
}

export function parseDocument(json: string): TacticsDocument {
  return normalizeDocument(JSON.parse(json));
}

export function stringifyDocument(document: TacticsDocument): string {
  return JSON.stringify(document, null, 2);
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
          job, phaseIndex === 0, group, `${phaseAt}.jobs.${group.id}`,
          catalog, error);
        if (job.travel) {
          // Travel needs an arrival for the main posture to take over at;
          // patrol/return/escort are all march. Mirrors the server.
          const arrivalKind = effectiveJob(tactic, phaseIndex, group.id)?.kind;
          if (!arrivalKind || ['patrol', 'return', 'escort'].includes(arrivalKind)) {
            error(`${phaseAt}.jobs.${group.id}.travel`,
              `${possessive(group.name)} job is all march — there is no arrival for its “on the way” posture to hand over at.`);
          }
        }
        for (const [classId, mark] of Object.entries(job.sigs ?? {})) {
          const geometry = signatureMarkGeometry(catalog, classId);
          const owned = group.slots.some((slot) =>
            document.squad[slot]?.class === classId);
          if (!owned) {
            error(`${phaseAt}.jobs.${group.id}.sigs`,
              `No ${classId} in ${group.name} for its signature marks.`);
            continue;
          }
          if (!geometry) {
            error(`${phaseAt}.jobs.${group.id}.sigs`,
              `${classId}'s signature is bundled by posture — nothing to draw.`);
            continue;
          }
          const points = 'points' in mark ? mark.points : mark.line;
          for (const point of points) {
            const bad = tilePlacementIssue(catalog, point, 'anchor');
            if (bad) {
              error(`${phaseAt}.jobs.${group.id}.sigs`,
                `${possessive(classId)} mark at (${point[0]},${point[1]}) is ${bad}.`);
            }
          }
          if ('line' in mark) {
            const [first] = mark.line;
            const last = mark.line[mark.line.length - 1];
            if (mark.line.length >= 2 && first[0] === last[0] && first[1] === last[1]) {
              error(`${phaseAt}.jobs.${group.id}.sigs`,
                `${classId}'s lane must not loop back on itself.`);
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
        if (rule.to === 'next') {
          if (last) error(`${ruleAt}`, 'The final phase has no next — it can only finish.');
        } else if (rule.to !== 'finish') {
          const target = phaseIds.indexOf(rule.to);
          if (target < 0) error(ruleAt, `Unknown phase '${rule.to}'.`);
          else if (target <= phaseIndex) {
            error(ruleAt, 'Transitions are forward-only — a phase may not jump to itself or backward.');
          }
        }
        validateCaseRows(rule, ruleAt, TRANSITION_ROWS, error);
        if (rule.triggers?.groupsSet) {
          error(`${ruleAt}`, '“Every Group is set” belongs in Required — all of them, not any one.');
        }
        if (rule.triggers?.healed) {
          error(`${ruleAt}`, '“Everyone is healed up” belongs in Required — all of them, not any one.');
        }
        for (const rows of [rule.required, rule.triggers]) {
          if (!rows) continue;
          if (rows.reached && !tactic.groups.some((group) =>
            ['converge', 'advance'].includes(phase.jobs[group.id]?.kind ?? ''))) {
            error(ruleAt, '“Destination reached” needs a Group that converges or advances in this phase.');
          }
          if (rows.groupsSet && !tactic.groups.some((group) =>
            jobHasGeometry(phase.jobs[group.id] ?? null))) {
            error(ruleAt, '“Every Group is set” needs at least one Group with drawn geometry in this phase.');
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
      && tactic.groups.some((group) => group.coreRule !== 'ignore')) {
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
  for (const [side, rows] of [
    ['required', value.required], ['triggers', value.triggers],
  ] as const) {
    if (!rows) continue;
    for (const key of Object.keys(rows) as CondKind[]) {
      if (!allowed.includes(key)) {
        error(`${at}.${side}`, `'${key}' is not a condition this step takes.`);
      }
    }
  }
}

/** How the tactic can end — drives the delivery nudges. */
export function tacticCanFinish(tactic: Tactic): {
  holdsForever: boolean;
  finishCarrying: boolean;
} {
  const last = tactic.phases[tactic.phases.length - 1];
  const rules = tactic.phases.flatMap((phase) => phase.continue ?? []);
  const holdsForever = (last?.continue ?? []).length === 0
    && !tactic.maxDuration
    && !tactic.abort;
  const finishCarrying = rules.some((rule) => rule.to === 'finish'
    && (rule.required?.coreAcquired || rule.triggers?.coreAcquired));
  return { holdsForever, finishCarrying: Boolean(finishCarrying) };
}

function validateJobGeometry(
  job: TacticJob,
  firstPhase: boolean,
  group: TacticGroup,
  at: string,
  catalog: TacticalSheetCatalog,
  error: (path: string, message: string) => void,
) {
  const expected = expectedGeometry(job.kind);
  const anchorIssue = (point: Point) => tilePlacementIssue(catalog, point, 'anchor');
  const routeIssue = (point: Point) => tilePlacementIssue(catalog, point, 'route');
  if (expected === 'posts') {
    if (!job.posts || job.posts.length !== group.slots.length) {
      error(at, `${group.name} needs one post per bot (${job.posts?.length ?? 0}/${group.slots.length}).`);
    } else {
      for (const post of job.posts) {
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
  return Boolean(job.posts?.length || job.rect || (job.path?.length ?? 0) > 1 || job.point);
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
