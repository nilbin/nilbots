namespace BotArena.App.Shared;

/// <summary>Authentication resolved at a transport boundary for application use cases.</summary>
public sealed record ApplicationActor(
    Guid? AccountId,
    bool IsSystemAccount,
    IReadOnlySet<string> Roles)
{
    public bool IsAuthenticated => AccountId.HasValue;
}
