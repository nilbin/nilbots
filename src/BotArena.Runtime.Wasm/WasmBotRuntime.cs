using System.Collections.Concurrent;
using System.Text;
using BotArena.Engine;
using Wasmtime;
using WasmtimeEngine = Wasmtime.Engine;

namespace BotArena.Runtime.Wasm;

/// <summary>Runtime limits enforced on every WASM bot (plan §10, §18). These values are part
/// of the runtime configuration version.</summary>
public sealed record WasmRuntimeOptions
{
    public const uint DefaultMaxTableElements = 16 * 1024;

    public required string ModulePath { get; init; }

    /// <summary>Guest-side bot selection passed in the init line (built-in artifacts host
    /// several bots; player artifacts ignore it).</summary>
    public string BotName { get; init; } = "";

    /// <summary>Fuel budget granted at the start of every tick. Unused fuel does not carry over.</summary>
    public ulong FuelPerTick { get; init; } = 200_000_000;

    /// <summary>One-time budget for runtime startup + match initialization.</summary>
    public ulong StartupFuel { get; init; } = 5_000_000_000;

    public long MaxMemoryBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// Actor-path ceiling for indirect-function table growth. Linear-memory
    /// limits do not account for host allocations backing WebAssembly tables.
    /// </summary>
    public uint MaxTableElements { get; init; } =
        DefaultMaxTableElements;

    /// <summary>Wall-clock backstop only; fuel is the deterministic limit. A tick that hits
    /// this without exhausting fuel is treated as a permanent runtime failure.</summary>
    public int TickTimeoutMs { get; init; } = 30_000;
}

/// <summary>
/// The canonical bot runtime (plan §3, §17): executes a compiled WASM artifact in Wasmtime
/// with fuel metering, a fixed memory ceiling, no WASI capabilities beyond stdio plumbing,
/// and trap isolation. The guest runs `_start` on a dedicated thread and exchanges
/// observation/decision lines with the engine thread through two host functions:
///   botarena::next_observation(ptr, cap) -> len   (0 ends the match loop)
///   botarena::post_decision(ptr, len)
/// A trap, fuel exhaustion, memory overrun, or protocol violation permanently faults the
/// instance; every subsequent tick then reports a fault and the engine's fault rules apply.
/// </summary>
public sealed class WasmBotRuntime : IBotRuntime
{
    private readonly WasmRuntimeOptions _options;

    private WasmtimeEngine? _engine;
    private Module? _module;
    private Store? _store;
    private Thread? _guestThread;
    private BlockingCollection<string>? _toGuest;
    private BlockingCollection<string>? _fromGuest;
    private CancellationTokenSource? _guestDead;
    private string _deathReason = "";

    public WasmBotRuntime(WasmRuntimeOptions options) => _options = options;

    /// <summary>Fuel left after the most recent tick — surfaced for diagnostics/metrics.</summary>
    public ulong LastFuelRemaining { get; private set; }

    /// <summary>Peak fuel consumed in a single tick this match (gen-3 finding: fuel DQs
    /// were undiagnosable — this lets the CLI report how close a bot runs to the limit).</summary>
    public ulong MaxFuelUsedPerTick { get; private set; }

    public void StartMatch(BotMatchStart start)
    {
        CleanUp();
        MaxFuelUsedPerTick = 0;
        _guestDead = new CancellationTokenSource();
        _toGuest = new BlockingCollection<string>(boundedCapacity: 1);
        _fromGuest = new BlockingCollection<string>(boundedCapacity: 1);

        _engine = new WasmtimeEngine(new Config().WithFuelConsumption(true));
        _module = Module.FromFile(_engine, _options.ModulePath);
        _store = new Store(_engine);
        _store.SetLimits(memorySize: _options.MaxMemoryBytes);
        // No preopened directories, no environment, no inherited stdio: the guest gets
        // nothing but the args WASI requires to boot (plan §18).
        _store.SetWasiConfiguration(new WasiConfiguration().WithArgs("bot.wasm"));
        _store.Fuel = _options.StartupFuel;

        var linker = new Linker(_engine) { AllowShadowing = true };
        linker.DefineWasi();
        linker.DefineFunction("botarena", "next_observation",
            (Caller caller, int pointer, int capacity) => NextObservation(caller, pointer, capacity));
        linker.DefineFunction("botarena", "post_decision",
            (Caller caller, int pointer, int length) => PostDecision(caller, pointer, length));
        DefineDeterministicWasiShims(linker, start.BotRandomSeed);

        var instance = linker.Instantiate(_store, _module);
        var startFunction = instance.GetAction("_start")
            ?? throw new InvalidOperationException($"Artifact '{_options.ModulePath}' does not export _start.");

        _guestThread = new Thread(() => RunGuest(startFunction)) { IsBackground = true, Name = "wasm-bot-guest" };
        _guestThread.Start();

        string reply = Exchange(WasmProtocol.FormatInit(start, _options.BotName))
            ?? throw new InvalidOperationException($"WASM guest failed during startup: {_deathReason}");
        if (!reply.StartsWith("R ", StringComparison.Ordinal))
            throw new InvalidOperationException($"WASM guest handshake failed, got: '{reply}'");
    }

    public BotDecision ExecuteTick(BotObservation observation)
    {
        string? reply = Exchange(WasmProtocol.FormatObservation(observation));
        if (reply is null)
            return BotDecision.Fault(_deathReason);
        LastFuelRemaining = _store?.Fuel ?? 0;
        ulong used = _options.FuelPerTick > LastFuelRemaining
            ? _options.FuelPerTick - LastFuelRemaining
            : 0;
        if (used > MaxFuelUsedPerTick)
            MaxFuelUsedPerTick = used;
        return WasmProtocol.ParseReply(reply);
    }

