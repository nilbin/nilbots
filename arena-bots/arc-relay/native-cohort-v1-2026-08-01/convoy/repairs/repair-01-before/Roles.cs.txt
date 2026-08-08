using BotArena.Sdk;

/// <summary>Whole-army assignment for one convoy tick.</summary>
internal static class Roles
{
    internal enum Role
    {
        MainCarrier,
        RelayCatcher,
        MainPickup,
        UpperScreen,
        LowerScreen,
        ConvoyMedic,
        ConvoySuppressor,
        FirstPicket,
        SecondPicket,
        WingReturn,
        Reserve,
    }

    internal sealed class Plan
    {
        private readonly Dictionary<int, Role> _roles = [];
        private readonly Dictionary<int, string> _picketWells = [];

        public MindBody? MainCarrier { get; set; }
        public MindBody? Catcher { get; set; }
        public MindBody? Pickup { get; set; }
        public MindBody? Protected => MainCarrier ?? Pickup;
        public int? HandoffSourceUnitId { get; set; }
        public int? HandoffTargetUnitId { get; set; }

        public Role this[MindBody body] => _roles.TryGetValue(
            body.UnitId,
            out Role role)
                ? role
                : Role.Reserve;

        public void Assign(MindBody body, Role role) =>
            _roles[body.UnitId] = role;

        public bool IsAssigned(MindBody body) => _roles.ContainsKey(body.UnitId);

        public void AssignPicket(MindBody body, Role role, string wellId)
        {
            Assign(body, role);
            _picketWells[body.UnitId] = wellId;
        }

        public string? PicketWell(MindBody body) =>
            _picketWells.TryGetValue(body.UnitId, out string? wellId)
                ? wellId
                : null;
    }

