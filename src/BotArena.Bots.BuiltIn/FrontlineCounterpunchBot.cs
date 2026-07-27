using BotArena.Sdk;

namespace BotArena.Bots.BuiltIn;

/// <summary>
/// Conditional-defense smoke doctrine. Its Prime builds one child, the team
/// holds the own-side Frontline position, and visible contact triggers a
/// mobile close-and-fire response.
/// </summary>
public sealed class FrontlineCounterpunchBot : IActorBot
{
    private ActorMatchStart? _start;

    public void StartLife(ActorMatchStart start) => _start = start;

    public ActorDecision Tick(ActorContext context)
    {
        ActorMatchStart start = _start
            ?? throw new InvalidOperationException("Life was not started.");
        PublicFrontlineDefinition? frontline = start.Contract.Rules.Frontline;
        int? supportUnitId = start.Contract.Topology.UnitSlots
            .Where(slot =>
                slot.TeamId == context.Self.ActorId.TeamId
                && slot.UnitId != frontline?.Fabrication.FabricatorUnitId)
            .OrderBy(slot => slot.UnitId)
            .Select(slot => (int?)slot.UnitId)
            .FirstOrDefault();
        if (supportUnitId is not null
            && FrontlineReferenceBotLogic.IsFabricator(start, context))
        {
            ActorDecision? fabrication =
                FrontlineReferenceBotLogic.TryFabricate(
                    context,
                    supportUnitId);
            if (fabrication is not null)
                return fabrication;
            if (context.TeamUnits.Any(unit =>
                    unit.UnitId == supportUnitId
                    && unit.LifecycleStatus
                        != FrontlineLifecycleStatus.Active))
            {
                return FrontlineReferenceBotLogic.MoveToHomePad(
                    start,
                    context);
            }
        }

        ActorDecision? attack =
            FrontlineReferenceBotLogic.TryAttack(start, context);
        if (attack is not null)
            return attack;
        return context.Enemies.Length > 0
            ? FrontlineReferenceBotLogic.MoveToVisibleEnemy(start, context)
            : FrontlineReferenceBotLogic.MoveToDefensiveLine(start, context);
    }
}
