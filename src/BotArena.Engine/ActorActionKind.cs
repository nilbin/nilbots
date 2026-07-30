namespace BotArena.Engine;

/// <summary>Closed semantic action families understood by actor contracts.</summary>
public enum ActorActionKind
{
    Wait = 0,
    Movement = 1,
    Rotation = 2,
    Attack = 3,
    Fabrication = 4,
    SameLifeTransition = 5,
    Replication = 6,

    /// <summary>
    /// A verb the MODE owns rather than the world: it moves the mode's own
    /// state and nothing on the board. Additive append (DECISIONS #156's
    /// discipline), so every contract that declares no such action behaves
    /// exactly as it always has.
    /// </summary>
    ModeInvestment = 7,
}
