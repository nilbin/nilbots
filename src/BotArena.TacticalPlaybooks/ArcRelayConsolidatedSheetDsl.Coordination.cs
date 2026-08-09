using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private sealed record ConsolidatedCohort(
        string Id,
        int Take,
        string Select);

    private sealed record ConsolidatedFocus(
        string Id,
        int MaximumAttackers);

    private sealed record ConsolidatedCohortConsumer(
        string Role,
        DoctrineTarget Destination,
        string Path);

    private static Dictionary<string, ConsolidatedCohort> ParseCohorts(
        JsonObject source,
        string path)
    {
        var result = new Dictionary<string, ConsolidatedCohort>(
            StringComparer.Ordinal);
        foreach ((string id, JsonNode? node) in source)
        {
            IdentifierValue(id, path, "cohort");
            JsonObject value = node as JsonObject
                ?? throw Error($"{path}.{id}", "cohort must be an object.");
            ConsolidatedKeys(value, $"{path}.{id}", ["take", "select"]);
            int take = RequiredIntNode(value, "take", $"{path}.{id}", 1, 8);
            string select = value["select"]?.GetValue<string>() ?? "";
            if (select != "nearest")
                throw Error($"{path}.{id}", "select must be 'nearest'.");
            result.Add(id, new(id, take, select));
        }
        return result;
    }

    private static Dictionary<string, ConsolidatedFocus> ParseFocus(
        JsonObject source,
        string path)
    {
        var result = new Dictionary<string, ConsolidatedFocus>(
            StringComparer.Ordinal);
        foreach ((string id, JsonNode? node) in source)
        {
            IdentifierValue(id, path, "focus scope");
            JsonObject value = node as JsonObject
                ?? throw Error($"{path}.{id}", "focus must be an object.");
            ConsolidatedKeys(value, $"{path}.{id}", ["maximumAttackers"]);
            result.Add(id, new(
                id,
                RequiredIntNode(
                    value, "maximumAttackers", $"{path}.{id}", 1, 8)));
        }
        return result;
    }

    private static void ValidateModeCoordination(
        string role,
        int index,
        JsonObject mode,
        string verb,
        bool isFloor,
        IReadOnlyDictionary<string, ConsolidatedCohort> cohorts,
        IReadOnlyDictionary<string, ConsolidatedFocus> focus,
        IReadOnlyDictionary<string, List<ConsolidatedCohortConsumer>>
            cohortConsumers,
        IReadOnlyDictionary<string, List<string>> focusConsumers,
        string path)
    {
        if (verb != "fetch")
        {
            JsonNode focusNode = mode["focus"]
                ?? throw Error(path,
                    $"{verb} needs focus: a shared name or an inline "
                    + "{ maximumAttackers } object.");
            if (focusNode is JsonValue scalar
                && scalar.TryGetValue<string>(out string? focusId))
            {
                IdentifierValue(focusId!, $"{path}.focus", "focus scope");
                if (!focus.ContainsKey(focusId!))
                    throw Error(path, $"unknown focus scope '{focusId}'.");
                focusConsumers[focusId!].Add($"{role}.modes[{index}]");
            }
            else if (focusNode is JsonObject inline)
            {
                ConsolidatedKeys(
                    inline, $"{path}.focus", ["maximumAttackers"]);
                _ = RequiredIntNode(
                    inline, "maximumAttackers", $"{path}.focus", 1, 8);
            }
            else
            {
                throw Error(path,
                    "focus must be a shared name or "
                    + "{ maximumAttackers }.");
            }
        }
        else if (mode.ContainsKey("focus"))
        {
            throw Error(path,
                "fetch gets its fight behavior from custody; focus here "
                + "would have no consumer.");
        }

        if (mode["cohort"] is JsonNode cohortNode)
        {
            if (isFloor)
                throw Error(path,
                    "a floor cannot be cohort-capped: a rejected body needs "
                    + "a lower rung to fall through to.");
            if (verb is not ("patrol" or "assault"))
                throw Error(path,
                    "cohort is supported only on a patrol or assault with "
                    + "one authored spatial destination.");
            if (cohortNode is JsonValue scalar
                && scalar.TryGetValue<string>(out string? cohortId))
            {
                IdentifierValue(cohortId!, $"{path}.cohort", "cohort");
                if (!cohorts.ContainsKey(cohortId!))
                    throw Error(path, $"unknown cohort '{cohortId}'.");
                if (cohortConsumers[cohortId!].Any(consumer =>
                        string.Equals(
                            consumer.Role, role, StringComparison.Ordinal)))
                {
                    throw Error(path,
                        $"role '{role}' references cohort '{cohortId}' "
                        + "twice; membership must name one predictable rung.");
                }
                cohortConsumers[cohortId!].Add(new(
                    role,
                    DoctrineDestination(mode[verb]!, $"{path}.{verb}"),
                    path));
            }
            else if (cohortNode is JsonObject inline)
            {
                ConsolidatedKeys(
                    inline, $"{path}.cohort", ["take", "select"]);
                _ = RequiredIntNode(
                    inline, "take", $"{path}.cohort", 1, 8);
                if (inline["select"]?.GetValue<string>() != "nearest")
                {
                    throw Error(
                        $"{path}.cohort", "select must be 'nearest'.");
                }
            }
            else
            {
                throw Error(path,
                    "cohort must be a shared name or "
                    + "{ take, select } for one rung.");
            }
            if (mode["posture"] is null)
                throw Error(path,
                    "a cohort rung needs its compact front/core/rear posture.");
        }

        if (mode["posture"] is not JsonNode postureNode)
            return;
        string posture = postureNode.GetValue<string>();
        string[] legal = verb == "follow"
            ? ["trail", "screen"]
            : ["front", "core", "rear"];
        if (!legal.Contains(posture, StringComparer.Ordinal))
        {
            throw Error(path,
                $"posture for {verb} must be one of: "
                + string.Join(", ", legal) + ".");
        }
        if (verb is not ("follow" or "patrol" or "assault"))
            throw Error(path, $"{verb} does not use posture.");
    }

    private static void ValidateCoordinationConsumers(
        IReadOnlyDictionary<string, ConsolidatedCohort> cohorts,
        IReadOnlyDictionary<string, ConsolidatedFocus> focus,
        IReadOnlyDictionary<string, List<ConsolidatedCohortConsumer>>
            cohortConsumers,
        IReadOnlyDictionary<string, List<string>> focusConsumers,
        string path)
    {
        foreach (ConsolidatedCohort cohort in cohorts.Values)
        {
            List<ConsolidatedCohortConsumer> consumers =
                cohortConsumers[cohort.Id];
            if (consumers.Count < 2)
                throw Error($"{path}.doctrines.cohorts.{cohort.Id}",
                    "a cohort needs at least two role rungs; inline a "
                    + "single-body choice in that role's ladder.");
            DoctrineTarget destination = consumers[0].Destination;
            ConsolidatedCohortConsumer? mismatch = consumers.FirstOrDefault(
                consumer => consumer.Destination != destination);
            if (mismatch is not null)
            {
                throw Error(mismatch.Path,
                    $"cohort '{cohort.Id}' has more than one destination; "
                    + "'nearest' must name one route or zone exactly.");
            }
        }
        foreach (ConsolidatedFocus policy in focus.Values)
        {
            if (focusConsumers[policy.Id].Count < 2)
            {
                throw Error($"{path}.doctrines.focus.{policy.Id}",
                    "a shared focus scope needs at least two rung consumers; "
                    + "use inline { maximumAttackers } for one rung.");
            }
        }
    }

    private static (string Scope, int MaximumAttackers) ResolveFocus(
        JsonNode value,
        IReadOnlyDictionary<string, ConsolidatedFocus> focus,
        string consumerId,
        string path)
    {
        if (value is JsonValue scalar
            && scalar.TryGetValue<string>(out string? id))
        {
            if (!focus.TryGetValue(id!, out ConsolidatedFocus? policy))
                throw Error(path, $"unknown focus scope '{id}'.");
            return ($"focus:{policy.Id}", policy.MaximumAttackers);
        }
        JsonObject inline = value as JsonObject
            ?? throw Error(path,
                "focus must be a shared name or { maximumAttackers }.");
        return ($"focus:inline:{consumerId}", RequiredIntNode(
            inline, "maximumAttackers", $"{path}.focus", 1, 8));
    }

    private static JsonObject CohortDistance(
        System.Text.Json.JsonElement mode,
        string verb,
        System.Text.Json.JsonElement cohortReference,
        string consumerId,
        IReadOnlyDictionary<string, ConsolidatedCohort> cohorts,
        string path,
        out JsonObject runtime)
    {
        ConsolidatedCohort cohort;
        if (cohortReference.ValueKind ==
            System.Text.Json.JsonValueKind.String)
        {
            string cohortId = cohortReference.GetString()!;
            if (!cohorts.TryGetValue(cohortId, out cohort!))
                throw Error(path, $"unknown cohort '{cohortId}'.");
        }
        else
        {
            JsonObject inline = JsonNode.Parse(
                cohortReference.GetRawText())!.AsObject();
            ConsolidatedKeys(
                inline, $"{path}.cohort", ["take", "select"]);
            string select = inline["select"]?.GetValue<string>() ?? "";
            if (select != "nearest")
                throw Error($"{path}.cohort", "select must be 'nearest'.");
            cohort = new(
                $"inline-{consumerId}",
                RequiredIntNode(
                    inline, "take", $"{path}.cohort", 1, 8),
                select);
        }
        DoctrineTarget destination = DoctrineTargetFromElement(
            mode.GetProperty(verb), $"{path}.{verb}");
        runtime = new JsonObject
        {
            ["cohortId"] = cohort.Id,
            ["take"] = cohort.Take,
            ["select"] = cohort.Select,
            ["destinationKind"] = destination.Kind,
            ["destinationTarget"] = destination.Target,
        };
        return new JsonObject
        {
            ["kind"] = destination.Kind,
            ["target"] = destination.Target,
        };
    }

}
