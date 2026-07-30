import type {
  ReplayActorIdentity,
  ReplayDirection,
  ReplayModel,
  ReplayPosition,
  ReplayProjectileHeading,
} from '../replayModel';
import { isDestructionEvent } from '../replayModel';

export type ProjectileTerminalKind =
  | 'damage-hit'
  | 'destruction'
  | 'guard-absorbed'
  | 'deflected'
  | 'spent'
  | 'purged';

export interface ProjectileTerminal {
  projectileId: string;
  ownerActor: ReplayActorIdentity;
  targetActor: ReplayActorIdentity | null;
  targetFacing: ReplayDirection | null;
  position: ReplayPosition;
  heading: ReplayProjectileHeading;
  tick: number;
  kind: ProjectileTerminalKind;
}

export interface ProjectileTerminalSample extends ProjectileTerminal {
  /** 0 when the terminal presentation begins, 1 when it is complete. */
  age: number;
}

const terminalIndex = new WeakMap<
  ReplayModel,
  ReadonlyMap<number, readonly ProjectileTerminal[]>
>();

/**
 * Exact projectile endings in one authoritative replay tick.
 *
 * Disappearance alone is not enough to call something a miss. Generation 3 says whether
 * a traversal hit, was harmlessly range/path exhausted, or was removed by lifecycle/fault
 * cleanup. Older replay generations lack that terminal union, so only there does an
 * otherwise unexplained disappearance retain the historical spent/out-of-range reading.
 */
export function projectileTerminalsInTick(
  replay: ReplayModel,
  tick: number,
): readonly ProjectileTerminal[] {
  let index = terminalIndex.get(replay);
  if (!index) {
    index = buildTerminalIndex(replay);
    terminalIndex.set(replay, index);
  }
  return index.get(tick) ?? [];
}

/**
 * Harmless dissipations at their replay-authored presentation window.
 *
 * Damage, destruction, guarded contact, deflection and administrative purges are
 * deliberately excluded. Generation 3 has an exact traversal terminal, so its effect
 * shares the late-tick contact window and remains visible even on the match's final tick.
 * Older generations retain their historical following-tick fallback because their
 * traversal never said exactly why or when an otherwise eventless bolt disappeared.
 */
export function projectileDissipationsAt(
  replay: ReplayModel,
  time: number,
): ProjectileTerminalSample[] {
  if (replay.ticks.length === 0) return [];
  const clamped = Math.max(0, Math.min(time, replay.ticks.length));
  const tick = Math.min(Math.floor(clamped), replay.ticks.length - 1);
  const fraction = Math.max(0, Math.min(clamped - tick, 1));
  const deathTick = replay.sourceVersion === 3 ? tick : tick - 1;
  const age =
    replay.sourceVersion === 3
      ? (fraction - 0.6) / 0.4
      : fraction;
  if (age < 0 || age > 1) return [];
  return projectileTerminalsInTick(replay, deathTick)
    .filter((terminal) => terminal.kind === 'spent')
    .map((terminal) => ({ ...terminal, age }));
}

