namespace BotArena.Engine;

/// <summary>
/// Recomputes mode-owned terminal legality from the final generic world. This
/// is a semantic replay check, not a second world simulation.
/// </summary>
internal static class GenericDeathmatchResultEvidence
{
    public static void Validate(
        ActorResolvedMatchDefinition definition,
        GenericActorWorldSnapshot finalState,
        GenericActorMatchResult result)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(finalState);
        ArgumentNullException.ThrowIfNull(result);
        if (definition.Rules.GameMode
                is not DeathmatchGameModeDefinition gameMode
            || result.Mode
                is not GenericActorMatchModeResult.Deathmatch deathmatch)
        {
            throw new ArgumentException(
                "Deathmatch result evidence requires a Deathmatch contract and typed mode result.",
                nameof(result));
        }

        int[] eligible = result.EligibleTeamIds.ToArray();
        Dictionary<int, long> activeHealth = definition.Topology.Teams
            .ToDictionary(
                team => team.TeamId,
                team => finalState.ActiveLives
                    .Where(life => life.ActorId.TeamId == team.TeamId)
                    .Sum(life => (long)life.Health));
        var kernel = new DeathmatchModeKernel(
            definition.Topology,
            gameMode);
        TeamStandings expected = deathmatch.Reason switch
        {
            GenericDeathmatchEndReason.FaultEligibility
                when eligible.Length <= 1 =>
                kernel.ResolveTimeoutStandings(
                    deathmatch.Scores,
                    activeHealth,
                    eligible),
            GenericDeathmatchEndReason.KillLimit
                when eligible.Length > 1 =>
                ResolveKillLimit(
                    kernel,
                    deathmatch.Scores,
                    activeHealth,
                    eligible),
            GenericDeathmatchEndReason.MaxTicks
                when eligible.Length > 1
                     && result.EndTick
                        == definition.Rules.Limits.MaxTicks - 1 =>
                ResolveTimeout(
                    kernel,
                    deathmatch.Scores,
                    activeHealth,
                    eligible),
            _ => throw InvalidResult(
                "completion reason is not legal for the final eligibility/tick state"),
        };

        if (!StandingsSemanticallyEqual(expected, result.Standings))
        {
            throw InvalidResult(
                "standings do not follow the resolved Deathmatch victory policy");
        }
    }

    private static TeamStandings ResolveKillLimit(
        DeathmatchModeKernel kernel,
        DeathmatchScoreState scores,
        IReadOnlyDictionary<int, long> activeHealth,
        IReadOnlyCollection<int> eligible)
    {
        TeamStandings? standings = kernel.ApplyJointTick(
                scores,
                damageContacts: [],
                activeHealth,
                eligible)
            .KillLimitStandings;
        return standings
            ?? throw InvalidResult(
                "kill-limit completion was claimed before its threshold");
    }

    private static TeamStandings ResolveTimeout(
        DeathmatchModeKernel kernel,
        DeathmatchScoreState scores,
        IReadOnlyDictionary<int, long> activeHealth,
        IReadOnlyCollection<int> eligible)
    {
        if (kernel.ApplyJointTick(
                scores,
                damageContacts: [],
                activeHealth,
                eligible)
            .KillLimitStandings is not null)
        {
            throw InvalidResult(
                "max-tick completion cannot override a reached kill limit");
        }
        return kernel.ResolveTimeoutStandings(
            scores,
            activeHealth,
            eligible);
    }

    private static bool StandingsSemanticallyEqual(
        TeamStandings left,
        TeamStandings right) =>
        left.WinnerTeamId == right.WinnerTeamId
        && left.Standings.Length == right.Standings.Length
        && left.Standings.Zip(right.Standings).All(pair =>
            pair.First.TeamId == pair.Second.TeamId
            && pair.First.Rank == pair.Second.Rank
            && pair.First.Outcome == pair.Second.Outcome
            && pair.First.Scores.SequenceEqual(pair.Second.Scores));

    private static ArgumentException InvalidResult(string reason) =>
        new(
            $"Deathmatch terminal evidence is invalid: {reason}.",
            "result");
}
