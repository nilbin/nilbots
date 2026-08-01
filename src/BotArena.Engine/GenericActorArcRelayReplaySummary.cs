using System.Collections.Immutable;
using System.Globalization;

namespace BotArena.Engine;

/// <summary>
/// Verified, grep-friendly Arc Relay counters derived from replay-v3's
/// authoritative states and event ledger. The public facade keeps the private
/// replay DTO graph inside Engine while the CLI and later dynamics tools share
/// one interpretation of the mechanics.
/// </summary>
public sealed record GenericActorArcRelayReplaySummary
{
    private GenericActorArcRelayReplaySummary(
        int ticks,
        string resultReason,
        int? winnerTeamId,
        int scheduledBirths,
        int actualBirths,
        int pickups,
        int steals,
        int carrierChanges,
        int carriedRelocations,
        int forcedRelocations,
        int handoffs,
        int arcTosses,
        int voluntaryDrops,
        int deathDrops,
        int banks,
        int pulses,
        long pendingWellTicks,
        long rearmWellTicks,
        ImmutableSortedDictionary<int, int> liveCoreTickHistogram,
        ImmutableSortedDictionary<string, int> pickupByClass,
        ImmutableSortedDictionary<string, int> carryTicksByClass,
        ImmutableSortedDictionary<string, SignatureCounters> signatures)
    {
        Ticks = ticks;
        ResultReason = resultReason;
        WinnerTeamId = winnerTeamId;
        ScheduledBirths = scheduledBirths;
        ActualBirths = actualBirths;
        Pickups = pickups;
        Steals = steals;
        CarrierChanges = carrierChanges;
        CarriedRelocations = carriedRelocations;
        ForcedRelocations = forcedRelocations;
        Handoffs = handoffs;
        ArcTosses = arcTosses;
        VoluntaryDrops = voluntaryDrops;
        DeathDrops = deathDrops;
        Banks = banks;
        Pulses = pulses;
        PendingWellTicks = pendingWellTicks;
        RearmWellTicks = rearmWellTicks;
        LiveCoreTickHistogram = liveCoreTickHistogram;
        PickupByClass = pickupByClass;
        CarryTicksByClass = carryTicksByClass;
        Signatures = signatures;
    }

    public int Ticks { get; }
    public string ResultReason { get; }
    public int? WinnerTeamId { get; }
    public int ScheduledBirths { get; }
    public int ActualBirths { get; }
    public int Pickups { get; }
    public int Steals { get; }
    public int CarrierChanges { get; }
    public int CarriedRelocations { get; }
    public int ForcedRelocations { get; }
    public int Handoffs { get; }
    public int ArcTosses { get; }
    public int VoluntaryDrops { get; }
    public int DeathDrops { get; }
    public int Banks { get; }
    public int Pulses { get; }
    public long PendingWellTicks { get; }
    public long RearmWellTicks { get; }
    public ImmutableSortedDictionary<int, int> LiveCoreTickHistogram { get; }
    public ImmutableSortedDictionary<string, int> PickupByClass { get; }
    public ImmutableSortedDictionary<string, int> CarryTicksByClass { get; }
    public ImmutableSortedDictionary<string, SignatureCounters> Signatures
    {
        get;
    }

    public static GenericActorArcRelayReplaySummary Read(string canonicalJson)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        ReplayV3 replay = ReplayV3Serializer.ReadCanonicalComplete(
            canonicalJson);
        if (replay.Result?.Mode is not ReplayV3.ModeResult.ArcRelay result)
        {
            throw new ArgumentException(
                "Replay-v3 summary requires a completed Arc Relay replay.",
                nameof(canonicalJson));
        }

