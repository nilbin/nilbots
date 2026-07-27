using System.Collections.Concurrent;
using System.Diagnostics;
using BotArena.Engine;
using Wasmtime;
using Sdk = BotArena.Sdk;
using WasmtimeEngine = Wasmtime.Engine;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// One isolated WASM actor-life instance. The factory shares only immutable
/// compiled code; this runtime owns its Store, linear memory, globals, guest
/// thread, deterministic clock/entropy stream, and protocol state.
/// </summary>
public sealed class WasmActorRuntime : IActorRuntime
{
    private readonly WasmtimeEngine _engine;
    private readonly Module _module;
    private readonly WasmRuntimeOptions _options;
    private readonly Action _releaseFactoryRuntime;

    private Store? _store;
    private Thread? _guestThread;
    private BlockingCollection<byte[]>? _toGuest;
    private BlockingCollection<ActorRuntimeReply>? _fromGuest;
    private CancellationTokenSource? _guestDead;
    private string _deathReason = "";
    private int _replyExpected;
    private bool _started;
    private bool _disposed;

    internal WasmActorRuntime(
        WasmtimeEngine engine,
        Module module,
        WasmRuntimeOptions options,
        Action releaseFactoryRuntime)
    {
        _engine = engine;
        _module = module;
        _options = options;
        _releaseFactoryRuntime = releaseFactoryRuntime;
    }

    public ulong LastFuelRemaining { get; private set; }

    public ulong MaxFuelUsedPerTick { get; private set; }

    public void StartLife(ActorMatchStart start)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
            throw new InvalidOperationException("An actor runtime can start only one life.");
        ArgumentNullException.ThrowIfNull(start);

        _started = true;
        _guestDead = new CancellationTokenSource();
        _toGuest = new BlockingCollection<byte[]>(boundedCapacity: 1);
        _fromGuest = new BlockingCollection<ActorRuntimeReply>(
            boundedCapacity: 1);

        _store = new Store(_engine);
        _store.SetLimits(
            memorySize: _options.MaxMemoryBytes,
            tableElements: _options.MaxTableElements,
            instances: 1,
            tables: 1,
            memories: 1);
        _store.SetWasiConfiguration(
            new WasiConfiguration().WithArgs("bot.wasm"));
        _store.Fuel = _options.StartupFuel;
        // A hostile module can loop before its first host import. Arm epoch
        // interruption before _start so the startup timeout remains enforceable.
        _store.SetEpochDeadline(1);

        using var linker = new Linker(_engine) { AllowShadowing = true };
        linker.DefineWasi();
        // Wasmtime's synchronous poll_oneoff can block inside a host callback,
        // where fuel and epoch interruption cannot stop it. Actor bots have no
        // real-time scheduling capability, so fail deterministicly and
        // immediately instead of allowing a life to pin its guest thread.
        linker.DefineFunction(
            "wasi_snapshot_preview1",
            "poll_oneoff",
            (Caller caller, int subscriptions, int events, int count, int result) =>
                52); // WASI_ERRNO_NOSYS
        linker.DefineFunction(
            "botarena",
            "next_observation",
            (Caller caller, int pointer, int capacity) =>
                NextMessage(caller, pointer, capacity));
        linker.DefineFunction(
            "botarena",
            "post_decision",
            (Caller caller, int pointer, int length) =>
                PostReply(caller, pointer, length));
        DefineDeterministicWasiShims(linker, start.ActorRandomSeed);

        var instance = linker.Instantiate(_store, _module);
        Action startFunction = instance.GetAction("_start")
            ?? throw new InvalidOperationException(
                $"Artifact '{_options.ModulePath}' does not export _start.");

        var guestThread = new Thread(() => RunGuest(startFunction))
        {
            IsBackground = true,
            Name = $"wasm-actor-{start.ActorId}",
        };
        guestThread.Start();
        _guestThread = guestThread;

