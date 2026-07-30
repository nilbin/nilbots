using System.Collections.Immutable;
using BotArena.Runtime;
using Sdk = BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// The team stream's whole product is agreement without a channel, so this
/// suite pins the agreement itself — not the arithmetic (RandomTests pins
/// that) and not the wire (the codec tests pin that). Every case runs real
/// lives through a real session and the real SDK surface.
/// </summary>
public sealed class TeamRandomCoordinationTests
{
    private const int DrawsPerTick = 4;

    [Fact]
    public void TwoLivesOnOneTeamDrawIdenticalSequencesAtTheSameTick()
    {
        var ledger = new DrawLedger();
        RunTeamsMatch(ledger, matchSeed: 909, maxTicks: 6);

        (ActorIdentity Actor, int Tick)[] teamZero = [.. ledger.Keys
            .Where(key => key.Actor.TeamId == 0)];
        Assert.NotEmpty(teamZero);

        foreach (IGrouping<int, (ActorIdentity Actor, int Tick)> tick in
                 teamZero.GroupBy(key => key.Tick))
        {
            (ActorIdentity Actor, int Tick)[] keys = [.. tick];
            Assert.True(
                keys.Length >= 2,
                $"tick {tick.Key} needs at least two team-0 lives to compare");
            int[] expected = ledger.Draws(keys[0]);
            foreach ((ActorIdentity Actor, int Tick) key in keys.Skip(1))
                Assert.Equal(expected, ledger.Draws(key));
        }
    }

    [Fact]
    public void PrivateRandomUseDoesNotShiftTheTeamStream()
    {
        // The trap this capability exists to close: one life burning private
        // draws must not move its teammates' shared plan. Unit 1 draws from
        // context.Random a tick-varying number of times; unit 0 never touches
        // it, and their team draws still agree exactly.
        var ledger = new DrawLedger();
        RunTeamsMatch(
            ledger,
            matchSeed: 4_242,
            maxTicks: 5,
            privateDrawsFor: actorId =>
                actorId.UnitId == 1 ? actorId.UnitId + 3 : 0);

        foreach (IGrouping<int, (ActorIdentity Actor, int Tick)> tick in
                 ledger.Keys
                     .Where(key => key.Actor.TeamId == 0)
                     .GroupBy(key => key.Tick))
        {
            (ActorIdentity Actor, int Tick)[] keys = [.. tick];
            Assert.Equal(2, keys.Length);
            Assert.Equal(ledger.Draws(keys[0]), ledger.Draws(keys[1]));
        }
    }

    [Fact]
    public void TheTwoTeamsDrawUnrelatedSequences()
    {
        var ledger = new DrawLedger();
        RunTeamsMatch(ledger, matchSeed: 909, maxTicks: 6);

        int comparedTicks = 0;
        foreach (IGrouping<int, (ActorIdentity Actor, int Tick)> tick in
                 ledger.Keys.GroupBy(key => key.Tick))
        {
            (ActorIdentity Actor, int Tick)? zero = tick
                .FirstOrDefault(key => key.Actor.TeamId == 0);
            (ActorIdentity Actor, int Tick)? one = tick
                .FirstOrDefault(key => key.Actor.TeamId == 1);
            if (zero is null || one is null)
                continue;
            comparedTicks++;
            Assert.NotEqual(
                ledger.Draws(zero.Value),
                ledger.Draws(one.Value));
        }

        Assert.True(comparedTicks >= 4);
    }

