import type * as V3 from './replayWireV3';

type ActorTuple = [number, number, number];
type PositionTuple = [number, number];
type TransitionTuple = [string, string, string, number, number];
type ParticipantTuple = [number, number, string, boolean, string | null];
type SlotTuple = [number, number, number, number, unknown[]];
type LifeTuple = [
  number,
  number,
  number,
  number,
  number,
  string,
  number,
  number,
  V3.ReplayV3Direction,
  number,
  number,
  number | null,
  number,
  string,
  ActorTuple | null,
  string | null,
  string | null,
  TransitionTuple | null,
];
type ProjectileTuple = [
  string,
  number,
  number,
  ActorTuple,
  string,
  number,
  PositionTuple,
  PositionTuple,
  V3.ReplayV3ProjectileHeading,
  V3.ReplayV3ProjectileHeading,
  V3.ReplayV3ShotProgram | null,
  PositionTuple[],
  number,
  number,
  number,
];
type WorldTuple = [
  number,
  string,
  ParticipantTuple[],
  SlotTuple[],
  LifeTuple[],
  ProjectileTuple[],
  V3.ReplayV3Scoreboard,
  V3.ReplayV3ModeState,
];
type ActionTuple = [string, number, V3.ReplayV3ActionArgument[]];
type TeamVisionTuple = [number, PositionTuple[]];
/**
 * `[teamId, unitId, orderId, action]` — what a body was told to do, and what it
 * did about it this tick.
 *
 * A CHANGE, not a state: the projection publishes a row only when a body's
 * reason differs from the one already published for it, and the viewer carries
 * the table forward. A body holding one order for two hundred ticks costs one
 * row rather than two hundred, which is the only reason this fits inside a
 * transport that also drops every other word of debug text.
 */
type UnitOrderTuple = [number, number, string | null, string];
type TurnTuple = [
  ActorTuple,
  number,
  string | null,
  [string, number, boolean][],
  ActionTuple,
  ActionTuple,
  string,
];

/**
 * Spectator-only Arc Relay transport. It is deliberately not replay v4: the
 * canonical replay remains v3 and owns verification/training facts. This is a
 * compact, reproducible projection addressed by that canonical hash.
 */
export interface ArcRelayBroadcastV1 {
  broadcastVersion: 1;
  canonicalReplayHash: string;
  header: V3.ReplayV3Header;
  initial: WorldTuple;
  worlds: WorldTuple[];
  turns: TurnTuple[][];
  /** Additive: absent on archived broadcasts written before team-perspective fog. */
  vision?: TeamVisionTuple[][];
  /** Additive: absent on archived broadcasts written before published orders. */
  orders?: UnitOrderTuple[][];
  startEvents: V3.ReplayV3AuthoritativeEvent[][];
  events: V3.ReplayV3AuthoritativeEvent[][];
  traversals: V3.ReplayV3ProjectileTraversal[][];
  births: LifeTuple[][];
  result: V3.ReplayV3Result;
}

/**
 * Product playback transport. Unlike the audit-addressed v1 gallery slice,
 * v2 owns a deterministic hash of its compact payload and may be truncated by
 * the hosted presentation clock without exposing terminal facts.
 */
export interface ArcRelayBroadcastV2 {
  broadcastVersion: 2;
  replayHash: string | null;
  partial: boolean;
  header: V3.ReplayV3Header;
  initial: WorldTuple;
  worlds: WorldTuple[];
  turns: TurnTuple[][];
  /** Additive: absent on stored broadcasts written before team-perspective fog. */
  vision?: TeamVisionTuple[][];
  /** Additive: absent on stored broadcasts written before published orders. */
  orders?: UnitOrderTuple[][];
  startEvents: V3.ReplayV3AuthoritativeEvent[][];
  events: V3.ReplayV3AuthoritativeEvent[][];
  traversals: V3.ReplayV3ProjectileTraversal[][];
  births: LifeTuple[][];
  result: V3.ReplayV3Result | null;
}

