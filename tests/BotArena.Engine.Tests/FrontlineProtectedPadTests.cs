using BotArena.Engine;
using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public sealed class FrontlineProtectedPadTests
{
    [Fact]
    public void EnemyGroundMovement_CannotEnterProtectedPad()
    {
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen());

        Step(session, BotAction.MoveForward, BotAction.TurnRight);
        Step(session, BotAction.MoveForward, BotAction.MoveForward);
        Step(session, BotAction.MoveForward, BotAction.Wait);
        Step(session, BotAction.MoveForward, BotAction.Wait);
        Step(session, BotAction.MoveForward, BotAction.Wait);
        FrontlineStepResult blocked =
            Step(session, BotAction.MoveForward, BotAction.Wait);

        FrontlineActionResolution resolution = blocked.ActionResolutions
            .Single(action => action.ActorId.TeamId == 0);
        Assert.Equal(ActionResult.Blocked, resolution.Result);
        Assert.Equal(
            new Position(6, 2),
            session.State.GetUnit(0, 0).ActiveLife?.Position);
        Assert.Contains(
            blocked.Events,
            gameEvent =>
                gameEvent.Type == FrontlineMatchEventType.MoveBlocked
                && gameEvent.ActorId?.TeamId == 0
                && gameEvent.To == new Position(7, 2));
    }

    [Fact]
    public void ProtectedPad_GrantsNoProjectileImmunity()
    {
        var session = new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen());

        for (int tick = 0; tick < 5; tick++)
            Step(session, BotAction.MoveForward, BotAction.Wait);

        FrontlineStepResult shot =
            Step(session, BotAction.Shoot, BotAction.Wait);

        Assert.Equal(
            2,
            session.State.GetUnit(1, 0).ActiveLife?.Health);
        Assert.Contains(
            shot.Events,
            gameEvent =>
                gameEvent.Type == FrontlineMatchEventType.Damage
                && gameEvent.ActorId?.TeamId == 1
                && gameEvent.To == new Position(7, 2));
    }

    private static FrontlineStepResult Step(
        FrontlineMatchSession session,
        BotAction team0,
        BotAction team1)
    {
        FrontlineTickStart tickStart = session.PrepareTick();
        return session.Step(tickStart.ActiveActors.ToDictionary(
            actorId => actorId,
            actorId => BotDecision.Of(actorId.TeamId == 0 ? team0 : team1)));
    }
}
