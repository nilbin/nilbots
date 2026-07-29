#if BOTARENA_ACTOR_CONTRACTS
namespace BotArena.ActorContracts;
#else
namespace BotArena.Sdk;
#endif

/// <summary>
/// Contract versions for the generic actor-match programming model. The
/// framing protocol remains actor protocol 1.0; negotiation selects this
/// complete tuple before the host sends a version-specific MatchStart.
/// </summary>
#if BOTARENA_ACTOR_CONTRACTS
internal static class GenericActorContractVersions
#else
public static class GenericActorContractVersions
#endif
{
    internal const int MaxCanonicalContractDepth = 64;
    internal const int MaxSemanticIdBytes = 64;
    internal const string GenericV2ContractProfileId =
        "generic-actor-match-2";
    internal const int GenericV2RuntimeContractVersion = 2;
    internal const int GenericV2MatchStartSchemaVersion = 2;
    internal const int GenericV2ObservationSchemaVersion = 2;
    internal const int GenericV2DecisionSchemaVersion = 2;
    internal const int GenericV2MatchContractSchemaVersion = 2;
    /// <summary>Exact all-or-nothing actor contract profile identifier.</summary>
    public const string ContractProfileId = "generic-actor-match-3";
    /// <summary>Actor framing protocol version used by this profile.</summary>
    public const string RuntimeProtocolVersion = "1.0";
    /// <summary>Runtime configuration schema version used by this profile.</summary>
    public const string RuntimeConfigurationVersion = "1.0";
    /// <summary>
    /// Canonical contract budget inside the one-MiB host frame. The reserved
    /// 1024 bytes conservatively cover the maximum bot name, tagged envelope,
    /// identity, and two maximum-length lineage handles.
    /// </summary>
    public const int MaxCanonicalContractBytes = 1024 * 1024 - 1024;
    /// <summary>
    /// Maximum JSON values in one canonical static contract. The Engine
    /// admission boundary enforces the same profile limit before delivery.
    /// </summary>
    public const int MaxCanonicalContractNodes = 65_536;
    /// <summary>
    /// Maximum direct properties or items in one canonical JSON container.
    /// The Engine admission boundary enforces the same profile limit.
    /// </summary>
    public const int MaxCanonicalContractCollectionCount = 4096;
    /// <summary>Generic host/runtime behavior contract version.</summary>
    public const int RuntimeContractVersion = 3;
    /// <summary>MatchStart payload schema version.</summary>
    public const int MatchStartSchemaVersion = 3;
    /// <summary>Per-tick observation schema version.</summary>
    public const int ObservationSchemaVersion = 3;
    /// <summary>Per-tick decision schema version.</summary>
    public const int DecisionSchemaVersion = 2;
    /// <summary>Resolved static match-contract schema version.</summary>
    public const int MatchContractSchemaVersion = 3;
    /// <summary>Replay format associated with generic match projection.</summary>
    public const int ReplayFormatVersion = 3;
}
