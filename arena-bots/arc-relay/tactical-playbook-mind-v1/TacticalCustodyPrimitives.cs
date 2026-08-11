using BotArena.Sdk;

/// <summary>
/// Pure custody selectors. The runtime owns observations and commands; these
/// helpers make timeout, recovery, and escort choices deterministic and easy
/// to fixture without a full match.
/// </summary>
internal static class TacticalCustodyPrimitives
{
    /// <summary>
    /// A genuinely unreachable delivery fallback is an episode, not a
    /// terminal state. Re-test the objective on this cadence while its static
    /// terrain path remains absent.
    /// </summary>
    internal const int FallbackRetryTicks = 8;

    internal static int CarrierPreferenceRank(string preference) =>
        preference switch
        {
            "require" => 0,
            "prefer" => 1,
            "allow" => 2,
            "forbid" => int.MaxValue,
            _ => throw new InvalidDataException(
                $"Unknown carrier preference '{preference}'."),
        };

    /// <summary>
    /// A body can carry only one Core. It must never reserve a second pickup
    /// while delivering the first, regardless of its authored preference.
    /// </summary>
    internal static int PickupCarrierRank(
        string preference,
        bool alreadyCarrying) => alreadyCarrying
            ? int.MaxValue
            : CarrierPreferenceRank(preference);

    internal static bool MayRecoverDrop(
        string recoveryPolicy,
        ActorIdentity candidate,
        ActorIdentity sourceCarrier,
        bool safeConversion) => recoveryPolicy switch
    {
        "same-carrier" => candidate == sourceCarrier,
        "nearest-authorized" => true,
        "guard-until-safe" => safeConversion,
        _ => throw new InvalidDataException(
            $"Unknown drop recovery policy '{recoveryPolicy}'."),
    };

    /// <summary>
    /// An accidental carrier can hold a Core without ever committing to a
    /// custody policy. Runtime progress represents that state with an empty
    /// id; a later drop must remain claimable instead of treating the empty
    /// sentinel as a real, impossible custody id.
    /// </summary>
    internal static string? CommittedCustodyOrNull(string? custodyId) =>
        string.IsNullOrEmpty(custodyId) ? null : custodyId;

    /// <summary>
    /// A custody policy states whether distance or authored carrier preference
    /// is the primary allocation key. Stable unit id always breaks the final
    /// tie, so both policies are deterministic.
    /// </summary>
    internal static (int Primary, int Secondary, int UnitId) PickupPriority(
        string pickupSelection,
        int carrierPreference,
        int distance,
        int unitId) => string.Equals(
            pickupSelection, "nearest-authorized", StringComparison.Ordinal)
        ? (distance, carrierPreference, unitId)
        : (carrierPreference, distance, unitId);

    /// <summary>
    /// A last-confirmed loose Core remains actionable through fog for the
    /// authored Core-memory window. Seeing its tile without seeing the Core is
    /// conclusive negative evidence and clears it immediately.
    /// </summary>
    internal static bool LooseCoreMemoryActive(
        int tick,
        int lastConfirmedTick,
        int memoryTicks,
        bool tileVisible,
        bool coreVisible) => tick - lastConfirmedTick <= memoryTicks
        && (!tileVisible || coreVisible);

    internal static int CompareEscortCandidate(
        Position escortPosition,
        (ActorIdentity ActorId, Position Position) left,
        (ActorIdentity ActorId, Position Position) right)
    {
        int distance = escortPosition.ChebyshevDistance(left.Position)
            .CompareTo(escortPosition.ChebyshevDistance(right.Position));
        return distance != 0 ? distance : left.ActorId.CompareTo(right.ActorId);
    }

    internal static bool TransferWindowOpen(
        int carriedTicks,
        int transferTimeoutTicks) => carriedTicks < transferTimeoutTicks;

    internal static string TransferRendezvousMover(
        bool transferWindowOpen) => transferWindowOpen
            ? "authorized-carrier"
            : "accidental-carrier-delivers";

    internal static bool DeliveryTimedOut(
        int stagnantTicks,
        int deliveryTimeoutTicks) => stagnantTicks >= deliveryTimeoutTicks;

    internal static StalledDeliveryDisposition StalledDelivery(
        int stagnantTicks,
        int deliveryTimeoutTicks,
        bool terrainReachable)
    {
        if (!DeliveryTimedOut(stagnantTicks, deliveryTimeoutTicks))
            return StalledDeliveryDisposition.Continue;
        if (terrainReachable)
            return StalledDeliveryDisposition.RetryObjective;
        return (stagnantTicks - deliveryTimeoutTicks) % FallbackRetryTicks == 0
            ? StalledDeliveryDisposition.RetryObjective
            : StalledDeliveryDisposition.Fallback;
    }

    internal enum StalledDeliveryDisposition
    {
        Continue,
        RetryObjective,
        Fallback,
    }
}
