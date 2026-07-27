namespace BotArena.Engine.Tests;

public sealed class DeathmatchActorMatchModeDriverTests
{
    [Fact]
    public void DriverOwnsOrderedScoreDeltasProjectionAndTypedCompletion()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    KillsToWin = 1,
                });
        var driver = new DeathmatchActorMatchModeDriver(
            definition.Topology,
            (DeathmatchGameModeDefinition)definition.Rules.GameMode);
        var world = new GenericActorModeWorldView(
            new Dictionary<int, long>
            {
                [1] = 0,
                [0] = 3,
            },
            [1, 0],
            [
                new GenericActorModeActiveLife(
                    new ActorIdentity(0, 0, 0),
                    "mobile",
                    new Position(1, 1),
                    health: 3),
            ]);

        GenericActorModeTickResult tick = driver.ApplyJointTick(
            world,
            new GenericActorModeTickInput(
                tick: 0,
                [
                    new GenericActorModeDamageContact(
                        sourceTeamId: 0,
                        targetTeamId: 1,
                        actualHealthRemoved: 1,
                        causedDestruction: true),
                ]));

        Assert.True(tick.ModeObjectiveReached);
        Assert.Null(tick.ModeChange);
        Assert.Collection(
            tick.ScoreChanges,
            change =>
            {
                Assert.Equal(0, change.TeamId);
                Assert.Equal("kills", change.Channel);
                Assert.Equal(1, change.NewValue);
            },
            change =>
            {
                Assert.Equal(0, change.TeamId);
                Assert.Equal("damage-dealt", change.Channel);
                Assert.Equal(1, change.NewValue);
            },
            change =>
            {
                Assert.Equal(1, change.TeamId);
                Assert.Equal("deaths", change.Channel);
                Assert.Equal(1, change.NewValue);
            });
        Assert.DoesNotContain(
            tick.ScoreChanges,
            change => change.Channel == "active-health");

        GenericActorModeProjection projection = driver.Project(world);
        Assert.IsType<
            GenericActorRuntimeObservation.ModeObservationState.Deathmatch>(
            projection.Mode);
        Assert.Equal(
            0,
            projection.Scoreboard.Teams
                .Single(team => team.TeamId == 1)
                .Scores
                .Single(score => score.Channel == "active-health")
                .Value);

        var completion =
            Assert.IsType<GenericActorModeCompletion.Deathmatch>(
                driver.ResolveCompletion(
                    GenericActorModeCompletionKind.ModeObjective,
                    endTick: 0,
                    world));
        Assert.Equal("kill-limit", completion.CompletionReason);
        Assert.Equal(
            GenericDeathmatchEndReason.KillLimit,
            completion.CompatibilityResult.Reason);
        Assert.Equal(
            completion.CompatibilityResult.Scores,
            Assert.IsType<GenericActorMatchModeResult.Deathmatch>(
                    completion.ModeResult)
                .Scores);
    }
}
