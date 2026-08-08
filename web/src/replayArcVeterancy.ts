import type {
  ReplayModel,
  ReplayPosition,
} from './replayModel';

/**
 * Veterancy and heal-zone derivations shared by both renderers.
 *
 * Level is not part of the per-tick mode state — it exists in the replay only
 * as `leveled-up` facts — so the viewer folds those facts once per replay
 * here rather than in each renderer. The fold is per exact life
 * (team/unit/life): a respawn is a new life with no facts, which is precisely
 * the death-reset rule, so no explicit reset handling is needed.
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
}

const lifeKey = (teamId: number, unitId: number, lifeId: number) =>
  `${teamId}:${unitId}:${lifeId}`;

const cache = new WeakMap<ReplayModel, ArcVeterancyIndex>();

/**
 * The per-replay index, built on first use. Both renderers draw every frame,
 * so the fold must not run per frame — and the flat renderer is a free
 * function without a construction step to hang it on, hence a cache rather
 * than a parameter.
 */
export function arcVeterancyFor(replay: ReplayModel): ArcVeterancyIndex {
  let index = cache.get(replay);
  if (!index) {
    index = buildArcVeterancy(replay);
    cache.set(replay, index);
  }
  return index;
}

/** Ticks a heal glow survives past its fact — the -05 lineage cadence. */
const HEAL_GLOW_TICKS = 3;

export function buildArcVeterancy(replay: ReplayModel): ArcVeterancyIndex {
  const levels = new Map<string, { tick: number; level: number }[]>();
  const heals = new Map<string, number[]>();
  for (const [tickIndex, tick] of replay.ticks.entries()) {
    for (const event of [...tick.lifecycleEvents, ...tick.events]) {
      const fact = event.arcRelayFact;
      if (!fact) continue;
      if (fact.kind === 'leveled-up') {
        const key = lifeKey(
          fact.actor.teamId,
          fact.actor.unitId,
          fact.actor.lifeId,
        );
        const steps = levels.get(key) ?? [];
        steps.push({ tick: tickIndex, level: fact.level });
        levels.set(key, steps);
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
    }
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
  };
}
