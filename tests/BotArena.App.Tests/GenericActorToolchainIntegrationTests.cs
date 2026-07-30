using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.Runtime.Wasm;
using BotArena.Toolchain;
using BotArena.Engine.Tests;
using Engine = BotArena.Engine;

namespace BotArena.App.Tests;

public sealed class GenericActorToolchainIntegrationTests
{
    [SkippableFact]
    public void ControlledBuilder_CompilesAndAdmitsGenericActorArtifact()
    {
        Skip.If(
            Environment.GetEnvironmentVariable(
                "BOTARENA_GENERIC_WASM_SMOKE") != "1",
            "Set BOTARENA_GENERIC_WASM_SMOKE=1 to run the NativeAOT generic profile gate.");

        BuiltBot built = BotBuilder.BuildFromSources(
            [
                new SourceFile(
                    "GenericSmokeBot.cs",
                    """
                    using BotArena.Sdk;

                    public sealed class GenericSmokeBot : IGenericActorBot
                    {
                        public GenericActorDecision Tick(
                            GenericActorContext context) =>
                            GenericActorDecision.WithoutArguments("wait", 0);
                    }
                    """),
            ],
            "GenericSmokeBot",
            "generic actor NativeAOT smoke",
            noCache: false,
            quiet: true);

        WasmArtifactValidation metadata =
            WasmArtifactValidator.Validate(built.WasmPath);

        Assert.Equal(
            built.ArtifactHash,
            BotBuilder.Sha256File(built.WasmPath));
        Assert.InRange(
            new FileInfo(built.WasmPath).Length,
            1,
            BotBuilder.MaxArtifactBytes);
        Assert.NotNull(metadata);

        var profileProbe = new SubmissionContractProfileProbe(
            MatchExecutionSettings.FromEnvironment());
        SubmissionContractProfileProbe.Result detected =
            profileProbe.Probe(built.WasmPath);
        Assert.True(
            detected.SupportedContractProfiles.SequenceEqual(
                [Engine.BotArenaVersions.GenericActorContractProfileId]),
            detected.FailureSummary);

        Engine.ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        using var factory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions
            {
                ModulePath = built.WasmPath,
                BotName = "generic actor NativeAOT smoke",
            });
        using Engine.IGenericActorRuntime runtime =
            factory.CreateRuntime();
        Engine.GenericActorRuntimeStart start = Start(contract);
        runtime.StartLife(start);

        Engine.GenericActorRuntimeDecision decision =
            runtime.ExecuteTick(Observation(contract, start));

        Assert.Equal("wait", decision.ActionId);
        Assert.Equal(0, decision.ActionCode);
        Assert.Empty(decision.Arguments);
    }

    private static Engine.GenericActorRuntimeStart Start(
        Engine.ActorResolvedMatchDefinition contract) =>
        new()
        {
            SchemaVersion =
                Engine.BotArenaVersions.GenericActorMatchStartSchemaVersion,
            RuntimeContractVersion =
                Engine.BotArenaVersions.GenericActorRuntimeContractVersion,
            ActorId = new Engine.ActorIdentity(0, 0, 0),
            ParticipantId = 10,
            ActorRandomSeed = ulong.MaxValue,
            TeamRandomSeed = ulong.MaxValue - 1,
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
        Engine.GenericActorRuntimeStart start) =>
        new(
            Engine.BotArenaVersions.GenericActorObservationSchemaVersion,
            Tick: 0,
            Engine.ActorContractFingerprint.ComputeMatch(contract),
            new Engine.GenericActorRuntimeObservation.ObservedSelfState(
                start.ActorId,
                Generation: 0,
                FormId: "mobile",
                new Engine.Position(1, 3),
                Engine.Direction.East,
                Health: 3,
                Cooldown: 0,
                Energy: 10,
                PreviousActionResolution: null,
                PendingSameLifeTransition: null),
            [
                new(
                    TeamId: 0,
                    UnitId: 0,
                    new Engine.GenericActorRuntimeObservation.UnitSlotState
                        .Active(
                            start.ActorId,
                            Generation: 0,
                            FormId: "mobile")),
            ],
            [
                new(
                    ParticipantId: 10,
                    TeamId: 0,
                    RuntimeFaultCount: 0,
                    Disqualified: false),
                new(
                    ParticipantId: 20,
                    TeamId: 1,
                    RuntimeFaultCount: 0,
                    Disqualified: false),
            ],
            [],
            [],
            [],
            [],
            [],
            [],
            new Engine.GenericActorRuntimeObservation.ScoreboardState(
                [
                    new(
                        TeamId: 0,
                        Eligible: true,
                        [new("kills", 0)]),
                    new(
                        TeamId: 1,
                        Eligible: true,
                        [new("kills", 0)]),
                ]),
            new Engine.GenericActorRuntimeObservation.ModeObservationState
                .Deathmatch("deathmatch"),
            [
                new(
                    "wait",
                    0,
                    AllowedByForm: true,
                    Available: true,
                    []),
            ]);
}