export type ArcRelayBroadcast = ArcRelayBroadcastV1 | ArcRelayBroadcastV2;

export function isArcRelayBroadcastV1(
  input: unknown,
): input is ArcRelayBroadcast {
  return (
    typeof input === 'object' &&
    input !== null &&
    ((input as { broadcastVersion?: unknown }).broadcastVersion === 1 ||
      (input as { broadcastVersion?: unknown }).broadcastVersion === 2)
  );
}

/** Expand the transport into the existing replay-v3 normalization boundary. */
export function expandArcRelayBroadcastV1(
  broadcast: ArcRelayBroadcast,
): V3.ReplayV3Document {
  if (broadcast.header.replayVersion !== 3) {
    throw new Error('Arc Relay broadcast header must address replay v3.');
  }
  const count = broadcast.worlds.length;
  if (
    broadcast.turns.length !== count ||
    (broadcast.vision !== undefined && broadcast.vision.length !== count) ||
    (broadcast.orders !== undefined && broadcast.orders.length !== count) ||
    broadcast.startEvents.length !== count ||
    broadcast.events.length !== count ||
    broadcast.traversals.length !== count ||
    broadcast.births.length !== count
  ) {
    throw new Error('Arc Relay broadcast columns must have equal tick counts.');
  }
  const replayHash = broadcast.broadcastVersion === 1
    ? broadcast.canonicalReplayHash
    : broadcast.replayHash;
  const partial = broadcast.broadcastVersion === 2 && broadcast.partial;
  if ((!partial && (replayHash === null || !/^[0-9a-f]{64}$/.test(replayHash))) ||
      (partial && replayHash !== null)) {
    throw new Error('Arc Relay broadcast has an invalid replay hash state.');
  }
  if ((partial && broadcast.result !== null) || (!partial && broadcast.result === null)) {
    throw new Error('Arc Relay broadcast result and partial marker disagree.');
  }

  const fingerprint = broadcast.header.contract.matchContractFingerprint;
  const initial = world(broadcast.initial, fingerprint);
  let previous = initial;
  const ticks: V3.ReplayV3Tick[] = [];
  for (let tick = 0; tick < count; tick += 1) {
    const births = broadcast.births[tick].map(life);
    const before = withBirths(previous, births);
    const lifeStarts = births.map((entry) => lifeStart(entry, broadcast.header));
    const turns = broadcast.turns[tick].map((entry) =>
      actorTurn(entry, tick, before, broadcast.header),
    );
    const after = world(broadcast.worlds[tick], fingerprint);
    ticks.push({
      tick,
      tickStart: {
        tick,
        state: before,
        activeActorIds: before.activeLives.map((entry) => entry.actorId),
        lifeStarts,
        events: broadcast.startEvents[tick],
        traversals: [],
      },
      // normalizeReplayV3 consumes actorTurns first. Broadcasts are already a
      // spectator projection, so one minimal public turn per body is the
      // honest shape. The published team-visible tile union stays as one
      // replay-model sidecar per team rather than being multiplied by every
      // body during normalization.
      actorTurns: turns,
      events: broadcast.events[tick],
      traversals: broadcast.traversals[tick],
      postState: after,
    });
    previous = after;
  }

  return {
    header: broadcast.header,
    initialFrame: {
      state: initial,
      lifeStarts: initial.activeLives.map((entry) =>
        lifeStart(entry, broadcast.header),
      ),
      events: [],
    },
    ticks,
    result: broadcast.result,
    replayHash,
    partial,
  };
}

function actor(value: ActorTuple): V3.ReplayV3ActorId {
  return { teamId: value[0], unitId: value[1], lifeId: value[2] };
}

function position(value: PositionTuple): V3.ReplayV3Position {
  return { x: value[0], y: value[1] };
}

function pending(
  value: TransitionTuple | null,
): V3.ReplayV3PendingSameLifeTransition | null {
  return value === null
    ? null
    : {
        transitionId: value[0],
        operationId: value[1],
        targetFormId: value[2],
        startedTick: value[3],
        dueTick: value[4],
      };
}