    [Fact]
    public void DrawsChangeFromTickToTick()
    {
        var ledger = new DrawLedger();
        RunTeamsMatch(ledger, matchSeed: 909, maxTicks: 6);

        int[][] perTick =
        [
            .. ledger.Keys
                .Where(key => key.Actor.TeamId == 0)
                .GroupBy(key => key.Tick)
                .OrderBy(group => group.Key)
                .Select(group => ledger.Draws(group.First())),
        ];

        Assert.True(perTick.Length >= 5);
        Assert.Equal(
            perTick.Length,
            perTick
                .Select(draws => string.Join(",", draws))
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void ALifeBornMidMatchAgreesWithItsTeammatesOnItsFirstTick()
    {
        // A consumable stream cannot do this: the returning life has drawn
        // nothing yet, while its teammate has drawn on every prior tick. The
        // per-tick re-derivation makes the age irrelevant.
        var ledger = new DrawLedger();
        (ActorIdentity Actor, int Tick)[] respawned = RunRespawnMatch(ledger);

        Assert.NotEmpty(respawned);
        foreach ((ActorIdentity Actor, int Tick) newborn in respawned)
        {
            (ActorIdentity Actor, int Tick)[] veterans = [.. ledger.Keys
                .Where(key =>
                    key.Tick == newborn.Tick
                    && key.Actor.TeamId == newborn.Actor.TeamId
                    && key.Actor != newborn.Actor)];
            Assert.NotEmpty(veterans);
            foreach ((ActorIdentity Actor, int Tick) veteran in veterans)
            {
                Assert.Equal(
                    ledger.Draws(veteran),
                    ledger.Draws(newborn));
            }
        }
    }

    [Fact]
    public void EveryLifeOnATeamReceivesTheIdenticalTeamSeed()
    {
        var ledger = new DrawLedger();
        RunTeamsMatch(ledger, matchSeed: 909, maxTicks: 6);

        foreach (IGrouping<int, Sdk.GenericActorMatchStart> team in
                 ledger.Starts.GroupBy(start => start.ActorId.TeamId))
        {
            Assert.True(team.Count() >= 2);
            Assert.Single(team.Select(start => start.TeamRandomSeed)
                .Distinct());
        }
        Assert.Equal(
            2,
            ledger.Starts
                .Select(start => start.TeamRandomSeed)
                .Distinct()
                .Count());
        Assert.True(
            ledger.Starts
                .Select(start => start.ActorRandomSeed)
                .Distinct()
                .Count() > 2,
            "the per-life stream must stay per life");
    }

    [Fact]
    public void ATeamRandomDrivenMatchReplaysToTheIdenticalHash()
    {
        string first = TeamRandomDrivenReplayHash(matchSeed: 5_150);
        string second = TeamRandomDrivenReplayHash(matchSeed: 5_150);
        string other = TeamRandomDrivenReplayHash(matchSeed: 5_151);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    private static string TeamRandomDrivenReplayHash(ulong matchSeed)
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "teams",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 6,
                });
        using GenericDeathmatchSession session = new(
            definition,
            Configurations(
                definition,
                () => new TeamRandomSteeredBot()),
            matchSeed);
        session.Run();
        return ReplayV3Serializer.ComputeHash(
            ReplayV3Projection.Project(session.Chronology, presentation: null));
    }