        Dictionary<ReplayV3.ActorId, string> classByActor = replay
            .InitialFrame.State.ActiveLives
            .Concat(replay.Ticks.SelectMany(value =>
                value.TickStart.State.ActiveLives.Concat(
                    value.PostState.ActiveLives)))
            .GroupBy(value => value.ActorId)
            .ToDictionary(
                value => value.Key,
                value => ClassId(value.First().FormId));
        var pickupByClass = new Dictionary<string, int>(StringComparer.Ordinal);
        var carryTicksByClass = new Dictionary<string, int>(StringComparer.Ordinal);
        var signatureCounts = new Dictionary<string, MutableSignatureCounters>(
            StringComparer.Ordinal);
        var lastCarrierByCore = new Dictionary<ReplayV3.ArcCoreId,
            ReplayV3.ActorId>();
        var lastPossessingTeamByCore = new Dictionary<ReplayV3.ArcCoreId, int>();
        var liveHistogram = new Dictionary<int, int>();

        int scheduledBirths = 0;
        int actualBirths = 0;
        int pickups = 0;
        int steals = 0;
        int carrierChanges = 0;
        int carriedRelocations = 0;
        int forcedRelocations = 0;
        int handoffs = 0;
        int arcTosses = 0;
        int voluntaryDrops = 0;
        int deathDrops = 0;
        int banks = 0;
        int pulses = 0;
        long pendingWellTicks = 0;
        long rearmWellTicks = 0;
        ReplayV3.ModeState.ArcRelay previous = ArcState(
            replay.InitialFrame.State);

