using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the forward rally (S2 in
/// <c>docs/DESIGN-FORENSICS-DYNAMICS-2026-07-29.md</c>): reinforcements arrive
/// at the own-side objective beside the fight instead of at home, which is
/// what flattens the 4-vs-20-tick transit gradient. The derived tile respects
/// occupancy and every existing reservation, and falls back to the
/// permanently reserved assigned spawn.
///
/// The fixture's two near regions are two tiles wide and mirror images of
/// each other, so every assertion here distinguishes the team-advance order
/// the arms select from the historical map-absolute order, which is retained
/// and still resolvable for archived replays.
/// </summary>
public sealed class FrontlineForwardRallyPlacementTests
{
    /// <summary>Team 0 advances east, so its rear tile is the west one.</summary>
    private static readonly Position NearWest = new(2, 2);

    /// <summary>Team 1 advances west, so its rear tile is the east one.</summary>
    private static readonly Position NearEast = new(6, 2);

    /// <summary>
    /// The forward tile of each near region: the next candidate once the rear
    /// one is taken, and — for team 1 — the tile the historical absolute
    /// order handed it, one step ahead of its mirror partner.
    /// </summary>
    private static readonly Position NearWestForward = new(3, 2);
    private static readonly Position NearEastForward = new(5, 2);
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
    public void AnOccupiedRallyTileFallsThroughForwardBeforeTheAssignedSpawn()
    {
        ActorResolvedMatchDefinition definition = Definition(rally: true);

        // Fall-through walks the same team-relative order: the tile ahead of
        // the blocked rear one, and only then the reserved assigned spawn.
        Assert.Equal(
            NearEastForward,
            FrontlineForwardRallyPlacement.Resolve(
                definition,
                teamId: 1,
                assignedSpawn: EastSpawn,
                activePositionIndex: 2,
                blocked: Blocked(NearEast)));
        Assert.Equal(
            NearWestForward,
            FrontlineForwardRallyPlacement.Resolve(
                definition,
                teamId: 0,
                assignedSpawn: new Position(1, 3),
                activePositionIndex: 2,
                blocked: Blocked(NearWest)));
    }

    [Fact]
    public void AnExhaustedRallyRegionFallsBackToTheAssignedSpawn()
    {
        ActorResolvedMatchDefinition definition = Definition(rally: true);

        Assert.Equal(
            EastSpawn,
            FrontlineForwardRallyPlacement.Resolve(
                definition,
                teamId: 1,
                assignedSpawn: EastSpawn,
                activePositionIndex: 2,
                blocked: Blocked(NearEast, NearEastForward)));
    }

    /// <summary>
    /// The historical map-absolute placement stays defined and resolvable so
    /// archived replays keep verifying. On this mirrored fixture it is also
    /// the measured bias in miniature: both teams scan row-then-column, so
    /// team 1 lands one tile further forward than team 0's mirror image.
    /// </summary>
    [Fact]
    public void TheHistoricalAbsoluteOrderStillResolvesAndStillLeansOneWay()
    {
        ActorResolvedMatchDefinition historical = Definition(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileThenAssignedSpawn);

        Assert.True(FrontlineForwardRallyPlacement.IsEnabled(historical));
        Assert.Equal(
            NearWest,
            FrontlineForwardRallyPlacement.Resolve(
                historical,
                teamId: 0,
                assignedSpawn: new Position(1, 3),
                activePositionIndex: 2,
                blocked: EmptyBlocked));
        Assert.Equal(
            NearEastForward,
            FrontlineForwardRallyPlacement.Resolve(
                historical,
                teamId: 1,
                assignedSpawn: EastSpawn,
                activePositionIndex: 2,
                blocked: EmptyBlocked));
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
    public void MultiTileRegionsWalkForwardFromTheTeamsOwnRearTile()
    {
        ActorResolvedMatchDefinition labs = FrontlineLabsDefinition
            .CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ForwardRally);

        // frontline-position-1 is (6,5) (7,5) (6,6) (7,6); team 0 advances
        // east, so its rear column is x=6 and it walks north-to-south there
        // before stepping forward to x=7.
        Assert.Equal(
            new Position(6, 5),
            FrontlineForwardRallyPlacement.Resolve(
                labs,
                teamId: 0,
                assignedSpawn: new Position(2, 7),
                activePositionIndex: 2,
                blocked: EmptyBlocked));
        Assert.Equal(
            new Position(6, 6),
            FrontlineForwardRallyPlacement.Resolve(
                labs,
                teamId: 0,
                assignedSpawn: new Position(2, 7),
                activePositionIndex: 2,
                blocked: Blocked(new Position(6, 5))));
        Assert.Equal(
            new Position(7, 5),
            FrontlineForwardRallyPlacement.Resolve(
                labs,
                teamId: 0,
                assignedSpawn: new Position(2, 7),
                activePositionIndex: 2,
                blocked: Blocked(
                    new Position(6, 5),
                    new Position(6, 6))));
        // frontline-position-3 is the reflection of position-1, and team 1
        // advances west, so it takes the reflected tiles in the reflected
        // order: rear column x=16 first.
        Assert.Equal(
            new Position(16, 5),
            FrontlineForwardRallyPlacement.Resolve(
                labs,
                teamId: 1,
                assignedSpawn: new Position(20, 7),
                activePositionIndex: 2,
                blocked: EmptyBlocked));
        Assert.Equal(
            new Position(16, 6),
            FrontlineForwardRallyPlacement.Resolve(
                labs,
                teamId: 1,
                assignedSpawn: new Position(20, 7),
                activePositionIndex: 2,
                blocked: Blocked(new Position(16, 5))));
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

    private static ImmutableHashSet<Position> Blocked(
        params Position[] positions) =>
        [.. positions];

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

    private static ActorResolvedMatchDefinition Definition(bool rally) =>
        Definition(
            rally
                ? ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                    .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn
                : ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                    .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims);

    /// <summary>
    /// The SDK Frontline arena with one-hit lives and an immediate automatic
    /// return, so exactly one death and one arrival happen inside a short
    /// tick budget. Team 0 shoots east down row three; the objective chain
    /// runs along row two, one tile above the firing lane, with mirrored
    /// two-tile near regions so the placement order is observable.
    /// </summary>
    private static ActorResolvedMatchDefinition Definition(
        ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind placement)
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
                placement),
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
            RallyMap(),
            baseline.Format,
            baseline.Topology,
            baseline.InitialDeployment,
            baseline.LifecycleAssignments,
            baseline.ParticipantRegionAssignments,
            baseline.ModeMapBinding);
    }

    /// <summary>
    /// The SDK Frontline arena's geometry with the two near objectives
    /// widened to two tiles. Everything is symmetric under the x-reflection
    /// that swaps the teams — walls, spawn anchors, and the objective chain
    /// read backwards — so a placement that scans the map absolutely and one
    /// that scans it relative to the placing team disagree exactly here.
    /// </summary>
    private static ActorMapDefinition RallyMap() =>
        new(
            "sdk-frontline-arena",
            version: 1,
            [
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#########",
            ],
            [
                Anchor("west", 1, 3, Direction.East),
                Anchor("east", 7, 3, Direction.West),
            ],
            [
                Objective("far-west", [(1, 2)]),
                Objective("near-west", [(2, 2), (3, 2)]),
                Objective("centre", [(4, 2)]),
                Objective("near-east", [(5, 2), (6, 2)]),
                Objective("far-east", [(7, 2)]),
            ],
            []);

    private static ActorMapSpawnAnchorDefinition Anchor(
        string id,
        int x,
        int y,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(id, new Position(x, y), facing),
            [ActorMovementLayer.Ground]);

    private static ActorMapRegionDefinition Objective(
        string id,
        IEnumerable<(int X, int Y)> tiles) =>
        new(
            id,
            ActorMapRegionDefinition.RegionKind.Objective,
            [.. tiles.Select(tile => new Position(tile.X, tile.Y))]);
}
