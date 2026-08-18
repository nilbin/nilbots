using System.Text.Json;

namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Validation for the player-owned Arc Relay Tactics v1 document and the
/// compiler entry that materializes it into the executable Orders v2 pair.
/// Tactics are the only authored source; the materialized pair is regenerated
/// wholesale on every compile and is never hand-edited.
/// </summary>
public static partial class ArcRelayTacticalPlaybookCompiler
{
    public const string TacticsSchema = "arc-relay-tactics-v1";

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
        new(StringComparer.Ordinal) { "fight-on", "patch", "heal-tile" };

    private static readonly HashSet<string> TacticStageKinds =
        new(StringComparer.Ordinal)
        { "posts", "area", "line", "approach", "none" };

    private static readonly HashSet<string> TacticActKinds =
        new(StringComparer.Ordinal)
        { "none", "converge", "advance", "patrol", "return", "escort" };

    private static readonly HashSet<string> TacticWells =
        new(StringComparer.Ordinal)
        { "north", "centre", "south", "own", "enemy" };

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
    /// Compile a Tactics document end to end: validate the player source,
    /// deterministically materialize the Orders v2 + map-plan pair, and run
    /// the existing shared compile over the materialized pair. The returned
    /// compilation carries the tactics hash and both materialized sources.
    /// </summary>
    public static TacticalPlaybookCompilation CompileTactics(
        byte[] tacticsSource,
        string tacticsName = "tactics.json")
    {
        ArgumentNullException.ThrowIfNull(tacticsSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(tacticsName);
        using JsonDocument document = Parse(tacticsSource, tacticsName);
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
            TacticsSha256 = Sha256(tacticsSource),
            MaterializedOrders = materialized.OrdersJson,
            MaterializedMapPlan = materialized.MapPlanJson,
        };
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
        Exact(root, "schema", TacticsSchema, path);
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
            [
                "id", "name", "recipe", "start", "release", "finish",
                "delivery", "groups",
            ]);
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
        Object(start, $"{path}.start", ["mode"], ["conds"]);
        string startMode = OneOf(start, "mode", $"{path}.start", "always", "when");
        ArcRelayTacticConditionRows? startRows = null;
        if (startMode == "when")
        {
            if (!start.TryGetProperty("conds", out JsonElement startConds))
                throw Error($"{path}.start", "mode 'when' needs conds.");
            startRows = ValidateConditionRows(
                startConds, $"{path}.start.conds", width, height,
                allowCarrier: true, allowContact: false);
            if (startRows.ActiveCount == 0)
                throw Error($"{path}.start.conds",
                    "mode 'when' needs at least one active condition row.");
        }
        else if (start.TryGetProperty("conds", out _))
        {
            throw Error($"{path}.start", "mode 'always' does not take conds.");
        }

        JsonElement release = value.GetProperty("release");
        Object(release, $"{path}.release", ["mode"], ["conds"]);
        string releaseMode = OneOf(release, "mode", $"{path}.release",
            "immediate", "ready", "ready-when");
        ArcRelayTacticConditionRows? releaseRows = null;
        if (releaseMode == "ready-when")
        {
            if (!release.TryGetProperty("conds", out JsonElement releaseConds))
                throw Error($"{path}.release", "mode 'ready-when' needs conds.");
            releaseRows = ValidateConditionRows(
                releaseConds, $"{path}.release.conds", width, height,
                allowCarrier: false, allowContact: true);
            if (releaseRows.ActiveCount == 0)
            {
                throw Error($"{path}.release.conds",
                    "mode 'ready-when' needs at least one active condition row.");
            }
        }
        else if (release.TryGetProperty("conds", out _))
        {
            throw Error($"{path}.release",
                $"mode '{releaseMode}' does not take conds.");
        }

