using BotArena.Engine;
using BotArena.Sdk;
// The Engine and the SDK deliberately keep independent geometry types; the
// scaffold only ever sees the SDK's, so bind the names that way here too.
using ActorIdentity = BotArena.Sdk.ActorIdentity;
using Direction = BotArena.Sdk.Direction;
using Position = BotArena.Sdk.Position;
using ProjectileHeading = BotArena.Sdk.ProjectileHeading;

namespace BotArena.Cli.Tests;

/// <summary>
/// The scaffold's contract reading, checked against real resolved contracts
/// rather than hand-written JSON. Every case here is a value that moves
/// between arms without moving the observation schema, which is exactly the
/// class of fact a bot can only get right by reading it.
/// </summary>
public sealed class ArenaBasicsTemplateTests
{
    [Fact]
    public void BaselineContract_DeclaresNoHoldAndBinaryControl()
    {
        GenericActorResolvedMatchContract contract =
            Contract(FrontlineLabsPendulumArm.None);

        ArenaBasics.CaptureRules capture = Capture(contract);

        Assert.Null(capture.HoldTicks);
        Assert.False(capture.SurplusWeightScalesGain);
        Assert.False(capture.OnlyEnemySolePresenceDecays);
        Assert.False(ArenaBasics.ArrivalsRallyForward(contract));
        Assert.True(capture.Threshold > 0);
        Assert.True(capture.GainPerSoleTeamTick > 0);
    }

    [Fact]
    public void StickyContract_PublishesAPositiveHold()
    {
        ArenaBasics.CaptureRules capture = Capture(
            Contract(FrontlineLabsPendulumArm.StickyFrontline));

        Assert.NotNull(capture.HoldTicks);
        Assert.True(capture.HoldTicks > 0);
    }

    [Fact]
    public void ContestMajorityContract_ScalesGainWithSurplusWeight()
    {
        ArenaBasics.CaptureRules capture = Capture(
            Contract(FrontlineLabsPendulumArm.ContestMajority));

        Assert.True(capture.SurplusWeightScalesGain);
        Assert.Null(capture.HoldTicks);
    }

    [Fact]
    public void EnemySoleDecayContract_ErodesOnlyUnderEnemySolePresence()
    {
        ArenaBasics.CaptureRules capture = Capture(
            Contract(FrontlineLabsPendulumArm.EnemySoleDecay));

        Assert.True(capture.OnlyEnemySolePresenceDecays);
        Assert.False(
            Capture(Contract(FrontlineLabsPendulumArm.None))
                .OnlyEnemySolePresenceDecays);
    }

    [Fact]
    public void ComposedContract_ReportsEveryArmItCarries()
    {
        ArenaBasics.CaptureRules capture = Capture(
            Contract(
                FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ForwardRally
                | FrontlineLabsPendulumArm.ContestMajority));

        Assert.NotNull(capture.HoldTicks);
        Assert.True(capture.SurplusWeightScalesGain);
        Assert.True(
            ArenaBasics.ArrivalsRallyForward(
                Contract(
                    FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally
                    | FrontlineLabsPendulumArm.ContestMajority)));
    }

    [Fact]
    public void NumbersOnlyContract_ReportsItsOwnCaptureThreshold()
    {
        GenericActorResolvedMatchContract contract = Parse(
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                captureThreshold: 9,
                primeRespawnTicks: 9));

