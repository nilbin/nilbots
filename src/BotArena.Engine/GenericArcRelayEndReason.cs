namespace BotArena.Engine;

/// <summary>Terminal reason for one schema-3 Arc Relay session.</summary>
public enum GenericArcRelayEndReason
{
    FaultEligibility = 0,
    ReactorDestroyed = 1,
    MaxTicks = 2,
}
