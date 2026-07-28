using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Mode-neutral source state supplied to the same-life transition kernel.
/// Runtime and mode ownership remain with the caller.
/// </summary>
public sealed record ActorSameLifeTransitionActorSnapshot(
    ActorIdentity ActorId,
    int ParticipantId,
    int Generation,
    string FormId,
    Position Position,
    Direction Facing,
    int Health,
    int Cooldown,
    int? Energy,
    bool HasPriorSameLifeTransition,
    ImmutableArray<string> IrreversibleReturnFormIds,
    ActorSameLifeTransitionReservation? PendingSameLifeTransition);
