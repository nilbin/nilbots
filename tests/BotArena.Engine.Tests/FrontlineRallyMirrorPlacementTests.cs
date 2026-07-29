using System.Collections.Immutable;
using BotArena.ActorContracts;

namespace BotArena.Engine.Tests;

/// <summary>
/// The load-bearing property of the team-advance rally placement: it commutes
/// with the map reflection that swaps the teams. Reflect the world — walls,
/// spawn anchors, objective regions, deployment — and every arrival reflects
/// exactly, for either team, at every objective position, with or without
/// occupancy. That is what the historical map-absolute order could not do: a
/// single row-then-column scan over two mirror-image rally regions hands the
/// two teams non-mirrored tiles, which an identical-bot facing-locked mirror
/// probe measured as a 4/4 side sweep on <c>--pendulum forward-rally</c>.
///
/// The historical value stays defined and resolvable so archived replays keep
/// verifying; the tests below assert both that it still resolves and that it
/// still fails this property, so the two placements can never be confused.
/// </summary>
public sealed class FrontlineRallyMirrorPlacementTests
{
    private const ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
        TeamAdvanceOrder = ActorLifecycleDefinition
            .ActorAutomaticReturnPlacementKind
            .OwnSideChainAdjacentObjectiveTileInTeamAdvanceOrderThenAssignedSpawn;

    private const ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
        AbsoluteOrder = ActorLifecycleDefinition
            .ActorAutomaticReturnPlacementKind
            .OwnSideChainAdjacentObjectiveTileThenAssignedSpawn;

    /// <summary>The two home spawns, which reflect onto each other.</summary>
    private static readonly Position TeamZeroSpawn = new(2, 7);
    private static readonly Position TeamOneSpawn = new(20, 7);

    [Fact]
    public void ThePlacementCommutesWithTheReflectionThatSwapsTheTeams()
    {
        ActorResolvedMatchDefinition world = Arm(TeamAdvanceOrder);
        ActorResolvedMatchDefinition reflected = Reflect(world);

        Assert.Empty(Mismatches(world, reflected));
    }

    /// <summary>
    /// Same sweep, historical placement: it does not commute, and the very
    /// first disagreement is the shipped map's centre fight — the tile a
    /// reinforcing team lands on differs by one step along the advance axis
    /// depending on which side it is playing.
    /// </summary>
    [Fact]
    public void TheHistoricalAbsoluteOrderDoesNotCommuteWhichWasTheBias()
    {
        ActorResolvedMatchDefinition world = Arm(AbsoluteOrder);
        ActorResolvedMatchDefinition reflected = Reflect(world);

        Assert.NotEmpty(Mismatches(world, reflected));
        // Team 1's own side of the centre is frontline-position-3, the exact
        // reflection of team 0's frontline-position-1 — but the absolute scan
        // gives team 0 the rear tile of its region and team 1 the forward one.
        Assert.Equal(
            new Position(6, 5),
            Resolve(world, teamId: 0, TeamZeroSpawn, 2, []));
        Assert.Equal(
            new Position(15, 5),
            Resolve(world, teamId: 1, TeamOneSpawn, 2, []));
        Assert.NotEqual(
            new Position(16, 5),
            Resolve(world, teamId: 1, TeamOneSpawn, 2, []));
    }

    /// <summary>
    /// The shipped arm's answer, stated as tiles: each team takes the
    /// rear-most tile of its own-side region, and the two are reflections.
    /// </summary>
    [Fact]
    public void EachTeamTakesTheRearTileOfItsOwnSideRegion()
    {
        ActorResolvedMatchDefinition arm =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ForwardRally);

