namespace BotArena.Engine;

/// <summary>
/// A side with its own objective score and win/loss result. Class identity is
/// absent for classless contracts and otherwise names the homogeneous chassis
/// selected for this scoring team.
/// </summary>
public sealed record PublicScoringTeam(
    int TeamId,
    string? ClassId = null);
