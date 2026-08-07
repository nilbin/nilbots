using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// Pins the ROOTED windup (DECISIONS #221) and the lock-side cancels it
/// leaves standing (DECISIONS #215).
/// <para>
/// A declared strike is a commitment: the declarer may not walk out of its
/// own shot. The engine states that as a single mutually-exclusive rule
/// rather than as a movement prohibition — a move or strafe COMMAND during
/// the windup abandons the declare (no bolt, the dead-shooter precedent)
/// and the move itself proceeds. Everything else that cancels a strike is
/// about the LOCK: it died, it crossed the frozen wedge, or it left the
/// shooter's line of sight.
/// </para>
/// <para>
/// The duel arena puts one body on each side of an open lane. The strike
/// reach and the sight range are set independently per test, which is what
/// isolates "left the wedge" from "left sight": the enemy always takes the
/// same one step away, and only the two numbers decide which rule it broke.
/// </para>
/// </summary>
public sealed class GenericActorStrikeWindupRootingTests
{
    private const int WindupTicks = 2;
    private static readonly Position ShooterSpawn = new(1, 3);
    private static readonly Position EnemySpawn = new(5, 3);

    [Fact]
    public void RootedDeclarerFiresFromTheTileItDeclaredOn()
    {
        using Duel duel = Duel.Create(strikeRange: 6, visionRange: 8);

        duel.Step(shooter: Shoot(1, 0), enemy: Wait());
        duel.Step(shooter: Wait(), enemy: Wait());
        GenericActorMatchStepResult matured =
            duel.Step(shooter: Wait(), enemy: Wait());

        GenericActorProjectileTraversal bolt =
            Assert.Single(matured.ProjectileTraversals);
        Assert.Equal(ShooterSpawn, bolt.From);
        Assert.Equal(ShooterSpawn, duel.Shooter(matured).Position);
        Assert.Equal(2, duel.Enemy(matured).Health);
    }

    [Fact]
    public void MoveCommandDuringTheWindupAbandonsTheStrikeAndTheMoveProceeds()
    {
        using Duel duel = Duel.Create(strikeRange: 6, visionRange: 8);

        duel.Step(shooter: Shoot(1, 0), enemy: Wait());
        GenericActorMatchStepResult walked =
            duel.Step(shooter: Move(Direction.North), enemy: Wait());
        GenericActorMatchStepResult wouldHaveMatured =
            duel.Step(shooter: Wait(), enemy: Wait());

        // The move is untouched: the body stands one tile north of the tile
        // it declared on.
        Assert.Equal(
            ShooterSpawn.Offset(0, -1),
            duel.Shooter(walked).Position);
        // And the declare is simply gone. No bolt on the abandoning tick, no
        // bolt when the windup would have matured, and no damage ever.
        Assert.Empty(walked.ProjectileTraversals);
        Assert.Empty(wouldHaveMatured.ProjectileTraversals);
        Assert.Equal(3, duel.Enemy(wouldHaveMatured).Health);
    }

