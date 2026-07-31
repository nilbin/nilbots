using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// One tick's complete reply from a mind: a decision MAP, not one action.
///
/// <para>It may be SHORTER than the participant's live-body set — every body
/// the mind did not command keeps the host's pre-filled wait — and it may name
/// bodies that are no longer live, which the host records as
/// <c>Rejected</c> rather than faulting. Both are recorded and neither is
/// elided, because the distinction between "the engine refused this at runtime"
/// and "this document is malformed" is what keeps honest replays verifiable and
/// forged ones refusable.</para>
///
/// <para><see cref="Tick"/> is echoed from the observation. A stale or wrong
/// tick fails the exchange outright: under a correlated request/reply protocol
/// there is exactly one reply per released request, and a reply that answers a
/// different tick is not a late answer, it is a broken guest.</para>
/// </summary>
public sealed record MindDecisions
{
    /// <summary>Creates one mind reply.</summary>
    /// <param name="schemaVersion">Negotiated decision schema version.</param>
    /// <param name="tick">The observation's tick, echoed exactly.</param>
    /// <param name="commands">Commands in submission order.</param>
    /// <param name="intents">
    /// RESERVED inter-mind declarations. Must be empty: no shipped format has
    /// allied minds, so the encoder refuses a non-empty collection outright
    /// rather than writing bytes no host will honour.
    /// </param>
    /// <param name="debugMessage">
    /// Optional mind-scoped diagnostic text collected from
    /// <see cref="MindContext.Debug"/>. A mind reasons once per tick over the
    /// whole army, so its diagnostics belong to the turn rather than to any one
    /// body.
    /// </param>
    public MindDecisions(
        int schemaVersion,
        int tick,
        IEnumerable<MindCommand> commands,
        IEnumerable<MindDeclaredIntent>? intents = null,
        string? debugMessage = null)
    {
        if (schemaVersion
            != GenericMindContractVersions.DecisionSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                "Mind decisions require the exact profile's decision schema.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(tick);

        SchemaVersion = schemaVersion;
        Tick = tick;
        Commands = GenericActorDynamicValueRules.Snapshot(
            commands,
            nameof(commands));
        Intents = intents is null
            ? []
            : GenericActorDynamicValueRules.Snapshot(
                intents,
                nameof(intents));
        DebugMessage = debugMessage is null
            ? null
            : GenericActorDynamicValueRules.Text(
                debugMessage,
                4096,
                nameof(debugMessage));
    }

    /// <summary>Negotiated decision schema version.</summary>
    public int SchemaVersion { get; }

    /// <summary>The observation's tick, echoed exactly.</summary>
    public int Tick { get; }

    /// <summary>Commands in submission order.</summary>
    public ImmutableArray<MindCommand> Commands { get; }

    /// <summary>
    /// RESERVED inter-mind declarations; always empty under every shipped
    /// format.
    /// </summary>
    public ImmutableArray<MindDeclaredIntent> Intents { get; }

    /// <summary>
    /// Mind-scoped diagnostic text for this tick, or <see langword="null"/>.
    /// Non-authoritative and never an action parameter.
    /// </summary>
    public string? DebugMessage { get; }
}

/// <summary>
/// RESERVED. One declaration a mind would publish to its ALLIED minds, arriving
/// one tick later so that observations stay frozen before any same-tick
/// decision executes.
///
/// <para>Nothing may declare one today, because no shipped format has allied
/// minds and a mind never needs to negotiate with itself. The type and its
/// field IDs exist now because reusing a field ID later is the one change this
/// protocol cannot make without a new version.</para>
/// </summary>
/// <param name="TagId">Lowercase kebab semantic ID, at most 32 UTF-8 bytes.</param>
/// <param name="Value">The declaration's payload.</param>
public sealed record MindDeclaredIntent(string TagId, long Value);
