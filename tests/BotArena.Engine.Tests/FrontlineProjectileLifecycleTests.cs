using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class FrontlineProjectileLifecycleTests
{
    [Fact]
    public void ProgrammedCurve_MatchesLegacyLaunchDwellAdvanceAndWallDespawn()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            projectileTicksPerTile: 2,
            projectileTilesPerAdvance: 2);
        var sessions = CreateSessions(rules);
        var program = new ShotProgram(
            InitialAimOffset: 0,
            BendDirection: -1,
            BendAfterTiles: 2,
            BendEveryTiles: 1,
            BendCount: 2);
        Position[] fullPath =
        [
            new(2, 2),
            new(3, 2),
            new(4, 1),
        ];

        (FrontlineStepResult launch, TickResult legacyLaunch) =
            StepAndAssertProjectileParity(
                sessions,
                BotDecision.Shoot(program),
                BotDecision.Of(BotAction.Wait));

        Assert.Equal(fullPath, Assert.Single(launch.ProjectileTraversals).ProgrammedPath);
        Assert.Equal(
            [new Position(2, 2)],
            Assert.Single(launch.ProjectileTraversals).Path);
        Assert.Equal(
            [new Position(2, 2)],
            Assert.Single(legacyLaunch.ProjectileTraversals).Path);
        Assert.Equal(new Position(2, 2), Assert.Single(
            sessions.Frontline.State.Projectiles).Position);

        (FrontlineStepResult dwell, TickResult legacyDwell) =
            StepAndAssertProjectileParity(
                sessions,
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.Wait));

        Assert.Empty(dwell.ProjectileTraversals);
        Assert.Empty(legacyDwell.ProjectileTraversals);
        Assert.Equal(new Position(2, 2), Assert.Single(
            sessions.Frontline.State.Projectiles).Position);

        (FrontlineStepResult advance, TickResult legacyAdvance) =
            StepAndAssertProjectileParity(
                sessions,
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.Wait));

        Assert.Equal(
            [new Position(3, 2), new Position(4, 1)],
            Assert.Single(advance.ProjectileTraversals).Path);
        Assert.Equal(
            [new Position(3, 2), new Position(4, 1)],
            Assert.Single(legacyAdvance.ProjectileTraversals).Path);
        Assert.Empty(sessions.Frontline.State.Projectiles);
        Assert.Empty(sessions.Legacy.State.Projectiles);
    }

    [Fact]
    public void ProgrammedStraightShot_StopsAfterItsFinalRangeTileLikeLegacy()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            projectileTicksPerTile: 1,
            projectileTilesPerAdvance: 2) with
        {
            ShotRange = 3,
        };
        var sessions = CreateSessions(rules);

        (FrontlineStepResult launch, TickResult legacyLaunch) =
            StepAndAssertProjectileParity(
                sessions,
                BotDecision.Shoot(ShotProgram.Straight),
                BotDecision.Of(BotAction.Wait));

        Assert.Equal(
            [new Position(2, 2)],
            Assert.Single(launch.ProjectileTraversals).Path);
        Assert.Equal(
            [new Position(2, 2)],
            Assert.Single(legacyLaunch.ProjectileTraversals).Path);

        (FrontlineStepResult advance, TickResult legacyAdvance) =
            StepAndAssertProjectileParity(
                sessions,
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.Wait));

        Assert.Equal(
            [new Position(3, 2), new Position(4, 2)],
            Assert.Single(advance.ProjectileTraversals).Path);
        Assert.Equal(
            [new Position(3, 2), new Position(4, 2)],
            Assert.Single(legacyAdvance.ProjectileTraversals).Path);
        Assert.Empty(sessions.Frontline.State.Projectiles);
        Assert.Empty(sessions.Legacy.State.Projectiles);
    }

    [Fact]
    public void MovementIntoAnOccupiedProjectile_ResolvesBeforeItsAdvanceLikeLegacy()
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(
            projectileTicksPerTile: 2,
            projectileTilesPerAdvance: 1);
        var sessions = CreateSessions(rules);

        for (int tick = 0; tick < 3; tick++)
        {
            StepAndAssertProjectileParity(
                sessions,
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.MoveForward));
        }

        StepAndAssertProjectileParity(
            sessions,
            BotDecision.Of(BotAction.Shoot),
            BotDecision.Of(BotAction.MoveForward));
        Assert.Equal(new Position(2, 2), Assert.Single(
            sessions.Frontline.State.Projectiles).Position);
        Assert.Equal(new Position(3, 2), ActiveLife(
            sessions.Frontline, teamId: 1).Position);

        (FrontlineStepResult frontline, TickResult legacy) =
            StepAndAssertProjectileParity(
                sessions,
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.MoveForward));

        Assert.Equal(new Position(2, 2), ActiveLife(
            sessions.Frontline, teamId: 1).Position);
        Assert.Equal(2, ActiveLife(sessions.Frontline, teamId: 1).Health);
        Assert.Empty(sessions.Frontline.State.Projectiles);
        Assert.Empty(sessions.Legacy.State.Projectiles);
        Assert.Empty(frontline.ProjectileTraversals);
        Assert.Empty(legacy.ProjectileTraversals);

        int moveIndex = frontline.Events
            .Select((@event, index) => (@event, index))
            .Single(item =>
                item.@event.Type == FrontlineMatchEventType.Move
                && item.@event.ActorId?.TeamId == 1)
            .index;
        int damageIndex = frontline.Events
            .Select((@event, index) => (@event, index))
            .Single(item =>
                item.@event.Type == FrontlineMatchEventType.Damage
                && item.@event.ActorId?.TeamId == 1)
            .index;
        Assert.True(moveIndex < damageIndex);
    }

    [Fact]
    public void SimultaneousFatalFire_MatchesLegacyDamageBeforeFrontlineRespawn()
    {
        GameRules rules = WithPrimeHealth(
            FrontlineTestDefinitions.PrimeOnlyRules(
                projectileTicksPerTile: 1,
                projectileTilesPerAdvance: 2),
            health: 1);
        var sessions = CreateSessions(rules);

        StepAndAssertProjectileParity(
            sessions,
            BotDecision.Of(BotAction.Shoot),
            BotDecision.Of(BotAction.Shoot));
        for (int tick = 1; tick <= 2; tick++)
        {
            StepAndAssertProjectileParity(
                sessions,
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.Wait));
        }

        FrontlineStepResult frontline = StepFrontline(
            sessions.Frontline,
            BotDecision.Of(BotAction.Wait),
            BotDecision.Of(BotAction.Wait));
        TickResult legacy = sessions.Legacy.Step(
            [
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.Wait),
            ]);

        Assert.Equal(3, frontline.Tick);
        Assert.Equal(frontline.Tick, legacy.Tick);
        Assert.Equal(
            [0, 1],
            frontline.Events
                .Where(@event => @event.Type == FrontlineMatchEventType.Damage)
                .Select(@event => @event.ActorId!.Value.TeamId)
                .Order()
                .ToArray());
        Assert.Equal(
            [0, 1],
            legacy.Events
                .Where(@event => @event.Type == GameEventType.Damage)
                .Select(@event => @event.TargetSlot!.Value)
                .Order()
                .ToArray());
        Assert.Equal(
            [0, 1],
            frontline.Events
                .Where(@event => @event.Type == FrontlineMatchEventType.Destroyed)
                .Select(@event => @event.ActorId!.Value.TeamId)
                .Order()
                .ToArray());
        Assert.All(
            sessions.Frontline.State.Teams,
            team =>
            {
                FrontlineUnitState unit = team.GetUnit(unitId: 0);
                Assert.Null(unit.ActiveLife);
                Assert.Equal(FrontlineLifecycleStatus.Respawning, unit.LifecycleStatus);
                Assert.Equal(1, unit.DamageDealt);
            });
        Assert.All(
            sessions.Legacy.State.Bots,
            bot =>
            {
                Assert.Equal(0, bot.Health);
                Assert.Equal(BotStatus.Destroyed, bot.Status);
                Assert.Equal(1, bot.DamageDealt);
            });
        Assert.False(frontline.MatchCompleted);
        Assert.True(legacy.MatchCompleted);
        Assert.Null(sessions.Legacy.Result!.WinnerSlot);
    }

    [Fact]
    public void DeadLifeProjectile_PersistsAndPassesThroughItsRespawnedAlly()
    {
        GameRules rules = WithPrimeHealth(
            FrontlineTestDefinitions.PrimeOnlyRules(
                primeRespawnTicks: 1,
                projectileTicksPerTile: 3,
                projectileTilesPerAdvance: 1),
            health: 1);
        var session = new FrontlineMatchSession(
            MatchDefinitionResolver.Resolve(
                rules,
                FrontlineTestDefinitions.OpenMapV2()));
        var originalActor = new FrontlineActorId(0, 0, 0);

        StepFrontline(
            session,
            BotDecision.Of(BotAction.Shoot),
            BotDecision.Of(BotAction.Shoot));
        for (int tick = 1; tick <= 4; tick++)
        {
            StepFrontline(
                session,
                BotDecision.Of(BotAction.MoveForward),
                BotDecision.Of(BotAction.Wait));
        }

        FrontlineUnitState unit = session.State.GetUnit(teamId: 0, unitId: 0);
        Assert.Null(unit.ActiveLife);
        Assert.Equal(6, unit.RespawnAtTick);
        FrontlineProjectileState inheritedProjectile =
            Assert.Single(session.State.Projectiles);
        Assert.Equal(originalActor, inheritedProjectile.OwnerActorId);
        Assert.Equal(new Position(3, 2), inheritedProjectile.Position);

        FrontlineStepResult absentTick = StepFrontline(
            session,
            BotDecision.Of(BotAction.Wait),
            BotDecision.Of(BotAction.Wait));
        Assert.DoesNotContain(
            absentTick.TickStart.ActiveActors,
            actorId => actorId.TeamId == 0);

        FrontlineTickStart respawn = session.PrepareTick();
        var respawnedActor = new FrontlineActorId(0, 0, 1);
        Assert.Equal([respawnedActor], respawn.RespawnedActors);
        session.Step(KeyedDecisions(
            respawn,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.Wait)));
        Assert.Equal(new Position(2, 2), ActiveLife(session, teamId: 0).Position);

        StepFrontline(
            session,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.Wait));
        Assert.Equal(new Position(3, 2), ActiveLife(session, teamId: 0).Position);

        FrontlineStepResult passThrough = StepFrontline(
            session,
            BotDecision.Of(BotAction.MoveForward),
            BotDecision.Of(BotAction.Wait));

        FrontlineLifeState newLife = ActiveLife(session, teamId: 0);
        Assert.Equal(respawnedActor, newLife.ActorId);
        Assert.Equal(new Position(4, 2), newLife.Position);
        Assert.Equal(1, newLife.Health);
        Assert.DoesNotContain(
            passThrough.Events,
            @event => @event.Type == FrontlineMatchEventType.Damage);
        inheritedProjectile = Assert.Single(session.State.Projectiles);
        Assert.Equal(originalActor, inheritedProjectile.OwnerActorId);
        Assert.Equal(new Position(4, 2), inheritedProjectile.Position);

        FrontlineStepResult eventualHit = null!;
        for (int tick = 9; tick <= 15; tick++)
        {
            eventualHit = StepFrontline(
                session,
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.Wait));
        }

        FrontlineMatchEvent damage = Assert.Single(
            eventualHit.Events,
            @event => @event.Type == FrontlineMatchEventType.Damage);
        Assert.Equal(new FrontlineActorId(1, 0, 0), damage.ActorId);
        Assert.Equal(originalActor, damage.OtherActorId);
        Assert.Equal(1, unit.DamageDealt);
        Assert.Equal(0, newLife.DamageDealt);
        Assert.Empty(session.State.Projectiles);
    }

    [Fact]
    public void SimultaneousOverkill_CreditsOnlyActualTargetHealth()
    {
        GameRules rules = WithPrimeHealth(
            FrontlineTestDefinitions.PrimeOnlyRules(
                shootCooldownTicks: 0,
                projectileTicksPerTile: 3,
                projectileTilesPerAdvance: 1),
            health: 1);
        var session = new FrontlineMatchSession(
            MatchDefinitionResolver.Resolve(
                rules,
                FrontlineTestDefinitions.OpenMapV2()));

        for (int tick = 0; tick < 4; tick++)
        {
            StepFrontline(
                session,
                BotDecision.Of(BotAction.Wait),
                BotDecision.Of(BotAction.MoveForward));
        }
        StepFrontline(
            session,
            BotDecision.Of(BotAction.Shoot),
            BotDecision.Of(BotAction.Wait));
        Assert.Equal(new Position(2, 2), Assert.Single(
            session.State.Projectiles).Position);

        FrontlineStepResult overkill = StepFrontline(
            session,
            BotDecision.Of(BotAction.Shoot),
            BotDecision.Of(BotAction.MoveForward));

        FrontlineUnitState shooter =
            session.State.GetUnit(teamId: 0, unitId: 0);
        FrontlineUnitState target =
            session.State.GetUnit(teamId: 1, unitId: 0);
        Assert.Equal(1, shooter.DamageDealt);
        Assert.Null(target.ActiveLife);
        FrontlineMatchEvent damage = Assert.Single(
            overkill.Events,
            @event => @event.Type == FrontlineMatchEventType.Damage);
        Assert.Equal(1, damage.Amount);
        Assert.Equal(0, damage.NewHealth);
        Assert.Empty(session.State.Projectiles);
    }

    private static SessionPair CreateSessions(GameRules rules) =>
        new(
            new FrontlineMatchSession(
                MatchDefinitionResolver.Resolve(
                    rules,
                    FrontlineTestDefinitions.OpenMapV2())),
            new MatchSession(
                FrontlineTestDefinitions.OpenMapV1(),
                rules));

    private static GameRules WithPrimeHealth(GameRules rules, int health)
    {
        FrontlineRules frontline = rules.Frontline
            ?? throw new ArgumentException("Frontline rules are required.", nameof(rules));
        return rules with
        {
            MaxHealth = health,
            Frontline = frontline with
            {
                PrimeForm = frontline.PrimeForm with { MaxHealth = health },
            },
        };
    }

    private static (FrontlineStepResult Frontline, TickResult Legacy)
        StepAndAssertProjectileParity(
            SessionPair sessions,
            BotDecision team0,
            BotDecision team1)
    {
        FrontlineStepResult frontline =
            StepFrontline(sessions.Frontline, team0, team1);
        TickResult legacy = sessions.Legacy.Step([team0, team1]);

        Assert.Equal(legacy.Tick, frontline.Tick);
        foreach (int teamId in new[] { 0, 1 })
        {
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
            legacy.ProjectileTraversals.Select(TraversalSnapshot.FromLegacy),
            frontline.ProjectileTraversals.Select(TraversalSnapshot.FromFrontline));
        Assert.Equal(
            sessions.Legacy.State.Projectiles
                .OrderBy(projectile => projectile.Id)
                .Select(ProjectileSnapshot.FromLegacy),
            sessions.Frontline.State.Projectiles
                .OrderBy(projectile => projectile.Id)
                .Select(ProjectileSnapshot.FromFrontline));
        Assert.Equal(sessions.Legacy.State.Tick, sessions.Frontline.State.Tick);
        return (frontline, legacy);
    }

    private static FrontlineStepResult StepFrontline(
        FrontlineMatchSession session,
        BotDecision team0,
        BotDecision team1)
    {
        FrontlineTickStart tickStart = session.PrepareTick();
        return session.Step(KeyedDecisions(tickStart, team0, team1));
    }

    private static IReadOnlyDictionary<FrontlineActorId, BotDecision>
        KeyedDecisions(
            FrontlineTickStart tickStart,
            BotDecision team0,
            BotDecision team1) =>
        tickStart.ActiveActors
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

    private static FrontlineLifeState ActiveLife(
        FrontlineMatchSession session,
        int teamId) =>
        Assert.IsType<FrontlineLifeState>(
            session.State.GetUnit(teamId, unitId: 0).ActiveLife);

    private sealed record SessionPair(
        FrontlineMatchSession Frontline,
        MatchSession Legacy);

    private sealed record ActorSnapshot(
        int TeamId,
        Position Position,
        Direction Facing,
        int Health,
        int Cooldown,
        int Energy,
        ActionResult LastActionResult);

    private sealed record TraversalSnapshot(
        long Id,
        int OwnerTeamId,
        Direction Direction,
        Position From,
        string Path,
        ProjectileHeading? Heading,
        string ProgrammedPath)
    {
        public static TraversalSnapshot FromLegacy(
            ProjectileTickTraversal traversal) =>
            new(
                traversal.Id,
                traversal.OwnerSlot,
                traversal.Direction,
                traversal.From,
                PathKey(traversal.Path),
                traversal.Heading,
                PathKey(traversal.ProgrammedPath));

        public static TraversalSnapshot FromFrontline(
            FrontlineProjectileTraversal traversal) =>
            new(
                traversal.Id,
                traversal.OwnerActorId.TeamId,
                traversal.Direction,
                traversal.From,
                PathKey(traversal.Path),
                traversal.Heading,
                PathKey(traversal.ProgrammedPath));
    }

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
    }

    private static string PathKey(IReadOnlyList<Position>? path) =>
        path is null ? "<straight>" : string.Join(";", path);
}
