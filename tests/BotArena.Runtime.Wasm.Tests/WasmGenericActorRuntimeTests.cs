using System.Security.Cryptography;
using BotArena.Runtime.Wasm;
using Engine = BotArena.Engine;

namespace BotArena.Runtime.Wasm.Tests;

public sealed class WasmGenericActorRuntimeTests
{
    [Fact]
    public void CapturedArtifactRunsWithPerLifeStateAndPerTickFuel()
    {
        using GenericActorWasmTestFixture.TemporaryArtifact artifact =
            GenericActorWasmTestFixture.CreateHappyArtifact();
        byte[] capturedBytes = File.ReadAllBytes(artifact.Path);
        string expectedHash = Convert.ToHexStringLower(
            SHA256.HashData(capturedBytes));
        const ulong fuelPerTick = 1_000_000;
        using var factory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions
            {
                ModulePath = artifact.Path,
                BotName = "generic-probe",
                FuelPerTick = fuelPerTick,
                TickTimeoutMs = 1_000,
            });
        File.WriteAllBytes(artifact.Path, [0, 1, 2, 3]);

        using var first =
            Assert.IsType<WasmGenericActorRuntime>(
                factory.CreateRuntime());
        using var second =
            Assert.IsType<WasmGenericActorRuntime>(
                factory.CreateRuntime());
        Engine.ActorResolvedMatchDefinition contract =
            GenericActorWasmTestFixture.Contract();
        first.StartLife(
            GenericActorWasmTestFixture.Start(
                contract,
                teamId: 0,
                seed: 100));
        second.StartLife(
            GenericActorWasmTestFixture.Start(
                contract,
                teamId: 1,
                seed: 200));

        Engine.GenericActorRuntimeDecision firstTick =
            first.ExecuteTick(
                GenericActorWasmTestFixture.Observation(
                    contract,
                    teamId: 0,
                    tick: 0));
        Engine.GenericActorRuntimeDecision secondTick =
            first.ExecuteTick(
                GenericActorWasmTestFixture.Observation(
                    contract,
                    teamId: 0,
                    tick: 1));
        Engine.GenericActorRuntimeDecision otherLifeFirstTick =
            second.ExecuteTick(
                GenericActorWasmTestFixture.Observation(
                    contract,
                    teamId: 1,
                    tick: 0));

