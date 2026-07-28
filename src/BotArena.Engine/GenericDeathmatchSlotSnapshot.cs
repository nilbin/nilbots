namespace BotArena.Engine;

/// <summary>Immutable stable-slot state exposed by a generic session.</summary>
public sealed record GenericDeathmatchSlotSnapshot(
    int TeamId,
    int UnitId,
    int ParticipantId,
    GenericActorRuntimeObservation.UnitSlotState State);
