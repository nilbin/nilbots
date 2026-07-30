using BotArena.Engine;
using BotArena.Guest;
using BotArena.Sdk;

namespace BotArena.Guest.Tests;

/// <summary>
/// The guest carries its own copy of the team stream's arithmetic (it cannot
/// reference the Engine), so the two must be pinned bit-identical here — the
/// same discipline GuestRandom follows for the per-life stream. A drift here
/// would show up in production as teammates disagreeing inside WASM while the
/// in-process runtime agreed.
/// </summary>
public sealed class GuestTeamRandomTests
{
    [Fact]
    public void GuestTeamStreamMatchesTheEngineStreamBitForBit()
    {
        const ulong teamSeed = 0x0FED_CBA9_8765_4321UL;
        var guest = new GuestTeamRandom(teamSeed);
        var engine = new TeamTickRandom(teamSeed);

        for (int tick = 0; tick < 32; tick++)
        {
            guest.BeginTick(tick);
            engine.BeginTick(tick);
            for (int index = 0; index < 8; index++)
            {
                Assert.Equal(
                    engine.NextInt(-5, 1_000),
                    guest.NextInt(-5, 1_000));
                Assert.Equal(engine.NextBool(), guest.NextBool());
                Assert.Equal(engine.NextDouble(), guest.NextDouble());
            }
        }
    }

    [Fact]
    public void GuestTeamStreamIsPathIndependentAcrossTicks()
    {
        const ulong teamSeed = 987_654_321UL;
        var veteran = new GuestTeamRandom(teamSeed);
        for (int tick = 0; tick < 9; tick++)
        {
            veteran.BeginTick(tick);
            for (int index = 0; index <= tick; index++)
                veteran.NextInt(0, 100);
        }
        veteran.BeginTick(9);

        var newborn = new GuestTeamRandom(teamSeed);
        newborn.BeginTick(9);

        for (int index = 0; index < 6; index++)
            Assert.Equal(veteran.NextInt(0, 100), newborn.NextInt(0, 100));
    }

    [Fact]
    public void GuestTeamStreamRejectsANegativeTickAndAnEmptyRange()
    {
        var random = new GuestTeamRandom(1);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => random.BeginTick(-1));
        Assert.Throws<ArgumentException>(() => random.NextInt(4, 4));
    }

    [Fact]
    public void TheGuestSessionReSeedsTheTeamStreamForEveryObservedTick()
    {
        GenericActorMatchStart start = GenericGuestTestFixture.Start();
        var bot = new TeamDrawBot();
        GenericActorGuestSession session = GenericActorGuestSession.Start(
            new GenericActorMatchStartEnvelope("bot", start),
            _ => bot);

        for (int tick = 0; tick < 4; tick++)
        {
            session.HandleTick(
                GenericGuestTestFixture.Context(start, tick));
        }

        var expected = new TeamTickRandom(start.TeamRandomSeed);
        for (int tick = 0; tick < 4; tick++)
        {
            expected.BeginTick(tick);
            Assert.Equal(
                new[]
                {
                    expected.NextInt(0, 1_000),
                    expected.NextInt(0, 1_000),
                },
                bot.Draws[tick]);
        }
        Assert.Equal(4, bot.Draws.Count);
        Assert.Equal(
            4,
            bot.Draws.Values
                .Select(draws => string.Join(",", draws))
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void TheGuestSessionDeliversTheTeamSeedFromMatchStart()
    {
        GenericActorMatchStart start = GenericGuestTestFixture.Start();
        var bot = new TeamDrawBot();
        GenericActorGuestSession.Start(
            new GenericActorMatchStartEnvelope("bot", start),
            _ => bot);

        Assert.NotNull(bot.Start);
        Assert.Equal(start.TeamRandomSeed, bot.Start.TeamRandomSeed);
        Assert.NotEqual(start.ActorRandomSeed, bot.Start.TeamRandomSeed);
    }

    private sealed class TeamDrawBot : IGenericActorBot
    {
        public GenericActorMatchStart? Start { get; private set; }

        public Dictionary<int, int[]> Draws { get; } = [];

        public void StartLife(GenericActorMatchStart start) => Start = start;

        public GenericActorDecision Tick(GenericActorContext context)
        {
            Draws[context.Tick] =
            [
                context.TeamRandom.NextInt(0, 1_000),
                context.TeamRandom.NextInt(0, 1_000),
            ];
            return new GenericActorDecision("wait", 0, []);
        }
    }
}
