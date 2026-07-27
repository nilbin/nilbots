using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class FrontlineCombatParityTests
{
    [Fact]
    public void TurnsAndWallMovement_MatchLegacyResolution()
    {
        var sessions = CreateSessions();

        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.TurnLeft),
            BotDecision.Of(BotAction.TurnRight));
        Assert.Equal(Direction.North, ActiveLife(sessions.Frontline, 0).Facing);
        Assert.Equal(Direction.North, ActiveLife(sessions.Frontline, 1).Facing);

        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));
        Assert.Equal(new Position(1, 1), ActiveLife(sessions.Frontline, 0).Position);
        Assert.Equal(new Position(7, 1), ActiveLife(sessions.Frontline, 1).Position);

        (FrontlineStepResult frontline, TickResult legacy) = StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));

        Assert.All(
            frontline.ActionResolutions,
            resolution => Assert.Equal(ActionResult.Blocked, resolution.Result));
        Assert.All(
            legacy.Bots,
            resolution => Assert.Equal(ActionResult.Blocked, resolution.Result));
        Assert.Equal(new Position(1, 1), ActiveLife(sessions.Frontline, 0).Position);
        Assert.Equal(new Position(7, 1), ActiveLife(sessions.Frontline, 1).Position);
    }

    [Fact]
    public void SameDestination_BlocksBothLikeLegacy()
    {
        var sessions = CreateSessions();

        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));
        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));
        (FrontlineStepResult frontline, TickResult legacy) = StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));

        Assert.All(
            frontline.ActionResolutions,
            resolution => Assert.Equal(ActionResult.Blocked, resolution.Result));
        Assert.All(
            legacy.Bots,
            resolution => Assert.Equal(ActionResult.Blocked, resolution.Result));
        Assert.Equal(new Position(3, 2), ActiveLife(sessions.Frontline, 0).Position);
        Assert.Equal(new Position(5, 2), ActiveLife(sessions.Frontline, 1).Position);
    }

    [Fact]
    public void SwappingAdjacentTiles_BlocksBothLikeLegacy()
    {
        var sessions = CreateSessions();

        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));
        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));
        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.Wait));
        (FrontlineStepResult frontline, TickResult legacy) = StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));

        Assert.All(
            frontline.ActionResolutions,
            resolution => Assert.Equal(ActionResult.Blocked, resolution.Result));
        Assert.All(
            legacy.Bots,
            resolution => Assert.Equal(ActionResult.Blocked, resolution.Result));
        Assert.Equal(new Position(4, 2), ActiveLife(sessions.Frontline, 0).Position);
        Assert.Equal(new Position(5, 2), ActiveLife(sessions.Frontline, 1).Position);
    }

    [Fact]
    public void FollowingIntoAVacatedTile_MovesBothLikeLegacy()
    {
        var sessions = CreateSessions();

        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));
        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));
        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.TurnRight));
        StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.Wait),
            BotDecision.Of(BotAction.TurnRight));
        (FrontlineStepResult frontline, TickResult legacy) = StepAndAssertParity(
            sessions,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.MoveForward));

        Assert.All(
            frontline.ActionResolutions,
            resolution => Assert.Equal(ActionResult.Success, resolution.Result));
        Assert.All(
            legacy.Bots,
            resolution => Assert.Equal(ActionResult.Success, resolution.Result));
        Assert.Equal(new Position(5, 2), ActiveLife(sessions.Frontline, 0).Position);
        Assert.Equal(new Position(6, 2), ActiveLife(sessions.Frontline, 1).Position);
    }

    [Fact]
    public void Cooldown_AllowsShotsAtTicksZeroAndThreeLikeLegacy()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            shootCooldownTicks: 2,
            projectileTicksPerTile: 2,
            projectileTilesPerAdvance: 1);
        var sessions = CreateSessions(rules);
        var frontlineResults = new List<ActionResult>();
        var legacyResults = new List<ActionResult>();
        var frontlineShotTicks = new List<int>();
        var legacyShotTicks = new List<int>();

        for (int tick = 0; tick <= 3; tick++)
        {
            (FrontlineStepResult frontline, TickResult legacy) =
                StepAndAssertParity(
                    sessions,
                    BotDecision.Of(BotAction.Shoot),
                    BotDecision.Of(BotAction.Wait));
            frontlineResults.Add(Resolution(frontline, teamId: 0).Result);
            legacyResults.Add(legacy.Bots.Single(bot => bot.Slot == 0).Result);
            frontlineShotTicks.AddRange(
                frontline.Events
                    .Where(@event =>
                        @event.Type == FrontlineMatchEventType.Shot
                        && @event.ActorId?.TeamId == 0)
                    .Select(@event => @event.Tick));
            if (legacy.Events.Any(@event =>
                    @event.Type == GameEventType.Shot
                    && @event.Slot == 0))
            {
                legacyShotTicks.Add(legacy.Tick);
            }
        }

        Assert.Equal(
            [
                ActionResult.Success,
                ActionResult.OnCooldown,
                ActionResult.OnCooldown,
                ActionResult.Success,
            ],
            frontlineResults);
        Assert.Equal(frontlineResults, legacyResults);
        Assert.Equal([0, 3], frontlineShotTicks);
        Assert.Equal(frontlineShotTicks, legacyShotTicks);
    }

    private static SessionPair CreateSessions(GameRules? rules = null)
    {
        GameRules effective = rules ?? FrontlineTestDefinitions.PrimeOnlyRules();
        return new SessionPair(
            new FrontlineMatchSession(
                MatchDefinitionResolver.Resolve(
                    effective,
                    FrontlineTestDefinitions.OpenMapV2())),
            new MatchSession(
                FrontlineTestDefinitions.OpenMapV1(),
                effective));
    }

    private static (FrontlineStepResult Frontline, TickResult Legacy)
        StepAndAssertParity(
            SessionPair sessions,
            BotDecision team0,
            BotDecision team1)
    {
        FrontlineTickStart tickStart = sessions.Frontline.PrepareTick();
        Assert.Equal(
            [new FrontlineActorId(0, 0, 0), new FrontlineActorId(1, 0, 0)],
            tickStart.ActiveActors);

        var decisions = tickStart.ActiveActors
            .OrderByDescending(actorId => actorId)
            .ToDictionary(
                actorId => actorId,
                actorId => actorId.TeamId switch
                {
                    0 => team0,
                    1 => team1,
                    _ => throw new InvalidOperationException(
                        $"Unexpected team {actorId.TeamId}."),
                });
        FrontlineStepResult frontline = sessions.Frontline.Step(decisions);
        TickResult legacy = sessions.Legacy.Step([team0, team1]);

        Assert.Equal(legacy.Tick, frontline.Tick);
        foreach (int teamId in new[] { 0, 1 })
        {
            FrontlineActionResolution frontlineResolution =
                Resolution(frontline, teamId);
            BotTickResolution legacyResolution =
                legacy.Bots.Single(resolution => resolution.Slot == teamId);
            Assert.Equal(
                new ResolutionSnapshot(
                    legacyResolution.Slot,
                    legacyResolution.ChosenAction,
                    legacyResolution.ValidatedAction,
                    legacyResolution.Result),
                new ResolutionSnapshot(
                    frontlineResolution.ActorId.TeamId,
                    frontlineResolution.ChosenAction,
                    frontlineResolution.ValidatedAction,
                    frontlineResolution.Result));

            FrontlineLifeState frontlineLife =
                ActiveLife(sessions.Frontline, teamId);
            BotState legacyBot =
                sessions.Legacy.State.Bots.Single(bot => bot.Slot == teamId);
            Assert.Equal(
                new ActorSnapshot(
                    legacyBot.Slot,
                    legacyBot.Position,
                    legacyBot.Facing,
                    legacyBot.Health,
                    legacyBot.Cooldown,
                    legacyBot.Energy,
                    legacyBot.LastActionResult),
                new ActorSnapshot(
                    frontlineLife.ActorId.TeamId,
                    frontlineLife.Position,
                    frontlineLife.Facing,
                    frontlineLife.Health,
                    frontlineLife.Cooldown,
                    frontlineLife.Energy,
                    frontlineLife.LastActionResult));
        }

        Assert.Equal(
            sessions.Legacy.State.Projectiles
                .OrderBy(projectile => projectile.Id)
                .Select(projectile => ProjectileSnapshot.FromLegacy(projectile)),
            sessions.Frontline.State.Projectiles
                .OrderBy(projectile => projectile.Id)
                .Select(projectile => ProjectileSnapshot.FromFrontline(projectile)));
        Assert.Equal(sessions.Legacy.State.Tick, sessions.Frontline.State.Tick);
        return (frontline, legacy);
    }

    private static FrontlineActionResolution Resolution(
        FrontlineStepResult result,
        int teamId) =>
        result.ActionResolutions.Single(
            resolution => resolution.ActorId.TeamId == teamId);

    private static FrontlineLifeState ActiveLife(
        FrontlineMatchSession session,
        int teamId) =>
        Assert.IsType<FrontlineLifeState>(
            session.State.GetUnit(teamId, unitId: 0).ActiveLife);

    private sealed record SessionPair(
        FrontlineMatchSession Frontline,
        MatchSession Legacy);

    private sealed record ResolutionSnapshot(
        int TeamId,
        BotAction ChosenAction,
        BotAction ValidatedAction,
        ActionResult Result);

    private sealed record ActorSnapshot(
        int TeamId,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        int Energy,
        ActionResult LastActionResult);

    private sealed record ProjectileSnapshot(
        long Id,
        int OwnerTeamId,
        Position Position,
        Direction Direction,
        ProjectileHeading? Heading,
        string ProgrammedPath,
        int NextProgrammedPathIndex,
        int TilesTraveled,
        int Phase)
    {
        public static ProjectileSnapshot FromLegacy(ProjectileState projectile) =>
            new(
                projectile.Id,
                projectile.OwnerSlot,
                projectile.Position,
                projectile.Direction,
                projectile.Heading,
                PathKey(projectile.ProgrammedPath),
                projectile.NextProgrammedPathIndex,
                projectile.TilesTraveled,
                projectile.Phase);

        public static ProjectileSnapshot FromFrontline(
            FrontlineProjectileState projectile) =>
            new(
                projectile.Id,
                projectile.OwnerActorId.TeamId,
                projectile.Position,
                projectile.Direction,
                projectile.Heading,
                PathKey(projectile.ProgrammedPath),
                projectile.NextProgrammedPathIndex,
                projectile.TilesTraveled,
                projectile.Phase);

        private static string PathKey(IReadOnlyList<Position>? path) =>
            path is null ? "<straight>" : string.Join(";", path);
    }
}