function buildTerminalIndex(
  replay: ReplayModel,
): ReadonlyMap<number, readonly ProjectileTerminal[]> {
  const index = new Map<number, readonly ProjectileTerminal[]>();
  for (const [tickIndex, tick] of replay.ticks.entries()) {
    const surviving = new Set(
      (tick.after.projectiles ?? []).map(
        (projectile) => projectile.projectileId,
      ),
    );
    const candidates = new Map<
      string,
      {
        ownerActor: ReplayActorIdentity;
        position: ReplayPosition;
        heading: ReplayProjectileHeading;
        terminal: Readonly<Record<string, unknown>> | null;
      }
    >();

    for (const projectile of tick.before.projectiles ?? []) {
      candidates.set(projectile.projectileId, {
        ownerActor: projectile.ownerActor,
        position: projectile.position,
        heading: projectile.heading ?? projectile.launchDirection,
        terminal: null,
      });
    }
    for (const traversal of tick.projectileTraversals) {
      candidates.set(traversal.projectileId, {
        ownerActor: traversal.ownerActor,
        position:
          traversal.path[traversal.path.length - 1] ?? traversal.from,
        heading:
          traversal.finalHeading ??
          traversal.heading ??
          traversal.launchDirection,
        terminal: traversal.terminal ?? null,
      });
    }

    const results: ProjectileTerminal[] = [];
    for (const [projectileId, candidate] of candidates) {
      if (surviving.has(projectileId)) continue;
      const related = tick.events.filter(
        (event) => event.projectileId === projectileId,
      );
      const destruction = related.find((event) =>
        isDestructionEvent(event.type),
      );
      const damage = related.find((event) => event.type === 'damage');
      const deflection = related.find(
        (event) => event.type === 'projectile-deflected',
      );
      const absorption = related.find((event) =>
        isGuardAbsorptionEvent(event.type),
      );

      let kind: ProjectileTerminalKind;
      let position = candidate.position;
      const contactEvent =
        destruction ?? damage ?? deflection ?? absorption ?? null;
      if (destruction) {
        kind = 'destruction';
        position = eventPosition(destruction) ?? position;
      } else if (damage) {
        kind = 'damage-hit';
        position = eventPosition(damage) ?? position;
      } else if (deflection) {
        kind = 'deflected';
        position = eventPosition(deflection) ?? position;
      } else if (absorption) {
        kind = 'guard-absorbed';
        position = eventPosition(absorption) ?? position;
      } else {
        kind = terminalKind(
          replay.sourceVersion,
          candidate.terminal,
        );
      }

      results.push({
        projectileId,
        ownerActor: candidate.ownerActor,
        targetActor:
          contactEvent?.targetActor ??
          targetActorFromTerminal(tick, candidate.terminal),
        targetFacing: contactEvent?.toFacing ?? null,
        position: { ...position },
        heading: candidate.heading,
        tick: tickIndex,
        kind,
      });
    }
    if (results.length > 0)
      index.set(
        tickIndex,
        results.sort((left, right) =>
          compareProjectileId(left.projectileId, right.projectileId),
        ),
      );
  }
  return index;
}

function terminalKind(
  sourceVersion: ReplayModel['sourceVersion'],
  terminal: Readonly<Record<string, unknown>> | null,
): ProjectileTerminalKind {
  const kind = terminal?.kind;
  if (kind === 'actor-contact' || kind === 'movement-contact')
    return terminal?.appliedDamage === true
      ? 'damage-hit'
      : 'guard-absorbed';
  if (
    kind === 'range-exhausted' ||
    kind === 'wall-or-path-exhausted'
  )
    return 'spent';
  if (
    kind === 'lifecycle-placement-purge' ||
    kind === 'participant-disqualification' ||
    kind === 'retained'
  )
    return 'purged';
  // Replay v1/v2 did not retain a terminal discriminator. Their existing presentation
  // truth is therefore "disappeared without a contact event" => spent.
  return sourceVersion < 3 ? 'spent' : 'purged';
}

function isGuardAbsorptionEvent(type: string): boolean {
  return type === 'projectile-absorbed' || type === 'guard-absorbed';
}

function eventPosition(event: {
  to: ReplayPosition | null;
  from: ReplayPosition | null;
}): ReplayPosition | null {
  return event.to ?? event.from;
}

function compareProjectileId(left: string, right: string): number {
  if (/^(?:0|[1-9][0-9]*)$/u.test(left) && /^(?:0|[1-9][0-9]*)$/u.test(right)) {
    const leftNumber = BigInt(left);
    const rightNumber = BigInt(right);
    return leftNumber < rightNumber ? -1 : leftNumber > rightNumber ? 1 : 0;
  }
  return left < right ? -1 : left > right ? 1 : 0;
}

function targetActorFromTerminal(
  tick: ReplayModel['ticks'][number],
  terminal: Readonly<Record<string, unknown>> | null,
): ReplayActorIdentity | null {
  const value = terminal?.targetActorId;
  if (!value || typeof value !== 'object') return null;
  const candidate = value as Record<string, unknown>;
  if (
    typeof candidate.teamId !== 'number' ||
    typeof candidate.unitId !== 'number' ||
    typeof candidate.lifeId !== 'number'
  )
    return null;
  const actor = [...tick.before.actors, ...tick.after.actors].find(
    (entry) =>
      entry.identity.teamId === candidate.teamId &&
      entry.identity.unitId === candidate.unitId &&
      entry.identity.lifeId === candidate.lifeId,
  );
  return actor?.identity ?? null;
}
