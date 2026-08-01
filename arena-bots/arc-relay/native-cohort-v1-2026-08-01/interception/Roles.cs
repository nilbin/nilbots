using BotArena.Sdk;

internal enum Role
{
    ReturnCarrier,
    CoreRecovery,
    CarrierHook,
    FocusPaint,
    CutlineBurst,
    RailCut,
    FlareWatch,
    RelayRunner,
    RouteGuard,
}

internal sealed class RoleMap
{
    private readonly Dictionary<int, Role> _roles = [];
    private readonly Dictionary<int, Position> _coreTargets = [];

    public Role this[MindBody body] =>
        _roles.TryGetValue(body.UnitId, out Role role)
            ? role
            : Role.RouteGuard;

    public Position? CoreTarget(MindBody body) =>
        _coreTargets.TryGetValue(body.UnitId, out Position target)
            ? target
            : null;

    public void Assign(MindBody body, Role role) =>
        _roles[body.UnitId] = role;

    public void AssignRecovery(MindBody body, Position target)
    {
        _roles[body.UnitId] = Role.CoreRecovery;
        _coreTargets[body.UnitId] = target;
    }
}

/// <summary>Assigns all live bodies once per tick from capability and Core state.</summary>
internal static class Roles
{
    public static RoleMap Assign(
        GenericActorResolvedMatchContract contract,
        MindContext mind)
    {
        var map = new RoleMap();
        HashSet<int> assigned = [];

        foreach (MindBody body in mind.Bodies)
        {
            if (ArenaBasics.CarriedCore(mind, body.ActorId) is null)
                continue;
            map.Assign(body, Role.ReturnCarrier);
            assigned.Add(body.UnitId);
        }

        foreach (GenericActorContext.ArcRelayCoreState core
                 in ArenaBasics.LooseCores(mind))
        {
            MindBody? recovery = mind.Bodies
                .Where(body => !assigned.Contains(body.UnitId))
                .OrderByDescending(body =>
                    ArenaBasics.HasSignature(contract, body, "arc-toss"))
                .ThenBy(body => body.Position.ChebyshevDistance(core.Position))
                .ThenByDescending(body => body.Health)
                .ThenBy(body => body.ClassId, StringComparer.Ordinal)
                .ThenBy(body => body.UnitId)
                .FirstOrDefault();
            if (recovery is null)
                break;
            map.AssignRecovery(recovery, core.Position);
            assigned.Add(recovery.UnitId);
        }

        foreach (MindBody body in mind.Bodies.Where(body =>
                     !assigned.Contains(body.UnitId)))
        {
            Role role = ArenaBasics.HasSignature(contract, body, "tractor-hook")
                ? Role.CarrierHook
                : ArenaBasics.HasSignature(contract, body, "target-paint")
                    ? Role.FocusPaint
                    : ArenaBasics.HasSignature(contract, body, "kinetic-burst")
                        ? Role.CutlineBurst
                        : ArenaBasics.HasSignature(contract, body, "rail-line")
                            ? Role.RailCut
                            : ArenaBasics.HasSignature(contract, body, "survey-flare")
                                ? Role.FlareWatch
                                : ArenaBasics.HasSignature(contract, body, "arc-toss")
                                    ? Role.RelayRunner
                                    : Role.RouteGuard;
            map.Assign(body, role);
        }
        return map;
    }
}
