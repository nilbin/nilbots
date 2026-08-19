using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Validation for the player-owned Arc Relay Tactics v2 document and the
/// compiler entry that materializes it into the executable Orders v3 pair.
/// Tactics are the only authored source; the materialized pair is regenerated
/// wholesale on every compile and is never hand-edited. Saved v1 documents
/// migrate deterministically on load (Stage → phase 1, Release → phase 1's
/// transition, Act → phase 2, Finish → phase 2's finish transition).
/// </summary>
public static partial class ArcRelayTacticalPlaybookCompiler
{
    public const string TacticsSchema = "arc-relay-tactics-v1";
    public const string TacticsSchemaV2 = "arc-relay-tactics-v2";

    private static readonly HashSet<string> TacticRecipes =
        new(StringComparer.Ordinal)
        {
            "hold-area", "centre-rush", "pincer", "core-raid", "patrol",
            "deliver-core", "blank",
        };

    private static readonly HashSet<string> TacticBuilds =
        new(StringComparer.Ordinal)
        { "balanced", "durable", "aggressive", "scout" };

    private static readonly HashSet<string> TacticPostures =
        new(StringComparer.Ordinal)
        { "wait", "hold", "aggressive", "steal", "escort", "observe" };

    private static readonly HashSet<string> TacticRecoveries =
        new(StringComparer.Ordinal)
        { "fight-on", "patch", "heal-tile", "heal-up" };

    /// <summary>One job vocabulary for every phase: the old stage and act
    /// kinds merged. 'none' carries the group's nearest earlier geometric
    /// job forward under this phase's posture.</summary>
    private static readonly HashSet<string> TacticJobKinds =
        new(StringComparer.Ordinal)
        {
            "posts", "area", "line", "approach", "converge", "advance",
            "patrol", "return", "escort", "none",
        };

    private static readonly HashSet<string> TacticWells =
        new(StringComparer.Ordinal)
        { "north", "centre", "south", "own", "enemy" };

    private static readonly HashSet<string> TacticDurations =
        new(StringComparer.Ordinal) { "none", "brief", "standard", "long" };

    /// <summary>Which condition rows each case context accepts.</summary>
    private static readonly string[] TacticStartRows =
    [
        "enemiesAt", "coreLoose", "coreSoon", "enemiesDown", "carrier",
        "losses", "lossesWithin",
    ];

    private static readonly string[] TacticTransitionRows =
    [
        "groupsSet", "held", "healed", "contact", "enemiesAt", "coreLoose",
        "coreSoon", "enemiesDown", "kills", "losses", "cleared",
        "coreAcquired", "coreBanked", "coreLost", "reached", "observed",
    ];

    private static readonly string[] TacticAbortRows =
    [
        "losses", "lossesWithin", "coreLost", "enemiesAt",
    ];

    /// <summary>Spatial signature mark budgets for classes whose descriptor
    /// admits an exact anchor. All other geometry kinds derive from the
    /// descriptor capabilities (RouteForward → line lane, longshot's rail is
    /// the one named-target line special-case).</summary>
    private static readonly Dictionary<string, int> TacticAnchorMarkBudgets =
        new(StringComparer.Ordinal)
        {
            ["lantern"] = 2, ["mortar"] = 2, ["minesmith"] = 3,
            ["mason"] = 2, ["veil"] = 2, ["nest"] = 2,
        };

