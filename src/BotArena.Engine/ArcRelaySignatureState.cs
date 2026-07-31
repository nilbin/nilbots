using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Current visible tell, channel, field, or construct created by one class
/// signature. Positions are canonical and carry the complete active shape.
/// </summary>
public sealed record ArcRelaySignatureState
{
    public ArcRelaySignatureState(
        string operationId,
        string signatureId,
        ArcRelaySignatureDefinition.SignatureKind kind,
        ActorIdentity ownerActorId,
        int ownerTeamId,
        SignaturePhase phase,
        int startedTick,
        int? completesAtTick,
        int? endsAtTick,
        IEnumerable<Position> positions,
        ActorIdentity? targetActorId = null,
        int remainingCapacity = 0,
        bool suppressed = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureId);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (ownerTeamId < 0 || ownerActorId.TeamId != ownerTeamId)
            throw new ArgumentOutOfRangeException(nameof(ownerTeamId));
        if (!Enum.IsDefined(phase))
            throw new ArgumentOutOfRangeException(nameof(phase));
        if (startedTick < 0 || completesAtTick < 0 || endsAtTick < 0)
            throw new ArgumentOutOfRangeException(nameof(startedTick));
        if (remainingCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(remainingCapacity));
        ArgumentNullException.ThrowIfNull(positions);
        Position[] shape = [.. positions];
        if (shape.Length == 0 || shape.Distinct().Count() != shape.Length)
        {
            throw new ArgumentException(
                "A signature state needs a non-empty unique position shape.",
                nameof(positions));
        }

        OperationId = operationId;
        SignatureId = signatureId;
        Kind = kind;
        OwnerActorId = ownerActorId;
        OwnerTeamId = ownerTeamId;
        Phase = phase;
        StartedTick = startedTick;
        CompletesAtTick = completesAtTick;
        EndsAtTick = endsAtTick;
        Positions = shape.OrderBy(value => value.Y)
            .ThenBy(value => value.X).ToImmutableArray();
        TargetActorId = targetActorId;
        RemainingCapacity = remainingCapacity;
        Suppressed = suppressed;
    }

    public string OperationId { get; }
    public string SignatureId { get; }
    public ArcRelaySignatureDefinition.SignatureKind Kind { get; }
    public ActorIdentity OwnerActorId { get; }
    public int OwnerTeamId { get; }
    public SignaturePhase Phase { get; }
    public int StartedTick { get; }
    public int? CompletesAtTick { get; }
    public int? EndsAtTick { get; }
    public ImmutableArray<Position> Positions { get; }
    public ActorIdentity? TargetActorId { get; }
    public int RemainingCapacity { get; }
    public bool Suppressed { get; }

    public bool Equals(ArcRelaySignatureState? other) =>
        other is not null
        && OperationId == other.OperationId
        && SignatureId == other.SignatureId
        && Kind == other.Kind
        && OwnerActorId == other.OwnerActorId
        && OwnerTeamId == other.OwnerTeamId
        && Phase == other.Phase
        && StartedTick == other.StartedTick
        && CompletesAtTick == other.CompletesAtTick
        && EndsAtTick == other.EndsAtTick
        && Positions.SequenceEqual(other.Positions)
        && TargetActorId == other.TargetActorId
        && RemainingCapacity == other.RemainingCapacity
        && Suppressed == other.Suppressed;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(OperationId, StringComparer.Ordinal);
        hash.Add(SignatureId, StringComparer.Ordinal);
        hash.Add(Kind);
        hash.Add(OwnerActorId);
        hash.Add(OwnerTeamId);
        hash.Add(Phase);
        hash.Add(StartedTick);
        hash.Add(CompletesAtTick);
        hash.Add(EndsAtTick);
        foreach (Position position in Positions)
            hash.Add(position);
        hash.Add(TargetActorId);
        hash.Add(RemainingCapacity);
        hash.Add(Suppressed);
        return hash.ToHashCode();
    }

    public enum SignaturePhase
    {
        Tell = 0,
        Active = 1,
        Channel = 2,
        InFlight = 3,
    }
}
