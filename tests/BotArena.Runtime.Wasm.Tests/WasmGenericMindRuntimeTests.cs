using System.Security.Cryptography;
using BotArena.Runtime.Wasm;
using Engine = BotArena.Engine;

namespace BotArena.Runtime.Wasm.Tests;

public sealed class WasmGenericMindRuntimeTests
{
    [Fact]
    public void OneStoreDrivesTheWholeMatchAcrossDeathAndReturn()
    {
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.Happy();
        byte[] captured = File.ReadAllBytes(artifact.Path);
        string expectedHash =
            Convert.ToHexStringLower(SHA256.HashData(captured));
        using var factory = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions
            {
                ModulePath = artifact.Path,
                BotName = "mind-probe",
                TickTimeoutMs = 5_000,
            });
        // Prove the factory captured the bytes rather than holding the path.
        File.WriteAllBytes(artifact.Path, [0, 1, 2, 3]);

        Engine.ActorResolvedMatchDefinition contract =
            GenericMindWasmTestFixture.Contract();
        using var mind = Assert.IsType<WasmGenericMindRuntime>(
            factory.CreateRuntime());
        mind.StartMatch(GenericMindWasmTestFixture.Start(contract));

        var accepted = new List<string>();
        for (int tick = 0; tick < 6; tick++)
        {
            Engine.GenericMindRuntimeDecisions decisions = mind.ExecuteTick(
                GenericMindWasmTestFixture.Observation(contract, tick));
            accepted.Add(
                $"{tick}:"
                + string.Join(
                    ",",
                    decisions.Commands.Select(command =>
                        $"{command.UnitId}/{command.LifeId}/"
                        + $"{command.ActionId}/{command.RoleTag}")));
        }

