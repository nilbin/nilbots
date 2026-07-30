using BotArena.Sdk;

namespace BotArena.Sdk.Tests;

internal static class GenericActorDynamicTestFixture
{
    public static readonly ActorIdentity SelfActor = new(0, 0, 4);
    public static readonly ActorIdentity AllyActor = new(0, 1, 3);
    public static readonly ActorIdentity EnemyActor = new(1, 0, 5);

    public static GenericActorDecision FullDecision() =>
        new(
            "test-action",
            99,
            [
                new GenericActorActionArgument.ProjectileHeadingArgument(
                    ProjectileHeading.NorthEast),
                new GenericActorActionArgument.FormTargetArgument("turret"),
                new GenericActorActionArgument.UnitTargetArgument(
                    new GenericActorActionArgument.UnitTarget(0, 2)),
                new GenericActorActionArgument.DirectionArgument(
                    Direction.West),
                new GenericActorActionArgument.ShotProgramArgument(
                    new ShotProgram(1, -1, 2, 3, 2)),
            ],
            "diagnostic");

    public static GenericActorActionResolution.ResolvedAction WaitAction() =>
        new("wait", 0, []);

    public static GenericActorActionResolution.ResolvedAction MoveAction() =>
        new(
            "move",
            1,
            [
                new GenericActorActionArgument.DirectionArgument(
                    Direction.East),
            ]);

    public static GenericActorActionResolution.ResolvedAction ShootAction() =>
        new(
            "shoot",
            4,
            [
                new GenericActorActionArgument.ShotProgramArgument(
                    new ShotProgram(1, 1, 2, 2, 1)),
                new GenericActorActionArgument.ProjectileHeadingArgument(
                    ProjectileHeading.SouthEast),
            ]);

    public static GenericActorActionResolution Resolution(
        GenericActorActionResolution.ActionOutcome outcome)
    {
        GenericActorActionResolution.ResolvedAction requested = MoveAction();
        GenericActorRuntimeFaultContext? fault =
            outcome == GenericActorActionResolution.ActionOutcome.Faulted
                ? Fault(long.MaxValue)
                : null;
        return new GenericActorActionResolution(
            submittedAction: requested,
            acceptedAction: outcome
                    == GenericActorActionResolution.ActionOutcome.Faulted
                ? WaitAction()
                : requested,
            validatedAction: outcome
                    == GenericActorActionResolution.ActionOutcome.Faulted
                ? WaitAction()
                : requested,
            outcome,
            fault);
    }

    public static GenericActorRuntimeFaultContext Fault(long count = 7) =>
        new(
            participantId: 10,
            SelfActor,
            GenericActorRuntimeFaultContext.FaultStage.TickExecution,
            GenericActorRuntimeFaultContext.Codes.TickExecutionFailed,
            count,
            disqualificationTriggered: count > 5);

    public static GenericActorActionLegality FullLegality() =>
        new(
            "test-action",
            99,
            allowedByForm: true,
            available: true,
            [
                new GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint(
                        [
                            ProjectileHeading.West,
                            ProjectileHeading.North,
                        ]),
                new GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint(["turret", "mobile"]),
                new GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint(
                        [
                            new GenericActorActionArgument.UnitTarget(0, 3),
                            new GenericActorActionArgument.UnitTarget(0, 1),
                        ]),
                new GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint(
                        [Direction.South, Direction.North]),
                new GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint(true),
            ]);

