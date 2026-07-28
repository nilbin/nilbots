namespace BotArena.Engine;

/// <summary>
/// A movement-layer capability declared by a generic actor contract.
/// Declaring compatibility does not implement movement semantics; a session
/// must explicitly support the selected layer before admitting a match.
/// </summary>
public enum ActorMovementLayer
{
    Ground = 0,
    Air = 1,
}