        foreach (ReplayV3.TickFrame tick in replay.Ticks)
        {
            ReplayV3.ModeState.ArcRelay tickStart = ArcState(
                tick.TickStart.State);
            foreach (ReplayV3.ArcWell well in previous.Wells)
            {
                if (well.NextScheduledBirthTick == tick.Tick)
                    scheduledBirths++;
            }

            ReplayV3.ModeState.ArcRelay post = ArcState(tick.PostState);
            Increment(liveHistogram, post.VisibleCores.Length);
            pendingWellTicks += post.Wells.Count(value => value.PendingCharge);
            rearmWellTicks += post.Wells.Count(value =>
                value.RearmCompletesAtTick is not null);
            foreach (ReplayV3.ArcCore core in post.VisibleCores)
            {
                if (core.CarrierActorId is not ReplayV3.ActorId carrier
                    || !classByActor.TryGetValue(carrier, out string? classId))
                {
                    continue;
                }
                Increment(carryTicksByClass, classId);
            }

            foreach (ReplayV3.ArcSignature active in post.VisibleSignatures)
            {
                MutableSignatureCounters counters = Signature(
                    signatureCounts,
                    active.SignatureId);
                counters.MaxConcurrent = Math.Max(
                    counters.MaxConcurrent,
                    post.VisibleSignatures.Count(value =>
                        value.OwnerTeamId == active.OwnerTeamId
                        && string.Equals(
                            value.SignatureId,
                            active.SignatureId,
                            StringComparison.Ordinal)));
            }

            foreach (ReplayV3.ArcRelayFact fact in Facts(tick))
            {
                switch (fact)
                {
                    case ReplayV3.ArcRelayFact.CoreBorn:
                        actualBirths++;
                        break;
                    case ReplayV3.ArcRelayFact.CorePickedUp value:
                        pickups++;
                        if (lastCarrierByCore.TryGetValue(
                                value.CoreId,
                                out ReplayV3.ActorId? priorCarrier)
                            && priorCarrier != value.CarrierActorId)
                        {
                            carrierChanges++;
                        }
                        if (lastPossessingTeamByCore.TryGetValue(
                                value.CoreId,
                                out int priorTeam)
                            && priorTeam != value.CarrierActorId.TeamId)
                        {
                            steals++;
                        }
                        lastCarrierByCore[value.CoreId] = value.CarrierActorId;
                        lastPossessingTeamByCore[value.CoreId] =
                            value.CarrierActorId.TeamId;
                        if (classByActor.TryGetValue(
                                value.CarrierActorId,
                                out string? pickupClass))
                        {
                            Increment(pickupByClass, pickupClass);
                        }
                        break;
                    case ReplayV3.ArcRelayFact.CoreRelocated value
                        when string.Equals(
                            value.RelocationKind,
                            "carried-movement",
                            StringComparison.Ordinal):
                        carriedRelocations++;
                        break;
                    case ReplayV3.ArcRelayFact.CoreRelocated value
                        when string.Equals(
                            value.RelocationKind,
                            "forced-displacement",
                            StringComparison.Ordinal):
                        forcedRelocations++;
                        break;
                    case ReplayV3.ArcRelayFact.CoreHandedOff value:
                        handoffs++;
                        carrierChanges++;
                        lastCarrierByCore[value.CoreId] = value.TargetActorId;
                        lastPossessingTeamByCore[value.CoreId] =
                            value.TargetActorId.TeamId;
                        break;
                    case ReplayV3.ArcRelayFact.CoreDropped value
                        when string.Equals(
                            value.DropKind,
                            "voluntary",
                            StringComparison.Ordinal):
                        voluntaryDrops++;
                        break;
                    case ReplayV3.ArcRelayFact.CoreDropped value
                        when string.Equals(
                            value.DropKind,
                            "destruction",
                            StringComparison.Ordinal):
                        deathDrops++;
                        break;
                    case ReplayV3.ArcRelayFact.CoreBanked:
                        banks++;
                        break;
                    case ReplayV3.ArcRelayFact.Pulse:
                        pulses++;
                        break;
                    case ReplayV3.ArcRelayFact.SignatureChanged value:
                        MutableSignatureCounters counters = Signature(
                            signatureCounts,
                            value.SignatureId);
                        if (string.Equals(
                                value.Reason,
                                "started",
                                StringComparison.Ordinal))
                        {
                            counters.Attempts++;
                        }
                        if (string.Equals(
                                value.SignatureId,
                                "arc-toss",
                                StringComparison.Ordinal)
                            && string.Equals(
                                value.Reason,
                                "launched",
                                StringComparison.Ordinal))
                        {
                            arcTosses++;
                        }
                        if (value.Phase is null)
                        {
                            if (IsCounterReason(value.Reason))
                                counters.Counters++;
                            else
                                counters.Completions++;
                        }
                        break;
                    case ReplayV3.ArcRelayFact.BodyRelocated value:
                        Signature(signatureCounts, value.SignatureId)
                            .UsefulEffects++;
                        break;
                    case ReplayV3.ArcRelayFact.SignatureDamage value:
                        Signature(signatureCounts, value.SignatureId)
                            .UsefulEffects++;
                        break;
                    case ReplayV3.ArcRelayFact.SignatureRepair value:
                        Signature(signatureCounts, value.SignatureId)
                            .UsefulEffects++;
                        break;
                }
            }
            previous = post;
        }

