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
    public IGenericMindRuntime CreateRuntime() =>
        new InProcessGenericMindRuntime(
            botFactory,
            trustedArcRelayStockProjection);
}
