using System.Security.Cryptography;
using System.Collections.Immutable;
using BotArena.Engine;
using Wasmtime;
using WasmtimeEngine = Wasmtime.Engine;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Match-scoped owner of one compiled generic actor WASM module. Every active
/// life receives an independent Store, instance, thread, deterministic shims,
/// linear memory, and protocol state.
/// </summary>
public sealed class WasmGenericActorRuntimeFactory
    : IGenericActorRuntimeFactory
{
    private readonly object _gate = new();
    private readonly WasmRuntimeOptions _options;
    private WasmtimeEngine? _engine;
    private Module? _module;
    private int _activeRuntimes;
    private readonly List<RuntimeDiagnostic> _diagnostics = [];

    /// <summary>
    /// SHA-256 of the exact bytes captured, validated, and compiled by this
    /// factory.
    /// </summary>
    public string ArtifactHash { get; }

    /// <summary>
    /// Completed life-runtime diagnostics. Hosted callers may ignore these;
    /// local authoring tools can expose the precise sandbox failure and peak
    /// fuel use without weakening public replay fault redaction.
    /// </summary>
    public ImmutableArray<RuntimeDiagnostic> Diagnostics
    {
        get
        {
            lock (_gate)
                return _diagnostics.ToImmutableArray();
        }
    }

    public WasmGenericActorRuntimeFactory(WasmRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        byte[] artifactBytes =
            WasmArtifactValidator.ReadArtifact(options.ModulePath);
        WasmArtifactValidator.ValidateBinaryEnvelope(artifactBytes);
        ArtifactHash = Convert.ToHexStringLower(
            SHA256.HashData(artifactBytes));
        var engine = new WasmtimeEngine(
            new Config()
                .WithFuelConsumption(true)
                .WithEpochInterruption(true));
        Module? module = null;
        try
        {
            module = WasmModuleCache.LoadOrCompile(
                engine,
                Path.GetFileName(options.ModulePath),
                artifactBytes,
                ArtifactHash,
                configTag: "fuel-epoch");
            WasmArtifactValidator.Validate(module, artifactBytes);
            _engine = engine;
            _module = module;
        }
        catch
        {
            module?.Dispose();
            engine.Dispose();
            throw;
        }
    }

    public IGenericActorRuntime CreateRuntime()
    {
        lock (_gate)
        {
            if (_engine is null || _module is null)
            {
                throw new ObjectDisposedException(
                    nameof(WasmGenericActorRuntimeFactory));
            }
            _activeRuntimes++;
            return new WasmGenericActorRuntime(
                _engine,
                _module,
                _options,
                ReleaseRuntime);
        }
    }

    private void ReleaseRuntime(RuntimeDiagnostic diagnostic)
    {
        lock (_gate)
        {
            _diagnostics.Add(diagnostic);
            _activeRuntimes--;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_engine is null)
                return;
            if (_activeRuntimes != 0)
            {
                throw new InvalidOperationException(
                    "Dispose every generic actor-life runtime before its " +
                    "WASM artifact factory.");
            }
            _module?.Dispose();
            _engine.Dispose();
            _module = null;
            _engine = null;
        }
    }

    public sealed record RuntimeDiagnostic(
        ActorIdentity? ActorId,
        ulong MaxFuelUsedPerTick,
        ulong FuelPerTick,
        string? FailureReason);
}
