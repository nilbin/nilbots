using BotArena.Sdk;

/// <summary>
/// Team-local custody provenance for Tactics sheets. The first friendly
/// pickup binds one stable Core identity to the Tactic phase that owned that
/// pickup. Possession may then move, disappear, or become hostile without
/// rewriting the binding. A banked/replaced Core is the only normal end of
/// that provenance.
///
/// The squad coordinator still advances normally. This class only widens the
/// enabled tag set for the Core's current friendly carrier and the pickup
/// phase's authored escort group, so ordinary phase Instructions remain the
/// sole executor priority system.
/// </summary>
internal sealed class CustodyLatch(int teamId)
{
    internal sealed record CoreObservation(
        GenericActorContext.ArcRelayCoreId CoreId,
        ActorIdentity? Carrier);

    internal sealed record Latch(
        GenericActorContext.ArcRelayCoreId CoreId,
        int TeamId,
        ActorIdentity? Carrier,
        string TacticTag,
        string PhaseTag,
        int[] EscortUnits);

    private readonly Dictionary<GenericActorContext.ArcRelayCoreId, Latch>
        _byCore = [];

    /// <summary>
    /// Reconcile public Core lifecycle and currently visible possession.
    /// Missing visibility never clears provenance. A new own carrier binds
    /// only when this team has not previously seen that Core in custody.
    /// </summary>
    public void Observe(
        IReadOnlyCollection<CoreObservation> visibleCores,
        IReadOnlyCollection<GenericActorContext.ArcRelayCoreId>
            outstandingCores,
        string? pickupTacticId,
        string? pickupPhaseId,
        IReadOnlyList<int[]>? pickupCustodyGroups = null)
    {
        var outstanding = outstandingCores.ToHashSet();
        foreach (GenericActorContext.ArcRelayCoreId coreId in
                 _byCore.Keys.Where(core => !outstanding.Contains(core))
                     .ToArray())
        {
            _byCore.Remove(coreId);
        }

        foreach (CoreObservation observed in visibleCores)
        {
            ActorIdentity? ownCarrier = observed.Carrier is ActorIdentity carrier
                && carrier.TeamId == teamId
                    ? carrier
                    : null;
            if (_byCore.TryGetValue(observed.CoreId, out Latch? existing))
            {
                _byCore[observed.CoreId] = existing with
                {
                    Carrier = ownCarrier,
                };
                continue;
            }
            if (ownCarrier is null
                || pickupTacticId is null
                || pickupPhaseId is null)
            {
                continue;
            }

            int[] escorts = (pickupCustodyGroups ?? [])
                .FirstOrDefault(group => group.Contains(ownCarrier.UnitId))
                ?.ToArray()
                ?? [];
            _byCore[observed.CoreId] = new Latch(
                observed.CoreId,
                teamId,
                ownCarrier,
                pickupTacticId,
                $"{pickupTacticId}/{pickupPhaseId}",
                escorts);
        }
    }

    /// <summary>
    /// Own carried Core wins over an escort claim. With several escortable
    /// Cores, stable Core identity orders the result instead of observation
    /// or dictionary order.
    /// </summary>
    public Latch? For(int unitId) =>
        Active().FirstOrDefault(latch => latch.Carrier!.UnitId == unitId)
        ?? Active().FirstOrDefault(latch =>
            latch.Carrier!.UnitId != unitId
            && latch.EscortUnits.Contains(unitId));

    private IEnumerable<Latch> Active() => _byCore.Values
        .Where(latch => latch.Carrier is not null)
        .OrderBy(latch => latch.CoreId.SourceWellId, StringComparer.Ordinal)
        .ThenBy(latch => latch.CoreId.SourceOrdinal);

    /// <summary>This body's enabled set. A body actively consuming custody
    /// runs the Core's pinned pickup phase alone: unioning in the squad's
    /// current phase could let an earlier Tactic or phase floor shadow the
    /// pinned return. This remains ordinary tag eligibility, not a second
    /// scheduler. Null keeps legacy sheets on their unchanged path.</summary>
    public IReadOnlySet<string>? Augment(
        int unitId,
        IReadOnlySet<string>? squadTags)
    {
        if (squadTags is null)
            return null;
        Latch? latch = For(unitId);
        if (latch is null)
            return squadTags;
        return new HashSet<string>(StringComparer.Ordinal)
        {
            latch.TacticTag,
            latch.PhaseTag,
        };
    }
}
