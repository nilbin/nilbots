namespace BotArena.Engine;

/// <summary>Terminal reason for one schema-3 generic Deathmatch session.</summary>
public enum GenericDeathmatchEndReason
{
    FaultEligibility = 0,
    KillLimit = 1,
    MaxTicks = 2,
}
