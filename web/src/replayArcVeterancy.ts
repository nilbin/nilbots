import type {
  ReplayModel,
  ReplayPosition,
} from './replayModel';

/**
 * Veterancy and heal-zone derivations shared by both renderers.
 *
 * Level is not part of the per-tick mode state — it exists in the replay only
 * as `leveled-up` facts — so the viewer folds those facts once per replay
 * here rather than in each renderer. Levels are displayed per exact life,
 * while a `veterancy-retained` fact seeds the next life of the same team-local
 * roster slot. Replays from reset-on-death rules have no such fact and retain
 * their historical fresh-life behavior.
 *
 * Heal channels are likewise fact-driven: the engine emits `zone-healed`
 * once per recovered point (every `healZoneTicksPerHp` waited ticks), so
 * "this bot is channeling right now" is "a heal landed within the cadence
 * window around now" — honest to the replay without re-deriving rules.
 */
export interface ArcVeterancyIndex {
  /** Highest level this exact life has reached at `time`; 1 when none. */
  levelAt(time: number, teamId: number, unitId: number, lifeId: number): number;
  /**
   * The current fact-driven promotion beat, or null outside its 1.8-tick
   * presentation window. The returned age is deterministic under seeking.
   */
  levelUpAt(
    time: number,
    teamId: number,
    unitId: number,
    lifeId: number,
  ): { level: number; age: number } | null;
  /**
   * 1 exactly when a zone-heal landed on this life at `time`, fading to 0
   * over the heal cadence — so a channelling bot glows continuously and the
   * glow dies within a couple of ticks of it stepping off or being hit.
   */
  healGlowAt(time: number, teamId: number, unitId: number, lifeId: number): number;
  /** Static heal-zone tiles from the map contract; empty when none exist. */
  healTiles: ReplayPosition[];
  /** Whether this replay carries veterancy at all (any leveled-up fact). */
  hasLevels: boolean;
  /** Highest level any life reaches in this replay; 1 without veterancy. */
  maxLevel: number;
  /** Successful vitality investments owned by this exact life at `time`. */
  vitalityPointsAt(
    time: number,
    teamId: number,
    unitId: number,
    lifeId: number,
  ): number;
}

const lifeKey = (teamId: number, unitId: number, lifeId: number) =>
  `${teamId}:${unitId}:${lifeId}`;
const slotKey = (teamId: number, unitId: number) => `${teamId}:${unitId}`;

const cache = new WeakMap<
  ReplayModel,
  { tickCount: number; index: ArcVeterancyIndex }
>();

/**
 * The per-replay index, built on first use. Both renderers draw every frame,
 * so the fold must not run per frame — and the flat renderer is a free
 * function without a construction step to hang it on, hence a cache rather
 * than a parameter.
 */
export function arcVeterancyFor(replay: ReplayModel): ArcVeterancyIndex {
  const cached = cache.get(replay);
  if (cached?.tickCount === replay.ticks.length) return cached.index;
  const index = buildArcVeterancy(replay);
  cache.set(replay, { tickCount: replay.ticks.length, index });
  return index;
}

/** Ticks a heal glow survives past its fact — the -05 lineage cadence. */
const HEAL_GLOW_TICKS = 3;
export const LEVEL_UP_BEAT_TICKS = 1.8;

