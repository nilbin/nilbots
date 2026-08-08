using SdkDirection = BotArena.Sdk.Direction;
using SdkPosition = BotArena.Sdk.Position;
using SdkShotPaths = BotArena.Sdk.ShotPaths;
using SdkShotProgram = BotArena.Sdk.ShotProgram;

namespace BotArena.Engine.Tests;

/// <summary>
/// Exact local-game checks for the canonical central-lane engagement. These
/// are not bot benchmarks: they establish what the public timing and private
/// path language can make strategically available before policy quality is
/// considered.
/// </summary>
public class FrontlineLabsDuelTheoryTests
{
    private enum LastMileClass
    {
        ForcedEarlyEvasion,
        UniversalLastResponse,
        PartialPrivateFork,
        FullThreeChoiceFork,
    }

    private sealed record LastMileAnalysis(
        LastMileClass Classification,
        ProjectileHeading Heading,
        int TargetDistance,
        Position Target,
        IReadOnlyDictionary<string, Position> Responses,
        IReadOnlyList<IReadOnlySet<string>> HitRows);

    private static readonly Position Shooter = new(8, 7);
    private static readonly Position Target = new(14, 7);
    private static readonly ProjectileHeading[] CardinalHeadings =
    [
        ProjectileHeading.North,
        ProjectileHeading.East,
        ProjectileHeading.South,
        ProjectileHeading.West,
    ];

    private static readonly IReadOnlyDictionary<string, Position>
        LastMomentResponses =
            new Dictionary<string, Position>(StringComparer.Ordinal)
            {
                ["hold"] = Target,
                ["north"] = Target.Offset(0, -1),
                ["south"] = Target.Offset(0, 1),
                ["east"] = Target.Offset(1, 0),
                ["west"] = Target.Offset(-1, 0),
            };

    [Fact]
    public void CurrentCentralLane_TerminatesTheRelevantBendsAtWalls()
    {
        IReadOnlyDictionary<string, IReadOnlyList<Position>> paths =
            Programs(bendAfterTiles: 4)
                .ToDictionary(
                    pair => pair.Key,
                    pair => Path(pair.Value),
                    StringComparer.Ordinal);

        Assert.Equal(8, paths["straight"].Count);
        Assert.Equal(new Position(12, 7), Assert.Single(
            paths["left"].Skip(3)));
        Assert.Equal(new Position(12, 7), Assert.Single(
            paths["right"].Skip(3)));

        IReadOnlyDictionary<string, IReadOnlyList<Position>> laterPaths =
            Programs(bendAfterTiles: 5)
                .ToDictionary(
                    pair => pair.Key,
                    pair => Path(pair.Value),
                    StringComparer.Ordinal);
        Assert.Equal(new Position(13, 7), laterPaths["left"][4]);
        Assert.Equal(new Position(13, 7), laterPaths["right"][4]);
        Assert.Equal(5, laterPaths["left"].Count);
        Assert.Equal(5, laterPaths["right"].Count);

        // The map has symmetric wall clusters immediately above and below
        // x=13. Strict diagonal corners therefore consume both a four-tile
        // and a five-tile bend before either can enter the target's natural
        // one-step lateral destination.
    }

