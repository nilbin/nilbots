using System.Collections.Immutable;
using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the route cooldown (#181) in play: a cooldown-bearing route is
/// refused for its declared window after each completion, the availability
/// mask says so, a zero cooldown keeps the historical contract
/// byte-identical, and the chronology validator accepts the produced
/// histories (the session run IS the validator run). The clock is keyed by
/// unit slot, so it survives the body by construction; automatic returns
/// are exempt by design.
/// </summary>
public sealed class ActorRouteCooldownTests
{
    private static GenericActorMatchChronology Cycle(int forwardCooldown)
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.DefinitionWithSameLifeTransition(
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 40,
                },
                new GenericDeathmatchSessionTestFixture.SameLifeOptions
                {
                    DurationTicks = 1,
                    IrreversibleForLife = false,
                    IncludeReverseRoute = true,
                    ReverseDurationTicks = 1,
                    ForwardCooldownTicks = forwardCooldown,
                });

        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) =>
                    observation.ActionLegalities.Any(legality =>
                        legality.ActionId == "transform"
                        && legality.Available)
                        ? GenericDeathmatchSessionTestFixture.Transform(
                            observation.Self.FormId == "anchored"
                                ? "mobile"
                                : "anchored")
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            11);
        session.Run();
        return session.Chronology;
    }

    private static int[] ForwardCompletionTicks(
        GenericActorMatchChronology chronology) =>
    [
        .. chronology.Ticks
            .SelectMany(frame => frame.TickStart.Events.Concat(frame.Events))
            .Where(item => item.Kind
                    == GenericActorRuntimeObservation.EventKind
                        .FormTransitionCompleted
                && item.Payload is GenericActorRuntimeObservation
                    .EventPayload.FormTransition transition
                && transition.TransitionId == "anchor-mobile"
                && transition.ActorId.TeamId == 0)
            .Select(item => item.Tick)
            .OrderBy(tick => tick),
    ];

    [Fact]
    public void ADeclaredCooldownSpacesTheRouteAndZeroKeepsHistory()
    {
        int[] free = ForwardCompletionTicks(Cycle(forwardCooldown: 0));
        int[] priced = ForwardCompletionTicks(Cycle(forwardCooldown: 6));

        Assert.True(
            free.Length >= 4,
            $"the free cycler only completed {free.Length} anchors");
        Assert.True(
            priced.Length >= 2,
            $"the priced cycler only completed {priced.Length} anchors");
        // Zero cooldown: the fastest legal cycle (forward 1 + reverse 1).
        Assert.True(
            free.Zip(free.Skip(1), (a, b) => b - a).Min() <= 3,
            "the historical contract slowed down");
        // Cooldown 6: a completion at T refuses the route while
        // tick < T + 7, and a duration-1 windup completes the tick it
        // starts — so consecutive forward completions sit exactly
        // cooldown + 1 apart at the fastest.
        int minGap = priced.Zip(priced.Skip(1), (a, b) => b - a).Min();
        Assert.True(
            minGap >= 7,
            $"cooldown 6 allowed a forward completion gap of {minGap}");
        // And the cooldown does not deadlock the cycle: it repeats.
        Assert.True(priced.Length >= 3, "the priced cycle stalled");
    }
}
