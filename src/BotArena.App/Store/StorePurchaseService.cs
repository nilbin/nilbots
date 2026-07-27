using BotArena.App.Cosmetics;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Store;

/// <summary>
/// Turning a completed payment into owned cosmetics.
/// <para>
/// The only thing in the application that knows a purchase grants anything, and it does so
/// through the same <see cref="CosmeticEntitlementService.GrantForEventAsync"/> that
/// achievements use. A pack's id is its entitlement source id, so nothing downstream — the
/// catalog, the garage, the unlock toast — can tell a bought chassis from an earned one.
/// That is deliberate: the day it needs to, that becomes a design decision made on purpose
/// rather than a distinction that leaked in through the checkout.
/// </para>
/// </summary>
public sealed class StorePurchaseService(
    AppDbContext db,
    CosmeticCatalog catalog,
    CosmeticEntitlementService entitlements,
    TimeProvider timeProvider,
    ILogger<StorePurchaseService> logger)
{
    /// <summary>
    /// Record a checkout that is about to start.
    /// </summary>
    /// <remarks>
    /// Written before the customer reaches the provider, so an abandoned or failed payment
    /// still leaves something to answer questions with.
    /// </remarks>
    public async Task<Purchase> BeginAsync(
        Guid userId,
        string packId,
        string provider,
        string providerReference,
        CancellationToken cancellationToken)
    {
        if (catalog.FindPack(packId) is null)
            throw new ArgumentException($"Unknown pack '{packId}'.", nameof(packId));

        var purchase = new Purchase
        {
            UserId = userId,
            PackId = packId,
            Provider = provider,
            ProviderReference = providerReference,
            State = PurchaseStates.Pending,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(cancellationToken);
        return purchase;
    }

    /// <summary>
    /// A payment completed: grant the pack.
    /// </summary>
    /// <remarks>
    /// Idempotent on <c>(provider, reference)</c>, because providers retry webhooks and
    /// replay them by hand. A second delivery finds the purchase already completed and does
    /// nothing — and even if it did not, `GrantForEventAsync` would grant nothing new,
    /// since entitlements are already deduped per account and source.
    /// </remarks>
    /// <returns>How many entitlements this call newly granted.</returns>
    public async Task<int> CompleteAsync(
        string provider,
        string providerReference,
        long? amountMinor,
        string? currency,
        CancellationToken cancellationToken)
    {
        Purchase? purchase = await db.Purchases.SingleOrDefaultAsync(
            row => row.Provider == provider && row.ProviderReference == providerReference,
            cancellationToken);

        if (purchase is null)
        {
            // The provider knows about a payment we have no record of starting. Refusing
            // silently would mean a paying customer gets nothing, so this is loud: it needs
            // a person, not a retry.
            logger.LogError(
                "Payment {Reference} from {Provider} matches no known checkout",
                providerReference,
                provider);
            throw new InvalidOperationException(
                $"No checkout recorded for {provider} reference '{providerReference}'.");
        }

        if (purchase.State == PurchaseStates.Completed)
            return 0;

        purchase.State = PurchaseStates.Completed;
        purchase.CompletedAt = timeProvider.GetUtcNow().UtcDateTime;
        purchase.AmountMinor = amountMinor;
        purchase.Currency = currency;

        int granted = await entitlements.GrantForEventAsync(
            purchase.UserId,
            CosmeticCatalog.PurchaseSource,
            purchase.PackId,
            metadata: new { purchase.Provider, purchase.ProviderReference },
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return granted;
    }

    /// <summary>
    /// Record a refund. Deliberately does not revoke.
    /// </summary>
    /// <remarks>
    /// Every match the bot has already fought carries a snapshot of how it looked, so
    /// clawing the cosmetic back cannot rewrite that history — it would only remove the
    /// chassis from a player mid-ladder while every replay still shows it. Recording the
    /// state and leaving the entitlement is the honest version; repeat abuse is a decision
    /// for a person looking at these rows.
    /// </remarks>
    public async Task RefundAsync(
        string provider,
        string providerReference,
        CancellationToken cancellationToken)
    {
        Purchase? purchase = await db.Purchases.SingleOrDefaultAsync(
            row => row.Provider == provider && row.ProviderReference == providerReference,
            cancellationToken);
        if (purchase is null) return;

        purchase.State = PurchaseStates.Refunded;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Purchase {PurchaseId} refunded; entitlement retained by policy", purchase.Id);
    }
}
