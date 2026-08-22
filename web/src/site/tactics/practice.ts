import type { Tactic, TacticsDocument } from './tacticsDocument';

/**
 * Practice attribution: the materializer mints ordinal ids
 * (`vex > t2 > t2-p1 > t2-p1-g1-move`), the coordinator prefixes every
 * decision with the active tactic/phase (`orders:t2/p1 …`, `orders:idle …`)
 * and appends `!markers` on transition/abort ticks — so a replay command
 * maps straight back to the Tactic, Group and named phase the player
 * authored, with no sidecar bookkeeping. Pre-program replays (the old
 * `t2-go`/`t2-s` shape) still parse.
 */

export interface TacticAttribution {
  roleId: string;
  tacticOrdinal: number | null;
  tactic: Tactic | null;
  groupOrdinal: number | null;
  groupName: string | null;
  /** Ordinal of the leaf's phase (1-based), null for invariants/baseline. */
  phaseOrdinal: number | null;
  phaseName: string | null;
  kind: 'phase' | 'invariant' | 'baseline';
  /** The coordinator's transition/abort/begin marker on this tick, if any. */
  marker: string | null;
  leafId: string;
}

const PROGRAM_PREFIX = /orders:(idle|[a-z0-9-]+\/[a-z0-9-]+)(?:!([^ ]+))? /;
const PATH_PATTERN = /([a-z0-9-]+) > ([a-z0-9-]+)(?: > ([a-z0-9-]+))?(?: > ([a-z0-9-]+))?/;

export function parseTacticAttribution(
  debugMessage: string,
  document: TacticsDocument | null,
): TacticAttribution | null {
  const at = debugMessage.indexOf('orders:');
  if (at < 0) return null;
  let rest = debugMessage.slice(at + 'orders:'.length);
  let marker: string | null = null;
  const program = PROGRAM_PREFIX.exec(debugMessage.slice(at));
  if (program) {
    marker = program[2] ?? null;
    rest = debugMessage.slice(at + program[0].length);
  }
  const match = PATH_PATTERN.exec(rest);
  if (!match) return null;
  const [, roleId, root, second, third] = match;
  const leafId = third ?? second ?? root;
  const ordinalMatch = /^t(\d+)$/.exec(root);
  if (!ordinalMatch) {
    return {
      roleId,
      tacticOrdinal: null,
      tactic: null,
      groupOrdinal: null,
      groupName: null,
      phaseOrdinal: null,
      phaseName: null,
      kind: 'baseline',
      marker,
      leafId,
    };
  }
  const ordinal = Number(ordinalMatch[1]);
  const tactic = document?.tactics[ordinal - 1] ?? null;
  // Program leaves: t2-p1-g1-move; tactic-scoped invariants: t2-g1-bank.
  // Legacy leaves: t2-g1-s-move under t2-s / t2-go wrappers.
  const phasedLeaf = /^t\d+-p(\d+)-g(\d+)-/.exec(leafId);
  const invariantLeaf = /^t\d+-g(\d+)-/.exec(leafId);
  const legacyLeaf = /^t\d+-g(\d+)-(s|a)-/.exec(leafId);
  let phaseOrdinal: number | null = null;
  let groupOrdinal: number | null = null;
  let kind: TacticAttribution['kind'] = 'invariant';
  if (phasedLeaf) {
    phaseOrdinal = Number(phasedLeaf[1]);
    groupOrdinal = Number(phasedLeaf[2]);
    kind = 'phase';
  } else if (legacyLeaf) {
    groupOrdinal = Number(legacyLeaf[1]);
    phaseOrdinal = legacyLeaf[2] === 's' ? 1 : 2;
    kind = 'phase';
  } else if (second === `${root}-go`) {
    phaseOrdinal = 2;
    kind = 'phase';
  } else if (second === `${root}-s`) {
    phaseOrdinal = 1;
    kind = 'phase';
  } else if (invariantLeaf) {
    groupOrdinal = Number(invariantLeaf[1]);
    kind = 'invariant';
  }
  const phase = phaseOrdinal !== null
    ? tactic?.phases?.[phaseOrdinal - 1] ?? null
    : null;
  return {
    roleId,
    tacticOrdinal: ordinal,
    tactic,
    groupOrdinal,
    groupName: groupOrdinal !== null
      ? tactic?.groups[groupOrdinal - 1]?.name ?? null
      : null,
    phaseOrdinal,
    phaseName: phase?.name ?? null,
    kind,
    marker,
    leafId,
  };
}

/** A short human label: “Pincer · Flankers · Close the trap” or “between tactics”. */
export function attributionLabel(attribution: TacticAttribution): string {
  if (attribution.kind === 'baseline') return 'between tactics';
  const tacticName = attribution.tactic?.name
    ?? (attribution.tacticOrdinal !== null ? `Tactic ${attribution.tacticOrdinal}` : 'a tactic');
  const parts = [tacticName];
  if (attribution.groupName) parts.push(attribution.groupName);
  if (attribution.kind === 'phase') {
    parts.push(attribution.phaseName
      ?? (attribution.phaseOrdinal !== null ? `Phase ${attribution.phaseOrdinal}` : 'a phase'));
  }
  return parts.join(' · ');
}

/**
 * Training bodies read back the same way every other order chip does — from
 * the executor's own decision reason, with no sidecar bookkeeping. A train
 * job materializes as a move whose target key is the resolved Workshop
 * (`move:workshop:north`), and the tile is named `at-target:workshop:north`
 * the moment the trainee is standing on it.
 */
const TRAIN_TARGET = /(?:^|[:\s])workshop:([a-z][a-z0-9-]*)/;
const TRAIN_ON_TILE = /at-target:workshop:([a-z][a-z0-9-]*)/;

