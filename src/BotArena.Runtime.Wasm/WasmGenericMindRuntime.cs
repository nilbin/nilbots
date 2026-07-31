using System.Collections.Concurrent;
using System.Diagnostics;
using BotArena.Engine;
using Wasmtime;
using Sdk = BotArena.Sdk;
using WasmtimeEngine = Wasmtime.Engine;

namespace BotArena.Runtime.Wasm;

/// <summary>
/// One isolated WASM MIND instance: a single Store, linear memory, globals,
/// guest thread, deterministic clock/entropy stream, protocol state and mind
/// object, alive for one participant for the WHOLE MATCH.
///
/// <para>Bodies are data inside it. A body's destruction disposes nothing —
/// that is the entire topology change, and the reason a mind's memory can
/// outlive the bodies executing its plans. Only the match's end or the
/// participant's disqualification disposes the Store.</para>
///
/// <para>The fuel discipline has one trap worth naming, because getting it
/// wrong is silent: the per-tick budget is refilled ONLY when the released
/// message is an observation. <c>Hello</c>, <c>MindStart</c> and
/// <c>MatchEnd</c> all draw from the one-time startup pool, so refilling on
/// them would quietly hand startup and shutdown a per-tick budget each, and
/// nothing would fail a test.</para>
/// </summary>
public sealed class WasmGenericMindRuntime : IGenericMindRuntime
{
    private readonly WasmtimeEngine _engine;
    private readonly Module _module;
    private readonly WasmMindRuntimeOptions _options;
    private readonly Action<WasmGenericMindRuntimeFactory.RuntimeDiagnostic>
        _releaseFactoryRuntime;

    private Store? _store;
    private Thread? _guestThread;
    private BlockingCollection<byte[]>? _toGuest;
    private BlockingCollection<MindRuntimeReply>? _fromGuest;
    private CancellationTokenSource? _guestDead;
    private string _deathReason = "";
    private int _replyExpected;
    private bool _started;
    private bool _disposed;
    private int? _participantId;
    private string? _failureReason;
    private Sdk.MindWaitAction _waitAction;
    private ulong _currentTickFuel;

    internal WasmGenericMindRuntime(
        WasmtimeEngine engine,
        Module module,
        WasmMindRuntimeOptions options,
        Action<WasmGenericMindRuntimeFactory.RuntimeDiagnostic>
            releaseFactoryRuntime)
    {
        _engine = engine;
        _module = module;
        _options = options;
        _releaseFactoryRuntime = releaseFactoryRuntime;
    }

    /// <summary>Fuel left after the most recent tick.</summary>
    public ulong LastFuelRemaining { get; private set; }

    /// <summary>The budget granted for the most recent tick.</summary>
    public ulong LastTickFuelBudget { get; private set; }

    /// <summary>Peak fuel consumed in a single tick this match.</summary>
    public ulong MaxFuelUsedPerTick { get; private set; }

    /// <summary>Live own bodies at the start of the most recent tick.</summary>
    public int LastLiveBodyCount { get; private set; }

