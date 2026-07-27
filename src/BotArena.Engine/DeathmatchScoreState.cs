using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Immutable, canonically ordered Deathmatch score state. Applying a joint
/// tick returns a new state, so validation or checked-arithmetic failures
/// cannot leave partially applied counters behind.
/// </summary>
public sealed class DeathmatchScoreState
{
    public DeathmatchScoreState(
        IReadOnlyCollection<DeathmatchTeamScore> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);

        DeathmatchTeamScore[] snapshot = [.. teams];
        if (snapshot.Length == 0)
        {
            throw new ArgumentException(
                "Deathmatch score state needs at least one team.",
                nameof(teams));
        }
        if (snapshot.Any(team => team is null))
        {
            throw new ArgumentException(
                "Deathmatch score state cannot contain null teams.",
                nameof(teams));
        }
        if (snapshot
            .Select(team => team.TeamId)
            .Distinct()
            .Count() != snapshot.Length)
        {
            throw new ArgumentException(
                "Deathmatch score-state team IDs must be unique.",
                nameof(teams));
        }

        Teams = snapshot
            .OrderBy(team => team.TeamId)
            .ToImmutableArray();
    }

    /// <summary>Ordered by stable scoring-team ID.</summary>
    public ImmutableArray<DeathmatchTeamScore> Teams { get; }
}