        Assert.Equal(expectedHash, factory.ArtifactHash);
        Assert.Equal("calls=1", firstTick.DebugMessage);
        Assert.Equal("calls=2", secondTick.DebugMessage);
        Assert.Equal("calls=1", otherLifeFirstTick.DebugMessage);
        Assert.InRange(first.LastFuelRemaining, 1UL, fuelPerTick - 1);
        Assert.InRange(second.LastFuelRemaining, 1UL, fuelPerTick - 1);
        Assert.InRange(first.MaxFuelUsedPerTick, 1UL, fuelPerTick);
        Assert.InRange(second.MaxFuelUsedPerTick, 1UL, fuelPerTick);
    }

    [Fact]
    public void FactoryRefusesDisposalUntilEveryLifeIsDisposed()
    {
        using GenericActorWasmTestFixture.TemporaryArtifact artifact =
            GenericActorWasmTestFixture.CreateHappyArtifact();
        var factory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions { ModulePath = artifact.Path });
        Engine.IGenericActorRuntime runtime = factory.CreateRuntime();

        try
        {
            Assert.Throws<InvalidOperationException>(
                () => factory.Dispose());
        }
        finally
        {
            runtime.Dispose();
            factory.Dispose();
        }

        Assert.Throws<ObjectDisposedException>(
            () => factory.CreateRuntime());
    }

    [Fact]
    public void UnsolicitedDuplicateReplyPermanentlyFaultsTheLife()
    {
        using GenericActorWasmTestFixture.TemporaryArtifact artifact =
            GenericActorWasmTestFixture.CreateDoubleReplyArtifact();
        using var factory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions
            {
                ModulePath = artifact.Path,
                TickTimeoutMs = 1_000,
            });
        using Engine.IGenericActorRuntime runtime =
            factory.CreateRuntime();

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(
                () => runtime.StartLife(
                    GenericActorWasmTestFixture.Start(
                        GenericActorWasmTestFixture.Contract(),
                        teamId: 0)));

        Assert.Contains(
            "unsolicited or duplicate",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FuelExhaustionPermanentlyFaultsTheLife()
    {
        using GenericActorWasmTestFixture.TemporaryArtifact artifact =
            GenericActorWasmTestFixture.CreateFuelHogArtifact();
        using var factory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions
            {
                ModulePath = artifact.Path,
                FuelPerTick = 10_000,
                TickTimeoutMs = 1_000,
            });
        using Engine.IGenericActorRuntime runtime =
            factory.CreateRuntime();
        Engine.ActorResolvedMatchDefinition contract =
            GenericActorWasmTestFixture.Contract();
        runtime.StartLife(
            GenericActorWasmTestFixture.Start(
                contract,
                teamId: 0));

        InvalidOperationException first =
            Assert.Throws<InvalidOperationException>(
                () => runtime.ExecuteTick(
                    GenericActorWasmTestFixture.Observation(
                        contract,
                        teamId: 0,
                        tick: 0)));
        InvalidOperationException later =
            Assert.Throws<InvalidOperationException>(
                () => runtime.ExecuteTick(
                    GenericActorWasmTestFixture.Observation(
                        contract,
                        teamId: 0,
                        tick: 1)));

        Assert.Contains("Fuel", first.Message, StringComparison.Ordinal);
        Assert.Contains("Fuel", later.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WallClockTimeoutPermanentlyFaultsTheLife()
    {
        using GenericActorWasmTestFixture.TemporaryArtifact artifact =
            GenericActorWasmTestFixture.CreateFuelHogArtifact();
        using var factory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions
            {
                ModulePath = artifact.Path,
                FuelPerTick = ulong.MaxValue,
                TickTimeoutMs = 1_000,
            });
        using Engine.IGenericActorRuntime runtime =
            factory.CreateRuntime();
        Engine.ActorResolvedMatchDefinition contract =
            GenericActorWasmTestFixture.Contract();
        runtime.StartLife(
            GenericActorWasmTestFixture.Start(
                contract,
                teamId: 0));

        TimeoutException first =
            Assert.Throws<TimeoutException>(
                () => runtime.ExecuteTick(
                    GenericActorWasmTestFixture.Observation(
                        contract,
                        teamId: 0,
                        tick: 0)));
        InvalidOperationException later =
            Assert.Throws<InvalidOperationException>(
                () => runtime.ExecuteTick(
                    GenericActorWasmTestFixture.Observation(
                        contract,
                        teamId: 0,
                        tick: 1)));

        Assert.Contains(
            "wall-clock message timeout",
            first.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "wall-clock message timeout",
            later.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WrongTickReplyPermanentlyFaultsTheLife()
    {
        using GenericActorWasmTestFixture.TemporaryArtifact artifact =
            GenericActorWasmTestFixture.CreateWrongTickReplyArtifact();
        using var factory = new WasmGenericActorRuntimeFactory(
            new WasmRuntimeOptions
            {
                ModulePath = artifact.Path,
                TickTimeoutMs = 1_000,
            });
        using Engine.IGenericActorRuntime runtime =
            factory.CreateRuntime();
        Engine.ActorResolvedMatchDefinition contract =
            GenericActorWasmTestFixture.Contract();
        runtime.StartLife(
            GenericActorWasmTestFixture.Start(
                contract,
                teamId: 0));

        Assert.Throws<FormatException>(
            () => runtime.ExecuteTick(
                GenericActorWasmTestFixture.Observation(
                    contract,
                    teamId: 0,
                    tick: 0)));
        InvalidOperationException later =
            Assert.Throws<InvalidOperationException>(
                () => runtime.ExecuteTick(
                    GenericActorWasmTestFixture.Observation(
                        contract,
                        teamId: 0,
                        tick: 1)));

        Assert.Contains(
            "invalid terminal reply",
            later.Message,
            StringComparison.Ordinal);
    }
}
