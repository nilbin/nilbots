using System.Text.Json;
using BotArena.Sdk;

/// <summary>
/// Executes the consolidated sheet DSL as an ordered ladder. It deliberately
/// knows nothing about any shipped playbook id, role name, predicate name, or
/// route name. The linked document supplies all of those; this class supplies
/// only the fixed executor semantics shared by every direct-ladder sheet.
/// </summary>
internal sealed class DirectLadderRuntime
{
    private static readonly string[] Verbs =
    ["patrol", "intercept", "assault", "recover", "fetch", "follow"];

    private readonly Dictionary<string, RoleLadder> _ladders;
    private readonly Dictionary<(ActorIdentity Actor, string RungId), RungState>
        _states = [];
    private readonly Dictionary<int, SelectedRung> _selected = [];
    private readonly Dictionary<int, ActorIdentity> _latestLives = [];

    private DirectLadderRuntime(
        TacticalPlaybookPackage.Playbook runtimePlaybook,
        Dictionary<string, RoleLadder> ladders)
    {
        RuntimePlaybook = runtimePlaybook;
        _ladders = ladders;
    }

    internal TacticalPlaybookPackage.Playbook RuntimePlaybook { get; }

    internal string TraceSummary => _selected.Count == 0
        ? "rungs=none"
        : "rungs=" + string.Join(",", _selected
            .OrderBy(value => value.Key)
            .Select(value => $"{value.Key}:{value.Value.Rung.TraceName}"));

    internal int SelectedPriority(int unitId) =>
        _selected.GetValueOrDefault(unitId)?.Rung.Priority ?? int.MaxValue;

    internal bool IsRungActive(int unitId, string rungId) =>
        _latestLives.TryGetValue(unitId, out ActorIdentity? actor)
        && _states.TryGetValue((actor, rungId), out RungState? state)
        && state.Active;