    /// <summary>
    /// Compile a Tactics document end to end: migrate a v1 document to v2,
    /// validate the player source, deterministically materialize the Orders
    /// v3 + map-plan pair, and run the existing shared compile over the
    /// materialized pair. The returned compilation carries the tactics hash
    /// and both materialized sources.
    /// </summary>
    public static TacticalPlaybookCompilation CompileTactics(
        byte[] tacticsSource,
        string tacticsName = "tactics.json")
    {
        ArgumentNullException.ThrowIfNull(tacticsSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(tacticsName);
        byte[] current = MigrateTactics(tacticsSource, tacticsName);
        using JsonDocument document = Parse(current, tacticsName);
        ArcRelayTacticsDocument tactics = ValidateTacticsDocument(
            document.RootElement, tacticsName);
        ArcRelayTacticsMaterialization materialized =
            ArcRelayTacticsMaterializer.Materialize(tactics);
        TacticalPlaybookCompilation compilation;
        try
        {
            compilation = Compile(
                materialized.OrdersJson,
                materialized.MapPlanJson,
                $"{tacticsName}(materialized-orders)",
                $"{tacticsName}(materialized-map-plan)");
        }
        catch (InvalidDataException error) when (
            error.Message.StartsWith(
                "Compiled Arc Relay sheet is", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "tactics: this sheet materializes past the runtime orders "
                + $"budget ({error.Message}) — reuse the same ground across "
                + "tactics or simplify groups, marks, and conditions.",
                error);
        }
        return compilation with
        {
            TacticsSha256 = Sha256(current),
            MaterializedOrders = materialized.OrdersJson,
            MaterializedMapPlan = materialized.MapPlanJson,
        };
    }

    /// <summary>
    /// Deterministic v1 → v2 document migration. A v2 (or unrecognized)
    /// document passes through unchanged; malformed v1 fragments are left
    /// as they are so validation reports them with real paths. Hosted saves
    /// and the editor both migrate before validating, so v2 is the only
    /// validated and stored shape.
    /// </summary>
    public static byte[] MigrateTactics(
        byte[] tacticsSource,
        string tacticsName = "tactics.json")
    {
        ArgumentNullException.ThrowIfNull(tacticsSource);
        using (JsonDocument probe = Parse(tacticsSource, tacticsName))
        {
            if (probe.RootElement.ValueKind != JsonValueKind.Object
                || !probe.RootElement.TryGetProperty(
                    "schema", out JsonElement schema)
                || schema.ValueKind != JsonValueKind.String
                || !string.Equals(schema.GetString(), TacticsSchema,
                    StringComparison.Ordinal))
            {
                return tacticsSource;
            }
        }
        JsonObject document = JsonNode.Parse(tacticsSource)!.AsObject();
        document["schema"] = TacticsSchemaV2;
        if (document["tactics"] is JsonArray tactics)
        {
            foreach (JsonNode? entry in tactics)
            {
                if (entry is JsonObject tactic)
                    MigrateTactic(tactic);
            }
        }
        return Encoding.UTF8.GetBytes(document.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void MigrateTactic(JsonObject tactic)
    {
        // start: OR-joined rows become one case of triggers.
        if (tactic["start"] is JsonObject start
            && start["conds"] is JsonObject startConds)
        {
            start.Remove("conds");
            start["cases"] = new JsonArray(new JsonObject
            {
                ["triggers"] = startConds.DeepClone(),
            });
        }

        string releaseMode =
            (tactic["release"] as JsonObject)?["mode"]?.GetValue<string>()
            ?? "immediate";
        JsonObject? releaseConds =
            (tactic["release"] as JsonObject)?["conds"]?.DeepClone()
                as JsonObject;
        tactic.Remove("release");

        // finish → tactic-wide maxDuration plus the final phase's finish
        // transition; 'persistent' becomes the explicit empty continue.
        JsonObject? outcomes = null;
        string maxAttempt = "none";
        if (tactic["finish"] is JsonObject finish)
        {
            maxAttempt = finish["maxAttempt"]?.GetValue<string>() ?? "none";
            outcomes = finish["outcomes"]?.DeepClone() as JsonObject;
            outcomes?.Remove("persistent");
        }
        tactic.Remove("finish");
        if (maxAttempt != "none")
            tactic["maxDuration"] = maxAttempt;

        // Groups shrink to membership; their stage/act/sigs become phases.
        var stageJobs = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        var actJobs = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        var order = new List<string>();
        bool anyStage = false;
        if (tactic["groups"] is JsonArray groups)
        {
            foreach (JsonNode? entry in groups)
            {
                if (entry is not JsonObject group
                    || group["id"]?.GetValue<string>() is not string groupId)
                    continue;
                order.Add(groupId);
                JsonObject? sigs = group["sigs"] as JsonObject;
                JsonObject stage = MigratedJob(
                    group["stage"] as JsonObject,
                    sigs?["stage"] as JsonObject);
                JsonObject act = MigratedJob(
                    group["act"] as JsonObject,
                    sigs?["act"] as JsonObject);
                anyStage |= stage["kind"]?.GetValue<string>() is string kind
                    && kind != "none";
                stageJobs[groupId] = stage;
                actJobs[groupId] = act;
                group.Remove("stage");
                group.Remove("act");
                group.Remove("sigs");
            }
        }

        JsonArray? finishContinue = outcomes is { Count: > 0 }
            ? new JsonArray(new JsonObject
            {
                ["triggers"] = outcomes,
                ["to"] = "finish",
            })
            : null;
        var phases = new JsonArray();
        // An immediate release (or a plan with no staged group at all) was a
        // single-phase tactic: the act jobs, with act 'none' groups holding
        // their stage geometry under the act posture.
        if (releaseMode == "immediate" || !anyStage)
        {
            var jobs = new JsonObject();
            foreach (string groupId in order)
            {
                jobs[groupId] = ActOrCarriedStage(
                    actJobs[groupId], stageJobs[groupId]);
            }
            var phase = new JsonObject
            {
                ["id"] = "act",
                ["name"] = "Act",
                ["jobs"] = jobs,
            };
            if (finishContinue is not null)
                phase["continue"] = finishContinue;
            phases.Add(phase);
        }
        else
        {
            var stagePhaseJobs = new JsonObject();
            var actPhaseJobs = new JsonObject();
            foreach (string groupId in order)
            {
                stagePhaseJobs[groupId] = stageJobs[groupId];
                // Act 'none' now means "carry the previous phase's ground
                // under this posture" — exactly the old continuation.
                actPhaseJobs[groupId] = actJobs[groupId];
            }
            var releaseRule = new JsonObject
            {
                ["required"] = new JsonObject { ["groupsSet"] = true },
                ["to"] = "next",
            };
            if (releaseConds is { Count: > 0 })
                releaseRule["triggers"] = releaseConds;
            phases.Add(new JsonObject
            {
                ["id"] = "stage",
                ["name"] = "Stage",
                ["jobs"] = stagePhaseJobs,
                ["continue"] = new JsonArray(releaseRule),
            });
            var actPhase = new JsonObject
            {
                ["id"] = "act",
                ["name"] = "Act",
                ["jobs"] = actPhaseJobs,
            };
            if (finishContinue is not null)
                actPhase["continue"] = finishContinue;
            phases.Add(actPhase);
        }
        tactic["phases"] = phases;
    }

    private static JsonObject WithoutNulls(this JsonObject value)
    {
        foreach (string name in value
                     .Where(property => property.Value is null)
                     .Select(property => property.Key).ToArray())
            value.Remove(name);
        return value;
    }

    /// <summary>A v1 stage/act object plus its signature marks, as one v2
    /// job. Missing fragments migrate to a quiet 'none' job so validation
    /// reports on the migrated shape with real paths.</summary>
    private static JsonObject MigratedJob(
        JsonObject? phase,
        JsonObject? sigs)
    {
        JsonObject job = phase?.DeepClone() as JsonObject ?? new JsonObject
        {
            ["kind"] = "none",
            ["posture"] = "wait",
            ["recovery"] = "fight-on",
        };
        if (sigs is { Count: > 0 })
            job["sigs"] = sigs.DeepClone();
        return job;
    }

    /// <summary>The single-phase (immediate) form: the act job, or — for an
    /// act of 'none' — the stage geometry under the act's posture, because a
    /// lone phase has no earlier phase to carry forward from.</summary>
    private static JsonObject ActOrCarriedStage(JsonObject act, JsonObject stage)
    {
        if (act["kind"]?.GetValue<string>() != "none"
            || stage["kind"]?.GetValue<string>() == "none")
        {
            return act;
        }
        JsonObject carried = stage.DeepClone().AsObject();
        carried["posture"] = act["posture"]?.DeepClone();
        carried["recovery"] = act["recovery"]?.DeepClone();
        if (act["sigs"] is JsonObject actSigs)
            carried["sigs"] = actSigs.DeepClone();
        else
            carried.Remove("sigs");
        return carried.WithoutNulls();
    }

    internal static ArcRelayTacticsDocument ValidateTacticsDocument(
        JsonElement root,
        string path)
    {
        Object(root, path,
            [
                "schema", "sheetId", "mapId", "mapWidth", "mapHeight",
                "squad", "tactics",
            ]);
        Exact(root, "schema", TacticsSchemaV2, path);
        string sheetId = Identifier(root, "sheetId", path);
        string mapId = NonEmptyString(root, "mapId", path);
        int width = Range(root, "mapWidth", path, 8, 64);
        int height = Range(root, "mapHeight", path, 8, 64);

        JsonElement[] squadValues = BoundedArray(
            root.GetProperty("squad"), $"{path}.squad", 8, 8);
        var squad = new List<ArcRelayTacticsSquadBot>(8);
        var classCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int slot = 0; slot < squadValues.Length; slot++)
        {
            string at = $"{path}.squad[{slot}]";
            JsonElement bot = squadValues[slot];
            Object(bot, at, ["slot", "class", "build", "name"]);
            if (RequiredInt(bot, "slot", at) != slot)
                throw Error(at, $"squad slots must be 0..7 in order.");
            string classId = RequiredString(bot, "class", at);
            if (!ArcRelaySignatureAuthoringCatalog.All.Any(value =>
                    string.Equals(value.ClassId, classId, StringComparison.Ordinal)))
                throw Error(at, $"unknown class '{classId}'.");
            classCounts[classId] = classCounts.GetValueOrDefault(classId) + 1;
            if (classCounts[classId] > 2)
                throw Error(at, $"at most two bots may share class '{classId}'.");
            string build = RequiredString(bot, "build", at);
            if (!TacticBuilds.Contains(build))
                throw Error(at, $"unknown build '{build}'.");
            string name = NonEmptyString(bot, "name", at);
            if (name.Length > 24)
                throw Error(at, "bot name exceeds 24 characters.");
            squad.Add(new ArcRelayTacticsSquadBot(slot, classId, build, name));
        }

        JsonElement[] tacticValues = BoundedArray(
            root.GetProperty("tactics"), $"{path}.tactics", 1, 8);
        var tactics = new List<ArcRelayTactic>(tacticValues.Length);
        var tacticIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < tacticValues.Length; index++)
        {
            ArcRelayTactic tactic = ValidateTactic(
                tacticValues[index],
                $"{path}.tactics[{index}]",
                squad,
                width,
                height);
            if (!tacticIds.Add(tactic.Id))
                throw Error($"{path}.tactics[{index}]",
                    $"duplicate tactic id '{tactic.Id}'.");
            tactics.Add(tactic);
        }
        return new ArcRelayTacticsDocument(
            sheetId, mapId, width, height, squad, tactics);
    }

    private static ArcRelayTactic ValidateTactic(
        JsonElement value,
        string path,
        IReadOnlyList<ArcRelayTacticsSquadBot> squad,
        int width,
        int height)
    {
        Object(value, path,
            ["id", "name", "recipe", "start", "delivery", "groups", "phases"],
            ["abort", "maxDuration"]);
        string id = Identifier(value, "id", path);
        if (id.Length > 24)
            throw Error($"{path}.id", "tactic id exceeds 24 characters.");
        string name = NonEmptyString(value, "name", path);
        if (name.Length > 40)
            throw Error($"{path}.name", "tactic name exceeds 40 characters.");
        string recipe = RequiredString(value, "recipe", path);
        if (!TacticRecipes.Contains(recipe))
            throw Error($"{path}.recipe", $"unknown recipe '{recipe}'.");

        JsonElement start = value.GetProperty("start");
        Object(start, $"{path}.start", ["mode"], ["cases"]);
        string startMode = OneOf(start, "mode", $"{path}.start", "always", "when");
        List<ArcRelayTacticCase>? startCases = null;
        if (startMode == "when")
        {
            if (!start.TryGetProperty("cases", out JsonElement startCaseValues))
                throw Error($"{path}.start", "mode 'when' needs cases.");
            startCases = ValidateCases(
                startCaseValues, $"{path}.start.cases", width, height,
                TacticStartRows, maximum: 3);
        }
        else if (start.TryGetProperty("cases", out _))
        {
            throw Error($"{path}.start", "mode 'always' does not take cases.");
        }

        List<ArcRelayTacticCase>? abortCases = null;
        if (value.TryGetProperty("abort", out JsonElement abort))
        {
            Object(abort, $"{path}.abort", ["cases"]);
            abortCases = ValidateCases(
                abort.GetProperty("cases"), $"{path}.abort.cases",
                width, height, TacticAbortRows, maximum: 2);
        }

        string maxDuration = "none";
        if (value.TryGetProperty("maxDuration", out JsonElement duration))
        {
            maxDuration = StringValue(duration, $"{path}.maxDuration");
            if (!TacticDurations.Contains(maxDuration))
                throw Error($"{path}.maxDuration",
                    $"unknown duration '{maxDuration}'.");
        }

        JsonElement[] groupValues = BoundedArray(
            value.GetProperty("groups"), $"{path}.groups", 1, 3);
        var groups = new List<ArcRelayTacticGroup>(groupValues.Length);
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        var assigned = new HashSet<int>();
        for (int index = 0; index < groupValues.Length; index++)
        {
            string at = $"{path}.groups[{index}]";
            JsonElement group = groupValues[index];
            Object(group, at, ["id", "name", "slots", "coreRule"]);
            string groupId = Identifier(group, "id", at);
            if (groupId.Length > 24)
                throw Error($"{at}.id", "group id exceeds 24 characters.");
            if (!groupIds.Add(groupId))
                throw Error(at, $"duplicate group id '{groupId}'.");
            string groupName = NonEmptyString(group, "name", at);
            if (groupName.Length > 40)
                throw Error($"{at}.name", "group name exceeds 40 characters.");
            var groupSlots = new List<int>();
            foreach (JsonElement slotValue in BoundedArray(
                group.GetProperty("slots"), $"{at}.slots", 1, 8))
            {
                if (slotValue.ValueKind != JsonValueKind.Number
                    || !slotValue.TryGetInt32(out int slot)
                    || slot is < 0 or > 7
                    || groupSlots.Contains(slot))
                {
                    throw Error($"{at}.slots",
                        "group slots are unique squad slots 0..7.");
                }
                if (!assigned.Add(slot))
                    throw Error(at, $"slot {slot} belongs to more than one group.");
                groupSlots.Add(slot);
            }
            string coreRule = OneOf(group, "coreRule", at,
                "ignore", "grab", "hunt");
            groups.Add(new ArcRelayTacticGroup(
                groupId, groupName, groupSlots, coreRule));
        }
        if (assigned.Count != 8)
        {
            throw Error($"{path}.groups",
                "every squad slot 0..7 must belong to exactly one group.");
        }

        JsonElement[] phaseValues = BoundedArray(
            value.GetProperty("phases"), $"{path}.phases", 1, 8);
        var phases = new List<ArcRelayTacticPhaseDef>(phaseValues.Length);
        var phaseIds = new List<string>(phaseValues.Length);
        for (int index = 0; index < phaseValues.Length; index++)
        {
            string at = $"{path}.phases[{index}]";
            JsonElement phase = phaseValues[index];
            Object(phase, at, ["id", "name", "jobs"], ["continue"]);
            string phaseId = Identifier(phase, "id", at);
            if (phaseId.Length > 24)
                throw Error($"{at}.id", "phase id exceeds 24 characters.");
            if (phaseIds.Contains(phaseId))
                throw Error(at, $"duplicate phase id '{phaseId}'.");
            string phaseName = NonEmptyString(phase, "name", at);
            if (phaseName.Length > 40)
                throw Error($"{at}.name", "phase name exceeds 40 characters.");
            phaseIds.Add(phaseId);
            phases.Add(new ArcRelayTacticPhaseDef(
                phaseId, phaseName,
                ValidateJobs(
                    phase.GetProperty("jobs"), $"{at}.jobs", squad, groups,
                    width, height),
                []));
        }
        if (!phases[0].Jobs.Values.Any(job => job.HasGeometry))
        {
            throw Error($"{path}.phases[0]",
                "the first phase needs at least one group with drawn "
                + "geometry; later phases may carry it forward.");
        }
        // Transitions validate after every phase id is known: targets are
        // forward-only, and only the final phase may hold forever.
        for (int index = 0; index < phaseValues.Length; index++)
        {
            string at = $"{path}.phases[{index}]";
            bool last = index == phaseValues.Length - 1;
            if (!phaseValues[index].TryGetProperty(
                    "continue", out JsonElement rules))
            {
                if (!last)
                {
                    throw Error(at,
                        "every phase before the last needs a continue rule; "
                        + "only the final phase may hold forever.");
                }
                continue;
            }
            JsonElement[] ruleValues = BoundedArray(
                rules, $"{at}.continue", 1, 3);
            var transitions = new List<ArcRelayTacticTransition>();
            for (int rule = 0; rule < ruleValues.Length; rule++)
            {
                string ruleAt = $"{at}.continue[{rule}]";
                Object(ruleValues[rule], ruleAt,
                    ["to"], ["required", "triggers"]);
                string to = RequiredString(ruleValues[rule], "to", ruleAt);
                if (to == "next")
                {
                    if (last)
                        throw Error($"{ruleAt}.to",
                            "the final phase has no next; use 'finish'.");
                }
                else if (to != "finish")
                {
                    int target = phaseIds.IndexOf(to);
                    if (target < 0)
                        throw Error($"{ruleAt}.to", $"unknown phase '{to}'.");
                    if (target <= index)
                        throw Error($"{ruleAt}.to",
                            "transitions are forward-only; a phase may not "
                            + "jump to itself or backward.");
                }
                ArcRelayTacticCase when = ValidateCase(
                    ruleValues[rule], ruleAt, width, height,
                    TacticTransitionRows);
                ValidateTransitionRowContext(
                    when, ruleAt, phases[index], phases);
                transitions.Add(new ArcRelayTacticTransition(when, to));
            }
            phases[index] = phases[index] with { Continue = transitions };
        }

        JsonElement delivery = value.GetProperty("delivery");
        Object(delivery, $"{path}.delivery", ["plan"], ["preferredSlots"]);
        string plan = OneOf(delivery, "plan", $"{path}.delivery",
            "none", "solo", "escort");
        var preferred = new List<int>();
        if (delivery.TryGetProperty("preferredSlots", out JsonElement slots))
        {
            foreach (JsonElement slotValue in BoundedArray(
                slots, $"{path}.delivery.preferredSlots", 1, 8))
            {
                if (slotValue.ValueKind != JsonValueKind.Number
                    || !slotValue.TryGetInt32(out int slot)
                    || slot is < 0 or > 7
                    || preferred.Contains(slot))
                {
                    throw Error($"{path}.delivery.preferredSlots",
                        "preferred carriers are unique squad slots 0..7.");
                }
                preferred.Add(slot);
            }
        }

        return new ArcRelayTactic(
            id, name, recipe,
            startCases,
            abortCases,
            maxDuration,
            new ArcRelayTacticDelivery(plan, preferred),
            groups,
            phases);
    }

    /// <summary>Rows that reference the phase's own drawn plan must find it
    /// there: 'reached' needs a converge/advance job in the transition's
    /// phase, 'groupsSet' needs geometry to be set at, and 'observed' needs
    /// drawn geometry somewhere in the tactic.</summary>
    private static void ValidateTransitionRowContext(
        ArcRelayTacticCase value,
        string path,
        ArcRelayTacticPhaseDef phase,
        IReadOnlyList<ArcRelayTacticPhaseDef> phases)
    {
        foreach (ArcRelayTacticConditionRows? rows in new[]
                 { value.Required, value.Triggers })
        {
            if (rows is null)
                continue;
            if (rows.Reached && !phase.Jobs.Values.Any(job =>
                    job.Kind is "converge" or "advance"))
            {
                throw Error(path,
                    "'reached' needs a group that converges or advances "
                    + "somewhere in this phase.");
            }
            if (rows.GroupsSet && !phase.Jobs.Values.Any(job =>
                    job.HasGeometry))
            {
                throw Error(path,
                    "'groupsSet' needs at least one group with drawn "
                    + "geometry in this phase.");
            }
            if (rows.Observed && !phases.Any(candidate =>
                    candidate.Jobs.Values.Any(job => job.HasGeometry)))
            {
                throw Error(path,
                    "'observed' needs at least one group with drawn geometry.");
            }
        }
    }

    private static Dictionary<string, ArcRelayTacticJob> ValidateJobs(
        JsonElement value,
        string path,
        IReadOnlyList<ArcRelayTacticsSquadBot> squad,
        IReadOnlyList<ArcRelayTacticGroup> groups,
        int width,
        int height)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected object.");
        var jobs = new Dictionary<string, ArcRelayTacticJob>(
            StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            ArcRelayTacticGroup? group = groups.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, property.Name,
                    StringComparison.Ordinal));
            if (group is null)
                throw Error($"{path}.{property.Name}",
                    $"unknown group '{property.Name}'.");
            if (!jobs.TryAdd(property.Name, ValidateJob(
                    property.Value, $"{path}.{property.Name}", squad, group,
                    width, height)))
                throw Error(path, $"duplicate job for '{property.Name}'.");
        }
        foreach (ArcRelayTacticGroup group in groups)
        {
            if (!jobs.ContainsKey(group.Id))
                throw Error(path, $"group '{group.Id}' has no job this phase.");
        }
        return jobs;
    }

    private static ArcRelayTacticJob ValidateJob(
        JsonElement value,
        string path,
        IReadOnlyList<ArcRelayTacticsSquadBot> squad,
        ArcRelayTacticGroup group,
        int width,
        int height)
    {
        string[] geometryKeys = ["posts", "rect", "path", "point"];
        Object(value, path, ["kind", "posture", "recovery"],
            [.. geometryKeys, "via", "sigs", "travel"]);
        string kind = RequiredString(value, "kind", path);
        if (!TacticJobKinds.Contains(kind))
            throw Error($"{path}.kind", $"unknown job kind '{kind}'.");
        string posture = RequiredString(value, "posture", path);
        if (!TacticPostures.Contains(posture))
            throw Error($"{path}.posture", $"unknown posture '{posture}'.");
        // The optional on-the-way posture: how the group behaves while
        // MOVING to this job's ground; the main posture takes over once set.
        string? travel = null;
        if (value.TryGetProperty("travel", out JsonElement travelValue))
        {
            travel = StringValue(travelValue, $"{path}.travel");
            if (!TacticPostures.Contains(travel))
                throw Error($"{path}.travel", $"unknown posture '{travel}'.");
        }
        string recovery = RequiredString(value, "recovery", path);
        if (!TacticRecoveries.Contains(recovery))
            throw Error($"{path}.recovery", $"unknown recovery '{recovery}'.");

        string? expectedGeometry = kind switch
        {
            "posts" => "posts",
            "area" => "rect",
            "line" or "advance" or "patrol" => "path",
            "approach" or "converge" => "point",
            _ => null,
        };
        foreach (string key in geometryKeys)
        {
            bool present = value.TryGetProperty(key, out _);
            if (present && key != expectedGeometry)
                throw Error($"{path}.{key}",
                    $"kind '{kind}' does not take {key}.");
            if (!present && key == expectedGeometry)
                throw Error(path, $"kind '{kind}' needs {key}.");
        }

        List<ArcRelayTacticPost>? posts = null;
        int[]? rect = null;
        List<int[]>? pathPoints = null;
        int[]? point = null;
        if (expectedGeometry == "posts")
        {
            JsonElement[] postValues = BoundedArray(
                value.GetProperty("posts"), $"{path}.posts", 1, 8);
            if (postValues.Length != group.Slots.Count)
            {
                throw Error($"{path}.posts",
                    $"posts must match the group's {group.Slots.Count} bots, one each.");
            }
            posts = [];
            var tiles = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < postValues.Length; index++)
            {
                string at = $"{path}.posts[{index}]";
                Object(postValues[index], at, ["at", "facing"], ["via"]);
                int[] tile = BoundedPoint(
                    postValues[index].GetProperty("at"), $"{at}.at",
                    width, height);
                if (!tiles.Add($"{tile[0]},{tile[1]}"))
                    throw Error(at, "two posts share the same tile.");
                string facing = OneOf(postValues[index], "facing", at,
                    "n", "e", "s", "w");
                // A post's own drawn way in: the bot walks this route to its
                // post instead of the direct path (overrides the group via).
                List<int[]>? postVia = null;
                if (postValues[index].TryGetProperty(
                        "via", out JsonElement postViaValue))
                {
                    JsonElement[] viaPoints = BoundedArray(
                        postViaValue, $"{at}.via", 2, 16);
                    postVia = [];
                    foreach (JsonElement pointValue in viaPoints)
                    {
                        postVia.Add(BoundedPoint(
                            pointValue, $"{at}.via", width, height));
                    }
                }
                posts.Add(new ArcRelayTacticPost(tile, facing, postVia));
            }
        }
        else if (expectedGeometry == "rect")
        {
            rect = BoundedRect(
                value.GetProperty("rect"), $"{path}.rect", width, height);
        }
        else if (expectedGeometry == "path")
        {
            JsonElement[] pathValues = BoundedArray(
                value.GetProperty("path"), $"{path}.path", 2, 16);
            pathPoints = [];
            foreach (JsonElement pointValue in pathValues)
            {
                pathPoints.Add(BoundedPoint(
                    pointValue, $"{path}.path", width, height));
            }
        }
        else if (expectedGeometry == "point")
        {
            point = BoundedPoint(
                value.GetProperty("point"), $"{path}.point", width, height);
        }

        List<int[]>? via = null;
        if (value.TryGetProperty("via", out JsonElement viaValue))
        {
            // A drawn approach route for reaching the job's ground. Advance
            // and patrol already carry their own route in `path`; a second
            // one would be ambiguous. Escort follows a body and none carries
            // the previous phase's plan, so neither has an approach to draw.
            bool viaLegal = kind
                is "posts" or "area" or "line" or "approach"
                or "converge" or "return";
            if (!viaLegal)
                throw Error($"{path}.via", $"kind '{kind}' does not take via.");
            via = [];
            foreach (JsonElement pointValue in BoundedArray(
                viaValue, $"{path}.via", 2, 16))
            {
                via.Add(BoundedPoint(
                    pointValue, $"{path}.via", width, height));
            }
        }

        var sigs = new List<ArcRelayTacticSignatureMark>();
        if (value.TryGetProperty("sigs", out JsonElement sigValues))
        {
            ValidateSignatureMarks(
                sigValues, $"{path}.sigs", squad, group.Slots, width, height,
                sigs);
        }
        return new ArcRelayTacticJob(
            kind, posture, travel, recovery, posts, rect, pathPoints, point,
            via, sigs);
    }

    private static void ValidateSignatureMarks(
        JsonElement value,
        string path,
        IReadOnlyList<ArcRelayTacticsSquadBot> squad,
        IReadOnlyList<int> groupSlots,
        int width,
        int height,
        List<ArcRelayTacticSignatureMark> marks)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw Error(path, "expected object.");
        foreach (JsonProperty property in value.EnumerateObject())
        {
            string classId = property.Name;
            string at = $"{path}.{classId}";
            if (!groupSlots.Any(slot => string.Equals(
                    squad[slot].ClassId, classId, StringComparison.Ordinal)))
            {
                throw Error(at,
                    $"no bot of class '{classId}' is in this group.");
            }
            if (marks.Any(mark => string.Equals(
                    mark.ClassId, classId, StringComparison.Ordinal)))
                throw Error(at, $"duplicate marks for class '{classId}'.");
            ArcRelaySignatureAuthoringDescriptor descriptor =
                ArcRelaySignatureAuthoringCatalog.ForClass(classId);
            bool anchorMarks = descriptor.Allows(
                ArcRelaySignatureTargetCapability.ExactAnchor);
            bool laneMarks = descriptor.Allows(
                ArcRelaySignatureTargetCapability.RouteForward);
            bool railLine = string.Equals(
                classId, "longshot", StringComparison.Ordinal);
            if (anchorMarks)
            {
                Object(property.Value, at, ["points"]);
                int budget = TacticAnchorMarkBudgets[classId];
                JsonElement[] pointValues = BoundedArray(
                    property.Value.GetProperty("points"),
                    $"{at}.points", 1, budget);
                var points = new List<int[]>();
                foreach (JsonElement pointValue in pointValues)
                {
                    points.Add(BoundedPoint(
                        pointValue, $"{at}.points", width, height));
                }
                marks.Add(new ArcRelayTacticSignatureMark(
                    classId, Points: points, Line: null));
            }
            else if (laneMarks || railLine)
            {
                Object(property.Value, at, ["line"]);
                JsonElement[] lineValues = BoundedArray(
                    property.Value.GetProperty("line"), $"{at}.line",
                    2, railLine ? 2 : 8);
                var line = new List<int[]>();
                foreach (JsonElement pointValue in lineValues)
                {
                    line.Add(BoundedPoint(
                        pointValue, $"{at}.line", width, height));
                }
                if (line[0][0] == line[^1][0] && line[0][1] == line[^1][1])
                    throw Error($"{at}.line",
                        "a signature lane must not loop back to its start.");
                marks.Add(new ArcRelayTacticSignatureMark(
                    classId, Points: null, Line: line));
            }
            else
            {
                throw Error(at,
                    $"'{classId}' has no spatial signature marks; its "
                    + "signature is bundled by posture.");
            }
        }
    }

    private static List<ArcRelayTacticCase> ValidateCases(
        JsonElement value,
        string path,
        int width,
        int height,
        string[] allowedRows,
        int maximum)
    {
        JsonElement[] caseValues = BoundedArray(value, path, 1, maximum);
        var cases = new List<ArcRelayTacticCase>(caseValues.Length);
        for (int index = 0; index < caseValues.Length; index++)
        {
            Object(caseValues[index], $"{path}[{index}]", [],
                ["required", "triggers"]);
            cases.Add(ValidateCase(
                caseValues[index], $"{path}[{index}]", width, height,
                allowedRows));
        }
        return cases;
    }

    /// <summary>One case: all rows in 'required' must hold AND any row in
    /// 'triggers' may fire. At least one row must be active overall. The
    /// carrier of the case (a start/abort case entry or a continue rule)
    /// supplies its own Object() shape check.</summary>
    private static ArcRelayTacticCase ValidateCase(
        JsonElement value,
        string path,
        int width,
        int height,
        string[] allowedRows)
    {
        ArcRelayTacticConditionRows? required = null;
        if (value.TryGetProperty("required", out JsonElement requiredValue))
        {
            required = ValidateConditionRows(
                requiredValue, $"{path}.required", width, height, allowedRows);
        }
        ArcRelayTacticConditionRows? triggers = null;
        if (value.TryGetProperty("triggers", out JsonElement triggerValue))
        {
            triggers = ValidateConditionRows(
                triggerValue, $"{path}.triggers", width, height, allowedRows);
            if (triggers.GroupsSet)
            {
                throw Error($"{path}.triggers",
                    "groupsSet belongs in required — every group must be in "
                    + "position, not any one bot.");
            }
            if (triggers.Healed is not null)
            {
                throw Error($"{path}.triggers",
                    "healed belongs in required — every living bot must be "
                    + "topped up, not any one bot.");
            }
        }
        if ((required?.ActiveCount ?? 0) + (triggers?.ActiveCount ?? 0) == 0)
            throw Error(path, "a case needs at least one active condition row.");
        return new ArcRelayTacticCase(required, triggers);
    }

    private static ArcRelayTacticConditionRows ValidateConditionRows(
        JsonElement value,
        string path,
        int width,
        int height,
        string[] allowedRows)
    {
        Object(value, path, [], allowedRows);

        bool groupsSet = false;
        if (value.TryGetProperty("groupsSet", out JsonElement groupsSetValue))
        {
            if (groupsSetValue.ValueKind != JsonValueKind.True)
                throw Error($"{path}.groupsSet", "groupsSet must be true.");
            groupsSet = true;
        }
        int? heldTicks = null;
        if (value.TryGetProperty("held", out JsonElement heldValue))
        {
            string at = $"{path}.held";
            Object(heldValue, at, ["ticks"]);
            heldTicks = Range(heldValue, "ticks", at, 1, 600);
        }
        int? healed = null;
        if (value.TryGetProperty("healed", out JsonElement healedValue))
        {
            string at = $"{path}.healed";
            Object(healedValue, at, ["percent"]);
            healed = Range(healedValue, "percent", at, 1, 100);
        }
        ArcRelayTacticEnemiesAtRow? enemiesAt = null;
        if (value.TryGetProperty("enemiesAt", out JsonElement enemies))
        {
            string at = $"{path}.enemiesAt";
            Object(enemies, at, ["count", "zone"]);
            int count = Range(enemies, "count", at, 1, 8);
            enemiesAt = new ArcRelayTacticEnemiesAtRow(
                count,
                ValidateZoneRef(enemies.GetProperty("zone"), $"{at}.zone",
                    width, height));
        }
        bool contact = false;
        if (value.TryGetProperty("contact", out JsonElement contactValue))
        {
            if (contactValue.ValueKind != JsonValueKind.True)
                throw Error($"{path}.contact", "contact must be true.");
            contact = true;
        }
        string? coreLooseWell = null;
        if (value.TryGetProperty("coreLoose", out JsonElement coreLoose))
        {
            string at = $"{path}.coreLoose";
            Object(coreLoose, at, ["well"]);
            coreLooseWell = RequiredString(coreLoose, "well", at);
            if (!TacticWells.Contains(coreLooseWell))
                throw Error(at, "well must be north/centre/south/own/enemy.");
        }
        ArcRelayTacticCoreSoonRow? coreSoon = null;
        if (value.TryGetProperty("coreSoon", out JsonElement coreSoonValue))
        {
            string at = $"{path}.coreSoon";
            Object(coreSoonValue, at, ["well", "ticks"]);
            string well = RequiredString(coreSoonValue, "well", at);
            if (!TacticWells.Contains(well))
                throw Error(at, "well must be north/centre/south/own/enemy.");
            coreSoon = new ArcRelayTacticCoreSoonRow(
                well, Range(coreSoonValue, "ticks", at, 1, 120));
        }
        int? enemiesDown = null;
        if (value.TryGetProperty("enemiesDown", out JsonElement enemiesDownValue))
        {
            string at = $"{path}.enemiesDown";
            Object(enemiesDownValue, at, ["count"]);
            enemiesDown = Range(enemiesDownValue, "count", at, 1, 8);
        }
        bool carrier = false;
        if (value.TryGetProperty("carrier", out JsonElement carrierValue))
        {
            if (carrierValue.ValueKind != JsonValueKind.True)
                throw Error($"{path}.carrier", "carrier must be true.");
            carrier = true;
        }
        int? losses = null;
        if (value.TryGetProperty("losses", out JsonElement lossesValue))
        {
            string at = $"{path}.losses";
            Object(lossesValue, at, ["count"]);
            losses = Range(lossesValue, "count", at, 1, 8);
        }
        ArcRelayTacticLossesWithinRow? lossesWithin = null;
        if (value.TryGetProperty("lossesWithin", out JsonElement lossesWithinValue))
        {
            string at = $"{path}.lossesWithin";
            Object(lossesWithinValue, at, ["count", "ticks"]);
            lossesWithin = new ArcRelayTacticLossesWithinRow(
                Range(lossesWithinValue, "count", at, 1, 8),
                Range(lossesWithinValue, "ticks", at, 1, 600));
        }
        int? kills = null;
        if (value.TryGetProperty("kills", out JsonElement killsValue))
        {
            string at = $"{path}.kills";
            Object(killsValue, at, ["count"]);
            kills = Range(killsValue, "count", at, 1, 8);
        }
        ArcRelayTacticZoneRef? cleared = null;
        if (value.TryGetProperty("cleared", out JsonElement clearedValue))
        {
            string at = $"{path}.cleared";
            Object(clearedValue, at, ["zone"]);
            cleared = ValidateZoneRef(
                clearedValue.GetProperty("zone"), $"{at}.zone", width, height);
        }
        bool coreAcquired = FlagRow(value, "coreAcquired", path);
        bool coreBanked = FlagRow(value, "coreBanked", path);
        bool coreLost = FlagRow(value, "coreLost", path);
        bool reached = FlagRow(value, "reached", path);
        bool observed = FlagRow(value, "observed", path);
        return new ArcRelayTacticConditionRows(
            enemiesAt, contact, coreLooseWell, coreSoon, enemiesDown,
            carrier, losses, lossesWithin, groupsSet, heldTicks, healed,
            kills, cleared, coreAcquired, coreBanked, coreLost, reached,
            observed);
    }

    private static bool FlagRow(
        JsonElement rows,
        string name,
        string path)
    {
        if (!rows.TryGetProperty(name, out JsonElement value))
            return false;
        if (value.ValueKind != JsonValueKind.True)
            throw Error($"{path}.{name}", $"{name} must be true.");
        return true;
    }

    private static ArcRelayTacticZoneRef ValidateZoneRef(
        JsonElement value,
        string path,
        int width,
        int height)
    {
        Object(value, path, ["rect"], ["name"]);
        int[] rect = BoundedRect(value.GetProperty("rect"), $"{path}.rect",
            width, height);
        string? name = null;
        if (value.TryGetProperty("name", out JsonElement nameValue))
        {
            name = StringValue(nameValue, $"{path}.name");
            IdentifierValue(name, $"{path}.name", "zone name");
        }
        return new ArcRelayTacticZoneRef(name, rect);
    }

    private static int[] BoundedPoint(
        JsonElement value,
        string path,
        int width,
        int height)
    {
        Point(value, path);
        int[] point = value.EnumerateArray()
            .Select(coordinate => coordinate.GetInt32()).ToArray();
        if (point[0] >= width || point[1] >= height)
            throw Error(path, "point is outside the declared map.");
        return point;
    }

    private static int[] BoundedRect(
        JsonElement value,
        string path,
        int width,
        int height)
    {
        Rectangle(value, path);
        int[] rect = value.EnumerateArray()
            .Select(coordinate => coordinate.GetInt32()).ToArray();
        if (rect[0] > rect[2] || rect[1] > rect[3])
            throw Error(path, "rectangle must be normalized.");
        if (rect[2] >= width || rect[3] >= height)
            throw Error(path, "rectangle is outside the declared map.");
        return rect;
    }
}

