using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// ONE DECISION MAP (docs/DESIGN-MIND-ARCHITECTURE §7.4, "dissolves").
///
/// <para>Wave 8 reported the invest same-tick race: two teammates investing on
/// the same tick against a bank that covers one tier, the second Blocked and a
/// body's action wasted. The memo's claim is that the architecture DISSOLVES it
/// — "one mind, one decision — it simply does not issue two" — and this file
/// checks the two mechanical facts that claim rests on, because a claim about a
/// dissolved coordination problem is only true if the information really is in
/// one place and the outcome really is ordered.</para>
///
/// <list type="number">
/// <item>THE MASK PATH. Every own live body's <c>invest</c> legality is
/// IDENTICAL within a tick, because it is a function of the team's frozen
/// tick-start bank. Under the per-life profile that same identical answer
/// reached N independent deciders, which is exactly what made the race
/// unavoidable; a mind sees it once, for the whole army, in one call.</item>
/// <item>THE RESOLUTION PATH. A mind's commands are ONE MAP: at most one
/// command per body (a duplicate faults the turn), and the bodies it resolves
/// are published in canonical order. So even a mind that does issue two invests
/// gets a deterministic, ordered outcome rather than a race.</item>
/// </list>
/// </summary>
public sealed class GenericMindSingleDecisionMapTests
{
    private const string InvestActionId = "invest";

    private static ActorResolvedMatchDefinition EconomyMindDefinition() =>
        GenericMindSessionTestFixture.OnMindProfile(
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                (
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                capture: FrontlineLabsCaptureArm.Channel,
                economy: FrontlineLabsEconomyArm.Scrap,
                roster: FrontlineLabsRosterArm.Legion));

    [Fact]
    public void EveryBodySeesTheSameInvestMaskBecauseTheBankIsOneNumber()
    {
        ActorResolvedMatchDefinition definition = EconomyMindDefinition();
        Assert.Contains(
            definition.Rules.Actions,
            action => string.Equals(
                action.Id,
                InvestActionId,
                StringComparison.Ordinal));

        var seen = new List<GenericMindRuntimeObservation>();
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) =>
                {
                    seen.Add(observation);
                    return GenericMindSessionTestFixture.ScriptedMind(
                        definition,
                        observation);
                });
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_675_309);
        for (int tick = 0; tick < 40; tick++)
            session.Step();

        int multiBodyTicks = 0;
        foreach (GenericMindRuntimeObservation observation in seen)
        {
            if (observation.Bodies.Length < 2)
                continue;
            multiBodyTicks++;
            GenericActorRuntimeActionLegality?[] invest = [
                .. observation.Bodies.Select(body =>
                    body.ActionLegalities.SingleOrDefault(legality =>
                        string.Equals(
                            legality.ActionId,
                            InvestActionId,
                            StringComparison.Ordinal))),
            ];
            // One answer for the whole army: same availability, same allowed
            // tracks, on every body, in the same call.
            Assert.All(
                invest,
                legality => Assert.Equal(
                    Describe(invest[0]),
                    Describe(legality)));
        }

        Assert.True(
            multiBodyTicks > 20,
            "the legion roster should field several bodies per tick");
    }

    [Fact]
    public void CommandsAreOneMapPerBodyResolvedInCanonicalOrder()
    {
        ActorResolvedMatchDefinition definition = EconomyMindDefinition();
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) =>
                    GenericMindSessionTestFixture.ScriptedMind(
                        definition,
                        observation));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_675_309);
        for (int tick = 0; tick < 40; tick++)
            session.Step();

        foreach (GenericActorMatchMindTurn turn in session.Chronology.Ticks
                     .SelectMany(frame => frame.MindTurns))
        {
            // At most one command per body — the map's key set.
            ImmutableArray<(int UnitId, int LifeId)> keys = [
                .. turn.Commands.Select(resolution => (
                    resolution.Command.UnitId,
                    resolution.Command.LifeId)),
            ];
            Assert.Equal(keys.Length, keys.Distinct().Count());
            // And the bodies it resolved are published canonically ordered, so
            // two commands that contend for one scarce resource resolve in a
            // fixed order rather than by whichever runtime answered first.
            Assert.Equal(
                turn.ResolvedBodies.Order().ToArray(),
                turn.ResolvedBodies.ToArray());
        }
    }

    private static string Describe(
        GenericActorRuntimeActionLegality? legality) =>
        legality is null
            ? "absent"
            : $"{legality.AllowedByForm}/{legality.Available}/"
            + string.Join(
                ",",
                legality.Constraints
                    .OfType<GenericActorRuntimeActionLegality.ArgumentConstraint
                        .UpgradeTrackConstraint>()
                    .SelectMany(constraint => constraint.AllowedTrackIds)
                    .Order(StringComparer.Ordinal));
}
