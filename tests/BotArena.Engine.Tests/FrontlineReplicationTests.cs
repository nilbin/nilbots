using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public sealed class FrontlineReplicationTests
{
    [Fact]
    public void Fabrication_UnlocksExplicitTargetAndCreatesChildNextTick()
    {
        FrontlineMatchSession session = CreateSession();
        FrontlineTickStart tick0 = session.PrepareTick();
        FrontlineUnitState child = session.State.GetUnit(0, 1);

        Assert.Equal(FrontlineLifecycleStatus.Locked, child.LifecycleStatus);
        Assert.Equal(1, child.UnlockAtTick);
        ActorObservation beforeUnlock = Project(session, tick0)
            .Actors.Single(actor => actor.Self.ActorId == new ActorIdentity(0, 0, 0));
        ObservedActionAvailability beforeAction = beforeUnlock.Actions.Single(
            action => action.ActionId == PublicActionIds.Fabricate);
        Assert.False(beforeAction.Available);
        Assert.Empty(Assert.IsType<
            System.Collections.Immutable.ImmutableArray<ObservedUnitTarget>>(
                beforeAction.AllowedUnitTargets));

        session.StepActors(Waits(tick0.ActiveActors));
        FrontlineTickStart tick1 = session.PrepareTick();
        Assert.Contains(
            tick1.Events,
            value => value.Type == FrontlineMatchEventType.FabricationUnlocked
                && value.TeamId == 0
                && value.UnitId == 1);
        Assert.Equal(
            1,
            child.UnlockAtTick); // Historical first-ready tick remains public provenance.
        ActorObservation unlocked = Project(session, tick1)
            .Actors.Single(actor => actor.Self.ActorId == new ActorIdentity(0, 0, 0));
        ObservedActionAvailability action = unlocked.Actions.Single(
            value => value.ActionId == PublicActionIds.Fabricate);
        Assert.True(action.Available);
        Assert.Equal(
            [new ObservedUnitTarget(0, 1)],
            action.AllowedUnitTargets!.Value.ToArray());

        Position primePosition = session.State.GetActiveLife(
            new FrontlineActorId(0, 0, 0)).Position;
        Dictionary<FrontlineActorId, ActorDecision> decisions = Waits(
            tick1.ActiveActors);
        decisions[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 1));
        FrontlineStepResult queued = session.StepActors(decisions);
        FrontlineActionResolution resolution =
            queued.ActionResolutions.Single(value =>
                value.ActorId == new FrontlineActorId(0, 0, 0));

        Assert.Equal(PublicActionIds.Fabricate, resolution.ChosenActionId);
        Assert.Equal(PublicActionCodes.Fabricate, resolution.ChosenActionCode);
        Assert.Equal(PublicActionIds.Fabricate, resolution.ValidatedActionId);
        Assert.Equal(ActionResult.Success, resolution.Result);
        Assert.Equal(primePosition, session.State.GetActiveLife(
            new FrontlineActorId(0, 0, 0)).Position);
        Assert.Equal(
            FrontlineLifecycleStatus.FabricationQueued,
            child.LifecycleStatus);
        Assert.Equal(2, child.FabricationAtTick);
        Assert.Equal(new Position(1, 4), child.ReservedSpawn);
        Assert.Equal(2, queued.TickStart.ActiveActors.Count);

        FrontlineTickStart tick2 = session.PrepareTick();
        var childActor = new FrontlineActorId(0, 1, 0);
        Assert.Contains(childActor, tick2.ActiveActors);
        Assert.Contains(
            tick2.SpawnedLives,
            value => value == new FrontlineLifeSpawn(
                childActor,
                ActorSpawnReason.Fabrication));
        Assert.Equal(FrontlineLifecycleStatus.Active, child.LifecycleStatus);
        Assert.Equal(
            session.State.Definition.FrontlineRules!.ChildForm.MaxHealth,
            child.ActiveLife!.Health);
        Assert.Equal(Direction.East, child.ActiveLife.Facing);
    }

    [Fact]
    public void Fabrication_BlockedFullPadLeavesReadySlotUnreserved()
    {
        var session = new FrontlineMatchSession(MatchDefinitionResolver.Resolve(
            FrontlineTestDefinitions.ReplicationRules(),
            FrontlineTestDefinitions.OpenMapV2()));
        session.StepActors(Waits(session.PrepareTick().ActiveActors));
        FrontlineTickStart tick1 = session.PrepareTick();
        ObservedActionAvailability masked = Project(session, tick1)
            .Actors.Single(actor =>
                actor.Self.ActorId == new ActorIdentity(0, 0, 0))
            .Actions.Single(action =>
                action.ActionId == PublicActionIds.Fabricate);
        Assert.True(masked.Available);
        Assert.Equal(
            [new ObservedUnitTarget(0, 1)],
            masked.AllowedUnitTargets!.Value.ToArray());
        Dictionary<FrontlineActorId, ActorDecision> decisions = Waits(
            tick1.ActiveActors);
        decisions[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 1));

        FrontlineStepResult step = session.StepActors(decisions);
        FrontlineActionResolution resolution =
            step.ActionResolutions.Single(value => value.ActorId.TeamId == 0);
        FrontlineUnitState child = session.State.GetUnit(0, 1);

        Assert.Equal(PublicActionIds.Fabricate, resolution.ChosenActionId);
        Assert.Equal(PublicActionIds.Wait, resolution.ValidatedActionId);
        Assert.Equal(ActionResult.Blocked, resolution.Result);
        Assert.Equal(FrontlineLifecycleStatus.Ready, child.LifecycleStatus);
        Assert.Null(child.FabricationAtTick);
        Assert.Null(child.ReservedSpawn);
        Assert.DoesNotContain(
            step.Events,
            value => value.Type == FrontlineMatchEventType.FabricationQueued
                && value.TeamId == 0);
    }

    [Fact]
    public void Fabrication_FullPreTickPadCanSucceedAfterAlliedMoveVacatesTile()
    {
        GameRules baseline = FrontlineTestDefinitions.ReplicationRules(
            maxTicks: 10);
        GameRules rules = baseline with
        {
            Frontline = baseline.Frontline! with
            {
                MaxUnitsPerTeam = 4,
                FabricationUnlockTicks = [1, 2, 3],
            },
        };
        FrontlineMatchSession session = CreateSession(rules);

        session.StepActors(Waits(session.PrepareTick().ActiveActors));
        FrontlineTickStart firstUnlock = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> first =
            Waits(firstUnlock.ActiveActors);
        first[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 1));
        session.StepActors(first);

        FrontlineTickStart secondUnlock = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> second =
            Waits(secondUnlock.ActiveActors);
        second[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 2));
        session.StepActors(second);

        FrontlineTickStart fullPad = session.PrepareTick();
        ObservedActionAvailability fabrication = Project(session, fullPad)
            .Actors.Single(actor =>
                actor.Self.ActorId == new ActorIdentity(0, 0, 0))
            .Actions.Single(action =>
                action.ActionId == PublicActionIds.Fabricate);
        Assert.True(fabrication.Available);
        Assert.Equal(
            [new ObservedUnitTarget(0, 3)],
            fabrication.AllowedUnitTargets!.Value.ToArray());

        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(fullPad.ActiveActors);
        decisions[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 3));
        decisions[new FrontlineActorId(0, 2, 0)] =
            ActorDecision.MoveForward();
        FrontlineStepResult step = session.StepActors(decisions);

        Assert.Equal(
            ActionResult.Success,
            step.ActionResolutions.Single(value =>
                value.ActorId == new FrontlineActorId(0, 0, 0)).Result);
        Assert.Equal(
            new Position(2, 4),
            session.State.GetUnit(0, 3).ReservedSpawn);
        Assert.Contains(
            step.Events,
            value =>
                value.Type == FrontlineMatchEventType.FabricationQueued
                && value.TeamId == 0
                && value.UnitId == 3);
    }

    [Fact]
    public void Fabrication_OffHomePadMaskAndResolverBothBlock()
    {
        FrontlineMatchSession session = CreateSession();
        FrontlineTickStart tick0 = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> opening =
            Waits(tick0.ActiveActors);
        opening[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.MoveForward();
        session.StepActors(opening);

        FrontlineTickStart tick1 = session.PrepareTick();
        ObservedActionAvailability masked = Project(session, tick1)
            .Actors.Single(actor =>
                actor.Self.ActorId == new ActorIdentity(0, 0, 0))
            .Actions.Single(action =>
                action.ActionId == PublicActionIds.Fabricate);
        Assert.False(masked.Available);
        Assert.Empty(masked.AllowedUnitTargets!.Value);

        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(tick1.ActiveActors);
        decisions[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 1));
        FrontlineStepResult step = session.StepActors(decisions);

        FrontlineActionResolution result = step.ActionResolutions.Single(
            value => value.ActorId == new FrontlineActorId(0, 0, 0));
        Assert.Equal(PublicActionIds.Fabricate, result.ChosenActionId);
        Assert.Equal(PublicActionIds.Wait, result.ValidatedActionId);
        Assert.Equal(ActionResult.Blocked, result.Result);
        Assert.Equal(
            FrontlineLifecycleStatus.Ready,
            session.State.GetUnit(0, 1).LifecycleStatus);
    }

    [Fact]
    public void DestroyedChild_BecomesReadyExactlyThenRefabricatesFreshLife()
    {
        FrontlineMatchSession session = CreateSession(
            FrontlineTestDefinitions.ReplicationRules(
                maxTicks: 12,
                childRebuildTicks: 2));
        SpawnFirstChild(session, teamIds: [0]);
        FrontlineTickStart tick2 = session.PrepareTick();
        FrontlineUnitState child = session.State.GetUnit(0, 1);
        child.ActiveLife!.Health = 0;

        session.StepActors(Waits(tick2.ActiveActors));
        Assert.Equal(FrontlineLifecycleStatus.Rebuilding, child.LifecycleStatus);
        Assert.Equal(5, child.RebuildReadyAtTick);
        Assert.Null(child.ActiveLife);

        for (int expectedTick = 3; expectedTick < 5; expectedTick++)
        {
            FrontlineTickStart waiting = session.PrepareTick();
            Assert.Equal(expectedTick, waiting.Tick);
            Assert.Equal(
                FrontlineLifecycleStatus.Rebuilding,
                child.LifecycleStatus);
            session.StepActors(Waits(waiting.ActiveActors));
        }

        FrontlineTickStart ready = session.PrepareTick();
        Assert.Equal(5, ready.Tick);
        Assert.Equal(FrontlineLifecycleStatus.Ready, child.LifecycleStatus);
        Assert.Contains(
            ready.Events,
            value => value.Type == FrontlineMatchEventType.RebuildReady
                && value.TeamId == 0
                && value.UnitId == 1);
        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(ready.ActiveActors);
        decisions[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 1));
        session.StepActors(decisions);

        FrontlineTickStart rebuilt = session.PrepareTick();
        var life1 = new FrontlineActorId(0, 1, 1);
        Assert.Contains(life1, rebuilt.ActiveActors);
        Assert.Contains(
            rebuilt.SpawnedLives,
            value => value == new FrontlineLifeSpawn(
                life1,
                ActorSpawnReason.Rebuild));
        Assert.Equal(2, child.NextLifeId);
    }

    [Fact]
    public void PrimeSpawn_RemainsReservedWhilePrimeIsRespawning()
    {
        FrontlineMatchSession session = CreateSession(
            FrontlineTestDefinitions.ReplicationRules(
                maxTicks: 10,
                primeRespawnTicks: 3));
        SpawnFirstChild(session, teamIds: [0]);
        FrontlineTickStart tick2 = session.PrepareTick();
        FrontlineUnitState prime = session.State.GetUnit(0, 0);
        FrontlineUnitState child = session.State.GetUnit(0, 1);
        prime.ActiveLife!.Health = 0;
        session.StepActors(Waits(tick2.ActiveActors));
        Assert.Equal(6, prime.RespawnAtTick);

        FrontlineTickStart tick3 = session.PrepareTick();
        child.ActiveLife!.Position = new Position(2, 3);
        child.ActiveLife.Facing = Direction.West;
        Dictionary<FrontlineActorId, ActorDecision> movement =
            Waits(tick3.ActiveActors);
        movement[child.ActiveLife.ActorId] = ActorDecision.MoveForward();
        FrontlineStepResult blocked = session.StepActors(movement);
        Assert.Equal(
            ActionResult.Blocked,
            blocked.ActionResolutions.Single(value =>
                value.ActorId == child.ActiveLife.ActorId).Result);
        Assert.Equal(new Position(2, 3), child.ActiveLife.Position);

        while (session.State.Tick < 6)
        {
            FrontlineTickStart waiting = session.PrepareTick();
            session.StepActors(Waits(waiting.ActiveActors));
        }
        FrontlineTickStart respawn = session.PrepareTick();
        Assert.Contains(new FrontlineActorId(0, 0, 1), respawn.ActiveActors);
        Assert.Equal(new Position(1, 3), prime.ActiveLife!.Position);
        Assert.NotEqual(prime.ActiveLife.Position, child.ActiveLife!.Position);
    }

    [Fact]
    public void Fabrication_InvalidForeignTargetRejectsAtomically()
    {
        FrontlineMatchSession session = CreateSession();
        session.StepActors(Waits(session.PrepareTick().ActiveActors));
        FrontlineTickStart tick1 = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> invalid =
            Waits(tick1.ActiveActors);
        invalid[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(1, 1));

        Assert.Throws<ArgumentException>(() => session.StepActors(invalid));
        Assert.Equal(1, session.State.Tick);
        Assert.Equal(
            FrontlineLifecycleStatus.Ready,
            session.State.GetUnit(0, 1).LifecycleStatus);
        Assert.Same(tick1, session.PrepareTick());

        session.StepActors(Waits(tick1.ActiveActors));
        Assert.Equal(2, session.State.Tick);
    }

    [Fact]
    public void SixBodies_ResolveCollisionAndAlliedPassThroughFocusFireCanonically()
    {
        GameRules rules = FrontlineTestDefinitions.ReplicationRules(
                maxTicks: 10,
                shootCooldownTicks: 0) with
        {
            DamagePerHit = 1,
            ProgrammedShotLaunchTiles = 8,
        };
        FrontlineMatchSession session = CreateSession(rules);
        SpawnAllChildren(session);
        FrontlineTickStart collisionTick = session.PrepareTick();
        Assert.Equal(6, collisionTick.ActiveActors.Count);

        FrontlineLifeState left = session.State.GetUnit(0, 0).ActiveLife!;
        FrontlineLifeState right = session.State.GetUnit(0, 1).ActiveLife!;
        left.Position = new Position(3, 3);
        left.Facing = Direction.East;
        right.Position = new Position(5, 3);
        right.Facing = Direction.West;
        Dictionary<FrontlineActorId, ActorDecision> collision =
            Waits(collisionTick.ActiveActors);
        collision[left.ActorId] = ActorDecision.MoveForward();
        collision[right.ActorId] = ActorDecision.MoveForward();

        FrontlineStepResult collisionStep = session.StepActors(collision);
        Assert.Equal(
            [left.ActorId, right.ActorId],
            collisionStep.ActionResolutions
                .Where(value => value.Result == ActionResult.Blocked)
                .Select(value => value.ActorId)
                .ToArray());

        FrontlineTickStart fireTick = session.PrepareTick();
        FrontlineLifeState[] shooters =
        [
            session.State.GetUnit(0, 0).ActiveLife!,
            session.State.GetUnit(0, 1).ActiveLife!,
            session.State.GetUnit(0, 2).ActiveLife!,
        ];
        for (int index = 0; index < shooters.Length; index++)
        {
            shooters[index].Position = new Position(index + 1, 2);
            shooters[index].Facing = Direction.East;
        }
        FrontlineLifeState target = session.State.GetUnit(1, 0).ActiveLife!;
        target.Position = new Position(5, 2);
        session.State.GetUnit(1, 1).ActiveLife!.Position =
            new Position(10, 4);
        session.State.GetUnit(1, 2).ActiveLife!.Position =
            new Position(11, 4);
        Dictionary<FrontlineActorId, ActorDecision> fire =
            Waits(fireTick.ActiveActors);
        foreach (FrontlineLifeState shooter in shooters)
            fire[shooter.ActorId] = ActorDecision.Shoot(ShotProgram.Straight);

        FrontlineStepResult focus = session.StepActors(fire);
        Assert.Equal(
            fireTick.ActiveActors.Order().ToArray(),
            focus.ActionResolutions.Select(value => value.ActorId).ToArray());
        Assert.Equal(
            [0, 1, 2],
            focus.Events
                .Where(value =>
                    value.Type == FrontlineMatchEventType.Damage
                    && value.ActorId == target.ActorId)
                .Select(value => value.OtherActorId!.Value.UnitId)
                .ToArray());
        Assert.Contains(
            focus.Events,
            value => value.Type == FrontlineMatchEventType.Destroyed
                && value.ActorId == target.ActorId);
        Assert.Equal(
            FrontlineLifecycleStatus.Respawning,
            session.State.GetUnit(1, 0).LifecycleStatus);
        FrontlineProjectileTraversal rearShot =
            focus.ProjectileTraversals.Single(value =>
                value.OwnerActorId == shooters[0].ActorId);
        Assert.Contains(new Position(2, 2), rearShot.Path);
        Assert.Contains(new Position(3, 2), rearShot.Path);
    }

    [Fact]
    public void Reset_RestoresPrimeAndLockedChildSlotLineage()
    {
        FrontlineMatchSession session = CreateSession();
        SpawnFirstChild(session, teamIds: [0]);
        session.Reset();

        Assert.Equal(0, session.State.Tick);
        foreach (FrontlineTeamState team in session.State.Teams)
        {
            Assert.Equal([0, 1, 2], team.Units.Select(unit => unit.UnitId));
            Assert.Equal(
                new FrontlineActorId(team.TeamId, 0, 0),
                team.GetUnit(0).ActiveLife!.ActorId);
            Assert.Equal(1, team.GetUnit(0).NextLifeId);
            Assert.Equal(
                [FrontlineLifecycleStatus.Locked, FrontlineLifecycleStatus.Locked],
                team.Units.Skip(1).Select(unit => unit.LifecycleStatus));
            Assert.All(team.Units.Skip(1), unit =>
            {
                Assert.Equal(0, unit.NextLifeId);
                Assert.False(unit.HasSpawned);
                Assert.Null(unit.ActiveLife);
                Assert.Null(unit.ReservedSpawn);
            });
        }
    }

    private static FrontlineMatchSession CreateSession(GameRules? rules = null) =>
        new(MatchDefinitionResolver.Resolve(
            rules ?? FrontlineTestDefinitions.ReplicationRules(),
            FrontlineTestDefinitions.ReplicationMapV2()));

    private static ActorObservationFrame Project(
        FrontlineMatchSession session,
        FrontlineTickStart tickStart)
    {
        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(
                session.State.Definition.Rules,
                session.State.Definition.Map,
                session.State.Definition.Topology);
        return new FrontlineObservationProjector().Project(
            session.State,
            tickStart,
            [],
            contract);
    }

    private static void SpawnFirstChild(
        FrontlineMatchSession session,
        IReadOnlyList<int> teamIds)
    {
        session.StepActors(Waits(session.PrepareTick().ActiveActors));
        FrontlineTickStart unlock = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(unlock.ActiveActors);
        foreach (int teamId in teamIds)
        {
            decisions[new FrontlineActorId(teamId, 0, 0)] =
                ActorDecision.Fabricate(new ObservedUnitTarget(teamId, 1));
        }
        session.StepActors(decisions);
    }

    private static void SpawnAllChildren(FrontlineMatchSession session)
    {
        SpawnFirstChild(session, [0, 1]);
        FrontlineTickStart secondUnlock = session.PrepareTick();
        Dictionary<FrontlineActorId, ActorDecision> decisions =
            Waits(secondUnlock.ActiveActors);
        decisions[new FrontlineActorId(0, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(0, 2));
        decisions[new FrontlineActorId(1, 0, 0)] =
            ActorDecision.Fabricate(new ObservedUnitTarget(1, 2));
        session.StepActors(decisions);
    }

    private static Dictionary<FrontlineActorId, ActorDecision> Waits(
        IReadOnlyList<FrontlineActorId> actors) =>
        actors.ToDictionary(actor => actor, _ => ActorDecision.Wait());
}
