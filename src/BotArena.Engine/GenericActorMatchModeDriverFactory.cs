namespace BotArena.Engine;

/// <summary>
/// Closed construction boundary for supported executable mode/binding pairs.
/// Adding a mode requires an explicit typed pair here.
/// </summary>
internal static class GenericActorMatchModeDriverFactory
{
    public static IGenericActorMatchModeDriver Create(
        ActorResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return (definition.Rules.GameMode, definition.ModeMapBinding) switch
        {
            (
                DeathmatchGameModeDefinition mode,
                DeathmatchActorModeMapBindingDefinition
            ) => new DeathmatchActorMatchModeDriver(
                definition.Topology,
                mode),
            _ => throw new ArgumentException(
                "GenericActorMatchSession does not support this exact game-mode and map-binding pair.",
                nameof(definition)),
        };
    }
}