    internal static DirectLadderRuntime Create(JsonElement source)
    {
        TacticalPlaybookPackage.Role[] roles = source.GetProperty("roles")
            .EnumerateArray().Select(ParseRole).ToArray();
        var conditions = source.GetProperty("conditions")
            .EnumerateObject().ToDictionary(
                value => value.Name,
                value => ParseCondition(value.Value),
                StringComparer.Ordinal);
        conditions.TryAdd("always", new TacticalPlaybookPackage.Condition(
            "always", "equals", 1, "", "", 0));
        conditions.TryAdd("no-live-friendlies",
            new TacticalPlaybookPackage.Condition(
                "live-friendlies", "equals", 0, "", "", 0));

        JsonElement doctrineRoot = source.GetProperty("doctrines");
        JsonElement doctrineRoles = doctrineRoot.GetProperty("roles");
        Dictionary<string, SharedCohort> cohorts = doctrineRoot
            .GetProperty("cohorts").EnumerateObject().ToDictionary(
                value => value.Name,
                value => new SharedCohort(
                    value.Name,
                    value.Value.GetProperty("take").GetInt32(),
                    value.Value.GetProperty("select").GetString()!),
                StringComparer.Ordinal);
        Dictionary<string, int> focus = doctrineRoot.GetProperty("focus")
            .EnumerateObject().ToDictionary(
                value => value.Name,
                value => value.Value.GetProperty(
                    "maximumAttackers").GetInt32(),
                StringComparer.Ordinal);

        var supportPolicies = new List<TacticalPlaybookPackage.SupportPolicy>();
        var builders = new Dictionary<string, List<RungBuilder>>(
            StringComparer.Ordinal);
        var doctrineByRole = new Dictionary<string, JsonElement>(
            StringComparer.Ordinal);
        foreach (TacticalPlaybookPackage.Role role in roles)
        {
            JsonElement doctrine = doctrineRoles.GetProperty(role.RoleId);
            doctrineByRole.Add(role.RoleId, doctrine.Clone());
            if (doctrine.TryGetProperty("repair", out JsonElement repair))
                supportPolicies.Add(ParseRepair(role.RoleId, repair));
            JsonElement[] modes = doctrine.GetProperty("modes")
                .EnumerateArray().ToArray();
            var roleBuilders = new List<RungBuilder>();
            for (int index = 0; index < modes.Length; index++)
            {
                JsonElement mode = modes[index];
                string verb = Verbs.Single(name => mode.TryGetProperty(
                    name, out _));
                string rungId = $"rung-{role.RoleId}-{index + 1}-{verb}";
                ConditionExpression? whileCondition = mode.TryGetProperty(
                        "while", out JsonElement whileText)
                    ? ParseExpression(
                        whileText.GetString()!, conditions)
                    : null;
                ConditionExpression? untilCondition = mode.TryGetProperty(
                        "until", out JsonElement untilText)
                    ? ParseExpression(
                        untilText.GetString()!, conditions)
                    : null;
                Destination? destination = verb is "patrol" or "assault"
                    ? ParseDestination(mode.GetProperty(verb))
                    : null;
                CohortSpec? cohort = mode.TryGetProperty(
                        "cohort", out JsonElement cohortValue)
                    ? ParseCohort(
                        cohortValue, cohorts, destination!, role.RoleId, index)
                    : null;
                roleBuilders.Add(new RungBuilder(
                    rungId,
                    role.RoleId,
                    index,
                    verb,
                    mode.Clone(),
                    whileCondition,
                    untilCondition,
                    destination,
                    cohort));
            }
            builders.Add(role.RoleId, roleBuilders);
        }

        var engagements = new List<TacticalPlaybookPackage.Engagement>();
        var orders = new List<TacticalPlaybookPackage.Order>();
        var fetchRungs = new List<TacticalPlaybookPackage.FetchRung>();
        var modeRungs = new List<TacticalPlaybookPackage.ModeRung>();
        var ladders = new Dictionary<string, RoleLadder>(StringComparer.Ordinal);
        foreach (TacticalPlaybookPackage.Role role in roles)
        {
            JsonElement doctrine = doctrineByRole[role.RoleId];
            List<RungBuilder> roleBuilders = builders[role.RoleId];
            Destination floor = roleBuilders[^1].Destination
                ?? throw new InvalidDataException(
                    $"Direct role '{role.RoleId}' has no patrol floor.");
            TacticalPlaybookPackage.WithdrawRung[] withdrawRungs = roleBuilders
                .Where(value => value.Verb == "patrol"
                    && value.Priority != roleBuilders[^1].Priority)
                .Select(value => new TacticalPlaybookPackage.WithdrawRung(
                    value.RungId,
                    value.Destination!.Target))
                .ToArray();
            string supportId = supportPolicies.Any(value =>
                    value.Providers.Contains(
                        role.RoleId, StringComparer.Ordinal))
                ? $"{role.RoleId}-repair"
                : "";
            var rungs = new List<Rung>();
            foreach (RungBuilder builder in roleBuilders)
            {
                if (builder.Verb == "fetch")
                {
                    string custodyId = builder.Mode.GetProperty(
                        "fetch").GetString()!;
                    fetchRungs.Add(new TacticalPlaybookPackage.FetchRung(
                        role.RoleId, custodyId, builder.Priority));
                    rungs.Add(builder.Build(fetchCustodyId: custodyId));
                    continue;
                }

                FightPolicy fight = ResolveFight(
                    doctrine, builder.Mode, role.RoleId, floor,
                    withdrawRungs);
                (string focusScope, int maximumAttackers) = ResolveFocus(
                    builder.Mode.GetProperty("focus"),
                    focus,
                    role.RoleId,
                    builder.Priority);
                TacticalPlaybookPackage.Engagement engagement =
                    BuildEngagement(
                        builder.RungId,
                        role.RoleId,
                        focusScope,
                        maximumAttackers,
                        fight);
                TacticalPlaybookPackage.Order order = BuildOrder(
                    builder,
                    doctrine,
                    fight,
                    supportId,
                    doctrine.GetProperty("custody").GetString()!);
                engagements.Add(engagement);
                orders.Add(order);
                modeRungs.Add(new TacticalPlaybookPackage.ModeRung(
                    builder.RungId, role.RoleId, builder.Priority));
                rungs.Add(builder.Build(order: order));
            }
            ladders.Add(role.RoleId, new RoleLadder(role.RoleId, rungs));
        }

        TacticalPlaybookPackage.CustodyPolicy[] custodyPolicies = source
            .GetProperty("custodyPolicies").EnumerateArray()
            .Select(value => ParseCustody(
                value,
                fetchRungs,
                conditions))
            .ToArray();
        TacticalPlaybookPackage.Group[] groups = roles.Select(role =>
            new TacticalPlaybookPackage.Group(
                role.RoleId,
                [role.RoleId],
                role.Preferred > 0 ? 1 : 0,
                role.Preferred,
                role.Maximum,
                new TacticalPlaybookPackage.Membership(
                    "stable-slot", role.DeathPolicy switch
                    {
                        "hold-vacancy" => "hold-vacancy",
                        "promote-best" => "promote-role",
                        "rebalance" => "rebalance",
                        string unknown => throw new InvalidDataException(
                            $"Direct role '{role.RoleId}' has unknown "
                            + $"deathPolicy '{unknown}'."),
                    }, "never", "unassigned"),
                new TacticalPlaybookPackage.StateMachine(
                    "active",
                    [new TacticalPlaybookPackage.LocalState(
                        "active", 0, [])])))
            .ToArray();
        string[] floorOrders = roles.Select(role =>
                ladders[role.RoleId].Rungs.Last(value =>
                    value.Order is not null).Order!.OrderId)
            .ToArray();
        JsonElement memory = source.GetProperty("memory");
        var runtime = new TacticalPlaybookPackage.Playbook
        {
            Schema = source.GetProperty("schema").GetString()!,
            PlaybookId = source.GetProperty("playbookId").GetString()!,
            AuditStatus = ParseAudit(source.GetProperty("auditStatus")),
            Composition = source.GetProperty("composition").EnumerateArray()
                .Select(value => value.GetString()!).ToArray(),
            Layout = ParseLayoutReference(source.GetProperty("layout")),
            Perspective = "team-relative",
            Memory = new TacticalPlaybookPackage.MemoryPolicy(
                memory.GetProperty("enemyUnavailableTicks").GetInt32(),
                memory.GetProperty("lastSeenEnemyTicks").GetInt32(),
                memory.GetProperty("securedCoreTicks").GetInt32(),
                memory.GetProperty("objectiveProgressTicks").GetInt32(),
                FormationStableTicks: 0),
            FetchRungs = fetchRungs.ToArray(),
            ModeRungs = modeRungs.ToArray(),
            Roles = roles,
            Groups = groups,
            Formations = [],
            Engagements = engagements.ToArray(),
            SupportPolicies = supportPolicies.ToArray(),
            CustodyPolicies = custodyPolicies,
            Orders = orders.ToArray(),
            Coordination = new TacticalPlaybookPackage.Coordination(
                "steady",
                [new TacticalPlaybookPackage.Phase(
                    "steady", 0, floorOrders, [])],
                []),
        };
        return new DirectLadderRuntime(runtime, ladders);
    }

