using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Cli;

/// <summary>
/// Emits the deterministic runtime package plus editor/debug artifacts for a
/// tactical playbook. The explain view retains the authored role ladders;
/// it never expands them into a second execution vocabulary.
/// </summary>
public static class ArcRelayTacticalPlaybookCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(options, "playbook", "out");
        string playbook = options.GetValueOrDefault("playbook", "");
        if (string.IsNullOrWhiteSpace(playbook))
            throw new InvalidOperationException(
                "arc-relay-playbook requires --playbook <json>.");

        TacticalPlaybookCompilation compilation =
            ArcRelayTacticalPlaybookCompiler.Compile(playbook);
        string output = Path.GetFullPath(options.GetValueOrDefault(
            "out", Path.Combine("out", "arc-relay-playbook")));
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, "playbook.atp"),
            compilation.LinkedData);
        File.WriteAllBytes(Path.Combine(output, "normalized-playbook.json"),
            Pretty(compilation.NormalizedPlaybook));
        File.WriteAllBytes(Path.Combine(output, "normalized-layout.json"),
            Pretty(compilation.NormalizedLayout));
        File.WriteAllBytes(Path.Combine(output, "explain.json"),
            Explain(compilation));

        Console.WriteLine($"Playbook: {compilation.PlaybookSha256}");
        Console.WriteLine($"Layout:   {compilation.LayoutSha256}");
        Console.WriteLine($"Package:  {Sha256(compilation.LinkedData)} "
            + $"({compilation.LinkedData.Length} bytes)");
        Console.WriteLine($"Output:   {output}");
        return 0;
    }

    internal static byte[] Explain(TacticalPlaybookCompilation compilation)
    {
        JsonObject playbook = JsonNode.Parse(compilation.NormalizedPlaybook)!
            .AsObject();
        JsonObject layout = JsonNode.Parse(compilation.NormalizedLayout)!
            .AsObject();
        var ladders = new JsonArray();
        JsonObject roleDoctrines = playbook["doctrines"]!["roles"]!
            .AsObject();
        foreach ((string roleId, JsonNode? doctrineNode) in roleDoctrines)
        {
            JsonObject doctrine = doctrineNode!.AsObject();
            var rungs = new JsonArray();
            int priority = 0;
            foreach (JsonObject mode in doctrine["modes"]!.AsArray()
                         .Select(node => node!.AsObject()))
            {
                string verb = new[]
                    {
                        "patrol", "intercept", "assault", "recover",
                        "fetch", "follow",
                    }
                    .Single(mode.ContainsKey);
                JsonObject rung = mode.DeepClone().AsObject();
                rung.Insert(0, "rungId",
                    $"rung-{roleId}-{priority + 1}-{verb}");
                rung.Insert(1, "priority", priority);
                rungs.Add(rung);
                priority++;
            }
            ladders.Add(new JsonObject
            {
                ["roleId"] = roleId,
                ["fight"] = doctrine["fight"]?.DeepClone(),
                ["repair"] = doctrine["repair"]?.DeepClone(),
                ["custody"] = doctrine["custody"]?.DeepClone(),
                ["scavenge"] = doctrine["scavenge"]?.DeepClone(),
                ["modes"] = rungs,
            });
        }

        var explain = new JsonObject
        {
            ["schema"] = "arc-relay-tactical-playbook-explain-v1",
            ["playbook"] = new JsonObject
            {
                ["id"] = playbook["playbookId"]!.DeepClone(),
                ["sourcePath"] = compilation.PlaybookPath,
                ["sourceSha256"] = compilation.PlaybookSha256,
                ["normalizedBytes"] = compilation.NormalizedPlaybook.Length,
                ["execution"] = "direct-role-ladders",
            },
            ["layout"] = new JsonObject
            {
                ["id"] = layout["layoutId"]!.DeepClone(),
                ["sourcePath"] = compilation.LayoutPath,
                ["sourceSha256"] = compilation.LayoutSha256,
                ["mapId"] = layout["mapId"]!.DeepClone(),
                ["bindings"] = layout["bindings"]!.DeepClone(),
                ["zones"] = layout["zones"]!.DeepClone(),
                ["routes"] = layout["routes"]!.DeepClone(),
                ["anchors"] = layout["anchors"]!.DeepClone(),
            },
            ["runtimePackage"] = new JsonObject
            {
                ["sha256"] = Sha256(compilation.LinkedData),
                ["bytes"] = compilation.LinkedData.Length,
                ["envelope"] = "ATL1",
            },
            ["composition"] = playbook["composition"]!.DeepClone(),
            ["roles"] = playbook["roles"]!.DeepClone(),
            ["custodyPolicies"] =
                playbook["custodyPolicies"]!.DeepClone(),
            ["cohorts"] =
                playbook["doctrines"]!["cohorts"]!.DeepClone(),
            ["focus"] = playbook["doctrines"]!["focus"]!.DeepClone(),
            ["conditions"] = playbook["conditions"]!.DeepClone(),
            ["ladders"] = ladders,
            ["executorPrecedence"] = new JsonArray(
                "custody-emergency", "self-preservation", "repair",
                "signature", "focus-fire", "movement", "facing", "hold"),
            ["commandProvenance"] =
                "tp:steady:<role>:rung-<role>-<position>-<verb>:<channel>",
        };
        return JsonSerializer.SerializeToUtf8Bytes(explain,
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static byte[] Pretty(byte[] canonical)
    {
        JsonNode value = JsonNode.Parse(canonical)
            ?? throw new InvalidDataException("Canonical JSON was empty.");
        return JsonSerializer.SerializeToUtf8Bytes(value,
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Sha256(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}
