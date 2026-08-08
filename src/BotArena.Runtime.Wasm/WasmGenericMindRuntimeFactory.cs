using System.Collections.Immutable;
using System.Security.Cryptography;
using BotArena.Engine;
using Wasmtime;
using WasmtimeEngine = Wasmtime.Engine;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Match-scoped owner of one compiled mind WASM module. It shares only
/// immutable compiled code; every runtime it creates owns its own Store,
/// instance, thread, deterministic shims, linear memory and protocol state.
///
/// <para>The contrast with the per-life factory beside it is the whole
/// operational win: that one creates a runtime per life — at a full roster,
/// several Stores and several guest threads per participant, re-created on
/// every respawn, each paying the startup fuel again. This one creates exactly
/// ONE per participant per match.</para>
/// </summary>
public sealed class WasmGenericMindRuntimeFactory : IGenericMindRuntimeFactory
{
    private readonly object _gate = new();
    private readonly WasmMindRuntimeOptions _options;
    private WasmtimeEngine? _engine;
    private Module? _module;
    private int _activeRuntimes;
    private readonly List<RuntimeDiagnostic> _diagnostics = [];

    /// <summary>
    /// SHA-256 of the exact bytes captured, validated and compiled by this
    /// factory.
    /// </summary>
    public string ArtifactHash { get; }

    /// <summary>
    /// Completed mind-runtime diagnostics. Hosted callers may ignore these;
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

    public WasmGenericMindRuntimeFactory(WasmMindRuntimeOptions options)
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
            // Configuration 2.0's ceiling, not 1.0's: a mind may declare up to
            // 128 MiB of initial linear memory.
            WasmArtifactValidator.Validate(
                module,
                artifactBytes,
                options.MaxMemoryBytes);
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

    public IGenericMindRuntime CreateRuntime()
    {
        lock (_gate)
        {
            if (_engine is null || _module is null)
            {
                throw new ObjectDisposedException(
                    nameof(WasmGenericMindRuntimeFactory));
            }
            _activeRuntimes++;
            return new WasmGenericMindRuntime(
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
                    "Dispose every mind runtime before its WASM artifact "
                    + "factory.");
            }
            _module?.Dispose();
            _engine.Dispose();
            _module = null;
            _engine = null;
        }
    }

    /// <param name="ParticipantId">
    /// The mind's participant, or null if it never started.
    /// </param>
    /// <param name="MaxFuelUsedPerTick">Peak single-tick consumption.</param>
    /// <param name="LastTickFuelBudget">
    /// The budget of the final tick — a FUNCTION of that tick's live body
    /// count, not a constant, so a run's headroom is only meaningful beside it.
    /// </param>
    /// <param name="FailureReason">The precise sandbox failure, if any.</param>
    public sealed record RuntimeDiagnostic(
        int? ParticipantId,
        ulong MaxFuelUsedPerTick,
        ulong LastTickFuelBudget,
        string? FailureReason);
}