function life(value: LifeTuple): V3.ReplayV3LifeState {
  return {
    actorId: actor([value[0], value[1], value[2]]),
    participantId: value[3],
    generation: value[4],
    formId: value[5],
    position: { x: value[6], y: value[7] },
    facing: value[8],
    health: value[9],
    cooldown: value[10],
    energy: value[11],
    spawnedAtTick: value[12],
    spawnReason: value[13],
    parentActorId: value[14] === null ? null : actor(value[14]),
    sourceTransitionId: value[15],
    sourceOperationId: value[16],
    previousActionResolution: null,
    pendingSameLifeTransition: pending(value[17]),
  };
}

function slotState(value: unknown[]): V3.ReplayV3UnitSlotState {
  const kind = value[0];
  if (kind === 'active') {
    return {
      kind,
      actorId: actor(value[1] as ActorTuple),
      generation: value[2] as number,
      formId: value[3] as string,
    };
  }
  if (kind === 'availability-pending') {
    return {
      kind,
      reason: value[1] as string,
      dueTick: value[2] as number,
    };
  }
  if (kind === 'automatic-return-pending') {
    return {
      kind,
      dueTick: value[1] as number,
      targetFormId: value[2] as string,
      generation: value[3] as number,
    };
  }
  if (kind === 'fabrication-pending' || kind === 'replication-pending') {
    return {
      kind,
      dueTick: value[1] as number,
      sourceActorId: actor(value[2] as ActorTuple),
      transitionId: value[3] as string,
      operationId: value[4] as string,
      targetFormId: value[5] as string,
      reservedPosition: position(value[6] as PositionTuple),
    };
  }
  if (kind === 'ready' || kind === 'permanently-dormant') return { kind };
  throw new Error(`Unknown compact slot state ${String(kind)}.`);
}

function world(
  value: WorldTuple,
  fingerprint: string,
): V3.ReplayV3WorldState {
  return {
    matchContractFingerprint: fingerprint,
    nextTick: value[0],
    nextProjectileId: value[1],
    participants: value[2].map((entry) => ({
      participantId: entry[0],
      teamId: entry[1],
      runtimeFaultCount: entry[2],
      disqualified: entry[3],
      classId: entry[4],
    })),
    slots: value[3].map((entry) => ({
      teamId: entry[0],
      unitId: entry[1],
      participantId: entry[2],
      nextLifeId: entry[3],
      state: slotState(entry[4]),
      pendingParentActorId: null,
      splitReservation: null,
    })),
    activeLives: value[4].map(life),
    pendingReplications: [],
    projectiles: value[5].map((entry) => ({
      projectileId: entry[0],
      ownerParticipantId: entry[1],
      ownerTeamId: entry[2],
      ownerActorId: actor(entry[3]),
      attackProfileId: entry[4],
      spawnedAtTick: entry[5],
      origin: position(entry[6]),
      position: position(entry[7]),
      launchHeading: entry[8],
      heading: entry[9],
      shotProgram: entry[10],
      committedPath: entry[11].map(position),
      nextPathIndex: entry[12],
      remainingTiles: entry[13],
      ticksUntilAdvance: entry[14],
    })),
    scoreboard: value[6],
    mode: value[7],
  };
}

function actorKey(value: V3.ReplayV3ActorId): string {
  return `${value.teamId}:${value.unitId}:${value.lifeId}`;
}

