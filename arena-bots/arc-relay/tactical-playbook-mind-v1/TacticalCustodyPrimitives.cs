using BotArena.Sdk;

/// <summary>
/// Pure custody selectors. The runtime owns observations and commands; these
/// helpers make timeout, recovery, and escort choices deterministic and easy
/// to fixture without a full match.
/// </summary>
internal static class TacticalCustodyPrimitives
{
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
}
