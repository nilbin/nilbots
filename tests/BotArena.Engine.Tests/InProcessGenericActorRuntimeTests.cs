using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using BotArena.Runtime;
using Engine = BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Engine.Tests;

public sealed class InProcessGenericActorRuntimeTests
{
    [Fact]
    public void ExecuteTick_MapsIndependentEngineAndSdkContracts()
    {
        Engine.ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        var actorId = new Engine.ActorIdentity(0, 0, 0);
        var bot = new CapturingBot();
        using var factory =
            new InProcessGenericActorRuntimeFactory(() => bot);
        using Engine.IGenericActorRuntime runtime =
            factory.CreateRuntime();

        runtime.StartLife(Start(contract, actorId, seed: 719));
        Engine.GenericActorRuntimeDecision decision =
            runtime.ExecuteTick(Observation(contract, actorId));

        Assert.NotNull(bot.Start);
        Assert.Equal(
            Engine.BotArenaVersions.GenericActorRuntimeContractVersion,
            bot.Start.RuntimeContractVersion);
        Assert.Equal("deathmatch-sdk-contract", bot.Start.Contract.Rules.RulesetId);
        Assert.Equal(new Sdk.ActorIdentity(0, 0, 0), bot.Start.ActorId);
        Assert.Equal(
            Engine.ActorContractFingerprint.ComputeMatch(contract),
            bot.Start.Contract.MatchContractFingerprint);

        Assert.NotNull(bot.Context);
        Assert.Equal([10, 20], bot.Context.Participants
            .Select(participant => participant.ParticipantId));
        Assert.Equal([0, 1], bot.Context.Scoreboard.Teams
            .Select(team => team.TeamId));
        Assert.Equal(["wait", "shoot"], bot.Context.ActionLegalities
            .Select(action => action.ActionId));
        Assert.InRange(bot.RandomValue, 0, 10_000);

        Assert.Equal("custom-action", decision.ActionId);
        Assert.Equal(77, decision.ActionCode);
        Assert.Equal("returned\ncollected-7", decision.DebugMessage);
        Assert.Collection(
            decision.Arguments,
            argument => Assert.Equal(
                Engine.ShotProgram.Straight,
                Assert.IsType<
                    Engine.GenericActorRuntimeActionArgument.ShotProgramArgument>(
                        argument).Value),
            argument => Assert.Equal(
                Engine.Direction.West,
                Assert.IsType<
                    Engine.GenericActorRuntimeActionArgument.DirectionArgument>(
                        argument).Value),
            argument => Assert.Equal(
                new Engine.GenericActorRuntimeActionArgument.UnitTarget(0, 1),
                Assert.IsType<
                    Engine.GenericActorRuntimeActionArgument.UnitTargetArgument>(
                        argument).Value),
            argument => Assert.Equal(
                "turret",
                Assert.IsType<
                    Engine.GenericActorRuntimeActionArgument.FormTargetArgument>(
                        argument).FormId),
            argument => Assert.Equal(
                Engine.ProjectileHeading.NorthEast,
                Assert.IsType<
                    Engine.GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument>(argument).Value));
    }

    [Fact]
    public void Runtime_RequiresOneStartAndFactoryCreatesFreshInstances()
    {
        Engine.ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        var actorId = new Engine.ActorIdentity(0, 0, 0);
        using var factory = new InProcessGenericActorRuntimeFactory(
            () => new CapturingBot());
        using Engine.IGenericActorRuntime first = factory.CreateRuntime();
        using Engine.IGenericActorRuntime second = factory.CreateRuntime();

        Assert.NotSame(first, second);
        Assert.Throws<InvalidOperationException>(
            () => first.ExecuteTick(null!));

        Engine.GenericActorRuntimeStart start =
            Start(contract, actorId, seed: 17);
        first.StartLife(start);
        Assert.Throws<InvalidOperationException>(
            () => first.StartLife(start));
    }

