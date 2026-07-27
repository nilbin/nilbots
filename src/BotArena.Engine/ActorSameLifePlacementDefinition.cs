using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Queue- and completion-time placement legality for a same-life form
/// transition. The tagged shape lets later movement-layer transitions add a
/// supported position policy without changing the aggregate boundary.
/// </summary>
public sealed record ActorSameLifePlacementDefinition
{
    public ActorSameLifePlacementDefinition(
        PositionContinuityKind positionContinuity,
        LegalityEvaluationKind legalityEvaluation,
        IEnumerable<ActorMapTileTagDefinition.TileTagKind> requiredTileTags,
        IEnumerable<ActorMapTileTagDefinition.TileTagKind> forbiddenTileTags,
        FailedCompletionKind failedCompletion)
    {
        if (!Enum.IsDefined(positionContinuity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(positionContinuity));
        }
        if (!Enum.IsDefined(legalityEvaluation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(legalityEvaluation));
        }
        ArgumentNullException.ThrowIfNull(requiredTileTags);
        ArgumentNullException.ThrowIfNull(forbiddenTileTags);
        if (!Enum.IsDefined(failedCompletion))
            throw new ArgumentOutOfRangeException(nameof(failedCompletion));

        ImmutableArray<ActorMapTileTagDefinition.TileTagKind> required =
            SnapshotTags(requiredTileTags, nameof(requiredTileTags));
        ImmutableArray<ActorMapTileTagDefinition.TileTagKind> forbidden =
            SnapshotTags(forbiddenTileTags, nameof(forbiddenTileTags));
        if (required.Intersect(forbidden).Any())
        {
            throw new ArgumentException(
                "A tile tag cannot be both required and forbidden.");
        }

        PositionContinuity = positionContinuity;
        LegalityEvaluation = legalityEvaluation;
        RequiredTileTags = required;
        ForbiddenTileTags = forbidden;
        FailedCompletion = failedCompletion;
    }

    public PositionContinuityKind PositionContinuity { get; }
    public LegalityEvaluationKind LegalityEvaluation { get; }
    public ImmutableArray<ActorMapTileTagDefinition.TileTagKind>
        RequiredTileTags { get; }
    public ImmutableArray<ActorMapTileTagDefinition.TileTagKind>
        ForbiddenTileTags { get; }
    public FailedCompletionKind FailedCompletion { get; }

    public enum PositionContinuityKind
    {
        SameOccupiedGroundTile = 0,
    }

    public enum LegalityEvaluationKind
    {
        QueueAndCompletionTileTags = 0,
    }

    public enum FailedCompletionKind
    {
        CancelAndRemainInSourceForm = 0,
    }

    private static ImmutableArray<ActorMapTileTagDefinition.TileTagKind>
        SnapshotTags(
            IEnumerable<ActorMapTileTagDefinition.TileTagKind> values,
            string parameterName)
    {
        ActorMapTileTagDefinition.TileTagKind[] snapshot = [.. values];
        if (snapshot.Any(value => !Enum.IsDefined(value)))
            throw new ArgumentOutOfRangeException(parameterName);
        if (snapshot.Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Tile-tag sets cannot contain duplicates.",
                parameterName);
        }
        return snapshot.Order().ToImmutableArray();
    }
}