    /// <summary>Sends one line to the guest and waits for its reply; null means the guest is dead.</summary>
    private string? Exchange(string line)
    {
        if (_guestDead is null || _toGuest is null || _fromGuest is null)
            throw new InvalidOperationException("StartMatch must be called before ExecuteTick.");
        if (_guestDead.IsCancellationRequested)
            return null;
        try
        {
            _toGuest.Add(line, _guestDead.Token);
            if (_fromGuest.TryTake(out string? reply, _options.TickTimeoutMs, _guestDead.Token))
                return reply;
            MarkDead("WASM guest exceeded the wall-clock tick timeout.");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private void RunGuest(Action startFunction)
    {
        try
        {
            startFunction();
            MarkDead("WASM guest exited before the match completed.");
        }
        catch (TrapException ex)
        {
            MarkDead(ex.Type switch
            {
                TrapCode.OutOfFuel => "Fuel limit exceeded.",
                _ => $"WASM trap: {ex.Type}.",
            });
        }
        catch (WasmtimeException ex)
        {
            MarkDead($"WASM runtime failure: {ex.Message}");
        }
        catch (Exception ex)
        {
            MarkDead($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private void MarkDead(string reason)
    {
        if (_guestDead is { IsCancellationRequested: false })
        {
            _deathReason = reason;
            _guestDead.Cancel();
        }
    }

    /// <summary>
    /// Replaces the ambient-nondeterminism WASI imports with deterministic versions
    /// (plan §5, §18: no host clock, no OS entropy). The .NET runtime inside the guest
    /// boots via these (hash seeds, timers), and a bot calling DateTime.UtcNow or
    /// Random.Shared gets deterministic values instead of real ones.
    /// - clock_time_get: a logical clock advancing 1ms per call, never wall time.
    /// - random_get: a SplitMix64 stream derived from the bot seed — stable per
    ///   match/slot/rules version, unrelated to the SDK's IBotRandom stream.
    /// </summary>
    private void DefineDeterministicWasiShims(Linker linker, ulong botSeed)
    {
        long logicalNanos = 0;
        var entropy = new DeterministicRandom(DeterministicRandom.Mix(botSeed ^ 0xB07A_5EED_C10C_0FF5UL));

        linker.DefineFunction("wasi_snapshot_preview1", "clock_time_get",
            (Caller caller, int clockId, long precision, int resultPointer) =>
            {
                logicalNanos += 1_000_000;
                var memory = caller.GetMemory("memory");
                if (memory is null || resultPointer < 0 || resultPointer + 8 > memory.GetLength())
                    return 28; // WASI errno: EINVAL
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(
                    memory.GetSpan(resultPointer, 8), logicalNanos);
                return 0;
            });

        linker.DefineFunction("wasi_snapshot_preview1", "random_get",
            (Caller caller, int bufferPointer, int bufferLength) =>
            {
                var memory = caller.GetMemory("memory");
                if (memory is null || bufferPointer < 0 || bufferLength < 0 ||
                    bufferPointer + bufferLength > memory.GetLength())
                    return 28; // WASI errno: EINVAL
                var span = memory.GetSpan(bufferPointer, bufferLength);
                for (int i = 0; i < span.Length; i += 8)
                {
                    ulong value = entropy.NextUInt64();
                    for (int b = 0; b < 8 && i + b < span.Length; b++)
                        span[i + b] = (byte)(value >> (b * 8));
                }
                return 0;
            });
    }

    private int NextObservation(Caller caller, int pointer, int capacity)
    {
        string line;
        try
        {
            line = _toGuest!.Take(_guestDead!.Token);
        }
        catch (OperationCanceledException)
        {
            return 0; // Match over (or host shutting down): tell the guest loop to exit.
        }
        var bytes = Encoding.UTF8.GetBytes(line);
        var memory = caller.GetMemory("memory")
            ?? throw new InvalidOperationException("Guest exports no memory.");
        if (pointer < 0 || capacity < 0 || bytes.Length > capacity ||
            pointer + bytes.Length > memory.GetLength())
            return -1;
        bytes.CopyTo(memory.GetSpan(pointer, bytes.Length));
        // Per-tick fuel budget: reset, do not accumulate (plan §10.1).
        caller.Fuel = _options.FuelPerTick;
        return bytes.Length;
    }

    private void PostDecision(Caller caller, int pointer, int length)
    {
        var memory = caller.GetMemory("memory")
            ?? throw new InvalidOperationException("Guest exports no memory.");
        if (pointer < 0 || length is < 0 or > 64 * 1024 || pointer + length > memory.GetLength())
        {
            MarkDead("Guest posted an out-of-bounds decision buffer.");
            return;
        }
        string reply = Encoding.UTF8.GetString(memory.GetSpan(pointer, length));
        _fromGuest!.Add(reply);
    }

    private void CleanUp()
    {
        MarkDead("Runtime disposed.");
        if (_guestThread is not null && !_guestThread.Join(TimeSpan.FromSeconds(5)))
            throw new InvalidOperationException("WASM guest thread failed to stop.");
        _guestThread = null;
        _store?.Dispose();
        _module?.Dispose();
        _engine?.Dispose();
        _toGuest?.Dispose();
        _fromGuest?.Dispose();
        _guestDead?.Dispose();
        _store = null;
        _module = null;
        _engine = null;
        _toGuest = null;
        _fromGuest = null;
        _guestDead = null;
    }

    public void Dispose() => CleanUp();
}