    public void StartMatch(GenericMindRuntimeStart start)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            throw new InvalidOperationException(
                "A mind runtime can start only one match.");
        }
        ArgumentNullException.ThrowIfNull(start);

        _started = true;
        _participantId = start.ParticipantId;
        _guestDead = new CancellationTokenSource();
        _toGuest = new BlockingCollection<byte[]>(boundedCapacity: 1);
        _fromGuest = new BlockingCollection<MindRuntimeReply>(
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
        _store.SetEpochDeadline(1);

        using var linker = new Linker(_engine) { AllowShadowing = true };
        linker.DefineWasi();
        linker.DefineFunction(
            "wasi_snapshot_preview1",
            "poll_oneoff",
            (Caller caller, int subscriptions, int events, int count, int result) =>
                52);
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
        DefineDeterministicWasiShims(linker, start.MindRandomSeed);

        Instance instance = linker.Instantiate(_store, _module);
        Action startFunction = instance.GetAction("_start")
            ?? throw new InvalidOperationException(
                $"Artifact '{_options.ModulePath}' does not export _start.");

        var guestThread = new Thread(() => RunGuest(startFunction))
        {
            IsBackground = true,
            Name = $"wasm-generic-mind-p{start.ParticipantId}",
        };
        guestThread.Start();
        _guestThread = guestThread;

        try
        {
            MindRuntimeReply helloReply = Exchange(
                GenericMindWasmProtocol.FormatHello());
            GenericMindWasmProtocol.ParseHelloAck(helloReply.Bytes);

            Sdk.MindStart sdkStart = GenericMindSdkModelMapper.ToSdk(start);
            _waitAction = GenericMindSdkModelMapper.WaitActionOf(
                sdkStart.Contract);

            MindRuntimeReply readyReply = Exchange(
                GenericMindWasmProtocol.FormatMindStart(
                    start,
                    _options.BotName));
            GenericMindWasmProtocol.ParseReady(readyReply.Bytes, start);
        }
        catch
        {
            _failureReason = string.IsNullOrWhiteSpace(_deathReason)
                ? "WASM mind startup negotiation failed."
                : _deathReason;
            MarkDead("WASM mind startup negotiation failed.");
            _engine.IncrementEpoch();
            throw;
        }
    }

    public GenericMindRuntimeDecisions ExecuteTick(
        GenericMindRuntimeObservation observation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_started)
        {
            throw new InvalidOperationException(
                "StartMatch must be called before ExecuteTick.");
        }
        ArgumentNullException.ThrowIfNull(observation);

        // Authoritative tick-start state, fixed before the call: the budget is
        // a pure function of what the replay already records, so two hosts
        // compute the same number.
        LastLiveBodyCount = observation.Bodies.Length;
        _currentTickFuel = _options.TickFuel(observation.Bodies.Length);
        LastTickFuelBudget = _currentTickFuel;

        MindRuntimeReply reply;
        try
        {
            reply = Exchange(
                GenericMindWasmProtocol.FormatObservation(
                    observation,
                    _waitAction));
        }
        catch (Exception exception)
        {
            _failureReason = string.IsNullOrWhiteSpace(_deathReason)
                ? $"{exception.GetType().Name}: {exception.Message}"
                : _deathReason;
            throw;
        }
        LastFuelRemaining = reply.FuelRemaining;
        ulong used = _currentTickFuel > LastFuelRemaining
            ? _currentTickFuel - LastFuelRemaining
            : 0;
        if (used > MaxFuelUsedPerTick)
            MaxFuelUsedPerTick = used;

        try
        {
            return GenericMindWasmProtocol.ParseDecisions(
                reply.Bytes,
                observation.Tick);
        }
        catch
        {
            _failureReason = "WASM mind posted an invalid terminal reply.";
            MarkDead("WASM mind posted an invalid terminal reply.");
            _engine.IncrementEpoch();
            throw;
        }
    }

    private MindRuntimeReply Exchange(byte[] frame)
    {
        if (_guestDead is null || _toGuest is null || _fromGuest is null)
        {
            throw new InvalidOperationException(
                "Mind runtime has not started.");
        }
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
                    out MindRuntimeReply? reply,
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
                MarkDead("WASM mind exited before the match ended.");
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
        MarkDead("WASM mind exceeded the wall-clock message timeout.");
        _engine.IncrementEpoch();
        throw new TimeoutException(_deathReason);
    }

    private void DefineDeterministicWasiShims(Linker linker, ulong mindSeed)
    {
        long logicalNanos = 0;
        var entropy = new DeterministicRandom(
            DeterministicRandom.Mix(mindSeed ^ 0xB07A_5EED_C10C_0FF5UL));

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
            MarkDead("Guest supplied an invalid mind message buffer.");
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
            && Interlocked.CompareExchange(ref _replyExpected, 1, 0) != 0)
        {
            MarkDead("Guest consumed another mind request before replying.");
            return -1;
        }

        if (frame.Length >= GenericMindWasmProtocol.HeaderSize)
        {
            caller.Store.SetEpochDeadline(1);
            // ONLY the observation refills. Startup and shutdown draw from the
            // one-time startup pool, and the budget never accumulates across
            // ticks.
            if (messageType == Sdk.ActorWireMessageType.Observation)
                caller.Fuel = _currentTickFuel;
        }
        return frame.Length;
    }

    private void PostReply(Caller caller, int pointer, int length)
    {
        Memory? memory = caller.GetMemory("memory")
            ?? throw new InvalidOperationException("Guest exports no memory.");
        long memoryLength = memory.GetLength();
        if (pointer < 0
            || length is < 0 or > GenericMindWasmProtocol.MaxGuestFrameBytes
            || length > memoryLength
            || pointer > memoryLength - length)
        {
            MarkDead("Guest posted an out-of-bounds mind reply.");
            return;
        }

        if (Interlocked.CompareExchange(ref _replyExpected, 0, 1) != 1)
        {
            MarkDead("Guest posted an unsolicited or duplicate mind reply.");
            return;
        }

        byte[] bytes = memory.GetSpan(pointer, length).ToArray();
        caller.Store.SetEpochDeadline(1);
        if (!_fromGuest!.TryAdd(new MindRuntimeReply(bytes, caller.Fuel)))
            MarkDead("Guest posted more than one mind reply.");
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
                _toGuest.TryAdd(GenericMindWasmProtocol.FormatMatchEnd());
            }
            if (_guestThread is not null
                && !_guestThread.Join(TimeSpan.FromSeconds(2)))
            {
                MarkDead("Runtime disposed.");
                _engine.IncrementEpoch();
                if (!_guestThread.Join(TimeSpan.FromSeconds(3)))
                {
                    throw new InvalidOperationException(
                        "WASM mind thread failed to stop; its Store was "
                        + "quarantined.");
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
                _releaseFactoryRuntime(new(
                    _participantId,
                    MaxFuelUsedPerTick,
                    LastTickFuelBudget,
                    _failureReason));
            }
        }
    }

    private sealed record MindRuntimeReply(byte[] Bytes, ulong FuelRemaining);
}
