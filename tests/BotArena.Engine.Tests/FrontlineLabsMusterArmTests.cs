using BotArena.ActorContracts;
using BotArena.Sdk;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the MUSTER arm's contract, map, and identity
/// (<c>docs/DESIGN-SIDE-OBJECTIVES-2026-07-30.md</c>): the shared
/// secondary-control capability round-trips through the canonical mirror, a
/// ruleset that declares no side objective keeps byte-identical fingerprints,
/// the arm's own map generation widens the centre-column alcoves and carries
/// two mirror-symmetric sites, and every class-pair identity fits the
/// 64-character canonical budget.
/// </summary>
public sealed class FrontlineLabsMusterArmTests
{
    private const FrontlineLabsPendulumArm Keel =
        FrontlineLabsPendulumArm.StickyFrontline
        | FrontlineLabsPendulumArm.ForwardRally
        | FrontlineLabsPendulumArm.ContestMajority
        | FrontlineLabsPendulumArm.EnemySoleDecay;

    private const FrontlineLabsSkillKit WholeKit =
        FrontlineLabsSkillKit.StrikerVolley
        | FrontlineLabsSkillKit.BulwarkAegisShell
        | FrontlineLabsSkillKit.FabricatorFiveSlots;

    /// <summary>
    /// The plain class pair, with and without the flag. Without a side
    /// objective the pair IS the historical classes arm, so it keeps that
    /// factory and that identity byte for byte.
    /// </summary>
    private static ActorResolvedMatchDefinition Arm(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsSideObjectiveArm sideObjective) =>
        sideObjective == FrontlineLabsSideObjectiveArm.None
            ? FrontlineLabsDefinition.CreateClassesExperiment(
                teamZero,
                teamOne)
            : FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                (teamZero, teamOne),
                sideObjective: sideObjective);

    /// <summary>The wave-shaped candidate game, with and without the flag.</summary>
    private static ActorResolvedMatchDefinition FullGame(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsSideObjectiveArm sideObjective)
    {
        bool fabricator =
            teamZero.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots
            || teamOne.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots;
        return FrontlineLabsDefinition.CreatePendulumExperiment(
            Keel,
            (teamZero, teamOne),
            movementCoupling: ActorMovementFacingCoupling.FacingLocked,
            skills: WholeKit,
            bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
            fiveSlots: fabricator
                ? FrontlineLabsFiveSlotVariant.Wane
                : FrontlineLabsFiveSlotVariant.Full,
            stanceGround: FrontlineLabsStanceGroundArm.Open,
            aim: FrontlineLabsAimArm.Offset,
            cooldown: FrontlineLabsCooldownArm.Ticking,
            volley: FrontlineLabsVolleyArm.Salvo,
            sideObjective: sideObjective);
    }

    /// <summary>
    /// The whole point of the additive discipline: adding a closed typed
    /// capability moves no bytes for anyone who does not declare it. Every
    /// existing arm — the immutable hosted v1 included — keeps its exact
    /// rules, map, and aggregate match fingerprints.
    /// </summary>
    [Fact]
    public void RulesetsWithoutASideObjectiveKeepByteIdenticalFingerprints()
    {
        foreach (ActorResolvedMatchDefinition definition in new[]
                 {
                     FrontlineLabsDefinition.Create(),
                     FrontlineLabsDefinition.CreateOneBendShotsExperiment(),
                     FullGame(
                         FrontlineLabsClassDefinition.Bulwark,
                         FrontlineLabsClassDefinition.Striker,
                         FrontlineLabsSideObjectiveArm.None),
                 })
        {
            string canonical =
                ActorContractManifestSerializer.ToCanonicalJson(definition);
            Assert.DoesNotContain(
                "secondaryControl",
                canonical,
                StringComparison.Ordinal);
        }

        // The hosted v1 ruleset's own fingerprints, pinned literally against
        // the definition the App admits.
        ActorResolvedMatchDefinition hosted = FrontlineLabsDefinition.Create();
        Assert.Equal(
            FrontlineLabsDefinition.RulesetId,
            hosted.Rules.RulesetId);
        Assert.Equal(FrontlineLabsDefinition.MapId, hosted.Map.Id);
        Assert.Null(
            ((FrontlineGameModeDefinition)hosted.Rules.GameMode)
                .SecondaryControl);
    }

    /// <summary>
    /// The capability is real contract data: the canonical writer emits the
    /// declared site regions, latch threshold, ownership, effect, and rally
    /// scope, and the SDK canonical reader — the same parser the admission
    /// validator runs — reproduces the exact match fingerprint from them.
    /// </summary>
    [Fact]
    public void TheSecondaryControlCapabilityRoundTripsThroughTheMirror()
    {
        ActorResolvedMatchDefinition muster = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsSideObjectiveArm.Muster);
        string canonical =
            ActorContractManifestSerializer.ToCanonicalJson(muster);

        Assert.Contains(
            "\"secondaryControl\":{\"regionIds\":[\"muster-site-north\","
            + "\"muster-site-south\"],\"captureThresholdTicks\":"
            + $"{FrontlineLabsMusterSite.LatchTicks},"
            + "\"ownership\":\"latched-until-recaptured-by-sole-objective-"
            + "weight\",\"effect\":\"muster\",\"rallyScope\":\"prime-"
            + "automatic-return-only\"}",
            canonical,
            StringComparison.Ordinal);

        GenericActorResolvedMatchContract contract =
            ActorCanonicalContractReader.Parse(canonical);
        var mode =
            (GenericActorRulesContract.FrontlineGameMode)contract.Rules.GameMode;
        GenericActorRulesContract.FrontlineSecondaryControl secondary =
            Assert.IsType<GenericActorRulesContract.FrontlineSecondaryControl>(
                mode.SecondaryControl);
        Assert.Equal(
            [
                FrontlineLabsMusterSite.NorthRegionId,
                FrontlineLabsMusterSite.SouthRegionId,
            ],
            secondary.RegionIds.ToArray());
        Assert.Equal(
            FrontlineLabsMusterSite.LatchTicks,
            secondary.CaptureThresholdTicks);
        Assert.Equal("muster", secondary.Effect);
        Assert.Equal(
            "prime-automatic-return-only",
            secondary.RallyScope);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(muster),
            contract.MatchContractFingerprint);

        GenericActorCanonicalContractValidation validation =
            GenericActorCanonicalContractValidator.Validate(canonical);
        Assert.Equal(muster.Rules.RulesetId, validation.RulesetId);
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(muster),
            validation.MatchContractFingerprint);
    }

    /// <summary>
    /// The arm mints its own map generation instead of editing an existing
    /// one, so no shipped map golden moves, and the muster map differs from
    /// the classes map by exactly the widened shoulders plus the two sites.
    /// </summary>
    [Fact]
    public void TheMusterMapIsANewGenerationBesideTheClassesMap()
    {
        ActorMapDefinition classes = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsSideObjectiveArm.None).Map;
        ActorMapDefinition muster = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsSideObjectiveArm.Muster).Map;

        Assert.Equal("frontline-labs-01-classes", classes.Id);
        Assert.Equal("frontline-labs-02-muster-classes", muster.Id);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMap(classes),
            ActorContractFingerprint.ComputeMap(muster));

        // Exactly four tiles change from wall to floor, and they are the
        // alcove shoulders the design required before a shell could be
        // stopped from owning a 1-wide cul-de-sac.
        Position[] opened =
        [
            .. FrontlineLabsMusterSite.OpenedShoulders
                .Select(tile => new Position(tile.X, tile.Y)),
        ];
        Assert.All(opened, tile =>
        {
            Assert.True(classes.IsWall(tile), $"{tile} was already floor");
            Assert.False(muster.IsWall(tile), $"{tile} is still wall");
        });
        for (int y = 0; y < muster.Height; y++)
        {
            for (int x = 0; x < muster.Width; x++)
            {
                var tile = new Position(x, y);
                if (opened.Contains(tile))
                    continue;
                Assert.Equal(classes.IsWall(tile), muster.IsWall(tile));
            }
        }
    }

    /// <summary>
    /// Mirror fairness, verified the way the map's own regions are: the
    /// fairness axis is the vertical centre line the two spawns face across,
    /// so every row must read the same forwards and backwards and both sites
    /// must map onto themselves. The two sites are also exact reflections of
    /// each other across the centre row, so neither half of the map is the
    /// better half to hold.
    /// </summary>
    [Fact]
    public void TheMusterSitesAreMirrorFairAndUnflankableNowhere()
    {
        ActorMapDefinition map = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker,
            FrontlineLabsSideObjectiveArm.Muster).Map;
        int mirrorX = map.Width - 1;
        int mirrorY = map.Height - 1;

        Assert.All(map.TileRows, row => Assert.Equal(
            row,
            new string([.. row.Reverse()])));

        ActorMapRegionDefinition north = map.Regions.Single(region =>
            region.RegionId == FrontlineLabsMusterSite.NorthRegionId);
        ActorMapRegionDefinition south = map.Regions.Single(region =>
            region.RegionId == FrontlineLabsMusterSite.SouthRegionId);
        Assert.All(
            new[] { north, south },
            region =>
            {
                Assert.Equal(
                    ActorMapRegionDefinition.RegionKind.Objective,
                    region.Kind);
                Assert.Equal(2, region.Tiles.Length);
                Assert.All(region.Tiles, tile =>
                {
                    Assert.False(map.IsWall(tile));
                    // Self-mirroring across the fairness axis: both spawns
                    // are the same number of steps from every site tile.
                    Assert.Equal(
                        tile,
                        new Position(mirrorX - tile.X, tile.Y));
                    // And no site tile is a 1-wide dead end, which is what
                    // the shoulder widening buys.
                    Assert.True(
                        OpenNeighbours(map, tile) >= 2,
                        $"{tile} has one approach heading");
                });
            });
        Assert.Equal(
            [.. south.Tiles.OrderBy(tile => tile.Y)],
            [
                .. north.Tiles
                    .Select(tile => new Position(tile.X, mirrorY - tile.Y))
                    .OrderBy(tile => tile.Y),
            ]);

        // Neither site is part of the frontline chain — the memo's L2 shape:
        // a side objective sits OFF the line it is meant to open the map
        // around.
        var binding =
            (FrontlineActorModeMapBindingDefinition)Arm(
                FrontlineLabsClassDefinition.Bulwark,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsSideObjectiveArm.Muster).ModeMapBinding;
        Assert.DoesNotContain(
            FrontlineLabsMusterSite.NorthRegionId,
            binding.OrderedObjectiveRegionIds);
        Assert.DoesNotContain(
            FrontlineLabsMusterSite.SouthRegionId,
            binding.OrderedObjectiveRegionIds);
    }

    /// <summary>
    /// The flag is what a rally costs now. Even on the keel — which hands
    /// both teams the forward placement unconditionally — the muster arm
    /// reverts the lifecycle placement to the reserved home spawn, so
    /// ownership is the only thing that can move an arrival.
    /// </summary>
    [Fact]
    public void MusterTakesTheUnconditionalRallyAwayFromBothTeams()
    {
        ActorResolvedMatchDefinition keel =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker));
        ActorResolvedMatchDefinition flagged =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                sideObjective: FrontlineLabsSideObjectiveArm.Muster);

        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn,
            keel.Rules.Lifecycle.AutomaticReturnPlacement);
        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims,
            flagged.Rules.Lifecycle.AutomaticReturnPlacement);
    }

    /// <summary>
    /// The identity budget. A side objective is a real arm on every pair —
    /// it changes the map for both teams, so it is never inert-omitted — and
    /// the worst class cell leaves eight characters for arms and tuning
    /// beside <c>facing-locked</c>. The full candidate game therefore
    /// re-mints under one registered token, exactly as <c>swell</c> did.
    /// </summary>
    [Fact]
    public void EveryClassPairIdentityFitsTheCanonicalBudget()
    {
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            string bare = Arm(zero, one, FrontlineLabsSideObjectiveArm.Muster)
                .Rules.RulesetId;
            Assert.EndsWith("-muster", bare, StringComparison.Ordinal);
            Assert.True(bare.Length <= 64, $"{bare} is {bare.Length}");

            string full = FullGame(
                zero,
                one,
                FrontlineLabsSideObjectiveArm.Muster).Rules.RulesetId;
            bool striker =
                zero.Skill == FrontlineLabsSkillKit.StrikerVolley
                || one.Skill == FrontlineLabsSkillKit.StrikerVolley;
            bool fabricator =
                zero.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots
                || one.Skill == FrontlineLabsSkillKit.FabricatorFiveSlots;
            Assert.Contains(
                striker
                    ? "-banner-"
                    : fabricator
                        ? "-ensign-"
                        : "-pennant-",
                full,
                StringComparison.Ordinal);
            Assert.True(full.Length <= 64, $"{full} is {full.Length}");
        }

        // The registered tokens are distinct rulesets, not aliases: the
        // flagged game and the unflagged game never share an identity or a
        // fingerprint.
        ActorResolvedMatchDefinition worstFlagged = FullGame(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsSideObjectiveArm.Muster);
        ActorResolvedMatchDefinition worstPlain = FullGame(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsSideObjectiveArm.None);
        Assert.Equal(
            "frontline-labs-1-fabricator-vs-fabricator-ensign-facing-locked",
            worstFlagged.Rules.RulesetId);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(worstPlain),
            ActorContractFingerprint.ComputeMatch(worstFlagged));
    }

    private static int OpenNeighbours(
        ActorMapDefinition map,
        Position tile) =>
        new[] { (0, -1), (1, 0), (0, 1), (-1, 0) }
            .Count(step => !map.IsWall(tile.Offset(step.Item1, step.Item2)));
}
