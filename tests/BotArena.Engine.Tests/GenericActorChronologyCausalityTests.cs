using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorChronologyCausalityTests
{
    [Fact]
    public void RejectsLifeStartsWithoutExactSpawnEvidence()
    {
        using GenericDeathmatchSession session = Session(
            new GenericDeathmatchSessionTestFixture.Options
            {
                MaxTicks = 1,
            });
        GenericActorMatchChronology chronology = session.Chronology;
        var initial = new GenericActorMatchInitialFrame(
            chronology.InitialFrame.State,
            chronology.InitialFrame.LifeStarts,
            events: []);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                initial,
                ticks: [],
                result: null));
    }

    [Fact]
    public void RejectsTickStartSpawnWithoutExactSpawnEvidence()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 4,
                    IncludeSplit = true,
                    SplitDurationTicks = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ActorId == new ActorIdentity(0, 0, 0)
                    && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 904);
        ExecuteTicks(session, 2);
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame completed = chronology.Ticks[1];
        GenericActorAuthoritativeEvent missingSpawn = completed
            .TickStart.Events
            .First(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned);
        GenericActorAuthoritativeEvent[] incompleteEvents = completed
            .TickStart.Events
            .Where(item => !ReferenceEquals(item, missingSpawn))
            .ToArray();
        var tickStart = new GenericActorMatchTickStart(
            completed.Tick,
            completed.TickStart.State,
            completed.TickStart.ActiveActorIds,
            completed.TickStart.LifeStarts,
            incompleteEvents,
            completed.TickStart.Traversals);
        var frame = new GenericActorMatchTickFrame(
            tickStart,
            completed.ActorTurns,
            completed.Events,
            completed.Traversals,
            completed.PostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [chronology.Ticks[0], frame],
                result: null));
    }

    [Fact]
    public void RejectsRemovedLifeWithoutRetirementOrDestructionEvidence()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                    MaxHealth = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) => observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 905);
        ExecuteTicks(session, 2);
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame terminal = chronology.Ticks[1];
        GenericActorAuthoritativeEvent[] incompleteEvents = terminal.Events
            .Where(item => item.Kind
                != GenericActorRuntimeObservation.EventKind.Destruction)
            .ToArray();
        var frame = new GenericActorMatchTickFrame(
            terminal.TickStart,
            terminal.ActorTurns,
            incompleteEvents,
            terminal.Traversals,
            terminal.PostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [chronology.Ticks[0], frame],
                result: null));
    }

    [Fact]
    public void RejectsStaleDerivedActiveHealth()
    {
        using GenericDeathmatchSession session = Session(
            new GenericDeathmatchSessionTestFixture.Options
            {
                MaxTicks = 1,
            });
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorWorldSnapshot source = chronology.InitialFrame.State;
        string activeHealth = ActorContractCanonicalIds.Id(
            ScoreChannelDefinition.ChannelKind.ActiveHealth);
        var scoreboard =
            new GenericActorRuntimeObservation.ScoreboardState(
                source.Scoreboard.Teams
                    .Select((team, teamIndex) =>
                        new GenericActorRuntimeObservation.TeamScoreState(
                            team.TeamId,
                            team.Eligible,
                            team.Scores.Select(score =>
                                new GenericActorRuntimeObservation.ScoreValue(
                                    score.Channel,
                                    teamIndex == 0
                                    && string.Equals(
                                        score.Channel,
                                        activeHealth,
                                        StringComparison.Ordinal)
                                        ? score.Value + 1
                                        : score.Value))
                                .ToImmutableArray()))
                    .ToImmutableArray());
        GenericActorWorldSnapshot stale =
            CopyWorld(source, chronology.Descriptor.Definition, scoreboard);
        var initial = new GenericActorMatchInitialFrame(
            stale,
            chronology.InitialFrame.LifeStarts,
            chronology.InitialFrame.Events);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                initial,
                ticks: [],
                result: null));
    }

    [Fact]
    public void RejectsProjectileRemovalWithoutLifecyclePlacementTraversal()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) => observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 906);
        ExecuteTicks(session, 2);
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchTickFrame second = chronology.Ticks[1];
        Assert.NotEmpty(second.TickStart.State.Projectiles);
        GenericActorWorldSnapshot missingProjectile = CopyWorld(
            second.TickStart.State,
            definition,
            projectiles: second.TickStart.State.Projectiles.Skip(1));
        var tickStart = new GenericActorMatchTickStart(
            second.Tick,
            missingProjectile,
            second.TickStart.ActiveActorIds,
            second.TickStart.LifeStarts,
            second.TickStart.Events,
            second.TickStart.Traversals);
        var frame = new GenericActorMatchTickFrame(
            tickStart,
            second.ActorTurns,
            second.Events,
            second.Traversals,
            second.PostState);

        Assert.Throws<ArgumentException>(() =>
            new GenericActorMatchChronology(
                chronology.Descriptor,
                chronology.InitialFrame,
                [chronology.Ticks[0], frame],
                result: null));
    }

    private static GenericDeathmatchSession Session(
        GenericDeathmatchSessionTestFixture.Options options)
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                options);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        return new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 903);
    }

    private static void ExecuteTicks(
        GenericDeathmatchSession session,
        int count)
    {
        for (int index = 0; index < count; index++)
        {
            session.PrepareTick();
            session.Step();
        }
    }

    private static GenericActorWorldSnapshot CopyWorld(
        GenericActorWorldSnapshot source,
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeObservation.ScoreboardState? scoreboard = null,
        IEnumerable<GenericActorWorldSnapshot.ProjectileSnapshot>?
            projectiles = null) =>
        new(
            definition,
            source.NextTick,
            source.NextProjectileId,
            source.Participants,
            source.Slots,
            source.ActiveLives,
            source.PendingReplications,
            projectiles is null
                ? source.Projectiles
                : projectiles.ToArray(),
            scoreboard ?? source.Scoreboard,
            source.Mode);
}