    [Fact]
    public void DebugOutput_IsBoundedAtFourKiBUtf8()
    {
        Engine.ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        var actorId = new Engine.ActorIdentity(0, 0, 0);
        using var factory = new InProcessGenericActorRuntimeFactory(
            () => new VerboseBot());
        using Engine.IGenericActorRuntime runtime = factory.CreateRuntime();
        runtime.StartLife(Start(contract, actorId, seed: 8));

        Engine.GenericActorRuntimeDecision decision =
            runtime.ExecuteTick(Observation(contract, actorId));

        Assert.NotNull(decision.DebugMessage);
        Assert.InRange(
            Encoding.UTF8.GetByteCount(decision.DebugMessage),
            1,
            4096);
        Assert.StartsWith("returned\n🙂", decision.DebugMessage);
        Assert.DoesNotContain('\uFFFD', decision.DebugMessage);
    }

    [Fact]
    public void DynamicDtoUnions_StayInLockstepWithSdk()
    {
        AssertCasesEqual(
            typeof(Engine.GenericActorRuntimeActionArgument),
            typeof(Sdk.GenericActorActionArgument));
        AssertCasesEqual(
            typeof(Engine.GenericActorRuntimeActionLegality.ArgumentConstraint),
            typeof(Sdk.GenericActorActionLegality.ArgumentConstraint));
        AssertCasesEqual(
            typeof(Engine.GenericActorRuntimeObservation.UnitSlotState),
            typeof(Sdk.GenericActorContext.UnitSlotState));
        AssertCasesEqual(
            typeof(Engine.GenericActorRuntimeObservation.EventPayload),
            typeof(Sdk.GenericActorContext.EventPayload));
        AssertCasesEqual(
            typeof(Engine.GenericActorRuntimeObservation.ModeObservationState),
            typeof(Sdk.GenericActorContext.ModeObservationState));
    }

