namespace BotArena.Engine.Tests;

public sealed class GenericDeathmatchResultEvidenceTests
{
    [Fact]
    public void RecomputesTimeoutStandingsAndRejectsAClaimedWinner()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_101);

        session.Run();
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchResult result = chronology.Result!;
        GenericActorWorldSnapshot finalState =
            chronology.Ticks[^1].PostState;
        GenericDeathmatchResultEvidence.Validate(
            definition,
            finalState,
            result);

        TeamStanding[] original = result.Standings.Standings
            .OrderBy(standing => standing.TeamId)
            .ToArray();
        var forgedStandings = new TeamStandings(
            definition.Topology,
            definition.Rules.GameMode,
            [
                new TeamStanding(
                    original[0].TeamId,
                    rank: 1,
                    TeamStandingOutcome.Win,
                    original[0].Scores),
                new TeamStanding(
                    original[1].TeamId,
                    rank: 2,
                    TeamStandingOutcome.Loss,
                    original[1].Scores),
            ]);
        GenericActorMatchResult forged = new(
            result.CompletionReason,
            result.EndTick,
            forgedStandings,
            result.EligibleTeamIds,
            result.Units,
            result.Mode);

        Assert.Throws<ArgumentException>(() =>
            GenericDeathmatchResultEvidence.Validate(
                definition,
                finalState,
                forged));
    }

    [Fact]
    public void RejectsACompletionReasonUnavailableInTheResolvedRules()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 1,
                    KillsToWin = null,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 8_102);

        session.Run();
        GenericActorMatchChronology chronology = session.Chronology;
        GenericActorMatchResult result = chronology.Result!;
        var forged = new GenericActorMatchResult(
            "kill-limit",
            result.EndTick,
            result.Standings,
            result.EligibleTeamIds,
            result.Units,
            new GenericActorMatchModeResult.Deathmatch(
                GenericDeathmatchEndReason.KillLimit,
                Assert.IsType<
                    GenericActorMatchModeResult.Deathmatch>(result.Mode)
                    .Scores));

        Assert.Throws<ArgumentException>(() =>
            GenericDeathmatchResultEvidence.Validate(
                definition,
                chronology.Ticks[^1].PostState,
                forged));
    }
}