    [Fact]
    public void OpenFiveTileBend_CreatesAThreeWayPrivateLastMileFork()
    {
        IReadOnlyDictionary<string, IReadOnlyList<Position>> paths =
            Programs(bendAfterTiles: 5)
                .ToDictionary(
                    pair => pair.Key,
                    pair => OpenPath(pair.Value),
                    StringComparer.Ordinal);

        // All three programs remain observationally identical before the
        // defender commits the last-moment response.
        Assert.All(
            paths.Values,
            path => Assert.Equal(new Position(13, 7), path[4]));

        var hitMatrix = paths.ToDictionary(
            pair => pair.Key,
            pair => LastMomentResponses.ToDictionary(
                response => response.Key,
                response => HitsOnNextAdvance(
                    pair.Value,
                    response.Value),
                StringComparer.Ordinal),
            StringComparer.Ordinal);

        Assert.Equal(
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hold"] = true,
                ["north"] = false,
                ["south"] = false,
                ["east"] = true,
                ["west"] = true,
            },
            hitMatrix["straight"]);
        Assert.Equal(
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hold"] = false,
                ["north"] = true,
                ["south"] = false,
                ["east"] = false,
                ["west"] = true,
            },
            hitMatrix["left"]);
        Assert.Equal(
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hold"] = false,
                ["north"] = false,
                ["south"] = true,
                ["east"] = false,
                ["west"] = true,
            },
            hitMatrix["right"]);

        // No shot is unavoidable, and no single last-moment response answers
        // every private shot. Removing dominated west/east variants leaves
        // straight/left/right versus hold/north/south: the 3x3 identity
        // game, whose symmetric zero-sum equilibrium mixes each at 1/3.
        Assert.All(
            hitMatrix.Values,
            row => Assert.Contains(row.Values, hit => !hit));
        Assert.All(
            LastMomentResponses.Keys,
            response => Assert.Contains(
                hitMatrix.Values,
                row => row[response]));
    }

    [Fact]
    public void CurrentObjectiveChamber_HasThePrivateForkAtFourTileRange()
    {
        var target = new Position(12, 7);
        IReadOnlyDictionary<string, Position> responses =
            Responses(target);
        IReadOnlyDictionary<string, IReadOnlyList<Position>> paths =
            Programs(bendAfterTiles: 3)
                .ToDictionary(
                    pair => pair.Key,
                    pair => Path(pair.Value),
                    StringComparer.Ordinal);
        const int publicPathIndex = 2;

        Assert.All(
            paths.Values,
            path => Assert.Equal(
                new Position(11, 7),
                path[publicPathIndex]));

        var hitMatrix = paths.ToDictionary(
            pair => pair.Key,
            pair => responses.ToDictionary(
                response => response.Key,
                response => HitsOnNextAdvance(
                    pair.Value,
                    response.Value,
                    publicPathIndex),
                StringComparer.Ordinal),
            StringComparer.Ordinal);

        Assert.True(hitMatrix["straight"]["hold"]);
        Assert.True(hitMatrix["left"]["north"]);
        Assert.True(hitMatrix["right"]["south"]);
        Assert.All(
            responses.Keys,
            response => Assert.Contains(
                hitMatrix.Values,
                row => row[response]));
        Assert.All(
            hitMatrix.Values,
            row => Assert.Contains(row.Values, hit => !hit));

        ActorMapRegionDefinition objective =
            FrontlineLabsDefinition
                .CreateOneBendShotsExperiment()
                .Map
                .Regions
                .Single(region =>
                    region.RegionId == "frontline-position-2");
        Assert.Contains(responses["hold"], objective.Tiles);
        Assert.Contains(responses["south"], objective.Tiles);
        Assert.DoesNotContain(responses["north"], objective.Tiles);
    }

    [Fact]
    public void CurrentMap_ObjectiveLastMilesAreMostlyPredictionForks()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorMapDefinition map = definition.Map;
        ActorAttackProfileDefinition profile =
            definition.Rules.AttackProfiles.Single(candidate =>
                candidate.Id == "mobile-bolt");

        LastMileAnalysis[] analyses =
        [
            .. from y in Enumerable.Range(0, map.Height)
            from x in Enumerable.Range(0, map.Width)
            let shooter = new Position(x, y)
            where !map.IsWall(shooter)
            from heading in CardinalHeadings
            from distance in Enumerable.Range(2, 4)
            let analysis = AnalyzeLastMile(
                map,
                profile,
                shooter,
                heading,
                distance)
            where analysis is not null
            select analysis,
        ];

        Assert.Equal(1750, analyses.Length);
        Assert.Equal(
            72,
            analyses.Count(analysis =>
                analysis.Classification
                    == LastMileClass.ForcedEarlyEvasion));
        Assert.Equal(
            836,
            analyses.Count(analysis =>
                analysis.Classification
                    == LastMileClass.UniversalLastResponse));
        Assert.Equal(
            354,
            analyses.Count(analysis =>
                analysis.Classification
                    == LastMileClass.PartialPrivateFork));
        Assert.Equal(
            488,
            analyses.Count(analysis =>
                analysis.Classification
                    == LastMileClass.FullThreeChoiceFork));
        Assert.All(
            analyses.Where(analysis =>
                analysis.Classification
                    == LastMileClass.UniversalLastResponse),
            analysis => Assert.Contains(
                analysis.HitRows,
                row => row.Contains("hold")));

        ActorMapRegionDefinition[] objectives =
        [
            .. map.Regions.Where(region =>
                region.RegionId.StartsWith(
                    "frontline-position-",
                    StringComparison.Ordinal)),
        ];
        LastMileAnalysis[] objectiveCentred =
        [
            .. analyses.Where(analysis =>
                objectives.Any(objective =>
                    objective.Tiles.Contains(analysis.Target))),
        ];

        Assert.Equal(163, objectiveCentred.Length);
        Assert.DoesNotContain(
            objectiveCentred,
            analysis =>
                analysis.Classification
                    == LastMileClass.ForcedEarlyEvasion);
        Assert.Equal(
            82,
            objectiveCentred.Count(analysis =>
                analysis.Classification
                    == LastMileClass.UniversalLastResponse));
        Assert.Equal(
            38,
            objectiveCentred.Count(analysis =>
                analysis.Classification
                    == LastMileClass.PartialPrivateFork));
        Assert.Equal(
            43,
            objectiveCentred.Count(analysis =>
                analysis.Classification
                    == LastMileClass.FullThreeChoiceFork));
        Assert.Equal(
            132,
            objectiveCentred.Count(analysis =>
                HasSafeStayAndLeaveTradeoff(
                    analysis,
                    objectives.Single(objective =>
                        objective.Tiles.Contains(analysis.Target)))));
    }

    [Fact]
    public void CandidateVerticalObjectiveStrips_ImprovePrimaryAxisCosts()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorMapDefinition map = definition.Map;
        ActorAttackProfileDefinition profile =
            definition.Rules.AttackProfiles.Single(candidate =>
                candidate.Id == "mobile-bolt");
        LastMileAnalysis[] primaryAxisAnalyses =
        [
            .. from y in Enumerable.Range(0, map.Height)
            from x in Enumerable.Range(0, map.Width)
            let shooter = new Position(x, y)
            where !map.IsWall(shooter)
            from heading in new[]
            {
                ProjectileHeading.East,
                ProjectileHeading.West,
            }
            from distance in Enumerable.Range(2, 4)
            let analysis = AnalyzeLastMile(
                map,
                profile,
                shooter,
                heading,
                distance)
            where analysis is not null
            select analysis,
        ];
        IReadOnlyList<IReadOnlySet<Position>> currentObjectives =
        [
            .. map.Regions
                .Where(region => region.RegionId.StartsWith(
                    "frontline-position-",
                    StringComparison.Ordinal))
                .Select(region =>
                    (IReadOnlySet<Position>)region.Tiles.ToHashSet()),
        ];
        IReadOnlyList<IReadOnlySet<Position>> verticalStrips =
        [
            .. FrontlineLabsDefinition
                .CreateOneBendShotsExperiment(
                    FrontlineLabsDuelMapArm.ThinFronts)
                .Map
                .Regions
                .Where(region => region.RegionId.StartsWith(
                    "frontline-position-",
                    StringComparison.Ordinal))
                .Select(region =>
                    (IReadOnlySet<Position>)region.Tiles.ToHashSet()),
        ];
        Assert.All(
            verticalStrips.SelectMany(strip => strip),
            tile => Assert.False(map.IsWall(tile)));

        LastMileAnalysis[] current =
        [
            .. primaryAxisAnalyses.Where(analysis =>
                currentObjectives.Any(objective =>
                    objective.Contains(analysis.Target))),
        ];
        LastMileAnalysis[] candidate =
        [
            .. primaryAxisAnalyses.Where(analysis =>
                verticalStrips.Any(objective =>
                    objective.Contains(analysis.Target))),
        ];

        Assert.Equal(48, current.Length);
        Assert.Equal(
            26,
            current.Count(analysis =>
                analysis.Classification
                    == LastMileClass.FullThreeChoiceFork));
        Assert.Equal(
            16,
            current.Count(analysis =>
                UniversalResponseCanStay(
                    analysis,
                    currentObjectives.Single(objective =>
                        objective.Contains(analysis.Target)))));

        Assert.Equal(30, candidate.Length);
        Assert.Equal(
            20,
            candidate.Count(analysis =>
                analysis.Classification
                    == LastMileClass.FullThreeChoiceFork));
        Assert.Equal(
            4,
            candidate.Count(analysis =>
                UniversalResponseCanStay(
                    analysis,
                    verticalStrips.Single(objective =>
                        objective.Contains(analysis.Target)))));

        Assert.True(20d / 30d > 26d / 48d);
        Assert.True(4d / 30d < 16d / 48d);
    }

    [Fact]
    public void CurrentEntryChoke_OpensFromRetreatIntoPrediction()
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorMapDefinition map = definition.Map;
        ActorAttackProfileDefinition profile =
            definition.Rules.AttackProfiles.Single(candidate =>
                candidate.Id == "mobile-bolt");
        var eastShooter = new Position(14, 7);

        LastMileAnalysis rangeFive = Assert.IsType<LastMileAnalysis>(
            AnalyzeLastMile(
                map,
                profile,
                eastShooter,
                ProjectileHeading.West,
                targetDistance: 5));
        Assert.Equal(new Position(9, 7), rangeFive.Target);
        Assert.Equal(
            LastMileClass.UniversalLastResponse,
            rangeFive.Classification);
        Assert.Equal(
            ["forward"],
            UniversalResponses(rangeFive));
        Assert.True(map.IsWall(rangeFive.Target.Offset(0, -1)));
        Assert.True(map.IsWall(rangeFive.Target.Offset(0, 1)));

        LastMileAnalysis rangeFour = Assert.IsType<LastMileAnalysis>(
            AnalyzeLastMile(
                map,
                profile,
                eastShooter,
                ProjectileHeading.West,
                targetDistance: 4));
        Assert.Equal(new Position(10, 7), rangeFour.Target);
        Assert.Equal(
            LastMileClass.FullThreeChoiceFork,
            rangeFour.Classification);
        ActorMapRegionDefinition centre =
            map.Regions.Single(region =>
                region.RegionId == "frontline-position-2");
        Assert.Contains(rangeFour.Target, centre.Tiles);

        // At x=9, waiting for the final public projectile state leaves only
        // a retreat to x=8. Advancing one tile earlier reaches x=10, where
        // straight/left/right form a genuine private fork on the objective.
        // This is an initiative-timing probe for T4+ policies, not a forced
        // hit and not a reason to add more shot programs.
    }

    [Fact]
    public void PerpendicularCrossfire_CoversWhatEachDuelShotCannot()
    {
        var target = new Position(12, 7);
        IReadOnlyList<Position> horizontal = OpenPath(
            new Position(8, 7),
            SdkDirection.East,
            ShotProgram.Straight);
        IReadOnlyList<Position> vertical = OpenPath(
            new Position(12, 3),
            SdkDirection.South,
            ShotProgram.Straight);
        const int publicPathIndex = 2;
        IReadOnlyDictionary<string, Position> responses =
            Responses(target);
        Position[] currentProjectileTiles =
        [
            horizontal[publicPathIndex],
            vertical[publicPathIndex],
        ];
        var relevantResponses = responses
            .Where(pair =>
                !currentProjectileTiles.Contains(pair.Value))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
        var horizontalHits = responses
            .Where(pair =>
                relevantResponses.Contains(pair.Key)
                && HitsOnNextAdvance(
                    horizontal,
                    pair.Value,
                    publicPathIndex))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
        var verticalHits = responses
            .Where(pair =>
                relevantResponses.Contains(pair.Key)
                && HitsOnNextAdvance(
                    vertical,
                    pair.Value,
                    publicPathIndex))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.False(horizontalHits.SetEquals(relevantResponses));
        Assert.False(verticalHits.SetEquals(relevantResponses));
        Assert.True(
            horizontalHits
                .Concat(verticalHits)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(relevantResponses));

        // One range-four straight shot leaves a lateral response. Two
        // synchronized perpendicular shots cover Hold and both remaining
        // moves after movement into either current projectile is removed.
        // The defender must disrupt positioning or cadence earlier. That is
        // a readable C3 setup cost, not a more complicated projectile verb.
    }

    private static IReadOnlyDictionary<string, ShotProgram> Programs(
        int bendAfterTiles) =>
        new Dictionary<string, ShotProgram>(StringComparer.Ordinal)
        {
            ["straight"] = ShotProgram.Straight,
            ["left"] = new ShotProgram(0, -1, bendAfterTiles, 1, 1),
            ["right"] = new ShotProgram(0, 1, bendAfterTiles, 1, 1),
        };

    private static LastMileAnalysis? AnalyzeLastMile(
        ActorMapDefinition map,
        ActorAttackProfileDefinition profile,
        Position shooter,
        ProjectileHeading heading,
        int targetDistance)
    {
        var (dx, dy) = heading.Vector();
        Position target = shooter.Offset(
            dx * targetDistance,
            dy * targetDistance);
        IReadOnlyList<Position> straight =
            GenericActorProjectilePath.Trace(
                map,
                shooter,
                heading,
                profile,
                ShotProgram.Straight);
        if (map.IsWall(target)
            || straight.Count < targetDistance
            || straight[targetDistance - 1] != target)
        {
            return null;
        }

        int bendAfterTiles = targetDistance - 1;
        IReadOnlyList<IReadOnlyList<Position>> paths =
        [
            straight,
            GenericActorProjectilePath.Trace(
                map,
                shooter,
                heading,
                profile,
                new ShotProgram(0, -1, bendAfterTiles, 1, 1)),
            GenericActorProjectilePath.Trace(
                map,
                shooter,
                heading,
                profile,
                new ShotProgram(0, 1, bendAfterTiles, 1, 1)),
        ];
        const int tilesPerAdvance = 2;
        int publicPathIndex =
            (targetDistance - 2) / tilesPerAdvance
            * tilesPerAdvance;
        Assert.All(
            paths,
            path => Assert.Equal(
                straight.Take(publicPathIndex + 1),
                path.Take(publicPathIndex + 1)));

        IReadOnlyDictionary<string, Position> responses =
            RelativeResponses(target, heading)
                .Where(pair => !map.IsWall(pair.Value))
                .Where(pair =>
                    pair.Value != straight[publicPathIndex])
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);
        IReadOnlyList<IReadOnlySet<string>> hitRows =
        [
            .. paths.Select(path =>
                (IReadOnlySet<string>)responses
                    .Where(pair => HitsOnNextAdvance(
                        path,
                        pair.Value,
                        publicPathIndex))
                    .Select(pair => pair.Key)
                    .ToHashSet(StringComparer.Ordinal)),
        ];
        var allResponses = responses.Keys.ToHashSet(
            StringComparer.Ordinal);
        var hitByAny = hitRows
            .SelectMany(row => row)
            .ToHashSet(StringComparer.Ordinal);

        LastMileClass classification;
        if (hitRows.Any(row => row.SetEquals(allResponses)))
        {
            classification = LastMileClass.ForcedEarlyEvasion;
        }
        else if (allResponses.Except(hitByAny).Any())
        {
            classification = LastMileClass.UniversalLastResponse;
        }
        else
        {
            int uniquelyUsefulShots = hitRows
                .Select((row, index) => row.Except(
                    hitRows
                        .Where((_, otherIndex) => otherIndex != index)
                        .SelectMany(other => other)))
                .Count(uniqueHits => uniqueHits.Any());
            classification = uniquelyUsefulShots == 3
                ? LastMileClass.FullThreeChoiceFork
                : LastMileClass.PartialPrivateFork;
        }

        return new LastMileAnalysis(
            classification,
            heading,
            targetDistance,
            target,
            responses,
            hitRows);
    }

    private static bool UniversalResponseCanStay(
        LastMileAnalysis analysis,
        IReadOnlySet<Position> objective)
        => UniversalResponses(analysis)
            .Any(response =>
                objective.Contains(analysis.Responses[response]));

    private static IReadOnlySet<string> UniversalResponses(
        LastMileAnalysis analysis)
    {
        var hitByAny = analysis.HitRows
            .SelectMany(row => row)
            .ToHashSet(StringComparer.Ordinal);
        return analysis.Responses.Keys
            .Where(response => !hitByAny.Contains(response))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool HasSafeStayAndLeaveTradeoff(
        LastMileAnalysis analysis,
        ActorMapRegionDefinition objective) =>
        analysis.HitRows.Any(hitRow =>
        {
            string[] safeResponses =
            [
                .. analysis.Responses.Keys.Except(hitRow),
            ];
            return safeResponses.Any(response =>
                    objective.Tiles.Contains(
                        analysis.Responses[response]))
                && safeResponses.Any(response =>
                    !objective.Tiles.Contains(
                        analysis.Responses[response]));
        });

    private static IReadOnlyDictionary<string, Position>
        RelativeResponses(
            Position target,
            ProjectileHeading heading)
    {
        var (forwardX, forwardY) = heading.Vector();
        var (rightX, rightY) = heading.Turned(2).Vector();
        var (backX, backY) = heading.Turned(4).Vector();
        var (leftX, leftY) = heading.Turned(-2).Vector();
        return new Dictionary<string, Position>(StringComparer.Ordinal)
        {
            ["hold"] = target,
            ["forward"] = target.Offset(forwardX, forwardY),
            ["right"] = target.Offset(rightX, rightY),
            ["back"] = target.Offset(backX, backY),
            ["left"] = target.Offset(leftX, leftY),
        };
    }

    private static IReadOnlyList<Position> Path(ShotProgram program)
    {
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreateOneBendShotsExperiment();
        ActorAttackProfileDefinition profile =
            definition.Rules.AttackProfiles.Single(candidate =>
                candidate.Id == "mobile-bolt");
        return GenericActorProjectilePath.Trace(
            definition.Map,
            Shooter,
            ProjectileHeading.East,
            profile,
            program);
    }

    private static IReadOnlyList<Position> OpenPath(ShotProgram program) =>
        OpenPath(Shooter, SdkDirection.East, program);

    private static IReadOnlyList<Position> OpenPath(
        Position shooter,
        SdkDirection heading,
        ShotProgram program) =>
        SdkShotPaths.Preview(
                new SdkPosition(shooter.X, shooter.Y),
                heading,
                new SdkShotProgram(
                    program.InitialAimOffset,
                    program.BendDirection,
                    program.BendAfterTiles,
                    program.BendEveryTiles,
                    program.BendCount),
                maxPathTiles: 8)
            .Select(position => new Position(position.X, position.Y))
            .ToArray();

    private static bool HitsOnNextAdvance(
        IReadOnlyList<Position> path,
        Position destination,
        int publicPathIndex = 4)
    {
        const int tilesPerAdvance = 2;
        return destination == path[publicPathIndex]
            || path
                .Skip(publicPathIndex + 1)
                .Take(tilesPerAdvance)
                .Contains(destination);
    }

    private static IReadOnlyDictionary<string, Position> Responses(
        Position target) =>
        new Dictionary<string, Position>(StringComparer.Ordinal)
        {
            ["hold"] = target,
            ["north"] = target.Offset(0, -1),
            ["south"] = target.Offset(0, 1),
            ["east"] = target.Offset(1, 0),
            ["west"] = target.Offset(-1, 0),
        };
}