export interface TrainingIntent {
  /** The Workshop the executor resolved for this body ('north' / 'south'). */
  workshopId: string;
  /** True once the trainee is standing on the rules-owned training tile. */
  onTile: boolean;
}

/** The trainee's own intent this tick, or null when the body is doing
 * something else entirely. */
export function parseTrainingIntent(debugMessage: string): TrainingIntent | null {
  const standing = TRAIN_ON_TILE.exec(debugMessage);
  if (standing) return { workshopId: standing[1], onTile: true };
  const walking = TRAIN_TARGET.exec(debugMessage);
  return walking ? { workshopId: walking[1], onTile: false } : null;
}

/** One Workshop's public state, exactly as the replay publishes it. Both
 * seats see this, so a readback built on it reveals nothing fogged. */
export interface WorkshopReadState {
  workshopId: string;
  phase: 'ready' | 'training' | 'contested' | 'dormant';
  /** Team channelling it; null in every phase but `training`. */
  trainingTeamId: number | null;
  progressTicks: number;
  requiredTicks: number;
}

/** Why a channel ended without paying out — the replay's own vocabulary. */
export type TrainingInterruption =
  | 'left-tile' | 'moved' | 'damaged' | 'acted-in-combat' | 'acquired-core'
  | 'died' | 'contested';

/** The player's words for each interruption the rules publish. */
export function trainingInterruptionPhrase(reason: TrainingInterruption): string {
  switch (reason) {
    case 'left-tile': return 'it stepped off the training tile';
    case 'moved': return 'it moved';
    case 'damaged': return 'it took damage';
    case 'acted-in-combat': return 'it fought';
    case 'acquired-core': return 'it picked up a Core';
    case 'died': return 'it was destroyed';
    case 'contested': return 'enemies walked into the room';
  }
}

/** Workshop ids are global map identities, so they read the same for both
 * seats — no mirroring, no "your"/"their". */
export function workshopReadLabel(workshopId: string): string {
  if (workshopId === 'north') return 'the north Workshop';
  if (workshopId === 'south') return 'the south Workshop';
  return `the ${workshopId.replace(/-/g, ' ')} Workshop`;
}

export interface TrainingRead extends TrainingIntent {
  /** One plain-language line for a chip, feed row or practice readback. */
  line: string;
  /** True when the trainee is on the tile and the channel is NOT counting. */
  stalled: boolean;
}

/**
 * The training readback: what this body is doing, and — where the page has
 * the replay's public Workshop state — why the channel is not counting, in
 * player terms. Everything it consumes is public to both teams.
 */
export function trainingRead(
  debugMessage: string,
  context: {
    workshops?: readonly WorkshopReadState[];
    /** The trainee's team, so "training" can read ours or theirs. */
    teamId?: number | null;
    /** The latest published interruption for this Workshop, if known. */
    interrupted?: TrainingInterruption | null;
  } = {},
): TrainingRead | null {
  const intent = parseTrainingIntent(debugMessage);
  if (!intent) return null;
  const where = workshopReadLabel(intent.workshopId);
  if (!intent.onTile) {
    return { ...intent, stalled: false, line: `Walking to ${where}.` };
  }
  const state = context.workshops?.find((value) =>
    value.workshopId === intent.workshopId);
  if (!state) {
    return { ...intent, stalled: false, line: `Holding the channel at ${where}.` };
  }
  const ours = context.teamId != null && state.trainingTeamId === context.teamId;
  if (state.phase === 'training') {
    return ours
      ? {
        ...intent,
        stalled: false,
        line: `Training at ${where} — ${state.progressTicks} of ${state.requiredTicks} ticks.`,
      }
      : {
        ...intent,
        stalled: true,
        line: `Waiting at ${where} — their channel is running, not ours.`,
      };
  }
  if (state.phase === 'contested') {
    return {
      ...intent,
      stalled: true,
      line: `Stalled at ${where} — enemies in the room have paused it.`,
    };
  }
  if (state.phase === 'dormant') {
    return {
      ...intent,
      stalled: true,
      line: `Waiting at ${where} — the room is recharging.`,
    };
  }
  // Ready with our body standing on the tile: either the channel is about to
  // start, or the last one ended for a reason the rules already published.
  return context.interrupted
    ? {
      ...intent,
      stalled: true,
      line: `Stalled at ${where} — progress reset because ${trainingInterruptionPhrase(context.interrupted)}.`,
    }
    : {
      ...intent,
      stalled: false,
      line: `Standing at ${where} — the channel starts as soon as it settles.`,
    };
}

const ORDER_ID = /^t(\d+)(?:-p(\d+))?-g(\d+)-([a-z0-9-]+)$/;

/** Resolve a materialized order id (`t1-p2-g1-fight`, `t1-g2-grab`) to the
 * authored names for a viewer chip: "Centre Rush · Claim center · fight".
 * The tactic name leads only when the sheet has several. Baseline order ids
 * return null — the raw id ("hold-ground") is already the honest label. */
export function orderIdLabel(
  orderId: string,
  document: TacticsDocument,
): string | null {
  const match = ORDER_ID.exec(orderId);
  if (!match) return null;
  const [, tacticOrdinal, phaseOrdinal, , kind] = match;
  const tactic = document.tactics[Number(tacticOrdinal) - 1];
  if (!tactic) return null;
  const parts: string[] = [];
  if (document.tactics.length > 1) parts.push(tactic.name);
  if (phaseOrdinal) {
    const phase = tactic.phases[Number(phaseOrdinal) - 1];
    parts.push(phase?.name ?? `phase ${phaseOrdinal}`);
  } else if (parts.length === 0) {
    parts.push(tactic.name);
  }
  parts.push(kind);
  return parts.join(' · ');
}