        Assert.Equal(9, Capture(contract).Threshold);
    }

    [Fact]
    public void ArrivalTiles_FollowTheChainOnlyWhenTheContractSaysSo()
    {
        const int Team = 0;
        const int Unit = 0;
        const int ActiveIndex = 2;

        GenericActorResolvedMatchContract home =
            Contract(FrontlineLabsPendulumArm.None);
        GenericActorResolvedMatchContract rally =
            Contract(FrontlineLabsPendulumArm.ForwardRally);

        Position[] homeTiles = ArenaBasics.ExpectedArrivalTiles(
            home,
            Team,
            Unit,
            ActiveIndex);
        Position[] rallyTiles = ArenaBasics.ExpectedArrivalTiles(
            rally,
            Team,
            Unit,
            ActiveIndex);
        Position[] ownSide = ArenaBasics.OwnSideObjectiveTiles(
            rally,
            Team,
            ActiveIndex);

        // The home arrival is the slot's declared spawn anchor; the rallying
        // arrival is the objective one step behind the front. They must not be
        // the same tiles, or the test proves nothing.
        Assert.NotEmpty(homeTiles);
        Assert.NotEmpty(rallyTiles);
        Assert.Equal(ownSide, rallyTiles);
        Assert.Empty(homeTiles.Intersect(rallyTiles));
    }

    [Fact]
    public void ObjectiveGeometry_IsChainDerivedForBothTeams()
    {
        GenericActorResolvedMatchContract contract =
            Contract(FrontlineLabsPendulumArm.None);

        Direction? zero = ArenaBasics.AdvanceDirection(contract, 0);
        Direction? one = ArenaBasics.AdvanceDirection(contract, 1);

        Assert.NotNull(zero);
        Assert.NotNull(one);
        Assert.NotEqual(zero, one);
        Assert.NotEmpty(ArenaBasics.ObjectiveTiles(contract, 0));
        Assert.Empty(ArenaBasics.ObjectiveTiles(contract, -1));
        Assert.Empty(ArenaBasics.ObjectiveTiles(contract, 9999));
        // A team standing on its own end of the chain has nothing behind it,
        // and the helper says so instead of throwing.
        Assert.Empty(
            ArenaBasics.OwnSideObjectiveTiles(contract, 0, 0));
    }

    [Fact]
    public void ObjectivePresence_CountsFormWeightPerSide()
    {
        GenericActorResolvedMatchContract contract =
            Contract(FrontlineLabsPendulumArm.ContestMajority);
        // Objective 2 is the centre of the chain on this map.
        Position[] objective = ArenaBasics.ObjectiveTiles(contract, 2);
        Assert.True(objective.Length >= 4);

        GenericActorContext context = Observation(
            contract,
            self: objective[0],
            activePositionIndex: 2,
            allies:
            [
                (objective[1], "prime-mobile"),
                // Weight zero: a fortified body holds the tile and counts for
                // nothing, which is why presence is weighed rather than tallied.
                (objective[2], "turret"),
            ],
            enemies: [(objective[3], "prime-mobile")]);

        (int own, int enemy, bool selfPresent) =
            ArenaBasics.ObjectivePresence(contract, context);

        Assert.True(selfPresent);
        Assert.Equal(2, own);
        Assert.Equal(1, enemy);
    }

    [Fact]
    public void ObjectivePresence_IgnoresBodiesOffTheActiveObjective()
    {
        GenericActorResolvedMatchContract contract =
            Contract(FrontlineLabsPendulumArm.None);
        Position[] elsewhere = ArenaBasics.ObjectiveTiles(contract, 1);

        GenericActorContext context = Observation(
            contract,
            self: new Position(9, 7),
            activePositionIndex: 2,
            allies: [(elsewhere[0], "prime-mobile")],
            enemies: [(elsewhere[1], "prime-mobile")]);

        Assert.Equal((0, 0, false), ArenaBasics.ObjectivePresence(
            contract,
            context));
    }

    [Fact]
    public void Advance_CommitsThroughBodiesStandingBetweenItAndTheObjective()
    {
        GenericActorResolvedMatchContract contract =
            Contract(FrontlineLabsPendulumArm.ForwardRally);
        // Every walkable approach to objective 1 occupied by an allied body —
        // the state a contract that rallies arrivals onto one region produces
        // whenever reinforcements land together. Bodies move; only walls do
        // not, so the route must survive them.
        (Position Position, string FormId)[] blockers =
        [
            (new Position(8, 5), "prime-mobile"),
            (new Position(6, 4), "prime-mobile"),
            (new Position(7, 4), "prime-mobile"),
            (new Position(6, 7), "prime-mobile"),
            (new Position(7, 7), "prime-mobile"),
        ];
        var occupied = blockers.Select(blocker => blocker.Position).ToHashSet();

        GenericActorContext context = Observation(
            contract,
            self: new Position(9, 5),
            activePositionIndex: 1,
            allies: blockers);

        GenericActorDecision? decision =
            ArenaBasics.TryAdvanceToActiveObjective(contract, context);

        Assert.NotNull(decision);
        Direction step = Assert.IsType<
                GenericActorActionArgument.DirectionArgument>(
                Assert.Single(decision.Arguments))
            .Value;
        (int dx, int dy) = step.Vector();
        // It commits, and it still refuses to walk into a body this tick:
        // only the first step is executed, so only the first step is bound by
        // where bodies stand right now.
        Assert.DoesNotContain(
            new Position(9, 5).Offset(dx, dy),
            occupied);
    }

    /// <summary>
    /// A minimal canonical observation for one team-0 prime. Only the fields
    /// the scaffold reads are populated; everything else is the emptiest legal
    /// value, so a test failure points at the helper rather than at scenery.
    /// </summary>
    /// <summary>
    /// The scaffold READS the live hold now instead of reconstructing it. Both
    /// sides of it matter: the owner and the expiry come straight off the
    /// observation, and the "whose is it" answer — the one that previously had
    /// no derivation at all — is correct for the team that does NOT own it.
    /// </summary>
    [Fact]
    public void TheScaffoldReadsTheLiveHoldRatherThanInferringIt()
    {
        GenericActorResolvedMatchContract contract =
            Contract(
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally
                    | FrontlineLabsPendulumArm.ContestMajority
                    | FrontlineLabsPendulumArm.EnemySoleDecay);

        // This life is team 0; the hold belongs to team 1.
        ArenaBasics.Hold hostile = Assert.IsType<ArenaBasics.Hold>(
            ArenaBasics.LiveHold(
                Observation(
                    contract,
                    new Position(3, 7),
                    activePositionIndex: 2,
                    holdOwnerTeamId: 1,
                    holdEndsAtTick: 41)));
        Assert.Equal(1, hostile.OwnerTeamId);
        Assert.False(hostile.Mine);
        Assert.Equal(41, hostile.EndsAtTick);
        // The observation is built on tick 1.
        Assert.Equal(40, hostile.RemainingTicks);

        ArenaBasics.Hold own = Assert.IsType<ArenaBasics.Hold>(
            ArenaBasics.LiveHold(
                Observation(
                    contract,
                    new Position(3, 7),
                    activePositionIndex: 2,
                    holdOwnerTeamId: 0,
                    holdEndsAtTick: 41)));
        Assert.True(own.Mine);

        // No hold live: the scaffold answers "none" rather than guessing from
        // front displacement, and the same is true on a contract that declares
        // no hold at all.
        Assert.Null(
            ArenaBasics.LiveHold(
                Observation(contract, new Position(3, 7), 2)));
        Assert.Null(
            ArenaBasics.LiveHold(
                Observation(
                    Contract(FrontlineLabsPendulumArm.None),
                    new Position(3, 7),
                    2)));
    }

    /// <summary>
    /// "Should I eat this?" is now answerable from the bolt: the arrival tick
    /// comes from the published cadence and the bill from the published damage.
    /// </summary>
    [Fact]
    public void TheScaffoldPricesAnIncomingBoltFromTheBoltItself()
    {
        GenericActorResolvedMatchContract contract =
            Contract(FrontlineLabsPendulumArm.None);
        var target = new Position(8, 7);
        var bolt = new GenericActorContext.ObservedProjectile(
            projectileId: 4,
            ownerTeamId: 1,
            ownerActorId: null,
            new Position(3, 7),
            ProjectileHeading.East,
            tilesPerAdvance: 2,
            ticksUntilAdvance: 1,
            remainingTiles: 9,
            [new ActorIdentity(0, 0, 0)],
            ticksPerAdvance: 3,
            damagePerHit: 2);

        ArenaBasics.Incoming incoming = Assert.IsType<ArenaBasics.Incoming>(
            ArenaBasics.Threat(bolt, target));
        Assert.Equal(5, incoming.Tiles);
        Assert.Equal(2, incoming.Damage);
        // Five tiles at two tiles per advance is three advances: the first is
        // one tick away and each later one costs a full three-tick cadence.
        Assert.Equal(1 + 2 * 3, incoming.TicksUntilArrival);

        // Off the heading, and beyond the remaining range, are both "no".
        Assert.Null(ArenaBasics.Threat(bolt, new Position(8, 9)));
        Assert.Null(ArenaBasics.Threat(bolt, new Position(20, 7)));
    }

    private static GenericActorContext Observation(
        GenericActorResolvedMatchContract contract,
        Position self,
        int activePositionIndex,
        IEnumerable<(Position Position, string FormId)>? allies = null,
        IEnumerable<(Position Position, string FormId)>? enemies = null,
        int? holdOwnerTeamId = null,
        int? holdEndsAtTick = null,
        IEnumerable<GenericActorContext.ObservedProjectile>? projectiles =
            null)
    {
        var selfId = new ActorIdentity(0, 0, 0);
        int allyUnit = 1;
        int enemyUnit = 0;
        return new GenericActorContext(
            GenericActorContext.CurrentSchemaVersion,
            tick: 1,
            contract.MatchContractFingerprint,
            new GenericActorContext.ObservedSelfState(
                selfId,
                generation: 0,
                "prime-mobile",
                self,
                Direction.East,
                health: 3,
                cooldown: 0,
                energy: null,
                previousActionResolution: null,
                pendingSameLifeTransition: null),
            [
                new GenericActorContext.ObservedUnitSlot(
                    0,
                    0,
                    new GenericActorContext.UnitSlotState.Active(
                        selfId,
                        0,
                        "prime-mobile")),
            ],
            [
                new GenericActorContext.ObservedParticipantStatus(
                    0,
                    0,
                    0,
                    false),
                new GenericActorContext.ObservedParticipantStatus(
                    1,
                    1,
                    0,
                    false),
            ],
            (allies ?? []).Select(ally =>
                new GenericActorContext.ObservedAllyState(
                    new ActorIdentity(0, allyUnit++, 0),
                    0,
                    ally.FormId,
                    ally.Position,
                    Direction.East,
                    3,
                    0,
                    null,
                    null,
                    null)),
            (enemies ?? []).Select(enemy =>
                new GenericActorContext.ObservedEnemyState(
                    new ActorIdentity(1, enemyUnit++, 0),
                    enemy.FormId,
                    enemy.Position,
                    Direction.West,
                    3,
                    null,
                    [selfId])),
            visibleTiles: [],
            visibleProjectiles: projectiles ?? [],
            visibleEvents: [],
            heardSounds: null,
            new GenericActorContext.ScoreboardState(
            [
                TeamScore(0),
                TeamScore(1),
            ]),
            new GenericActorContext.ModeObservationState.Frontline(
                "frontline",
                activePositionIndex,
                claimingTeamId: null,
                captureProgress: 0,
                decayTicksElapsed: 0,
                controlResumesAtTick: 0,
                holdOwnerTeamId,
                holdEndsAtTick),
            AllCardinalsLegal(contract))
        {
            Random = new AlwaysFalseRandom(),
        };
    }

    private static GenericActorContext.TeamScoreState TeamScore(int teamId) =>
        new(
            teamId,
            true,
            [new GenericActorContext.ScoreValue("territorial-progress", 0)]);

    /// <summary>
    /// Every declared action available with all four cardinals allowed. The
    /// scaffold selects actions by contract kind, so the IDs come from the
    /// contract rather than from a hard-coded catalog.
    /// </summary>
    private static GenericActorActionLegality[] AllCardinalsLegal(
        GenericActorResolvedMatchContract contract) =>
        contract.Rules.Actions
            .Where(action =>
                action.Kind
                    is GenericActorRulesContract.ActionKind.Movement
                    or GenericActorRulesContract.ActionKind.Rotation
                    or GenericActorRulesContract.ActionKind.Wait)
            .Select(action => new GenericActorActionLegality(
                action.Id,
                action.Code,
                allowedByForm: true,
                available: true,
                action.Kind == GenericActorRulesContract.ActionKind.Wait
                    ? []
                    :
                    [
                        new GenericActorActionLegality.ArgumentConstraint
                            .DirectionConstraint(
                            [
                                Direction.North,
                                Direction.East,
                                Direction.South,
                                Direction.West,
                            ]),
                    ]))
            .ToArray();

    /// <summary>
    /// The scaffold consumes the per-life stream only to break residual
    /// direction ties; a constant answer keeps the assertions about routing.
    /// </summary>
    private sealed class AlwaysFalseRandom : IBotRandom
    {
        public int NextInt(int minimumInclusive, int maximumExclusive) =>
            minimumInclusive;

        public bool NextBool() => false;

        public double NextDouble() => 0;
    }

    private static ArenaBasics.CaptureRules Capture(
        GenericActorResolvedMatchContract contract)
    {
        ArenaBasics.CaptureRules? capture = ArenaBasics.Capture(contract);
        Assert.NotNull(capture);
        return capture;
    }

    /// <summary>
    /// The counterweighted contract for one arm, or the measured control when
    /// no arm is asked for — the control is not an arm and has its own factory.
    /// </summary>
    private static GenericActorResolvedMatchContract Contract(
        FrontlineLabsPendulumArm arm) =>
        Parse(
            arm == FrontlineLabsPendulumArm.None
                ? FrontlineLabsDefinition.Create()
                : FrontlineLabsDefinition.CreatePendulumExperiment(arm));

    private static GenericActorResolvedMatchContract Parse(
        ActorResolvedMatchDefinition definition) =>
        ActorCanonicalContractReader.Parse(
            ActorContractManifestSerializer.ToCanonicalJson(definition));
}
