using System.Collections.Immutable;
using System.Reflection;
using BotArena.Engine;
using BotArena.Sdk;
using ActorIdentity = BotArena.Sdk.ActorIdentity;
using Direction = BotArena.Sdk.Direction;
using Position = BotArena.Sdk.Position;

/// <summary>
/// The tactical firing-ground boundary for Tapline-15: a body behind its own
/// teammate must seek reachable clear ground instead of holding and wasting a
/// shot into the teammate. Historical transparent lanes remain unchanged.
/// </summary>
public sealed class FriendlyFiringLaneTests
{
    private static readonly Position ShooterTile = new(12, 13);
    private static readonly Position BlockerTile = new(14, 13);
    private static readonly Position TargetTile = new(17, 13);

    [Fact]
    public void BlockedLongshotDropsItsCurrentTileAndKeepsClearRepositionGoals()
    {
        MindBody shooter = Body(0, ShooterTile, ArcRelayLaunchClassIds.Longshot);
        MindBody blocker = Body(1, BlockerTile, ArcRelayLaunchClassIds.Kestrel);
        MindContext mind = Context(shooter, blocker);
        GenericActorResolvedMatchContract contract = Contract(
            ArcRelayLoopProfile.Tapline15);

        int[] reachable = ArenaBasics.FiringEpisodeReachability(
            contract,
            mind,
            shooter,
            ShooterTile,
            chaseLimit: 4);
        Position[] goals = ArenaBasics.FiringContributionGoals(
            contract,
            mind,
            shooter,
            TargetTile,
            reachable);

        Assert.DoesNotContain(ShooterTile, goals);
        Assert.NotEmpty(goals);
        Assert.Contains(goals, tile =>
            tile != ShooterTile
            && ArenaBasics.FirstOwnBodyOnLine(
                tile,
                TargetTile,
                new HashSet<Position> { BlockerTile }) is null);
    }

    [Fact]
    public void HistoricalTransparentLaneStillKeepsTheCurrentFiringTile()
    {
        MindBody shooter = Body(0, ShooterTile, ArcRelayLaunchClassIds.Longshot);
        MindBody blocker = Body(1, BlockerTile, ArcRelayLaunchClassIds.Kestrel);
        MindContext mind = Context(shooter, blocker);
        GenericActorResolvedMatchContract contract = Contract(
            ArcRelayLoopProfile.Tapline14);

        int[] reachable = ArenaBasics.FiringEpisodeReachability(
            contract,
            mind,
            shooter,
            ShooterTile,
            chaseLimit: 4);
        Position[] goals = ArenaBasics.FiringContributionGoals(
            contract,
            mind,
            shooter,
            TargetTile,
            reachable);

        Assert.Contains(ShooterTile, goals);
    }

    private static GenericActorResolvedMatchContract Contract(
        ArcRelayLoopProfile profile) => ActorCanonicalContractReader.Parse(
        ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.Create(loopProfile: profile)));

    private static MindContext Context(params MindBody[] bodies) => new(
        MindContext.CurrentSchemaVersion,
        tick: 0,
        new string('a', 64),
        bodies,
        slots: [],
        allies: [],
        enemies: [],
        visibleTiles: [],
        visibleProjectiles: [],
        visibleEvents: [],
        heardSounds: [],
        new GenericActorContext.ScoreboardState(
        [
            new GenericActorContext.TeamScoreState(
                0,
                eligible: true,
                [new GenericActorContext.ScoreValue("score", 0)]),
        ]),
        new GenericActorContext.ModeObservationState.Deathmatch("fixture"),
        participants: []);

    private static MindBody Body(int unitId, Position position, string classId)
    {
        ConstructorInfo constructor = Assert.Single(typeof(MindBody)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        return (MindBody)constructor.Invoke(
        [
            new ActorIdentity(0, unitId, 0),
            0,
            ArcRelayH0Definition.FormPrefix + classId,
            position,
            Direction.East,
            30,
            0,
            1,
            null,
            null,
            classId,
            ImmutableArray<GenericActorContext.ObservedRouteCooldown>.Empty,
            0,
            null,
            false,
            0,
            new GenericActorMatchStart.LifeOrigin(
                GenericActorMatchStart.SpawnReason.Initial,
                0,
                null,
                null,
                null),
            null,
            0UL,
            ImmutableArray<GenericActorActionLegality>.Empty,
            new MindWaitAction("wait", 0),
        ]);
    }
}
