namespace BotArena.Engine;

/// <summary>One generic actor participant and its replay provenance.</summary>
public sealed record GenericActorParticipantConfiguration
{
    public required int ParticipantId { get; init; }
    public required int TeamId { get; init; }
    public required string Name { get; init; }

    /// <summary>
    /// Per-LIFE runtime owner, used by the <c>generic-actor-match-2</c>
    /// profile. A contract resolved on the mind profile ignores it and uses
    /// <see cref="MindRuntimeFactory"/> instead.
    /// </summary>
    public IGenericActorRuntimeFactory? RuntimeFactory { get; init; }

    /// <summary>
    /// Per-PARTICIPANT runtime owner, required by and only by the
    /// <c>generic-mind-match-1</c> profile (DECISIONS #191). One factory
    /// produces one mind that lives the whole match and commands every body
    /// this participant owns.
    /// <para>The two factories sit side by side rather than replacing one
    /// another because the two profiles coexist BESIDE each other: the same
    /// configuration record can describe a participant on either.</para>
    /// </summary>
    public IGenericMindRuntimeFactory? MindRuntimeFactory { get; init; }

    public string RuntimeKind { get; init; } = "in-process-generic-actor";
    /// <summary>
    /// Content digest for submitted artifacts. Local/built-in runtimes may
    /// leave this null; competitive admission can require a digest without
    /// forcing the simulation layer to invent provenance.
    /// </summary>
    public string? ArtifactHash { get; init; }
    /// <summary>
    /// Content digest of immutable participant-local mind data. Product modes
    /// use this to bind a separately authored sheet to the replay without
    /// rebuilding or pretending that it is part of the executable artifact.
    /// </summary>
    public string? MindDataHash { get; init; }
    /// <summary>
    /// Participant-local program data delivered once to a mind. Evaluation
    /// harnesses and product modes own the schema at their boundary; Engine
    /// treats it as opaque bytes and records <see cref="MindDataHash"/>.
    /// </summary>
    public System.Collections.Immutable.ImmutableArray<byte> MindEvaluationData
    { get; init; } = [];
    public string Accent { get; init; } = "#38bdf8";
    public string? LookId { get; init; }
    public string? ProjectileLookId { get; init; }
}