        Assert.Equal(
            TeamAdvanceOrder,
            arm.Rules.Lifecycle.AutomaticReturnPlacement);
        Assert.Equal(
            new Position(6, 5),
            Resolve(arm, teamId: 0, TeamZeroSpawn, 2, []));
        Assert.Equal(
            Mirror(arm, new Position(6, 5)),
            Resolve(arm, teamId: 1, TeamOneSpawn, 2, []));
    }

    /// <summary>
    /// The order is a total function of the contract, not of enumeration: the
    /// same inputs resolve to the same tile every time, and a blocked set
    /// built in a different order resolves identically.
    /// </summary>
    [Fact]
    public void ThePlacementIsDeterministicAndOrderIndependent()
    {
        ActorResolvedMatchDefinition arm = Arm(TeamAdvanceOrder);
        Position[] blocked =
        [
            new(16, 5),
            new(16, 6),
        ];

        Position first = Resolve(arm, 1, TeamOneSpawn, 2, blocked);
        Assert.Equal(new Position(15, 5), first);
        Assert.Equal(first, Resolve(arm, 1, TeamOneSpawn, 2, blocked));
        Assert.Equal(
            first,
            Resolve(arm, 1, TeamOneSpawn, 2, [.. blocked.Reverse()]));
    }

    /// <summary>
    /// Occupancy walks the same team-relative order, so a crowded rally
    /// region fills from each team's own rear forward and only then falls
    /// back to the permanently reserved assigned spawn.
    /// </summary>
    [Fact]
    public void OccupancyFallsThroughInTheTeamsOwnAdvanceOrder()
    {
        ActorResolvedMatchDefinition arm = Arm(TeamAdvanceOrder);
        Position[] region = [new(15, 5), new(16, 5), new(15, 6), new(16, 6)];
        var blocked = new List<Position>();
        var taken = new List<Position>();

        for (int index = 0; index < region.Length; index++)
        {
            Position tile = Resolve(arm, 1, TeamOneSpawn, 2, blocked);
            taken.Add(tile);
            blocked.Add(tile);
        }

        Assert.Equal(
            [new(16, 5), new(16, 6), new(15, 5), new(15, 6)],
            taken);
        // Region exhausted: the reserved assigned spawn is the last answer.
        Assert.Equal(TeamOneSpawn, Resolve(arm, 1, TeamOneSpawn, 2, blocked));
        // And team 0's sequence is the exact reflection of team 1's.
        var mirrored = new List<Position>();
        var mirrorBlocked = new List<Position>();
        for (int index = 0; index < region.Length; index++)
        {
            Position tile = Resolve(arm, 0, TeamZeroSpawn, 2, mirrorBlocked);
            mirrored.Add(tile);
            mirrorBlocked.Add(tile);
        }

        Assert.Equal(
            [.. taken.Select(tile => Mirror(arm, tile))],
            mirrored);
    }

    /// <summary>
    /// Both placements are canonical values of the same contract field: the
    /// writer emits each one's own ID and the SDK canonical reader — the same
    /// parser the admission validator runs — accepts both and reproduces the
    /// exact match fingerprint.
    /// </summary>
    [Fact]
    public void BothPlacementsRoundTripThroughTheCanonicalMirror()
    {
        foreach ((ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
            Placement, string Token) entry in new[]
            {
                (TeamAdvanceOrder,
                    "own-side-chain-adjacent-objective-tile-in-team-advance-"
                    + "order-then-assigned-spawn"),
                (AbsoluteOrder,
                    "own-side-chain-adjacent-objective-tile-then-assigned-"
                    + "spawn"),
            })
        {
            ActorResolvedMatchDefinition arm = Arm(entry.Placement);
            string canonical =
                ActorContractManifestSerializer.ToCanonicalJson(arm);

            Assert.Contains(
                $"\"automaticReturnPlacement\":\"{entry.Token}\"",
                canonical,
                StringComparison.Ordinal);
            GenericActorCanonicalContractValidation validation =
                GenericActorCanonicalContractValidator.Validate(canonical);
            Assert.Equal(arm.Rules.RulesetId, validation.RulesetId);
            Assert.Equal(
                ActorContractFingerprint.ComputeMatch(arm),
                validation.MatchContractFingerprint);
        }
    }

    /// <summary>
    /// The two placements are different content, so every rally-carrying arm
    /// fingerprints differently under them while the arm identity — the
    /// registered token — is unchanged. Arms without rally are untouched.
    /// </summary>
    [Fact]
    public void RallyArmsFingerprintDistinctlyFromTheHistoricalPlacement()
    {
        foreach (FrontlineLabsPendulumArm rally in new[]
            {
                FrontlineLabsPendulumArm.ForwardRally,
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally,
                FrontlineLabsPendulumArm.StickyFrontline
                    | FrontlineLabsPendulumArm.ForwardRally
                    | FrontlineLabsPendulumArm.ContestMajority,
            })
        {
            ActorResolvedMatchDefinition arm =
                FrontlineLabsDefinition.CreatePendulumExperiment(rally);
            ActorResolvedMatchDefinition historical = WithPlacement(
                arm,
                AbsoluteOrder);

            Assert.Equal(TeamAdvanceOrder,
                arm.Rules.Lifecycle.AutomaticReturnPlacement);
            Assert.Equal(arm.Rules.RulesetId, historical.Rules.RulesetId);
            Assert.NotEqual(
                ActorContractFingerprint.ComputeRules(historical.Rules),
                ActorContractFingerprint.ComputeRules(arm.Rules));
            Assert.NotEqual(
                ActorContractFingerprint.ComputeMatch(historical),
                ActorContractFingerprint.ComputeMatch(arm));
        }

        // A pendulum arm that carries no rally keeps the assigned-spawn
        // placement, so its bytes cannot have moved.
        ActorResolvedMatchDefinition majority =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ContestMajority);
        Assert.Equal(
            ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind
                .AssignedSpawnPermanentlyReservedForSlotAgainstOtherActorsAndLifecycleClaims,
            majority.Rules.Lifecycle.AutomaticReturnPlacement);
    }

    /// <summary>
    /// Every placement the two worlds produce, paired: the reflected world's
    /// answer against the reflection of the original world's answer, for both
    /// teams, every objective position, and a family of blocked sets that
    /// includes each rally tile on its own.
    /// </summary>
    private static List<string> Mismatches(
        ActorResolvedMatchDefinition world,
        ActorResolvedMatchDefinition reflected)
    {
        var mismatches = new List<string>();
        var binding =
            (FrontlineActorModeMapBindingDefinition)world.ModeMapBinding;
        Position[] candidates =
        [
            .. world.Map.Regions
                .Where(region =>
                    binding.OrderedObjectiveRegionIds.Contains(
                        region.RegionId,
                        StringComparer.Ordinal))
                .SelectMany(region => region.Tiles),
        ];

        foreach (int teamId in new[] { 0, 1 })
        {
            Position spawn = teamId == 0 ? TeamZeroSpawn : TeamOneSpawn;
            for (int index = 0;
                index < binding.OrderedObjectiveRegionIds.Length;
                index++)
            {
                foreach (Position[] blocked in BlockedSets(candidates))
                {
                    Position expected = Mirror(
                        world,
                        Resolve(world, teamId, spawn, index, blocked));
                    Position actual = Resolve(
                        reflected,
                        teamId,
                        Mirror(world, spawn),
                        index,
                        [.. blocked.Select(tile => Mirror(world, tile))]);
                    if (expected != actual)
                    {
                        mismatches.Add(
                            $"team {teamId} at position {index} with "
                            + $"{blocked.Length} blocked: expected {expected}, "
                            + $"reflected world gave {actual}");
                    }
                }
            }
        }
        return mismatches;
    }

    private static IEnumerable<Position[]> BlockedSets(Position[] candidates)
    {
        yield return [];
        foreach (Position candidate in candidates)
            yield return [candidate];
        yield return candidates;
        yield return [.. candidates.Where(tile => tile.Y % 2 == 0)];
    }

    private static Position Resolve(
        ActorResolvedMatchDefinition definition,
        int teamId,
        Position assignedSpawn,
        int activePositionIndex,
        IEnumerable<Position> blocked) =>
        FrontlineForwardRallyPlacement.Resolve(
            definition,
            teamId,
            assignedSpawn,
            activePositionIndex,
            blocked.ToImmutableHashSet());

    /// <summary>The shipped forward-rally arm under a chosen placement.</summary>
    private static ActorResolvedMatchDefinition Arm(
        ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind placement) =>
        WithPlacement(
            FrontlineLabsDefinition.CreatePendulumExperiment(
                FrontlineLabsPendulumArm.ForwardRally),
            placement);

    private static Position Mirror(
        ActorResolvedMatchDefinition definition,
        Position position) =>
        new(definition.Map.Width - 1 - position.X, position.Y);

    /// <summary>
    /// The same match reflected across the map's vertical axis. Team IDs are
    /// deliberately NOT swapped: each team keeps its identity and inherits the
    /// mirrored world, which is exactly the substitution a placement derived
    /// from team ID rather than from geometry would fail.
    /// </summary>
    private static ActorResolvedMatchDefinition Reflect(
        ActorResolvedMatchDefinition source)
    {
        int width = source.Map.Width;
        Position Flip(Position position) =>
            new(width - 1 - position.X, position.Y);
        InitialSpawnDefinition FlipSpawn(InitialSpawnDefinition spawn) =>
            new(
                spawn.SpawnId,
                Flip(spawn.Position),
                spawn.Facing switch
                {
                    Direction.East => Direction.West,
                    Direction.West => Direction.East,
                    Direction other => other,
                });

        var map = new ActorMapDefinition(
            source.Map.Id,
            source.Map.Version,
            [
                .. source.Map.TileRows.Select(row =>
                    new string([.. row.Reverse()])),
            ],
            [
                .. source.Map.SpawnAnchors.Select(anchor =>
                    anchor with { Spawn = FlipSpawn(anchor.Spawn) }),
            ],
            [
                .. source.Map.Regions.Select(region =>
                    region with { Tiles = [.. region.Tiles.Select(Flip)] }),
            ],
            [
                .. source.Map.TileTags.Select(tag =>
                    tag with { Tiles = [.. tag.Tiles.Select(Flip)] }),
            ]);
        var deployment = new InitialDeploymentDefinition(
            [.. source.InitialDeployment.Spawns.Select(FlipSpawn)],
            source.InitialDeployment.Lives);
        return new ActorResolvedMatchDefinition(
            source.Rules,
            map,
            source.Format,
            source.Topology,
            deployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding);
    }

    /// <summary>
    /// The same contract with one lifecycle field moved, so the two
    /// placements can be compared on identical rules.
    /// </summary>
    private static ActorResolvedMatchDefinition WithPlacement(
        ActorResolvedMatchDefinition source,
        ActorLifecycleDefinition.ActorAutomaticReturnPlacementKind placement) =>
        new(
            new ActorRulesDefinition(
                source.Rules.RulesetId,
                source.Rules.Limits,
                source.Rules.SeedMechanics,
                source.Rules.GameMode,
                new ActorLifecycleDefinition(
                    source.Rules.Lifecycle.Profiles,
                    placement),
                source.Rules.Forms,
                source.Rules.MovementProfiles,
                source.Rules.VisionProfiles,
                source.Rules.AttackProfiles,
                source.Rules.Actions,
                source.Rules.FabricationTransitions,
                source.Rules.SameLifeTransitions,
                source.Rules.ReplicationTransitions,
                source.Rules.TeamPerception,
                source.Rules.Collisions,
                source.Rules.TickResolution),
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding);
}
