namespace BotArena.Engine.Tests;

/// <summary>
/// Ambush terrain prototype (owner direction 2026-08-05): the counterflow
/// map plus chiral-paired sightline breakers and dead-end alcoves, minted
/// BESIDE -03 so every prior ruleset's bytes never move.
/// </summary>
public sealed class ArcRelayAmbushWarrenTests
{
    [Fact]
    public void AmbushWarrenMintsBesideForwardCombat3WithoutMovingIt()
    {
        // The terrain experiment deliberately changes no rules: the rules
        // documents are byte-identical and only the MAP mints a new
        // fingerprint, so any behavior difference is terrain and nothing
        // else.
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.ForwardCombat3)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren)));
        string threeMap = ActorContractFingerprint.ComputeMap(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.ForwardCombat3).Map);
        string warrenMap = ActorContractFingerprint.ComputeMap(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.AmbushWarren).Map);
        Assert.NotEqual(threeMap, warrenMap);
        Assert.Equal(
            threeMap,
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.ForwardCombat3).Map));
    }

    [Fact]
    public void WarrenTerrainFormsTheAlcovesAndKeepsTheLandmarksOpen()
    {
        ActorMapDefinition map = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.AmbushWarren).Map;
        // Alcove walls present, in exact chiral pairs.
        foreach ((int x, int y) in new[]
                 {
                     (7, 4), (7, 5), (8, 5), (23, 18), (23, 17), (22, 17),
                     (7, 17), (8, 19), (23, 5), (22, 3),
                     (9, 2), (21, 20), (12, 6), (18, 16),
                 })
        {
            Assert.True(map.IsWall(new Position(x, y)), $"({x},{y}) open");
        }
        // The pockets themselves and every landmark stay standable.
        foreach ((int x, int y) in new[]
                 {
                     (8, 4), (22, 18), (8, 18), (22, 4),
                     (15, 4), (15, 11), (15, 18), (2, 11), (28, 11),
                     (5, 8), (25, 14),
                 })
        {
            Assert.False(map.IsWall(new Position(x, y)), $"({x},{y}) walled");
        }
    }

    [Fact]
    public void PredationRulesMintBesideTheTerrainArm()
    {
        string terrain = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren));
        string predation = ActorContractFingerprint.ComputeRules(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren2));
        Assert.NotEqual(terrain, predation);
        // The terrain arm re-derives byte-identically beside the rules arm.
        Assert.Equal(
            terrain,
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren)));
        string json = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren2));
        Assert.Contains("\"rearArcDamageMultiplier\":2", json);
        Assert.Contains("\"respawnDelayTicks\":30", json);
        Assert.Contains("\"omnidirectionalProximityRange\":0", json);
        string terrainJson = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren));
        Assert.DoesNotContain("rearArcDamageMultiplier", terrainJson);
        Assert.Contains("\"omnidirectionalProximityRange\":1", terrainJson);
    }

    [Fact]
    public void CounterflowMapIsUntouchedByTheWarrenMint()
    {
        ActorMapDefinition counterflow = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.ForwardCombat3).Map;
        foreach ((int x, int y) in new[]
                 { (7, 4), (8, 5), (9, 2), (12, 6), (22, 17) })
        {
            Assert.False(
                counterflow.IsWall(new Position(x, y)),
                $"({x},{y}) walled on counterflow");
        }
    }
}
