namespace BotArena.App.Matches;

/// <summary>
/// The only creation path for immutable participant identity, artifact, and
/// presentation snapshots in new ranked and unranked matches.
/// </summary>
public sealed class MatchParticipantSnapshotFactory
{
    public MatchParticipant Create(
        Guid matchId,
        int slot,
        AdmittedMatchBot admitted) =>
        new()
        {
            MatchId = matchId,
            Slot = slot,
            BotId = admitted.Bot.Id,
            BotVersionId = admitted.Version.Id,
            NameSnapshot = admitted.Bot.Name,
            OwnerDisplayNameSnapshot = admitted.OwnerDisplayName,
            AccentSnapshot = admitted.Bot.Accent,
            LookIdSnapshot = admitted.Bot.LookId,
            ProjectileLookIdSnapshot = admitted.Bot.ProjectileLookId,
            ArtifactHashSnapshot = admitted.Version.ArtifactHash ?? "",
        };
}
