#if BOTARENA_ACTOR_CONTRACTS
namespace BotArena.ActorContracts;
#else
namespace BotArena.Sdk;
#endif

/// <summary>
/// Contract versions and budgets for the participant-scoped MIND programming
/// model (DECISIONS #190/#191, <c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c>
/// §1.2/§4.2/§4.3). The profile sits BESIDE
/// <see cref="GenericActorContractVersions"/> rather than superseding it: the
/// per-life generation stays byte-exact so the hosted playlist, the eight
/// measured lineages, and every frozen cohort remain valid.
/// <para>P0 lands these values inert. Nothing negotiates, encodes, or enforces
/// them until P2 ships the SDK/Guest/Runtime half.</para>
/// </summary>
#if BOTARENA_ACTOR_CONTRACTS
internal static class GenericMindContractVersions
#else
public static class GenericMindContractVersions
#endif
{
    /// <summary>Exact all-or-nothing mind contract profile identifier.</summary>
    public const string ContractProfileId = "generic-mind-match-1";

    /// <summary>
    /// Framing protocol, CARRIED unchanged from the actor generation: the
    /// 12-byte NBV2 header, tagged fields, correlated request/reply, frame
    /// caps, and the typed Fault/Unsupported terminals are all identical. New
    /// message types are a profile matter, not a framing matter (§1.2).
    /// </summary>
    public const string RuntimeProtocolVersion = "1.0";

    /// <summary>
    /// Runtime CONFIGURATION 2.0 — minted because the fuel formula, the linear
    /// memory ceiling, and the instance topology all change (§4.2/§4.3).
    /// Configuration 1.0 stays exactly as pinned for the per-life generation.
    /// </summary>
    public const string RuntimeConfigurationVersion = "2.0";

    /// <summary>Fresh: participant-scoped host behaviour.</summary>
    public const int RuntimeContractVersion = 1;

    /// <summary>Fresh: MindStart, not the per-life MatchStart.</summary>
    public const int MatchStartSchemaVersion = 1;

    /// <summary>Fresh: union-once observation carrying <c>Bodies[]</c>.</summary>
    public const int ObservationSchemaVersion = 1;

    /// <summary>Fresh: the decision MAP.</summary>
    public const int DecisionSchemaVersion = 1;

    /// <summary>
    /// CARRIED OVER, unchanged, and load-bearing: the rules, map, forms,
    /// actions, transitions, lifecycle, mode and economy are identical. The
    /// mind plays the same game. Carrying schema 2 is what keeps
    /// <c>frontline-labs-1</c>'s rules fingerprint valid on the mind profile
    /// and what makes the §7.2 null pin mean anything at all.
    /// </summary>
    public const int MatchContractSchemaVersion =
        GenericActorContractVersions.MatchContractSchemaVersion;

    /// <summary>
    /// Replay 3, carried: replay 3 grows a <c>mindTurns</c> alternative to
    /// <c>actorTurns</c>, keyed by the contract profile (§5.1).
    /// </summary>
    public const int ReplayFormatVersion =
        GenericActorContractVersions.ReplayFormatVersion;

    /// <summary>
    /// Once-per-tick shared-reasoning allowance, available even on a tick the
    /// mind owns no live body — which is what makes the §2.7 tick invariant
    /// affordable. 1.25x one body's budget.
    /// </summary>
    public const long BaseTickFuel = 250_000_000;

    /// <summary>
    /// EXACTLY today's per-life budget, so per-body compute is preserved
    /// unchanged and the null pin cannot be confounded by a compute
    /// difference (§4.2).
    /// </summary>
    public const long PerBodyTickFuel = 200_000_000;

    /// <summary>
    /// Paid once per participant per match instead of once per life — the
    /// 25-40x startup saving in §0.
    /// </summary>
    public const long StartupFuel = 5_000_000_000;

    /// <summary>
    /// 64 MiB -> 128 MiB. The mind is the only instance for the participant
    /// and now holds match-long belief state; even doubled, per-participant
    /// memory falls 4.5x (§4.3).
    /// </summary>
    public const long LinearMemoryBytes = 128L * 1024 * 1024;

    /// <summary>
    /// Role tags are capped at 24 UTF-8 bytes rather than the 64-byte semantic
    /// ID cap, because this is a display label sent per body per tick and the
    /// budget should be visibly tight (§12.1).
    /// </summary>
    public const int MaxRoleTagUtf8Bytes = 24;

    /// <summary>Declared-intent tag cap (§11.1). Reserved; unused in v1.</summary>
    public const int MaxIntentTagUtf8Bytes = 32;

    /// <summary>
    /// At most eight declared intents leave a mind per tick, and at most eight
    /// per allied participant arrive (§11.1). Reserved; both collections are
    /// empty in v1 and a non-empty submission is Rejected, never Faulted.
    /// </summary>
    public const int MaxDeclaredIntentsPerTick = 8;

    /// <summary>
    /// The one-tick delay is the design, not a limitation: tick T's
    /// <c>alliedIntents</c> come from tick T-1's decision frames, which
    /// preserves the frozen-observation invariant and makes intents
    /// replay-verifiable (§11.2).
    /// </summary>
    public const int AlliedIntentDeliveryDelayTicks = 1;

    /// <summary>
    /// Tick fuel as a pure function of authoritative tick-start state:
    /// <c>250M + 200M x liveOwnBodies</c>. <paramref name="liveOwnBodies"/> is
    /// fixed by PrepareTick before the call and recorded in the replay, so two
    /// hosts compute the same number (§4.2).
    /// </summary>
    public static long TickFuel(int liveOwnBodies)
    {
        if (liveOwnBodies < 0)
            throw new ArgumentOutOfRangeException(nameof(liveOwnBodies));
        return checked(BaseTickFuel + (PerBodyTickFuel * liveOwnBodies));
    }
}
