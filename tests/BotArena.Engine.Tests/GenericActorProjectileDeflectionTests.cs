using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The deflecting projectile guard, exercised on a small controlled contract
/// where every body's tile and facing is scripted. The class-arm suites prove
/// the skill; this one proves the rule: what a return is, who owns it, where
/// it starts, which way it flies, and in what order identities are issued when
/// one tick contains more than one deflection.
/// </summary>
public sealed class GenericActorProjectileDeflectionTests
{
    /// <summary>West spawn, facing East.</summary>
    private static readonly Position WestTile = new(1, 3);

    /// <summary>East spawn, facing West.</summary>
    private static readonly Position EastTile = new(7, 3);

    /// <summary>
    /// Two bodies facing each other, both firing on the same tick: each bolt
    /// dies on the other's arc, and both returns are issued inside one tick.
    /// Identity follows contact order — the lower incoming projectile ID is
    /// resolved first and therefore takes the lower returned ID — and the
    /// whole exchange repeats byte for byte on a second run.
    /// </summary>
    [Fact]
    public void TwoDeflectionsInOneTickTakeContactOrderIdentities()
    {
        GenericActorMatchChronology chronology = RunDuel();

        GenericActorMatchTickFrame frame = chronology.Ticks.First(item =>
            Deflections(item).Length > 0);
        ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.ProjectileDeflected>
            deflections = Deflections(frame);

        Assert.Equal(2, deflections.Length);
        // Contact order is projectile-identity order, and the returned
        // identities follow it contiguously from the tick's launch counter.
        Assert.Equal(
            [0L, 1L],
            deflections.Select(item => item.ProjectileId));
        Assert.Equal(
            [2L, 3L],
            deflections.Select(item => item.DeflectedProjectileId));
        // Each return belongs to the body that turned it, starts on that
        // body's own tile, and flies the exact reverse of what arrived.
        Assert.Equal(EastTile, deflections[0].Position);
        Assert.Equal(1, deflections[0].TargetActorId.TeamId);
        Assert.Equal(0, deflections[0].SourceTeamId);
        Assert.Equal(ProjectileHeading.East, deflections[0].Heading);
        Assert.Equal(WestTile, deflections[1].Position);
        Assert.Equal(0, deflections[1].TargetActorId.TeamId);
        Assert.Equal(1, deflections[1].SourceTeamId);
        Assert.Equal(ProjectileHeading.West, deflections[1].Heading);

        foreach (GenericActorRuntimeObservation.EventPayload.ProjectileDeflected
                 deflected in deflections)
        {
            GenericActorProjectileTraversal launch = frame.Traversals.Single(
                traversal =>
                    traversal.ProjectileId
                        == deflected.DeflectedProjectileId);
            Assert.Equal(
                GenericActorProjectileTraversal.TraversalTrigger
                    .GuardDeflection,
                launch.Trigger);
            Assert.Equal(deflected.TargetActorId, launch.OwnerActorId);
            Assert.Equal(deflected.Position, launch.From);
            Assert.Equal(
                deflected.Heading.Reversed(),
                launch.LaunchHeading);
        }
    }

