using BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime;

/// <summary>
/// Diagnostic mind factory for SDK development and cross-runtime parity tests.
/// Unlike the per-life factory beside it, this one produces exactly ONE runtime
/// per participant per match — the topology change is the profile.
/// </summary>
public sealed class InProcessGenericMindRuntimeFactory(
    Func<Sdk.IGenericMindBot> botFactory,
    bool trustedArcRelayStockProjection = false) : IGenericMindRuntimeFactory
{
    /// <summary>
    /// True only for the frozen legacy stock minds whose observation usage is
    /// deliberately smaller than the public MIND contract. Tactical minds
    /// consume events and the complete visible map and must leave this false.
    /// </summary>
    public bool UsesTrustedArcRelayStockProjection =>
        trustedArcRelayStockProjection;

    public IGenericMindRuntime CreateRuntime() =>
        new InProcessGenericMindRuntime(
            botFactory,
            trustedArcRelayStockProjection);
}
