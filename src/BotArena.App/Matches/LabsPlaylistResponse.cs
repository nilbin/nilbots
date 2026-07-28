namespace BotArena.App.Matches;

public sealed record LabsPlaylistResponse(
    Guid PlaylistVersionId,
    string Key,
    string DisplayName,
    int Version,
    string GameModeId,
    string RulesetId,
    string MatchFormatId,
    int ParticipantCount,
    int ScoringTeamCount,
    int ParticipantsPerTeam,
    string RequiredContractProfileId);
