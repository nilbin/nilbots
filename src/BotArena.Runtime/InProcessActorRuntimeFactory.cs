using BotArena.Engine;

namespace BotArena.Runtime;

/// <summary>
/// Diagnostic actor factory for SDK development and parity tests. The
/// canonical player environment remains <c>WasmActorRuntimeFactory</c>.
/// </summary>
public sealed class InProcessActorRuntimeFactory(
    Func<Sdk.IActorBot> botFactory) : IActorRuntimeFactory
{
    public IActorRuntime CreateRuntime() =>
        new InProcessActorRuntime(botFactory);
}