    internal Dictionary<int, TacticalPlaybookPackage.Order> Resolve(
        int tick,
        IReadOnlyList<TacticalTaskCandidate> candidates,
        TacticalPlaybookPackage package,
        IReadOnlySet<int> recovering,
        Func<TacticalPlaybookPackage.ConditionGroup,
            TacticalTaskCandidate, int, bool> evaluate)
    {
        _selected.Clear();
        HashSet<ActorIdentity> live = candidates.Select(value => value.ActorId)
            .ToHashSet();
        foreach ((ActorIdentity actor, string rungId) in _states.Keys
                     .Where(key => !live.Contains(key.Actor)).ToArray())
            _states.Remove((actor, rungId));
        _latestLives.Clear();
        foreach (TacticalTaskCandidate candidate in candidates)
            _latestLives[candidate.UnitId] = candidate.ActorId;

        var eligible = new Dictionary<ActorIdentity, List<Rung>>();
        foreach (TacticalTaskCandidate candidate in candidates
                     .OrderBy(value => value.UnitId))
        {
            RoleLadder ladder = _ladders[candidate.RoleId];
            var bodyRungs = new List<Rung>();
            foreach (Rung rung in ladder.Rungs)
            {
                if (rung.Verb == "fetch")
                    continue;
                bool implicitGate = rung.Verb switch
                {
                    "recover" => recovering.Contains(candidate.UnitId),
                    "follow" => candidates.Any(other =>
                        other.ActorId != candidate.ActorId
                        && string.Equals(
                            other.RoleId,
                            rung.FollowRole,
                            StringComparison.Ordinal)),
                    _ => true,
                };
                (ActorIdentity, string) key = (candidate.ActorId, rung.RungId);
                RungState state = _states.GetValueOrDefault(key)
                    ?? new RungState(false, tick);
                int modeTicks = state.Active ? tick - state.StartedTick : 0;
                bool until = state.Active
                    && (rung.Until?.Matches(
                            group => evaluate(group, candidate, modeTicks))
                        ?? false);
                if (state.Active && (!implicitGate || until))
                    state = new RungState(false, tick);
                if (!state.Active
                    && implicitGate
                    && (rung.While?.Matches(
                            group => evaluate(group, candidate, 0))
                        ?? true))
                {
                    state = new RungState(true, tick);
                }
                _states[key] = state;
                if (state.Active)
                    bodyRungs.Add(rung);
            }
            eligible[candidate.ActorId] = bodyRungs
                .OrderBy(value => value.Priority).ToList();
        }

        var cursor = candidates.ToDictionary(
            value => value.ActorId, _ => 0);
        var settled = new HashSet<ActorIdentity>();
        while (settled.Count < candidates.Count)
        {
            var waiting = new Dictionary<string,
                List<(TacticalTaskCandidate Candidate, Rung Rung)>>(
                StringComparer.Ordinal);
            foreach (TacticalTaskCandidate candidate in candidates
                         .OrderBy(value => value.UnitId))
            {
                if (settled.Contains(candidate.ActorId))
                    continue;
                List<Rung> ladder = eligible[candidate.ActorId];
                int index = cursor[candidate.ActorId];
                if (index >= ladder.Count)
                {
                    throw new InvalidDataException(
                        $"Direct role '{candidate.RoleId}' has no active floor.");
                }
                Rung rung = ladder[index];
                if (rung.Cohort is null)
                {
                    Select(candidate, rung);
                    settled.Add(candidate.ActorId);
                    continue;
                }
                if (!waiting.TryGetValue(
                        rung.Cohort.CohortId, out var members))
                {
                    members = [];
                    waiting.Add(rung.Cohort.CohortId, members);
                }
                members.Add((candidate, rung));
            }
            if (waiting.Count == 0)
                break;
            foreach ((_, var members) in waiting.OrderBy(
                         value => value.Key, StringComparer.Ordinal))
            {
                CohortSpec policy = members[0].Rung.Cohort!;
                HashSet<ActorIdentity> winners = members
                    .OrderBy(value => Distance(
                        package, value.Candidate, policy.Destination))
                    .ThenBy(value => value.Candidate.UnitId)
                    .Take(policy.Take)
                    .Select(value => value.Candidate.ActorId)
                    .ToHashSet();
                foreach ((TacticalTaskCandidate candidate, Rung rung) in
                         members)
                {
                    if (winners.Contains(candidate.ActorId))
                    {
                        Select(candidate, rung);
                        settled.Add(candidate.ActorId);
                    }
                    else
                    {
                        cursor[candidate.ActorId]++;
                    }
                }
            }
        }
        return _selected.ToDictionary(
            value => value.Key,
            value => value.Value.Rung.Order
                ?? throw new InvalidDataException(
                    "A fetch rung cannot become the movement directive."));
    }

