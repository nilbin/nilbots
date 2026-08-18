using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BotArena.Cli;

/// <summary>
/// Emits the deterministic runtime package plus editor/debug artifacts for a
/// orders sheet. The explain view retains the authored tree exactly; it never
/// expands it into a second execution vocabulary.
/// </summary>
public static class ArcRelayTacticalPlaybookCommand
{
    public static int Run(IReadOnlyList<string> args)
    {
        Dictionary<string, string> options = CliSupport.ParseOptions(args);
        CliSupport.RejectUnknownOptions(options, "playbook", "tactics", "out");
        string playbook = options.GetValueOrDefault("playbook", "");
        string tactics = options.GetValueOrDefault("tactics", "");
        if (string.IsNullOrWhiteSpace(playbook)
            == string.IsNullOrWhiteSpace(tactics))
        {
            throw new InvalidOperationException(
                "arc-relay-playbook requires exactly one of --playbook <json> "
                + "or --tactics <json>.");
        }

        TacticalPlaybookCompilation compilation =
            string.IsNullOrWhiteSpace(tactics)
                ? ArcRelayTacticalPlaybookCompiler.Compile(playbook)
                : ArcRelayTacticalPlaybookCompiler.CompileTactics(
                    File.ReadAllBytes(Path.GetFullPath(tactics)),
                    Path.GetFullPath(tactics));
        string output = Path.GetFullPath(options.GetValueOrDefault(
            "out", Path.Combine("out", "arc-relay-playbook")));
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, "playbook.atp"),
            compilation.LinkedData);
        File.WriteAllBytes(Path.Combine(output, "normalized-playbook.json"),
            Pretty(compilation.NormalizedPlaybook));
        File.WriteAllBytes(Path.Combine(output, "normalized-map-plan.json"),
            Pretty(compilation.NormalizedLayout));
        if (compilation.MaterializedOrders is not null)
        {
            // The pair is written under the exact names the materialized
            // orders reference, so `nilbots arc-relay --sheet
            // <out>/materialized-orders.json` plays it directly.
            File.WriteAllBytes(Path.Combine(output, "materialized-orders.json"),
                compilation.MaterializedOrders);
            File.WriteAllBytes(
                Path.Combine(output, "map-plan.json"),
                compilation.MaterializedMapPlan!);
        }
        File.WriteAllBytes(Path.Combine(output, "explain.json"),
            Explain(compilation));

        if (compilation.TacticsSha256 is not null)
            Console.WriteLine($"Tactics:  {compilation.TacticsSha256}");
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
        var explain = new JsonObject
        {
            ["schema"] = "arc-relay-orders-explain-v2",
            ["sheet"] = new JsonObject
            {
                ["id"] = playbook["sheetId"]!.DeepClone(),
                ["sourcePath"] = compilation.PlaybookPath,
                ["sourceSha256"] = compilation.PlaybookSha256,
                ["normalizedBytes"] = compilation.NormalizedPlaybook.Length,
                ["execution"] = "direct-order-tree",
            },
            ["mapPlan"] = new JsonObject
            {
                ["id"] = layout["mapPlanId"]!.DeepClone(),
                ["sourcePath"] = compilation.LayoutPath,
                ["sourceSha256"] = compilation.LayoutSha256,
                ["mapId"] = layout["mapId"]!.DeepClone(),
                ["zones"] = layout["zones"]!.DeepClone(),
                ["routes"] = layout["routes"]!.DeepClone(),
                ["anchors"] = layout["anchors"]!.DeepClone(),
            },
            ["runtimePackage"] = new JsonObject
            {
                ["sha256"] = Sha256(compilation.LinkedData),
                ["bytes"] = compilation.LinkedData.Length,
                ["envelope"] = "ATL2",
            },
            ["roles"] = playbook["roles"]!.DeepClone(),
            ["sharedOrders"] = playbook["sharedOrders"]!.DeepClone(),
            ["capacities"] = playbook["capacities"]!.DeepClone(),
            ["conditions"] = playbook["conditions"]!.DeepClone(),
            ["commandProvenance"] =
                "orders:<role>:<authored-order-path>:<action>:<reason>",
        };
        if (compilation.TacticsSha256 is not null)
        {
            explain["materializedTactics"] = new JsonObject
            {
                ["schema"] = "arc-relay-tactics-v1",
                ["sourceSha256"] = compilation.TacticsSha256,
                ["note"] = "orders and map plan above were deterministically "
                    + "materialized from the tactics document",
            };
        }
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
