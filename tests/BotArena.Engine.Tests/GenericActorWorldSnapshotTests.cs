using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorWorldSnapshotTests
{
    [Fact]
    public void RetainsExactResolvedMatchContractFingerprint()
    {
        Fixture fixture = CreateFixture();

        GenericActorWorldSnapshot snapshot = Snapshot(fixture);

        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(fixture.Definition),
            snapshot.MatchContractFingerprint);
    }

    [Fact]
    public void FabricationPendingRequiresCatalogSourceAndOutputAssignment()
    {
        Fixture fixture = CreateFixture();
        ActorIdentity source = fixture.Lives
            .Single(value =>
                value.ActorId.TeamId == 0
                && value.ActorId.UnitId == 0)
            .ActorId;

        GenericActorWorldSnapshot.SlotSnapshot ValidTarget(
            string transitionId,
            ActorIdentity sourceActorId,
            string targetFormId,
            Position position) =>
            new(
                teamId: 0,
                unitId: 1,
                participantId: 10,
                nextLifeId: 0,
                new GenericActorRuntimeObservation.UnitSlotState
                    .FabricationPending(
                        dueTick: 1,
                        sourceActorId,
                        transitionId,
                        operationId: "fabrication-0",
                        targetFormId,
                        position),
                pendingParentActorId: null,
                splitReservation: null);

        _ = Snapshot(
            fixture,
            slots: ReplaceSlot(
                fixture,
                ValidTarget(
                    "fabricate-child",
                    source,
                    "child",
                    new Position(3, 3))));

        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(
                    fixture,
                    ValidTarget(
                        "unknown-fabrication",
                        source,
                        "child",
                        new Position(3, 3)))));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(
                    fixture,
                    ValidTarget(
                        "fabricate-child",
                        new ActorIdentity(1, 0, 0),
                        "child",
                        new Position(3, 3)))));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(
                    fixture,
                    ValidTarget(
                        "fabricate-child",
                        source,
                        "turret",
                        new Position(3, 3)))));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(
                    fixture,
                    ValidTarget(
                        "fabricate-child",
                        source,
                        "child",
                        new Position(4, 3)))));
    }

    [Fact]
    public void ActiveLifeMustBeTheLatestIssuedSlotLife()
    {
        Fixture fixture = CreateFixture();
        GenericActorWorldSnapshot.SlotSnapshot original = fixture.Slots
            .Single(value => value.TeamId == 0 && value.UnitId == 0);

        Assert.Throws<ArgumentException>(() =>
        {
            var skipped = new GenericActorWorldSnapshot.SlotSnapshot(
                original.TeamId,
                original.UnitId,
                original.ParticipantId,
                nextLifeId: 2,
                original.State,
                original.PendingParentActorId,
                original.SplitReservation);
            _ = Snapshot(
                fixture,
                slots: ReplaceSlot(fixture, skipped));
        });
    }

    [Fact]
    public void SplitSlotReservationUsesFullSemanticEquality()
    {
        Fixture fixture = CreateFixture();
        GenericActorWorldSnapshot.LifeSnapshot source = fixture.Lives
            .Single(value =>
                value.ActorId.TeamId == 0
                && value.ActorId.UnitId == 0);
        ImmutableArray<SplitReplicationReservedDescendant> descendants =
        [
            new(
                TeamId: 0,
                UnitId: 0,
                FormId: "child",
                Generation: 1,
                new Position(1, 2)),
            new(
                TeamId: 0,
                UnitId: 1,
                FormId: "child",
                Generation: 1,
                new Position(1, 4)),
        ];
        var canonical = new SplitReplicationReservation(
            source.ActorId,
            source.ParticipantId,
            source.Generation,
            source.FormId,
            source.Position,
            source.Facing,
            TransitionId: "split-mobile",
            OperationId: "split-0",
            QueuedTick: 0,
            DueTick: 1,
            descendants);
        var semanticCopy = new SplitReplicationReservation(
            canonical.SourceActorId,
            canonical.ParticipantId,
            canonical.SourceGeneration,
            canonical.SourceFormId,
            canonical.SourcePosition,
            canonical.SourceFacing,
            canonical.TransitionId,
            canonical.OperationId,
            canonical.QueuedTick,
            canonical.DueTick,
            canonical.Descendants
                .Select(value => value with { })
                .ToImmutableArray());

        GenericActorWorldSnapshot.SlotSnapshot Target(
            SplitReplicationReservation slotReservation) =>
            CreateTarget(slotReservation);

        GenericActorWorldSnapshot.SlotSnapshot CreateTarget(
            SplitReplicationReservation slotReservation)
        {
            SplitReplicationReservedDescendant descendant =
                slotReservation.Descendants.Single(value =>
                    value.TeamId == 0 && value.UnitId == 1);
            return new GenericActorWorldSnapshot.SlotSnapshot(
                descendant.TeamId,
                descendant.UnitId,
                participantId: 10,
                nextLifeId: 0,
                new GenericActorRuntimeObservation.UnitSlotState
                    .ReplicationPending(
                        slotReservation.DueTick,
                        slotReservation.SourceActorId,
                        slotReservation.TransitionId,
                        slotReservation.OperationId,
                        descendant.FormId,
                        descendant.Position),
                pendingParentActorId: null,
                slotReservation);
        }

        _ = Snapshot(
            fixture,
            slots: ReplaceSlot(fixture, Target(semanticCopy)),
            replications: [canonical]);

        SplitReplicationReservation contradictory =
            semanticCopy with { SourceFacing = Direction.North };
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(
                    fixture,
                    Target(contradictory)),
                replications: [canonical]));

        SplitReplicationReservation invalidStaticEvidence =
            canonical with
            {
                Descendants = canonical.Descendants
                    .Select(value => value.UnitId == 1
                        ? value with
                        {
                            Position = new Position(3, 3),
                        }
                        : value)
                    .ToImmutableArray(),
            };
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(
                    fixture,
                    Target(invalidStaticEvidence)),
                replications: [invalidStaticEvidence]));

        GenericActorWorldSnapshot.LifeSnapshot other =
            fixture.Lives.Single(value =>
                value.ActorId.TeamId == 1);
        var overlappingLife = new GenericActorWorldSnapshot.LifeSnapshot(
            other.ActorId,
            other.ParticipantId,
            other.Generation,
            other.FormId,
            new Position(1, 4),
            other.Facing,
            other.Health,
            other.Cooldown,
            other.Energy,
            other.SpawnedAtTick,
            other.SpawnReason,
            other.ParentActorId,
            other.SourceTransitionId,
            other.SourceOperationId,
            other.PreviousActionResolution,
            other.PendingSameLifeTransition);
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(fixture, Target(canonical)),
                lives: fixture.Lives
                    .Select(value => value.ActorId == other.ActorId
                        ? overlappingLife
                        : value)
                    .ToArray(),
                replications: [canonical]));
    }

    [Fact]
    public void RetainedProjectileRequiresExactIssuedProgrammedEvidence()
    {
        Fixture fixture = CreateFixture();
        GenericActorWorldSnapshot.LifeSnapshot owner =
            fixture.Lives.Single(value =>
                value.ActorId.TeamId == 0
                && value.ActorId.UnitId == 0);
        ActorAttackProfileDefinition profile = fixture.Definition.Rules
            .AttackProfiles.Single(value => string.Equals(
                value.Id,
                "mobile-bolt",
                StringComparison.Ordinal));
        ActorShotProgramValue configuredDefault =
            profile.ShotProgram.DefaultProgram;
        var program = new ShotProgram(
            configuredDefault.InitialAimOffset,
            configuredDefault.BendDirection,
            configuredDefault.BendAfterTiles,
            configuredDefault.BendEveryTiles,
            configuredDefault.BendCount);
        ProjectileHeading launchHeading = owner.Facing
            .ToProjectileHeading()
            .Turned(program.InitialAimOffset);
        ImmutableArray<Position> path =
            GenericActorProjectilePath.Trace(
                fixture.Definition.Map,
                owner.Position,
                launchHeading,
                profile,
                program);
        Assert.True(path.Length > 2);

        GenericActorWorldSnapshot.ProjectileSnapshot Projectile(
            ActorIdentity? ownerActorId = null,
            int? spawnedAtTick = null,
            ShotProgram? shotProgram = null,
            bool includeShotProgram = true,
            IReadOnlyList<Position>? committedPath = null,
            int nextPathIndex = 1,
            ProjectileHeading? heading = null)
        {
            IReadOnlyList<Position> selectedPath =
                committedPath ?? path;
            Position traversedFrom = nextPathIndex == 1
                ? owner.Position
                : selectedPath[nextPathIndex - 2];
            return new GenericActorWorldSnapshot.ProjectileSnapshot(
                projectileId: 0,
                owner.ParticipantId,
                owner.ActorId.TeamId,
                ownerActorId ?? owner.ActorId,
                profile.Id,
                spawnedAtTick ?? 0,
                owner.Position,
                selectedPath[nextPathIndex - 1],
                launchHeading,
                heading
                    ?? ProjectileHeadingExtensions.Between(
                        traversedFrom,
                        selectedPath[nextPathIndex - 1]),
                includeShotProgram
                    ? shotProgram ?? program
                    : null,
                selectedPath,
                nextPathIndex,
                remainingTiles:
                    profile.Projectile.MaxTravelTiles
                    - nextPathIndex,
                profile.Projectile.TicksPerAdvance);
        }

        void Accept(
            GenericActorWorldSnapshot.ProjectileSnapshot projectile) =>
            _ = Snapshot(
                fixture,
                projectiles: [projectile],
                nextProjectileId: 1);

        void Reject(
            GenericActorWorldSnapshot.ProjectileSnapshot projectile) =>
            Assert.Throws<ArgumentException>(() =>
                Snapshot(
                    fixture,
                    projectiles: [projectile],
                    nextProjectileId: 1));

        Accept(Projectile());
        Reject(Projectile(
            ownerActorId: new ActorIdentity(
                owner.ActorId.TeamId,
                owner.ActorId.UnitId,
                lifeId: 1)));
        Reject(Projectile(spawnedAtTick: 1));
        Reject(Projectile(includeShotProgram: false));
        Reject(Projectile(
            shotProgram: new ShotProgram(
                InitialAimOffset: 0,
                BendDirection: 1,
                BendAfterTiles: 1,
                BendEveryTiles: 0,
                BendCount: 1)));
        Reject(Projectile(committedPath: path[..^1]));
        Reject(Projectile(heading: ProjectileHeading.North));

        var curve = new ShotProgram(
            InitialAimOffset: 0,
            BendDirection: 1,
            BendAfterTiles: 1,
            BendEveryTiles: 1,
            BendCount: 1);
        ImmutableArray<Position> curvedPath =
            GenericActorProjectilePath.Trace(
                fixture.Definition.Map,
                owner.Position,
                launchHeading,
                profile,
                curve);
        Accept(Projectile(
            shotProgram: curve,
            committedPath: curvedPath,
            nextPathIndex: 2));
        Reject(Projectile(
            shotProgram: curve,
            committedPath: curvedPath,
            nextPathIndex: 2,
            heading: launchHeading));
    }

    [Fact]
    public void PendingSameLifeTransitionRequiresCatalogRouteAndExactClock()
    {
        Fixture fixture = CreateFixture();
        GenericActorWorldSnapshot.LifeSnapshot parent = fixture.Lives
            .Single(value =>
                value.ActorId.TeamId == 0
                && value.ActorId.UnitId == 0);
        var actorId = new ActorIdentity(0, 1, 0);

        GenericActorWorldSnapshot.LifeSnapshot Child(
            GenericActorRuntimeObservation.PendingSameLifeTransition
                pending) =>
            new(
                actorId,
                participantId: 10,
                generation: 1,
                formId: "child",
                position: new Position(3, 3),
                facing: Direction.East,
                health: 2,
                cooldown: 0,
                energy: EnergyFor(fixture.Definition, "child"),
                spawnedAtTick: 0,
                GenericActorRuntimeStart.SpawnReason.Fabrication,
                parent.ActorId,
                sourceTransitionId: "fabricate-child",
                sourceOperationId: "fabrication-completed-0",
                previousActionResolution: null,
                pending);

        var activeSlot = new GenericActorWorldSnapshot.SlotSnapshot(
            teamId: 0,
            unitId: 1,
            participantId: 10,
            nextLifeId: 1,
            new GenericActorRuntimeObservation.UnitSlotState.Active(
                actorId,
                Generation: 1,
                FormId: "child"),
            pendingParentActorId: null,
            splitReservation: null);
        var validPending =
            new GenericActorRuntimeObservation.PendingSameLifeTransition(
                TransitionId: "anchor-child",
                OperationId: "anchor-0",
                TargetFormId: "turret",
                StartedTick: 0,
                DueTick: 1);

        _ = Snapshot(
            fixture,
            slots: ReplaceSlot(fixture, activeSlot),
            lives: [.. fixture.Lives, Child(validPending)]);

        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(fixture, activeSlot),
                lives:
                [
                    .. fixture.Lives,
                    Child(validPending with
                    {
                        TransitionId = "unknown-transition",
                    }),
                ]));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(fixture, activeSlot),
                lives:
                [
                    .. fixture.Lives,
                    Child(validPending with { DueTick = 2 }),
                ]));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                slots: ReplaceSlot(fixture, activeSlot),
                lives:
                [
                    .. fixture.Lives,
                    Child(validPending with { TargetFormId = "child" }),
                ]));
    }

    [Fact]
    public void DisqualifiedParticipantMustBeCleanedAndTeamEligibilityUpdated()
    {
        Fixture fixture = CreateFixture();
        GenericActorRuntimeObservation.ObservedParticipantStatus[] statuses =
            fixture.Participants
                .Select(value => value.ParticipantId == 10
                    ? value with { Disqualified = true }
                    : value)
                .ToArray();
        GenericActorWorldSnapshot.SlotSnapshot[] cleanedSlots =
            fixture.Slots
                .Select(value => value.ParticipantId == 10
                    ? new GenericActorWorldSnapshot.SlotSnapshot(
                        value.TeamId,
                        value.UnitId,
                        value.ParticipantId,
                        value.NextLifeId,
                        new GenericActorRuntimeObservation.UnitSlotState
                            .PermanentlyDormant(),
                        pendingParentActorId: null,
                        splitReservation: null)
                    : value)
                .ToArray();
        GenericActorWorldSnapshot.LifeSnapshot[] cleanedLives =
            fixture.Lives
                .Where(value => value.ParticipantId != 10)
                .ToArray();
        GenericActorRuntimeObservation.ScoreboardState eligibleScoreboard =
            WithEligibility(
                fixture.Scoreboard,
                teamId: 0,
                eligible: false);

        _ = Snapshot(
            fixture,
            participants: statuses,
            slots: cleanedSlots,
            lives: cleanedLives,
            scoreboard: eligibleScoreboard);

        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                participants: statuses,
                scoreboard: eligibleScoreboard));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(
                fixture,
                participants: statuses,
                slots: cleanedSlots,
                lives: cleanedLives));
    }

    private static GenericActorWorldSnapshot Snapshot(
        Fixture fixture,
        IReadOnlyCollection<
            GenericActorRuntimeObservation.ObservedParticipantStatus>?
            participants = null,
        IReadOnlyCollection<GenericActorWorldSnapshot.SlotSnapshot>?
            slots = null,
        IReadOnlyCollection<GenericActorWorldSnapshot.LifeSnapshot>?
            lives = null,
        IReadOnlyCollection<SplitReplicationReservation>?
            replications = null,
        IReadOnlyCollection<GenericActorWorldSnapshot.ProjectileSnapshot>?
            projectiles = null,
        GenericActorRuntimeObservation.ScoreboardState? scoreboard = null,
        int nextTick = 1,
        long nextProjectileId = 0) =>
        new(
            fixture.Definition,
            nextTick,
            nextProjectileId,
            participants ?? fixture.Participants,
            slots ?? fixture.Slots,
            lives ?? fixture.Lives,
            replications ?? [],
            projectiles ?? [],
            scoreboard ?? fixture.Scoreboard,
            fixture.Mode);

    private static GenericActorWorldSnapshot.SlotSnapshot[] ReplaceSlot(
        Fixture fixture,
        GenericActorWorldSnapshot.SlotSnapshot replacement) =>
        fixture.Slots
            .Select(value =>
                value.TeamId == replacement.TeamId
                && value.UnitId == replacement.UnitId
                    ? replacement
                    : value)
            .ToArray();

    private static GenericActorRuntimeObservation.ScoreboardState
        WithEligibility(
            GenericActorRuntimeObservation.ScoreboardState scoreboard,
            int teamId,
            bool eligible) =>
        new(
            scoreboard.Teams
                .Select(value => value.TeamId == teamId
                    ? value with { Eligible = eligible }
                    : value)
                .ToImmutableArray());

    private static Fixture CreateFixture()
    {
        ActorResolvedMatchDefinition definition =
            GenericActorContractTestFixture.WithTransitions();
        Dictionary<(int TeamId, int UnitId), int> controllers =
            definition.Topology.UnitSlots.ToDictionary(
                value => (value.TeamId, value.UnitId),
                value => value.ControllerParticipantId);
        Dictionary<(int TeamId, int UnitId), InitialLifeDeployment>
            deployments = definition.InitialDeployment.Lives.ToDictionary(
                value => (value.TeamId, value.UnitId));
        Dictionary<string, InitialSpawnDefinition> spawns =
            definition.InitialDeployment.Spawns.ToDictionary(
                value => value.SpawnId,
                StringComparer.Ordinal);
        Dictionary<string, ActorFormDefinition> forms =
            definition.Rules.Forms.ToDictionary(
                value => value.Id,
                StringComparer.Ordinal);

        GenericActorWorldSnapshot.LifeSnapshot[] lives = deployments.Values
            .OrderBy(value => value.TeamId)
            .ThenBy(value => value.UnitId)
            .Select(deployment =>
            {
                InitialSpawnDefinition spawn = spawns[deployment.SpawnId];
                ActorFormDefinition form = forms[deployment.FormId];
                return new GenericActorWorldSnapshot.LifeSnapshot(
                    new ActorIdentity(
                        deployment.TeamId,
                        deployment.UnitId,
                        deployment.LifeId),
                    controllers[(deployment.TeamId, deployment.UnitId)],
                    generation: 0,
                    deployment.FormId,
                    spawn.Position,
                    spawn.Facing,
                    form.MaxHealth,
                    cooldown: 0,
                    EnergyFor(definition, deployment.FormId),
                    spawnedAtTick: 0,
                    GenericActorRuntimeStart.SpawnReason.Initial,
                    parentActorId: null,
                    sourceTransitionId: null,
                    sourceOperationId: null,
                    previousActionResolution: null,
                    pendingSameLifeTransition: null);
            })
            .ToArray();
        Dictionary<(int TeamId, int UnitId),
            GenericActorWorldSnapshot.LifeSnapshot> livesBySlot =
            lives.ToDictionary(
                value => (value.ActorId.TeamId, value.ActorId.UnitId));
        GenericActorWorldSnapshot.SlotSnapshot[] slots = definition.Topology
            .UnitSlots
            .OrderBy(value => value.TeamId)
            .ThenBy(value => value.UnitId)
            .Select(value =>
            {
                if (livesBySlot.TryGetValue(
                    (value.TeamId, value.UnitId),
                    out GenericActorWorldSnapshot.LifeSnapshot? life))
                {
                    return new GenericActorWorldSnapshot.SlotSnapshot(
                        value.TeamId,
                        value.UnitId,
                        value.ControllerParticipantId,
                        nextLifeId: life.ActorId.LifeId + 1,
                        new GenericActorRuntimeObservation.UnitSlotState.Active(
                            life.ActorId,
                            life.Generation,
                            life.FormId),
                        pendingParentActorId: null,
                        splitReservation: null);
                }

                return new GenericActorWorldSnapshot.SlotSnapshot(
                    value.TeamId,
                    value.UnitId,
                    value.ControllerParticipantId,
                    nextLifeId: 0,
                    new GenericActorRuntimeObservation.UnitSlotState.Ready(),
                    pendingParentActorId: null,
                    splitReservation: null);
            })
            .ToArray();
        GenericActorRuntimeObservation.ObservedParticipantStatus[]
            participants = definition.Topology.Participants
                .OrderBy(value => value.ParticipantId)
                .Select(value =>
                    new GenericActorRuntimeObservation
                        .ObservedParticipantStatus(
                            value.ParticipantId,
                            value.TeamId,
                            RuntimeFaultCount: 0,
                            Disqualified: false))
                .ToArray();
        ImmutableArray<string> scoreChannels = definition.Rules.GameMode
            .ScoreCatalog
            .Select(value => ActorContractCanonicalIds.Id(value.Channel))
            .ToImmutableArray();
        var scoreboard =
            new GenericActorRuntimeObservation.ScoreboardState(
                definition.Topology.Teams
                    .OrderBy(value => value.TeamId)
                    .Select(value =>
                        new GenericActorRuntimeObservation.TeamScoreState(
                            value.TeamId,
                            Eligible: true,
                            scoreChannels
                                .Select(channel =>
                                    new GenericActorRuntimeObservation
                                        .ScoreValue(channel, 0))
                                .ToImmutableArray()))
                    .ToImmutableArray());
        var mode =
            new GenericActorRuntimeObservation.ModeObservationState
                .Deathmatch(definition.Rules.GameMode.ModeId);
        return new Fixture(
            definition,
            participants,
            slots,
            lives,
            scoreboard,
            mode);
    }

    private static int? EnergyFor(
        ActorResolvedMatchDefinition definition,
        string formId)
    {
        ActorFormDefinition form = definition.Rules.Forms.Single(
            value => string.Equals(
                value.Id,
                formId,
                StringComparison.Ordinal));
        if (form.AttackProfileId is not string attackProfileId)
            return null;
        ActorAttackProfileDefinition attack =
            definition.Rules.AttackProfiles.Single(value =>
                string.Equals(
                    value.Id,
                    attackProfileId,
                    StringComparison.Ordinal));
        return attack.MaxEnergy > 0 ? attack.MaxEnergy : null;
    }

    private sealed record Fixture(
        ActorResolvedMatchDefinition Definition,
        ImmutableArray<
            GenericActorRuntimeObservation.ObservedParticipantStatus>
            Participants,
        ImmutableArray<GenericActorWorldSnapshot.SlotSnapshot> Slots,
        ImmutableArray<GenericActorWorldSnapshot.LifeSnapshot> Lives,
        GenericActorRuntimeObservation.ScoreboardState Scoreboard,
        GenericActorRuntimeObservation.ModeObservationState Mode)
    {
        public Fixture(
            ActorResolvedMatchDefinition definition,
            IEnumerable<
                GenericActorRuntimeObservation.ObservedParticipantStatus>
                participants,
            IEnumerable<GenericActorWorldSnapshot.SlotSnapshot> slots,
            IEnumerable<GenericActorWorldSnapshot.LifeSnapshot> lives,
            GenericActorRuntimeObservation.ScoreboardState scoreboard,
            GenericActorRuntimeObservation.ModeObservationState mode)
            : this(
                definition,
                participants.ToImmutableArray(),
                slots.ToImmutableArray(),
                lives.ToImmutableArray(),
                scoreboard,
                mode)
        {
        }
    }
}
