using BotArena.App.ArcRelay;
using BotArena.Engine;
using BotArena.TacticalPlaybooks;

namespace BotArena.App.Tests;

public sealed class ArcRelayClassCatalogTests
{
    [Fact]
    public void SignatureFieldManualExactlyNamesTheCurrentLaunchBand()
    {
        string path = Path.Combine(
            RepositoryRoot(),
            "docs",
            "ARC-RELAY-SIGNATURE-FIELD-MANUAL.html");
        string document = File.ReadAllText(path);
        ArcRelaySignatureDefinition[] signatures =
            ((ArcRelayGameModeDefinition)ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.Current).GameMode).Signatures.ToArray();

        Assert.Equal(16, signatures.Length);
        Assert.Equal(16, System.Text.RegularExpressions.Regex.Matches(
            document, "classId: \\\"[^\\\"]+\\\"").Count);
        foreach (ArcRelaySignatureDefinition signature in signatures)
        {
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(
                    document,
                    $"classId: \\\"{System.Text.RegularExpressions.Regex.Escape(
                        signature.ClassId)}\\\"")
                .Cast<System.Text.RegularExpressions.Match>());
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(
                    document,
                    $"id: \\\"{System.Text.RegularExpressions.Regex.Escape(
                        signature.SignatureId)}\\\"")
                .Cast<System.Text.RegularExpressions.Match>());
        }

        Assert.Contains("Broadloom-4", document, StringComparison.Ordinal);
        Assert.Contains("Broadloom-5 candidate", document,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SignatureAuthoringVocabularyExactlyCoversTheLaunchBand()
    {
        ArcRelaySignatureAuthoringDescriptor[] values =
            ArcRelaySignatureAuthoringCatalog.All.ToArray();
        Assert.Equal(16, values.Length);
        Assert.Equal(16, values.Select(value => value.ClassId).Distinct().Count());
        Assert.Equal(16, values.Select(value => value.SkillId).Distinct().Count());
        Assert.Equal(ArcRelayLaunchClassIds.All, values.Select(value =>
            value.ClassId));

        (string ClassId, string SkillId,
            ArcRelaySignatureTargetCapability Capabilities)[] expected =
        [
            ("kestrel", "vector-dash",
                ArcRelaySignatureTargetCapability.RouteForward),
            ("palisade", "prism-wall", 0),
            ("towline", "tractor-hook",
                ArcRelaySignatureTargetCapability.AuthoredRay),
            ("patchbay", "repair-beam",
                ArcRelaySignatureTargetCapability.FriendlyRole
                | ArcRelaySignatureTargetCapability.MostExperiencedAlly),
            ("lantern", "survey-flare",
                ArcRelaySignatureTargetCapability.ExactAnchor),
            ("mortar", "falling-star",
                ArcRelaySignatureTargetCapability.ExactAnchor),
            ("minesmith", "trip-node",
                ArcRelaySignatureTargetCapability.ExactAnchor),
            ("hush", "null-field", 0),
            ("relay", "arc-toss",
                ArcRelaySignatureTargetCapability.FriendlyRole
                | ArcRelaySignatureTargetCapability.MostExperiencedAlly
                | ArcRelaySignatureTargetCapability.RouteAheadRole
                | ArcRelaySignatureTargetCapability.RouteForward),
            ("switchback", "exchange",
                ArcRelaySignatureTargetCapability.FriendlyRole
                | ArcRelaySignatureTargetCapability.MostExperiencedAlly),
            ("longshot", "rail-line", 0),
            ("mason", "hardlight-block",
                ArcRelaySignatureTargetCapability.ExactAnchor),
            ("sunder", "target-paint", 0),
            ("repulsor", "kinetic-burst", 0),
            ("veil", "smoke-canister",
                ArcRelaySignatureTargetCapability.ExactAnchor),
            ("nest", "sentinel-seed",
                ArcRelaySignatureTargetCapability.ExactAnchor),
        ];

        Assert.Equal(expected, values.Select(value => (
            value.ClassId, value.SkillId, value.Capabilities)));
        Assert.All(values, value =>
            Assert.NotEmpty(value.AllowedNamedTargets));

        Assert.Equal(
            new[]
            {
                ArcRelaySignatureOpportunityBehavior.RouteForward,
                ArcRelaySignatureOpportunityBehavior.ScreenThreatenedLane,
                ArcRelaySignatureOpportunityBehavior.PullEnemyCarrier,
                ArcRelaySignatureOpportunityBehavior.RepairDamagedAlly,
                ArcRelaySignatureOpportunityBehavior.RevealSmoke,
                ArcRelaySignatureOpportunityBehavior.StrikeOccupiedAnchor,
                ArcRelaySignatureOpportunityBehavior.DeployUnoccupiedAnchor,
                ArcRelaySignatureOpportunityBehavior.SuppressHostileSignature,
                ArcRelaySignatureOpportunityBehavior.TransferForward,
                ArcRelaySignatureOpportunityBehavior.AdvanceFriendlyCarrier,
                ArcRelaySignatureOpportunityBehavior.RailVisibleEnemy,
                ArcRelaySignatureOpportunityBehavior.BlockUnoccupiedChoke,
                ArcRelaySignatureOpportunityBehavior.PaintSupportedEnemy,
                ArcRelaySignatureOpportunityBehavior.BurstCrowd,
                ArcRelaySignatureOpportunityBehavior.SmokeThreatenedCrossing,
                ArcRelaySignatureOpportunityBehavior.SeedAfterTeamContact,
            },
            values.Select(value => value.Opportunity));
        Assert.Equal(
            ArcRelaySignatureCustodyBehavior.CarrierSafe,
            ArcRelaySignatureAuthoringCatalog.ForClass("palisade").Custody);
        Assert.Equal(
            ArcRelaySignatureCustodyBehavior.TransferOnly,
            ArcRelaySignatureAuthoringCatalog.ForClass("relay").Custody);
        Assert.Equal(
            (ArcRelaySignatureGuidanceGeometry.None, 0),
            (ArcRelaySignatureAuthoringCatalog.ForClass("longshot")
                .GuidanceGeometry,
             ArcRelaySignatureAuthoringCatalog.ForClass("longshot")
                .GuidanceMarkLimit));
        Assert.Equal(
            (ArcRelaySignatureGuidanceGeometry.Points, 3),
            (ArcRelaySignatureAuthoringCatalog.ForClass("minesmith")
                .GuidanceGeometry,
             ArcRelaySignatureAuthoringCatalog.ForClass("minesmith")
                .GuidanceMarkLimit));
    }

    [Fact]
    public void ProductCatalogBindsEveryLaunchClassToItsEngineSignatureAndAction()
    {
        ArcRelayClassCatalog catalog = ArcRelayClassCatalog.Default;
        ActorRulesDefinition rules = ArcRelayH0Definition.CreateRules(
            ArcRelayLoopProfile.Current);
        var mode = Assert.IsType<ArcRelayGameModeDefinition>(rules.GameMode);

        Assert.Equal(ArcRelayLaunchClassIds.All, catalog.All.Select(value =>
            value.Id));
        Assert.Equal(
            ArcRelayLaunchClassIds.All.Order(StringComparer.Ordinal),
            mode.Signatures.Select(value => value.ClassId)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            catalog.All.Count,
            catalog.All.Select(value => value.SignatureId).Distinct().Count());
        Assert.Equal(
            catalog.All.Count,
            catalog.All.Select(value => value.ActionId).Distinct().Count());

        foreach (ArcRelayClassDefinition product in catalog.All)
        {
            ArcRelaySignatureDefinition signature = mode.Signatures.Single(
                value => value.ClassId == product.Id);
            Assert.Equal(signature.SignatureId, product.SignatureId);
            Assert.Equal(signature.ActionId, product.ActionId);
            Assert.Contains(rules.Actions, value =>
                value.Id == product.ActionId);
            Assert.False(string.IsNullOrWhiteSpace(product.SignatureName));
            Assert.False(string.IsNullOrWhiteSpace(product.Fantasy));
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName,
                   "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException(
            "Repository root not found.");
    }
}