function withBirths(
  previous: V3.ReplayV3WorldState,
  births: V3.ReplayV3LifeState[],
): V3.ReplayV3WorldState {
  if (births.length === 0) return previous;
  const born = new Map(births.map((entry) => [actorKey(entry.actorId), entry]));
  const activeLives = [
    ...previous.activeLives.filter((entry) => !born.has(actorKey(entry.actorId))),
    ...births,
  ].sort((left, right) =>
    left.actorId.teamId - right.actorId.teamId ||
    left.actorId.unitId - right.actorId.unitId ||
    left.actorId.lifeId - right.actorId.lifeId,
  );
  const byUnit = new Map(
    births.map((entry) => [
      `${entry.actorId.teamId}:${entry.actorId.unitId}`,
      entry,
    ]),
  );
  return {
    ...previous,
    activeLives,
    slots: previous.slots.map((slot) => {
      const bornLife = byUnit.get(`${slot.teamId}:${slot.unitId}`);
      return bornLife
        ? {
            ...slot,
            nextLifeId: Math.max(slot.nextLifeId, bornLife.actorId.lifeId + 1),
            state: {
              kind: 'active' as const,
              actorId: bornLife.actorId,
              generation: bornLife.generation,
              formId: bornLife.formId,
            },
          }
        : slot;
    }),
  };
}

function lifeStart(
  value: V3.ReplayV3LifeState,
  header: V3.ReplayV3Header,
): V3.ReplayV3LifeStart {
  return {
    schemaVersion: header.runtime.matchStartSchemaVersion,
    runtimeContractVersion: header.runtime.runtimeContractVersion,
    actorId: value.actorId,
    participantId: value.participantId,
    actorRandomSeed: '0',
    origin: {
      reason: value.spawnReason,
      generation: value.generation,
      parentActorId: value.parentActorId,
      sourceTransitionId: value.sourceTransitionId,
      sourceOperationId: value.sourceOperationId,
    },
    matchContractFingerprint: header.contract.matchContractFingerprint,
  };
}

function resolvedAction(value: ActionTuple): V3.ReplayV3ResolvedAction {
  return { actionId: value[0], actionCode: value[1], arguments: value[2] };
}

function actorTurn(
  value: TurnTuple,
  tick: number,
  before: V3.ReplayV3WorldState,
  header: V3.ReplayV3Header,
): V3.ReplayV3ActorTurn {
  const id = actor(value[0]);
  const own = before.activeLives.find(
    (entry) => actorKey(entry.actorId) === actorKey(id),
  );
  if (!own) throw new Error(`Broadcast turn names absent actor ${actorKey(id)}.`);
  const slot = header.contract.topology.unitSlots.find(
    (entry) => entry.teamId === id.teamId && entry.unitId === id.unitId,
  );
  const accepted = resolvedAction(value[4]);
  const validated = resolvedAction(value[5]);
  return {
    tick,
    participantId: value[1],
    actorId: id,
    observation: {
      schemaVersion: header.runtime.observationSchemaVersion,
      tick,
      matchContractFingerprint: header.contract.matchContractFingerprint,
      self: {
        actorId: id,
        generation: own.generation,
        formId: own.formId,
        position: own.position,
        facing: own.facing,
        health: own.health,
        cooldown: own.cooldown,
        energy: own.energy,
        previousActionResolution: null,
        pendingSameLifeTransition: own.pendingSameLifeTransition,
        classId: slot?.classId ?? null,
        ...(value[2] === null ? {} : { roleTag: value[2] }),
      },
      teamUnits: before.slots
        .filter((entry) => entry.teamId === id.teamId)
        .map((entry) => ({
          teamId: entry.teamId,
          unitId: entry.unitId,
          state: entry.state,
        })),
      participants: before.participants,
      allies: [],
      enemies: [],
      visibleTiles: [],
      visibleProjectiles: null,
      visibleEvents: [],
      heardSounds: null,
      scoreboard: before.scoreboard,
      mode: before.mode,
      actionLegalities: value[3].map((entry) => ({
        actionId: entry[0],
        actionCode: entry[1],
        allowedByForm: true,
        available: entry[2],
        constraints: [],
      })),
    },
    submittedDecision: null,
    actionResolution: {
      submittedAction: accepted,
      acceptedAction: accepted,
      validatedAction: validated,
      outcome: value[6],
      runtimeFault: null,
    },
  };
}