export function buildArcVeterancy(replay: ReplayModel): ArcVeterancyIndex {
  const levels = new Map<
    string,
    { tick: number; level: number; promoted: boolean }[]
  >();
  const heals = new Map<string, number[]>();
  const vitalityInvestments = new Map<string, number[]>();
  const retainedLevels = new Map<string, number>();
  const investmentOrder = new Map<string, string[]>();
  for (const [tickIndex, tick] of replay.ticks.entries()) {
    const foldEvent = (event: (typeof tick.events)[number]) => {
      if (event.type === 'life-spawned' && event.sourceActor) {
        const actor = event.sourceActor;
        const stable = slotKey(actor.teamId, actor.unitId);
        const retained = retainedLevels.get(stable);
        if (retained !== undefined) {
          const key = lifeKey(actor.teamId, actor.unitId, actor.lifeId);
          levels.set(key, [{ tick: tickIndex, level: retained, promoted: false }]);
          const vitality = (investmentOrder.get(stable) ?? [])
            .filter((track) => track === 'vitality').length;
          if (vitality > 0) {
            vitalityInvestments.set(key, Array.from(
              { length: vitality },
              () => tickIndex,
            ));
          }
        }
      }
      const fact = event.arcRelayFact;
      if (!fact) return;
      if (fact.kind === 'leveled-up') {
        const key = lifeKey(
          fact.actor.teamId,
          fact.actor.unitId,
          fact.actor.lifeId,
        );
        const steps = levels.get(key) ?? [];
        steps.push({ tick: tickIndex, level: fact.level, promoted: true });
        levels.set(key, steps);
      } else if (fact.kind === 'veterancy-retained') {
        const stable = slotKey(fact.actor.teamId, fact.actor.unitId);
        retainedLevels.set(stable, fact.retainedLevel);
        const order = investmentOrder.get(stable) ?? [];
        investmentOrder.set(stable, order.slice(0, fact.retainedSpentPoints));
      } else if (fact.kind === 'zone-healed') {
        const key = lifeKey(
          fact.actor.teamId,
          fact.actor.unitId,
          fact.actor.lifeId,
        );
        const ticks = heals.get(key) ?? [];
        ticks.push(tickIndex);
        heals.set(key, ticks);
      }
    };
    tick.lifecycleEvents.forEach(foldEvent);
    for (const turn of tick.actorTurns) {
      if (
        turn.actionResolution.validatedActionId !== 'invest' ||
        turn.actionResolution.result !== 'success' ||
        !turn.actionResolution.validatedPayload?.upgradeTrackId
      ) continue;
      const track = turn.actionResolution.validatedPayload.upgradeTrackId;
      if (track === 'vitality') {
        const key = lifeKey(
          turn.actor.teamId,
          turn.actor.unitId,
          turn.actor.lifeId,
        );
        const ticks = vitalityInvestments.get(key) ?? [];
        ticks.push(tickIndex);
        vitalityInvestments.set(key, ticks);
      }
      const stable = slotKey(turn.actor.teamId, turn.actor.unitId);
      const order = investmentOrder.get(stable) ?? [];
      order.push(track);
      investmentOrder.set(stable, order);
    }
    tick.events.forEach(foldEvent);
  }

  const healTiles = replay.map.regions
    .filter((region) => region.regionId.startsWith('heal-'))
    .flatMap((region) => region.tiles.map((tile) => ({ ...tile })));

  let maxLevel = 1;
  for (const steps of levels.values()) {
    for (const step of steps) maxLevel = Math.max(maxLevel, step.level);
  }

  return {
    hasLevels: levels.size > 0,
    healTiles,
    maxLevel,
    levelAt(time, teamId, unitId, lifeId) {
      const steps = levels.get(lifeKey(teamId, unitId, lifeId));
      if (!steps) return 1;
      let level = 1;
      for (const step of steps) {
        if (step.tick > time) break;
        level = Math.max(level, step.level);
      }
      return level;
    },
    levelUpAt(time, teamId, unitId, lifeId) {
      const steps = levels.get(lifeKey(teamId, unitId, lifeId));
      if (!steps) return null;
      let active: { level: number; age: number } | null = null;
      for (const step of steps) {
        if (step.tick > time) break;
        if (!step.promoted) continue;
        const age = time - step.tick;
        if (age + Number.EPSILON < LEVEL_UP_BEAT_TICKS)
          active = { level: step.level, age };
      }
      return active;
    },
    healGlowAt(time, teamId, unitId, lifeId) {
      const ticks = heals.get(lifeKey(teamId, unitId, lifeId));
      if (!ticks) return 0;
      let strength = 0;
      for (const tick of ticks) {
        if (tick > time) break;
        const age = time - tick;
        if (age <= HEAL_GLOW_TICKS) {
          strength = Math.max(strength, 1 - age / (HEAL_GLOW_TICKS + 1));
        }
      }
      return strength;
    },
    vitalityPointsAt(time, teamId, unitId, lifeId) {
      const ticks = vitalityInvestments.get(
        lifeKey(teamId, unitId, lifeId),
      );
      if (!ticks) return 0;
      let points = 0;
      for (const tick of ticks) {
        if (tick > time) break;
        points += 1;
      }
      return points;
    },
  };
}
