using BotArena.Engine;
using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public sealed class FrontlineObjectiveIntegrationTests
{
    [Fact]
    public void MovementPresence_CanAdvanceAndBreachBeforeMaxTicks()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 20,
            captureThreshold: 1,
            redeployPauseTicks: 0);
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveObjective(rules));

        Step(session, BotAction.MoveForward, BotAction.TurnRight);
        Step(session, BotAction.MoveForward, BotAction.MoveForward);
        FrontlineStepResult centreCapture =
            Step(session, BotAction.MoveForward, BotAction.Wait);

        Assert.Equal(2, session.State.Control.ActivePositionIndex);
        Assert.Contains(
            centreCapture.Events,
            gameEvent =>
                gameEvent.Type
                    == FrontlineMatchEventType.FrontlinePositionAdvanced
                && gameEvent.TeamId == 0
                && gameEvent.FromPositionIndex == 1
                && gameEvent.ToPositionIndex == 2);

        Step(session, BotAction.MoveForward, BotAction.Wait);
        FrontlineStepResult breach =
            Step(session, BotAction.MoveForward, BotAction.Wait);

        Assert.True(breach.MatchCompleted);
        Assert.NotNull(breach.Result);
        Assert.Equal(FrontlineMatchEndReason.BaseBreach, breach.Result.Reason);
        Assert.Equal(0, breach.Result.WinnerTeamId);
        Assert.Equal(4, breach.Result.EndTick);
        Assert.Contains(
            breach.Events,
            gameEvent =>
                gameEvent.Type == FrontlineMatchEventType.BaseBreached
                && gameEvent.TeamId == 0);
    }

    [Fact]
    public void BaseBreach_OnFinalAllowedTick_TakesPrecedenceOverMaxTicks()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 5,
            captureThreshold: 1,
            redeployPauseTicks: 0);
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveObjective(rules));

        Step(session, BotAction.MoveForward, BotAction.TurnRight);
        Step(session, BotAction.MoveForward, BotAction.MoveForward);
        Step(session, BotAction.MoveForward, BotAction.Wait);
        Step(session, BotAction.MoveForward, BotAction.Wait);
        FrontlineStepResult finalTick =
            Step(session, BotAction.MoveForward, BotAction.Wait);

        Assert.Equal(FrontlineMatchEndReason.BaseBreach, finalTick.Result?.Reason);
        Assert.Equal(0, finalTick.Result?.WinnerTeamId);
        Assert.Equal(4, finalTick.Result?.EndTick);
    }

    [Fact]
    public void MaxTickScore_CombinesPositionAndSignedClaimProgress()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 6,
            captureThreshold: 2,
            redeployPauseTicks: 0);
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveObjective(rules));

        Step(session, BotAction.MoveForward, BotAction.TurnRight);
        Step(session, BotAction.MoveForward, BotAction.MoveForward);
        Step(session, BotAction.MoveForward, BotAction.Wait);
        Step(session, BotAction.Wait, BotAction.Wait);
        Step(session, BotAction.MoveForward, BotAction.Wait);
        FrontlineStepResult finalTick =
            Step(session, BotAction.MoveForward, BotAction.Wait);

        Assert.Equal(FrontlineMatchEndReason.MaxTicks, finalTick.Result?.Reason);
        Assert.Equal(0, finalTick.Result?.WinnerTeamId);
        Assert.Equal(3L, finalTick.Result?.TerritorialScore);
        Assert.Equal(2, finalTick.Result?.Control.ActivePositionIndex);
        Assert.Equal(0, finalTick.Result?.Control.ClaimingTeamId);
        Assert.Equal(1, finalTick.Result?.Control.CaptureProgress);
        Assert.Collection(
            finalTick.Result!.Teams,
            team =>
            {
                Assert.Equal(0, team.TeamId);
                Assert.Equal(FrontlineTeamOutcome.Win, team.Outcome);
            },
            team =>
            {
                Assert.Equal(1, team.TeamId);
                Assert.Equal(FrontlineTeamOutcome.Loss, team.Outcome);
            });
    }

    [Fact]
    public void MaxTickScore_AtNeutralControl_IsDraw()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1);
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveObjective(rules));

        FrontlineStepResult finalTick =
            Step(session, BotAction.Wait, BotAction.Wait);

        Assert.Equal(FrontlineMatchEndReason.MaxTicks, finalTick.Result?.Reason);
        Assert.Null(finalTick.Result?.WinnerTeamId);
        Assert.Equal(0L, finalTick.Result?.TerritorialScore);
        Assert.All(
            finalTick.Result!.Teams,
            team => Assert.Equal(FrontlineTeamOutcome.Draw, team.Outcome));
    }

    [Fact]
    public void Objective_UsesPostDamageSurvivorsOnTheKillTick()
    {
        GameRules baseRules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 20,
            captureThreshold: 1,
            redeployPauseTicks: 0);
        FrontlineRules frontline = baseRules.Frontline!;
        GameRules rules = baseRules with
        {
            MaxHealth = 1,
            DamagePerHit = 1,
            Frontline = frontline with
            {
                PrimeForm = frontline.PrimeForm with { MaxHealth = 1 },
            },
        };
        var session = new FrontlineMatchSession(
            MatchDefinitionResolver.Resolve(rules, TwoTileCentreMap()));

        Step(session, BotAction.MoveForward, BotAction.MoveForward);
        FrontlineStepResult contested =
            Step(session, BotAction.MoveForward, BotAction.MoveForward);

        Assert.Null(contested.Control.ClaimingTeamId);
        Assert.Equal(0, contested.Control.CaptureProgress);

        FrontlineStepResult killAndCapture =
            Step(session, BotAction.Shoot, BotAction.Wait);

        Assert.Null(session.State.GetUnit(1, 0).ActiveLife);
        Assert.Equal(2, killAndCapture.Control.ActivePositionIndex);
        int damageIndex = EventIndex(
            killAndCapture,
            FrontlineMatchEventType.Damage);
        int destroyedIndex = EventIndex(
            killAndCapture,
            FrontlineMatchEventType.Destroyed);
        int captureIndex = EventIndex(
            killAndCapture,
            FrontlineMatchEventType.FrontlinePositionAdvanced);
        Assert.True(damageIndex < destroyedIndex);
        Assert.True(destroyedIndex < captureIndex);
        FrontlineMatchEvent shot = Assert.Single(
            killAndCapture.Events,
            gameEvent => gameEvent.Type == FrontlineMatchEventType.Shot);
        FrontlineMatchEvent damage = Assert.Single(
            killAndCapture.Events,
            gameEvent => gameEvent.Type == FrontlineMatchEventType.Damage);
        Assert.NotNull(shot.ProjectileId);
        Assert.Equal(shot.ProjectileId, damage.ProjectileId);
    }

    private static FrontlineStepResult Step(
        FrontlineMatchSession session,
        BotAction team0,
        BotAction team1)
    {
        FrontlineTickStart tickStart = session.PrepareTick();
        var decisions = tickStart.ActiveActors.ToDictionary(
            actorId => actorId,
            actorId => BotDecision.Of(actorId.TeamId == 0 ? team0 : team1));
        return session.Step(decisions);
    }

    private static int EventIndex(
        FrontlineStepResult result,
        FrontlineMatchEventType type) =>
        result.Events
            .Select((gameEvent, index) => (gameEvent, index))
            .Single(item => item.gameEvent.Type == type)
            .index;

    private static ArenaMap TwoTileCentreMap() => ArenaMap.FromJson("""
        {
          "formatVersion": 2,
          "id": "frontline-test-two-tile-centre",
          "version": 1,
          "width": 8,
          "height": 5,
          "tiles": [
            "########",
            "#......#",
            "#......#",
            "#......#",
            "########"
          ],
          "spawns": [
            { "teamId": 0, "x": 1, "y": 2, "facing": "East" },
            { "teamId": 1, "x": 6, "y": 2, "facing": "West" }
          ],
          "frontline": {
            "positions": [
              { "tiles": [[2,1]] },
              { "tiles": [[3,2], [4,2]] },
              { "tiles": [[5,1]] }
            ],
            "homePads": [
              { "teamId": 0, "tiles": [[1,2]] },
              { "teamId": 1, "tiles": [[6,2]] }
            ],
            "anchorForbiddenTiles": [
              [1,1], [2,1], [3,1], [4,1], [5,1], [6,1],
              [1,2], [2,2], [3,2], [4,2], [5,2], [6,2],
              [1,3], [2,3], [3,3], [4,3], [5,3], [6,3]
            ]
          }
        }
        """);
}
