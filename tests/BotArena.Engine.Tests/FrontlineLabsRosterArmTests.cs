using System.Collections.Immutable;
using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the LEGION roster arm's contract: the three registered slot shapes,
/// the staged availability the owner asked for (three live bodies at tick
/// zero, four fabricable slots for the fabricator, +2 at 150 and +3 at 300),
/// the new mirror-fair map generation the extra reserved anchors need, the
/// superseded five-slot schedule, and every class-pair identity inside the
/// 64-character canonical budget. A ruleset that declares no roster keeps
/// byte-identical map, topology, and match fingerprints.
/// </summary>
public sealed class FrontlineLabsRosterArmTests
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

    /// <summary>A bare roster cell: the roster arm and nothing else.</summary>
    internal static ActorResolvedMatchDefinition Arm(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsRosterArm roster = FrontlineLabsRosterArm.Legion) =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.None,
            (teamZero, teamOne),
            roster: roster);

    /// <summary>The v1.1 shipped game, with and without the roster.</summary>
    internal static ActorResolvedMatchDefinition FullGame(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsRosterArm roster)
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
            capture: FrontlineLabsCaptureArm.Channel,
            economy: FrontlineLabsEconomyArm.Scrap,
            roster: roster);
    }

    /// <summary>
    /// Eight slots per team, nine for the fabricator — and all three shapes
    /// are registered, because a profile ID names a topology and an
    /// unregistered shape faults rather than borrowing a neighbour's label.
    /// </summary>
    [Fact]
    public void EveryTeamFieldsEightSlotsAndTheFabricatorNine()
    {
        ActorResolvedMatchDefinition mirror = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker);
        Assert.Equal(8, mirror.Topology.UnitSlots.Count(slot =>
            slot.TeamId == 0));
        Assert.Equal(8, mirror.Topology.UnitSlots.Count(slot =>
            slot.TeamId == 1));
        Assert.Equal(
            FrontlineLabsDefinition.LegionMirrorTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(mirror.Topology));

        ActorResolvedMatchDefinition asymmetric = Arm(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Striker);
        Assert.Equal(9, asymmetric.Topology.UnitSlots.Count(slot =>
            slot.TeamId == 0));
        Assert.Equal(8, asymmetric.Topology.UnitSlots.Count(slot =>
            slot.TeamId == 1));
        Assert.Equal(
            FrontlineLabsDefinition.LegionAsymmetricSlotsTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                asymmetric.Topology));

        ActorResolvedMatchDefinition fabricators = Arm(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Fabricator);
        Assert.Equal(18, fabricators.Topology.UnitSlots.Length);
        Assert.Equal(
            FrontlineLabsDefinition
                .LegionFabricatorMirrorTopologyProfileId,
            FrontlineLabsDefinition.TopologyProfileIdFor(
                fabricators.Topology));
    }

    /// <summary>
    /// The opening: a class that receives companions stands three bodies on
    /// the field at tick zero, deployed on its own reserved anchors. The
    /// fabricator stands one and holds three unlocked slots, because its
    /// bodies are fabricated — the class verb #154 gave it — and they arrive
    /// in the field rather than on a pad.
    /// </summary>
    [Fact]
    public void ThreeBodiesStandAtTickZeroAndTheFabricatorHoldsFourSlots()
    {
        ActorResolvedMatchDefinition cell = Arm(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Striker);

        Assert.Equal(
            [1, 2],
            cell.Topology.InitialLives
                .Where(life => life.TeamId == 1)
                .Select(life => life.UnitId)
                .Where(unitId => unitId > 0)
                .Order());
        Assert.Equal(
            3,
            cell.Topology.InitialLives.Count(life => life.TeamId == 1));
        Assert.All(
            cell.Topology.InitialLives.Where(life =>
                life.TeamId == 1 && life.UnitId > 0),
            life => Assert.Equal(
                FrontlineLabsClassDefinition.Striker.ChildFormId,
                life.FormId));
        Assert.Equal(
            [
                "team-1-child-1",
                "team-1-child-2",
                "team-1-prime",
            ],
            cell.InitialDeployment.Lives
                .Where(life => life.TeamId == 1)
                .Select(life => life.SpawnId)
                .Order());

        // The fabricator side: one body, three slots ready from tick zero.
        Assert.Equal(
            1,
            cell.Topology.InitialLives.Count(life => life.TeamId == 0));
        Assert.Equal(
            [0, 0, 0],
            Companions(cell, 0)
                .Take(3)
                .Select(assignment => assignment.UnlockTick!.Value));
        Assert.All(
            Companions(cell, 0),
            assignment =>
            {
                Assert.Equal(
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.DormantUnlockAtTick,
                    assignment.InitialAvailability);
                Assert.Null(assignment.AssignedRespawnSpawnId);
            });
    }

    /// <summary>
    /// The staged schedule, declared rather than inferred: two slots at 150
    /// and three at 300, on both classes, with the automatic classes carrying
    /// the 0.10.4 automatic-activation kind and its reserved anchor.
    /// </summary>
    [Fact]
    public void TheTranchesDeclareTheirOwnAbsoluteActivationTicks()
    {
        ActorResolvedMatchDefinition cell = Arm(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Striker);

        Assert.Equal(
            [0, 0, 0, 150, 150, 300, 300, 300],
            Companions(cell, 0)
                .Select(assignment => assignment.UnlockTick!.Value));
        Assert.Equal(
            [150, 150, 300, 300, 300],
            Companions(cell, 1)
                .Where(assignment => assignment.UnlockTick is not null)
                .Select(assignment => assignment.UnlockTick!.Value));
        Assert.All(
            Companions(cell, 1).Take(2),
            assignment =>
            {
                Assert.Equal(
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.ActiveAtTickZero,
                    assignment.InitialAvailability);
                Assert.Null(assignment.UnlockTick);
                Assert.NotNull(assignment.AssignedRespawnSpawnId);
            });
        Assert.All(
            Companions(cell, 1).Skip(2),
            assignment =>
            {
                Assert.Equal(
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind
                        .DormantAutomaticActivationAtTick,
                    assignment.InitialAvailability);
                Assert.NotNull(assignment.AssignedRespawnSpawnId);
            });
        // Every activation lands inside the match, with the late tranche
        // still owing 200 ticks of defending.
        Assert.All(
            Companions(cell, 1),
            assignment => Assert.True(
                (assignment.UnlockTick ?? 0) + 200
                <= cell.Rules.Limits.MaxTicks));
    }

    /// <summary>
    /// The map is the constraint: a slot that returns automatically needs a
    /// reserved anchor, and the measured pad has room for two. So the arm
    /// mints its own generation — the classes map's exact tiles, seven
    /// mirror-fair companion anchors per team, and a pad widened to cover
    /// them. Nothing edits an existing map.
    /// </summary>
    [Fact]
    public void TheLegionMapIsTheClassesMapPlusMirrorFairAnchors()
    {
        ActorResolvedMatchDefinition legion = Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker);
        ActorMapDefinition map = legion.Map;
        ActorMapDefinition classes =
            FrontlineLabsDefinition.CreateClassesExperiment(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker)
                .Map;

        Assert.Equal("frontline-labs-03-legion-classes", map.Id);
        Assert.Equal(classes.TileRows.ToArray(), map.TileRows.ToArray());
        // The fairness axis is the vertical centre line the two spawns face
        // across: every row reads the same forwards and backwards.
        Assert.All(map.TileRows, row => Assert.Equal(
            row,
            new string([.. row.Reverse()])));

        int mirrorX = map.Width - 1;
        Assert.Equal(16, map.SpawnAnchors.Length);
        Assert.Equal(
            map.SpawnAnchors.Length,
            map.SpawnAnchors
                .Select(anchor => anchor.Spawn.Position)
                .Distinct()
                .Count());
        foreach (ActorMapSpawnAnchorDefinition anchor in map.SpawnAnchors)
        {
            Position tile = anchor.Spawn.Position;
            Assert.False(map.IsWall(tile));
            // Every anchor has an exact opposite-team reflection.
            Assert.Contains(
                map.SpawnAnchors,
                other => other.Spawn.Position
                    == new Position(mirrorX - tile.X, tile.Y));
        }
        // The measured opening geometry is unchanged: the first two
        // companion anchors are exactly the classes map's pair.
        foreach (ActorMapSpawnAnchorDefinition anchor in classes.SpawnAnchors)
        {
            Assert.Contains(
                map.SpawnAnchors,
                other => other.Spawn == anchor.Spawn);
        }

        // Every anchor stands on its own team's protected, banking pad — an
        // anchor an enemy can camp is a reserved return that can be denied.
        ImmutableArray<Position> protectedTiles = map.TileTags
            .Single(tag => tag.Kind
                == ActorMapTileTagDefinition.TileTagKind.SpawnProtected)
            .Tiles;
        Assert.Equal(20, protectedTiles.Length);
        Assert.All(
            map.SpawnAnchors,
            anchor => Assert.Contains(anchor.Spawn.Position, protectedTiles));
        foreach (int teamId in new[] { 0, 1 })
        {
            ImmutableArray<Position> pad = map.Regions
                .Single(region => region.RegionId == $"team-{teamId}-home-pad")
                .Tiles;
            Assert.Equal(10, pad.Length);
            Assert.All(
                map.SpawnAnchors
                    .Where(anchor =>
                        anchor.Spawn.SpawnId.StartsWith(
                            $"team-{teamId}-",
                            StringComparison.Ordinal)),
                anchor => Assert.Contains(anchor.Spawn.Position, pad));
        }
        // The pads are exact reflections of each other.
        Assert.Equal(
            map.Regions.Single(region =>
                    region.RegionId == "team-1-home-pad")
                .Tiles
                .OrderBy(tile => tile.Y)
                .ThenBy(tile => tile.X),
            map.Regions.Single(region =>
                    region.RegionId == "team-0-home-pad")
                .Tiles
                .Select(tile => new Position(mirrorX - tile.X, tile.Y))
                .OrderBy(tile => tile.Y)
                .ThenBy(tile => tile.X));
    }

    /// <summary>
    /// The additive discipline: a ruleset that declares no roster keeps its
    /// exact map, topology, and match fingerprints — the historical labs
    /// contracts, the hosted v1 included, do not move a byte.
    /// </summary>
    [Fact]
    public void RulesetsWithoutTheRosterKeepByteIdenticalFingerprints()
    {
        foreach (ActorResolvedMatchDefinition definition in new[]
                 {
                     FrontlineLabsDefinition.Create(),
                     FrontlineLabsDefinition.CreateAutomaticCompanionsExperiment(),
                     FrontlineLabsDefinition.CreateClassesExperiment(
                         FrontlineLabsClassDefinition.Bulwark,
                         FrontlineLabsClassDefinition.Striker),
                     FullGame(
                         FrontlineLabsClassDefinition.Bulwark,
                         FrontlineLabsClassDefinition.Striker,
                         FrontlineLabsRosterArm.None),
                 })
        {
            Assert.DoesNotContain(
                FrontlineLabsLegionRoster.MapId,
                definition.Map.Id,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                FrontlineLabsLegionRoster.ArmToken,
                definition.Rules.RulesetId,
                StringComparison.Ordinal);
            Assert.Equal(3, definition.Topology.UnitSlots.Count(slot =>
                slot.TeamId == 1));
            Assert.Equal(
                2,
                definition.Topology.InitialLives.Length);
        }

        // The measured generations keep their exact pad and anchor shape —
        // two companion anchors per team, a six-tile pad, twelve protected
        // tiles — which is what the pinned map fingerprints rest on.
        ActorMapDefinition classes =
            FrontlineLabsDefinition.CreateClassesExperiment(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker)
                .Map;
        Assert.Equal(6, classes.SpawnAnchors.Length);
        Assert.Equal(
            12,
            classes.TileTags
                .Single(tag => tag.Kind
                    == ActorMapTileTagDefinition.TileTagKind.SpawnProtected)
                .Tiles
                .Length);
        Assert.All(
            new[] { "team-0-home-pad", "team-1-home-pad" },
            regionId => Assert.Equal(
                6,
                classes.Regions
                    .Single(region => region.RegionId == regionId)
                    .Tiles
                    .Length));
    }

    /// <summary>
    /// The roster is a real arm on every pair — it changes what both teams
    /// field whatever chassis they are — so it moves the match fingerprint in
    /// every cell, and it needs a class pair to state its shape.
    /// </summary>
    [Fact]
    public void TheRosterIsARealArmOnEveryPairAndNeedsAClassPair()
    {
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                roster: FrontlineLabsRosterArm.Legion));
        // Two arms that each mint a map generation cannot share a cell.
        Assert.Throws<ArgumentException>(() =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                Keel,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker),
                sideObjective: FrontlineLabsSideObjectiveArm.Muster,
                roster: FrontlineLabsRosterArm.Legion));

        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            Assert.NotEqual(
                ActorContractFingerprint.ComputeMatch(
                    FullGame(zero, one, FrontlineLabsRosterArm.None)),
                ActorContractFingerprint.ComputeMatch(
                    FullGame(zero, one, FrontlineLabsRosterArm.Legion)));
        }
    }

    /// <summary>
    /// The roster authors the slot schedule FIVE SLOTS used to author, so the
    /// two variants whose only lever is that schedule write the unmodified
    /// skill's exact bytes here — and an arm that changes no bytes changes no
    /// identity. The rebuild-clock levers survive intact.
    /// </summary>
    [Fact]
    public void TheRosterSupersedesTheSlotScheduleButNotTheRebuildClock()
    {
        ActorResolvedMatchDefinition Cell(
            FrontlineLabsFiveSlotVariant variant) =>
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.None,
                (FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsClassDefinition.Striker),
                skills: FrontlineLabsSkillKit.FabricatorFiveSlots,
                fiveSlots: variant,
                roster: FrontlineLabsRosterArm.Legion);

        ActorResolvedMatchDefinition full =
            Cell(FrontlineLabsFiveSlotVariant.Full);
        foreach (FrontlineLabsFiveSlotVariant superseded in new[]
                 {
                     FrontlineLabsFiveSlotVariant.Trim,
                     FrontlineLabsFiveSlotVariant.Boom,
                 })
        {
            Assert.Equal(
                full.Rules.RulesetId,
                Cell(superseded).Rules.RulesetId);
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(full),
                ActorContractFingerprint.ComputeMatch(Cell(superseded)));
        }
        Assert.Equal(
            Cell(FrontlineLabsFiveSlotVariant.Drag).Rules.RulesetId,
            Cell(FrontlineLabsFiveSlotVariant.Moor).Rules.RulesetId);
        Assert.NotEqual(
            full.Rules.RulesetId,
            Cell(FrontlineLabsFiveSlotVariant.Wane).Rules.RulesetId);
        // Nine slots either way: the roster owns the count.
        Assert.All(
            new[]
            {
                FrontlineLabsFiveSlotVariant.Full,
                FrontlineLabsFiveSlotVariant.Trim,
                FrontlineLabsFiveSlotVariant.Wane,
            },
            variant => Assert.Equal(
                9,
                Cell(variant).Topology.UnitSlots.Count(slot =>
                    slot.TeamId == 0)));
        // COUNT without TEMPO: the late tranche keeps the skill's slower
        // rebuild profile where the skill is in the cell.
        Assert.All(
            Companions(full, 0).Skip(5),
            assignment => Assert.Equal(
                FrontlineLabsClassDefinition.Fabricator
                    .ExtraChildLifecycleProfileId,
                assignment.LifecycleProfileId));
        Assert.All(
            Companions(full, 0).Take(5),
            assignment => Assert.Equal(
                FrontlineLabsClassDefinition.Fabricator
                    .ChildLifecycleProfileId,
                assignment.LifecycleProfileId));
    }

    /// <summary>
    /// The identity budget. The roster re-mints the whole game — it changes
    /// what every other arm is priced against — so every canonical class pair
    /// has to spell the v1.1 game plus the roster inside 64 characters, which
    /// is exactly why the registered composites exist.
    /// </summary>
    [Fact]
    public void EveryLegionIdentityFitsTheCanonicalBudget()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bulwark-vs-bulwark"] = "barracks",
            ["bulwark-vs-fabricator"] = "cordon",
            ["bulwark-vs-striker"] = "garrison",
            ["fabricator-vs-fabricator"] = "cordon",
            ["fabricator-vs-striker"] = "garrison",
            ["striker-vs-striker"] = "garrison",
        };
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            string pair = $"{zero.Id}-vs-{one.Id}";
            string id = FullGame(zero, one, FrontlineLabsRosterArm.Legion)
                .Rules.RulesetId;
            Assert.True(id.Length <= 64, $"{id} is {id.Length}");
            Assert.Equal(
                $"frontline-labs-1-{pair}-{expected[pair]}-facing-locked",
                id);
        }

        // The candidate game without the two #187 arms carries the other
        // registered family, and a smaller cell still spells its factors.
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-keel-legion",
            FrontlineLabsDefinition.CreatePendulumExperiment(
                    Keel,
                    (FrontlineLabsClassDefinition.Bulwark,
                        FrontlineLabsClassDefinition.Striker),
                    roster: FrontlineLabsRosterArm.Legion)
                .Rules.RulesetId);
    }

    /// <summary>
    /// The re-mints of this window, pinned as strings: the old tokens named
    /// measured bytes and those bytes moved, so the old tokens must not appear
    /// on the new constants.
    /// </summary>
    [Fact]
    public void TheReMintedChannelAndEconomyTokensReplaceTheWaveEightOnes()
    {
        var expected = new Dictionary<string, (string Channel, string Full)>(
            StringComparer.Ordinal)
        {
            ["bulwark-vs-bulwark"] = ("pavise", "armoury"),
            ["bulwark-vs-fabricator"] = ("mine", "rampart"),
            ["bulwark-vs-striker"] = ("storm", "citadel"),
            ["fabricator-vs-fabricator"] = ("mine", "rampart"),
            ["fabricator-vs-striker"] = ("storm", "citadel"),
            ["striker-vs-striker"] = ("storm", "citadel"),
        };
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            string pair = $"{zero.Id}-vs-{one.Id}";
            string channel = Game(
                    zero,
                    one,
                    FrontlineLabsCaptureArm.Channel,
                    FrontlineLabsEconomyArm.None)
                .Rules.RulesetId;
            string full = Game(
                    zero,
                    one,
                    FrontlineLabsCaptureArm.Channel,
                    FrontlineLabsEconomyArm.Scrap)
                .Rules.RulesetId;
            Assert.Equal(
                $"frontline-labs-1-{pair}-{expected[pair].Channel}"
                + "-facing-locked",
                channel);
            Assert.Equal(
                $"frontline-labs-1-{pair}-{expected[pair].Full}"
                + "-facing-locked",
                full);
            Assert.True(full.Length <= 64, $"{full} is {full.Length}");
            foreach (string retired in new[]
                     {
                         "siege",
                         "sap",
                         "mantlet",
                         "forge",
                         "anvil",
                         "smelter",
                         "bastion",
                         "redoubt",
                         "smithy",
                     })
            {
                Assert.DoesNotContain(
                    $"-{retired}-",
                    $"{channel}-{full}-",
                    StringComparison.Ordinal);
            }
        }

        // The economy-only family re-mints too.
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-striker-foundry-facing-locked",
            Game(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker,
                    FrontlineLabsCaptureArm.Frozen,
                    FrontlineLabsEconomyArm.Scrap)
                .Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-fabricator-vs-fabricator-bellows-facing-locked",
            Game(
                    FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsCaptureArm.Frozen,
                    FrontlineLabsEconomyArm.Scrap)
                .Rules.RulesetId);
        Assert.Equal(
            "frontline-labs-1-bulwark-vs-bulwark-furnace-facing-locked",
            Game(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsCaptureArm.Frozen,
                    FrontlineLabsEconomyArm.Scrap)
                .Rules.RulesetId);
    }

    /// <summary>
    /// The whole legion contract resolves through the canonical mirror the
    /// admission validator runs, which is what proves the extra slots,
    /// anchors, and initial lives are legal contract data rather than a shape
    /// the engine merely tolerates.
    /// </summary>
    [Fact]
    public void TheLegionContractRoundTripsThroughTheCanonicalMirror()
    {
        foreach ((FrontlineLabsClassDefinition zero,
                  FrontlineLabsClassDefinition one) in
                 FrontlineLabsSkillArmTestFixture.CanonicalPairs())
        {
            ActorResolvedMatchDefinition legion = FullGame(
                zero,
                one,
                FrontlineLabsRosterArm.Legion);
            GenericActorCanonicalContractValidation validation =
                GenericActorCanonicalContractValidator.Validate(
                    ActorContractManifestSerializer.ToCanonicalJson(legion));
            Assert.Equal(legion.Rules.RulesetId, validation.RulesetId);
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(legion),
                validation.MatchContractFingerprint);
        }
    }

    private static ActorResolvedMatchDefinition Game(
        FrontlineLabsClassDefinition teamZero,
        FrontlineLabsClassDefinition teamOne,
        FrontlineLabsCaptureArm capture,
        FrontlineLabsEconomyArm economy)
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
            capture: capture,
            economy: economy);
    }

    private static ActorUnitSlotLifecycleAssignmentDefinition[] Companions(
        ActorResolvedMatchDefinition definition,
        int teamId) =>
        [
            .. definition.LifecycleAssignments
                .Where(assignment =>
                    assignment.TeamId == teamId && assignment.UnitId > 0)
                .OrderBy(assignment => assignment.UnitId),
        ];
}
