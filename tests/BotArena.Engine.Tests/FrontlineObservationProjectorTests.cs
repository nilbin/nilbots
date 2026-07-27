using System.Reflection;
using System.Text.Json;
using BotArena.Engine;
using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public class FrontlineObservationProjectorTests
{
    private static readonly FrontlineActorId Team0Life0 = new(0, 0, 0);
    private static readonly FrontlineActorId Team1Life0 = new(1, 0, 0);
    private static readonly ActorIdentity ObservedTeam0Life0 = new(0, 0, 0);
    private static readonly ActorIdentity ObservedTeam1Life0 = new(1, 0, 0);

    [Fact]
    public void Project_IsCanonicalAndPrimeConeDoesNotLeakUnseenEnemyLifecycle()
    {
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen();
        var session = new FrontlineMatchSession(definition);
        PublicMatchContractManifest contract = CreateContract(definition);
        var projector = new FrontlineObservationProjector();
        FrontlineTickStart tickZero = session.PrepareTick();

        ActorObservationFrame initial =
            projector.Project(
                session.State,
                tickZero,
                [],
                contract);

        Assert.Equal(0, initial.Tick);
        Assert.Equal(
            [ObservedTeam0Life0, ObservedTeam1Life0],
            initial.Actors.Select(actor => actor.Self.ActorId).ToArray());
        ActorObservation teamZero = ObservationFor(
            initial,
            ObservedTeam0Life0);
        Assert.Equal(
            BotArenaVersions.ActorObservationSchemaVersion,
            teamZero.SchemaVersion);
        Assert.Equal(
            contract.MatchContractFingerprint,
            teamZero.MatchContractFingerprint);
        Assert.Equal(
            TeamPerceptionMode.ImmediateUnion,
            teamZero.TeamPerception);
        Assert.Equal(
            teamZero.VisibleTiles
                .OrderBy(tile => tile.Position.Y)
                .ThenBy(tile => tile.Position.X),
            teamZero.VisibleTiles);
        Assert.Equal(
            teamZero.Actions
                .OrderBy(action => action.ActionCode)
                .ThenBy(action => action.ActionId, StringComparer.Ordinal),
            teamZero.Actions);
        ObservedEnemy initiallyVisible = Assert.Single(teamZero.Enemies);
        Assert.Equal(
            new ObservedEnemyActorRef(1, 0, "enemy-life-0"),
            initiallyVisible.Actor);
        Assert.Equal(
            [ObservedTeam0Life0],
            initiallyVisible.ObservedBy.ToArray());
        Assert.All(
            teamZero.VisibleTiles,
            tile => Assert.Equal(
                [ObservedTeam0Life0],
                tile.ObservedBy.ToArray()));

        FrontlineStepResult turn = session.Step(new Dictionary<
            FrontlineActorId,
            BotDecision>
        {
            [Team0Life0] = BotDecision.Of(BotAction.TurnLeft),
            [Team1Life0] = BotDecision.Of(BotAction.Wait),
        });
        FrontlineTickStart tickOne = session.PrepareTick();
        ActorObservationFrame afterTurn =
            projector.Project(
                session.State,
                tickOne,
                turn.Events,
                contract);
        ActorObservation northFacing = ObservationFor(
            afterTurn,
            ObservedTeam0Life0);

        Assert.Equal(Direction.North, northFacing.Self.Facing);
        Assert.Empty(northFacing.Enemies);
        Assert.Empty(northFacing.Allies);
        ObservedUnitSlot ownSlot = Assert.Single(northFacing.TeamUnits);
        Assert.Equal(0, ownSlot.TeamId);
        Assert.Equal(ObservedTeam0Life0, ownSlot.ActiveActorId);
        Assert.DoesNotContain(
            northFacing.TeamUnits,
            unit => unit.TeamId == 1);
        Assert.DoesNotContain(
            northFacing.VisibleTiles,
            tile => tile.Position == new Position(7, 2));
    }

    [Fact]
    public void Project_DistinguishesUnsupportedCapabilitiesFromNoCurrentFacts()
    {
        ResolvedMatchDefinition supportedDefinition =
            FrontlineTestDefinitions.ResolveOpen();
        var supportedSession =
            new FrontlineMatchSession(supportedDefinition);
        var supportedProjector = new FrontlineObservationProjector();
        ActorObservationFrame supported =
            supportedProjector.Project(
                supportedSession.State,
                supportedSession.PrepareTick(),
                [],
                CreateContract(supportedDefinition));

        Assert.All(
            supported.Actors,
            observation =>
            {
                Assert.True(observation.VisibleProjectiles.HasValue);
                Assert.Empty(observation.VisibleProjectiles.Value);
                Assert.True(observation.HeardSounds.HasValue);
                Assert.Empty(observation.HeardSounds.Value);
            });

        GameRules unsupportedRules =
            FrontlineTestDefinitions.PrimeOnlyRules() with
            {
                HearingRadius = 0,
                ProjectileTicksPerTile = 0,
                AllowProgrammedShots = false,
            };
        ResolvedMatchDefinition unsupportedDefinition =
            FrontlineTestDefinitions.ResolveOpen(unsupportedRules);
        FrontlineRules frontline = unsupportedDefinition.FrontlineRules!;
        FrontlineMatchState unsupportedState = CreateSyntheticState(
            unsupportedDefinition,
            tick: 0,
            new SyntheticActor(
                Team0Life0,
                frontline.PrimeForm.FormId,
                new Position(1, 2),
                Direction.East,
                frontline.PrimeForm.MaxHealth),
            new SyntheticActor(
                Team1Life0,
                frontline.PrimeForm.FormId,
                new Position(7, 2),
                Direction.West,
                frontline.PrimeForm.MaxHealth));
        ActorObservationFrame unsupported =
            new FrontlineObservationProjector().Project(
                unsupportedState,
                CreateTickStart(unsupportedState),
                [],
                CreateContract(unsupportedDefinition));

        Assert.All(
            unsupported.Actors,
            observation =>
            {
                Assert.Null(observation.VisibleProjectiles);
                Assert.Null(observation.HeardSounds);
            });
    }

    [Fact]
    public void Project_RedactsPrivateProjectileProgramAndUnseenOwnerLife()
    {
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen();
        var session = new FrontlineMatchSession(definition);
        PublicMatchContractManifest contract = CreateContract(definition);
        var projector = new FrontlineObservationProjector();
        FrontlineTickStart tickZero = session.PrepareTick();
        FrontlineStepResult fired =
            session.Step(new Dictionary<FrontlineActorId, BotDecision>
            {
                [Team0Life0] = BotDecision.Of(BotAction.Wait),
                [Team1Life0] = BotDecision.Shoot(ShotProgram.Straight),
            });
        FrontlineTickStart tickOne = session.PrepareTick();
        ActorObservation ownerVisible = ObservationFor(
            projector.Project(
                session.State,
                tickOne,
                fired.Events,
                contract),
            ObservedTeam0Life0);
        ObservedActorProjectile initiallyObserved = Assert.Single(
            ownerVisible.VisibleProjectiles!.Value);
        Assert.Null(initiallyObserved.AlliedOwnerActorId);
        Assert.Equal(
            new ObservedEnemyActorRef(1, 0, "enemy-life-0"),
            initiallyObserved.VisibleEnemyOwner);
        Assert.Equal(
            "projectile-0",
            initiallyObserved.ProjectileHandle);

        session.Step(new Dictionary<FrontlineActorId, BotDecision>
        {
            [Team0Life0] = BotDecision.Of(BotAction.TurnLeft),
            [Team1Life0] = BotDecision.Of(BotAction.Wait),
        });
        FrontlineTickStart tickTwo = session.PrepareTick();
        FrontlineStepResult advanceIntoSight =
            session.Step(WaitDecisions(tickTwo));
        FrontlineTickStart tickThree = session.PrepareTick();

        FrontlineProjectileState authoritative =
            Assert.Single(session.State.Projectiles);
        Assert.NotNull(authoritative.ShotProgram);
        Assert.NotNull(authoritative.ProgrammedPath);
        Assert.Equal(new Position(2, 2), authoritative.Position);

        ActorObservationFrame frame =
            projector.Project(
                session.State,
                tickThree,
                advanceIntoSight.Events,
                contract);
        ActorObservation teamZero = ObservationFor(
            frame,
            ObservedTeam0Life0);
        ActorObservation teamOne = ObservationFor(
            frame,
            ObservedTeam1Life0);
        Assert.Empty(teamZero.Enemies);
        Assert.True(teamZero.VisibleProjectiles.HasValue);
        ObservedActorProjectile redacted =
            Assert.Single(teamZero.VisibleProjectiles.Value);
        Assert.Equal(1, redacted.OwnerTeamId);
        Assert.Null(redacted.AlliedOwnerActorId);
        Assert.Null(redacted.VisibleEnemyOwner);
        Assert.Equal("projectile-0", redacted.ProjectileHandle);
        Assert.Equal(new Position(2, 2), redacted.Position);
        Assert.Equal(
            [ObservedTeam0Life0],
            redacted.ObservedBy.ToArray());

        Assert.True(teamOne.VisibleProjectiles.HasValue);
        ObservedActorProjectile allied =
            Assert.Single(teamOne.VisibleProjectiles.Value);
        Assert.Equal(1, allied.OwnerTeamId);
        Assert.Equal(ObservedTeam1Life0, allied.AlliedOwnerActorId);
        Assert.Null(allied.VisibleEnemyOwner);

        string[] publicProperties = typeof(ObservedActorProjectile)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain("ShotProgram", publicProperties);
        Assert.DoesNotContain("ProgrammedPath", publicProperties);
        Assert.DoesNotContain("ProjectileId", publicProperties);
        Assert.DoesNotContain("LaunchDirection", publicProperties);
        Assert.DoesNotContain("OwnerActorId", publicProperties);
        string json = JsonSerializer.Serialize(redacted).ToLowerInvariant();
        Assert.DoesNotContain("shotprogram", json);
        Assert.DoesNotContain("programmedpath", json);
        Assert.DoesNotContain("launchdirection", json);
        Assert.Equal(ProjectileHeading.West, redacted.Heading);

        Position frozenPosition = redacted.Position;
        session.Step(WaitDecisions(tickThree));
        Assert.Empty(session.State.Projectiles);
        Assert.Equal(frozenPosition, redacted.Position);
    }

    [Fact]
    public void Project_UsesPrimaryVisibilityAndRedactsEventSecrets()
    {
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen();
        var session = new FrontlineMatchSession(definition);
        PublicMatchContractManifest contract = CreateContract(definition);
        var projector = new FrontlineObservationProjector();
        FrontlineTickStart tickZero = session.PrepareTick();
        session.Step(new Dictionary<FrontlineActorId, BotDecision>
        {
            [Team0Life0] = BotDecision.Of(BotAction.TurnLeft),
            [Team1Life0] = BotDecision.Of(BotAction.Wait),
        });
        FrontlineTickStart tickOne = session.PrepareTick();
        FrontlineMatchEvent[] priorEvents =
        [
            new FrontlineMatchEvent
            {
                Tick = 0,
                Type = FrontlineMatchEventType.Shot,
                TeamId = 1,
                ActorId = Team1Life0,
                OtherActorId = Team0Life0,
                ProjectileId = 99,
                From = new Position(7, 2),
                To = new Position(1, 2),
                FromFacing = Direction.West,
                ToFacing = Direction.West,
                ShotProgram = ShotProgram.Straight,
                Action = BotAction.Shoot,
                ActionResult = ActionResult.Success,
            },
            new FrontlineMatchEvent
            {
                Tick = 0,
                Type = FrontlineMatchEventType.Respawned,
                TeamId = 1,
                ActorId = new FrontlineActorId(1, 0, 1),
                To = new Position(7, 2),
                ToFacing = Direction.West,
                NewHealth = 3,
                LifecycleStatus = FrontlineLifecycleStatus.Active,
            },
            new FrontlineMatchEvent
            {
                Tick = 0,
                Type = FrontlineMatchEventType.Damage,
                TeamId = 0,
                ActorId = Team0Life0,
                OtherActorId = Team1Life0,
                ProjectileId = 99,
                From = new Position(1, 2),
                To = new Position(7, 2),
                ShotProgram = ShotProgram.Straight,
                Amount = 2,
                NewHealth = 1,
            },
            new FrontlineMatchEvent
            {
                Tick = 0,
                Type = FrontlineMatchEventType.Destroyed,
                TeamId = 1,
                ActorId = new FrontlineActorId(1, 0, 99),
                OtherActorId = Team0Life0,
                ProjectileId = 100,
                From = new Position(1, 1),
                To = new Position(7, 2),
                NewHealth = 0,
                LifecycleStatus = FrontlineLifecycleStatus.Respawning,
                RespawnAtTick = 20,
            },
        ];

        ActorObservationFrame frame =
            projector.Project(
                session.State,
                tickOne,
                priorEvents,
                contract);
        ActorObservation teamZero = ObservationFor(
            frame,
            ObservedTeam0Life0);

        Assert.Empty(teamZero.Enemies);
        Assert.Equal(
            ["event-0", "event-1"],
            teamZero.VisibleEvents
                .Select(matchEvent => matchEvent.EventHandle)
                .ToArray());
        ObservedMatchEvent damage = teamZero.VisibleEvents[0];
        Assert.Equal(ObservedMatchEventType.Damage, damage.Type);
        Assert.Equal(ObservedTeam0Life0, damage.AlliedActorId);
        Assert.Null(damage.EnemyActor);
        Assert.Equal(new Position(1, 2), damage.Position);
        Assert.Equal("projectile-0", damage.ProjectileHandle);
        Assert.Equal(2, damage.Amount);
        Assert.Equal(1, damage.NewHealth);
        ObservedMatchEvent destroyed = teamZero.VisibleEvents[1];
        Assert.Equal(ObservedMatchEventType.Destroyed, destroyed.Type);
        Assert.Null(destroyed.AlliedActorId);
        Assert.Equal(
            new ObservedEnemyActorRef(1, 0, "enemy-life-0"),
            destroyed.EnemyActor);
        Assert.Equal("projectile-1", destroyed.ProjectileHandle);
        Assert.Equal(new Position(1, 1), destroyed.Position);

        Assert.True(teamZero.HeardSounds.HasValue);
        ObservedActorSound heard =
            Assert.Single(teamZero.HeardSounds.Value);
        Assert.Equal("event-2", heard.EventHandle);
        Assert.Equal(ObservedMatchEventType.Shot, heard.Type);
        Assert.Equal(ObservedTeam0Life0, heard.ObserverActorId);
        Assert.Equal(2, heard.Bearing);
        Assert.Equal(2, heard.Distance);

        string[] publicProperties = typeof(ObservedMatchEvent)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain("OtherActorId", publicProperties);
        Assert.DoesNotContain("ShotProgram", publicProperties);
        Assert.DoesNotContain("To", publicProperties);
        Assert.DoesNotContain("SourceOrdinal", publicProperties);
        Assert.DoesNotContain("ProjectileId", publicProperties);
        Assert.DoesNotContain("ActorId", publicProperties);

        string observationJson = JsonSerializer.Serialize(teamZero);
        Assert.DoesNotContain("\"lifeId\":99", observationJson);
        Assert.DoesNotContain("\"projectileId\"", observationJson);
        ActorObservationReplayAliases replayAliases = frame.ReplayAliases
            .Single(value => value.ActorId == ObservedTeam0Life0);
        Assert.Equal(
            [new ActorIdentity(1, 0, 99)],
            replayAliases.EnemyLives
                .Select(value => value.ActorId)
                .ToArray());
        Assert.Equal(
            [99L, 100L],
            replayAliases.Projectiles
                .Select(value => value.ProjectileId)
                .ToArray());
        Assert.Equal(
            [
                "resolution:0:2",
                "resolution:0:3",
                "resolution:0:0",
            ],
            replayAliases.Events
                .Select(value => value.EventId)
                .ToArray());
    }

    [Fact]
    public void Project_ComputesStaticCooldownAndEnergyActionAvailability()
    {
        GameRules baseline =
            FrontlineTestDefinitions.PrimeOnlyRules(
                shootCooldownTicks: 1);
        FrontlineRules frontline = baseline.Frontline!;
        GameRules rules = baseline with
        {
            MaxEnergy = 1,
            ShotEnergyCost = 1,
            EnergyRegenTicks = 0,
            Frontline = frontline with
            {
                PrimeForm = frontline.PrimeForm with
                {
                    CanMove = false,
                    AllowsProgrammedShots = false,
                },
            },
        };
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen(rules);
        var session = new FrontlineMatchSession(definition);
        PublicMatchContractManifest contract = CreateContract(definition);
        var projector = new FrontlineObservationProjector();
        FrontlineTickStart tickZero = session.PrepareTick();
        ActorObservation initial = ObservationFor(
            projector.Project(
                session.State,
                tickZero,
                [],
                contract),
            ObservedTeam0Life0);

        ObservedActionAvailability wait = Action(initial, PublicActionIds.Wait);
        ObservedActionAvailability move = Action(
            initial,
            PublicActionIds.MoveForward);
        ObservedActionAvailability shoot = Action(
            initial,
            PublicActionIds.Shoot);
        Assert.True(wait.Enabled);
        Assert.True(wait.Available);
        Assert.Empty(wait.ParameterKinds);
        Assert.Null(wait.ShotProgramAvailable);
        Assert.True(move.Enabled);
        Assert.False(move.Available);
        Assert.True(shoot.Enabled);
        Assert.True(shoot.Available);
        Assert.Equal(
            [PublicActionParameterKind.ShotProgram],
            shoot.ParameterKinds.ToArray());
        Assert.False(shoot.ShotProgramAvailable);
        Assert.Null(shoot.AllowedDirections);
        Assert.Null(shoot.AllowedUnitTargets);
        Assert.Null(shoot.AllowedFormTargets);
        Assert.Equal(1, initial.Self.Energy);

        FrontlineStepResult fired = session.Step(new Dictionary<
            FrontlineActorId,
            BotDecision>
        {
            [Team0Life0] = BotDecision.Of(BotAction.Shoot),
            [Team1Life0] = BotDecision.Of(BotAction.Wait),
        });
        FrontlineTickStart tickOne = session.PrepareTick();
        ActorObservation coolingDown = ObservationFor(
            projector.Project(
                session.State,
                tickOne,
                fired.Events,
                contract),
            ObservedTeam0Life0);
        Assert.Equal(1, coolingDown.Self.Cooldown);
        Assert.Equal(0, coolingDown.Self.Energy);
        Assert.False(Action(
            coolingDown,
            PublicActionIds.Shoot).Available);

        FrontlineStepResult waited =
            session.Step(WaitDecisions(tickOne));
        FrontlineTickStart tickTwo = session.PrepareTick();
        ActorObservation outOfEnergy = ObservationFor(
            projector.Project(
                session.State,
                tickTwo,
                waited.Events,
                contract),
            ObservedTeam0Life0);
        Assert.Equal(0, outOfEnergy.Self.Cooldown);
        Assert.Equal(0, outOfEnergy.Self.Energy);
        Assert.False(Action(
            outOfEnergy,
            PublicActionIds.Shoot).Available);
    }

    [Fact]
    public void Project_ImmediateUnionCarriesExactSensorProvenance()
    {
        GameRules baseline =
            FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 100);
        FrontlineRules baselineFrontline = baseline.Frontline!;
        GameRules rules = baseline with
        {
            Frontline = baselineFrontline with
            {
                MaxUnitsPerTeam = 3,
                FabricationUnlockTicks = [5, 10],
            },
        };
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen(rules);
        FrontlineRules frontline = definition.FrontlineRules!;
        FrontlineMatchState state = CreateSyntheticState(
            definition,
            tick: 20,
            new SyntheticActor(
                new FrontlineActorId(0, 0, 0),
                frontline.PrimeForm.FormId,
                new Position(1, 2),
                Direction.North,
                frontline.PrimeForm.MaxHealth),
            new SyntheticActor(
                new FrontlineActorId(0, 1, 0),
                frontline.ChildForm.FormId,
                new Position(2, 2),
                Direction.East,
                frontline.ChildForm.MaxHealth),
            new SyntheticActor(
                new FrontlineActorId(0, 2, 0),
                frontline.ChildForm.FormId,
                new Position(1, 3),
                Direction.South,
                frontline.ChildForm.MaxHealth),
            new SyntheticActor(
                new FrontlineActorId(1, 0, 0),
                frontline.PrimeForm.FormId,
                new Position(7, 2),
                Direction.West,
                frontline.PrimeForm.MaxHealth),
            new SyntheticActor(
                new FrontlineActorId(1, 1, 0),
                frontline.ChildForm.FormId,
                new Position(7, 1),
                Direction.North,
                frontline.ChildForm.MaxHealth),
            new SyntheticActor(
                new FrontlineActorId(1, 2, 0),
                frontline.ChildForm.FormId,
                new Position(7, 3),
                Direction.South,
                frontline.ChildForm.MaxHealth));
        var projector = new FrontlineObservationProjector();
        ActorObservationFrame frame =
            projector.Project(
                state,
                CreateTickStart(state),
                [],
                CreateContract(definition));

        ActorIdentity contributingSensor = new(0, 1, 0);
        ActorObservation firstTeamZero = ObservationFor(
            frame,
            new ActorIdentity(0, 0, 0));
        ActorObservation secondTeamZero = ObservationFor(
            frame,
            contributingSensor);
        Assert.Equal(TeamPerceptionMode.ImmediateUnion,
            firstTeamZero.TeamPerception);
        Assert.Equal(3, firstTeamZero.TeamUnits.Length);
        Assert.Equal(2, firstTeamZero.Allies.Length);
        Assert.Equal(3, firstTeamZero.Enemies.Length);
        Assert.Equal(
            firstTeamZero.Enemies.Select(enemy => enemy.Actor),
            secondTeamZero.Enemies.Select(enemy => enemy.Actor));
        Assert.Equal(
            firstTeamZero.Enemies.SelectMany(enemy => enemy.ObservedBy),
            secondTeamZero.Enemies.SelectMany(enemy => enemy.ObservedBy));
        ObservedEnemy target = firstTeamZero.Enemies.Single(enemy =>
            enemy.Actor == new ObservedEnemyActorRef(
                1,
                0,
                "enemy-life-0"));
        Assert.Equal(
            [contributingSensor],
            target.ObservedBy.ToArray());
        ObservedMapTile targetTile =
            firstTeamZero.VisibleTiles.Single(tile =>
                tile.Position == new Position(7, 2));
        Assert.Equal(
            [contributingSensor],
            targetTile.ObservedBy.ToArray());
        ActorObservationReplayAliases firstAliases = frame.ReplayAliases
            .Single(value =>
                value.ActorId == firstTeamZero.Self.ActorId);
        ActorObservationReplayAliases secondAliases = frame.ReplayAliases
            .Single(value =>
                value.ActorId == secondTeamZero.Self.ActorId);
        Assert.Equal(
            firstAliases.EnemyLives
                .Select(value => (
                    value.LifeHandle,
                    value.ActorId.ToString()))
                .ToArray(),
            secondAliases.EnemyLives
                .Select(value => (
                    value.LifeHandle,
                    value.ActorId.ToString()))
                .ToArray());
    }

    [Fact]
    public void Project_AliasesHideGlobalGapsAndRemainStableAcrossTicks()
    {
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen();
        FrontlineRules frontline = definition.FrontlineRules!;
        var enemyLife37 = new FrontlineActorId(1, 0, 37);
        FrontlineProjectileState hiddenProjectile =
            InvokeInternalConstructor<FrontlineProjectileState>(
                5L,
                enemyLife37,
                new Position(1, 4),
                Direction.West,
                null,
                null,
                null);
        FrontlineProjectileState visibleProjectile =
            InvokeInternalConstructor<FrontlineProjectileState>(
                900L,
                enemyLife37,
                new Position(2, 2),
                Direction.West,
                null,
                null,
                null);
        FrontlineMatchState state = CreateSyntheticStateCore(
            definition,
            tick: 0,
            [
                new SyntheticActor(
                    Team0Life0,
                    frontline.PrimeForm.FormId,
                    new Position(1, 2),
                    Direction.East,
                    frontline.PrimeForm.MaxHealth),
                new SyntheticActor(
                    enemyLife37,
                    frontline.PrimeForm.FormId,
                    new Position(7, 2),
                    Direction.West,
                    frontline.PrimeForm.MaxHealth),
            ],
            [hiddenProjectile, visibleProjectile],
            nextProjectileId: 901);
        var projector = new FrontlineObservationProjector();
        PublicMatchContractManifest contract = CreateContract(definition);

        ActorObservationFrame first = projector.Project(
            state,
            CreateTickStart(state),
            [],
            contract);
        ActorObservationFrame repeated = projector.Project(
            state,
            CreateTickStart(state),
            [],
            contract);
        ActorObservation teamZero = ObservationFor(
            first,
            ObservedTeam0Life0);
        Assert.Equal(
            new ObservedEnemyActorRef(1, 0, "enemy-life-0"),
            Assert.Single(teamZero.Enemies).Actor);
        ObservedActorProjectile observedProjectile =
            Assert.Single(teamZero.VisibleProjectiles!.Value);
        Assert.Equal("projectile-0", observedProjectile.ProjectileHandle);
        Assert.Equal(
            new ObservedEnemyActorRef(1, 0, "enemy-life-0"),
            observedProjectile.VisibleEnemyOwner);
        Assert.Equal(
            JsonSerializer.Serialize(teamZero),
            JsonSerializer.Serialize(ObservationFor(
                repeated,
                ObservedTeam0Life0)));
        ActorObservationReplayAliases firstJoin = first.ReplayAliases
            .Single(value => value.ActorId == ObservedTeam0Life0);
        ActorObservationReplayAliases repeatedJoin = repeated.ReplayAliases
            .Single(value => value.ActorId == ObservedTeam0Life0);
        Assert.Equal(
            firstJoin.EnemyLives
                .Select(value => (
                    value.LifeHandle,
                    value.ActorId.ToString()))
                .ToArray(),
            repeatedJoin.EnemyLives
                .Select(value => (
                    value.LifeHandle,
                    value.ActorId.ToString()))
                .ToArray());
        Assert.Equal(
            firstJoin.Projectiles
                .Select(value => (
                    value.ProjectileHandle,
                    value.ProjectileId))
                .ToArray(),
            repeatedJoin.Projectiles
                .Select(value => (
                    value.ProjectileHandle,
                    value.ProjectileId))
                .ToArray());
        Assert.Equal(
            firstJoin.Events
                .Select(value => (value.EventHandle, value.EventId))
                .ToArray(),
            repeatedJoin.Events
                .Select(value => (value.EventHandle, value.EventId))
                .ToArray());

        state.Tick = 1;
        state.Control = state.Control with { NextTick = 1 };
        ActorObservation sameLifeNextTick = ObservationFor(
            projector.Project(
                state,
                CreateTickStart(state),
                [],
                contract),
            ObservedTeam0Life0);
        Assert.Equal(
            "enemy-life-0",
            Assert.Single(sameLifeNextTick.Enemies).Actor.LifeHandle);
        Assert.Equal(
            "projectile-0",
            Assert.Single(sameLifeNextTick.VisibleProjectiles!.Value)
                .ProjectileHandle);

        FrontlineUnitState enemyUnit = state.GetUnit(1, 0);
        var enemyLife999 = new FrontlineActorId(1, 0, 999);
        enemyUnit.ActiveLife = InvokeInternalConstructor<FrontlineLifeState>(
            enemyLife999,
            frontline.PrimeForm.FormId,
            new Position(7, 2),
            Direction.West,
            frontline.PrimeForm.MaxHealth,
            2,
            definition.Rules.MaxEnergy);
        enemyUnit.NextLifeId = 1000;
        state.Tick = 2;
        state.Control = state.Control with { NextTick = 2 };
        ActorObservationFrame jumped = projector.Project(
            state,
            CreateTickStart(state),
            [],
            contract);
        ActorObservation afterJump = ObservationFor(
            jumped,
            ObservedTeam0Life0);
        Assert.Equal(
            new ObservedEnemyActorRef(1, 0, "enemy-life-1"),
            Assert.Single(afterJump.Enemies).Actor);
        ObservedActorProjectile oldLifeProjectile =
            Assert.Single(afterJump.VisibleProjectiles!.Value);
        Assert.Equal(
            "projectile-0",
            oldLifeProjectile.ProjectileHandle);
        Assert.Null(oldLifeProjectile.VisibleEnemyOwner);

        string publicJson = JsonSerializer.Serialize(afterJump);
        Assert.DoesNotContain("\"lifeId\":999", publicJson);
        Assert.DoesNotContain("\"projectileId\":900", publicJson);
        ActorObservationReplayAliases aliases = jumped.ReplayAliases
            .Single(value => value.ActorId == ObservedTeam0Life0);
        Assert.Equal(
            enemyLife999,
            Assert.Single(aliases.EnemyLives).ActorId.ToFrontline());
        Assert.Equal(900, Assert.Single(aliases.Projectiles).ProjectileId);
    }

    [Fact]
    public void Project_RejectsStaleHistoryAndDifferentValidContract()
    {
        ResolvedMatchDefinition definition =
            FrontlineTestDefinitions.ResolveOpen();
        var session = new FrontlineMatchSession(definition);
        PublicMatchContractManifest contract = CreateContract(definition);
        var projector = new FrontlineObservationProjector();
        FrontlineTickStart tickZero = session.PrepareTick();
        session.Step(WaitDecisions(tickZero));
        FrontlineTickStart tickOne = session.PrepareTick();
        var stale = new FrontlineMatchEvent
        {
            Tick = -1,
            Type = FrontlineMatchEventType.Move,
            TeamId = 0,
            ActorId = Team0Life0,
            From = new Position(1, 2),
            To = new Position(2, 2),
        };

        Assert.Throws<ArgumentException>(() =>
            projector.Project(
                session.State,
                tickOne,
                [stale],
                contract));
        projector.Project(
            session.State,
            tickOne,
            [],
            contract);
        projector.Project(
            session.State,
            tickOne,
            [],
            contract);
        var differentSession = new FrontlineMatchSession(definition);
        Assert.Throws<InvalidOperationException>(() =>
            projector.Project(
                differentSession.State,
                differentSession.PrepareTick(),
                [],
                contract));

        PublicRulesManifest alteredRules = contract.Rules with
        {
            RulesFingerprint = "",
            Vision = contract.Rules.Vision with
            {
                HearingRadius = contract.Rules.Vision.HearingRadius + 1,
            },
        };
        alteredRules = alteredRules with
        {
            RulesFingerprint = MatchContractFingerprint.ComputeRules(
                alteredRules,
                definition.Rules),
        };
        PublicMatchContractManifest alteredContract = contract with
        {
            MatchContractFingerprint = "",
            Rules = alteredRules,
        };
        alteredContract = alteredContract with
        {
            MatchContractFingerprint =
                MatchContractFingerprint.ComputeMatch(alteredContract),
        };

        Assert.Throws<ArgumentException>(() =>
            projector.Project(
                session.State,
                tickOne,
                [],
                alteredContract));
    }

    private static PublicMatchContractManifest CreateContract(
        ResolvedMatchDefinition definition) =>
        PublicRulesManifestFactory.CreateMatchContract(
            definition.Rules,
            definition.Map,
            definition.Topology);

    private static ActorObservation ObservationFor(
        ActorObservationFrame frame,
        ActorIdentity actorId) =>
        frame.Actors.Single(observation =>
            observation.Self.ActorId == actorId);

    private static ObservedActionAvailability Action(
        ActorObservation observation,
        string actionId) =>
        observation.Actions.Single(action => string.Equals(
            action.ActionId,
            actionId,
            StringComparison.Ordinal));

    private static IReadOnlyDictionary<FrontlineActorId, BotDecision>
        WaitDecisions(FrontlineTickStart tickStart) =>
        tickStart.ActiveActors.ToDictionary(
            actorId => actorId,
            _ => BotDecision.Of(BotAction.Wait));

    private static FrontlineTickStart CreateTickStart(
        FrontlineMatchState state)
    {
        FrontlineActorId[] actors = state.Teams
            .SelectMany(team => team.Units)
            .Where(unit => unit.ActiveLife is not null)
            .Select(unit => unit.ActiveLife!.ActorId)
            .Order()
            .ToArray();
        return new FrontlineTickStart(
            state.Tick,
            actors,
            Array.Empty<FrontlineActorId>(),
            Array.Empty<FrontlineMatchEvent>());
    }

    private static FrontlineMatchState CreateSyntheticState(
        ResolvedMatchDefinition definition,
        int tick,
        params SyntheticActor[] actors) =>
        CreateSyntheticStateCore(
            definition,
            tick,
            actors,
            Array.Empty<FrontlineProjectileState>(),
            nextProjectileId: 0);

    private static FrontlineMatchState CreateSyntheticStateCore(
        ResolvedMatchDefinition definition,
        int tick,
        IReadOnlyList<SyntheticActor> actors,
        IReadOnlyList<FrontlineProjectileState> projectiles,
        long nextProjectileId)
    {
        FrontlineRules frontline = definition.FrontlineRules!;
        FrontlineTeamState[] teams = actors
            .GroupBy(actor => actor.ActorId.TeamId)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                FrontlineUnitState[] units = group
                    .OrderBy(actor => actor.ActorId.UnitId)
                    .Select(actor =>
                    {
                        FrontlineLifeState life =
                            InvokeInternalConstructor<FrontlineLifeState>(
                                actor.ActorId,
                                actor.FormId,
                                actor.Position,
                                actor.Facing,
                                actor.Health,
                                tick,
                                definition.Rules.MaxEnergy);
                        return InvokeInternalConstructor<FrontlineUnitState>(
                            actor.ActorId.TeamId,
                            actor.ActorId.UnitId,
                            actor.FormId,
                            life,
                            actor.ActorId.LifeId + 1,
                            FrontlineLifecycleStatus.Active,
                            null);
                    })
                    .ToArray();
                return InvokeInternalConstructor<FrontlineTeamState>(
                    group.Key,
                    units);
            })
            .ToArray();
        FrontlineControlState control =
            FrontlineControlSystem.CreateInitial(frontline) with
            {
                NextTick = tick,
            };
        return InvokeInternalConstructor<FrontlineMatchState>(
            definition,
            teams,
            control,
            projectiles,
            nextProjectileId);
    }

    private static T InvokeInternalConstructor<T>(params object?[] arguments)
    {
        ConstructorInfo constructor = typeof(T)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (T)constructor.Invoke(arguments);
    }

    private sealed record SyntheticActor(
        FrontlineActorId ActorId,
        string FormId,
        Position Position,
        Direction Facing,
        int Health);
}
