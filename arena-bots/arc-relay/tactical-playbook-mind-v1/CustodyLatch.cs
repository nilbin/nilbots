using BotArena.Sdk;

/// <summary>
/// Custody latches at pickup (owner ruling, DECISIONS #348): a body that
/// takes a Core while a program phase is active finishes that carry under
/// the phase that began it — the pickup phase's tags stay lit for THAT
/// body even after the squad advances, so its custody leaves (bank plan,
/// drawn way home, escort arrangement) outrank the new phase's in tree
/// order. The latch dissolves the moment the Core leaves its hands: bank,
/// drop, death, or pass (the catcher latches fresh at ITS current phase).
/// Squad-level phase progress is untouched — only the carrier's own view
/// of which subtrees are lit widens.
/// </summary>
internal sealed class CustodyLatch
{
    internal sealed record Latch(
        ActorIdentity Carrier,
        string TacticTag,
        string PhaseTag);

    private readonly Dictionary<int, Latch> _byUnit = new();

    /// <summary>Track this tick's own-team carriers against the active
    /// program pair. A latch survives exactly as long as the same life
    /// keeps carrying; no active phase latches nothing.</summary>
    public void Observe(
        IReadOnlyCollection<ActorIdentity> carriers,
        string? tacticId,
        string? phaseId)
    {
        foreach (int unit in _byUnit.Keys.ToArray())
        {
            if (!carriers.Contains(_byUnit[unit].Carrier))
                _byUnit.Remove(unit);
        }
        if (tacticId is null || phaseId is null)
            return;
        foreach (ActorIdentity carrier in carriers)
        {
            if (!_byUnit.ContainsKey(carrier.UnitId))
            {
                _byUnit[carrier.UnitId] = new Latch(
                    carrier, tacticId, $"{tacticId}/{phaseId}");
            }
        }
    }

    public Latch? For(int unitId) => _byUnit.GetValueOrDefault(unitId);

    /// <summary>This body's enabled set: the squad's tags plus its own
    /// latched carry tags. Null (a legacy sheet with no program) stays
    /// null, and the untouched squad set is returned whenever the latch
    /// adds nothing.</summary>
    public IReadOnlySet<string>? Augment(
        int unitId,
        IReadOnlySet<string>? squadTags)
    {
        if (squadTags is null)
            return null;
        Latch? latch = _byUnit.GetValueOrDefault(unitId);
        if (latch is null
            || (squadTags.Contains(latch.TacticTag)
                && squadTags.Contains(latch.PhaseTag)))
        {
            return squadTags;
        }
        return new HashSet<string>(squadTags)
        {
            latch.TacticTag,
            latch.PhaseTag,
        };
    }
}