        try
        {
            ActorRuntimeReply helloReply = Exchange(
                ActorWasmProtocol.FormatHello());
            ActorWasmProtocol.ParseHelloAck(helloReply.Bytes);

            ActorRuntimeReply readyReply = Exchange(
                ActorWasmProtocol.FormatMatchStart(start, _options.BotName));
            ActorWasmProtocol.ParseReady(readyReply.Bytes, start);
        }
        catch
        {
            MarkDead("WASM actor startup negotiation failed.");
            _engine.IncrementEpoch();
            throw;
        }
    }

    public ActorDecision ExecuteTick(ActorObservation observation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started)
        {
            throw new InvalidOperationException(
                "StartLife must be called before ExecuteTick.");
        }

        ActorRuntimeReply reply = Exchange(
            ActorWasmProtocol.FormatObservation(observation));
        LastFuelRemaining = reply.FuelRemaining;
        ulong used = _options.FuelPerTick > LastFuelRemaining
            ? _options.FuelPerTick - LastFuelRemaining
            : 0;
        if (used > MaxFuelUsedPerTick)
            MaxFuelUsedPerTick = used;
        return ActorWasmProtocol.ParseDecision(reply.Bytes);
    }

    private ActorRuntimeReply Exchange(byte[] frame)
    {
        if (_guestDead is null || _toGuest is null || _fromGuest is null)
            throw new InvalidOperationException("Actor runtime has not started.");
        if (_guestDead.IsCancellationRequested)
            throw new InvalidOperationException(_deathReason);

        try
        {
            var elapsed = Stopwatch.StartNew();
            if (!_toGuest.TryAdd(
                    frame,
                    _options.TickTimeoutMs,
                    _guestDead.Token))
            {
                ThrowMessageTimeout();
            }
            int remainingMs = (int)Math.Clamp(
                _options.TickTimeoutMs - elapsed.ElapsedMilliseconds,
                0L,
                int.MaxValue);
            if (_fromGuest.TryTake(
                    out ActorRuntimeReply? reply,
                    remainingMs,
                    _guestDead.Token))
            {
                return reply;
            }
            ThrowMessageTimeout();
            throw new UnreachableException();
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException(_deathReason);
        }
    }

    private void RunGuest(Action startFunction)
    {
        try
        {
            startFunction();
            if (_guestDead is { IsCancellationRequested: false })
                MarkDead("WASM actor exited before its life ended.");
        }
        catch (TrapException exception)
        {
            MarkDead(exception.Type switch
            {
                TrapCode.OutOfFuel => "Fuel limit exceeded.",
                _ => $"WASM trap: {exception.Type}.",
            });
        }
        catch (WasmtimeException exception)
        {
            MarkDead($"WASM runtime failure: {exception.Message}");
        }
        catch (Exception exception)
        {
            MarkDead($"{exception.GetType().Name}: {exception.Message}");
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

    private void ThrowMessageTimeout()
    {
        MarkDead("WASM actor exceeded the wall-clock message timeout.");
        _engine.IncrementEpoch();
        throw new TimeoutException(_deathReason);
    }

    private void DefineDeterministicWasiShims(
        Linker linker,
        ulong actorSeed)
    {
        long logicalNanos = 0;
        var entropy = new DeterministicRandom(
            DeterministicRandom.Mix(
                actorSeed ^ 0xB07A_5EED_C10C_0FF5UL));

        linker.DefineFunction(
            "wasi_snapshot_preview1",
            "clock_time_get",
            (Caller caller, int clockId, long precision, int resultPointer) =>
            {
                logicalNanos += 1_000_000;
                Memory? memory = caller.GetMemory("memory");
                long memoryLength = memory?.GetLength() ?? 0;
                if (memory is null
                    || resultPointer < 0
                    || resultPointer > memoryLength - 8)
                {
                    return 28;
                }
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(
                    memory.GetSpan(resultPointer, 8),
                    logicalNanos);
                return 0;
            });

        linker.DefineFunction(
            "wasi_snapshot_preview1",
            "random_get",
            (Caller caller, int bufferPointer, int bufferLength) =>
            {
                Memory? memory = caller.GetMemory("memory");
                long memoryLength = memory?.GetLength() ?? 0;
                if (memory is null
                    || bufferPointer < 0
                    || bufferLength < 0
                    || bufferLength > memoryLength
                    || bufferPointer > memoryLength - bufferLength)
                {
                    return 28;
                }
                Span<byte> span = memory.GetSpan(
                    bufferPointer,
                    bufferLength);
                for (int i = 0; i < span.Length; i += 8)
                {
                    ulong value = entropy.NextUInt64();
                    for (int b = 0; b < 8 && i + b < span.Length; b++)
                        span[i + b] = (byte)(value >> (b * 8));
                }
                return 0;
            });
    }

    private int NextMessage(Caller caller, int pointer, int capacity)
    {
        byte[] frame;
        try
        {
            frame = _toGuest!.Take(_guestDead!.Token);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }

        Memory? memory = caller.GetMemory("memory")
            ?? throw new InvalidOperationException("Guest exports no memory.");
        long memoryLength = memory.GetLength();
        if (pointer < 0
            || capacity < 0
            || frame.Length > capacity
            || frame.Length > memoryLength
            || pointer > memoryLength - frame.Length)
        {
            MarkDead("Guest supplied an invalid actor message buffer.");
            return -1;
        }

        frame.CopyTo(memory.GetSpan(pointer, frame.Length));
        Sdk.ActorWireMessageType messageType =
            Sdk.ActorWireProtocol.PeekHostMessageType(frame);
        bool expectsReply = messageType is
            Sdk.ActorWireMessageType.Hello
            or Sdk.ActorWireMessageType.MatchStart
            or Sdk.ActorWireMessageType.Observation;
        if (expectsReply
            && Interlocked.CompareExchange(
                ref _replyExpected,
                1,
                0) != 0)
        {
            MarkDead(
                "Guest consumed another actor request before replying.");
            return -1;
        }

        if (frame.Length >= ActorWasmProtocol.HeaderSize)
        {
            // Blocked lives are inside next_observation. Reset this Store's
            // relative deadline immediately before it returns to WebAssembly,
            // so an epoch increment for another life cannot stale this one.
            caller.Store.SetEpochDeadline(1);
            if (messageType == Sdk.ActorWireMessageType.Observation)
            {
                // Observation is message type 5. Startup negotiation and
                // MatchStart use StartupFuel; ticks receive a fresh,
                // non-accumulating budget.
                caller.Fuel = _options.FuelPerTick;
            }
        }
        return frame.Length;
    }

    private void PostReply(Caller caller, int pointer, int length)
    {
        Memory? memory = caller.GetMemory("memory")
            ?? throw new InvalidOperationException("Guest exports no memory.");
        long memoryLength = memory.GetLength();
        if (pointer < 0
            || length is < 0 or > ActorWasmProtocol.MaxGuestFrameBytes
            || length > memoryLength
            || pointer > memoryLength - length)
        {
            MarkDead("Guest posted an out-of-bounds actor reply.");
            return;
        }

        if (Interlocked.CompareExchange(
                ref _replyExpected,
                0,
                1) != 1)
        {
            MarkDead(
                "Guest posted an unsolicited or duplicate actor reply.");
            return;
        }

        byte[] bytes = memory.GetSpan(pointer, length).ToArray();
        // Store is confined to the guest thread once _start begins. Capturing
        // fuel from Caller here avoids racing Store from the engine thread.
        // Keep interruption armed after the reply too. A hostile guest can
        // post once and then spin instead of returning to next_observation;
        // Dispose must still be able to stop it.
        caller.Store.SetEpochDeadline(1);
        if (!_fromGuest!.TryAdd(
                new ActorRuntimeReply(bytes, caller.Fuel)))
        {
            MarkDead("Guest posted more than one actor reply.");
        }
    }

    public void Dispose()
    {
        if (_disposed && _guestThread is null)
            return;
        _disposed = true;

        bool stopped = _guestThread is null;
        try
        {
            if (_guestThread is not null
                && _guestDead is { IsCancellationRequested: false }
                && _toGuest is not null)
            {
                _toGuest.TryAdd(ActorWasmProtocol.FormatMatchEnd());
            }
            if (_guestThread is not null
                && !_guestThread.Join(TimeSpan.FromSeconds(2)))
            {
                MarkDead("Runtime disposed.");
                _engine.IncrementEpoch();
                if (!_guestThread.Join(TimeSpan.FromSeconds(3)))
                {
                    throw new InvalidOperationException(
                        "WASM actor thread failed to stop; its Store was quarantined.");
                }
            }
            stopped = true;
        }
        finally
        {
            MarkDead("Runtime disposed.");
            if (stopped)
            {
                _guestThread = null;
                _store?.Dispose();
                _toGuest?.Dispose();
                _fromGuest?.Dispose();
                _guestDead?.Dispose();
                _store = null;
                _toGuest = null;
                _fromGuest = null;
                _guestDead = null;
                _releaseFactoryRuntime();
            }
        }
    }

    private sealed record ActorRuntimeReply(
        byte[] Bytes,
        ulong FuelRemaining);
}
