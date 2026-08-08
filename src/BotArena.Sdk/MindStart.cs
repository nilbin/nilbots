using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>
/// Immutable initialization delivered once to one mind, before tick 0. The
/// mind instance then lives the entire match; its own fields are the persistent
/// memory and there is no memory API to learn.
///
/// <para>Two things are deliberately ABSENT, and knowing why saves you a
/// wrong assumption. The slot table's STATE is not here — it is published
/// every tick in <see cref="MindContext.Slots"/>, so a slot's due tick,
/// readiness and pending fabrication are always current rather than a
/// start-time snapshot you would have to maintain. And a life's ORIGIN is not
/// here either — under the mind an origin is a per-BODY fact
/// (<see cref="MindBody.Origin"/>) delivered on the tick that body first
/// appears, not a start-time fact about "me".</para>
/// </summary>
public sealed record MindStart
{
    /// <summary>MindStart payload schema selected during negotiation.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Mind runtime-contract version selected during negotiation.</summary>
    public required int RuntimeContractVersion { get; init; }

    /// <summary>
    /// The submitted participant this mind IS. Commands can only ever reach
    /// bodies this participant controls: the decision map is keyed by
    /// <c>(unitId, lifeId)</c> WITHIN the participant, so "move my ally's body"
    /// is not merely refused — it is unspellable.
    /// </summary>
    public required int ParticipantId { get; init; }

    /// <summary>The scoring team this participant plays for.</summary>
    public required int TeamId { get; init; }

    /// <summary>
    /// Other participants sharing this mind's scoring team, ascending. EMPTY in
    /// head-to-head and in free-for-all, because there is exactly one
    /// participant per scoring team in both. It is non-empty only in a team
    /// format, where the other entries are allied MINDS whose bodies appear in
    /// <see cref="MindContext.Allies"/> and which you cannot command.
    /// </summary>
    public required ImmutableArray<int> AlliedParticipantIds { get; init; }

    /// <summary>
    /// Deterministic private stream for this mind, derived in the PARTICIPANT
    /// domain — one stream for the whole match rather than one per life. Read
    /// it through <see cref="MindContext.Random"/> rather than seeding your own
    /// generator from this value.
    /// </summary>
    public required ulong MindRandomSeed { get; init; }

    /// <summary>
    /// Root seed of the scoring team's SHARED deterministic stream, delivered
    /// identically to every mind on the team. Inside a single mind it is
    /// pointless — you do not need to agree with yourself — and in the shipped
    /// head-to-head format it is therefore inert. It becomes load-bearing the
    /// day a team format is admitted, because two separately authored minds
    /// cannot compute each other's plan and a shared stream the opponent cannot
    /// derive is the only channel that exists at tick 0. Read it through
    /// <see cref="MindContext.TeamRandom"/>.
    /// </summary>
    public required ulong TeamRandomSeed { get; init; }

    /// <summary>
    /// Frozen rules, map, format, topology, deployment and mode binding —
    /// byte-identical to what the per-life profile delivers, same canonical
    /// JSON and same fingerprints. The mind plays the same game; only who is
    /// driving it changed. Read thresholds, prices, ranges and cooldowns from
    /// here rather than hard-coding them.
    /// </summary>
    public required GenericActorResolvedMatchContract Contract { get; init; }

    /// <summary>
    /// Optional participant-local, deterministic evaluation data delivered
    /// once before tick 0. Gate 3 uses this to link a separately hashed
    /// provisional sheet to a frozen algorithm without rebuilding the WASM.
    /// It is not the future player-facing sheet schema.
    /// </summary>
    public ImmutableArray<byte> EvaluationData { get; init; } = [];
}
