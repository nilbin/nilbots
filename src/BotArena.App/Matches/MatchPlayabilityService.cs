using BotArena.App.Shared;
using Microsoft.EntityFrameworkCore;

namespace BotArena.App.Matches;

/// <summary>
/// Batch projection of the authoritative Duel participant admission policy.
/// Ownership is projected separately because only an owned playable bot may
/// initiate a match, while any playable bot may be selected as an opponent.
/// </summary>
public sealed class MatchPlayabilityService(
    AppDbContext db,
    MatchAdmissionService admission)
{
    public async Task<IReadOnlyList<MatchPlayabilityResponse>> ProjectAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var roster = await db.Bots
            .AsNoTracking()
            .OrderBy(bot => bot.CreatedAt)
            .ThenBy(bot => bot.Id)
            .Select(bot => new
            {
                bot.Id,
                IsOwned = bot.OwnerUserId == userId,
            })
            .ToListAsync(cancellationToken);
        IReadOnlyDictionary<
            Guid,
            ApplicationResult<AdmittedMatchBot>> admissions =
            await admission.AdmitManyAsync(
                roster.Select(bot => bot.Id).ToArray(),
                cancellationToken);

        var result =
            new List<MatchPlayabilityResponse>(roster.Count);
        foreach (var bot in roster)
        {
            ApplicationResult<AdmittedMatchBot> admitted =
                admissions[bot.Id];
            result.Add(new MatchPlayabilityResponse(
                bot.Id,
                bot.IsOwned,
                admitted.Succeeded,
                admitted.Error?.Code,
                admitted.Error?.Detail));
        }

        return result;
    }
}
