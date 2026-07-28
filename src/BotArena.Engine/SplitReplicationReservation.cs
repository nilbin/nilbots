using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>Canonical all-or-nothing reservation for one pending Split.</summary>
public sealed record SplitReplicationReservation(
    ActorIdentity SourceActorId,
    int ParticipantId,
    int SourceGeneration,
    string SourceFormId,
    Position SourcePosition,
    Direction SourceFacing,
    string TransitionId,
    string OperationId,
    int QueuedTick,
    int DueTick,
    ImmutableArray<SplitReplicationReservedDescendant> Descendants);
