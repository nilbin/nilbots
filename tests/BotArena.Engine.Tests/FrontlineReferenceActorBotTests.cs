using BotArena.Bots.BuiltIn;
using BotArena.Engine.Tests.Support;
using BotArena.Runtime;

namespace BotArena.Engine.Tests;

public sealed class FrontlineReferenceActorBotTests
{
    [Fact]
    public void Catalog_ExposesFourDistinctReferenceDoctrinesAndMetadata()
    {
        string[] doctrines =
        [
            "frontline-rusher",
            "frontline-swarm",
            "frontline-bastion",
            "frontline-counterpunch",
        ];

        Assert.All(
            doctrines,
            name =>
            {
                Assert.Contains(name, BuiltInActorBotCatalog.Names);
                Assert.NotNull(BuiltInActorBotCatalog.Create(name));
                Assert.NotEmpty(BuiltInActorBotCatalog.Accent(name));
                Assert.NotEmpty(BuiltInActorBotCatalog.Look(name));
                Assert.NotEmpty(BuiltInActorBotCatalog.ProjectileLook(name));
                Assert.NotEmpty(BuiltInActorBotCatalog.Describe(name));
            });
        Assert.Equal(
            doctrines.Length,
            doctrines
                .Select(name => BuiltInActorBotCatalog.Create(name).GetType())
                .Distinct()
                .Count());
        Assert.Throws<ArgumentException>(
            () => BuiltInActorBotCatalog.Create("unknown"));
    }

    [Fact]
    public void Rusher_AppliesMobileObjectivePressureWithoutFabricating()
    {
        FrontlineActorMatchRunResult run = Run(
            "frontline-rusher",
            "frontline-counterpunch",
            FrontlineTestDefinitions.ObjectiveMapV2(),
            LowVisionPrimeRules());

        ReplayV2ActorTurn[] turns = TeamTurns(run, teamId: 0);
        Assert.Contains(
            turns,
            turn => turn.AcceptedDecision.ActionId
                == PublicActionIds.MoveForward);
        Assert.DoesNotContain(
            turns,
            turn => turn.AcceptedDecision.ActionId
                is PublicActionIds.Fabricate or PublicActionIds.Transform);
        Assert.Contains(
            run.Replay.Ticks.SelectMany(tick => tick.Resolution.Events),
            value => value.TeamId == 0
                && value.Type
                    == FrontlineMatchEventType.FrontlineProgressChanged);
    }

    [Fact]
    public void Swarm_FabricatesEveryChildAndKeepsThemMobile()
    {
        FrontlineActorMatchRunResult run = Run(
            "frontline-swarm",
            "frontline-rusher",
            FrontlineTestDefinitions.ReplicationMapV2(),
            FrontlineTestDefinitions.ReplicationRules(maxTicks: 10));

        ReplayV2ActorTurn[] turns = TeamTurns(run, teamId: 0);
        Assert.Equal(
            2,
            turns.Count(turn => turn.AcceptedDecision.ActionId
                == PublicActionIds.Fabricate));
        Assert.Equal(
            [0, 1, 2],
            turns.Select(turn => turn.ActorId.UnitId)
                .Distinct()
                .Order()
                .ToArray());
        Assert.Contains(
            turns,
            turn => turn.ActorId.UnitId > 0
                && turn.AcceptedDecision.ActionId
                    == PublicActionIds.MoveForward);
        Assert.DoesNotContain(
            turns,
            turn => turn.AcceptedDecision.ActionId
                == PublicActionIds.Transform);
    }

    [Fact]
    public void Bastion_AnchorsFabricatedChildrenAndUsesDirectionalFire()
    {
        GameRules baseline =
            FrontlineTestDefinitions.ReplicationRules(maxTicks: 40);
        GameRules rules = baseline with
        {
            Frontline = baseline.Frontline! with
            {
                AnchorWindupTicks = 1,
            },
        };

        FrontlineActorMatchRunResult run = Run(
            "frontline-bastion",
            "frontline-rusher",
            FrontlineTestDefinitions.AnchorMapV2(),
            rules);

        ReplayV2ActorTurn[] turns = TeamTurns(run, teamId: 0);
        Assert.Equal(
            2,
            turns.Count(turn => turn.AcceptedDecision.ActionId
                == PublicActionIds.Fabricate));
        Assert.Contains(
            turns,
            turn => turn.ActorId.UnitId > 0
                && turn.AcceptedDecision.ActionId
                    == PublicActionIds.Transform);
        Assert.Contains(
            turns,
            turn => turn.ActorId.UnitId > 0
                && turn.AcceptedDecision.ActionId
                    == PublicActionIds.ShootDirection);
        Assert.Contains(
            run.Replay.Ticks.SelectMany(tick => tick.Resolution.Events),
            value => value.TeamId == 0
                && value.Type == FrontlineMatchEventType.FormChanged
                && value.ToFormId == "turret");
    }