/// <summary>The validated player-owned Tactics document.</summary>
internal sealed record ArcRelayTacticsDocument(
    string SheetId,
    string MapId,
    int MapWidth,
    int MapHeight,
    IReadOnlyList<ArcRelayTacticsSquadBot> Squad,
    IReadOnlyList<ArcRelayTactic> Tactics);

internal sealed record ArcRelayTacticsSquadBot(
    int Slot,
    string ClassId,
    string Build,
    string Name);

internal sealed record ArcRelayTactic(
    string Id,
    string Name,
    string Recipe,
    IReadOnlyList<ArcRelayTacticCase>? Start,
    IReadOnlyList<ArcRelayTacticCase>? Abort,
    string MaxDuration,
    ArcRelayTacticDelivery Delivery,
    IReadOnlyList<ArcRelayTacticGroup> Groups,
    IReadOnlyList<ArcRelayTacticPhaseDef> Phases);

/// <summary>One ordered phase of the operation: what every group does and
/// the latched, forward-only continue rules that leave it. An empty Continue
/// on the final phase means the operation holds forever once there.</summary>
internal sealed record ArcRelayTacticPhaseDef(
    string Id,
    string Name,
    IReadOnlyDictionary<string, ArcRelayTacticJob> Jobs,
    IReadOnlyList<ArcRelayTacticTransition> Continue);

