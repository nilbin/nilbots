using System.Text.Json.Nodes;

namespace BotArena.TacticalPlaybooks;

public static partial class ArcRelayTacticalPlaybookCompiler
{
    private sealed record ConsolidatedTempoStep(
        string Id,
        int ForTicks,
        int MinimumTicks);

    private sealed record ConsolidatedTempoInterrupt(
        string Id,
        string While,
        string Until,
        int MinimumTicks,
        int StableTicks);

    private sealed record ConsolidatedTempo(
        ConsolidatedTempoStep[] Cycle,
        ConsolidatedTempoInterrupt[] Interrupts)
    {
        public string[] ModeIds =>
        [
            .. Cycle.Select(step => step.Id),
            .. Interrupts.Select(interrupt => interrupt.Id),
        ];
    }

    private static ConsolidatedTempo ParseTempo(
        JsonObject source,
        string path)
    {
        ConsolidatedKeys(source, path, ["cycle"], ["interrupts"]);
        JsonArray cycleSource = RequiredArray(source, "cycle", path);
        if (cycleSource.Count is < 1 or > 8)
            throw Error($"{path}.cycle", "expected between 1 and 8 steps.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var cycle = new List<ConsolidatedTempoStep>();
        for (int index = 0; index < cycleSource.Count; index++)
        {
            JsonObject step = cycleSource[index]?.AsObject()
                ?? throw Error($"{path}.cycle[{index}]", "step must be an object.");
            string at = $"{path}.cycle[{index}]";
            ConsolidatedKeys(step, at, ["modeId", "forTicks"], ["minimumTicks"]);
            string id = step["modeId"]!.GetValue<string>();
            IdentifierValue(id, at, "tempo mode");
            if (!ids.Add(id))
                throw Error(at, $"duplicate tempo mode '{id}'.");
            cycle.Add(new ConsolidatedTempoStep(
                id,
                RequiredIntNode(step, "forTicks", at, 1, 2400),
                step["minimumTicks"] is null
                    ? 4
                    : RequiredIntNode(step, "minimumTicks", at, 0, 1200)));
        }

        JsonArray interruptSource = source["interrupts"] as JsonArray
            ?? new JsonArray();
        if (interruptSource.Count > 8)
            throw Error($"{path}.interrupts", "expected at most 8 interrupts.");
        var interrupts = new List<ConsolidatedTempoInterrupt>();
        for (int index = 0; index < interruptSource.Count; index++)
        {
            JsonObject interrupt = interruptSource[index]?.AsObject()
                ?? throw Error($"{path}.interrupts[{index}]",
                    "interrupt must be an object.");
            string at = $"{path}.interrupts[{index}]";
            ConsolidatedKeys(interrupt, at,
                ["modeId", "while", "until"],
                ["minimumTicks", "stableTicks"]);
            string id = interrupt["modeId"]!.GetValue<string>();
            IdentifierValue(id, at, "tempo mode");
            if (!ids.Add(id))
                throw Error(at, $"duplicate tempo mode '{id}'.");
            interrupts.Add(new ConsolidatedTempoInterrupt(
                id,
                interrupt["while"]!.GetValue<string>(),
                interrupt["until"]!.GetValue<string>(),
                interrupt["minimumTicks"] is null
                    ? 8
                    : RequiredIntNode(
                        interrupt, "minimumTicks", at, 0, 1200),
                interrupt["stableTicks"] is null
                    ? 2
                    : RequiredIntNode(
                        interrupt, "stableTicks", at, 1, 120)));
        }
        return new ConsolidatedTempo([.. cycle], [.. interrupts]);
    }

    private static void AddTempoPredicates(
        JsonObject predicates,
        ConsolidatedTempo tempo)
    {
        foreach (ConsolidatedTempoStep step in tempo.Cycle)
        {
            predicates[$"tempo-{step.Id}-complete"] = new JsonObject
            {
                ["fact"] = "phase-state-ticks",
                ["operator"] = "at-least",
                ["value"] = step.ForTicks,
            };
        }
    }

    private static JsonArray BuildTempoPhases(
        ConsolidatedTempo tempo,
        IReadOnlyCollection<string> standingOrderIds)
    {
        var phases = new JsonArray();
        for (int index = 0; index < tempo.Cycle.Length; index++)
        {
            ConsolidatedTempoStep step = tempo.Cycle[index];
            var transitions = new JsonArray();
            for (int interruptIndex = 0;
                 interruptIndex < tempo.Interrupts.Length;
                 interruptIndex++)
            {
                ConsolidatedTempoInterrupt interrupt =
                    tempo.Interrupts[interruptIndex];
                transitions.Add(new JsonObject
                {
                    ["priority"] = 10 + interruptIndex,
                    ["to"] = interrupt.Id,
                    ["cause"] = "success",
                    ["minimumPolicy"] = "respect",
                    ["stableTicks"] = interrupt.StableTicks,
                    ["when"] = interrupt.While,
                });
            }
            transitions.Add(new JsonObject
            {
                ["priority"] = 20,
                ["to"] = tempo.Cycle[(index + 1) % tempo.Cycle.Length].Id,
                ["cause"] = "success",
                ["minimumPolicy"] = "respect",
                ["stableTicks"] = 2,
                ["when"] = $"tempo-{step.Id}-complete",
            });
            phases.Add(new JsonObject
            {
                ["phaseId"] = step.Id,
                ["minimumTicks"] = step.MinimumTicks,
                ["maneuverId"] = "",
                ["standingOrderIds"] = new JsonArray(standingOrderIds
                    .Select(id => (JsonNode)id).ToArray()),
                ["transitions"] = transitions,
            });
        }
        foreach (ConsolidatedTempoInterrupt interrupt in tempo.Interrupts)
        {
            phases.Add(new JsonObject
            {
                ["phaseId"] = interrupt.Id,
                ["minimumTicks"] = interrupt.MinimumTicks,
                ["maneuverId"] = "",
                ["standingOrderIds"] = new JsonArray(standingOrderIds
                    .Select(id => (JsonNode)id).ToArray()),
                ["transitions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["priority"] = 10,
                        ["to"] = tempo.Cycle[0].Id,
                        ["cause"] = "failure",
                        ["minimumPolicy"] = "respect",
                        ["stableTicks"] = interrupt.StableTicks,
                        ["when"] = interrupt.Until,
                    },
                },
            });
        }
        return phases;
    }
}