    private static void RunTeamsMatch(
        DrawLedger ledger,
        ulong matchSeed,
        int maxTicks,
        Func<ActorIdentity, int>? privateDrawsFor = null)
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "teams",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = maxTicks,
                });
        using GenericDeathmatchSession session = new(
            definition,
            Configurations(
                definition,
                () => new DrawProbeBot(ledger, privateDrawsFor)),
            matchSeed);
        session.Run();
    }

    private static (ActorIdentity Actor, int Tick)[] RunRespawnMatch(
        DrawLedger ledger)
    {
        // Team 0 unit 0 shoots the moment it can; the fixture's west/north and
        // east/south spawns put one enemy in its line. Whoever dies returns
        // with a fresh lifeId while its teammate has been drawing all along.
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "teams",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 8,
                    MaxHealth = 1,
                    DamagePerHit = 1,
                    RespawnDelayTicks = 0,
                });
        using GenericDeathmatchSession session = new(
            definition,
            Configurations(
                definition,
                () => new DrawProbeBot(ledger, privateDrawsFor: null)
                {
                    ShootOnFirstTick = true,
                }),
            matchSeed: 31_337);
        session.Run();

        return [.. ledger.Starts
            .Where(start => start.Origin.Reason
                != Sdk.GenericActorMatchStart.SpawnReason.Initial)
            .Select(start => new ActorIdentity(
                start.ActorId.TeamId,
                start.ActorId.UnitId,
                start.ActorId.LifeId))
            .SelectMany(actor => ledger.Keys
                .Where(key => key.Actor == actor)
                .OrderBy(key => key.Tick)
                .Take(1))];
    }

    private static ImmutableArray<GenericActorParticipantConfiguration>
        Configurations(
            ActorResolvedMatchDefinition definition,
            Func<Sdk.IGenericActorBot> botFactory) =>
        [.. definition.Topology.Participants.Select(participant =>
            new GenericActorParticipantConfiguration
            {
                ParticipantId = participant.ParticipantId,
                TeamId = participant.TeamId,
                Name = $"participant-{participant.ParticipantId}",
                RuntimeFactory =
                    new InProcessGenericActorRuntimeFactory(botFactory),
                ArtifactHash =
                    $"team-random-{participant.ParticipantId}",
            })];

    /// <summary>Records every team draw by exact life and tick.</summary>
    private sealed class DrawLedger
    {
        private readonly Dictionary<(ActorIdentity Actor, int Tick), int[]>
            _draws = [];

        public List<Sdk.GenericActorMatchStart> Starts { get; } = [];

        public IEnumerable<(ActorIdentity Actor, int Tick)> Keys =>
            _draws.Keys;

        public int[] Draws((ActorIdentity Actor, int Tick) key) =>
            _draws[key];

        public void Record(ActorIdentity actor, int tick, int[] draws) =>
            _draws[(actor, tick)] = draws;
    }

    private sealed class DrawProbeBot(
        DrawLedger ledger,
        Func<ActorIdentity, int>? privateDrawsFor) : Sdk.IGenericActorBot
    {
        private ActorIdentity _actorId = new(0, 0, 0);

        public bool ShootOnFirstTick { get; init; }

        public void StartLife(Sdk.GenericActorMatchStart start)
        {
            _actorId = new ActorIdentity(
                start.ActorId.TeamId,
                start.ActorId.UnitId,
                start.ActorId.LifeId);
            ledger.Starts.Add(start);
        }

        public Sdk.GenericActorDecision Tick(Sdk.GenericActorContext context)
        {
            for (int index = 0;
                 index < (privateDrawsFor?.Invoke(_actorId) ?? 0);
                 index++)
            {
                context.Random.NextInt(0, 1_000);
            }

            int[] draws = new int[DrawsPerTick];
            for (int index = 0; index < draws.Length; index++)
                draws[index] = context.TeamRandom.NextInt(0, 1_000);
            ledger.Record(_actorId, context.Tick, draws);

            return ShootOnFirstTick
                    && _actorId.TeamId == 0
                    && _actorId.UnitId == 0
                    && context.Action("shoot") is not null
                ? new Sdk.GenericActorDecision(
                    "shoot",
                    4,
                    [
                        new Sdk.GenericActorActionArgument
                            .ShotProgramArgument(Sdk.ShotProgram.Straight),
                    ])
                : new Sdk.GenericActorDecision("wait", 0, []);
        }
    }

    /// <summary>
    /// Lets the shared stream pick the action, so the team stream is inside
    /// the authoritative history the replay hash covers.
    /// </summary>
    private sealed class TeamRandomSteeredBot : Sdk.IGenericActorBot
    {
        public Sdk.GenericActorDecision Tick(Sdk.GenericActorContext context)
        {
            Sdk.Direction[] directions =
            [
                Sdk.Direction.North,
                Sdk.Direction.East,
                Sdk.Direction.South,
                Sdk.Direction.West,
            ];
            Sdk.Direction direction =
                directions[context.TeamRandom.NextInt(0, directions.Length)];
            return context.TeamRandom.NextBool()
                    && context.Action("move") is not null
                ? new Sdk.GenericActorDecision(
                    "move",
                    1,
                    [
                        new Sdk.GenericActorActionArgument.DirectionArgument(
                            direction),
                    ])
                : new Sdk.GenericActorDecision("wait", 0, []);
        }
    }
}
