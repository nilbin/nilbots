using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Public generation-3 replay boundary for completed generic actor matches.
/// The internal replay DTO graph remains private to Engine while callers get
/// exact canonical wire JSON, its verified payload hash, and a safe
/// result-redacted broadcast prefix.
/// </summary>
public sealed record GenericActorReplayDocument
{
    private GenericActorReplayDocument(
        byte[] canonicalUtf8,
        string replayHash)
    {
        _canonicalUtf8 = canonicalUtf8;
        ReplayHash = replayHash;
    }

    private readonly byte[] _canonicalUtf8;
    private string? _canonicalJson;

    /// <summary>
    /// Exact canonical replay-v3 envelope, including <see cref="ReplayHash"/>.
    /// </summary>
    public string CanonicalJson =>
        _canonicalJson ??= System.Text.Encoding.UTF8.GetString(
            _canonicalUtf8);

    /// <summary>
    /// Exact canonical replay-v3 UTF-8 bytes. Byte-oriented archival paths use
    /// this boundary so a large replay need not be transcoded to UTF-16 and
    /// immediately back to UTF-8.
    /// </summary>
    public ReadOnlyMemory<byte> CanonicalUtf8 => _canonicalUtf8;

    /// <summary>Lowercase SHA-256 of the canonical replay payload.</summary>
    public string ReplayHash { get; }

    public bool Equals(GenericActorReplayDocument? other) =>
        ReferenceEquals(this, other)
        || other is not null
        && string.Equals(ReplayHash, other.ReplayHash, StringComparison.Ordinal)
        && _canonicalUtf8.AsSpan().SequenceEqual(other._canonicalUtf8);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(ReplayHash);

    public static GenericActorReplayDocument Create(
        GenericActorMatchSession session)
        => Create(session, presentation: null);

    /// <summary>
    /// Creates a completed replay with optional presentation that remains
    /// outside the resolved gameplay contract and its fingerprints.
    /// </summary>
    public static GenericActorReplayDocument Create(
        GenericActorMatchSession session,
        GenericActorReplayPresentation? presentation)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsCompleted)
        {
            throw new InvalidOperationException(
                "A replay document requires a completed generic actor match.");
        }

        return Create(session.Chronology, presentation);
    }

    public static GenericActorReplayDocument Create(
        GenericActorMatchChronology chronology)
        => Create(chronology, presentation: null);

    /// <summary>
    /// Creates a completed replay with optional presentation that remains
    /// outside the resolved gameplay contract and its fingerprints.
    /// </summary>
    public static GenericActorReplayDocument Create(
        GenericActorMatchChronology chronology,
        GenericActorReplayPresentation? presentation)
    {
        ArgumentNullException.ThrowIfNull(chronology);
        if (chronology.Partial)
        {
            throw new InvalidOperationException(
                "A replay document requires a completed generic actor chronology.");
        }

        ReplayV3 replay = ReplayV3Projection.Project(
            chronology,
            Presentation(presentation));
        (byte[] canonicalUtf8, string replayHash) =
            ReplayV3Serializer.CreateCanonicalDocument(replay);
        return new GenericActorReplayDocument(
            canonicalUtf8,
            replayHash);
    }

    private static ReplayV3.PresentationMetadata? Presentation(
        GenericActorReplayPresentation? presentation)
    {
        if (presentation is null)
            return null;

        return new ReplayV3.PresentationMetadata(
            presentation.ThemeId,
            presentation.Map is null
                ? null
                : new ReplayV3.MapPresentationMetadata(
                    presentation.Map.BoundaryWall,
                    presentation.Map.InteriorWall,
                    presentation.Map.WallGroups
                        .Select(group =>
                            new ReplayV3.WallGroupPresentationMetadata(
                                group.Family,
                                group.Tiles
                                    .Select(tile =>
                                        new ReplayV3.PositionValue(
                                            tile.X,
                                            tile.Y))
                                    .ToImmutableArray()))
                        .ToImmutableArray()),
            presentation.Forms
                .Select(form =>
                    new ReplayV3.FormPresentationMetadata(
                        form.FormId,
                        form.LookId,
                        form.ProjectileLookId))
                .ToImmutableArray());
    }

    /// <summary>
    /// Verifies a complete canonical replay-v3 envelope, including its payload
    /// hash, exact nested contract fingerprints, canonical ordering, and
    /// closed vocabularies.
    /// </summary>
    public static bool VerifyHash(
        string canonicalReplayJson,
        out string? failure)
    {
        ArgumentNullException.ThrowIfNull(canonicalReplayJson);
        return ReplayV3Serializer.VerifyHash(
            canonicalReplayJson,
            out failure);
    }

    /// <summary>
    /// Creates a canonical, explicitly unhashed broadcast prefix from a
    /// verified complete replay-v3 envelope. A presentation clock beyond the
    /// terminal tick safely exposes all available ticks, never terminal facts.
    /// </summary>
    public static string CreatePartialPrefix(
        string canonicalReplayJson,
        int visibleTickCount)
    {
        if (visibleTickCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibleTickCount));
        }

        ReplayV3 complete =
            ReplayV3Serializer.ReadCanonicalComplete(canonicalReplayJson);
        ReplayV3 partial = complete with
        {
            Ticks = complete.Ticks
                .Take(Math.Min(visibleTickCount, complete.Ticks.Length))
                .ToImmutableArray(),
            Result = null,
            ReplayHash = null,
            Partial = true,
        };
        return ReplayV3Serializer.ToJson(partial);
    }
}
