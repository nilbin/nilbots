using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class ProgrammedProjectileTests
{
    private static readonly GameRules ArcRules = GameRules.V0_1 with
    {
        RulesVersion = "test-programmed-arcs",
        MaxTicks = 8,
        ShotRange = 8,
        ProjectileTicksPerTile = 1,
        ProjectileTilesPerAdvance = 2,
        AllowProgrammedShots = true,
    };

    private static ArenaMap OpenArcRoom() => ArenaMap.Create("test-arc-open", [
        "###############",
        "#.............#",
        "#.............#",
        "#.............#",
        "#.............#",
        "#.............#",
        "#.............#",
        "#.............#",
        "#.............#",
        "#.............#",
        "#.............#",
        "#.............#",
        "###############",
    ], [new Spawn(4, 6, Direction.East), new Spawn(13, 6, Direction.West)]);

    [Fact]
    public void PathCatalogue_MatchesTheTheoryLabs125DistinctOpenPaths()
    {
        var programs = new List<ShotProgram>();
        foreach (int initial in new[] { -1, 0, 1 })
        {
            programs.Add(ShotProgram.Straight with { InitialAimOffset = initial });
            foreach (int bendDirection in new[] { -1, 1 })
                for (int bendAfter = 1; bendAfter <= 4; bendAfter++)
                    for (int bendEvery = 1; bendEvery <= 3; bendEvery++)
                        for (int bendCount = 1; bendCount <= 3; bendCount++)
                            programs.Add(new ShotProgram(
                                initial,
                                bendDirection,
                                bendAfter,
                                bendEvery,
                                bendCount));
        }

        Assert.Equal(219, programs.Count);
        Assert.All(programs, program => Assert.True(ArcRules.IsValidShotProgram(program)));
        int distinct = programs
            .Select(program => string.Join(
                ";",
                ProgrammedProjectilePath.Trace(
                    OpenArcRoom(),
                    new Position(4, 6),
                    Direction.East,
                    program,
                    ArcRules)))
            .Distinct(StringComparer.Ordinal)
            .Count();
        Assert.Equal(125, distinct);
    }

    [Fact]
    public void PathGenerator_ProducesTheTheoryLabHeadingSchedule()
    {
        var program = new ShotProgram(
            InitialAimOffset: -1,
            BendDirection: -1,
            BendAfterTiles: 2,
            BendEveryTiles: 1,
            BendCount: 2);

        var path = ProgrammedProjectilePath.Trace(
            OpenArcRoom(),
            new Position(4, 6),
            Direction.East,
            program,
            ArcRules);

        Assert.Equal(
            [
                new Position(5, 5),
                new Position(6, 4),
                new Position(6, 3),
                new Position(5, 2),
                new Position(4, 1),
            ],
            path);
    }

    [Fact]
    public void SdkPreview_MatchesTheAuthoritativeEnginePath()
    {
        var program = new ShotProgram(-1, -1, 2, 1, 2);
        var map = OpenArcRoom();
        var enginePath = ProgrammedProjectilePath.Trace(
            map,
            new Position(4, 6),
            Direction.East,
            program,
            ArcRules);
        var sdkProgram = new BotArena.Sdk.ShotProgram(-1, -1, 2, 1, 2);
        var sdkLimits = new BotArena.Sdk.ShotProgramLimits(1, 4, 3, 3, 8, 1, 2);

        var sdkPath = BotArena.Sdk.ShotPaths.Preview(
            new BotArena.Sdk.Position(4, 6),
            BotArena.Sdk.Direction.East,
            sdkProgram,
            sdkLimits.MaxPathTiles,
            p => map.IsWall(new Position(p.X, p.Y)));

        Assert.True(sdkLimits.IsValid(sdkProgram));
        Assert.Equal(
            enginePath.Select(p => new BotArena.Sdk.Position(p.X, p.Y)),
            sdkPath);
    }

    [Fact]
    public void DiagonalPath_CannotClipEitherOrthogonalWall()
    {
        var map = ArenaMap.Create("test-arc-corner", [
            "#####",
            "##..#",
            "#...#",
            "#...#",
            "#####",
        ], [new Spawn(1, 2, Direction.East), new Spawn(3, 3, Direction.West)]);
        var program = ShotProgram.Straight with { InitialAimOffset = -1 };

        var path = ProgrammedProjectilePath.Trace(
            map,
            new Position(1, 2),
            Direction.East,
            program,
            ArcRules);

        Assert.Empty(path);
    }

    [Fact]
    public void ProgrammedShot_LaunchesOneTileThenAdvancesTwoAndRevealsOnlyCurrentHeading()
    {
        var program = new ShotProgram(-1, -1, 2, 1, 2);
        var session = new MatchSession(OpenArcRoom(), ArcRules);

        var launch = session.Step(
            [BotDecision.Shoot(program), BotDecision.Of(BotAction.Wait)]);

        var projectile = Assert.Single(session.State.Projectiles);
        Assert.Equal(new Position(5, 5), projectile.Position);
        Assert.Equal(ProjectileHeading.NorthEast, projectile.Heading);
        Assert.Equal([new Position(5, 5)], Assert.Single(launch.ProjectileTraversals).Path);

        var observed = Assert.Single(session.BuildObservation(0).VisibleProjectiles!);
        Assert.Equal(ProjectileHeading.NorthEast, observed.Heading);
        Assert.Equal(ArcRules.GetShotProgramLimits(), session.BuildObservation(0).ShotPrograms);

        var advance = session.Step(
            [BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        projectile = Assert.Single(session.State.Projectiles);
        Assert.Equal(new Position(6, 3), projectile.Position);
        Assert.Equal(ProjectileHeading.North, projectile.Heading);
        Assert.Equal(
            [new Position(6, 4), new Position(6, 3)],
            Assert.Single(advance.ProjectileTraversals).Path);
    }

    [Fact]
    public void ProgrammedShot_LaunchTraversalChecksEveryConfiguredTile()
    {
        var rules = ArcRules with { ProgrammedShotLaunchTiles = 2 };
        var session = new MatchSession(OpenArcRoom(), rules);
        session.State.Bots[1].Position = new Position(6, 4);
        var program = new ShotProgram(-1, -1, 2, 1, 2);

        var launch = session.Step(
            [BotDecision.Shoot(program), BotDecision.Of(BotAction.Wait)]);

        Assert.Empty(session.State.Projectiles);
        Assert.Equal(2, session.State.Bots[1].Health);
        Assert.Equal(
            [new Position(5, 5), new Position(6, 4)],
            Assert.Single(launch.ProjectileTraversals).Path);
    }

    [Fact]
    public void ProgrammedShot_CanClearAVisibleCornerAndHitBehindCover()
    {
        var map = ArenaMap.Create("test-arc-around-wall", [
            "############",
            "#..........#",
            "#..##......#",
            "#..........#",
            "#......##..#",
            "#..........#",
            "#..........#",
            "############",
        ], [new Spawn(1, 1, Direction.East), new Spawn(6, 5, Direction.West)]);
        var program = new ShotProgram(0, 1, 4, 1, 2);
        var session = new MatchSession(map, ArcRules);
        Assert.False(Visibility.HasLineOfSight(
            map,
            session.State.Bots[0].Position,
            session.State.Bots[1].Position));

        session.Step([BotDecision.Shoot(program), BotDecision.Of(BotAction.Wait)]);
        for (int tick = 0; tick < 4; tick++)
            session.Step([BotDecision.Of(BotAction.Wait), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(2, session.State.Bots[1].Health);
        Assert.Empty(session.State.Projectiles);
    }

    [Fact]
    public void ProgramPayload_IsBlockedWhenDisabled_AndInvalidValuesFaultWhenEnabled()
    {
        var program = new ShotProgram(-1, -1, 2, 1, 2);
        var disabled = new MatchSession(OpenArcRoom(), ArcRules with
        {
            AllowProgrammedShots = false,
        });

        var blocked = disabled.Step(
            [BotDecision.Shoot(program), BotDecision.Of(BotAction.Wait)]);

        Assert.Equal(ActionResult.Blocked, blocked.Bots[0].Result);
        Assert.Empty(disabled.State.Projectiles);
        Assert.Equal(0, disabled.State.Bots[0].Faults);

        var enabled = new MatchSession(OpenArcRoom(), ArcRules);
        var invalid = enabled.Step(
            [
                BotDecision.Shoot(program with { BendCount = 4 }),
                BotDecision.Of(BotAction.Wait),
            ]);

        Assert.Equal(ActionResult.Faulted, invalid.Bots[0].Result);
        Assert.Equal(1, enabled.State.Bots[0].Faults);
        Assert.Empty(enabled.State.Projectiles);
    }

    [Fact]
    public void Replay_RetainsOmniscientProgramAndPathWithoutPuttingPathInObservation()
    {
        var program = new ShotProgram(-1, 1, 2, 2, 2);
        int ticks = 0;
        var run = new MatchEngine().Run(TestMaps.Config(
            OpenArcRoom(),
            new DelegateRuntime(_ => ticks++ == 0
                ? BotDecision.Shoot(program)
                : BotDecision.Of(BotAction.Wait)),
            new ScriptedRuntime(),
            rules: ArcRules with { MaxTicks = 3 }));

        var document = ReplaySerializer.FromJson(
            ReplaySerializer.ToJson(run.Replay));

        Assert.True(document.Header.ProgrammedShots == true);
        Assert.Equal(ArcRules.GetShotProgramLimits(), document.Header.ProgrammedShotLimits);
        Assert.Equal(program, document.Ticks[0].Bots[0].ShotProgram);
        Assert.NotEmpty(Assert.Single(document.Ticks[0].ProjectileTraversals!).ProgrammedPath!);
        Assert.NotEmpty(Assert.Single(document.Ticks[0].Projectiles!).ProgrammedPath!);
        Assert.Equal(run.ReplayHash, document.ReplayHash);
    }
}