/// <summary>To is "next", "finish", or a later phase's id.</summary>
internal sealed record ArcRelayTacticTransition(
    ArcRelayTacticCase When,
    string To);

/// <summary>all(Required rows) AND any(Triggers rows); an absent side is
/// vacuously true, and at least one row is active overall.</summary>
internal sealed record ArcRelayTacticCase(
    ArcRelayTacticConditionRows? Required,
    ArcRelayTacticConditionRows? Triggers);

internal sealed record ArcRelayTacticConditionRows(
    ArcRelayTacticEnemiesAtRow? EnemiesAt,
    bool Contact,
    string? CoreLooseWell,
    ArcRelayTacticCoreSoonRow? CoreSoon,
    int? EnemiesDown,
    bool Carrier,
    int? Losses,
    ArcRelayTacticLossesWithinRow? LossesWithin,
    bool GroupsSet,
    int? HeldTicks,
    int? Healed,
    int? Kills,
    ArcRelayTacticZoneRef? Cleared,
    bool CoreAcquired,
    bool CoreBanked,
    bool CoreLost,
    bool Reached,
    bool Observed)
{
    public int ActiveCount =>
        (EnemiesAt is null ? 0 : 1) + (Contact ? 1 : 0)
        + (CoreLooseWell is null ? 0 : 1) + (CoreSoon is null ? 0 : 1)
        + (EnemiesDown is null ? 0 : 1) + (Carrier ? 1 : 0)
        + (Losses is null ? 0 : 1) + (LossesWithin is null ? 0 : 1)
        + (GroupsSet ? 1 : 0) + (HeldTicks is null ? 0 : 1)
        + (Healed is null ? 0 : 1)
        + (Kills is null ? 0 : 1) + (Cleared is null ? 0 : 1)
        + (CoreAcquired ? 1 : 0) + (CoreBanked ? 1 : 0) + (CoreLost ? 1 : 0)
        + (Reached ? 1 : 0) + (Observed ? 1 : 0);
}

