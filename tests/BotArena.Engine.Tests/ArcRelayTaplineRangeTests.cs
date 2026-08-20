namespace BotArena.Engine.Tests;

/// <summary>
/// Pins Tapline-10's smaller combat envelope beside immutable Tapline-9.
/// </summary>
public sealed class ArcRelayTaplineRangeTests
{
    [Fact]
    public void Tapline10ContractsVisionAndEveryBasicGunBandOnly()
    {
        ActorResolvedMatchDefinition historical = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline9);
        ActorResolvedMatchDefinition current = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline10);

        Assert.Equal("arc-relay-tapline-10", current.Rules.RulesetId);
        Assert.Equal(historical.Map.Id, current.Map.Id);
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(historical.Map),
            ActorContractFingerprint.ComputeMap(current.Map));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(historical.Rules),
            ActorContractFingerprint.ComputeRules(current.Rules));

        Assert.Single(current.Rules.VisionProfiles);
        Assert.Equal(5, current.Rules.VisionProfiles.Single().Range);
        Assert.Equal(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["short-fast"] = 3,
                ["medium-steady"] = 4,
                ["long-slow"] = 6,
            },
            current.Rules.AttackProfiles
                .Where(profile => profile.Id is
                    "short-fast" or "medium-steady" or "long-slow")
                .ToDictionary(
                    profile => profile.Id,
                    profile => profile.Projectile.MaxTravelTiles,
                    StringComparer.Ordinal));

        Assert.Equal(
            historical.Rules.Forms.Select(form => (
                form.Id, form.AttackProfileId)),
            current.Rules.Forms.Select(form => (
                form.Id, form.AttackProfileId)));
        Assert.Equal(
            ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion,
            current.Rules.TeamPerception.Kind);
        Assert.True(current.Rules.AttackProfiles.Single(profile =>
                profile.Id == "long-slow").Projectile.MaxTravelTiles
            > current.Rules.VisionProfiles.Single().Range);
    }

    [Fact]
    public void Tapline9RetainsItsHistoricalCombatEnvelope()
    {
        ActorResolvedMatchDefinition historical = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline9);

        Assert.Equal(7, historical.Rules.VisionProfiles.Single().Range);
        Assert.Equal(4, Range(historical, "short-fast"));
        Assert.Equal(6, Range(historical, "medium-steady"));
        Assert.Equal(9, Range(historical, "long-slow"));
    }

    private static int Range(
        ActorResolvedMatchDefinition definition,
        string profileId) => definition.Rules.AttackProfiles.Single(profile =>
            profile.Id == profileId).Projectile.MaxTravelTiles;
}
