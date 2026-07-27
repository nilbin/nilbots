using BotArena.Engine;
using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class FrontlineRespawnTests
{
    [Fact]
    public void SimultaneousPrimeDeaths_QueueBothRespawnsWithoutEndingMatch()
    {
        const int respawnTicks = 3;
        var session = CreateLethalSession(respawnTicks);
        FrontlineActorId[] initialActors =
            session.PrepareTick().ActiveActors.ToArray();

        FrontlineStepResult destruction = session.Step(
            ShootDecisions(initialActors));

        Assert.Equal(0, destruction.Tick);
        Assert.False(destruction.MatchCompleted);
        Assert.Null(destruction.Result);
        Assert.False(session.IsCompleted);
        Assert.Null(session.Result);
        Assert.Equal(
            initialActors,
            destruction.Events
                .Where(matchEvent =>
                    matchEvent.Type == FrontlineMatchEventType.Destroyed)
                .Select(matchEvent => matchEvent.ActorId!.Value)
                .ToArray());
        foreach (FrontlineTeamState team in session.State.Teams)
        {
            FrontlineUnitState prime = Assert.Single(team.Units);
            Assert.Equal(
                FrontlineLifecycleStatus.Respawning,
                prime.LifecycleStatus);
            Assert.Null(prime.ActiveLife);
            Assert.Equal(1 + respawnTicks, prime.RespawnAtTick);
        }

        FrontlineTickStart nextTick = session.PrepareTick();
        Assert.Equal(1, nextTick.Tick);
        Assert.Empty(nextTick.ActiveActors);
        Assert.Empty(nextTick.RespawnedActors);
        Assert.Empty(nextTick.Events);
    }

    [Fact]
    public void DestroyedPrime_HasExactlyNEmptyTicksThenCleanLifeCanAct()
    {
        const int destructionTick = 2;
        const int respawnTicks = 3;
        int respawnTick = destructionTick + respawnTicks + 1;
        var session = CreateLethalSession(respawnTicks);

        StepBoth(session, BotAction.Wait, BotAction.Wait);
        StepBoth(session, BotAction.Wait, BotAction.Wait);
        FrontlineStepResult destruction =
            StepBoth(session, BotAction.Shoot, BotAction.Shoot);

        Assert.Equal(destructionTick, destruction.Tick);
        Assert.Equal(respawnTick,
            session.State.GetUnit(0, 0).RespawnAtTick);
        Assert.Equal(respawnTick,
            session.State.GetUnit(1, 0).RespawnAtTick);

        for (int absentTick = destructionTick + 1;
             absentTick <= destructionTick + respawnTicks;
             absentTick++)
        {
            FrontlineTickStart emptyStart = session.PrepareTick();
            Assert.Equal(absentTick, emptyStart.Tick);
            Assert.Empty(emptyStart.ActiveActors);
            Assert.Empty(emptyStart.RespawnedActors);
            Assert.Empty(emptyStart.Events);

            FrontlineStepResult emptyStep = session.Step(
                new Dictionary<FrontlineActorId, BotDecision>());

            Assert.Equal(absentTick, emptyStep.Tick);
            Assert.Empty(emptyStep.ActionResolutions);
            Assert.Equal(absentTick + 1, session.State.Tick);
            Assert.Equal(respawnTick,
                session.State.GetUnit(0, 0).RespawnAtTick);
            Assert.Equal(respawnTick,
                session.State.GetUnit(1, 0).RespawnAtTick);
        }

        FrontlineTickStart respawn = session.PrepareTick();
        var team0Life1 = new FrontlineActorId(0, 0, 1);
        var team1Life1 = new FrontlineActorId(1, 0, 1);

        Assert.Equal(respawnTick, respawn.Tick);
        Assert.Equal(
            [team0Life1, team1Life1],
            respawn.ActiveActors.ToArray());
        Assert.Equal(
            [team0Life1, team1Life1],
            respawn.RespawnedActors.ToArray());
        Assert.Equal(
            [team0Life1, team1Life1],
            respawn.Events
                .Select(matchEvent => matchEvent.ActorId!.Value)
                .ToArray());
        Assert.All(
            respawn.Events,
            matchEvent => Assert.Equal(
                FrontlineMatchEventType.Respawned,
                matchEvent.Type));
        Assert.Same(respawn, session.PrepareTick());

        AssertCleanRespawn(
            session,
            team0Life1,
            new Position(1, 2),
            Direction.East,
            respawnTick);
        AssertCleanRespawn(
            session,
            team1Life1,
            new Position(7, 2),
            Direction.West,
            respawnTick);

        FrontlineStepResult immediateAction = session.Step(
            new Dictionary<FrontlineActorId, BotDecision>
            {
                [team1Life1] = BotDecision.Of(BotAction.TurnRight),
                [team0Life1] = BotDecision.Of(BotAction.TurnLeft),
            });

        Assert.Equal(respawnTick, immediateAction.Tick);
        Assert.All(
            immediateAction.ActionResolutions,
            resolution => Assert.Equal(
                ActionResult.Success,
                resolution.Result));
        Assert.DoesNotContain(
            immediateAction.Events,
            matchEvent =>
                matchEvent.Type == FrontlineMatchEventType.Respawned);
        Assert.Equal(
            Direction.North,
            session.State.GetActiveLife(team0Life1).Facing);
        Assert.Equal(
            Direction.North,
            session.State.GetActiveLife(team1Life1).Facing);
    }

    [Fact]
    public void RepeatedRespawns_MonotonicallyAdvanceLifeIdentity()
    {
        const int respawnTicks = 1;
        var session = CreateLethalSession(respawnTicks);
        var observedLives = new List<FrontlineActorId[]>();

        FrontlineTickStart life0 = session.PrepareTick();
        observedLives.Add(life0.ActiveActors.ToArray());
        session.Step(ShootDecisions(life0.ActiveActors));
        StepEmptyTick(session, expectedTick: 1);

        FrontlineTickStart life1 = session.PrepareTick();
        observedLives.Add(life1.ActiveActors.ToArray());
        session.Step(ShootDecisions(life1.ActiveActors));
        StepEmptyTick(session, expectedTick: 3);

        FrontlineTickStart life2 = session.PrepareTick();
        observedLives.Add(life2.ActiveActors.ToArray());

        Assert.Equal(
            [
                new FrontlineActorId(0, 0, 0),
                new FrontlineActorId(1, 0, 0),
            ],
            observedLives[0]);
        Assert.Equal(
            [
                new FrontlineActorId(0, 0, 1),
                new FrontlineActorId(1, 0, 1),
            ],
            observedLives[1]);
        Assert.Equal(
            [
                new FrontlineActorId(0, 0, 2),
                new FrontlineActorId(1, 0, 2),
            ],
            observedLives[2]);
        Assert.Equal(3, session.State.GetUnit(0, 0).NextLifeId);
        Assert.Equal(3, session.State.GetUnit(1, 0).NextLifeId);
        Assert.Throws<KeyNotFoundException>(() =>
            session.State.GetActiveLife(observedLives[0][0]));
        Assert.Throws<KeyNotFoundException>(() =>
            session.State.GetActiveLife(observedLives[1][0]));
        Assert.Equal(
            observedLives[2][0],
            session.State.GetActiveLife(observedLives[2][0]).ActorId);
    }

    private static FrontlineMatchSession CreateLethalSession(
        int respawnTicks)
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 100,
            primeRespawnTicks: respawnTicks) with
        {
            DamagePerHit = 3,
            ProgrammedShotLaunchTiles = 8,
        };
        return new FrontlineMatchSession(
            FrontlineTestDefinitions.ResolveOpen(rules));
    }

    private static FrontlineStepResult StepBoth(
        FrontlineMatchSession session,
        BotAction team0Action,
        BotAction team1Action)
    {
        FrontlineTickStart start = session.PrepareTick();
        Assert.Equal(2, start.ActiveActors.Count);
        return session.Step(
            new Dictionary<FrontlineActorId, BotDecision>
            {
                [start.ActiveActors[1]] = BotDecision.Of(team1Action),
                [start.ActiveActors[0]] = BotDecision.Of(team0Action),
            });
    }

    private static IReadOnlyDictionary<FrontlineActorId, BotDecision>
        ShootDecisions(IReadOnlyList<FrontlineActorId> actors) =>
        actors.ToDictionary(
            actor => actor,
            _ => BotDecision.Of(BotAction.Shoot));

    private static void StepEmptyTick(
        FrontlineMatchSession session,
        int expectedTick)
    {
        FrontlineTickStart start = session.PrepareTick();
        Assert.Equal(expectedTick, start.Tick);
        Assert.Empty(start.ActiveActors);
        session.Step(new Dictionary<FrontlineActorId, BotDecision>());
    }

    private static void AssertCleanRespawn(
        FrontlineMatchSession session,
        FrontlineActorId actorId,
        Position position,
        Direction facing,
        int spawnedAtTick)
    {
        FrontlineUnitState unit =
            session.State.GetUnit(actorId.TeamId, actorId.UnitId);
        FrontlineLifeState life =
            session.State.GetActiveLife(actorId);

        Assert.Equal(FrontlineLifecycleStatus.Active, unit.LifecycleStatus);
        Assert.Null(unit.RespawnAtTick);
        Assert.Equal(actorId.LifeId + 1, unit.NextLifeId);
        Assert.Equal(actorId, life.ActorId);
        Assert.Equal(position, life.Position);
        Assert.Equal(facing, life.Facing);
        Assert.Equal(3, life.Health);
        Assert.Equal(0, life.Cooldown);
        Assert.Equal(0, life.Energy);
        Assert.Equal(0, life.DamageDealt);
        Assert.Equal(ActionResult.None, life.LastActionResult);
        Assert.Equal(spawnedAtTick, life.SpawnedAtTick);
    }
}