    private void Select(TacticalTaskCandidate candidate, Rung rung) =>
        _selected[candidate.UnitId] = new SelectedRung(candidate.ActorId, rung);

    private static int Distance(
        TacticalPlaybookPackage package,
        TacticalTaskCandidate candidate,
        Destination destination) => destination.Kind switch
    {
        "route" => package.RoutePoints(destination.Target)
            .Select(candidate.Position.ChebyshevDistance)
            .DefaultIfEmpty(int.MaxValue).Min(),
        "zone" => package.ZoneDistance(destination.Target, candidate.Position),
        _ => throw new InvalidDataException(
            $"Unknown direct cohort destination '{destination.Kind}'."),
    };

    private static TacticalPlaybookPackage.Role ParseRole(JsonElement value) =>
        new(
            value.GetProperty("roleId").GetString()!,
            Strings(value.GetProperty("candidateClasses")),
            value.GetProperty("minimum").GetInt32(),
            value.GetProperty("preferred").GetInt32(),
            value.GetProperty("maximum").GetInt32(),
            value.GetProperty("carrierPreference").GetString()!,
            value.GetProperty("deathPolicy").GetString()!,
            value.GetProperty("respawnPolicy").GetString()!,
            value.GetProperty("overflowRoleId").GetString()!,
            value.TryGetProperty("build", out JsonElement build)
                ? Strings(build) : null);

    private static TacticalPlaybookPackage.AuditStatus ParseAudit(
        JsonElement value) => new(
        value.GetProperty("provisionalEvaluationOnly").GetBoolean(),
        value.GetProperty("playerFacingProductSchema").GetBoolean());

    private static TacticalPlaybookPackage.LayoutReference ParseLayoutReference(
        JsonElement value) => new(
        value.GetProperty("path").GetString()!,
        value.GetProperty("sha256").GetString()!);

    private static TacticalPlaybookPackage.Condition ParseCondition(
        JsonElement value) => new(
        value.GetProperty("fact").GetString()!,
        value.GetProperty("operator").GetString()!,
        value.GetProperty("value").GetInt32(),
        value.TryGetProperty("subject", out JsonElement subject)
            ? subject.GetString()! : "",
        value.TryGetProperty("zone", out JsonElement zone)
            ? zone.GetString()! : "",
        value.TryGetProperty("freshnessTicks", out JsonElement freshness)
            ? freshness.GetInt32() : 0,
        value.TryGetProperty("valueParameter", out JsonElement parameter)
            ? parameter.GetString()! : "");