    public static GenericActorContext Context(
        bool nullCapabilities = false,
        bool emptyCapabilities = false,
        bool includeAllEvents = true,
        bool frontline = true,
        int schemaVersion = GenericActorContext.CurrentSchemaVersion)
    {
        GenericActorContext.ObservedEvent[] events = includeAllEvents
            ? Events().Reverse().ToArray()
            : [];
        GenericActorContext.ObservedSound[]? sounds = nullCapabilities
            ? null
            : emptyCapabilities
                ? []
                :
            [
                new(
                    "event-03",
                    sourceTick: 8,
                    sourceOrdinal: 3,
                    SelfActor,
                    GenericActorContext.EventKind.Attack,
                    bearing: 2,
                    distance: 1),
            ];
        GenericActorContext.ObservedProjectile[]? projectiles =
            nullCapabilities
                ? null
                : emptyCapabilities
                    ? []
                    :
                [
                    new(
                        projectileId: long.MaxValue,
                        ownerTeamId: 1,
                        ownerActorId: null,
                        new Position(4, 3),
                        ProjectileHeading.West,
                        tilesPerAdvance: 2,
                        ticksUntilAdvance: 1,
                        remainingTiles: 0,
                        [SelfActor, AllyActor],
                        ticksPerAdvance: 3,
                        damagePerHit: 2),
                ];

        return new GenericActorContext(
            schemaVersion,
            tick: 9,
            new string('a', 64),
            new GenericActorContext.ObservedSelfState(
                SelfActor,
                generation: 2,
                "mobile",
                new Position(2, 3),
                Direction.East,
                health: 4,
                cooldown: 1,
                energy: 2,
                Resolution(
                    GenericActorActionResolution.ActionOutcome.Blocked),
                new GenericActorContext.PendingSameLifeTransition(
                    "anchor",
                    "anchor:0:0:4:9",
                    "turret",
                    startedTick: 8,
                    dueTick: 10),
                classId: "striker"),
            UnitSlots().Reverse(),
            [
                new GenericActorContext.ObservedParticipantStatus(
                    40,
                    3,
                    0,
                    false,
                    "striker"),
                new GenericActorContext.ObservedParticipantStatus(
                    10,
                    0,
                    long.MaxValue,
                    false,
                    "striker"),
                new GenericActorContext.ObservedParticipantStatus(
                    30,
                    2,
                    2,
                    true,
                    "fabricator"),
                new GenericActorContext.ObservedParticipantStatus(
                    20,
                    1,
                    1,
                    false,
                    "bulwark"),
            ],
            [
                new GenericActorContext.ObservedAllyState(
                    AllyActor,
                    generation: 1,
                    "mobile",
                    new Position(3, 3),
                    Direction.North,
                    health: 3,
                    cooldown: 0,
                    energy: null,
                    previousActionResolution: null,
                    pendingSameLifeTransition: null,
                    classId: "striker"),
            ],
            [
                new GenericActorContext.ObservedEnemyState(
                    new ActorIdentity(2, 0, 1),
                    "turret",
                    new Position(6, 6),
                    Direction.South,
                    health: 8,
                    pendingSameLifeTransition: null,
                    [SelfActor],
                    classId: "fabricator"),
                new GenericActorContext.ObservedEnemyState(
                    EnemyActor,
                    "mobile",
                    new Position(5, 3),
                    Direction.West,
                    health: 2,
                    pendingSameLifeTransition: null,
                    [AllyActor, SelfActor],
                    classId: "bulwark"),
            ],
            [
                new GenericActorContext.ObservedTile(
                    new Position(3, 2),
                    isWall: true,
                    [AllyActor]),
                new GenericActorContext.ObservedTile(
                    new Position(2, 2),
                    isWall: false,
                    [SelfActor],
                    new GenericActorContext.SpawnReservation(
                        teamId: 0,
                        unitId: 4,
                        GenericActorContext.SpawnReservationKind.Fabrication,
                        dueTick: 12)),
            ],
            projectiles,
            events,
            sounds,
            Scoreboard(),
            frontline
                ? new GenericActorContext.ModeObservationState.Frontline(
                    "frontline",
                    activePositionIndex: 2,
                    claimingTeamId: 0,
                    captureProgress: 3,
                    decayTicksElapsed: 1,
                    controlResumesAtTick: 12,
                    holdOwnerTeamId: 1,
                    holdEndsAtTick: 47)
                : new GenericActorContext.ModeObservationState.Deathmatch(
                    "deathmatch"),
            [
                FullLegality(),
                new GenericActorActionLegality(
                    "wait",
                    0,
                    allowedByForm: true,
                    available: true,
                    []),
            ]);
    }

