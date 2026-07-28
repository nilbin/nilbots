namespace BotArena.Engine;

/// <summary>
/// Deathmatch has no mode-owned map regions. Spawn and transition-region
/// bindings remain part of the common resolved match contract.
/// </summary>
public sealed record DeathmatchActorModeMapBindingDefinition
    : ActorModeMapBindingDefinition
{
    public override ActorModeMapBindingDefinitionKind Kind =>
        ActorModeMapBindingDefinitionKind.Deathmatch;
}