    private static ConditionExpression ParseExpression(
        string text,
        IReadOnlyDictionary<string, TacticalPlaybookPackage.Condition>
            conditions)
    {
        TacticalPlaybookPackage.ConditionGroup[] groups = text
            .Split(" or ", StringSplitOptions.None)
            .Select(alternative => new TacticalPlaybookPackage.ConditionGroup(
                alternative.Split(" and ", StringSplitOptions.None)
                    .Select(token => conditions[token.Trim()]).ToArray(),
                []))
            .ToArray();
        return new ConditionExpression(groups);
    }

    private static Destination ParseDestination(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return new Destination("route", value.GetString()!);
        if (value.TryGetProperty("route", out JsonElement route))
            return new Destination("route", route.GetString()!);
        return new Destination(
            "zone", value.GetProperty("zone").GetString()!);
    }

    private static CohortSpec ParseCohort(
        JsonElement value,
        IReadOnlyDictionary<string, SharedCohort> shared,
        Destination destination,
        string role,
        int index)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            SharedCohort policy = shared[value.GetString()!];
            return new CohortSpec(
                policy.Id, policy.Take, policy.Select, destination);
        }
        return new CohortSpec(
            $"inline-{role}-{index + 1}",
            value.GetProperty("take").GetInt32(),
            value.GetProperty("select").GetString()!,
            destination);
    }

    private static TacticalPlaybookPackage.SupportPolicy ParseRepair(
        string role,
        JsonElement value) => new(
        $"{role}-repair",
        [role],
        Strings(value.GetProperty("prefer")).Select(term => term switch
        {
            "weakest" => "lowest-health",
            "carrier" => "carrier",
            "medic" => "medic",
            "focus" => "focus-participant",
            "anchor" => "formation-anchor",
            "any" => "any",
            _ => throw new InvalidDataException(
                $"Unknown repair preference '{term}'."),
        }).ToArray(),
        value.GetProperty("maximumProvidersPerTarget").GetInt32(),
        value.GetProperty("minimumProviderSeparation").GetInt32(),
        value.GetProperty("reserveHealthPercent").GetInt32(),
        value.GetProperty("survivalFallback").GetString()!);

    private static TacticalPlaybookPackage.CustodyPolicy ParseCustody(
        JsonElement value,
        IReadOnlyCollection<TacticalPlaybookPackage.FetchRung> fetchRungs,
        IReadOnlyDictionary<string, TacticalPlaybookPackage.Condition>
            conditions)
    {
        string id = value.GetProperty("custodyId").GetString()!;
        string[] authorized = fetchRungs
            .Where(rung => string.Equals(
                rung.CustodyId, id, StringComparison.Ordinal))
            .Select(rung => rung.RoleId).Distinct(StringComparer.Ordinal)
            .ToArray();
        TacticalPlaybookPackage.DeliveryRoute[]? deliveryRoutes =
            value.TryGetProperty("deliveryRoutes", out JsonElement routes)
                ? routes.EnumerateArray().Select(rule =>
                    new TacticalPlaybookPackage.DeliveryRoute(
                        rule.GetProperty("zone").GetString()!,
                        rule.GetProperty("route").GetString()!)).ToArray()
                : null;
        TacticalPlaybookPackage.BaitDrop? bait = null;
        if (value.TryGetProperty("baitDrop", out JsonElement baitValue))
        {
            bait = new TacticalPlaybookPackage.BaitDrop(
                baitValue.GetProperty("zone").GetString()!,
                ParseExpression(
                    baitValue.GetProperty("reclaim").GetString()!,
                    conditions).Groups);
        }
        return new TacticalPlaybookPackage.CustodyPolicy(
            id,
            authorized,
            authorized,
            Strings(value.GetProperty("sourceWells")),
            value.GetProperty("pickupReservationTicks").GetInt32(),
            value.GetProperty("transferTimeoutTicks").GetInt32(),
            value.GetProperty("deliveryTimeoutTicks").GetInt32(),
            value.GetProperty("accidentalPickup").GetString()!,
            value.GetProperty("dropRecovery").GetString()!,
            value.GetProperty("unreachableFallback").GetString()!,
            ParseExpression(
                value.GetProperty("safeConversion").GetString()!,
                conditions).Groups,
            value.TryGetProperty("emergencyRecoveryZones", out JsonElement ez)
                ? Strings(ez) : null,
            IntOr(value, "emergencyPickupRadius", 0),
            value.TryGetProperty("emergencyRecovery", out JsonElement er)
                ? ParseExpression(er.GetString()!, conditions).Groups : null,
            value.TryGetProperty("emergencyRecoveryRoles", out JsonElement rr)
                ? Strings(rr) : null,
            value.TryGetProperty(
                "emergencyRecoverySourceWells", out JsonElement rw)
                ? Strings(rw) : null,
            StringOrNull(value, "emergencyRecoveryDisposition"),
            StringOrNull(value, "emergencyDisplacementTarget"),
            IntOr(value, "emergencyDisplacementReleaseRadius", 0),
            deliveryRoutes,
            StringOr(value, "forwardPass", "none"),
            bait);
    }

    private static TacticalPlaybookPackage.Order BuildOrder(
        RungBuilder rung,
        JsonElement doctrine,
        FightPolicy fight,
        string supportId,
        string custodyId)
    {
        TacticalPlaybookPackage.Movement movement = rung.Verb switch
        {
            "patrol" => new TacticalPlaybookPackage.Movement(
                rung.Destination!.Target == "traffic"
                    ? "shadow-traffic" : rung.Destination.Kind,
                rung.Destination.Target == "traffic"
                    ? "" : rung.Destination.Target,
                2, "continuous", 10, "repath", fight.ChaseLeash,
                "free", Scan: "sweep"),
            "intercept" => new TacticalPlaybookPackage.Movement(
                rung.Mode.GetProperty("intercept").GetString() == "inbound"
                    ? "incoming-cutoff" : "enemy-carrier-cutoff",
                StringOr(rung.Mode, "from", ""),
                2, "continuous", 8, "repath", fight.ChaseLeash,
                "free", LeadTiles: 1),
            "recover" => new TacticalPlaybookPackage.Movement(
                "heal-beacon", "", 2, "continuous", 8, "repath",
                fight.ChaseLeash, "free"),
            "follow" => new TacticalPlaybookPackage.Movement(
                "hold", "", 2, "continuous", 8, "repath",
                fight.ChaseLeash, "free"),
            _ => new TacticalPlaybookPackage.Movement(
                rung.Destination!.Kind, rung.Destination.Target,
                2, "continuous", 10, "repath", fight.ChaseLeash, "free"),
        };
        string[]? scavenge = doctrine.TryGetProperty(
                "scavenge", out JsonElement collect)
            ? collect.ValueKind == JsonValueKind.String
                ? [collect.GetString()!]
                : Strings(collect)
            : null;
        TacticalPlaybookPackage.Follow? follow = rung.Verb == "follow"
            ? new TacticalPlaybookPackage.Follow(
                rung.Mode.GetProperty("follow").GetString()!,
                "nearest",
                rung.Mode.GetProperty("posture").GetString()!,
                2)
            : null;
        return new TacticalPlaybookPackage.Order(
            rung.RungId,
            rung.RoleId,
            rung.Priority,
            new TacticalPlaybookPackage.MemberSelection("all", null, null, null),
            movement,
            "",
            rung.RungId,
            supportId,
            custodyId,
            "active",
            new TacticalPlaybookPackage.Fallback(
                "reflow", "continue", "hold", ""),
            doctrine.TryGetProperty("conceal", out JsonElement conceal)
                && !conceal.GetBoolean() ? "" : "ambush",
            CollectZones: scavenge,
            Follow: follow,
            // Follow posture is interpreted by the follower primitive. The
            // order posture is a separate compact formation offset used by
            // patrol/assault rungs; feeding "trail" into both would make
            // the generic target offset parser guess at follower semantics.
            Posture: rung.Verb == "follow"
                ? ""
                : StringOr(rung.Mode, "posture", ""));
    }

    private static (string Scope, int MaximumAttackers) ResolveFocus(
        JsonElement value,
        IReadOnlyDictionary<string, int> shared,
        string role,
        int index)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string id = value.GetString()!;
            return ($"focus:{id}", shared[id]);
        }
        return ($"focus:inline:{role}-{index + 1}",
            value.GetProperty("maximumAttackers").GetInt32());
    }

    private static FightPolicy ResolveFight(
        JsonElement doctrine,
        JsonElement mode,
        string role,
        Destination floor,
        TacticalPlaybookPackage.WithdrawRung[] withdrawRungs)
    {
        JsonElement baseFight = doctrine.TryGetProperty(
            "fight", out JsonElement baseValue) ? baseValue : default;
        JsonElement modeFight = mode.TryGetProperty(
            "fight", out JsonElement modeValue) ? modeValue : default;
        int chaseLeash = FightInt(modeFight, baseFight, "chase", "leash", 0);
        int persist = FightInt(
            modeFight, baseFight, "chase", "persistTicks", 0);
        int threats = FightInt(
            modeFight, baseFight, "breakOff", "threats", 0);
        int breakHealth = FightInt(
            modeFight, baseFight, "breakOff", "health", 0);
        string? from = FightString(
            modeFight, baseFight, "engage", "from", null);
        string[] prefer = FightStrings(
            modeFight, baseFight, "targets", "prefer",
            ["carrier", "weakest"])
            .Select(term => term switch
            {
                "carrier" => "enemy-carrier",
                "weakest" => "lowest-health",
                "anchor" => "closest-to-anchor",
                "closest" => "closest-to-reactor",
                "strongest-threat" => "highest-threat",
                "freshest" => "fresh-respawn",
                _ => throw new InvalidDataException(
                    $"Unknown fight target preference '{term}'."),
            }).ToArray();
        return new FightPolicy(
            role,
            prefer,
            FightString(modeFight, baseFight, "", "collect", "yield")!,
            FightString(modeFight, baseFight, "", "heal", "yield")!,
            FightInt(modeFight, baseFight, "targets", "lone", 0),
            FightInt(modeFight, baseFight, "engage", "within", 0),
            FightInt(
                modeFight, baseFight, "engage", "killableTicks", 0),
            from,
            FightInt(
                modeFight, baseFight, "engage", "positionTicks", 16),
            FightString(modeFight, baseFight, "engage", "else", "strike")!,
            chaseLeash,
            FightBool(
                modeFight, baseFight, "chase", "onlyCatchable", false),
            FightInt(
                modeFight, baseFight, "chase", "executeBelowHealth", 0),
            persist,
            threats,
            FightInt(modeFight, baseFight, "breakOff", "within", 0),
            FightInt(
                modeFight, baseFight, "breakOff", "memoryTicks", 0),
            FightInt(
                modeFight, baseFight, "breakOff", "recoverTicks", 24),
            breakHealth,
            FightInt(modeFight, baseFight, "defense", "radius", 2),
            FightBool(modeFight, baseFight, "defense", "return", true),
            floor.Target,
            withdrawRungs);
    }

    private static TacticalPlaybookPackage.Engagement BuildEngagement(
        string id,
        string role,
        string focusScope,
        int maximumAttackers,
        FightPolicy fight)
    {
        TacticalPlaybookPackage.Commit? commit = null;
        bool gated = fight.BreakHealth > 0
            || fight.KillableTicks > 0
            || fight.From is not null
            || fight.OnlyCatchable
            || fight.ExecuteBelowHealth > 0
            || fight.Threats > 0;
        if (gated)
        {
            TacticalPlaybookPackage.CommitDisengageWhen? disengage =
                fight.Threats > 0 || fight.BreakHealth > 0
                    ? new TacticalPlaybookPackage.CommitDisengageWhen(
                        fight.Threats,
                        fight.FloorRoute,
                        fight.WithdrawRungs,
                        fight.RecoverTicks,
                        fight.BreakHealth)
                    : null;
            commit = new TacticalPlaybookPackage.Commit(
                new TacticalPlaybookPackage.CommitEngageWhen(
                    KillWithinTicks: fight.KillableTicks),
                fight.Threats > 0
                    ? new TacticalPlaybookPackage.CommitAwareness(
                        fight.ThreatsWithin, fight.MemoryTicks)
                    : null,
                fight.ChaseLeash > 0
                    || fight.OnlyCatchable
                    || fight.ExecuteBelowHealth > 0
                    ? new TacticalPlaybookPackage.CommitChase(
                        fight.ChaseLeash,
                        fight.OnlyCatchable,
                        fight.ExecuteBelowHealth)
                    : null,
                disengage,
                [role],
                fight.From is not null
                    ? new TacticalPlaybookPackage.CommitApproach(
                        fight.From, fight.PositionTicks, fight.Else)
                    : null);
        }
        return new TacticalPlaybookPackage.Engagement(
            id,
            [role],
            fight.Prefer,
            ["distance", "health", "unit-id", "life-id"],
            focusScope,
            4,
            "urgent-carrier",
            maximumAttackers,
            0,
            Math.Min(fight.ChaseLeash, 16),
            "rotate-to-engage",
            "damage-first",
            new TacticalPlaybookPackage.ReleasePolicy(
                fight.PersistTicks > 0 ? fight.PersistTicks : 2,
                fight.PersistTicks > 0 ? fight.PersistTicks : 3,
                true,
                true),
            new TacticalPlaybookPackage.SelfDefensePolicy(
                fight.DefenseRadius > 0,
                fight.DefenseRadius,
                fight.DefenseReturn),
            HoldFire: fight.Within > 0
                ? new TacticalPlaybookPackage.HoldFire(fight.Within)
                : null,
            Isolation: fight.Lone > 0
                ? new TacticalPlaybookPackage.Isolation(fight.Lone)
                : null,
            Commit: commit,
            Collect: fight.Collect,
            Heal: fight.Heal);
    }

    private static int FightInt(
        JsonElement mode,
        JsonElement doctrine,
        string block,
        string key,
        int fallback)
    {
        foreach (JsonElement fight in new[] { mode, doctrine })
        {
            if (fight.ValueKind != JsonValueKind.Object)
                continue;
            JsonElement owner = fight;
            if (block.Length > 0
                && !fight.TryGetProperty(block, out owner))
                continue;
            if (owner.TryGetProperty(key, out JsonElement value))
                return value.GetInt32();
        }
        return fallback;
    }

    private static bool FightBool(
        JsonElement mode,
        JsonElement doctrine,
        string block,
        string key,
        bool fallback)
    {
        foreach (JsonElement fight in new[] { mode, doctrine })
        {
            if (fight.ValueKind != JsonValueKind.Object)
                continue;
            JsonElement owner = fight;
            if (block.Length > 0
                && !fight.TryGetProperty(block, out owner))
                continue;
            if (owner.TryGetProperty(key, out JsonElement value))
                return value.GetBoolean();
        }
        return fallback;
    }

    private static string? FightString(
        JsonElement mode,
        JsonElement doctrine,
        string block,
        string key,
        string? fallback)
    {
        foreach (JsonElement fight in new[] { mode, doctrine })
        {
            if (fight.ValueKind != JsonValueKind.Object)
                continue;
            JsonElement owner = fight;
            if (block.Length > 0
                && !fight.TryGetProperty(block, out owner))
                continue;
            if (owner.TryGetProperty(key, out JsonElement value))
                return value.GetString();
        }
        return fallback;
    }

    private static string[] FightStrings(
        JsonElement mode,
        JsonElement doctrine,
        string block,
        string key,
        string[] fallback)
    {
        foreach (JsonElement fight in new[] { mode, doctrine })
        {
            if (fight.ValueKind != JsonValueKind.Object
                || !fight.TryGetProperty(block, out JsonElement owner))
                continue;
            if (owner.TryGetProperty(key, out JsonElement value))
                return Strings(value);
        }
        return fallback;
    }

    private static string[] Strings(JsonElement value) => value
        .EnumerateArray().Select(entry => entry.GetString()!).ToArray();

    private static int IntOr(JsonElement value, string name, int fallback) =>
        value.TryGetProperty(name, out JsonElement found)
            ? found.GetInt32() : fallback;

    private static string StringOr(
        JsonElement value,
        string name,
        string fallback) => value.TryGetProperty(name, out JsonElement found)
        ? found.GetString()! : fallback;

    private static string? StringOrNull(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement found)
            ? found.GetString() : null;

    private sealed record RoleLadder(string RoleId, List<Rung> Rungs);

    private sealed record RungBuilder(
        string RungId,
        string RoleId,
        int Priority,
        string Verb,
        JsonElement Mode,
        ConditionExpression? While,
        ConditionExpression? Until,
        Destination? Destination,
        CohortSpec? Cohort)
    {
        internal Rung Build(
            TacticalPlaybookPackage.Order? order = null,
            string? fetchCustodyId = null) => new(
            RungId,
            RoleId,
            Priority,
            Verb,
            While,
            Until,
            Cohort,
            order,
            Verb == "follow" ? Mode.GetProperty("follow").GetString() : null,
            fetchCustodyId);
    }

    private sealed record Rung(
        string RungId,
        string RoleId,
        int Priority,
        string Verb,
        ConditionExpression? While,
        ConditionExpression? Until,
        CohortSpec? Cohort,
        TacticalPlaybookPackage.Order? Order,
        string? FollowRole,
        string? FetchCustodyId)
    {
        internal string TraceName => $"{RoleId}[{Priority + 1}]/{Verb}";
    }

    private sealed record ConditionExpression(
        TacticalPlaybookPackage.ConditionGroup[] Groups)
    {
        internal bool Matches(
            Func<TacticalPlaybookPackage.ConditionGroup, bool> evaluate) =>
            Groups.Any(evaluate);
    }

    private sealed record SharedCohort(string Id, int Take, string Select);
    private sealed record CohortSpec(
        string CohortId,
        int Take,
        string Select,
        Destination Destination);
    private sealed record Destination(string Kind, string Target);
    private sealed record RungState(bool Active, int StartedTick);
    private sealed record SelectedRung(ActorIdentity ActorId, Rung Rung);

    private sealed record FightPolicy(
        string Role,
        string[] Prefer,
        string Collect,
        string Heal,
        int Lone,
        int Within,
        int KillableTicks,
        string? From,
        int PositionTicks,
        string Else,
        int ChaseLeash,
        bool OnlyCatchable,
        int ExecuteBelowHealth,
        int PersistTicks,
        int Threats,
        int ThreatsWithin,
        int MemoryTicks,
        int RecoverTicks,
        int BreakHealth,
        int DefenseRadius,
        bool DefenseReturn,
        string FloorRoute,
        TacticalPlaybookPackage.WithdrawRung[] WithdrawRungs);
}