        JsonElement[] groupValues = BoundedArray(
            value.GetProperty("groups"), $"{path}.groups", 1, 3);
        var groups = new List<ArcRelayTacticGroup>(groupValues.Length);
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        var assigned = new HashSet<int>();
        for (int index = 0; index < groupValues.Length; index++)
        {
            ArcRelayTacticGroup group = ValidateGroup(
                groupValues[index], $"{path}.groups[{index}]", squad,
                width, height);
            if (!groupIds.Add(group.Id))
                throw Error($"{path}.groups[{index}]",
                    $"duplicate group id '{group.Id}'.");
            foreach (int slot in group.Slots)
            {
                if (!assigned.Add(slot))
                    throw Error($"{path}.groups[{index}]",
                        $"slot {slot} belongs to more than one group.");
            }
            groups.Add(group);
        }
        if (assigned.Count != 8)
        {
            throw Error($"{path}.groups",
                "every squad slot 0..7 must belong to exactly one group.");
        }
        if (groups.All(group =>
                group.Stage.Kind == "none" && group.Act.Kind == "none"))
        {
            throw Error($"{path}.groups",
                "at least one group needs a stage or act plan.");
        }

        ArcRelayTacticOutcomes outcomes = ValidateFinish(
            value.GetProperty("finish"), $"{path}.finish", groups,
            width, height, out string maxAttempt);

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
            startRows,
            releaseMode,
            releaseRows,
            outcomes,
            maxAttempt,
            new ArcRelayTacticDelivery(plan, preferred),
            groups);
    }

    private static ArcRelayTacticGroup ValidateGroup(
        JsonElement value,
        string path,
        IReadOnlyList<ArcRelayTacticsSquadBot> squad,
        int width,
        int height)
    {
        Object(value, path,
            ["id", "name", "slots", "coreRule", "stage", "act"], ["sigs"]);
        string id = Identifier(value, "id", path);
        if (id.Length > 24)
            throw Error($"{path}.id", "group id exceeds 24 characters.");
        string name = NonEmptyString(value, "name", path);
        if (name.Length > 40)
            throw Error($"{path}.name", "group name exceeds 40 characters.");
        var groupSlots = new List<int>();
        foreach (JsonElement slotValue in BoundedArray(
            value.GetProperty("slots"), $"{path}.slots", 1, 8))
        {
            if (slotValue.ValueKind != JsonValueKind.Number
                || !slotValue.TryGetInt32(out int slot)
                || slot is < 0 or > 7
                || groupSlots.Contains(slot))
            {
                throw Error($"{path}.slots",
                    "group slots are unique squad slots 0..7.");
            }
            groupSlots.Add(slot);
        }
        string coreRule = OneOf(value, "coreRule", path,
            "ignore", "grab", "hunt");

        ArcRelayTacticPhase stage = ValidatePhase(
            value.GetProperty("stage"), $"{path}.stage", isStage: true,
            groupSlots.Count, width, height);
        ArcRelayTacticPhase act = ValidatePhase(
            value.GetProperty("act"), $"{path}.act", isStage: false,
            groupSlots.Count, width, height);

        var stageSigs = new List<ArcRelayTacticSignatureMark>();
        var actSigs = new List<ArcRelayTacticSignatureMark>();
        if (value.TryGetProperty("sigs", out JsonElement sigs))
        {
            Object(sigs, $"{path}.sigs", [], ["stage", "act"]);
            if (sigs.TryGetProperty("stage", out JsonElement stageMarks))
                ValidateSignatureMarks(
                    stageMarks, $"{path}.sigs.stage", squad, groupSlots,
                    width, height, stageSigs);
            if (sigs.TryGetProperty("act", out JsonElement actMarks))
                ValidateSignatureMarks(
                    actMarks, $"{path}.sigs.act", squad, groupSlots,
                    width, height, actSigs);
        }

        return new ArcRelayTacticGroup(
            id, name, groupSlots, coreRule, stage, act, stageSigs, actSigs);
    }

    private static ArcRelayTacticPhase ValidatePhase(
        JsonElement value,
        string path,
        bool isStage,
        int groupSize,
        int width,
        int height)
    {
        string[] geometryKeys = ["posts", "rect", "path", "point"];
        Object(value, path, ["kind", "posture", "recovery"],
            [.. geometryKeys, "via"]);
        string kind = RequiredString(value, "kind", path);
        if (!(isStage ? TacticStageKinds : TacticActKinds).Contains(kind))
        {
            throw Error($"{path}.kind",
                $"unknown {(isStage ? "stage" : "act")} kind '{kind}'.");
        }
        string posture = RequiredString(value, "posture", path);
        if (!TacticPostures.Contains(posture))
            throw Error($"{path}.posture", $"unknown posture '{posture}'.");
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
            if (postValues.Length != groupSize)
            {
                throw Error($"{path}.posts",
                    $"posts must match the group's {groupSize} bots, one each.");
            }
            posts = [];
            var tiles = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < postValues.Length; index++)
            {
                string at = $"{path}.posts[{index}]";
                Object(postValues[index], at, ["at", "facing"]);
                int[] tile = BoundedPoint(
                    postValues[index].GetProperty("at"), $"{at}.at",
                    width, height);
                if (!tiles.Add($"{tile[0]},{tile[1]}"))
                    throw Error(at, "two posts share the same tile.");
                string facing = OneOf(postValues[index], "facing", at,
                    "n", "e", "s", "w");
                posts.Add(new ArcRelayTacticPost(tile, facing));
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
            // A drawn approach route for reaching the phase's ground. Advance
            // and patrol already carry their own route in `path`; a second
            // one would be ambiguous. Escort follows a body and none goes
            // nowhere, so neither has an approach to draw.
            bool viaLegal = isStage
                ? kind is "posts" or "area" or "line" or "approach"
                : kind is "converge" or "return";
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
        return new ArcRelayTacticPhase(
            kind, posture, recovery, posts, rect, pathPoints, point, via);
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

    private static ArcRelayTacticConditionRows ValidateConditionRows(
        JsonElement value,
        string path,
        int width,
        int height,
        bool allowCarrier,
        bool allowContact)
    {
        var optional = new List<string> { "enemiesAt", "coreLoose", "losses" };
        if (allowCarrier)
            optional.Add("carrier");
        if (allowContact)
            optional.Add("contact");
        Object(value, path, [], optional);

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
            losses = Range(lossesValue, "count", at, 1, 7);
        }
        return new ArcRelayTacticConditionRows(
            enemiesAt, contact, coreLooseWell, carrier, losses);
    }

    private static ArcRelayTacticOutcomes ValidateFinish(
        JsonElement value,
        string path,
        IReadOnlyList<ArcRelayTacticGroup> groups,
        int width,
        int height,
        out string maxAttempt)
    {
        Object(value, path, ["outcomes", "maxAttempt"]);
        maxAttempt = OneOf(value, "maxAttempt", path,
            "none", "brief", "standard", "long");
        JsonElement outcomes = value.GetProperty("outcomes");
        Object(outcomes, $"{path}.outcomes", [],
            [
                "kills", "losses", "cleared", "coreAcquired", "coreBanked",
                "coreLost", "reached", "observed", "persistent",
            ]);

        int? kills = null;
        if (outcomes.TryGetProperty("kills", out JsonElement killsValue))
        {
            Object(killsValue, $"{path}.outcomes.kills", ["count"]);
            kills = Range(killsValue, "count", $"{path}.outcomes.kills", 1, 8);
        }
        int? losses = null;
        if (outcomes.TryGetProperty("losses", out JsonElement lossesValue))
        {
            Object(lossesValue, $"{path}.outcomes.losses", ["count"]);
            losses = Range(
                lossesValue, "count", $"{path}.outcomes.losses", 1, 8);
        }
        ArcRelayTacticZoneRef? cleared = null;
        if (outcomes.TryGetProperty("cleared", out JsonElement clearedValue))
        {
            Object(clearedValue, $"{path}.outcomes.cleared", ["zone"]);
            cleared = ValidateZoneRef(
                clearedValue.GetProperty("zone"),
                $"{path}.outcomes.cleared.zone", width, height);
        }
        bool coreAcquired = FlagOutcome(outcomes, "coreAcquired", path);
        bool coreBanked = FlagOutcome(outcomes, "coreBanked", path);
        bool coreLost = FlagOutcome(outcomes, "coreLost", path);
        bool reached = FlagOutcome(outcomes, "reached", path);
        bool observed = FlagOutcome(outcomes, "observed", path);
        bool persistent = FlagOutcome(outcomes, "persistent", path);

        if (reached && !groups.Any(group =>
                group.Act.Kind is "converge" or "advance"))
        {
            throw Error($"{path}.outcomes.reached",
                "'reached' needs a group that converges or advances somewhere.");
        }
        if (observed && !groups.Any(group => group.HasAnyGeometry))
        {
            throw Error($"{path}.outcomes.observed",
                "'observed' needs at least one group with drawn geometry.");
        }
        bool anyOutcome = kills.HasValue || losses.HasValue || cleared is not null
            || coreAcquired || coreBanked || coreLost || reached || observed
            || persistent;
        if (!anyOutcome && maxAttempt == "none")
        {
            throw Error($"{path}.outcomes",
                "pick at least one outcome (or a maximum attempt) so the "
                + "tactic can finish.");
        }
        return new ArcRelayTacticOutcomes(
            kills, losses, cleared, coreAcquired, coreBanked, coreLost,
            reached, observed, persistent);
    }

    private static bool FlagOutcome(
        JsonElement outcomes,
        string name,
        string path)
    {
        if (!outcomes.TryGetProperty(name, out JsonElement value))
            return false;
        if (value.ValueKind != JsonValueKind.True)
            throw Error($"{path}.outcomes.{name}", $"{name} must be true.");
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
    ArcRelayTacticConditionRows? Start,
    string ReleaseMode,
    ArcRelayTacticConditionRows? Release,
    ArcRelayTacticOutcomes Outcomes,
    string MaxAttempt,
    ArcRelayTacticDelivery Delivery,
    IReadOnlyList<ArcRelayTacticGroup> Groups);

