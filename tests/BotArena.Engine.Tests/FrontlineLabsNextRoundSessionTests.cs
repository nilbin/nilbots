namespace BotArena.Engine.Tests;

/// <summary>
/// The next round's package driven live: a body that dies under the hull level
/// walks home instead of rallying to the front (the same script under the keel
/// does not), and a whole 750-tick full-roster match runs to the horn and
/// verifies.
/// </summary>
public sealed class FrontlineLabsNextRoundSessionTests
{
    private static readonly Position TeamZeroHome = new(2, 7);

    /// <summary>The centre objective's own-side tile for team 0.</summary>
    private static readonly Position CentreTile = new(11, 7);

    /// <summary>Where the striker parks, inside its gun's travel of row 7.</summary>
    private static readonly Position PokeTile = new(16, 7);

    /// <summary>
    /// The A/B the owner asked for. One script, two pendulum levels: with the
    /// rally the dead bulwark comes back beside the fight, and under the hull
    /// level it comes back on its own reserved home pad — twelve tiles of walk
    /// that the fabricator's field-placed children no longer have to pay.
    /// </summary>
    [Fact]
    public void UnderTheHullLevelADeadBodyWalksHomeInsteadOfRallying()
    {
        Position hull = FirstRespawn(Run(rally: false));
        Position keel = FirstRespawn(Run(rally: true));

        Assert.Equal(TeamZeroHome, hull);
        Assert.NotEqual(TeamZeroHome, keel);
        // The keel's rally lands on the own-side chain-adjacent tile of an
        // objective region — exactly the free forward placement the ruling
        // takes away.
        Assert.Contains(
            FrontlineLabsDefinition
                .CreateClassesExperiment(
                    FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker)
                .Map.Regions
                .Where(region => region.Kind
                    == ActorMapRegionDefinition.RegionKind.Objective)
                .SelectMany(region => region.Tiles),
            tile => tile == keel);
    }

    /// <summary>
    /// A 750-tick full-roster match on the whole package: it reaches the horn,
    /// the chronology validator accepts every tick on the way, and the replay
    /// document verifies.
    /// </summary>
    [Fact]
    public void ASevenHundredAndFiftyTickPackageMatchRunsAndVerifies()
    {
        ActorResolvedMatchDefinition package =
            FrontlineLabsNextRoundPackageTests.Package(
                FrontlineLabsClassDefinition.Fabricator,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsRosterArm.Legion);
        GenericActorMatchChronology run = FrontlineLabsSkillArmTestFixture.Run(
            package,
            (start, observation) =>
                start.ActorId.TeamId == 0
                && observation.Self.ActorId.UnitId == 0
                    ? Fabricate(observation)
                    : GenericDeathmatchSessionTestFixture.Wait());

        Assert.Equal(750, package.Rules.Limits.MaxTicks);
        Assert.Equal(750, run.Ticks.Length);
        Assert.Equal(749, run.Ticks[^1].Tick);
        string json = ReplayV3Serializer.ToJson(
            ReplayV3Projection.Project(run));
        Assert.True(
            ReplayV3Serializer.VerifyHash(json, out string? failure),
            failure);
        // The late tranche had its second act: it arrived at 300 and the
        // match ran on for 450 more ticks.
        Assert.True(
            run.Ticks[^1].PostState.ActiveLives.Length >= 16,
            "the full roster never reached the horn");
        Assert.Equal(
            "frontline-labs-1-fabricator-vs-striker-warpath-facing-locked",
            package.Rules.RulesetId);
    }

    /// <summary>
    /// The scripted death. Team 1's striker parks on the open centre row and
    /// fires west; team 0's prime walks onto the centre objective and stands
    /// there until it dies. Everything else waits, so the only thing that
    /// differs between the two runs is the lifecycle placement.
    /// </summary>
    private static GenericActorMatchChronology Run(bool rally)
    {
        FrontlineLabsPendulumArm pendulum =
            FrontlineLabsPendulumArm.StickyFrontline
            | FrontlineLabsPendulumArm.ContestMajority
            | FrontlineLabsPendulumArm.EnemySoleDecay
            | (rally ? FrontlineLabsPendulumArm.ForwardRally : 0);
        ActorResolvedMatchDefinition definition =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                pendulum,
                (FrontlineLabsClassDefinition.Bulwark,
                    FrontlineLabsClassDefinition.Striker));
        return FrontlineLabsSkillArmTestFixture.Run(
            definition,
            (start, observation) => start.ActorId.TeamId == 1
                ? Poke(observation)
                : Stand(observation));
    }

    private static GenericActorRuntimeDecision Poke(
        GenericActorRuntimeObservation observation)
    {
        if (observation.Self.ActorId.UnitId != 0)
            return GenericDeathmatchSessionTestFixture.Wait();
        if (FrontlineLabsSkillArmTestFixture.WalkTo(observation, PokeTile)
            is { } step)
        {
            return step;
        }
        return FrontlineLabsSkillArmTestFixture.Allows(observation, "shoot")
            ? GenericDeathmatchSessionTestFixture.Shoot()
            : GenericDeathmatchSessionTestFixture.Wait();
    }

    private static GenericActorRuntimeDecision Stand(
        GenericActorRuntimeObservation observation)
    {
        if (observation.Self.ActorId.UnitId != 0
            || observation.Self.ActorId.LifeId != 0)
        {
            return GenericDeathmatchSessionTestFixture.Wait();
        }
        return FrontlineLabsSkillArmTestFixture.WalkTo(
                observation,
                CentreTile)
            ?? GenericDeathmatchSessionTestFixture.Wait();
    }

    private static GenericActorRuntimeDecision Fabricate(
        GenericActorRuntimeObservation observation)
    {
        GenericActorRuntimeObservation.ObservedUnitSlot? ready =
            observation.TeamUnits
                .Where(slot => slot.State
                    is GenericActorRuntimeObservation.UnitSlotState.Ready)
                .OrderBy(slot => slot.UnitId)
                .FirstOrDefault();
        return ready is not null
            && FrontlineLabsSkillArmTestFixture.Allows(
                observation,
                "fabricate")
            ? GenericDeathmatchSessionTestFixture.Fabricate(
                ready.TeamId,
                ready.UnitId)
            : GenericDeathmatchSessionTestFixture.Wait();
    }

    /// <summary>
    /// Where team 0's prime slot's SECOND life arrived, read from the
    /// authoritative tick-start lifecycle event rather than inferred.
    /// </summary>
    private static Position FirstRespawn(GenericActorMatchChronology run) =>
        run.Ticks
            .SelectMany(frame => frame.TickStart.Events)
            .Where(item => item.Kind
                == GenericActorRuntimeObservation.EventKind.LifeSpawned)
            .Select(item =>
                (GenericActorRuntimeObservation.EventPayload.LifeSpawned)
                    item.Payload)
            .First(payload =>
                payload.ActorId.TeamId == 0
                && payload.ActorId.UnitId == 0
                && payload.ActorId.LifeId > 0)
            .Position;
}
