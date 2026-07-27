using BotArena.Engine;
using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public sealed class FrontlineBoundaryTests
{
    [Fact]
    public void Resolver_RejectsPrimeRespawnScheduleBeyondAbsoluteTickRange()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 100,
            primeRespawnTicks: int.MaxValue);

        MatchDefinitionValidationException exception =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                FrontlineTestDefinitions.ResolveOpen(rules));

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "MaxTicks plus PrimeRespawnTicks",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Resolver_RejectsRedeployScheduleBeyondAbsoluteTickRange()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 100,
            redeployPauseTicks: int.MaxValue);

        MatchDefinitionValidationException exception =
            Assert.Throws<MatchDefinitionValidationException>(() =>
                FrontlineTestDefinitions.ResolveOpen(rules));

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "MaxTicks plus RedeployPauseTicks",
                StringComparison.Ordinal));
    }

    [Fact]
    public void DirectControlKernel_RejectsOverflowingRedeploySchedule()
    {
        var rules = new FrontlineRules
        {
            FrontlinePositionCount = 3,
            PushesToBreach = 2,
            CaptureThreshold = 1,
            RedeployPauseTicks = int.MaxValue,
        };
        FrontlineControlState state =
            FrontlineControlSystem.CreateInitial(rules);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrontlineControlSystem.Step(
                rules,
                state,
                tick: 0,
                new FrontlineTeamPresence(
                    Team0Present: true,
                    Team1Present: false)));
    }

    [Fact]
    public void FullEnergy_RegenerationDoesNotOverflow()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 2) with
        {
            MaxEnergy = int.MaxValue,
            EnergyRegenTicks = 1,
        };
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen(rules));
        FrontlineTickStart tickStart = session.PrepareTick();

        session.Step(tickStart.ActiveActors.ToDictionary(
            actorId => actorId,
            _ => BotDecision.Of(BotAction.Wait)));

        Assert.All(
            session.State.Teams,
            team => Assert.Equal(
                int.MaxValue,
                team.GetUnit(0).ActiveLife?.Energy));
    }
}
