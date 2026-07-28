using BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime;

/// <summary>
/// Diagnostic generic actor factory for SDK development and parity tests.
/// </summary>
public sealed class InProcessGenericActorRuntimeFactory(
    Func<Sdk.IGenericActorBot> botFactory) : IGenericActorRuntimeFactory
{
    public IGenericActorRuntime CreateRuntime() =>
        new InProcessGenericActorRuntime(botFactory);
}
