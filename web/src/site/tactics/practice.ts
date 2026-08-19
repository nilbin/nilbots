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
