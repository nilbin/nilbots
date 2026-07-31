namespace BotArena.Engine;

/// <summary>
/// Stable Core identity. Ordinals are source-local and never reused within a
/// match, so possession, flight, drop, and bank facts can be joined exactly.
/// </summary>
public readonly record struct ArcRelayCoreId
{
    public ArcRelayCoreId(string sourceWellId, int sourceOrdinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceWellId);
        if (sourceOrdinal < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceOrdinal));
        SourceWellId = sourceWellId;
        SourceOrdinal = sourceOrdinal;
    }

    public string SourceWellId { get; }
    public int SourceOrdinal { get; }
    public override string ToString() => $"{SourceWellId}:{SourceOrdinal}";
}
