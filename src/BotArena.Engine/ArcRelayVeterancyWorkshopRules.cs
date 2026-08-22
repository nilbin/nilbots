using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Rules contract for the neutral veterancy Workshops. A profile without
/// Workshops carries null and behaves byte-identically to history. The
/// numbers are mode-wide; each listed Workshop binds its own exact training
/// tile and authored contest region through the map binding.
/// </summary>
public sealed record ArcRelayVeterancyWorkshopRules
{
    public ArcRelayVeterancyWorkshopRules(
        ImmutableArray<string> workshopIds,
        int unlockTick,
        int requiredChannelTicks,
        int xpAward,
        int dormancyTicks)
    {
        if (workshopIds.IsDefaultOrEmpty
            || workshopIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Workshop rules need at least one non-empty workshop id.",
                nameof(workshopIds));
        }
        if (workshopIds.Distinct(StringComparer.Ordinal).Count()
            != workshopIds.Length)
        {
            throw new ArgumentException(
                "Workshop ids must be unique.",
                nameof(workshopIds));
        }
        if (unlockTick < 0)
            throw new ArgumentOutOfRangeException(nameof(unlockTick));
        if (requiredChannelTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredChannelTicks));
        if (xpAward <= 0)
            throw new ArgumentOutOfRangeException(nameof(xpAward));
        if (dormancyTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(dormancyTicks));
        WorkshopIds = workshopIds;
        UnlockTick = unlockTick;
        RequiredChannelTicks = requiredChannelTicks;
        XpAward = xpAward;
        DormancyTicks = dormancyTicks;
    }

    public ImmutableArray<string> WorkshopIds { get; }

    /// <summary>First tick a training tick may qualify.</summary>
    public int UnlockTick { get; }

    /// <summary>Consecutive qualifying ticks one session requires.</summary>
    public int RequiredChannelTicks { get; }

    /// <summary>Veterancy XP granted by one completed session.</summary>
    public int XpAward { get; }

    /// <summary>Global dormancy after a completion, in ticks.</summary>
    public int DormancyTicks { get; }
}
