using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public sealed class FrontlineAnchorTests
{
    [Fact]
    public void Transform_WindupOneChangesSameLifeAfterObjectiveAndPreservesState()
    {
        FrontlineMatchSession session = CreateSession();
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState life = PlaceChild(session);
        FrontlineActorId actorId = life.ActorId;
        life.Health = 2;
        life.Facing = Direction.South;
        life.DamageDealt = 7;
        session.State.GetActiveLife(new FrontlineActorId(0, 0, 0)).Position =
            new Position(7, 2);

        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(tick.ActiveActors);
        decisions[actorId] = ActorDecision.Transform("turret");
        FrontlineStepResult step = session.StepActors(decisions);

        Assert.Same(life, session.State.GetActiveLife(actorId));
        Assert.Equal("turret", life.FormId);
        Assert.Equal("child-mobile", session.State.GetUnit(0, 1).DefaultFormId);
        Assert.Equal(4, life.Health);
        Assert.Equal(Direction.South, life.Facing);
        Assert.Equal(7, life.DamageDealt);
        Assert.Null(life.PendingFormTransition);
        int objective = FindIndex(step.Events, value =>
            value.Type == FrontlineMatchEventType.FrontlineProgressChanged);
        int changed = FindIndex(step.Events, value =>
            value.Type == FrontlineMatchEventType.FormChanged);
        Assert.True(objective >= 0 && objective < changed);
        FrontlineMatchEvent started = Assert.Single(step.Events, value =>
            value.Type == FrontlineMatchEventType.FormTransitionStarted);
        FrontlineMatchEvent formChanged = Assert.Single(step.Events, value =>
            value.Type == FrontlineMatchEventType.FormChanged);
        Assert.Equal((2, 2), (
            started.FormTransitionStartedAtTick,
            started.FormTransitionCompletesAtTick));
        Assert.Equal(actorId, formChanged.ActorId);
        Assert.Equal(("child-mobile", "turret"), (
            formChanged.FromFormId,
            formChanged.ToFormId));
    }

    [Fact]
    public void PendingTransition_IsWaitOnlySurvivesDamageAndCompletes()
    {
        FrontlineMatchSession session = CreateSession(windupTicks: 2);
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState life = PlaceChild(session);
        life.Health = 2;
        StartTransform(session, tick, life.ActorId);
        Assert.Equal("child-mobile", life.FormId);
        Assert.Equal(3, life.PendingFormTransition?.CompletesAtTick);

        FrontlineTickStart due = session.PrepareTick();
        ActorObservation observation = Project(session, due, life.ActorId);
        Assert.Equal(
            [PublicActionIds.Wait],
            observation.Actions
                .Where(action => action.Available)
                .Select(action => action.ActionId)
                .ToArray());
        session.State.MutableProjectiles.Add(new FrontlineProjectileState(
            100,
            new FrontlineActorId(1, 0, 0),
            life.Position,
            Direction.West));
        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(due.ActiveActors);
        decisions[life.ActorId] = ActorDecision.MoveForward();
        FrontlineStepResult step = session.StepActors(decisions);

        FrontlineActionResolution resolution = step.ActionResolutions.Single(
            value => value.ActorId == life.ActorId);
        Assert.Equal(PublicActionIds.Wait, resolution.ValidatedActionId);
        Assert.Equal(ActionResult.Blocked, resolution.Result);
        Assert.Equal("turret", life.FormId);
        Assert.Equal(3, life.Health);
        Assert.Contains(step.Events, value =>
            value.Type == FrontlineMatchEventType.Damage);
        Assert.Contains(step.Events, value =>
            value.Type == FrontlineMatchEventType.FormChanged);
        Assert.DoesNotContain(step.Events, value =>
            value.Type == FrontlineMatchEventType.FormTransitionCancelled);
    }

    [Fact]
    public void LethalDamage_EmitsDestroyedThenCancellationAndRebuildUsesDefault()
    {
        FrontlineMatchSession session = CreateSession(windupTicks: 3);
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState life = PlaceChild(session);
        StartTransform(session, tick, life.ActorId);

        FrontlineTickStart next = session.PrepareTick();
        life.Health = 1;
        session.State.MutableProjectiles.Add(new FrontlineProjectileState(
            101,
            new FrontlineActorId(1, 0, 0),
            life.Position,
            Direction.West));
        FrontlineStepResult step = session.StepActors(Waits(next.ActiveActors));
        int destroyed = FindIndex(step.Events, value =>
            value.Type == FrontlineMatchEventType.Destroyed);
        int cancelled = FindIndex(step.Events, value =>
            value.Type == FrontlineMatchEventType.FormTransitionCancelled);

        Assert.True(destroyed >= 0 && cancelled == destroyed + 1);
        FrontlineMatchEvent cancellation = step.Events[cancelled];
        Assert.Equal(life.ActorId, cancellation.ActorId);
        Assert.Equal(("child-mobile", "turret"), (
            cancellation.FromFormId,
            cancellation.ToFormId));
        Assert.Equal(0, cancellation.NewHealth);
        FrontlineUnitState unit = session.State.GetUnit(0, 1);
        Assert.Null(unit.ActiveLife);
        Assert.Equal("child-mobile", unit.FormId);
        Assert.Equal("child-mobile", unit.DefaultFormId);
        Assert.DoesNotContain(step.Events, value =>
            value.Type == FrontlineMatchEventType.FormChanged);

        while (session.State.Tick < 6)
        {
            FrontlineTickStart waiting = session.PrepareTick();
            session.StepActors(Waits(waiting.ActiveActors));
        }
        FrontlineTickStart ready = session.PrepareTick();
        Assert.Contains(ready.Events, value =>
            value.Type == FrontlineMatchEventType.RebuildReady
            && value.UnitId == 1);
        Dictionary<FrontlineActorId, ActorDecision> fabricate =
            Waits(ready.ActiveActors);
        fabricate[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 1));
        session.StepActors(fabricate);
        FrontlineTickStart rebuilt = session.PrepareTick();
        Assert.Contains(rebuilt.Events, value =>
            value.Type == FrontlineMatchEventType.Fabricated
            && value.UnitId == 1);
        FrontlineLifeState fresh = session.State.GetUnit(0, 1).ActiveLife!;
        Assert.NotSame(life, fresh);
        Assert.Equal(new FrontlineActorId(0, 1, 1), fresh.ActorId);
        Assert.Equal("child-mobile", fresh.FormId);
        Assert.Null(fresh.PendingFormTransition);
    }

    [Fact]
    public void TerminalFutureTransition_RemainsPendingWithoutCancellation()
    {
        FrontlineMatchSession session = CreateSession(
            maxTicks: 3,
            windupTicks: 3);
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState life = PlaceChild(session);
        FrontlineStepResult terminal = StartTransform(
            session,
            tick,
            life.ActorId);

        Assert.True(terminal.MatchCompleted);
        Assert.Equal(4, life.PendingFormTransition?.CompletesAtTick);
        Assert.Equal("child-mobile", life.FormId);
        Assert.DoesNotContain(terminal.Events, value =>
            value.Type is FrontlineMatchEventType.FormChanged
                or FrontlineMatchEventType.FormTransitionCancelled);
        FrontlineUnitMatchResult result = session.Result!.Teams
            .Single(team => team.TeamId == 0).Units
            .Single(unit => unit.UnitId == 1);
        Assert.NotNull(result.PendingFormTransition);
    }

    [Fact]
    public void Transform_ClampsHealthAtTurretMaximum()
    {
        FrontlineMatchSession session = CreateSession(
            anchorHealthGain: 10);
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState life = PlaceChild(session);
        life.Health = 3;

        FrontlineStepResult step = StartTransform(
            session,
            tick,
            life.ActorId);

        Assert.Equal(5, life.Health);
        Assert.Equal(
            5,
            Assert.Single(step.Events, value =>
                    value.Type == FrontlineMatchEventType.FormChanged)
                .NewHealth);
    }

    [Fact]
    public void Transform_IsUnavailableAndBlockedOnEveryForbiddenTileClass()
    {
        FrontlineMatchSession session = CreateSession();
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState life = PlaceChild(session);
        Position[] forbiddenClasses =
        [
            new(2, 3),  // own protected pad
            new(7, 2),  // objective
            new(4, 7),  // authored spawn-safety ray
            new(12, 3), // opposing protected pad
        ];

        foreach (Position position in forbiddenClasses)
        {
            life.Position = position;
            ActorObservation observation = Project(
                session,
                tick,
                life.ActorId);
            ObservedActionAvailability transform =
                observation.Actions.Single(action =>
                    action.ActionId == PublicActionIds.Transform);
            Assert.False(transform.Available);
            Assert.True(transform.AllowedFormTargets!.Value.IsEmpty);

            Dictionary<FrontlineActorId, ActorDecision> decisions =
                Waits(tick.ActiveActors);
            decisions[life.ActorId] =
                ActorDecision.Transform("turret");
            FrontlineStepResult step = session.StepActors(decisions);
            FrontlineActionResolution resolution =
                step.ActionResolutions.Single(value =>
                    value.ActorId == life.ActorId);
            Assert.Equal(PublicActionIds.Wait,
                resolution.ValidatedActionId);
            Assert.Equal(ActionResult.Blocked, resolution.Result);
            Assert.Equal("child-mobile", life.FormId);
            Assert.Null(life.PendingFormTransition);
            Assert.DoesNotContain(step.Events, value =>
                value.Type
                    == FrontlineMatchEventType.FormTransitionStarted);
            tick = session.PrepareTick();
        }
    }

    [Fact]
    public void PendingTransition_IsSharedAcrossSelfAllyAndVisibleEnemy()
    {
        FrontlineMatchSession session = CreateSession(windupTicks: 3);
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState child = PlaceChild(session);
        child.Position = new Position(7, 4);
        FrontlineLifeState enemy =
            session.State.GetActiveLife(new FrontlineActorId(1, 0, 0));
        enemy.Position = new Position(7, 6);
        enemy.Facing = Direction.North;
        StartTransform(session, tick, child.ActorId);

        FrontlineTickStart pendingTick = session.PrepareTick();
        ActorObservation self = Project(
            session,
            pendingTick,
            child.ActorId);
        ActorObservation ally = Project(
            session,
            pendingTick,
            new FrontlineActorId(0, 0, 0));
        ActorObservation opponent = Project(
            session,
            pendingTick,
            enemy.ActorId);
        ObservedFormTransition expected = Assert.IsType<
            ObservedFormTransition>(self.Self.PendingFormTransition);

        Assert.Equal(
            expected,
            ally.Allies.Single(value =>
                    value.ActorId
                        == ActorIdentity.FromFrontline(child.ActorId))
                .PendingFormTransition);
        Assert.Equal(
            expected,
            opponent.Enemies.Single(value =>
                    value.Actor.TeamId == 0
                    && value.Actor.UnitId == 1)
                .PendingFormTransition);
        Assert.All(
            self.TeamUnits.Where(unit => unit.UnitId == 1),
            unit => Assert.Equal("child-mobile", unit.FormId));
    }

    [Fact]
    public void Turret_HasOmnidirectionalVisionAndZeroObjectivePresence()
    {
        FrontlineMatchSession session = CreateSession();
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState child = PlaceChild(session);
        child.Position = new Position(7, 5);
        child.Facing = Direction.North;
        FrontlineLifeState enemy =
            session.State.GetActiveLife(new FrontlineActorId(1, 0, 0));
        enemy.Position = new Position(7, 7);
        Assert.DoesNotContain(
            Project(session, tick, child.ActorId).Enemies,
            value => value.Actor.TeamId == 1);

        StartTransform(session, tick, child.ActorId);
        FrontlineTickStart turretTick = session.PrepareTick();
        Assert.Contains(
            Project(session, turretTick, child.ActorId).Enemies,
            value => value.Actor.TeamId == 1);

        child.Position = new Position(7, 2);
        enemy.Position = new Position(13, 4);
        FrontlineStepResult step =
            session.StepActors(Waits(turretTick.ActiveActors));
        Assert.Null(session.State.Control.ClaimingTeamId);
        Assert.Equal(0, session.State.Control.CaptureProgress);
        Assert.DoesNotContain(step.Events, value =>
            value.Type
                == FrontlineMatchEventType.FrontlineProgressChanged);
    }

    [Fact]
    public void TurretFire_IsAbsoluteStraightStrictAndUnlimitedRangePersists()
    {
        FrontlineMatchSession session = CreateSession(
            maxTicks: 12,
            windupTicks: 1,
            shotRange: 0,
            shootCooldownTicks: 1);
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState life = PlaceChild(session);
        life.Facing = Direction.North;
        StartTransform(session, tick, life.ActorId);

        FrontlineTickStart fireTick = session.PrepareTick();
        ActorObservation observation = Project(session, fireTick, life.ActorId);
        Assert.Equal(
            [PublicActionIds.ShootDirection, PublicActionIds.Wait],
            observation.Actions
                .Where(action => action.Available)
                .Select(action => action.ActionId)
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.Equal(
            Enum.GetValues<ProjectileHeading>(),
            observation.Actions.Single(action =>
                    action.ActionId == PublicActionIds.ShootDirection)
                .AllowedProjectileHeadings!.Value.ToArray());
        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(fireTick.ActiveActors);
        decisions[life.ActorId] =
            ActorDecision.ShootDirection(ProjectileHeading.East);
        FrontlineStepResult fired = session.StepActors(decisions);

        FrontlineMatchEvent shot = Assert.Single(fired.Events, value =>
            value.Type == FrontlineMatchEventType.Shot
            && value.ActorId == life.ActorId);
        Assert.Equal(PublicActionIds.ShootDirection, shot.ActionId);
        Assert.Equal(ProjectileHeading.East, shot.ProjectileHeading);
        Assert.Equal(Direction.North, life.Facing);
        FrontlineProjectileState projectile =
            Assert.Single(session.State.Projectiles);
        Assert.Equal(ProjectileHeading.East, projectile.Heading);
        Assert.Null(projectile.ShotProgram);
        Assert.Null(projectile.ProgrammedPath);

        session.StepActors(Waits(session.PrepareTick().ActiveActors));
        Assert.Equal(new Position(10, 4),
            Assert.Single(session.State.Projectiles).Position);
        FrontlineTickStart cornerTick = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> corner =
            Waits(cornerTick.ActiveActors);
        corner[life.ActorId] =
            ActorDecision.ShootDirection(ProjectileHeading.NorthWest);
        FrontlineStepResult blockedCorner = session.StepActors(corner);
        FrontlineMatchEvent cornerShot = Assert.Single(
            blockedCorner.Events,
            value =>
                value.Type == FrontlineMatchEventType.Shot
                && value.ActorId == life.ActorId);
        Assert.Null(cornerShot.ProjectileId);
        Assert.Equal(ProjectileHeading.NorthWest,
            cornerShot.ProjectileHeading);
        Assert.Equal(Direction.North, life.Facing);
    }

    [Theory]
    [InlineData(ProjectileHeading.North)]
    [InlineData(ProjectileHeading.NorthEast)]
    [InlineData(ProjectileHeading.East)]
    [InlineData(ProjectileHeading.SouthEast)]
    [InlineData(ProjectileHeading.South)]
    [InlineData(ProjectileHeading.SouthWest)]
    [InlineData(ProjectileHeading.West)]
    [InlineData(ProjectileHeading.NorthWest)]
    public void TurretFire_AllEightHeadingsUseOneProjectileAndStandardResources(
        ProjectileHeading heading)
    {
        FrontlineMatchSession session = CreateSession(
            shootCooldownTicks: 2,
            maxEnergy: 4,
            shotEnergyCost: 2,
            energyRegenTicks: 100);
        FrontlineTickStart tick = SpawnChild(session);
        FrontlineLifeState life = PlaceChild(session);
        life.Position = new Position(7, 6);
        life.Facing = Direction.North;
        StartTransform(session, tick, life.ActorId);

        FrontlineTickStart fireTick = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(fireTick.ActiveActors);
        decisions[life.ActorId] =
            ActorDecision.ShootDirection(heading);
        FrontlineStepResult fired = session.StepActors(decisions);

        FrontlineMatchEvent shot = Assert.Single(fired.Events, value =>
            value.Type == FrontlineMatchEventType.Shot
            && value.ActorId == life.ActorId);
        FrontlineProjectileTraversal traversal =
            Assert.Single(fired.ProjectileTraversals);
        (int dx, int dy) = heading.Vector();
        Position spawn = life.Position.Offset(dx, dy);
        Assert.Equal(heading, shot.ProjectileHeading);
        Assert.Equal(spawn, shot.To);
        Assert.Equal([spawn], traversal.Path.ToArray());
        Assert.Equal(heading, traversal.Heading);
        Assert.Null(traversal.ShotProgram);
        Assert.Null(traversal.ProgrammedPath);
        Assert.Single(session.State.Projectiles);
        Assert.Equal(2, life.Cooldown);
        Assert.Equal(2, life.Energy);
        Assert.Equal(Direction.North, life.Facing);

        ActorObservation cooling = Project(
            session,
            session.PrepareTick(),
            life.ActorId);
        ObservedActionAvailability availability =
            cooling.Actions.Single(action =>
                action.ActionId == PublicActionIds.ShootDirection);
        Assert.False(availability.Available);
        Assert.True(
            availability.AllowedProjectileHeadings!.Value.IsEmpty);
    }

    [Fact]
    public void OpenGroundOneVersusOne_DiagnosesTurretCadenceAndSurvival()
    {
        (
            FrontlineMatchSession session,
            FrontlineTickStart tick,
            FrontlineActorId turret,
            FrontlineActorId[] attackers) = PrepareTurretCombat(
                twoAttackers: false);

        List<FrontlineMatchEvent> events = RunAdjacentCombat(
            session,
            tick,
            turret,
            attackers);

        FrontlineActorId attacker = Assert.Single(attackers);
        Assert.Equal(
            3,
            events.Where(value =>
                    value.Type == FrontlineMatchEventType.Damage
                    && value.ActorId == attacker)
                .Sum(value => value.Amount));
        Assert.Equal(
            2,
            events.Where(value =>
                    value.Type == FrontlineMatchEventType.Damage
                    && value.ActorId == turret)
                .Sum(value => value.Amount));
        Assert.Contains(events, value =>
            value.Type == FrontlineMatchEventType.Destroyed
            && value.ActorId == attacker);
        Assert.DoesNotContain(events, value =>
            value.Type == FrontlineMatchEventType.Destroyed
            && value.ActorId == turret);
        Assert.Equal(
            3,
            session.State.GetUnit(0, 1).ActiveLife!.Health);
        Assert.Equal(
            FrontlineLifecycleStatus.Rebuilding,
            session.State.GetUnit(1, 1).LifecycleStatus);
    }

    [Fact]
    public void OpenGroundTwoVersusOne_DiagnosesCoordinatedDismantling()
    {
        (
            FrontlineMatchSession session,
            FrontlineTickStart tick,
            FrontlineActorId turret,
            FrontlineActorId[] attackers) = PrepareTurretCombat(
                twoAttackers: true);

        List<FrontlineMatchEvent> events = RunAdjacentCombat(
            session,
            tick,
            turret,
            attackers);

        Assert.Equal(
            5,
            events.Where(value =>
                    value.Type == FrontlineMatchEventType.Damage
                    && value.ActorId == turret)
                .Sum(value => value.Amount));
        Assert.Equal(
            4,
            events.Where(value =>
                    value.Type == FrontlineMatchEventType.Damage
                    && attackers.Contains(value.ActorId!.Value))
                .Sum(value => value.Amount));
        Assert.Contains(events, value =>
            value.Type == FrontlineMatchEventType.Destroyed
            && value.ActorId == attackers[0]);
        Assert.Contains(events, value =>
            value.Type == FrontlineMatchEventType.Destroyed
            && value.ActorId == turret);
        Assert.Null(session.State.GetUnit(0, 1).ActiveLife);
        Assert.Null(session.State.GetUnit(1, 1).ActiveLife);
        Assert.Equal(
            2,
            session.State.GetUnit(1, 2).ActiveLife!.Health);
        Assert.Equal(4, session.State.GetUnit(0, 1).DamageDealt);
        Assert.Equal(5, session.State.GetTeam(1).DamageDealt);
    }

    private static FrontlineMatchSession CreateSession(
        int maxTicks = 20,
        int windupTicks = 1,
        int shotRange = 8,
        int shootCooldownTicks = 0,
        int maxEnergy = 0,
        int shotEnergyCost = 0,
        int energyRegenTicks = 0,
        int anchorHealthGain = 2)
    {
        GameRules baseline = FrontlineTestDefinitions.ReplicationRules(
            maxTicks: maxTicks,
            firstUnlockTick: 1,
            secondUnlockTick: 2,
            shootCooldownTicks: shootCooldownTicks);
        GameRules rules = baseline with
        {
            ShotRange = shotRange,
            DamagePerHit = 1,
            MaxEnergy = maxEnergy,
            ShotEnergyCost = shotEnergyCost,
            EnergyRegenTicks = energyRegenTicks,
            AllowProgrammedShots = shotRange != 0,
            Frontline = baseline.Frontline! with
            {
                AnchorWindupTicks = windupTicks,
                AnchorHealthGain = anchorHealthGain,
                PrimeForm = baseline.Frontline.PrimeForm with
                {
                    AllowsProgrammedShots = shotRange != 0,
                },
                ChildForm = baseline.Frontline.ChildForm with
                {
                    AllowsProgrammedShots = shotRange != 0,
                },
                TurretForm = baseline.Frontline.TurretForm with
                {
                    ShootCooldownTicks = shootCooldownTicks,
                },
            },
        };
        return new FrontlineMatchSession(MatchDefinitionResolver.Resolve(
            rules,
            FrontlineTestDefinitions.AnchorMapV2()));
    }

    private static (
        FrontlineMatchSession Session,
        FrontlineTickStart CombatTick,
        FrontlineActorId Turret,
        FrontlineActorId[] Attackers)
        PrepareTurretCombat(bool twoAttackers)
    {
        FrontlineMatchSession session = CreateSession(
            maxTicks: 30,
            shootCooldownTicks: 1);
        session.StepActors(Waits(session.PrepareTick().ActiveActors));
        FrontlineTickStart unlocked = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> fabricate =
            Waits(unlocked.ActiveActors);
        fabricate[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 1));
        fabricate[new FrontlineActorId(1, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(1, 1));
        session.StepActors(fabricate);

        FrontlineTickStart transformTick = session.PrepareTick();
        FrontlineActorId turret =
            session.State.GetUnit(0, 1).ActiveLife!.ActorId;
        FrontlineActorId firstAttacker =
            session.State.GetUnit(1, 1).ActiveLife!.ActorId;
        session.State.GetActiveLife(turret).Position = new Position(6, 6);
        FrontlineLifeState first =
            session.State.GetActiveLife(firstAttacker);
        first.Position = new Position(7, 6);
        first.Facing = Direction.West;
        Dictionary<FrontlineActorId, ActorDecision> transform =
            Waits(transformTick.ActiveActors);
        transform[turret] = ActorDecision.Transform("turret");
        if (twoAttackers)
        {
            transform[new FrontlineActorId(1, 0, 0)] =
                ActorDecision.Fabricate(new ObservedUnitTarget(1, 2));
        }
        session.StepActors(transform);

        FrontlineTickStart combatTick = session.PrepareTick();
        FrontlineActorId[] attackers = twoAttackers
            ? [firstAttacker, session.State.GetUnit(1, 2).ActiveLife!.ActorId]
            : [firstAttacker];
        if (twoAttackers)
        {
            FrontlineLifeState second =
                session.State.GetActiveLife(attackers[1]);
            second.Position = new Position(6, 5);
            second.Facing = Direction.South;
        }
        Assert.Equal("turret",
            session.State.GetActiveLife(turret).FormId);
        return (session, combatTick, turret, attackers);
    }

    private static List<FrontlineMatchEvent> RunAdjacentCombat(
        FrontlineMatchSession session,
        FrontlineTickStart tick,
        FrontlineActorId turret,
        IReadOnlyList<FrontlineActorId> attackers)
    {
        var events = new List<FrontlineMatchEvent>();
        for (int round = 0; round < 12; round++)
        {
            Dictionary<FrontlineActorId, ActorDecision> decisions =
                Waits(tick.ActiveActors);
            FrontlineLifeState? turretLife =
                session.State.GetUnit(
                    turret.TeamId,
                    turret.UnitId).ActiveLife;
            if (turretLife is not null && turretLife.Cooldown == 0)
            {
                bool firstAttackerAlive =
                    session.State.GetUnit(
                        attackers[0].TeamId,
                        attackers[0].UnitId).ActiveLife is not null;
                decisions[turret] = ActorDecision.ShootDirection(
                    firstAttackerAlive
                        ? ProjectileHeading.East
                        : ProjectileHeading.North);
            }
            foreach (FrontlineActorId attacker in attackers)
            {
                FrontlineLifeState? life =
                    session.State.GetUnit(
                        attacker.TeamId,
                        attacker.UnitId).ActiveLife;
                if (life is not null && life.Cooldown == 0)
                    decisions[attacker] = ActorDecision.Shoot();
            }

            FrontlineStepResult step = session.StepActors(decisions);
            events.AddRange(step.Events);
            bool turretAlive =
                session.State.GetUnit(
                    turret.TeamId,
                    turret.UnitId).ActiveLife is not null;
            bool anyAttackerAlive = attackers.Any(attacker =>
                session.State.GetUnit(
                    attacker.TeamId,
                    attacker.UnitId).ActiveLife is not null);
            if (!turretAlive || !anyAttackerAlive)
                break;
            tick = session.PrepareTick();
        }
        return events;
    }

    private static FrontlineTickStart SpawnChild(
        FrontlineMatchSession session)
    {
        session.StepActors(Waits(session.PrepareTick().ActiveActors));
        FrontlineTickStart unlocked = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> fabricate =
            Waits(unlocked.ActiveActors);
        fabricate[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 1));
        session.StepActors(fabricate);
        return session.PrepareTick();
    }

    private static FrontlineLifeState PlaceChild(
        FrontlineMatchSession session)
    {
        FrontlineLifeState life =
            session.State.GetUnit(0, 1).ActiveLife!;
        life.Position = new Position(7, 4);
        return life;
    }

    private static FrontlineStepResult StartTransform(
        FrontlineMatchSession session,
        FrontlineTickStart tick,
        FrontlineActorId actorId)
    {
        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(tick.ActiveActors);
        decisions[actorId] = ActorDecision.Transform("turret");
        return session.StepActors(decisions);
    }

    private static ActorObservation Project(
        FrontlineMatchSession session,
        FrontlineTickStart tick,
        FrontlineActorId actorId) =>
        new FrontlineObservationProjector()
            .Project(
                session.State,
                tick,
                Array.Empty<FrontlineMatchEvent>(),
                PublicRulesManifestFactory.CreateMatchContract(
                    session.State.Definition.Rules,
                    session.State.Definition.Map,
                    session.State.Definition.Topology))
            .Actors.Single(value =>
                value.Self.ActorId == ActorIdentity.FromFrontline(actorId));

    private static Dictionary<FrontlineActorId, ActorDecision> Waits(
        IEnumerable<FrontlineActorId> actors) =>
        actors.ToDictionary(actor => actor, _ => ActorDecision.Wait());

    private static int FindIndex(
        IReadOnlyList<FrontlineMatchEvent> events,
        Func<FrontlineMatchEvent, bool> predicate)
    {
        for (int index = 0; index < events.Count; index++)
        {
            if (predicate(events[index]))
                return index;
        }
        return -1;
    }
}
