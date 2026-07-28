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
        string canonicalJson,
        string replayHash)
    {
        CanonicalJson = canonicalJson;
        ReplayHash = replayHash;
    }

    /// <summary>
    /// Exact canonical replay-v3 envelope, including <see cref="ReplayHash"/>.
    /// </summary>
    public string CanonicalJson { get; }

    /// <summary>Lowercase SHA-256 of the canonical replay payload.</summary>
    public string ReplayHash { get; }

    public static GenericActorReplayDocument Create(
        GenericActorMatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsCompleted)
        {
            throw new InvalidOperationException(
                "A replay document requires a completed generic actor match.");
        }

        return Create(session.Chronology);
    }

    public static GenericActorReplayDocument Create(
        GenericActorMatchChronology chronology)
    {
        ArgumentNullException.ThrowIfNull(chronology);
        if (chronology.Partial)
        {
            throw new InvalidOperationException(
                "A replay document requires a completed generic actor chronology.");
        }

        ReplayV3 replay = ReplayV3Projection.Project(chronology);
        string replayHash = ReplayV3Serializer.ComputeHash(replay);
        string canonicalJson = ReplayV3Serializer.ToJson(
            replay with { ReplayHash = replayHash });
        return new GenericActorReplayDocument(
            canonicalJson,
            replayHash);
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
