using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace BotArena.Cli.Tests;

public sealed class ArcRelayCoordinationParityControlTests
{
    [Fact]
    public void ParityControlChangesOnlyCoordinationAndAuditIdentity()
    {
        string root = FindRepoRoot();
        string sourcePath = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "forward-combat-operation-proof-v1-2026-08-03",
            "sheets",
            "baseline.json");
        string controlPath = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "controls",
            "coordination-parity-baseline.json");
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath))!
            .AsObject();
        JsonObject control = JsonNode.Parse(File.ReadAllBytes(controlPath))!
            .AsObject();

        Assert.Null(source["attackCoordination"]);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(
                File.ReadAllBytes(sourcePath))),
            control["dynamicStrategyAudit"]!["derivedFromSha256"]!
                .GetValue<string>());

        HashSet<string> allowedDifferences =
        [
            "sheetId",
            "auditDimensions",
            "dynamicStrategyAudit",
            "attackCoordination",
        ];
        Assert.Equal(
            source.Select(value => value.Key)
                .Where(key => !allowedDifferences.Contains(key))
                .Order(StringComparer.Ordinal),
            control.Select(value => value.Key)
                .Where(key => !allowedDifferences.Contains(key))
                .Order(StringComparer.Ordinal));
        foreach ((string key, JsonNode? value) in source)
        {
            if (!allowedDifferences.Contains(key))
                Assert.True(JsonNode.DeepEquals(value, control[key]), key);
        }

        JsonObject coordination = control["attackCoordination"]!.AsObject();
        Assert.Equal("shared-damage-budget",
            coordination["mode"]!.GetValue<string>());
        Assert.Equal(
            ["enemy-carrier", "lowest-health", "nearest"],
            coordination["targetPriorities"]!.AsArray()
                .Select(value => value!.GetValue<string>()));
        Assert.Equal(
            ["health", "distance", "actor-id"],
            coordination["tieBreakers"]!.AsArray()
                .Select(value => value!.GetValue<string>()));
        Assert.Equal(5,
            coordination["maximumAttackersPerTarget"]!.GetValue<int>());
        Assert.Equal(0, coordination["overkillDamage"]!.GetValue<int>());
        Assert.Equal(3, coordination["lockTicks"]!.GetValue<int>());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}
