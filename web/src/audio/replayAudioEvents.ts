import type {
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
 * pending-strike table winds up, then both a presented projectile and a matured fizzle
 * emit the same neutral release cue. A concrete armor contact adds the separate impact
 * voice; an early cancellation remains silent. Non-projectile categories report one
 * occurrence per tick, while up to four distinct projectile releases retain individual
 * quiet voices so the audio agrees with the visible volley. A recorded traversal can
 * still end in an impact, deflection, or destruction; that outcome keeps its own later
 * cue. Any foreground event masks a concurrent wind-up. Nothing is inferred from score
 * changes or future world state, so seeking is deterministic.
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
  const projectileReleaseActors = new Set<string>();
  const scheduleProjectileRelease = (
    actorKey: string,
    pan: number | null,
  ) => {
    if (projectileReleaseActors.has(actorKey)) return;
    projectileReleaseActors.add(actorKey);
    scheduled.push({
      cue: 'projectile',
      tickOffset: 0.08,
      priority: 3,
      pan,
    });
  };
  const authoritativeEvents = [...tick.lifecycleEvents, ...tick.events];
  const beforeStrikes = pendingStrikes(tick.before);
  const afterStrikeKeys = new Set(
    pendingStrikes(tick.after).map(strikeKey),
  );
  const beforeStrikeKeys = new Set(beforeStrikes.map(strikeKey));

  // Tapline's ordinary instant-ray resolution is published as an authoritative
  // traversal even when no separate `attack` event is present. WebGL now presents
  // that same traversal with the equipped projectile look, so give it the quiet
  // projectile pass too. Discrete projectiles are deliberately excluded: their
  // later movement traversals are not fresh launches.
  const presentedAttackProfiles = projectilePresentationProfileIds(replay);
  for (const traversal of tick.projectileTraversals) {
    if (
      traversal.attackProfileId === undefined ||
      !presentedAttackProfiles.has(traversal.attackProfileId)
    ) {
      continue;
    }
    scheduleProjectileRelease(
      traversal.ownerActorKey,
      panAt(traversal.from.x),
    );
  }

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
    const projectileResolved = hasProjectileResolution(tick, strike);
    const signatureResolved =
      signatureReason === 'completed' || signatureReason === 'launched';
    if (projectileResolved) continue;
    if (signatureResolved) {
      scheduleProjectileRelease(
        strike.shooter.actorKey,
        panAt(strikePosition(strike, tick.before)?.x),
      );
      continue;
    }
    if (strike.resolveAtTick <= tick.tick) {
      // The declared release matured without a traversal or hit. Give a fizzle,
      // interruption, or miss the same neutral release as an ordinary projectile;
      // armor impact remains the audible evidence that contact actually happened.
      scheduleProjectileRelease(
        strike.shooter.actorKey,
        panAt(strikePosition(strike, tick.before)?.x),
      );
    }
    // A strike removed before resolveAtTick is a voluntary cancellation and stays silent.
  }

  for (const event of authoritativeEvents) {
    // Events carry their origin tile; the projectile cue starts where it was fired.
    const pan = panAt(event.from?.x ?? event.to?.x);
    if (isAttackEvent(event.type)) {
      scheduleProjectileRelease(
        event.sourceActor?.actorKey ?? event.eventId,
        pan,
      );
    } else if (event.type === 'projectile-deflected') {
      scheduled.push({ cue: 'deflect', tickOffset: 0.52, priority: 4, pan });
    } else if (event.type === 'damage') {
      scheduled.push({ cue: 'impact', tickOffset: 0.56, priority: 4, pan });
    } else if (
      isDestructionEvent(event.type) ||
      event.type === 'disqualified'
    ) {
      // Both renderers begin the body's field-collapse at 0.55. The destruction
      // delivery is cut to an immediate onset, so schedule it on that same boundary
      // rather than letting it read as an after-the-fact respawn gesture.
      scheduled.push({ cue: 'destroyed', tickOffset: 0.55, priority: 4, pan });
    }

    const fact = event.arcRelayFact;
    if (!fact) continue;
    const factPan =
      'position' in fact ? panAt(fact.position.x) : pan;
    switch (fact.kind) {
      case 'zone-healed':
      case 'signature-repair':
        // Ground stations and medic beams restore the same thing at the same body.
        // Share one restrained field-seal voice and pan it from the healed target.
        scheduled.push({
          cue: 'healing',
          tickOffset: 0.42,
          priority: 5,
          pan: factPan,
        });
        break;
      case 'core-born':
        scheduled.push({
          cue: 'arc-birth',
          tickOffset: 0.18,
          priority: 5,
          pan: factPan,
        });
        break;
      case 'core-picked-up': {
        // A steal is still the same physical event: the loose Core levitates into a
        // carrier's field. Team ownership changes visually, not through a second sound.
        scheduled.push({
          cue: 'arc-pickup',
          tickOffset: 0.28,
          priority: 5,
          pan: factPan,
        });
        break;
      }
      case 'core-handed-off':
        scheduled.push({
          cue: 'arc-handoff',
          tickOffset: 0.31,
          priority: 5,
          pan: factPan,
        });
        break;
      case 'core-relocated':
        break;
      case 'core-dropped':
        scheduled.push({
          cue: 'arc-drop',
          tickOffset: 0.34,
          priority: 6,
          pan: factPan,
        });
        break;
      case 'core-banked':
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

  return sparseTickMix(scheduled);
}

/**
 * A replay tick is an audible moment, not a mandate to play one copy per bot.
 * Preserve exact events visually while keeping the shared sound field calm enough for
 * sixteen-body fights and headphones.
 */
function sparseTickMix(
  scheduled: readonly ReplayAudioEvent[],
): ReplayAudioEvent[] {
  const windupHasForegroundEvent = scheduled.some(
    (event) =>
      event.cue === 'projectile' ||
      (event.cue !== 'attack-windup' && event.priority >= 4),
  );
  const audible = scheduled.filter(
    (event) =>
      !(event.cue === 'attack-windup' && windupHasForegroundEvent),
  );

  // Tapline can show up to four distinct ordinary releases on one tick. Keep
  // every one audible, but offset their starts by 16 ms at normal speed so
  // identical buffers do not sum into one phase-locked transient. The hard cap
  // remains a comfort boundary for future larger formats.
  const projectileReleases = audible
    .filter((event) => event.cue === 'projectile')
    .slice(0, MAX_PROJECTILE_RELEASES_PER_TICK)
    .map((event, index) => ({
      ...event,
      tickOffset: event.tickOffset + index * PROJECTILE_RELEASE_STAGGER_TICKS,
    }));

  const byCue = new Map<SoundEffectCueId, ReplayAudioEvent[]>();
  for (const event of audible) {
    if (event.cue === 'projectile') continue;
    const batch = byCue.get(event.cue);
    if (batch) batch.push(event);
    else byCue.set(event.cue, [event]);
  }

  const coalesced = [...byCue.values()].map((batch) => {
    const pans = batch
      .map((event) => event.pan)
      .filter((pan): pan is number => pan !== null);
    return {
      cue: batch[0]!.cue,
      tickOffset: Math.min(...batch.map((event) => event.tickOffset)),
      priority: Math.max(...batch.map((event) => event.priority)),
      pan:
        pans.length === 0
          ? null
          : pans.reduce((sum, pan) => sum + pan, 0) / pans.length,
    };
  });
  return [...projectileReleases, ...coalesced];
}

const MAX_PROJECTILE_RELEASES_PER_TICK = 4;
const PROJECTILE_RELEASE_STAGGER_TICKS = 0.04;

type PendingStrike = ReplayArcRelayModeState['pendingStrikes'][number];

const projectilePresentationProfileCache = new WeakMap<
  ReplayModel,
  ReadonlySet<string>
>();

function projectilePresentationProfileIds(
  replay: ReplayModel,
): ReadonlySet<string> {
  const cached = projectilePresentationProfileCache.get(replay);
  if (cached) return cached;
  const ids = new Set<string>();
  if (replay.contract.kind === 'v3-generic') {
    for (const profile of replay.contract.rawContract.rules.attackProfiles) {
      const projectile = profile.projectile as {
        mode?: unknown;
        strikeWindupTicks?: unknown;
        strikeSweep?: unknown;
      };
      if (
        projectile.mode === 'instant-ray' &&
        typeof projectile.strikeWindupTicks === 'number' &&
        projectile.strikeWindupTicks > 0 &&
        projectile.strikeSweep !== true
      ) {
        ids.add(profile.id);
      }
    }
  }
  projectilePresentationProfileCache.set(replay, ids);
  return ids;
}

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

function hasProjectileResolution(
  tick: ReplayTick,
  strike: PendingStrike,
): boolean {
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
