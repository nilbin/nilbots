using BotArena.App.Cosmetics;

namespace BotArena.App.Store;

/// <summary>Where to send the buyer, and the reference to expect back.</summary>
public sealed record CheckoutSession(string Url, string ProviderReference);

/// <summary>
/// Taking money, without saying who from.
/// <para>
/// The interface exists so the provider stays one class, the same way
/// <c>IPushTransport</c> keeps Expo swappable. Nothing above it knows whether the money
/// arrived through Paddle, an App Store receipt, or a manual grant — and that matters
/// sooner here than it did for push, because iOS in-app purchase is a *second* provider
/// rather than a replacement: the App Store requires its own flow for purchases made in
/// the app while the web keeps this one.
/// </para>
/// <para>
/// **Merchant of record, not a payment processor.** Whoever implements this is the legal
/// seller to the customer and owes the VAT and sales tax in every jurisdiction the buyer
/// might be in — roughly 100 of them once the US and the digital-services regimes beyond
/// the EU are counted. See <c>docs/MONETIZATION-OPTIONS.md</c>; that decision is the reason
/// this interface is thin.
/// </para>
/// </summary>
public interface IStorePaymentProvider
{
    /// <summary>Provider name recorded on the purchase — `paddle`, `manual`.</summary>
    string Name { get; }

    /// <summary>Whether this deployment can actually sell. False hides the store.</summary>
    bool IsConfigured { get; }

    Task<CheckoutSession> CreateCheckoutAsync(
        Guid userId,
        CosmeticPack pack,
        CancellationToken cancellationToken);
}

/// <summary>
/// The provider for a deployment that is not selling anything.
/// <para>
/// The default, and every environment is in this state until credentials exist. The store
/// endpoint reports <c>open: false</c>, the site renders no buy button, and attempting a
/// checkout throws rather than half-working — which is what should happen, because a
/// checkout that silently does nothing is indistinguishable from a payment that failed.
/// </para>
/// </summary>
public sealed class ClosedStore : IStorePaymentProvider
{
    public string Name => "none";
    public bool IsConfigured => false;

    public Task<CheckoutSession> CreateCheckoutAsync(
        Guid userId,
        CosmeticPack pack,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "No payment provider is configured; the store is closed.");
}
