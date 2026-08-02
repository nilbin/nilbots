using BotArena.App.Bots;

namespace BotArena.App.ArcRelay;

public sealed record ArcRelayCompositionSlotResponse(
    int Slot,
    string ClassId,
    string ClassName,
    string LookId);

public sealed record ArcRelayEntrantCardResponse(
    Guid Id,
    string Kind,
    string Name,
    string OwnerDisplayName,
    int Revision,
    ArcRelayCrestDescriptor Crest,
    IReadOnlyList<ArcRelayCompositionSlotResponse> Composition,
    double Rating,
    int RankedMatches,
    bool LadderOptedIn,
    string Status,
    string? SuspensionReason,
    Guid? SuspensionMatchId,
    string? ArtifactHash,
    string ContentHash,
    bool IsOwner);

public sealed record ArcRelayMindResponse(
    ArcRelayEntrantCardResponse Entrant,
    string EntryType,
    IReadOnlyList<SourceFileDto> Files,
    ArcRelayCompositionDeclaration Composition,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? BuildLog);

public sealed record ArcRelayLadderResponse(
    Guid LadderId,
    string Name,
    string PairingPolicy,
    int MaximumOptedInPerAccount,
    int MaximumMatchesPerEntrantPerDay,
    IReadOnlyList<ArcRelayEntrantCardResponse> Entrants);

public sealed record ArcRelayCrestOptionsResponse(
    Guid EntrantId,
    IReadOnlyList<ArcRelayCrestDescriptor> Options);

public sealed record CreateArcRelayMindRequest(
    string Name,
    string EntryType,
    IReadOnlyList<SourceFileDto> Files,
    ArcRelayCompositionDeclaration Composition,
    int CrestVariant = 0);

public sealed record ReviseArcRelayMindRequest(
    string Name,
    int ExpectedRevision,
    string EntryType,
    IReadOnlyList<SourceFileDto> Files,
    ArcRelayCompositionDeclaration Composition);

public sealed record SetArcRelayCrestRequest(int Variant);

public sealed record SetArcRelayLadderOptInRequest(bool OptedIn);

public sealed record ArcRelayPreflightResponse(
    Guid MatchId,
    string Status);

public sealed record CreateArcRelayScrimmageRequest(
    Guid EntrantId,
    Guid OpponentEntrantId,
    long? Seed);
