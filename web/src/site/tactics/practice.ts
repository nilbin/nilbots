import type { Tactic, TacticsDocument } from './tacticsDocument';

/**
 * Practice attribution: the materializer mints ordinal ids
 * (`vex > t2 > t2-go > t2-g1-a-move`), and every executor debugMessage
 * carries that path — so a replay command maps straight back to the Tactic,
 * Group and phase the player authored, with no sidecar bookkeeping.
 */

export interface TacticAttribution {
  roleId: string;
  tacticOrdinal: number | null;
  tactic: Tactic | null;
  groupOrdinal: number | null;
  groupName: string | null;
  phase: 'stage' | 'act' | 'invariant' | 'baseline';
  leafId: string;
}

const PATH_PATTERN = /orders:([a-z0-9-]+) > ([a-z0-9-]+)(?: > ([a-z0-9-]+))?(?: > ([a-z0-9-]+))?/;

export function parseTacticAttribution(
  debugMessage: string,
  document: TacticsDocument | null,
): TacticAttribution | null {
  const match = PATH_PATTERN.exec(debugMessage);
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
      phase: 'baseline',
      leafId,
    };
  }
  const ordinal = Number(ordinalMatch[1]);
  const tactic = document?.tactics[ordinal - 1] ?? null;
  const leafMatch = /^t\d+-g(\d+)-(s|a)-/.exec(leafId);
  const groupOrdinal = leafMatch ? Number(leafMatch[1]) : null;
  const phase: TacticAttribution['phase'] = leafMatch
    ? (leafMatch[2] === 's' ? 'stage' : 'act')
    : second === `${root}-go` ? 'act'
      : second === `${root}-s` ? 'stage'
        : 'invariant';
  return {
    roleId,
    tacticOrdinal: ordinal,
    tactic,
    groupOrdinal,
    groupName: groupOrdinal !== null
      ? tactic?.groups[groupOrdinal - 1]?.name ?? null
      : null,
    phase,
    leafId,
  };
}

/** A short human label: “Pincer · Flankers · Act” or “between tactics”. */
export function attributionLabel(attribution: TacticAttribution): string {
  if (attribution.phase === 'baseline') return 'between tactics';
  const tacticName = attribution.tactic?.name
    ?? (attribution.tacticOrdinal !== null ? `Tactic ${attribution.tacticOrdinal}` : 'a tactic');
  const parts = [tacticName];
  if (attribution.groupName) parts.push(attribution.groupName);
  if (attribution.phase === 'stage') parts.push('Stage');
  if (attribution.phase === 'act') parts.push('Act');
  return parts.join(' · ');
}