        return new GenericActorArcRelayReplaySummary(
            replay.Ticks.Length,
            result.Reason,
            replay.Result.Standings.WinnerTeamId,
            scheduledBirths,
            actualBirths,
            pickups,
            steals,
            carrierChanges,
            carriedRelocations,
            forcedRelocations,
            handoffs,
            arcTosses,
            voluntaryDrops,
            deathDrops,
            banks,
            pulses,
            pendingWellTicks,
            rearmWellTicks,
            liveHistogram.ToImmutableSortedDictionary(),
            pickupByClass.ToImmutableSortedDictionary(StringComparer.Ordinal),
            carryTicksByClass.ToImmutableSortedDictionary(StringComparer.Ordinal),
            signatureCounts.ToImmutableSortedDictionary(
                value => value.Key,
                value => value.Value.Freeze(),
                StringComparer.Ordinal));
    }

    public string Format()
    {
        var lines = new List<string>
        {
            $"Arc Relay: {Ticks.ToString(CultureInfo.InvariantCulture)} ticks; result {ResultReason}; winner {(WinnerTeamId?.ToString(CultureInfo.InvariantCulture) ?? "draw")}",
            $"Cores: scheduled {ScheduledBirths}, born {ActualBirths}, pickups {Pickups}, steals {Steals}, carrier-changes {CarrierChanges}",
            $"Travel: carried {CarriedRelocations}, forced {ForcedRelocations}, handoffs {Handoffs}, arc-tosses {ArcTosses}",
            $"Drops: voluntary {VoluntaryDrops}, death {DeathDrops}; banks {Banks}; pulses {Pulses}",
            $"Wells: pending-well-ticks {PendingWellTicks}, rearm-well-ticks {RearmWellTicks}",
            "Live cores: " + Join(LiveCoreTickHistogram, value =>
                $"{value.Key}={value.Value}"),
            "Pickup by class: " + Join(PickupByClass, value =>
                $"{value.Key}={value.Value}"),
            "Carry ticks by class: " + Join(CarryTicksByClass, value =>
                $"{value.Key}={value.Value}"),
            "Signatures: " + Join(Signatures, value =>
                $"{value.Key}=attempts:{value.Value.Attempts},complete:{value.Value.Completions},countered:{value.Value.Counters},effects:{value.Value.UsefulEffects},max-stack:{value.Value.MaxConcurrent}"),
        };
        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<ReplayV3.ArcRelayFact> Facts(
        ReplayV3.TickFrame tick) =>
        tick.TickStart.Events.Concat(tick.Events)
            .Select(value => value.Payload)
            .OfType<ReplayV3.EventPayload.ArcRelay>()
            .Select(value => value.Fact);

    private static ReplayV3.ModeState.ArcRelay ArcState(
        ReplayV3.WorldState state) =>
        state.Mode as ReplayV3.ModeState.ArcRelay
        ?? throw new ArgumentException(
            "Arc Relay replay changed mode kind inside its chronology.");

    private static string ClassId(string formId) =>
        formId.StartsWith(
            ArcRelayH0Definition.FormPrefix,
            StringComparison.Ordinal)
            ? formId[ArcRelayH0Definition.FormPrefix.Length..]
            : formId;

    private static MutableSignatureCounters Signature(
        IDictionary<string, MutableSignatureCounters> values,
        string signatureId)
    {
        if (!values.TryGetValue(
                signatureId,
                out MutableSignatureCounters? counters))
        {
            counters = new MutableSignatureCounters();
            values.Add(signatureId, counters);
        }
        return counters;
    }

    private static bool IsCounterReason(string reason) =>
        reason.StartsWith("cancelled", StringComparison.Ordinal)
        || reason.StartsWith("interrupted", StringComparison.Ordinal)
        || reason.StartsWith("ended-null", StringComparison.Ordinal)
        || string.Equals(reason, "owner-destroyed", StringComparison.Ordinal)
        || string.Equals(reason, "target-destroyed", StringComparison.Ordinal);

    private static void Increment<TKey>(IDictionary<TKey, int> values, TKey key)
        where TKey : notnull
    {
        values.TryGetValue(key, out int current);
        values[key] = current + 1;
    }

    private static string Join<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> values,
        Func<KeyValuePair<TKey, TValue>, string> format) =>
        string.Join(" ", values.Select(format).DefaultIfEmpty("none"));

    public sealed record SignatureCounters(
        int Attempts,
        int Completions,
        int Counters,
        int UsefulEffects,
        int MaxConcurrent);

    private sealed class MutableSignatureCounters
    {
        public int Attempts { get; set; }
        public int Completions { get; set; }
        public int Counters { get; set; }
        public int UsefulEffects { get; set; }
        public int MaxConcurrent { get; set; }

        public SignatureCounters Freeze() => new(
            Attempts,
            Completions,
            Counters,
            UsefulEffects,
            MaxConcurrent);
    }
}