        Assert.Equal(expectedHash, factory.ArtifactHash);
        // One instance, one mind object, six ticks — three of them with the
        // whole army dead. Nothing was disposed and nothing was re-created,
        // which is the entire topology change.
        Assert.Equal(
            [
                "0:0/0/wait/channeler,1/0/wait/screen",
                "1:",
                "2:0/0/wait/channeler,1/0/wait/screen",
                "3:",
                "4:0/0/wait/channeler,1/0/wait/screen",
                "5:",
            ],
            accepted);
    }

    [Fact]
    public void TheTickBudgetIsBasePlusPerBodyAndIsRefilledOnlyByObservations()
    {
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.Happy();
        using var factory = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions
            {
                ModulePath = artifact.Path,
                TickTimeoutMs = 5_000,
            });
        Engine.ActorResolvedMatchDefinition contract =
            GenericMindWasmTestFixture.Contract();
        using var mind = Assert.IsType<WasmGenericMindRuntime>(
            factory.CreateRuntime());
        mind.StartMatch(GenericMindWasmTestFixture.Start(contract));

        // Two live bodies: 250M + 2 x 200M.
        mind.ExecuteTick(
            GenericMindWasmTestFixture.Observation(contract, tick: 0));
        Assert.Equal(2, mind.LastLiveBodyCount);
        Assert.Equal(650_000_000UL, mind.LastTickFuelBudget);
        Assert.InRange(
            mind.LastFuelRemaining,
            1UL,
            mind.LastTickFuelBudget - 1);

        // Zero live bodies: the base term alone, and it is genuinely granted —
        // this is what makes the "ticks with no bodies" invariant affordable
        // rather than a subsidy the guest cannot actually spend.
        mind.ExecuteTick(
            GenericMindWasmTestFixture.Observation(contract, tick: 1));
        Assert.Equal(0, mind.LastLiveBodyCount);
        Assert.Equal(250_000_000UL, mind.LastTickFuelBudget);
        Assert.InRange(mind.LastFuelRemaining, 1UL, 250_000_000UL - 1);

        // Back to a full roster: the budget tracks the work rather than the
        // roster arm, so it rises again.
        mind.ExecuteTick(
            GenericMindWasmTestFixture.Observation(contract, tick: 2));
        Assert.Equal(650_000_000UL, mind.LastTickFuelBudget);
        Assert.InRange(mind.MaxFuelUsedPerTick, 1UL, 650_000_000UL);

        // The per-body term is EXACTLY the per-life budget, which is what keeps
        // a comparison between the profiles from being confounded by compute.
        Assert.Equal(
            (ulong)Engine.GenericMindTickBudget.PerBodyTickFuel,
            200_000_000UL);
        Assert.Equal(
            (ulong)Engine.GenericMindTickBudget.BaseTickFuel,
            250_000_000UL);
    }

    [Fact]
    public void ConfigurationTwoPinsTheStartupPoolAndTheMemoryCeiling()
    {
        var options = new WasmMindRuntimeOptions { ModulePath = "unused" };

        Assert.Equal(5_000_000_000UL, options.StartupFuel);
        Assert.Equal(128L * 1024 * 1024, options.MaxMemoryBytes);
        Assert.Equal(16_384u, options.MaxTableElements);
        Assert.Equal(30_000, options.TickTimeoutMs);
        Assert.Equal(250_000_000UL, options.TickFuel(0));
        Assert.Equal(2_050_000_000UL, options.TickFuel(9));
    }

    [Fact]
    public void AnArtifactDemandingMoreThanTheMemoryCeilingIsRefusedOutright()
    {
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.OversizedMemory();

        // 2049 pages: one page past 128 MiB. The ceiling is enforced when the
        // artifact is captured and compiled, so a greedy mind never reaches
        // tick 0 rather than dying somewhere inside a match.
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => new WasmGenericMindRuntimeFactory(
                new WasmMindRuntimeOptions { ModulePath = artifact.Path }));
        Assert.Contains("128 MiB", error.Message, StringComparison.Ordinal);

        // And the per-life ceiling is NOT what the mind is measured against:
        // configuration 2.0 doubled it, and the same artifact under 64 MiB
        // would have been refused a page earlier.
        Assert.Equal(
            64L * 1024 * 1024,
            WasmArtifactValidator.MaxInitialMemoryBytes);
        Assert.Equal(
            128L * 1024 * 1024,
            WasmArtifactValidator.MaxMindInitialMemoryBytes);
    }

    [Fact]
    public void FuelExhaustionFaultsTheTickAndNamesTheReason()
    {
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.FuelHog();
        using var factory = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions
            {
                ModulePath = artifact.Path,
                // A small budget so the spin loop trips deterministically
                // without burning a real 650M on a test.
                BaseTickFuel = 1_000_000,
                PerBodyTickFuel = 0,
                TickTimeoutMs = 10_000,
            });
        Engine.ActorResolvedMatchDefinition contract =
            GenericMindWasmTestFixture.Contract();
        Engine.IGenericMindRuntime mind = factory.CreateRuntime();
        mind.StartMatch(GenericMindWasmTestFixture.Start(contract));

        // The tick throws, which is what the coordinator turns into a
        // participant-scoped fault. Under the shipped allowance of zero that
        // disqualifies the participant and every slot it owns.
        Assert.ThrowsAny<Exception>(
            () => mind.ExecuteTick(
                GenericMindWasmTestFixture.Observation(contract, tick: 0)));
        mind.Dispose();

        WasmGenericMindRuntimeFactory.RuntimeDiagnostic diagnostic =
            Assert.Single(factory.Diagnostics);
        Assert.Equal(10, diagnostic.ParticipantId);
        Assert.Equal("Fuel limit exceeded.", diagnostic.FailureReason);
        factory.Dispose();
    }

    [Fact]
    public void ReadyMustAttestTheMindTupleAndNotThePerLifeOne()
    {
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.WrongSchemaReady();
        using var factory = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions
            {
                ModulePath = artifact.Path,
                TickTimeoutMs = 5_000,
            });
        using Engine.IGenericMindRuntime mind = factory.CreateRuntime();

        // An artifact compiled against a single-decision reply cannot answer a
        // mind observation at all. Refusing it at Ready is the whole reason
        // attestation exists.
        FormatException error = Assert.Throws<FormatException>(
            () => mind.StartMatch(
                GenericMindWasmTestFixture.Start(
                    GenericMindWasmTestFixture.Contract())));
        Assert.Contains(
            "does not attest the exact mind contract profile",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AReplyForAnotherTickFailsTheExchange()
    {
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.StaleTickReply();
        using var factory = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions
            {
                ModulePath = artifact.Path,
                TickTimeoutMs = 5_000,
            });
        Engine.ActorResolvedMatchDefinition contract =
            GenericMindWasmTestFixture.Contract();
        using Engine.IGenericMindRuntime mind = factory.CreateRuntime();
        mind.StartMatch(GenericMindWasmTestFixture.Start(contract));

        // Tick 0 is answered correctly by a reply baked for tick 0.
        mind.ExecuteTick(
            GenericMindWasmTestFixture.Observation(contract, tick: 0));

        // Tick 2 gets the same stale bytes back. Under a correlated
        // request/reply protocol that is not a late answer, it is a broken
        // guest.
        FormatException error = Assert.Throws<FormatException>(
            () => mind.ExecuteTick(
                GenericMindWasmTestFixture.Observation(contract, tick: 2)));
        Assert.Contains(
            "while tick 2 was outstanding",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheFactoryRefusesDisposalUntilEveryMindIsDisposed()
    {
        using GenericMindWasmTestFixture.TemporaryArtifact artifact =
            GenericMindWasmTestFixture.Happy();
        var factory = new WasmGenericMindRuntimeFactory(
            new WasmMindRuntimeOptions { ModulePath = artifact.Path });
        Engine.IGenericMindRuntime mind = factory.CreateRuntime();

        try
        {
            Assert.Throws<InvalidOperationException>(() => factory.Dispose());
        }
        finally
        {
            mind.Dispose();
            factory.Dispose();
        }

        Assert.Throws<ObjectDisposedException>(() => factory.CreateRuntime());
    }
}
