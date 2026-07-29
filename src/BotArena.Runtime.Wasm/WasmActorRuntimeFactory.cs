using System.Security.Cryptography;
using BotArena.Engine;
using Wasmtime;
using WasmtimeEngine = Wasmtime.Engine;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// Match-scoped owner of one compiled WASM module. Every active body life gets
/// an independent <see cref="Store"/>, instance, thread, deterministic shims,
/// and private memory from <see cref="CreateRuntime"/>.
/// </summary>
public sealed class WasmActorRuntimeFactory : IActorRuntimeFactory
{
    private readonly object _gate = new();
    private readonly WasmRuntimeOptions _options;
    private WasmtimeEngine? _engine;
    private Module? _module;
    private int _activeRuntimes;

    /// <summary>
    /// SHA-256 of the exact bytes captured, validated, and compiled by this
    /// factory. Replay provenance should use this value rather than rereading
    /// a path that another process could replace.
    /// </summary>
    public string ArtifactHash { get; }

    public WasmActorRuntimeFactory(WasmRuntimeOptions options)
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

    public IActorRuntime CreateRuntime()
    {
        lock (_gate)
        {
            if (_engine is null || _module is null)
                throw new ObjectDisposedException(nameof(WasmActorRuntimeFactory));
            _activeRuntimes++;
            return new WasmActorRuntime(
                _engine,
                _module,
                _options,
                ReleaseRuntime);
        }
    }

    private void ReleaseRuntime()
    {
        lock (_gate)
            _activeRuntimes--;
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
                    "Dispose every actor-life runtime before its WASM artifact factory.");
            }
            _module?.Dispose();
            _engine.Dispose();
            _module = null;
            _engine = null;
        }
    }
}