    [Fact]
    public void Counterpunch_HoldsOwnSideUntilVisualContactThenEngages()
    {
        FrontlineActorMatchRunResult run = Run(
            "frontline-counterpunch",
            "frontline-rusher",
            FrontlineTestDefinitions.ObjectiveMapV2(),
            LowVisionPrimeRules());

        ReplayV2ActorTurn[] turns = TeamTurns(run, teamId: 0);
        Assert.Contains(
            turns,
            turn => turn.Observation.Enemies.Length == 0
                && turn.AcceptedDecision.ActionId == PublicActionIds.Wait);
        Assert.Contains(
            turns,
            turn => turn.Observation.Enemies.Length > 0
                && turn.AcceptedDecision.ActionId
                    is PublicActionIds.MoveForward
                    or PublicActionIds.TurnLeft
                    or PublicActionIds.TurnRight
                    or PublicActionIds.Shoot);
        Assert.DoesNotContain(
            turns,
            turn => turn.AcceptedDecision.ActionId
                == PublicActionIds.Transform);
    }

    [Fact]
    public void Counterpunch_UsesOnlyItsDesignatedSupportSlot()
    {
        FrontlineActorMatchRunResult run = Run(
            "frontline-counterpunch",
            "frontline-rusher",
            FrontlineTestDefinitions.ReplicationMapV2(),
            FrontlineTestDefinitions.ReplicationRules(maxTicks: 20));

        ReplayV2ActorTurn[] turns = TeamTurns(run, teamId: 0);
        ReplayV2ActorTurn[] fabrications = turns
            .Where(turn => turn.AcceptedDecision.ActionId
                == PublicActionIds.Fabricate)
            .ToArray();
        Assert.NotEmpty(fabrications);
        Assert.All(
            fabrications,
            fabrication => Assert.Equal(
                1,
                fabrication.AcceptedDecision.Payload?.UnitTarget?.UnitId));
        Assert.Contains(turns, turn => turn.ActorId.UnitId == 1);
        Assert.DoesNotContain(turns, turn => turn.ActorId.UnitId == 2);
    }

    private static FrontlineActorMatchRunResult Run(
        string teamZeroName,
        string teamOneName,
        ArenaMap map,
        GameRules rules)
    {
        using var teamZero = new InProcessActorRuntimeFactory(
            () => BuiltInActorBotCatalog.Create(teamZeroName));
        using var teamOne = new InProcessActorRuntimeFactory(
            () => BuiltInActorBotCatalog.Create(teamOneName));
        return new FrontlineActorMatchEngine().Run(new()
        {
            Map = map,
            Rules = rules,
            Seed = 42,
            Participants =
            [
                Participant(0, 0, teamZeroName, teamZero),
                Participant(1, 1, teamOneName, teamOne),
            ],
        });
    }

    private static GameRules LowVisionPrimeRules()
    {
        GameRules baseline =
            FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 20);
        return baseline with
        {
            Frontline = baseline.Frontline! with
            {
                PrimeForm = baseline.Frontline.PrimeForm with
                {
                    VisionRange = 2,
                },
            },
        };
    }

    private static ActorParticipantConfiguration Participant(
        int participantId,
        int teamId,
        string name,
        IActorRuntimeFactory factory) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = name,
            RuntimeFactory = factory,
            RuntimeKind = "in-process-reference",
            ArtifactHash = $"builtin:{name}",
            Accent = BuiltInActorBotCatalog.Accent(name),
            LookId = BuiltInActorBotCatalog.Look(name),
            ProjectileLookId = BuiltInActorBotCatalog.ProjectileLook(name),
        };

    private static ReplayV2ActorTurn[] TeamTurns(
        FrontlineActorMatchRunResult run,
        int teamId) =>
        run.Replay.Ticks
            .SelectMany(tick => tick.Actors)
            .Where(turn => turn.ActorId.TeamId == teamId)
            .ToArray();
}