/// <summary>"We lost {count} bots within the last {ticks} ticks" — the
/// remembered-casualty row for aborts and emergency starts.</summary>
internal sealed record ArcRelayTacticLossesWithinRow(int Count, int Ticks);

/// <summary>"A Core births at {well} within {ticks}": the Well's one
/// effective public clock — initial opening or bank-triggered recharge.</summary>
internal sealed record ArcRelayTacticCoreSoonRow(string Well, int Ticks);

internal sealed record ArcRelayTacticEnemiesAtRow(
    int Count,
    ArcRelayTacticZoneRef Zone);

internal sealed record ArcRelayTacticZoneRef(string? Name, int[] Rect);

internal sealed record ArcRelayTacticDelivery(
    string Plan,
    IReadOnlyList<int> PreferredSlots);

/// <summary>Membership only: what a group IS. What it does lives in each
/// phase's job for the group.</summary>
internal sealed record ArcRelayTacticGroup(
    string Id,
    string Name,
    IReadOnlyList<int> Slots,
    string CoreRule);

/// <summary>One group's assignment for one phase: movement geometry,
/// posture, recovery, approach route, and spatial signature marks. Kind
/// 'none' carries the group's nearest earlier geometric job forward under
/// this phase's posture.</summary>
internal sealed record ArcRelayTacticJob(
    string Kind,
    string Posture,
    string? Travel,
    string Recovery,
    IReadOnlyList<ArcRelayTacticPost>? Posts,
    int[]? Rect,
    IReadOnlyList<int[]>? Path,
    int[]? Point,
    IReadOnlyList<int[]>? Via,
    IReadOnlyList<ArcRelayTacticSignatureMark> Sigs)
{
    public bool HasGeometry =>
        Posts is not null || Rect is not null || Path is not null
        || Point is not null;
}

internal sealed record ArcRelayTacticPost(
    int[] At, string Facing, IReadOnlyList<int[]>? Via = null);

internal sealed record ArcRelayTacticSignatureMark(
    string ClassId,
    IReadOnlyList<int[]>? Points,
    IReadOnlyList<int[]>? Line);

/// <summary>The deterministic materializer output pair.</summary>
internal sealed record ArcRelayTacticsMaterialization(
    byte[] OrdersJson,
    byte[] MapPlanJson);
