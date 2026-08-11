import type {
  ReplayArcCoreId,
  ReplayArcRelayModeState,
  ReplayModel,
  ReplayTick,
  ReplayWorldSnapshot,
} from '../replayModel';
import {
  isArrivalEvent,
  isAttackEvent,
  isDestructionEvent,
} from '../replayModel';
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
 * Every cue is bound to an explicit public fact or an authoritative before/after
 * transition. In particular, declared strikes have a three-part grammar: entering the
 * pending-strike table winds up, an attack resolution emits the beam, and leaving the
 * table without one emits either a voluntary cancel or an interrupted fizzle. Nothing is
 * inferred from score changes or future world state, so seeking is deterministic.
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
  const authoritativeEvents = [...tick.lifecycleEvents, ...tick.events];
  const beforeStrikes = pendingStrikes(tick.before);
  const afterStrikeKeys = new Set(
    pendingStrikes(tick.after).map(strikeKey),
  );
  const beforeStrikeKeys = new Set(beforeStrikes.map(strikeKey));

  for (const strike of pendingStrikes(tick.after)) {
    if (beforeStrikeKeys.has(strikeKey(strike))) continue;
    scheduled.push({
      cue: 'attack-windup',
      tickOffset: 0.12,
      priority: 2,
      pan: panAt(strikePosition(strike, tick.after)?.x),
    });
  }

  for (const strike of beforeStrikes) {
    if (afterStrikeKeys.has(strikeKey(strike))) continue;
    const signatureReason = terminalSignatureReason(tick, strike);
    const beamResolved = hasBeamResolution(tick, strike);
    const signatureResolved =
      signatureReason === 'completed' || signatureReason === 'launched';
    if (beamResolved) continue;
    if (signatureResolved) {
      scheduled.push({
        cue: 'projectile',
        tickOffset: 0.46,
        priority: 3,
        pan: panAt(strikePosition(strike, tick.before)?.x),
      });
      continue;
    }
    const interrupted =
      signatureReason !== null && signatureReason !== 'abandoned-move';
    scheduled.push({
      cue:
        interrupted || tick.tick >= strike.resolveAtTick
          ? 'attack-fizzle'
          : 'attack-cancel',
      tickOffset: tick.tick >= strike.resolveAtTick ? 0.46 : 0.26,
      priority: 2,
      pan: panAt(strikePosition(strike, tick.before)?.x),
    });
  }

  const coreOwners = coreOwnersBefore(replay, tickIndex);
  for (const event of authoritativeEvents) {
    // Events carry their origin tile; a beam is placed where it was fired from.
    const pan = panAt(event.from?.x ?? event.to?.x);
    if (isAttackEvent(event.type)) {
      scheduled.push({ cue: 'projectile', tickOffset: 0.46, priority: 3, pan });
    } else if (event.type === 'projectile-deflected') {
      scheduled.push({ cue: 'deflect', tickOffset: 0.52, priority: 4, pan });
    } else if (event.type === 'damage') {
      scheduled.push({ cue: 'impact', tickOffset: 0.56, priority: 4, pan });
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
        } else {
          scheduled.push({
            cue: 'arc-pickup',
            tickOffset: 0.28,
            priority: 5,
            pan: factPan,
          });
        }
        coreOwners.set(key, fact.carrierActor.teamId);
        break;
      }
      case 'core-handed-off':
        coreOwners.set(coreKey(fact.coreId), fact.targetActor.teamId);
        scheduled.push({
          cue: 'arc-handoff',
          tickOffset: 0.31,
          priority: 5,
          pan: factPan,
        });
        break;
      case 'core-relocated':
        if (fact.carrierActor)
          coreOwners.set(coreKey(fact.coreId), fact.carrierActor.teamId);
        break;
      case 'core-dropped':
        coreOwners.set(coreKey(fact.coreId), fact.sourceActor.teamId);
        scheduled.push({
          cue: 'arc-drop',
          tickOffset: 0.34,
          priority: 6,
          pan: factPan,
        });
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

  const arrivals = new Set<string>();
  const levelUps = new Map<
    string,
    { level: number; pan: number | null }
  >();
  for (const event of authoritativeEvents) {
    if (isArrivalEvent(event.type)) {
      const actor = event.sourceActor ?? event.targetActor;
      const key = actor?.actorKey ?? event.eventId;
      if (!arrivals.has(key)) {
        arrivals.add(key);
        const state = actor
          ? tick.before.actors.find(
              (candidate) => candidate.actorKey === actor.actorKey,
            )
          : null;
        scheduled.push({
          cue: 'arrival',
          tickOffset: 0.12,
          priority: 3,
          pan: panAt(state?.position.x ?? event.to?.x ?? event.from?.x),
        });
      }
    }

    const fact = event.arcRelayFact;
    if (fact?.kind !== 'leveled-up') continue;
    const previous = levelUps.get(fact.actor.actorKey);
    if (!previous || fact.level > previous.level) {
      levelUps.set(fact.actor.actorKey, {
        level: fact.level,
        pan: panAt(fact.position.x),
      });
    }
  }
  for (const levelUp of levelUps.values()) {
    scheduled.push({
      cue: 'level-up',
      tickOffset: 0.74,
      priority: 6,
      pan: levelUp.pan,
    });
  }

  return scheduled;
}

type PendingStrike = ReplayArcRelayModeState['pendingStrikes'][number];

function pendingStrikes(
  snapshot: ReplayWorldSnapshot,
): readonly PendingStrike[] {
  const mode = snapshot.mode;
  return mode?.kind === 'arc-relay' && 'pendingStrikes' in mode
    ? (mode.pendingStrikes ?? [])
    : [];
}

function strikeKey(strike: PendingStrike): string {
  return `${strike.shooter.actorKey}:${strike.resolveAtTick}`;
}

function strikePosition(
  strike: PendingStrike,
  snapshot: ReplayWorldSnapshot,
) {
  return (
    strike.origin ??
    snapshot.actors.find(
      (actor) => actor.actorKey === strike.shooter.actorKey,
    )?.position ??
    null
  );
}

function hasBeamResolution(tick: ReplayTick, strike: PendingStrike): boolean {
  return (
    [...tick.lifecycleEvents, ...tick.events].some(
      (event) =>
        isAttackEvent(event.type) &&
        event.sourceActor?.actorKey === strike.shooter.actorKey,
    ) ||
    tick.projectileTraversals.some(
      (traversal) =>
        traversal.ownerActorKey === strike.shooter.actorKey,
    )
  );
}

function terminalSignatureReason(
  tick: ReplayTick,
  strike: PendingStrike,
): string | null {
  for (const event of [...tick.lifecycleEvents, ...tick.events]) {
    const fact = event.arcRelayFact;
    if (
      fact?.kind === 'signature-changed' &&
      fact.phase === null &&
      fact.ownerActor.actorKey === strike.shooter.actorKey
    ) {
      return fact.reason;
    }
  }
  return null;
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
    const tick = replay.ticks[index];
    if (!tick) continue;
    for (const event of [...tick.lifecycleEvents, ...tick.events]) {
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