    private static Engine.GenericActorRuntimeStart Start(
        Engine.ActorResolvedMatchDefinition contract,
        Engine.ActorIdentity actorId,
        ulong seed) =>
        new()
        {
            SchemaVersion =
                Engine.BotArenaVersions.GenericActorMatchStartSchemaVersion,
            RuntimeContractVersion =
                Engine.BotArenaVersions.GenericActorRuntimeContractVersion,
            ActorId = actorId,
            ParticipantId = 10,
            ActorRandomSeed = seed,
            Origin = new Engine.GenericActorRuntimeStart.LifeOrigin(
                Engine.GenericActorRuntimeStart.SpawnReason.Initial,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            Contract = contract,
        };

    private static Engine.GenericActorRuntimeObservation Observation(
        Engine.ActorResolvedMatchDefinition contract,
        Engine.ActorIdentity actorId)
    {
        Engine.GenericActorRuntimeActionLegality wait = new(
            "wait",
            0,
            AllowedByForm: true,
            Available: true,
            []);
        Engine.GenericActorRuntimeActionLegality shoot = new(
            "shoot",
            4,
            AllowedByForm: true,
            Available: true,
            [
                new Engine.GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ShotProgramConstraint(true),
            ]);
        return new Engine.GenericActorRuntimeObservation(
            Engine.BotArenaVersions.GenericActorObservationSchemaVersion,
            Tick: 0,
            Engine.ActorContractFingerprint.ComputeMatch(contract),
            new Engine.GenericActorRuntimeObservation.ObservedSelfState(
                actorId,
                Generation: 0,
                FormId: "mobile",
                new Engine.Position(1, 1),
                Engine.Direction.East,
                Health: 3,
                Cooldown: 0,
                Energy: 10,
                PreviousActionResolution: null,
                PendingSameLifeTransition: null),
            [
                new(
                    0,
                    0,
                    new Engine.GenericActorRuntimeObservation.UnitSlotState.Active(
                        actorId,
                        Generation: 0,
                        FormId: "mobile")),
            ],
            [
                new(
                    ParticipantId: 20,
                    TeamId: 1,
                    RuntimeFaultCount: 0,
                    Disqualified: false),
                new(
                    ParticipantId: 10,
                    TeamId: 0,
                    RuntimeFaultCount: 0,
                    Disqualified: false),
            ],
            [],
            [
                new(
                    new Engine.ActorIdentity(1, 0, 0),
                    FormId: "mobile",
                    new Engine.Position(4, 1),
                    Engine.Direction.West,
                    Health: 3,
                    PendingSameLifeTransition: null,
                    [actorId]),
            ],
            [
                new(
                    new Engine.Position(1, 1),
                    IsWall: false,
                    [actorId]),
            ],
            [],
            [
                new(
                    EventHandle: "event-0",
                    SourceTick: 0,
                    SourceOrdinal: 0,
                    Engine.GenericActorRuntimeObservation.EventKind.ScoreChanged,
                    new Engine.GenericActorRuntimeObservation.EventPayload.ScoreChanged(
                        TeamId: 0,
                        Channel: "kills",
                        NewValue: 0),
                    [actorId]),
            ],
            [],
            new Engine.GenericActorRuntimeObservation.ScoreboardState(
                [
                    new(
                        TeamId: 1,
                        Eligible: true,
                        [new("kills", 0)]),
                    new(
                        TeamId: 0,
                        Eligible: true,
                        [new("kills", 0)]),
                ]),
            new Engine.GenericActorRuntimeObservation.ModeObservationState.Deathmatch(
                "deathmatch"),
            [shoot, wait]);
    }

    private static void AssertCasesEqual(Type engineBase, Type sdkBase)
    {
        string[] engineCases = ConcreteNestedCases(engineBase);
        string[] sdkCases = ConcreteNestedCases(sdkBase);
        Assert.Equal(sdkCases, engineCases);
    }

    private static string[] ConcreteNestedCases(Type baseType) =>
        baseType
            .GetNestedTypes(BindingFlags.Public)
            .Where(type =>
                !type.IsAbstract
                && baseType.IsAssignableFrom(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private sealed class CapturingBot : Sdk.IGenericActorBot
    {
        public Sdk.GenericActorMatchStart Start { get; private set; } = null!;
        public Sdk.GenericActorContext Context { get; private set; } = null!;
        public int RandomValue { get; private set; }

        public void StartLife(Sdk.GenericActorMatchStart start)
        {
            Start = start;
        }

        public Sdk.GenericActorDecision Tick(
            Sdk.GenericActorContext context)
        {
            Context = context;
            RandomValue = context.Random.NextInt(0, 10_000);
            context.Debug.Write("collected-{0}", 7);
            return new Sdk.GenericActorDecision(
                "custom-action",
                77,
                [
                    new Sdk.GenericActorActionArgument.FormTargetArgument(
                        "turret"),
                    new Sdk.GenericActorActionArgument
                        .ProjectileHeadingArgument(
                            Sdk.ProjectileHeading.NorthEast),
                    new Sdk.GenericActorActionArgument.UnitTargetArgument(
                        new Sdk.GenericActorActionArgument.UnitTarget(0, 1)),
                    new Sdk.GenericActorActionArgument.DirectionArgument(
                        Sdk.Direction.West),
                    new Sdk.GenericActorActionArgument.ShotProgramArgument(
                        Sdk.ShotProgram.Straight),
                ],
                "returned");
        }
    }

    private sealed class VerboseBot : Sdk.IGenericActorBot
    {
        public Sdk.GenericActorDecision Tick(
            Sdk.GenericActorContext context)
        {
            context.Debug.Write(string.Concat(
                Enumerable.Repeat("🙂", 2000)));
            context.Debug.Write(new string('x', 10_000));
            return Sdk.GenericActorDecision.WithoutArguments(
                "wait",
                0,
                "returned");
        }
    }
}