    public static GenericActorContext.ObservedEvent[] Events()
    {
        GenericActorActionResolution.ResolvedAction move = MoveAction();
        GenericActorActionResolution.ResolvedAction shoot = ShootAction();
        GenericActorContext.EventPayload.FormTransition formTransition =
            new(
                SelfActor,
                "anchor",
                "anchor:0:0:4:9",
                "mobile",
                "turret",
                startedTick: 8,
                dueTick: 10);

        GenericActorContext.EventPayload[] payloads =
        [
            new GenericActorContext.EventPayload.Rotation(
                SelfActor,
                move,
                new Position(2, 3),
                Direction.North,
                Direction.East),
            new GenericActorContext.EventPayload.Movement(
                SelfActor,
                move,
                new Position(2, 3),
                new Position(3, 3),
                Direction.East),
            new GenericActorContext.EventPayload.MovementBlocked(
                SelfActor,
                move,
                new Position(3, 3),
                new Position(4, 3),
                Direction.East),
            new GenericActorContext.EventPayload.Attack(
                SelfActor,
                shoot,
                projectileId: long.MaxValue,
                new Position(3, 3),
                ProjectileHeading.SouthEast),
            new GenericActorContext.EventPayload.Damage(
                sourceTeamId: 1,
                sourceActorId: null,
                SelfActor,
                projectileId: long.MaxValue,
                amount: 2,
                newHealth: 2,
                new Position(3, 3)),
            new GenericActorContext.EventPayload.Destruction(
                SelfActor,
                sourceTeamId: 1,
                sourceActorId: null,
                projectileId: long.MaxValue,
                generation: 2,
                "mobile",
                new Position(3, 3)),
            new GenericActorContext.EventPayload.LifeSpawned(
                new ActorIdentity(2, 0, 0),
                participantId: 30,
                parentActorId: null,
                generation: 0,
                "mobile",
                health: 4,
                new Position(7, 7),
                GenericActorMatchStart.SpawnReason.Initial,
                sourceTransitionId: null,
                sourceOperationId: null),
            new GenericActorContext.EventPayload.LifeRetired(
                SelfActor,
                generation: 2,
                "mobile",
                new Position(3, 3),
                "replication",
                "split",
                "split:0:0:4:9"),
            new GenericActorContext.EventPayload.RuntimeFault(Fault()),
            new GenericActorContext.EventPayload.Participant(30, 2),
            new GenericActorContext.EventPayload.Lifecycle(
                "fabricate",
                "fabricate:0:0:4:9",
                SelfActor,
                targetTeamId: 0,
                targetUnitId: 4,
                dueTick: 10,
                cancellationReason: null),
            new GenericActorContext.EventPayload.Lifecycle(
                "fabricate",
                "fabricate:0:0:4:8",
                SelfActor,
                targetTeamId: 0,
                targetUnitId: 4,
                dueTick: null,
                cancellationReason: "source-destroyed"),
            new GenericActorContext.EventPayload.Lifecycle(
                "fabricate",
                "fabricate:0:0:4:7",
                SelfActor,
                targetTeamId: 0,
                targetUnitId: 4,
                dueTick: 9,
                cancellationReason: null),
            formTransition,
            formTransition,
            formTransition,
            new GenericActorContext.EventPayload.ScoreChanged(
                teamId: 0,
                "kills",
                newValue: long.MaxValue),
            new GenericActorContext.EventPayload.ModeChanged(
                new GenericActorContext.ModeObservationState.Deathmatch(
                    "deathmatch")),
            new GenericActorContext.EventPayload.LifecycleClockCancelled(
                targetTeamId: 0,
                targetUnitId: 5,
                new GenericActorContext.UnitSlotState
                    .AutomaticReturnPending(
                        dueTick: 12,
                        "mobile",
                        generation: 2),
                "participant-disqualified"),
        ];

        GenericActorContext.EventKind[] kinds =
            Enum.GetValues<GenericActorContext.EventKind>();
        return payloads
            .Select((payload, index) =>
                new GenericActorContext.ObservedEvent(
                    $"event-{index:D2}",
                    sourceTick: 8,
                    sourceOrdinal: index,
                    kinds[index],
                    payload,
                    [SelfActor, AllyActor]))
            .ToArray();
    }

    private static IEnumerable<GenericActorContext.ObservedUnitSlot>
        UnitSlots()
    {
        yield return new(
            0,
            0,
            new GenericActorContext.UnitSlotState.Active(
                SelfActor,
                generation: 2,
                "mobile"));
        yield return new(
            0,
            1,
            new GenericActorContext.UnitSlotState.AvailabilityPending(
                GenericActorContext.AvailabilityReason.DestructionRecovery,
                dueTick: 12));
        yield return new(
            0,
            2,
            new GenericActorContext.UnitSlotState.AutomaticReturnPending(
                dueTick: 13,
                "mobile",
                generation: 3));
        yield return new(
            0,
            3,
            new GenericActorContext.UnitSlotState.Ready());
        yield return new(
            0,
            4,
            new GenericActorContext.UnitSlotState.FabricationPending(
                dueTick: 10,
                SelfActor,
                "fabricate",
                "fabricate:0:0:4:9",
                "turret",
                new Position(3, 4)));
        yield return new(
            0,
            5,
            new GenericActorContext.UnitSlotState.ReplicationPending(
                dueTick: 10,
                SelfActor,
                "split",
                "split:0:0:4:9",
                "mobile",
                new Position(3, 2)));
        yield return new(
            0,
            6,
            new GenericActorContext.UnitSlotState.PermanentlyDormant());
    }

    private static GenericActorContext.ScoreboardState Scoreboard() =>
        new(
            Enumerable.Range(0, 4)
                .Reverse()
                .Select(teamId =>
                    new GenericActorContext.TeamScoreState(
                        teamId,
                        eligible: teamId != 2,
                        [
                            new GenericActorContext.ScoreValue(
                                "kills",
                                teamId == 0 ? long.MaxValue : teamId),
                            new GenericActorContext.ScoreValue(
                                "deaths",
                                teamId == 3 ? long.MinValue : -teamId),
                        ])));
}