    [Fact]
    public void AbandonmentIsTheCommandEvenWhenTheMoveResolvesBlocked()
    {
        using Duel duel = Duel.Create(strikeRange: 6, visionRange: 8);

        duel.Step(shooter: Shoot(1, 0), enemy: Wait());
        // West of the shooter's spawn is the arena wall, so this move is
        // accepted, attempted and blocked. The decision to leave was still
        // made, and the windup is spent on it.
        GenericActorMatchStepResult blocked =
            duel.Step(shooter: Move(Direction.West), enemy: Wait());
        GenericActorMatchStepResult wouldHaveMatured =
            duel.Step(shooter: Wait(), enemy: Wait());

        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Blocked,
            blocked.ActionResolutions
                .Single(value => value.ActorId.TeamId == 0)
                .Resolution.Outcome);
        Assert.Equal(ShooterSpawn, duel.Shooter(blocked).Position);
        Assert.Empty(wouldHaveMatured.ProjectileTraversals);
        Assert.Equal(3, duel.Enemy(wouldHaveMatured).Health);
    }

    [Fact]
    public void LockLeavingTheFrozenWedgeStillCancels()
    {
        // Reach 4 puts the enemy on the wedge's last ring, so its one step
        // away is a step out of the frozen wedge — earned geometry, #215 —
        // while sight range 8 keeps it in plain view the whole time.
        using Duel duel = Duel.Create(strikeRange: 4, visionRange: 8);

        duel.Step(shooter: Shoot(1, 0), enemy: Wait());
        duel.Step(shooter: Wait(), enemy: Move(Direction.East));
        GenericActorMatchStepResult matured =
            duel.Step(shooter: Wait(), enemy: Wait());

        Assert.Equal(EnemySpawn.Offset(1, 0), duel.Enemy(matured).Position);
        Assert.Empty(matured.ProjectileTraversals);
        Assert.Equal(3, duel.Enemy(matured).Health);
    }

    [Fact]
    public void LockLeavingTheShootersSightStillCancels()
    {
        // The same step, with the two numbers swapped around it: reach 6
        // keeps the enemy inside the frozen wedge, and sight range 4 means
        // the shooter can no longer see the tile it would fire at.
        using Duel duel = Duel.Create(strikeRange: 6, visionRange: 4);

        duel.Step(shooter: Shoot(1, 0), enemy: Wait());
        duel.Step(shooter: Wait(), enemy: Move(Direction.East));
        GenericActorMatchStepResult matured =
            duel.Step(shooter: Wait(), enemy: Wait());

        Position escaped = EnemySpawn.Offset(1, 0);
        Assert.Equal(escaped, duel.Enemy(matured).Position);
        Assert.Contains(escaped, duel.Wedge(ShooterSpawn, 6));
        Assert.Empty(matured.ProjectileTraversals);
        Assert.Equal(3, duel.Enemy(matured).Health);
    }

    [Fact]
    public void DeadLockStillCancelsTheStrikeThatWasAimedAtIt()
    {
        // One health, two declares one tick apart: the first strike kills the
        // locked life, and the second finds its lock gone. The respawn is a
        // new lifeId, so nothing re-follows into it.
        using Duel duel = Duel.Create(
            strikeRange: 6,
            visionRange: 8,
            maxHealth: 1);

        duel.Step(shooter: Shoot(1, 0), enemy: Wait());
        duel.Step(shooter: Shoot(1, 0), enemy: Wait());
        GenericActorMatchStepResult killed =
            duel.Step(shooter: Wait(), enemy: Wait());
        GenericActorMatchStepResult orphaned =
            duel.Step(shooter: Wait(), enemy: Wait());

        Assert.Single(killed.ProjectileTraversals);
        Assert.DoesNotContain(
            killed.PostState.ActiveLives,
            life => life.ActorId.TeamId == 1 && life.ActorId.LifeId == 0);
        Assert.Empty(orphaned.ProjectileTraversals);
    }

    [Fact]
    public void LockIsTheNamedTargetAndNotTheNearestBodyInTheWedge()
    {
        // The shooter names the enemy four tiles down the lane. A second
        // enemy stands two tiles away inside the same wedge. The nearest
        // body used to be the lock; the NAMED target is.
        using Duel duel = Duel.CreateSquad(
            strikeRange: 6,
            shooter: new Position(1, 3),
            friendly: new Position(1, 1),
            enemyOne: new Position(5, 3),
            enemyTwo: new Position(3, 5));

        duel.Step((0, 0, Shoot(1, 0)));
        duel.Step();
        GenericActorMatchStepResult matured = duel.Step();

        Assert.Single(matured.ProjectileTraversals);
        Assert.Equal(2, duel.Body(matured, 1, 0).Health);
        Assert.Equal(3, duel.Body(matured, 1, 1).Health);
    }

    [Fact]
    public void LockIsTheNamedTargetAndNotTheInterposedEnemyOnTheRay()
    {
        // Owner correction 2026-08-08: "the lock is the target picked by the
        // MIND and nothing else." A second enemy stands ON the aimed ray,
        // nearer than the named one — the first-enemy-on-the-ray rule would
        // have locked IT — and then steps aside during the windup. Under the
        // named lock the strike still belongs to the far target, so the bolt
        // flies down the vacated line and hits the body it was declared for.
        using Duel duel = Duel.CreateSquad(
            strikeRange: 6,
            shooter: new Position(1, 3),
            friendly: new Position(1, 1),
            enemyOne: new Position(5, 3),
            enemyTwo: new Position(3, 3));

        duel.Step((0, 0, Shoot(1, 0)));
        duel.Step((1, 1, Move(Direction.North)));
        GenericActorMatchStepResult matured = duel.Step();

        Assert.Equal(new Position(3, 2), duel.Body(matured, 1, 1).Position);
        Assert.Single(matured.ProjectileTraversals);
        Assert.Equal(2, duel.Body(matured, 1, 0).Health);
        Assert.Equal(3, duel.Body(matured, 1, 1).Health);
    }

    [Fact]
    public void TargetSteppingOffTheAimedRayOnTheDeclareTickIsStillLocked()
    {
        // Movement resolves before attacks inside one tick, so the body a
        // mind aimed at from the tick's opening state has usually already
        // taken its step by the time the declare is read. Aim alignment
        // cannot survive that; the NAME can. The target steps north out of
        // the aimed lane on the declare tick itself and is locked anyway,
        // because it is still inside the frozen wedge.
        using Duel duel = Duel.CreateSquad(
            strikeRange: 6,
            shooter: new Position(1, 3),
            friendly: new Position(1, 1),
            enemyOne: new Position(5, 3),
            enemyTwo: new Position(7, 5));

        GenericActorMatchStepResult declared =
            duel.Step((0, 0, Shoot(1, 0)), (1, 0, Move(Direction.North)));
        duel.Step();
        GenericActorMatchStepResult matured = duel.Step();

        Assert.Equal(new Position(5, 2), duel.Body(declared, 1, 0).Position);
        Assert.Single(matured.ProjectileTraversals);
        Assert.Equal(2, duel.Body(matured, 1, 0).Health);
    }

    [Fact]
    public void DeclareThatNamesNobodyLocksNothingAndKeepsTheWhiff()
    {
        // A suppressive declare down an empty lane. There is no
        // substitution: it locks nothing and fires the theatrical whiff
        // down the centre, which hurts nobody.
        using Duel duel = Duel.CreateSquad(
            strikeRange: 6,
            shooter: new Position(1, 3),
            friendly: new Position(1, 1),
            enemyOne: new Position(3, 5),
            enemyTwo: new Position(7, 5));

        duel.Step((0, 0, Shoot()));
        duel.Step();
        GenericActorMatchStepResult matured = duel.Step();

        Assert.Single(matured.ProjectileTraversals);
        Assert.Equal(3, duel.Body(matured, 1, 0).Health);
        Assert.Equal(3, duel.Body(matured, 1, 1).Health);
    }

    [Fact]
    public void TargetOutsideTheFrozenWedgeAtDeclareLocksNothing()
    {
        // The wedge is still the boundary of what a strike may be for. Reach
        // 2 leaves the named enemy far outside it at declare, so the strike
        // locks nothing and whiffs instead of reaching across the map.
        using Duel duel = Duel.CreateSquad(
            strikeRange: 2,
            shooter: new Position(1, 3),
            friendly: new Position(1, 1),
            enemyOne: new Position(7, 3),
            enemyTwo: new Position(7, 5));

        duel.Step((0, 0, Shoot(1, 0)));
        duel.Step();
        GenericActorMatchStepResult matured = duel.Step();

        Assert.Single(matured.ProjectileTraversals);
        Assert.Equal(3, duel.Body(matured, 1, 0).Health);
        Assert.Equal(3, duel.Body(matured, 1, 1).Health);
    }

    [Fact]
    public void FriendlyOnTheAimIsNeverTheLock()
    {
        // A teammate stands on the aimed line between shooter and target and
        // then steps off it. A lock would have been cancelled by that step;
        // this strike was declared for the enemy behind it, so it still
        // fires and still hits.
        using Duel duel = Duel.CreateSquad(
            strikeRange: 6,
            shooter: new Position(1, 3),
            friendly: new Position(3, 3),
            enemyOne: new Position(5, 3),
            enemyTwo: new Position(7, 5));

        duel.Step((0, 0, Shoot(1, 0)));
        duel.Step((0, 1, Move(Direction.North)));
        GenericActorMatchStepResult matured = duel.Step();

        Assert.Equal(new Position(3, 2), duel.Body(matured, 0, 1).Position);
        Assert.Single(matured.ProjectileTraversals);
        Assert.Equal(3, duel.Body(matured, 0, 1).Health);
        Assert.Equal(2, duel.Body(matured, 1, 0).Health);
    }

    [Fact]
    public void BodyStepingOntoTheFiringLineTakesTheBoltMeantForItsAlly()
    {
        // Bodyguarding lives at DELIVERY. The second enemy is outside the
        // wedge at declare, so it can never have been the lock, and it walks
        // onto the firing line during the windup: the bolt meets it first
        // and the locked target is untouched.
        using Duel duel = Duel.CreateSquad(
            strikeRange: 6,
            shooter: new Position(1, 3),
            friendly: new Position(1, 1),
            enemyOne: new Position(5, 3),
            enemyTwo: new Position(2, 5));

        duel.Step((0, 0, Shoot(1, 0)));
        duel.Step((1, 1, Move(Direction.North)));
        GenericActorMatchStepResult matured =
            duel.Step((1, 1, Move(Direction.North)));

        Assert.Equal(new Position(2, 3), duel.Body(matured, 1, 1).Position);
        Assert.Equal(2, duel.Body(matured, 1, 1).Health);
        Assert.Equal(3, duel.Body(matured, 1, 0).Health);
    }

    private static GenericActorRuntimeDecision Wait() =>
        GenericDeathmatchSessionTestFixture.Wait();

    /// <summary>
    /// A strike declared east. Naming a unit is what LOCKS it (DECISIONS
    /// #222, owner correction); the parameterless overload is the
    /// suppressive declare that names nobody and locks nothing.
    /// </summary>
    private static GenericActorRuntimeDecision Shoot() =>
        new(
            "shoot",
            4,
            [
                new GenericActorRuntimeActionArgument
                    .ProjectileHeadingArgument(ProjectileHeading.East),
            ],
            null);

    private static GenericActorRuntimeDecision Shoot(int teamId, int unitId) =>
        new(
            "shoot",
            4,
            [
                new GenericActorRuntimeActionArgument
                    .ProjectileHeadingArgument(ProjectileHeading.East),
                new GenericActorRuntimeActionArgument.UnitTargetArgument(
                    new GenericActorRuntimeActionArgument.UnitTarget(
                        teamId,
                        unitId)),
            ],
            null);

    private static GenericActorRuntimeDecision Move(Direction direction) =>
        GenericDeathmatchSessionTestFixture.Move(direction);

    /// <summary>
    /// A two-body duel whose decisions are dictated one tick at a time. The
    /// shooter is team 0 at <see cref="ShooterSpawn"/> facing east; the enemy
    /// is team 1 at <see cref="EnemySpawn"/>, four tiles down the lane.
    /// </summary>
    private sealed class Duel : IDisposable
    {
        private readonly ActorResolvedMatchDefinition _definition;
        private readonly GenericActorMatchSession _session;
        private readonly Dictionary<
            (int TeamId, int UnitId),
            GenericActorRuntimeDecision> _commands = [];

        private Duel(ActorResolvedMatchDefinition definition)
        {
            _definition = definition;
            Dictionary<
                int,
                GenericDeathmatchSessionTestFixture.RecordingFactory>
                factories = GenericDeathmatchSessionTestFixture.Factories(
                    definition,
                    (_, observation) => _commands.TryGetValue(
                        (observation.Self.ActorId.TeamId,
                            observation.Self.ActorId.UnitId),
                        out GenericActorRuntimeDecision? command)
                        ? command
                        : GenericDeathmatchSessionTestFixture.Wait());
            _session = new GenericActorMatchSession(
                definition,
                GenericDeathmatchSessionTestFixture.Configurations(
                    definition,
                    factories),
                matchSeed: 20_260_807UL);
        }

        public static Duel Create(
            int strikeRange,
            int visionRange,
            int maxHealth = 3) =>
            new(Definition(
                "head-to-head",
                strikeRange,
                visionRange,
                maxHealth,
                [ShooterSpawn, EnemySpawn]));

        /// <summary>
        /// Four bodies, two per team, placed exactly: the shooter (0, 0), its
        /// friendly (0, 1), and two enemies (1, 0) and (1, 1). Only the
        /// shooter's spawn carries a facing that matters.
        /// </summary>
        public static Duel CreateSquad(
            int strikeRange,
            Position shooter,
            Position friendly,
            Position enemyOne,
            Position enemyTwo) =>
            new(Definition(
                "teams",
                strikeRange,
                visionRange: 8,
                maxHealth: 3,
                [shooter, friendly, enemyOne, enemyTwo]));

        public GenericActorMatchStepResult Step(
            GenericActorRuntimeDecision shooter,
            GenericActorRuntimeDecision enemy) =>
            Step((0, 0, shooter), (1, 0, enemy));

        /// <summary>
        /// Commands the named bodies for one tick; everything unnamed waits.
        /// </summary>
        public GenericActorMatchStepResult Step(
            params (int TeamId, int UnitId, GenericActorRuntimeDecision Decision)[]
                commands)
        {
            _commands.Clear();
            foreach ((int teamId, int unitId, GenericActorRuntimeDecision decision)
                     in commands)
            {
                _commands[(teamId, unitId)] = decision;
            }
            return _session.Step();
        }

        public GenericActorWorldSnapshot.LifeSnapshot Body(
            GenericActorMatchStepResult step,
            int teamId,
            int unitId) =>
            step.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == teamId && life.ActorId.UnitId == unitId);

        public GenericActorWorldSnapshot.LifeSnapshot Shooter(
            GenericActorMatchStepResult step) =>
            step.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == 0);

        public GenericActorWorldSnapshot.LifeSnapshot Enemy(
            GenericActorMatchStepResult step) =>
            step.PostState.ActiveLives.Single(life =>
                life.ActorId.TeamId == 1);

        public ImmutableArray<Position> Wedge(Position origin, int range) =>
            GenericActorStrikeCone.Tiles(
                _definition.Map,
                origin,
                ProjectileHeading.East,
                range,
                diagonalCornersMustBeClear: true);

        public void Dispose() => _session.Dispose();

        /// <summary>
        /// An instant ray carries no programmed path, so the strike profile
        /// declares the canonical inert program.
        /// </summary>
        private static ActorShotProgramDefinition DisabledShotProgram() =>
            new(
                enabled: false,
                headingSectors: 8,
                ActorShotHeadingModel.EightWayClockwiseModuloV1,
                bendStepSectors: 1,
                minInitialAimSteps: 0,
                maxInitialAimSteps: 0,
                new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
                allowedCurvedBendDirections: [-1, 1],
                minBendAfterTiles: 1,
                maxBendAfterTiles: 1,
                minBendEveryTiles: 1,
                maxBendEveryTiles: 1,
                minBendCount: 1,
                maxBendCount: 1,
                launchTiles: 1,
                payloadOptional: false,
                new ActorShotProgramValue(0, 0, 0, 1, 0),
                invalidPayloadResult: null,
                ActorActionRejectionResult.Blocked,
                diagonalCornersMustBeClear: true);

        private static ActorResolvedMatchDefinition Definition(
            string formatName,
            int strikeRange,
            int visionRange,
            int maxHealth,
            IReadOnlyList<Position> spawnPositions)
        {
            ActorResolvedMatchDefinition baseline =
                GenericDeathmatchSessionTestFixture.Definition(
                    formatName,
                    new GenericDeathmatchSessionTestFixture.Options
                    {
                        MaxTicks = 16,
                        MaxHealth = maxHealth,
                        VisionRange = visionRange,
                    });
            ActorAttackProfileDefinition baselineAttack =
                baseline.Rules.AttackProfiles.Single();
            // A declared strike: an instant ray that lights its cone now and
            // resolves two ticks later. An instant ray may carry no shot
            // program, so aim is an absolute submitted heading and the
            // fixture's `shoot` takes one instead of a program.
            var attack = new ActorAttackProfileDefinition(
                baselineAttack.Id,
                omnidirectionalAim: true,
                new ActorProjectileDefinition(
                    ActorProjectileMode.InstantRay,
                    damagePerHit: 1,
                    strikeRange,
                    ticksPerAdvance: 0,
                    tilesPerAdvance: 1,
                    launchTiles: 1,
                    advancesOnLaunchTick: false,
                    damageAppliedSimultaneously: true,
                    diagonalCornersMustBeClear: true,
                    strikeWindupTicks: WindupTicks),
                cooldownTicks: 0,
                maxEnergy: 0,
                attackEnergyCost: 0,
                energyRegenerationIntervalTicks: 0,
                energyRegenerationAmount: 0,
                DisabledShotProgram());
            var rules = new ActorRulesDefinition(
                "generic-actor-strike-rooting-fixture",
                baseline.Rules.Limits,
                baseline.Rules.SeedMechanics,
                baseline.Rules.GameMode,
                baseline.Rules.Lifecycle,
                baseline.Rules.Forms,
                baseline.Rules.MovementProfiles,
                baseline.Rules.VisionProfiles,
                [attack],
                [
                    .. baseline.Rules.Actions.Where(action =>
                        action.Kind != ActorActionKind.Attack),
                    new ActorActionDefinition(
                        "shoot",
                        4,
                        ActorActionKind.Attack,
                        [
                            ActorActionParameterKind.ProjectileHeading,
                            ActorActionParameterKind.UnitTarget,
                        ]),
                ],
                baseline.Rules.FabricationTransitions,
                baseline.Rules.SameLifeTransitions,
                baseline.Rules.ReplicationTransitions,
                baseline.Rules.TeamPerception,
                baseline.Rules.Collisions,
                baseline.Rules.TickResolution);
            // The deployment hands its spawn anchors out team by team, unit
            // by unit, in the order it declares them, so re-siting those
            // anchors in that same order places the bodies exactly.
            // (The deployment's own Spawns array is canonically ordered by
            // spawn id, which is NOT the deployment order — the lives are.)
            ImmutableArray<string> spawnIds =
            [
                .. baseline.InitialDeployment.Lives
                    .OrderBy(life => life.TeamId)
                    .ThenBy(life => life.UnitId)
                    .Select(life => life.SpawnId),
            ];
            InitialSpawnDefinition[] spawns =
            [
                .. spawnPositions.Select((position, index) =>
                    new InitialSpawnDefinition(
                        spawnIds[index],
                        position,
                        index == 0 ? Direction.East : Direction.West)),
            ];
            var map = new ActorMapDefinition(
                "strike-rooting-arena",
                version: 1,
                baseline.Map.TileRows,
                [
                    .. spawns.Select(spawn =>
                        new ActorMapSpawnAnchorDefinition(
                            spawn,
                            [ActorMovementLayer.Ground])),
                ],
                baseline.Map.Regions,
                baseline.Map.TileTags);
            return new ActorResolvedMatchDefinition(
                rules,
                map,
                baseline.Format,
                baseline.Topology,
                new InitialDeploymentDefinition(
                    [.. spawns],
                    baseline.InitialDeployment.Lives),
                baseline.LifecycleAssignments,
                baseline.ParticipantRegionAssignments,
                baseline.ModeMapBinding,
                baseline.CapabilityVersions);
        }
    }
}