internal sealed record ArcRelayTacticConditionRows(
    ArcRelayTacticEnemiesAtRow? EnemiesAt,
    bool Contact,
    string? CoreLooseWell,
    bool Carrier,
    int? Losses)
{
    public int ActiveCount =>
        (EnemiesAt is null ? 0 : 1) + (Contact ? 1 : 0)
        + (CoreLooseWell is null ? 0 : 1) + (Carrier ? 1 : 0)
        + (Losses is null ? 0 : 1);
}

internal sealed record ArcRelayTacticEnemiesAtRow(
    int Count,
    ArcRelayTacticZoneRef Zone);

internal sealed record ArcRelayTacticZoneRef(string? Name, int[] Rect);

internal sealed record ArcRelayTacticOutcomes(
    int? Kills,
    int? Losses,
    ArcRelayTacticZoneRef? Cleared,
    bool CoreAcquired,
    bool CoreBanked,
    bool CoreLost,
    bool Reached,
    bool Observed,
    bool Persistent);

internal sealed record ArcRelayTacticDelivery(
    string Plan,
    IReadOnlyList<int> PreferredSlots);

internal sealed record ArcRelayTacticGroup(
    string Id,
    string Name,
    IReadOnlyList<int> Slots,
    string CoreRule,
    ArcRelayTacticPhase Stage,
    ArcRelayTacticPhase Act,
    IReadOnlyList<ArcRelayTacticSignatureMark> StageSigs,
    IReadOnlyList<ArcRelayTacticSignatureMark> ActSigs)
{
    public bool HasAnyGeometry =>
        Stage.HasGeometry || Act.HasGeometry;
}

internal sealed record ArcRelayTacticPhase(
    string Kind,
    string Posture,
    string Recovery,
    IReadOnlyList<ArcRelayTacticPost>? Posts,
    int[]? Rect,
    IReadOnlyList<int[]>? Path,
    int[]? Point,
    IReadOnlyList<int[]>? Via)
{
    public bool HasGeometry =>
        Posts is not null || Rect is not null || Path is not null
        || Point is not null;
}

internal sealed record ArcRelayTacticPost(int[] At, string Facing);

internal sealed record ArcRelayTacticSignatureMark(
    string ClassId,
    IReadOnlyList<int[]>? Points,
    IReadOnlyList<int[]>? Line);

/// <summary>The deterministic materializer output pair.</summary>
internal sealed record ArcRelayTacticsMaterialization(
    byte[] OrdersJson,
    byte[] MapPlanJson);