    public static Plan Assign(
        GenericActorResolvedMatchContract contract,
        MindContext mind,
        int teamId,
        string mainWellId,
        Position mainWell,
        IReadOnlyList<string> wingWellIds)
    {
        var plan = new Plan();
        if (contract.Rules.GameMode
                is not GenericActorRulesContract.ArcRelayGameMode rules
            || mind.Mode
                is not GenericActorContext.ModeObservationState.ArcRelay mode)
        {
            return plan;
        }

        Dictionary<int, GenericActorContext.ArcRelayCoreState> carried =
            mode.VisibleCores
                .Where(core =>
                    core.Disposition
                        == GenericActorContext.ArcRelayCoreDisposition.Carried
                    && core.CarrierActorId is { TeamId: var owner }
                    && owner == teamId)
                .ToDictionary(core => core.CarrierActorId!.UnitId);

        foreach ((int unitId, GenericActorContext.ArcRelayCoreState core)
                 in carried.OrderBy(entry => entry.Key))
        {
            MindBody? body = mind.Bodies.FirstOrDefault(candidate =>
                candidate.UnitId == unitId);
            if (body is null)
                continue;
            if (string.Equals(core.CoreId.SourceWellId, mainWellId,
                    StringComparison.Ordinal)
                && plan.MainCarrier is null)
            {
                plan.MainCarrier = body;
                plan.Assign(body, Role.MainCarrier);
            }
            else
            {
                plan.Assign(body, Role.WingReturn);
            }
        }

        MindBody[] relays = BodiesWithSignature(mind, rules, "arc-toss")
            .OrderBy(body => body.UnitId)
            .ToArray();
        if (plan.MainCarrier is null)
        {
            plan.Pickup = relays
                .Where(body => !plan.IsAssigned(body))
                .OrderBy(body => body.Position.ChebyshevDistance(mainWell))
                .ThenBy(body => body.UnitId)
                .FirstOrDefault();
            if (plan.Pickup is not null)
                plan.Assign(plan.Pickup, Role.MainPickup);
        }

        plan.Catcher = relays
            .Where(body => !plan.IsAssigned(body))
            .OrderBy(body =>
                body.Position.ChebyshevDistance(
                    (plan.Protected ?? body).Position))
            .ThenBy(body => body.UnitId)
            .FirstOrDefault();
        if (plan.Catcher is not null)
            plan.Assign(plan.Catcher, Role.RelayCatcher);

        MindBody[] screens = BodiesWithSignature(mind, rules, "prism-wall")
            .Where(body => !plan.IsAssigned(body))
            .OrderBy(body => body.UnitId)
            .Take(2)
            .ToArray();
        if (screens.ElementAtOrDefault(0) is MindBody upper)
            plan.Assign(upper, Role.UpperScreen);
        if (screens.ElementAtOrDefault(1) is MindBody lower)
            plan.Assign(lower, Role.LowerScreen);

        MindBody? medic = BodiesWithSignature(mind, rules, "repair-beam")
            .Where(body => !plan.IsAssigned(body))
            .OrderBy(body => body.UnitId)
            .FirstOrDefault();
        if (medic is not null)
            plan.Assign(medic, Role.ConvoyMedic);

        MindBody? suppressor = BodiesWithSignature(mind, rules, "null-field")
            .Where(body => !plan.IsAssigned(body))
            .OrderBy(body => body.UnitId)
            .FirstOrDefault();
        if (suppressor is not null)
            plan.Assign(suppressor, Role.ConvoySuppressor);

        if (wingWellIds.Count > 0)
        {
            MindBody? first = BodiesWithSignature(mind, rules, "tractor-hook")
                .Where(body => !plan.IsAssigned(body))
                .OrderBy(body => body.UnitId)
                .FirstOrDefault();
            if (first is not null)
                plan.AssignPicket(first, Role.FirstPicket, wingWellIds[0]);
        }
        if (wingWellIds.Count > 1)
        {
            MindBody? second = BodiesWithSignature(mind, rules, "survey-flare")
                .Where(body => !plan.IsAssigned(body))
                .OrderBy(body => body.UnitId)
                .FirstOrDefault();
            if (second is not null)
                plan.AssignPicket(second, Role.SecondPicket, wingWellIds[1]);
        }

        // A destroyed lead does not dissolve the route: the nearest remaining
        // unassigned body becomes the pickup until a Relay returns.
        if (plan.Protected is null)
        {
            plan.Pickup = mind.Bodies
                .Where(body => !plan.IsAssigned(body))
                .OrderBy(body => body.Position.ChebyshevDistance(mainWell))
                .ThenByDescending(body => body.Health)
                .ThenBy(body => body.UnitId)
                .FirstOrDefault();
            if (plan.Pickup is not null)
                plan.Assign(plan.Pickup, Role.MainPickup);
        }

        if (plan.MainCarrier is not null
            && plan.Catcher is not null
            && !carried.ContainsKey(plan.Catcher.UnitId)
            && ArenaBasics.CanHandoff(contract, plan.MainCarrier, plan.Catcher))
        {
            plan.HandoffSourceUnitId = plan.MainCarrier.UnitId;
            plan.HandoffTargetUnitId = plan.Catcher.UnitId;
        }
        return plan;
    }

    public static string Label(Role role) => role switch
    {
        Role.MainCarrier => "main-carrier",
        Role.RelayCatcher => "relay-catcher",
        Role.MainPickup => "main-pickup",
        Role.UpperScreen => "upper-prism-screen",
        Role.LowerScreen => "lower-prism-screen",
        Role.ConvoyMedic => "convoy-medic",
        Role.ConvoySuppressor => "convoy-suppressor",
        Role.FirstPicket => "first-well-picket",
        Role.SecondPicket => "second-well-picket",
        Role.WingReturn => "wing-core-return",
        _ => "convoy-reserve",
    };

    private static IEnumerable<MindBody> BodiesWithSignature(
        MindContext mind,
        GenericActorRulesContract.ArcRelayGameMode rules,
        string kind)
    {
        HashSet<string> classes = rules.Signatures
            .Where(signature => string.Equals(
                signature.Kind,
                kind,
                StringComparison.Ordinal))
            .Select(signature => signature.ClassId)
            .ToHashSet(StringComparer.Ordinal);
        return mind.Bodies.Where(body =>
            body.ClassId is not null && classes.Contains(body.ClassId));
    }
}