    /// <summary>
    /// The rule that makes the cascade terminate: a bolt returned on a tick is
    /// not eligible to be returned again on that same tick. Two guards one
    /// exchange apart would otherwise volley a single bolt forever inside one
    /// tick's launch phase.
    /// </summary>
    [Fact]
    public void ADeflectionNeverCascadesInsideOneTick()
    {
        GenericActorMatchChronology chronology = RunDuel();

        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            HashSet<long> issued = Deflections(frame)
                .Select(item => item.DeflectedProjectileId)
                .ToHashSet();
            Assert.DoesNotContain(
                Deflections(frame),
                item => issued.Contains(item.ProjectileId));
        }
    }

    /// <summary>
    /// The ownership flip is the whole mechanic: a return is the deflector's
    /// bolt, so it damages the side that fired the original. Two guards that
    /// keep staring at each other rally forever, so this probe turns one of
    /// them away mid-rally — the return then lands on the flank it left open.
    /// </summary>
    [Fact]
    public void AReturnedBoltDamagesTheSideThatFiredTheOriginal()
    {
        GenericActorMatchChronology chronology = RunDuel(turnAwayTick: 3);

        HashSet<long> returns =
        [
            .. chronology.Ticks
                .SelectMany(item => Deflections(item))
                .Select(item => item.DeflectedProjectileId),
        ];
        GenericActorRuntimeObservation.EventPayload.Damage[] returnHits =
        [
            .. chronology.Ticks
                .SelectMany(item => Damages(item))
                .Where(item => returns.Contains(item.ProjectileId)),
        ];

        Assert.NotEmpty(returnHits);
        // The bolt now belongs to the deflector's team, so the hit is scored
        // against the original shooter's team, never the deflector's.
        Assert.All(
            returnHits,
            hit => Assert.NotEqual(hit.SourceTeamId, hit.TargetActorId.TeamId));
    }

    /// <summary>
    /// Allied fire never reaches the guard: the collision contract answers an
    /// allied contact first, so a bolt crossing a teammate's protected arc is
    /// neither turned nor published.
    /// </summary>
    [Fact]
    public void AlliedFireCrossesAGuardedArcUntouched()
    {
        // Team 0 holds the west and north spawns. The north body walks onto
        // the west body's firing line and turns its own guarded face into the
        // incoming allied bolt; team 1 waits out of the exchange.
        var alliedTile = new Position(4, 3);
        GenericActorMatchChronology chronology = Run(
            GuardedDefinition("teams"),
            (_, observation) =>
            {
                if (observation.Self.ActorId.TeamId != 0)
                    return GenericDeathmatchSessionTestFixture.Wait();
                if (observation.Self.Position == WestTile)
                {
                    return observation.Tick >= 4
                        ? GenericDeathmatchSessionTestFixture.Shoot()
                        : GenericDeathmatchSessionTestFixture.Wait();
                }
                if (observation.Self.Position != alliedTile)
                {
                    return FrontlineLabsSkillArmTestFixture.WalkTo(
                            observation,
                            alliedTile)
                        ?? GenericDeathmatchSessionTestFixture.Wait();
                }
                return observation.Self.Facing == Direction.West
                    ? GenericDeathmatchSessionTestFixture.Wait()
                    : GenericDeathmatchSessionTestFixture.Rotate(
                        Direction.West);
            });

        GenericActorRuntimeObservation.EventPayload.ProjectileDeflected[]
            deflections =
            [.. chronology.Ticks.SelectMany(item => Deflections(item))];

        Assert.Contains(
            chronology.Ticks,
            frame => FrontlineLabsSkillArmTestFixture
                .Attacks(frame)
                .Length > 0);
        // The allied bolt crosses the teammate's protected face and is never
        // published as turned — while the enemy's guard, on the same map and
        // the same tick sequence, does turn what reaches it.
        Assert.NotEmpty(deflections);
        Assert.All(
            deflections,
            item => Assert.NotEqual(
                item.SourceTeamId,
                item.TargetActorId.TeamId));
        Assert.DoesNotContain(
            deflections,
            item => item.Position == alliedTile);
    }

    /// <summary>
    /// Two guarded bodies on one row, facing each other, both firing on tick
    /// zero. With <paramref name="turnAwayTick"/> set, team 0 turns north on
    /// that tick and stops answering the rally.
    /// </summary>
    private static GenericActorMatchChronology RunDuel(
        int turnAwayTick = -1) =>
        Run(
            GuardedDefinition("head-to-head"),
            (_, observation) =>
            {
                if (observation.Tick == 0)
                    return GenericDeathmatchSessionTestFixture.Shoot();
                return observation.Tick >= turnAwayTick
                    && turnAwayTick >= 0
                    && observation.Self.ActorId.TeamId == 0
                    && observation.Self.Facing != Direction.North
                    ? GenericDeathmatchSessionTestFixture.Rotate(
                        Direction.North)
                    : GenericDeathmatchSessionTestFixture.Wait();
            });

    private static GenericActorMatchChronology Run(
        ActorResolvedMatchDefinition definition,
        Func<
            GenericActorRuntimeStart,
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision> decide)
    {
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition, decide);
        using var session = new GenericActorMatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            11UL);
        session.Run();
        return session.Chronology;
    }

    /// <summary>
    /// The shared deathmatch fixture with one change: every body's form
    /// declares the deflecting guard, so any face is a mirror.
    /// </summary>
    private static ActorResolvedMatchDefinition GuardedDefinition(
        string formatName)
    {
        ActorResolvedMatchDefinition source =
            GenericDeathmatchSessionTestFixture.Definition(
                formatName,
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 24,
                    MaxHealth = 8,
                });
        ActorFormDefinition mobile = source.Rules.Forms.Single();
        var rules = new ActorRulesDefinition(
            "generic-deathmatch-projectile-guard-fixture",
            source.Rules.Limits,
            source.Rules.SeedMechanics,
            source.Rules.GameMode,
            source.Rules.Lifecycle,
            [
                new ActorFormDefinition(
                    mobile.Id,
                    mobile.MaxHealth,
                    mobile.MovementProfileId,
                    mobile.VisionProfileId,
                    mobile.AttackProfileId,
                    mobile.ObjectiveWeight,
                    mobile.AllowedActionIds,
                    ActorFormProjectileGuardKind
                        .FacingQuadrantContactsDeflected),
            ],
            source.Rules.MovementProfiles,
            source.Rules.VisionProfiles,
            source.Rules.AttackProfiles,
            source.Rules.Actions,
            source.Rules.FabricationTransitions,
            source.Rules.SameLifeTransitions,
            source.Rules.ReplicationTransitions,
            source.Rules.TeamPerception,
            source.Rules.Collisions,
            source.Rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            rules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.ProjectileDeflected>
        Deflections(GenericActorMatchTickFrame frame) =>
        FrontlineLabsSkillArmTestFixture.Deflections(frame);

    private static ImmutableArray<
            GenericActorRuntimeObservation.EventPayload.Damage>
        Damages(GenericActorMatchTickFrame frame) =>
        FrontlineLabsSkillArmTestFixture.Damages(frame);
}
