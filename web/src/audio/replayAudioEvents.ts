import type { ReplayArcCoreId, ReplayModel } from '../replayModel';
import { isAttackEvent, isDestructionEvent } from '../replayModel';
import type { SoundEffectCueId } from './soundEffects';

export interface ReplayAudioEvent {
  cue: SoundEffectCueId;
  /** Position within the visual tick; values above one intentionally trail it. */
  tickOffset: number;
  priority: number;
  /**
   * Where it happened across the arena, -1 (left edge) to 1 (right edge), or null when
   * the authoritative event has no usable presentation position.
   *
   * Panned rather than positioned in 3D: the arena is a flat plan view on a phone
   * speaker or a laptop, so left/right is the only axis a listener can actually resolve.
   */
  pan: number | null;
}

/**
 * Maps authoritative replay events onto presentation audio.
 *
 * Combat keeps its launch/impact/destruction vocabulary. Arc Relay adds four objective
 * voices, each bound only to an explicit public fact; no cue is inferred from scores or a
 * future world state. Generic arrivals remain visual-only because none of these seven
 * sounds describes a life materializing.
 */
export function replayAudioEventsAt(
  replay: ReplayModel,
  tickIndex: number,
): ReplayAudioEvent[] {
  const tick = replay.ticks[tickIndex];
  if (!tick) return [];

  const width = replay.map.width;
  /** Tile column to a pan position, with the map's centre column at dead centre. */
  const panAt = (x: number | undefined): number | null => {
    if (typeof x !== 'number' || width <= 1) return null;
    return Math.max(-1, Math.min(1, (x / (width - 1)) * 2 - 1));
  };

  const scheduled: ReplayAudioEvent[] = [];
  const coreOwners = coreOwnersBefore(replay, tickIndex);
  for (const event of tick.events) {
    // Events carry their origin tile; a shot is placed where it was fired from.
    const pan = panAt(event.from?.x ?? event.to?.x);
    if (isAttackEvent(event.type)) {
      scheduled.push({ cue: 'projectile', tickOffset: 0.46, priority: 1, pan });
    } else if (event.type === 'damage') {
      scheduled.push({ cue: 'impact', tickOffset: 0.56, priority: 2, pan });
    } else if (
      isDestructionEvent(event.type) ||
      event.type === 'disqualified'
    ) {
      scheduled.push({ cue: 'destroyed', tickOffset: 0.68, priority: 4, pan });
    }

    const fact = event.arcRelayFact;
    if (!fact) continue;
    const factPan =
      'position' in fact ? panAt(fact.position.x) : pan;
    switch (fact.kind) {
      case 'core-born':
        scheduled.push({
          cue: 'arc-birth',
          tickOffset: 0.18,
          priority: 5,
          pan: factPan,
        });
        break;
      case 'core-picked-up': {
        const key = coreKey(fact.coreId);
        const previousTeam = coreOwners.get(key);
        if (
          previousTeam !== undefined &&
          previousTeam !== fact.carrierActor.teamId
        ) {
          scheduled.push({
            cue: 'arc-steal',
            tickOffset: 0.34,
            priority: 7,
            pan: factPan,
          });
        }
        coreOwners.set(key, fact.carrierActor.teamId);
        break;
      }
      case 'core-handed-off':
        coreOwners.set(coreKey(fact.coreId), fact.targetActor.teamId);
        break;
      case 'core-relocated':
        if (fact.carrierActor)
          coreOwners.set(coreKey(fact.coreId), fact.carrierActor.teamId);
        break;
      case 'core-dropped':
        coreOwners.set(coreKey(fact.coreId), fact.sourceActor.teamId);
        break;
      case 'core-banked':
        coreOwners.set(coreKey(fact.coreId), fact.teamId);
        scheduled.push({
          cue: 'arc-bank',
          tickOffset: 0.42,
          priority: 8,
          pan: factPan,
        });
        break;
      case 'pulse': {
        const mode = tick.after.mode;
        const target =
          mode?.kind === 'arc-relay' && 'reactors' in mode
            ? mode.reactors.find((reactor) => reactor.teamId !== fact.teamId)
            : null;
        scheduled.push({
          cue: 'arc-pulse',
          tickOffset: 0.5,
          priority: 10,
          pan: panAt(target?.position.x),
        });
        break;
      }
    }
  }

  return scheduled;
}

function coreKey(coreId: ReplayArcCoreId): string {
  return `${coreId.sourceWellId}:${coreId.sourceOrdinal}`;
}

function coreOwnersBefore(
  replay: ReplayModel,
  tickIndex: number,
): Map<string, number> {
  const owners = new Map<string, number>();
  for (let index = 0; index < tickIndex; index += 1) {
    for (const event of replay.ticks[index]?.events ?? []) {
      const fact = event.arcRelayFact;
      if (!fact) continue;
      switch (fact.kind) {
        case 'core-picked-up':
          owners.set(coreKey(fact.coreId), fact.carrierActor.teamId);
          break;
        case 'core-handed-off':
          owners.set(coreKey(fact.coreId), fact.targetActor.teamId);
          break;
        case 'core-relocated':
          if (fact.carrierActor)
            owners.set(coreKey(fact.coreId), fact.carrierActor.teamId);
          break;
        case 'core-dropped':
          owners.set(coreKey(fact.coreId), fact.sourceActor.teamId);
          break;
        case 'core-banked':
          owners.set(coreKey(fact.coreId), fact.teamId);
          break;
      }
    }
  }
  return owners;
}
