namespace BotArena.App.Store;

public static class PurchaseStates
{
    /// <summary>Checkout started. Nothing granted, and nothing owed until the provider says so.</summary>
    public const string Pending = "pending";

    /// <summary>Paid and granted.</summary>
    public const string Completed = "completed";

    /// <summary>Refunded or charged back. The grant is deliberately *not* revoked — see below.</summary>
    public const string Refunded = "refunded";
}

/// <summary>
/// One attempt to buy one pack.
/// <para>
/// Written when checkout starts, not when payment lands, so an abandoned checkout leaves a
/// trace: without it a customer saying "I paid and got nothing" is unanswerable, because
/// the only record would be the provider's.
/// </para>
/// <para>
/// <see cref="ProviderReference"/> is what makes the webhook idempotent. Providers retry
/// aggressively and re-send on manual replay, and the same reference arriving twice must
/// grant once — the same property the notification writer gets from its dedupe key.
/// </para>
/// <para>
/// **A refund does not revoke the entitlement.** The appearance is snapshotted onto every
/// match the bot has already fought, so revoking cannot rewrite what those replays show;
/// it would only strip a cosmetic mid-ladder while the history keeps displaying it. The
/// state is recorded, and what to do about repeat abuse is a decision for a human with the
/// records in front of them, not an automated clawback.
/// </para>
/// </summary>
public class Purchase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid UserId { get; set; }

    /// <summary>The pack id, which is also its entitlement source id.</summary>
    public required string PackId { get; set; }

    /// <summary>Which provider processed it — `paddle`, or `manual` for a granted comp.</summary>
    public required string Provider { get; set; }

    /// <summary>The provider's own id for this transaction. Unique per provider.</summary>
    public required string ProviderReference { get; set; }

    public required string State { get; set; }

    /// <summary>Minor units, as charged. Recorded for reconciliation, never for display.</summary>
    public long? AmountMinor { get; set; }
    public string? Currency { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
