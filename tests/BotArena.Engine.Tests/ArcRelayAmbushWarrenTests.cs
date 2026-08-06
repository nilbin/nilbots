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
    public void DenseWarrenMintsBesideBothArmsWithoutMovingThem()
    {
        string warrenMap = ActorContractFingerprint.ComputeMap(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.AmbushWarren).Map);
        string denseMap = ActorContractFingerprint.ComputeMap(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.AmbushWarren3).Map);
        Assert.NotEqual(warrenMap, denseMap);
        Assert.Equal(
            warrenMap,
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren).Map));
        // Same predation rules as -02: the rules documents are identical, so
        // -02 vs -03 is a pure terrain-density A/B.
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren2)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren3)));
        ActorMapDefinition dense = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.AmbushWarren3).Map;
        foreach ((int x, int y) in new[]
                 { (7, 1), (20, 2), (12, 7), (13, 12), (23, 21), (18, 14) })
        {
            Assert.True(dense.IsWall(new Position(x, y)), $"({x},{y}) open");
        }
        foreach ((int x, int y) in new[]
                 { (8, 4), (22, 18), (15, 4), (15, 11), (15, 18), (5, 8) })
        {
            Assert.False(dense.IsWall(new Position(x, y)), $"({x},{y}) walled");
        }
    }

    [Fact]
    public void SerpentineWarrenMintsBesideTheDenseArmWithoutMovingIt()
    {
        string denseMap = ActorContractFingerprint.ComputeMap(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.AmbushWarren3).Map);
        string serpentineMap = ActorContractFingerprint.ComputeMap(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.AmbushWarren4).Map);
        Assert.NotEqual(denseMap, serpentineMap);
        Assert.Equal(
            denseMap,
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren3).Map));
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren2)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren4)));
        ActorMapDefinition serpentine = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.AmbushWarren4).Map;
        // The return chokes: walls above and below, passage tile open.
        foreach ((int x, int y) in new[]
                 { (7, 10), (7, 12), (23, 10), (23, 12), (14, 13), (16, 9) })
        {
            Assert.True(
                serpentine.IsWall(new Position(x, y)), $"({x},{y}) open");
        }
        foreach ((int x, int y) in new[]
                 { (7, 11), (23, 11), (15, 4), (15, 11), (15, 18) })
        {
            Assert.False(
                serpentine.IsWall(new Position(x, y)), $"({x},{y}) walled");
        }
    }

    [Fact]
    public void VeterancyMintsBesideTheSerpentineArmWithItsOwnMapAndRules()
    {
        // -05 changes BOTH rules (veterancy, heal zones) and map (heal
        // regions); -04 re-derives byte-identically beside it.
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren4)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren5)));
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren4).Map),
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren5).Map));
        string json = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren5));
        Assert.Contains("\"veterancyXpPerLevel\":2", json);
        Assert.Contains("\"veterancyMaxLevel\":3", json);
        Assert.Contains("\"healZoneTicksPerHp\":3", json);
        Assert.Contains("\"invest\"", json);
        string priorJson = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren4));
        Assert.DoesNotContain("veterancy", priorJson);
        Assert.DoesNotContain("healZone", priorJson);
    }

    [Fact]
    public void KillsLevelTheKillerAndPointsSpendIntoTracks()
    {
        var driver = new ArcRelayActorMatchModeDriver(
            ArcRelayH0Definition.Create(
                loopProfile: ArcRelayLoopProfile.AmbushWarren5),
            matchSeed: 0);
        var killer = ActorIdentity.FromTeamUnitLife(0, 0, 0);
        var victimOne = ActorIdentity.FromTeamUnitLife(1, 0, 0);
        var victimTwo = ActorIdentity.FromTeamUnitLife(1, 1, 0);

        // First kill: 1 XP - below the 2-XP threshold, still level 1.
        driver.HandleDestructions(10,
            [new FrontlineScrapDestruction(
                victimOne, new Position(10, 10), killer)]);
        Assert.Equal(1, driver.VeterancyLevel(killer));
        Assert.False(driver.TryInvest(11, killer, "damage"));

        // Second kill: reaches 2 XP - level 2, one point to spend.
        var events = driver.HandleDestructions(12,
            [new FrontlineScrapDestruction(
                victimTwo, new Position(10, 11), killer)]);
        Assert.Equal(2, driver.VeterancyLevel(killer));
        Assert.Contains(events, value =>
            value.Payload is GenericActorRuntimeObservation.EventPayload
                .ArcRelay { Fact: ArcRelayEvent.LeveledUp { Level: 2 } });
        Assert.True(driver.TryInvest(13, killer, "damage"));
        Assert.Equal(1, driver.VeterancyDamagePoints(killer));
        // The point is spent: a second invest is refused.
        Assert.False(driver.TryInvest(14, killer, "vision"));

        // Killing the level-2 killer pays the bounty: 1 + 1 XP at once.
        var avenger = ActorIdentity.FromTeamUnitLife(1, 2, 0);
        driver.HandleDestructions(20,
            [new FrontlineScrapDestruction(
                killer, new Position(11, 11), avenger)]);
        Assert.Equal(2, driver.VeterancyLevel(avenger));
        // And the dead killer's progression is gone with its life.
        Assert.Equal(1, driver.VeterancyLevel(killer));
        Assert.Equal(0, driver.VeterancyDamagePoints(killer));
    }

    [Fact]
    public void FairAlternationMintsBesideTheVeterancyArm()
    {
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren5)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren6)));
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren5).Map),
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren6).Map));
        string json = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren6));
        Assert.Contains("\"seedPhasedResolutionOrder\":true", json);
        Assert.DoesNotContain(
            "seedPhasedResolutionOrder",
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren5)));
    }

    [Fact]
    public void RotationalSpawnsMintBesideTheFairAlternationArm()
    {
        // -07 is a map-only mint: the rules stack is byte-identical to -06
        // (rotational assignment is anchor data, not a rules field), while
        // the map re-derives with team 1 unit N anchored at the 180-degree
        // rotation of team 0 unit N instead of its X-flip.
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren6)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren7)));
        ActorMapDefinition legacy = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.AmbushWarren6).Map;
        ActorMapDefinition rotated = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.AmbushWarren7).Map;
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMap(legacy),
            ActorContractFingerprint.ComputeMap(rotated));
        Assert.True(legacy.TileRows.SequenceEqual(rotated.TileRows));

        static Position AnchorOf(ActorMapDefinition map, int team, int unit)
            => map.SpawnAnchors.Single(anchor => anchor.Spawn.SpawnId
                    == $"team-{team}-unit-{unit}")
                .Spawn.Position;
        int maxX = legacy.Width - 1;
        int maxY = legacy.Height - 1;
        for (int unit = 0; unit < 8; unit++)
        {
            Position west = AnchorOf(legacy, 0, unit);
            Assert.Equal(west, AnchorOf(rotated, 0, unit));
            // The legacy assignment stays X-flipped forever; the rotated
            // arm pairs unit N with the full rotation of its own anchor.
            Assert.Equal(
                new Position(maxX - west.X, west.Y),
                AnchorOf(legacy, 1, unit));
            Assert.Equal(
                new Position(maxX - west.X, maxY - west.Y),
                AnchorOf(rotated, 1, unit));
        }
    }

    [Fact]
    public void SeedPhasedWellLeadMintsBesideTheRotationalArm()
    {
        // -08 is a rules-only mint on the -07 map: the flag alone changes
        // the canonical rules, and the driver swaps the rotationally paired
        // flank schedules when the seed coin says so.
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren7)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren8)));
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren7).Map),
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren8).Map));
        Assert.Contains(
            "\"seedPhasedWellLead\":true",
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren8)));
        Assert.DoesNotContain(
            "seedPhasedWellLead",
            ActorContractManifestSerializer.ToCanonicalJson(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren7)));

        // Golden coin values: changing these reorients which seeds swap.
        Assert.Equal(0, SeedDerivation.DeriveWellLeadSwap(0));
        Assert.Equal(1, SeedDerivation.DeriveWellLeadSwap(1));
        Assert.Equal(1, SeedDerivation.DeriveWellLeadSwap(2001));
        Assert.Equal(0, SeedDerivation.DeriveWellLeadSwap(3012));

        static (int? North, int? South) FirstBirths(
            ArcRelayLoopProfile profile,
            ulong matchSeed)
        {
            var driver = new ArcRelayActorMatchModeDriver(
                ArcRelayH0Definition.Create(loopProfile: profile),
                matchSeed);
            var arc = Assert.IsType<GenericActorModeState.ArcRelay>(
                driver.State).State;
            return (
                arc.Wells.Single(w => w.WellId == "north")
                    .NextScheduledBirthTick,
                arc.Wells.Single(w => w.WellId == "south")
                    .NextScheduledBirthTick);
        }

        (int? north, int? south) = FirstBirths(
            ArcRelayLoopProfile.AmbushWarren8, matchSeed: 0);
        Assert.True(north < south, "coin 0 keeps the north lead");
        (north, south) = FirstBirths(
            ArcRelayLoopProfile.AmbushWarren8, matchSeed: 1);
        Assert.True(south < north, "coin 1 hands the lead to the south");
        (north, south) = FirstBirths(
            ArcRelayLoopProfile.AmbushWarren7, matchSeed: 1);
        Assert.True(north < south, "-07 never swaps");
    }

    [Fact]
    public void ApexArmRaisesTheVeterancyCeilingBesideTheWellLeadArm()
    {
        Assert.NotEqual(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren8)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren9)));
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren8).Map),
            ActorContractFingerprint.ComputeMap(
                ArcRelayH0Definition.Create(
                    loopProfile: ArcRelayLoopProfile.AmbushWarren9).Map));
        string json = ActorContractManifestSerializer.ToCanonicalJson(
            ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren9));
        Assert.Contains("\"veterancyMaxLevel\":5", json);
        Assert.Contains("\"seedPhasedWellLead\":true", json);
    }

    [Fact]
    public void DeepWarrenWrapsTheSerpentineInteriorInShadowLanes()
    {
        ActorMapDefinition deep = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.AmbushWarren10).Map;
        ActorMapDefinition serpentine = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.AmbushWarren9).Map;
        Assert.Equal(27, deep.Height);
        Assert.Equal(31, deep.Width);
        // The interior is the serpentine map verbatim, two rows lower.
        for (int y = 1; y <= 21; y++)
            Assert.Equal(serpentine.TileRows[y], deep.TileRows[y + 2]);
        // Shadow corridors are fully open; screens carry exactly the three
        // gates; the whole canvas stays 180-degree chiral.
        Assert.Equal($"#{new string('.', 29)}#", deep.TileRows[1]);
        Assert.Equal(deep.TileRows[1], deep.TileRows[25]);
        Assert.Equal(deep.TileRows[2], deep.TileRows[24]);
        Assert.Equal(
            new[] { 3, 15, 27 },
            Enumerable.Range(0, 31)
                .Where(x => deep.TileRows[2][x] == '.')
                .ToArray());
        for (int y = 0; y < deep.Height; y++)
        {
            for (int x = 0; x < deep.Width; x++)
            {
                Assert.Equal(
                    deep.TileRows[y][x],
                    deep.TileRows[deep.Height - 1 - y][deep.Width - 1 - x]);
            }
        }
        // Landmarks and spawns dropped with the interior.
        Assert.Contains(
            deep.Regions,
            region => region.RegionId == "well-north"
                && region.Tiles.Single() == new Position(15, 6));
        Assert.Contains(
            deep.Regions,
            region => region.RegionId == "heal-south"
                && region.Tiles.Single() == new Position(15, 16));
        Assert.Equal(
            new Position(1, 10),
            deep.SpawnAnchors.Single(anchor =>
                anchor.Spawn.SpawnId == "team-0-unit-0").Spawn.Position);
        Assert.Equal(
            new Position(29, 16),
            deep.SpawnAnchors.Single(anchor =>
                anchor.Spawn.SpawnId == "team-1-unit-0").Spawn.Position);
        // Rules byte-identical to the -09 apex arm: map-only mint.
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren9)),
            ActorContractFingerprint.ComputeRules(
                ArcRelayH0Definition.CreateRules(
                    ArcRelayLoopProfile.AmbushWarren10)));
    }

    [Fact]
    public void ResolutionPhaseDerivationIsPinned()
    {
        // Golden values: changing these changes which seeds flip the
        // alternation parity, which is a gameplay change, not a test update.
        Assert.Equal(1, SeedDerivation.DeriveResolutionPhase(2001));
        Assert.Equal(1, SeedDerivation.DeriveResolutionPhase(2002));
        Assert.Equal(0, SeedDerivation.DeriveResolutionPhase(2004));
        Assert.Equal(0, SeedDerivation.DeriveResolutionPhase(3012));
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
