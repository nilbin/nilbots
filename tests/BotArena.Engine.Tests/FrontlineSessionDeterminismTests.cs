using System.Text.Json;
using BotArena.Engine;
using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public sealed class FrontlineSessionDeterminismTests
{
    [Fact]
    public void EquivalentJointActions_ProduceIdenticalLifecycleTrace()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            maxTicks: 12,
            primeRespawnTicks: 2,
            shootCooldownTicks: 0) with
        {
            DamagePerHit = 3,
            ProgrammedShotLaunchTiles = 8,
        };
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen(rules);
        var ascending = new FrontlineMatchSession(definition);
        var descending = new FrontlineMatchSession(definition);
        var ascendingTrace = new List<string>();
        var descendingTrace = new List<string>();

        while (!ascending.IsCompleted)
        {
            Assert.False(descending.IsCompleted);
            ascendingTrace.Add(StepAndSnapshot(
                ascending,
                descendingKeys: false));
            descendingTrace.Add(StepAndSnapshot(
                descending,
                descendingKeys: true));
        }

        Assert.True(descending.IsCompleted);
        Assert.Equal(ascendingTrace, descendingTrace);
        Assert.Equal(
            JsonSerializer.Serialize(ascending.Result),
            JsonSerializer.Serialize(descending.Result));
    }

    private static string StepAndSnapshot(
        FrontlineMatchSession session,
        bool descendingKeys)
    {
        FrontlineTickStart tickStart = session.PrepareTick();
        IEnumerable<FrontlineActorId> orderedActors = descendingKeys
            ? tickStart.ActiveActors.OrderByDescending(actorId => actorId)
            : tickStart.ActiveActors.OrderBy(actorId => actorId);
        Dictionary<FrontlineActorId, BotDecision> decisions =
            orderedActors.ToDictionary(
                actorId => actorId,
                _ => BotDecision.Of(BotAction.Shoot));
        FrontlineStepResult step = session.Step(decisions);

        return JsonSerializer.Serialize(new
        {
            Step = step,
            State = new
            {
                session.State.Tick,
                session.State.Control,
                Teams = session.State.Teams.Select(team => new
                {
                    team.TeamId,
                    team.DamageDealt,
                    Units = team.Units.Select(unit => new
                    {
                        unit.UnitId,
                        unit.FormId,
                        unit.LifecycleStatus,
                        unit.RespawnAtTick,
                        unit.NextLifeId,
                        unit.DamageDealt,
                        unit.ActiveLife,
                    }),
                }),
                Projectiles = session.State.Projectiles,
                session.State.NextProjectileId,
                session.State.Result,
            },
        });
    }
}
