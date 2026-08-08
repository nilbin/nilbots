namespace BotArena.Engine;

/// <summary>
/// Closed serializer-neutral binding between one actor game-mode semantic and
/// the typed regions of one resolved generation-3 map.
/// </summary>
public abstract record ActorModeMapBindingDefinition
{
    internal ActorModeMapBindingDefinition()
    {
    }

    public abstract ActorModeMapBindingDefinitionKind Kind { get; }

    public enum ActorModeMapBindingDefinitionKind
    {
        Deathmatch = 0,
        Frontline = 1,
        ArcRelay = 2,
    }
}
