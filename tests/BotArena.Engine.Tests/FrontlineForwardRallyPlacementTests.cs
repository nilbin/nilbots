using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the forward rally (S2 in
/// <c>docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md</c>): reinforcements arrive
/// at the own-side objective beside the fight instead of at home, which is
/// what flattens the 4-vs-20-tick transit gradient. The derived tile respects
/// occupancy and every existing reservation, and falls back to the
/// permanently reserved assigned spawn.
/// </summary>
public sealed class FrontlineForwardRallyPlacementTests
{
    private static readonly Position NearWest = new(3, 2);
    private static readonly Position NearEast = new(5, 2);
    private static readonly Position EastSpawn = new(7, 3);

    [Fact]
    public void AutomaticReturnLandsOnTheOwnSideObjectiveAndValidates()
    {
        using GenericActorMatchSession session = Session(rally: true);

        GenericActorRuntimeObservation.EventPayload.LifeSpawned returned =
            RunUntilAutomaticReturn(session);

        Assert.Equal(NearEast, returned.Position);
        // Accessing the chronology runs every causality validator, including
        // the automatic-return placement evidence.
        Assert.NotEmpty(session.Chronology.Ticks);
    }

    [Fact]
    public void TheBaselinePlacementStillReturnsToTheAssignedSpawn()
    {
        using GenericActorMatchSession session = Session(rally: false);

        GenericActorRuntimeObservation.EventPayload.LifeSpawned returned =
            RunUntilAutomaticReturn(session);

        Assert.Equal(EastSpawn, returned.Position);
        Assert.NotEmpty(session.Chronology.Ticks);
    }

    [Fact]
    public void EachTeamRalliesToItsOwnSideOfTheActiveObjective()
    {
        ActorResolvedMatchDefinition definition = Definition(rally: true);

        Assert.Equal(
            NearWest,
            FrontlineForwardRallyPlacement.Resolve(
                definition,
                teamId: 0,
                assignedSpawn: new Position(1, 3),
                activePositionIndex: 2,
                blocked: EmptyBlocked));
        Assert.Equal(
            NearEast,
            FrontlineForwardRallyPlacement.Resolve(
                definition,
                teamId: 1,
                assignedSpawn: EastSpawn,
                activePositionIndex: 2,
                blocked: EmptyBlocked));
    }

    [Fact]
    public void AnOccupiedOrReservedRallyTileFallsThroughToTheAssignedSpawn()
    {
        ActorResolvedMatchDefinition definition = Definition(rally: true);

        // The fixture's objective regions are one tile wide, so a blocked
        // rally tile leaves the region with no legal tile at all.
        Assert.Equal(
            EastSpawn,
            FrontlineForwardRallyPlacement.Resolve(
                definition,
                teamId: 1,
                assignedSpawn: EastSpawn,
                activePositionIndex: 2,
                blocked: Blocked(NearEast)));
    }

    [Fact]
    public void TheOwnChainEdgeHasNoRallyPositionAndFallsBack()
    {
        ActorResolvedMatchDefinition definition = Definition(rally: true);

        // Team 0 advances toward higher indices, so index 0 is its own edge.
        Assert.Equal(
            new Position(1, 3),
            FrontlineForwardRallyPlacement.Resolve(
                definition,
                teamId: 0,
                assignedSpawn: new Position(1, 3),
                activePositionIndex: 0,
                blocked: EmptyBlocked));
    }

    [Fact]
    public void MultiTileRegionsTakeTheFirstLegalTileInCanonicalMapOrder()
    {
        ActorResolvedMatchDefinition labs = FrontlineLabsDefinition
            .CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ForwardRally);

        // frontline-position-1 is (6,5) (7,5) (6,6) (7,6) in canonical
        // row-then-column order; team 0's own side of the centre is that
        // region.
        Assert.Equal(
            new Position(6, 5),
            FrontlineForwardRallyPlacement.Resolve(
                labs,
                teamId: 0,
                assignedSpawn: new Position(2, 7),
                activePositionIndex: 2,
                blocked: EmptyBlocked));
        Assert.Equal(
            new Position(7, 5),
            FrontlineForwardRallyPlacement.Resolve(
                labs,
                teamId: 0,
                assignedSpawn: new Position(2, 7),
                activePositionIndex: 2,
                blocked: Blocked(new Position(6, 5))));
        Assert.Equal(
            new Position(15, 5),
            FrontlineForwardRallyPlacement.Resolve(
                labs,
                teamId: 1,
                assignedSpawn: new Position(20, 7),
                activePositionIndex: 2,
                blocked: EmptyBlocked));
    }

    [Fact]
    public void TheBaselineContractNeverDerivesARallyTile()
    {
        ActorResolvedMatchDefinition baseline =
            FrontlineLabsDefinition.Create();

        Assert.False(FrontlineForwardRallyPlacement.IsEnabled(baseline));
        Assert.Equal(
            new Position(2, 7),
            FrontlineForwardRallyPlacement.Resolve(
                baseline,
                teamId: 0,
                assignedSpawn: new Position(2, 7),
                activePositionIndex: 2,
                blocked: EmptyBlocked));
    }

    private static ImmutableHashSet<Position> EmptyBlocked =>
        [];

    private static ImmutableHashSet<Position> Blocked(Position position) =>
        [position];

    private static GenericActorRuntimeObservation.EventPayload.LifeSpawned
        RunUntilAutomaticReturn(GenericActorMatchSession session)
    {
        for (int tick = 0; tick < 12 && !session.IsCompleted; tick++)
        {
            GenericActorMatchPreparedTick prepared = session.PrepareTick();
            GenericActorRuntimeObservation.EventPayload.LifeSpawned? spawned =
                prepared.TickStartEvents
                    .Select(item => item.Payload)
                    .OfType<GenericActorRuntimeObservation.EventPayload
                        .LifeSpawned>()
                    .FirstOrDefault(payload =>
                        payload.Reason
                        == GenericActorRuntimeStart.SpawnReason
                            .AutomaticReturn);
            if (spawned is not null)
                return spawned;
            session.Step(prepared.Observations);
        }

        throw new InvalidOperationException(
            "No automatic return occurred within the fixture's tick budget.");
    }

    private static GenericActorMatchSession Session(bool rally) =>
        new(
            Definition(rally),
            GenericDeathmatchSessionTestFixture.Configurations(
                Definition(rally),
                GenericDeathmatchSessionTestFixture.Factories(
                    Definition(rally),
                    (start, observation) =>
                        start.ParticipantId == 10 && observation.Tick == 0
                            ? GenericDeathmatchSessionTestFixture.Shoot()
                            : GenericDeathmatchSessionTestFixture.Wait())),
            matchSeed: 4_242);

    /// <summary>
    /// The SDK Frontline arena with one-hit lives and an immediate automatic
    /// return, so exactly one death and one arrival happen inside a short
    /// tick budget. Team 0 shoots east down row three; the objective chain
    /// runs along row two, one tile above the firing lane.
    /// </summary>
    private static ActorResolvedMatchDefinition Definition(bool rally)
    {
        ActorResolvedMatchDefinition baseline =
            GenericActorContractTestFixture.Frontline();
        ActorFormDefinition sourceMobile = baseline.Rules.Forms.Single();
        var rules = new ActorRulesDefinition(
            "frontline-forward-rally-fixture",
            new ActorRulesLimits(
                maxTicks: 12,
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification: 0)),
            baseline.Rules.SeedMechanics,
            new FrontlineGameModeDefinition(
                new FrontlineVictoryDefinition(
                    pushesToBreach: 3,
                    [
                        new ScoreRankingDefinition(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            ScoreRankingDefinition.SortDirection.HigherWins),
                    ]),
                [
                    new ScoreChannelDefinition(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                ],
                frontlinePositionCount: 5,
                new FrontlineCaptureDefinition(
                    threshold: 50,
                    gainPerSoleTeamTick: 1,
                    decayAmount: 0,
                    decayIntervalTicks: 0,
                    redeployPauseTicks: 0)),
            new ActorLifecycleDefinition(
                [
                    new ActorLifecycleProfileDefinition(
                        "prime-respawn",
                        ActorLifecycleProfileDefinition.DestructionPolicyKind
                            .AutomaticRespawn,
                        delayTicks: 0,
                        automaticReturnFormId: "mobile"),
                ],
                rally
                    ? ActorLifecycleDefinition
                        .ActorAutomaticReturnPlacementKind
                        .OwnSideChainAdjacentObjectiveTileThenAssignedSpawn
                    : ActorLifecycleDefinition
                        .ActorAutomaticReturnPlacementKind
                        .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims),
            [
                new ActorFormDefinition(
                    "mobile",
                    maxHealth: 1,
                    sourceMobile.MovementProfileId,
                    sourceMobile.VisionProfileId,
                    sourceMobile.AttackProfileId,
                    objectiveWeight: 1,
                    ["wait", "shoot"]),
            ],
            baseline.Rules.MovementProfiles,
            baseline.Rules.VisionProfiles,
            baseline.Rules.AttackProfiles,
            baseline.Rules.Actions,
            fabricationTransitions: [],
            sameLifeTransitions: [],
            replicationTransitions: [],
            baseline.Rules.TeamPerception,
            baseline.Rules.Collisions,
            baseline.Rules.TickResolution);

        return new ActorResolvedMatchDefinition(
            rules,
            baseline.Map,
            baseline.Format,
            baseline.Topology,
            baseline.InitialDeployment,
            baseline.LifecycleAssignments,
            baseline.ParticipantRegionAssignments,
            baseline.ModeMapBinding);
    }
}
