using System.Data;
using BotArena.App.Bots;
using BotArena.App.Matches;
using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Competition;

/// <summary>
/// Idempotently expands rules-version keyed Duel history into opaque playlist,
/// season, and ladder identities. The full scan and every repair commit as one
/// advisory-locked transaction or not at all.
/// <para>
/// This is deliberately rerunnable. An expand rollout must run it once when
/// the nullable schema lands and again after every old application image has
/// stopped writing; only that second pass can close the rolling-deployment
/// window before a later read switch or NOT NULL contract migration.
/// </para>
/// </summary>
public sealed class LegacyCompetitionIdentityBackfiller(
    AppDbContext db,
    LegacyCompetitionIdentityResolver resolver)
{
    public async Task RunAsync(
        string currentRulesVersion,
        CancellationToken cancellationToken = default)
    {
        LegacyCompetitionDefinition.ValidateRulesVersion(
            currentRulesVersion,
            nameof(currentRulesVersion));
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "The legacy competition backfill must own its transaction.");
        }

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                cancellationToken);
        try
        {
            await resolver.AcquireAdvisoryLockAsync(cancellationToken);
            await ValidateNoOrphanedMatchesAsync(cancellationToken);

            List<BotRating> ratings =
                await db.BotRatings.ToListAsync(cancellationToken);
            List<MatchSet> sets =
                await db.MatchSets.ToListAsync(cancellationToken);
            List<Match> matches =
                await db.Matches.ToListAsync(cancellationToken);
            List<(Guid Id, string RulesVersion)> existingAliases =
                await db.Ladders
                    .Where(ladder =>
                        ladder.LegacyRulesVersion != null)
                    .Select(ladder => new ValueTuple<Guid, string>(
                        ladder.Id,
                        ladder.LegacyRulesVersion!))
                    .ToListAsync(cancellationToken);

            var rulesVersions = new HashSet<string>(
                StringComparer.Ordinal)
            {
                currentRulesVersion,
            };
            AddRulesVersions(
                rulesVersions,
                ratings.Select(rating => (
                    rating.Id,
                    Kind: nameof(BotRating),
                    RulesVersion: rating.RulesVersion)));
            AddRulesVersions(
                rulesVersions,
                sets.Select(set => (
                    set.Id,
                    Kind: nameof(MatchSet),
                    RulesVersion: set.GameRulesVersion)));
            AddRulesVersions(
                rulesVersions,
                matches.Select(match => (
                    match.Id,
                    Kind: nameof(Match),
                    RulesVersion: match.GameRulesVersion)));
            AddRulesVersions(
                rulesVersions,
                existingAliases.Select(ladder => (
                    ladder.Id,
                    Kind: nameof(Ladder),
                    ladder.RulesVersion)));

            var identities =
                new Dictionary<string, LegacyCompetitionIdentity>(
                    StringComparer.Ordinal);
            foreach (string rulesVersion in rulesVersions.Order(
                         StringComparer.Ordinal))
            {
                identities.Add(
                    rulesVersion,
                    await resolver.ResolveOrCreateLockedAsync(
                        rulesVersion,
                        currentRulesVersion,
                        cancellationToken));
            }

            foreach (BotRating rating in ratings)
            {
                LegacyCompetitionIdentity identity =
                    identities[rating.RulesVersion];
                Repair(
                    rating.LadderId,
                    identity.LadderId,
                    value => rating.LadderId = value,
                    nameof(BotRating),
                    rating.Id,
                    nameof(BotRating.LadderId),
                    rating.RulesVersion);
            }

            var setsById = new Dictionary<Guid, MatchSet>();
            foreach (MatchSet set in sets)
            {
                LegacyCompetitionIdentity identity =
                    identities[set.GameRulesVersion];
                Repair(
                    set.PlaylistVersionId,
                    identity.PlaylistVersionId,
                    value => set.PlaylistVersionId = value,
                    nameof(MatchSet),
                    set.Id,
                    nameof(MatchSet.PlaylistVersionId),
                    set.GameRulesVersion);
                Repair(
                    set.LadderId,
                    identity.LadderId,
                    value => set.LadderId = value,
                    nameof(MatchSet),
                    set.Id,
                    nameof(MatchSet.LadderId),
                    set.GameRulesVersion);
                setsById.Add(set.Id, set);
            }

            foreach (Match match in matches)
            {
                LegacyCompetitionIdentity identity =
                    identities[match.GameRulesVersion];
                Repair(
                    match.PlaylistVersionId,
                    identity.PlaylistVersionId,
                    value => match.PlaylistVersionId = value,
                    nameof(Match),
                    match.Id,
                    nameof(Match.PlaylistVersionId),
                    match.GameRulesVersion);

                if (match.MatchSetId is not Guid matchSetId)
                    continue;
                MatchSet owningSet = setsById[matchSetId];
                if (!string.Equals(
                        match.GameRulesVersion,
                        owningSet.GameRulesVersion,
                        StringComparison.Ordinal))
                {
                    throw Contradiction(
                        nameof(Match),
                        match.Id,
                        match.GameRulesVersion,
                        $"belongs to MatchSet {owningSet.Id} on rules version " +
                        $"'{owningSet.GameRulesVersion}'");
                }
                if (match.PlaylistVersionId !=
                    owningSet.PlaylistVersionId)
                {
                    throw Contradiction(
                        nameof(Match),
                        match.Id,
                        match.GameRulesVersion,
                        $"does not share MatchSet {owningSet.Id}'s playlist version");
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task ValidateNoOrphanedMatchesAsync(
        CancellationToken cancellationToken)
    {
        var orphan = await db.Matches
            .Where(match =>
                match.MatchSetId != null &&
                !db.MatchSets.Any(set => set.Id == match.MatchSetId))
            .Select(match => new { match.Id, match.MatchSetId })
            .FirstOrDefaultAsync(cancellationToken);
        if (orphan is not null)
        {
            throw new InvalidOperationException(
                $"Legacy competition identity contradiction: Match " +
                $"{orphan.Id} references missing MatchSet {orphan.MatchSetId}.");
        }
    }

    private static void AddRulesVersions(
        ISet<string> target,
        IEnumerable<(Guid Id, string Kind, string RulesVersion)> rows)
    {
        foreach ((Guid id, string kind, string rulesVersion) in rows)
        {
            if (string.IsNullOrWhiteSpace(rulesVersion))
            {
                throw new InvalidOperationException(
                    $"Legacy competition identity contradiction: {kind} {id} " +
                    "has a blank rules version.");
            }
            LegacyCompetitionDefinition.ValidateRulesVersion(
                rulesVersion,
                $"{kind}.{nameof(rulesVersion)}");
            target.Add(rulesVersion);
        }
    }

    private static void Repair(
        Guid? actual,
        Guid expected,
        Action<Guid> fill,
        string rowKind,
        Guid rowId,
        string field,
        string rulesVersion)
    {
        if (actual is null)
        {
            fill(expected);
            return;
        }
        if (actual.Value != expected)
        {
            throw Contradiction(
                rowKind,
                rowId,
                rulesVersion,
                $"{field} is {actual.Value:D}, expected {expected:D}");
        }
    }

    private static InvalidOperationException Contradiction(
        string rowKind,
        Guid rowId,
        string rulesVersion,
        string detail) =>
        new(
            $"Legacy competition identity contradiction for {rowKind} " +
            $"{rowId} on rules version '{rulesVersion}': {detail}.");
}
