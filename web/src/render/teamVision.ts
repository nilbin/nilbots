import type {
  ReplayActorLifeKey,
  ReplayModel,
  ReplayStableUnitKey,
  ReplayTick,
} from '../replayModel';

/** The information a selected unit's whole team had collectively at one tick. */
export interface ReplayTeamVision {
  teamId: number;
  visibleTiles: ReadonlySet<string>;
  visibleActorKeys: ReadonlySet<ReplayActorLifeKey>;
  /** Opaque enemy observations can identify a stable slot without revealing its life. */
  visibleActorSlots: ReadonlySet<string>;
  /** Null means this replay generation exposes projectile sight only through visible tiles. */
  visibleProjectileIds: ReadonlySet<string> | null;
}

const tileKey = (x: number, y: number): string => `${x},${y}`;
const actorSlot = (teamId: number, unitId: number): string =>
  `${teamId}:${unitId}`;

/**
 * Union every active teammate's recorded observation.
 *
 * This remains replay-honest: it reveals nothing outside the team's published turns and
 * never consults a later tick. Selection chooses a team perspective, not one unit's
 * private camera cone.
 */
export function teamVisionAt(
  replay: ReplayModel,
  tick: ReplayTick | undefined,
  selectedUnitKey: ReplayStableUnitKey | null,
  enabled = true,
): ReplayTeamVision | null {
  if (!enabled || selectedUnitKey === null || !tick) return null;
  const teamId = replay.units.find(
    (unit) => unit.unitKey === selectedUnitKey,
  )?.teamId;
  if (teamId === undefined) return null;

  // Compact Arc broadcasts publish the union once per team. Keep it once in memory too:
  // expanding the same 100–200 tiles into eight teammate observations multiplied tens of
  // thousands of objects across a match and could push mobile Safari over its tab limit.
  if (tick.publishedTeamVision !== undefined) {
    const published = tick.publishedTeamVision.find(
      (entry) => entry.teamId === teamId,
    );
    const visibleTiles = new Set(
      (published?.visibleTiles ?? []).map((tile) => tileKey(tile.x, tile.y)),
    );
    const visibleActorKeys = new Set<ReplayActorLifeKey>();
    const visibleActorSlots = new Set<string>();
    for (const actor of tick.before.actors) {
      if (
        actor.identity.teamId !== teamId &&
        !visibleTiles.has(tileKey(actor.position.x, actor.position.y))
      )
        continue;
      visibleActorKeys.add(actor.actorKey);
      visibleActorSlots.add(
        actorSlot(actor.identity.teamId, actor.identity.unitId),
      );
    }
    return {
      teamId,
      visibleTiles,
      visibleActorKeys,
      visibleActorSlots,
      // The compact column exposes projectile sight through its authoritative tiles.
      visibleProjectileIds: null,
    };
  }

  const turns = tick.actorTurns.filter((turn) => turn.actor.teamId === teamId);
  // Archived compact broadcasts did not carry observation tiles. An active body can
  // always see at least its own tile, so all-empty active turns mean "not published",
  // not "the team sees nothing". Disable fog instead of showing a false black board.
  if (
    turns.length > 0 &&
    turns.every((turn) => turn.observation.visibleTiles.length === 0)
  )
    return null;

  const visibleTiles = new Set<string>();
  const visibleActorKeys = new Set<ReplayActorLifeKey>();
  const visibleActorSlots = new Set<string>();
  const visibleProjectileIds = new Set<string>();
  let hasExactProjectileVisibility = true;

  for (const turn of turns) {
    // A team's own active actors are always known to that team, even if an older replay
    // omitted one from the observation's allies array.
    visibleActorKeys.add(turn.actorKey);
    visibleActorSlots.add(actorSlot(turn.actor.teamId, turn.actor.unitId));

    for (const { position } of turn.observation.visibleTiles)
      visibleTiles.add(tileKey(position.x, position.y));

    for (const observed of [
      ...turn.observation.allies,
      ...turn.observation.enemies,
    ]) {
      if (observed.actor.kind === 'exact') {
        visibleActorKeys.add(observed.actor.identity.actorKey);
        visibleActorSlots.add(
          actorSlot(
            observed.actor.identity.teamId,
            observed.actor.identity.unitId,
          ),
        );
      } else {
        visibleActorSlots.add(
          actorSlot(observed.actor.teamId, observed.actor.unitId),
        );
      }
    }

    if (turn.observation.visibleProjectiles === null) {
      hasExactProjectileVisibility = false;
      continue;
    }
    const byHandle = new Map(
      turn.aliases.projectiles.map((alias) => [
        alias.projectileHandle,
        alias.projectileId,
      ]),
    );
    for (const projectile of turn.observation.visibleProjectiles) {
      const id =
        projectile.projectileId ??
        (projectile.projectileHandle === null
          ? undefined
          : byHandle.get(projectile.projectileHandle));
      if (id !== undefined) visibleProjectileIds.add(id);
    }
  }

  return {
    teamId,
    visibleTiles,
    visibleActorKeys,
    visibleActorSlots,
    visibleProjectileIds: hasExactProjectileVisibility
      ? visibleProjectileIds
      : null,
  };
}

export function teamVisionSeesActor(
  vision: ReplayTeamVision | null,
  actor: {
    actorKey: ReplayActorLifeKey;
    teamId: number;
    unitId: number;
  },
): boolean {
  if (vision === null || actor.teamId === vision.teamId) return true;
  return (
    vision.visibleActorKeys.has(actor.actorKey) ||
    vision.visibleActorSlots.has(actorSlot(actor.teamId, actor.unitId))
  );
}

export function teamVisionSeesProjectile(
  vision: ReplayTeamVision | null,
  projectileId: string,
  x: number,
  y: number,
): boolean {
  if (vision === null) return true;
  return vision.visibleProjectileIds !== null
    ? vision.visibleProjectileIds.has(projectileId)
    : vision.visibleTiles.has(tileKey(Math.round(x), Math.round(y)));
}
