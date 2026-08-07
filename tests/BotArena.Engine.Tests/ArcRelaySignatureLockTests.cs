using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The locked bolt-class line signature (owner ruling 2026-08-09: "same lock
/// on mechanism for some signatures with the windup etc similarly to regular
/// striking"). Rail and the grammar-2 hook declare exactly as a windup gun
/// does — a named target, a frozen 90° wedge, a lock that follows inside it,
/// and the strike's cancels — and differ ONLY in what leaves the muzzle: the
/// rail beam PIERCES its line, so interposition shares it instead of stopping
/// it.
/// <para>
/// Tracking follows the same spotter rule as a gun strike: the lock lives
/// while the shooter's TEAM sees it and a clear physical ray joins the two.
/// The shooter's own facing no longer enters it.
/// </para>
/// </summary>
public sealed class ArcRelaySignatureLockTests
{
    private static readonly Position Shooter = new(2, 4);
    private const string RailActionId = "rail-line";

    [Fact]
    public void TheLockIsTheNamedTargetAndTheBeamFollowsItInsideTheWedge()
    {
        // The named body steps off the declared ray DURING the windup and is
        // hit anyway: the beam re-aims at wherever its lock now stands, as
        // long as that tile is still inside the frozen wedge.
        using Arena arena = Arena.Create(
            Zero(Shooter),
            One(new Position(8, 4)));

        arena.Step((0, 0, arena.Rail(ProjectileHeading.East, target: 0)));
        GenericActorMatchStepResult matured =
            arena.Step((1, 0, Arena.Move(Direction.North)));

        Assert.Equal(new Position(8, 3), arena.Body(matured, 1, 0).Position);
        Assert.Equal(3, arena.Body(matured, 1, 0).Health);
    }

    [Fact]
    public void TheBeamPiercesAndEveryBodyOnTheLineTakesIt()
    {
        // Interposition SHARES a beam rather than stopping it — the whole
        // reason rail's delivery is not the gun's first-body-contact ray.
        // Two bodies stand between the shooter and its lock; all three are
        // hit by the one cast.
        using Arena arena = Arena.Create(
            Zero(Shooter),
            One(new Position(8, 4), new Position(5, 4), new Position(6, 4)));

        arena.Step((0, 0, arena.Rail(ProjectileHeading.East, target: 0)));
        GenericActorMatchStepResult matured = arena.Step();

        Assert.Equal(3, arena.Body(matured, 1, 0).Health);
        Assert.Equal(3, arena.Body(matured, 1, 1).Health);
        Assert.Equal(3, arena.Body(matured, 1, 2).Health);
    }

    [Fact]
    public void ALockThatLeavesTheFrozenWedgeCancelsTheBeam()
    {
        // Reach 12 puts the named body on the wedge's last ring, so its one
        // step away is a step out — earned geometry, exactly as for a strike.
        using Arena arena = Arena.Create(
            Zero(Shooter, new Position(14, 5)),
            One(new Position(14, 4)));

        arena.Step((0, 0, arena.Rail(ProjectileHeading.East, target: 0)));
        GenericActorMatchStepResult matured =
            arena.Step((1, 0, Arena.Move(Direction.East)));

        // The teammate beside it still sees it and no wall is in the way, so
        // the wedge is the only rule that could have ended this beam.
        Assert.Equal(new Position(15, 4), arena.Body(matured, 1, 0).Position);
        Assert.Equal(5, arena.Body(matured, 1, 0).Health);
    }

    [Fact]
    public void ATeammatesEyesKeepTheLockAliveForABlindShooter()
    {
        // Spotter doctrine. The named body stands ten tiles down the lane,
        // far outside the shooter's own seven-tile sight, and a teammate
        // beside it keeps the lock alive. Under the old own-eyes rule this
        // beam cancelled.
        using Arena arena = Arena.Create(
            Zero(Shooter, new Position(11, 5)),
            One(new Position(12, 4)));

        arena.Step((0, 0, arena.Rail(ProjectileHeading.East, target: 0)));
        GenericActorMatchStepResult matured = arena.Step();

        Assert.Equal(3, arena.Body(matured, 1, 0).Health);
    }

    [Fact]
    public void NobodyOnTheTeamSeeingTheLockCancelsTheBeam()
    {
        // The same declare with the spotter walked away: nothing on team 0
        // can see the named body, so the lock is not trackable and the beam
        // never fires.
        using Arena arena = Arena.Create(
            Zero(Shooter),
            One(new Position(12, 4)));

        arena.Step((0, 0, arena.Rail(ProjectileHeading.East, target: 0)));
        GenericActorMatchStepResult matured = arena.Step();

        Assert.Equal(5, arena.Body(matured, 1, 0).Health);
    }

    [Fact]
    public void AWallBetweenTheShooterAndTheLockCancelsTheBeam()
    {
        // The lock steps behind the pillar, and a teammate standing next to
        // it watches the whole thing: team vision is satisfied and the beam
        // still dies, because no ray joins the frozen origin to where the
        // lock went. (On a wedge frozen from that same origin the ray rule
        // and the wedge rule agree by construction — both are the strike
        // line — so what this pins is the OUTCOME the ruling names, not
        // which of the two clauses spoke first.)
        using Arena arena = Arena.Create(
            Zero(Shooter, new Position(7, 3)),
            One(new Position(8, 4)),
            pillar: new Position(6, 3));

        arena.Step((0, 0, arena.Rail(ProjectileHeading.East, target: 0)));
        GenericActorMatchStepResult matured =
            arena.Step((1, 0, Arena.Move(Direction.North)));

        Assert.Equal(new Position(8, 3), arena.Body(matured, 1, 0).Position);
        Assert.Equal(5, arena.Body(matured, 1, 0).Health);
    }

    [Fact]
    public void ADeclareThatNamesNobodyKeepsTheTheatricalWhiff()
    {
        // No name, no lock, and no substitution: the beam still goes down the
        // announced heading, which is what a suppressive declare is for. The
        // body standing in the lane eats it; the body off the lane does not.
        using Arena arena = Arena.Create(
            Zero(Shooter),
            One(new Position(6, 4), new Position(6, 6)));

        arena.Step((0, 0, arena.Rail(ProjectileHeading.East, target: null)));
        GenericActorMatchStepResult matured = arena.Step();

        Assert.Equal(3, arena.Body(matured, 1, 0).Health);
        Assert.Equal(5, arena.Body(matured, 1, 1).Health);
    }

    [Fact]
    public void TheWindupRidesThePendingStrikeWireAndTheDeclarerIsRooted()
    {
        // The gallery read. A winding-up beam publishes the same wire shape a
        // winding-up gun does — frozen apex, declared heading, frozen wedge,
        // locked body — which is exactly what the viewer's tracking ray
        // consumes, and it fires from the tile it declared on.
        using Arena arena = Arena.Create(
            Zero(Shooter),
            One(new Position(8, 4)));

        GenericActorMatchStepResult declared =
            arena.Step((0, 0, arena.Rail(ProjectileHeading.East, target: 0)));

        GenericActorRuntimeObservation.ArcRelayPendingStrikeState pending =
            Assert.Single(Arena.PendingStrikes(declared));
        Assert.Equal(0, pending.Shooter.TeamId);
        Assert.Equal(0, pending.Shooter.UnitId);
        Assert.Equal(Shooter, pending.Origin);
        Assert.Equal(ProjectileHeading.East, pending.CentralHeading);
        Assert.NotNull(pending.Target);
        Assert.Equal(1, pending.Target!.TeamId);
        Assert.Equal(0, pending.Target!.UnitId);
        Assert.Contains(new Position(8, 4), pending.Tiles);

        GenericActorMatchStepResult matured = arena.Step();
        Assert.Equal(Shooter, arena.Body(matured, 0, 0).Position);
        Assert.Empty(Arena.PendingStrikes(matured));
        Assert.Equal(3, arena.Body(matured, 1, 0).Health);
    }

    [Fact]
    public void SmokeIsNeverALockAndNeverRootsItsCaster()
    {
        // Utility is untouched: the canister lands the tick it is cast, it
        // publishes no pending strike, and its caster walks away freely.
        using Arena arena = Arena.Create(
            Zero(Shooter),
            One(new Position(8, 4)),
            teamZeroClasses: SmokeSheet);

        GenericActorMatchStepResult cast = arena.Step((0, 0, arena.Smoke()));

        Assert.Empty(Arena.PendingStrikes(cast));
        Assert.Contains(
            Arena.Signatures(cast),
            signature => signature.SignatureId == "smoke-canister"
                && signature.Phase
                    == ArcRelaySignatureState.SignaturePhase.Active);

        GenericActorMatchStepResult walked =
            arena.Step((0, 0, Arena.Move(Direction.North)));
        Assert.Equal(
            Shooter.Offset(0, -1),
            arena.Body(walked, 0, 0).Position);
    }

    /// <summary>
    /// The seven team-0 bodies that are not under test, parked in the west
    /// column: ten tiles and more from every lane, so team sight is exactly
    /// what each test places deliberately.
    /// </summary>
    private static readonly Position[] WestPark =
    [
        new(1, 1), new(1, 2), new(1, 5), new(1, 6), new(1, 7), new(2, 1),
        new(2, 2),
    ];

    /// <summary>The seven team-1 bodies that are not under test, parked out
    /// of every wedge in the east column.</summary>
    private static readonly Position[] EastPark =
    [
        new(18, 1), new(18, 2), new(18, 3), new(18, 5), new(18, 6),
        new(18, 7), new(17, 7),
    ];

    private static Position[] Zero(params Position[] placed) =>
        [.. placed, .. WestPark.Take(8 - placed.Length)];

    private static Position[] One(params Position[] placed) =>
        [.. placed, .. EastPark.Take(8 - placed.Length)];

    private static readonly string[] SmokeSheet =
    [
        ArcRelayLaunchClassIds.Veil,
        ArcRelayLaunchClassIds.Palisade,
        ArcRelayLaunchClassIds.Patchbay,
        ArcRelayLaunchClassIds.Lantern,
        ArcRelayLaunchClassIds.Mortar,
        ArcRelayLaunchClassIds.Minesmith,
        ArcRelayLaunchClassIds.Hush,
        ArcRelayLaunchClassIds.Relay,
    ];

    /// <summary>
    /// An open Arc Relay box on the ambush-11 ruleset — the one profile that
    /// carries the strike lock — with every body placed exactly. Team 0 unit
    /// 0 is the Longshot whose rail is under test; the bodies team 1 puts in
    /// the lane carry five hull, so one beam is survivable and readable.
    /// </summary>
    private sealed class Arena : IDisposable
    {
        private const int Width = 21;
        private const int Height = 9;
        private readonly ActorResolvedMatchDefinition _definition;
        private readonly GenericActorMatchSession _session;
        private readonly Dictionary<(int TeamId, int UnitId),
            GenericActorRuntimeDecision> _commands = [];

        private Arena(ActorResolvedMatchDefinition definition)
        {
            _definition = definition;
            Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
                factories = GenericMindSessionTestFixture.Factories(
                    definition,
                    (_, observation) => new GenericMindRuntimeDecisions(
                    [
                        .. observation.Bodies
                            .Where(body => _commands.ContainsKey(
                                (body.ActorId.TeamId, body.ActorId.UnitId)))
                            .Select(body =>
                            {
                                GenericActorRuntimeDecision decision =
                                    _commands[(body.ActorId.TeamId,
                                        body.ActorId.UnitId)];
                                return new GenericMindCommand(
                                    body.ActorId.UnitId,
                                    body.ActorId.LifeId,
                                    decision.ActionId,
                                    decision.ActionCode,
                                    decision.Arguments);
                            }),
                    ]));
            _session = new GenericActorMatchSession(
                definition,
                GenericMindSessionTestFixture.Configurations(
                    definition,
                    factories),
                matchSeed: 20_260_809UL);
        }

        public static Arena Create(
            IReadOnlyList<Position> teamZero,
            IReadOnlyList<Position> teamOne,
            Position? pillar = null,
            IReadOnlyList<string>? teamZeroClasses = null) =>
            new(Definition(teamZero, teamOne, pillar, teamZeroClasses));

        /// <summary>
        /// A rail declared down <paramref name="heading"/>, naming a team-1
        /// unit or nobody at all.
        /// </summary>
        public GenericActorRuntimeDecision Rail(
            ProjectileHeading heading,
            int? target)
        {
            ActorActionDefinition action = Action(RailActionId);
            return new GenericActorRuntimeDecision(
                action.Id,
                action.Code,
                target is int unitId
                    ?
                    [
                        new GenericActorRuntimeActionArgument
                            .ProjectileHeadingArgument(heading),
                        new GenericActorRuntimeActionArgument.UnitTargetArgument(
                            new GenericActorRuntimeActionArgument.UnitTarget(
                                1,
                                unitId)),
                    ]
                    :
                    [
                        new GenericActorRuntimeActionArgument
                            .ProjectileHeadingArgument(heading),
                    ],
                null);
        }

        public GenericActorRuntimeDecision Smoke()
        {
            ActorActionDefinition action = Action("smoke-canister");
            return new GenericActorRuntimeDecision(
                action.Id,
                action.Code,
                [
                    new GenericActorRuntimeActionArgument.PositionTargetArgument(
                        Shooter.Offset(1, 0)),
                ],
                null);
        }

        public static GenericActorRuntimeDecision Move(Direction direction) =>
            new(
                ArcRelayH0Definition.MoveActionId,
                1,
                [
                    new GenericActorRuntimeActionArgument
                        .ProjectileHeadingArgument(
                            direction.ToProjectileHeading()),
                ],
                null);

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

        public static ImmutableArray<
            GenericActorRuntimeObservation.ArcRelayPendingStrikeState>
            PendingStrikes(GenericActorMatchStepResult step) =>
            Arc(step).PendingStrikes;

        public static ImmutableArray<ArcRelaySignatureState> Signatures(
            GenericActorMatchStepResult step) =>
            Arc(step).VisibleSignatures;

        public void Dispose() => _session.Dispose();

        private static GenericActorRuntimeObservation.ModeObservationState
            .ArcRelay Arc(GenericActorMatchStepResult step) =>
            (GenericActorRuntimeObservation.ModeObservationState.ArcRelay)
                step.PostState.Mode!;

        private ActorActionDefinition Action(string id) =>
            _definition.Rules.Actions.Single(action => string.Equals(
                action.Id,
                id,
                StringComparison.Ordinal));

        private static ActorResolvedMatchDefinition Definition(
            IReadOnlyList<Position> teamZero,
            IReadOnlyList<Position> teamOne,
            Position? pillar,
            IReadOnlyList<string>? teamZeroClasses)
        {
            string[] classesZero = teamZeroClasses is null
                ?
                [
                    ArcRelayLaunchClassIds.Longshot,
                    ArcRelayLaunchClassIds.Towline,
                    ArcRelayLaunchClassIds.Veil,
                    ArcRelayLaunchClassIds.Palisade,
                    ArcRelayLaunchClassIds.Patchbay,
                    ArcRelayLaunchClassIds.Lantern,
                    ArcRelayLaunchClassIds.Mortar,
                    ArcRelayLaunchClassIds.Minesmith,
                ]
                : [.. teamZeroClasses];
            // Repulsor is the five-hull class whose STANDARD handling is not
            // facing-locked, so a body under test can step in any direction
            // and still survive one beam to report the damage.
            string[] classesOne =
            [
                ArcRelayLaunchClassIds.Repulsor,
                ArcRelayLaunchClassIds.Repulsor,
                ArcRelayLaunchClassIds.Mason,
                ArcRelayLaunchClassIds.Mason,
                ArcRelayLaunchClassIds.Sunder,
                ArcRelayLaunchClassIds.Sunder,
                ArcRelayLaunchClassIds.Hush,
                ArcRelayLaunchClassIds.Hush,
            ];
            ActorRulesDefinition rules = ArcRelayH0Definition.CreateRules(
                ArcRelayLoopProfile.AmbushWarren11);
            var slots = ImmutableArray.CreateBuilder<PublicUnitSlot>();
            var lives = ImmutableArray.CreateBuilder<PublicInitialLife>();
            var spawns = ImmutableArray.CreateBuilder<InitialSpawnDefinition>();
            var deployed = ImmutableArray.CreateBuilder<InitialLifeDeployment>();
            var lifecycle = ImmutableArray.CreateBuilder<
                ActorUnitSlotLifecycleAssignmentDefinition>();
            foreach ((int teamId, string[] classes, IReadOnlyList<Position> at)
                     in new[]
                     {
                         (0, classesZero, teamZero),
                         (1, classesOne, teamOne),
                     })
            {
                for (int unitId = 0; unitId < classes.Length; unitId++)
                {
                    string formId = ArcRelayH0Definition.FormPrefix
                        + classes[unitId];
                    string spawnId = $"team-{teamId}-unit-{unitId}";
                    slots.Add(new PublicUnitSlot(
                        teamId, unitId, teamId, classes[unitId]));
                    lives.Add(new PublicInitialLife(
                        teamId, unitId, 0, formId));
                    spawns.Add(new InitialSpawnDefinition(
                        spawnId,
                        at[unitId],
                        teamId == 0 ? Direction.East : Direction.West));
                    deployed.Add(new InitialLifeDeployment(
                        teamId, unitId, 0, formId, spawnId));
                    lifecycle.Add(new ActorUnitSlotLifecycleAssignmentDefinition(
                        teamId,
                        unitId,
                        ArcRelayH0Definition.LifecycleProfilePrefix
                            + classes[unitId],
                        initialGeneration: 0,
                        [formId],
                        ActorUnitSlotLifecycleAssignmentDefinition
                            .InitialAvailabilityKind.ActiveAtTickZero,
                        unlockTick: null,
                        assignedRespawnSpawnId: spawnId));
                }
            }
            var topology = new PublicMatchTopology
            {
                Teams = [new PublicScoringTeam(0), new PublicScoringTeam(1)],
                Participants =
                [
                    new PublicParticipant(0, 0),
                    new PublicParticipant(1, 1),
                ],
                UnitSlots = slots.ToImmutable(),
                InitialLives = lives.ToImmutable(),
            };
            return new ActorResolvedMatchDefinition(
                rules,
                Map(pillar, spawns.ToImmutable()),
                new HeadToHeadMatchFormatDefinition(),
                topology,
                new InitialDeploymentDefinition(
                    spawns.ToImmutable(),
                    deployed.ToImmutable()),
                lifecycle.ToImmutable(),
                [
                    new(0, ArcRelayH0Definition.ReactorRoleId, "reactor-west",
                        Direction.East),
                    new(0, ArcRelayH0Definition.HomePadRoleId, "home-west",
                        Direction.East),
                    new(1, ArcRelayH0Definition.ReactorRoleId, "reactor-east",
                        Direction.West),
                    new(1, ArcRelayH0Definition.HomePadRoleId, "home-east",
                        Direction.West),
                ],
                new ArcRelayActorModeMapBindingDefinition(
                    ["well-centre", "well-north", "well-south"],
                    ArcRelayH0Definition.ReactorRoleId,
                    ArcRelayH0Definition.HomePadRoleId),
                ActorMatchCapabilityVersions.Mind);
        }

        private static ActorMapDefinition Map(
            Position? pillar,
            ImmutableArray<InitialSpawnDefinition> spawns)
        {
            var rows = ImmutableArray.CreateBuilder<string>();
            for (int y = 0; y < Height; y++)
            {
                char[] row = new string('.', Width).ToCharArray();
                for (int x = 0; x < Width; x++)
                {
                    if (y == 0 || y == Height - 1 || x == 0 || x == Width - 1)
                        row[x] = '#';
                }
                if (pillar is Position wall && wall.Y == y)
                    row[wall.X] = '#';
                rows.Add(new string(row));
            }
            Position[] required =
            [
                new(10, 1), new(10, 4), new(10, 7), new(1, 4), new(19, 4),
            ];
            return new ActorMapDefinition(
                "arc-signature-lock-arena",
                version: 1,
                rows.ToImmutable(),
                [
                    .. spawns.Select(spawn =>
                        new ActorMapSpawnAnchorDefinition(
                            spawn,
                            [ActorMovementLayer.Ground])),
                ],
                [
                    Region("well-north", new Position(10, 1)),
                    Region("well-centre", new Position(10, 4)),
                    Region("well-south", new Position(10, 7)),
                    Region("reactor-west", new Position(1, 4)),
                    Region("reactor-east", new Position(19, 4)),
                    Region("home-west", new Position(1, 3)),
                    Region("home-east", new Position(19, 5)),
                ],
                [
                    new ActorMapTileTagDefinition(
                        "arc-required-tiles",
                        ActorMapTileTagDefinition.TileTagKind
                            .SignaturePlacementForbidden,
                        [.. required]),
                ]);
        }

        private static ActorMapRegionDefinition Region(
            string id,
            Position position) =>
            new(id, ActorMapRegionDefinition.RegionKind.Objective, [position]);
    }
}
