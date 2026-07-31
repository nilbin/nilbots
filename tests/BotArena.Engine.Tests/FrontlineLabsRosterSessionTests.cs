namespace BotArena.Engine.Tests;

/// <summary>
/// The LEGION roster driven live through whole matches on the real arm. The
/// definition tests pin what the contract declares; these pin that the SESSION
/// delivers it — three bodies standing on the first tick, the staged
/// activations arriving on the exact declared ticks without a body asking for
/// them, the fabricator fielding its opening four by spending its class verb,
/// and the whole full-roster match producing a replay that verifies.
/// </summary>
public sealed class FrontlineLabsRosterSessionTests
{
    /// <summary>
    /// The bodies a team has standing at tick zero, and the ticks its later
    /// slots put bodies on the field. Nothing in the script asks for a
    /// companion: the roster's activations are lifecycle facts.
    /// </summary>
    [Fact]
    public void TheStagedActivationsArriveOnTheDeclaredTicks()
    {
        ActorResolvedMatchDefinition legion = FrontlineLabsRosterArmTests.Arm(
            FrontlineLabsClassDefinition.Bulwark,
            FrontlineLabsClassDefinition.Striker);
        GenericActorMatchChronology run = FrontlineLabsSkillArmTestFixture.Run(
            legion,
            (_, _) => GenericDeathmatchSessionTestFixture.Wait());

        // Three live bodies on the very first tick, on both sides.
        foreach (int teamId in new[] { 0, 1 })
        {
            Assert.Equal(
                3,
                run.InitialFrame.State.ActiveLives.Count(life =>
                    life.ActorId.TeamId == teamId));
        }

        // And the tranches arrive exactly when the contract said they would.
        foreach (int teamId in new[] { 0, 1 })
        {
            Assert.Equal(
                [
                    0,
                    0,
                    0,
                    FrontlineLabsLegionRoster.MidTrancheUnlockTick,
                    FrontlineLabsLegionRoster.MidTrancheUnlockTick,
                    FrontlineLabsLegionRoster.LateTrancheUnlockTick,
                    FrontlineLabsLegionRoster.LateTrancheUnlockTick,
                    FrontlineLabsLegionRoster.LateTrancheUnlockTick,
                ],
                Enumerable.Range(0, 8).Select(unitId =>
                    FirstStandingTick(run, teamId, unitId)));
        }

        // The endgame roster: eight bodies a side, standing together.
        GenericActorMatchTickFrame late = run.Ticks.Single(frame =>
            frame.Tick == FrontlineLabsLegionRoster.LateTrancheUnlockTick);
        Assert.Equal(
            16,
            late.PostState.ActiveLives.Length);
    }

    /// <summary>
    /// The fabricator's opening is four SLOTS rather than four bodies: its
    /// three companions are unlocked from tick zero and cost the prime an
    /// action each, which is the bargain #154 gave the class — and they
    /// materialise beside it in the field rather than on a pad.
    /// </summary>
    [Fact]
    public void TheFabricatorFieldsItsOpeningFourByFabricatingThem()
    {
        ActorResolvedMatchDefinition legion = FrontlineLabsRosterArmTests.Arm(
            FrontlineLabsClassDefinition.Fabricator,
            FrontlineLabsClassDefinition.Striker);
        GenericActorMatchChronology run = FrontlineLabsSkillArmTestFixture.Run(
            legion,
            (start, observation) =>
                start.ActorId.TeamId != 0
                || observation.Self.ActorId.UnitId != 0
                    ? GenericDeathmatchSessionTestFixture.Wait()
                    : Fabricate(observation));

        // One body at tick zero, and four standing within ten ticks — the
        // fabricator's opening is paid in prime actions, not handed over.
        Assert.Equal(
            1,
            run.InitialFrame.State.ActiveLives.Count(life =>
                life.ActorId.TeamId == 0));
        Assert.Equal(
            4,
            run.Ticks
                .Single(frame => frame.Tick == 10)
                .PostState.ActiveLives
                .Count(life => life.ActorId.TeamId == 0));
        // None of them arrived on a pad: forward fabrication places beside
        // the prime, and the pads are SpawnProtected against it.
        Assert.All(
            run.Ticks
                .Single(frame => frame.Tick == 10)
                .PostState.ActiveLives
                .Where(life => life.ActorId.TeamId == 0
                    && life.ActorId.UnitId > 0),
            life => Assert.DoesNotContain(
                life.Position,
                legion.Map.Regions
                    .Single(region => region.RegionId == "team-0-home-pad")
                    .Tiles));

        // Nine slots in the end, against the striker's eight.
        Assert.Equal(
            9,
            run.Ticks
                .Single(frame => frame.Tick
                    == FrontlineLabsLegionRoster.LateTrancheUnlockTick)
                .PostState.Slots
                .Count(slot => slot.TeamId == 0));
    }

    /// <summary>
    /// A whole full-roster match on the v1.1 shipped game — eight bodies
    /// against nine, the channel, the economy — runs to the horn and produces
    /// a replay-v3 document that verifies. The chronology validator re-derives
    /// every lifecycle activation from the document as it goes, so a staged
    /// roster that the contract did not declare could not have got here.
    /// </summary>
    [Fact]
    public void AFullRosterMatchRunsAndItsReplayVerifies()
    {
        ActorResolvedMatchDefinition legion =
            FrontlineLabsRosterArmTests.FullGame(
                FrontlineLabsClassDefinition.Fabricator,
                FrontlineLabsClassDefinition.Striker,
                FrontlineLabsRosterArm.Legion);
        GenericActorMatchChronology run = FrontlineLabsSkillArmTestFixture.Run(
            legion,
            (start, observation) =>
                start.ActorId.TeamId == 0
                && observation.Self.ActorId.UnitId == 0
                    ? Fabricate(observation)
                    : GenericDeathmatchSessionTestFixture.Wait());

        string json = ReplayV3Serializer.ToJson(ReplayV3Projection.Project(run));
        Assert.True(
            ReplayV3Serializer.VerifyHash(json, out string? failure),
            failure);
        Assert.Equal(
            legion.Rules.RulesetId,
            run.Descriptor.Definition.Rules.RulesetId);
        Assert.Contains("garrison", legion.Rules.RulesetId, StringComparison.Ordinal);
        Assert.True(
            run.Ticks[^1].PostState.ActiveLives.Length >= 16,
            "the full roster never reached the field");
    }

    /// <summary>
    /// Fabricates the lowest ready slot whenever the mask allows it, and waits
    /// otherwise — the shortest legal path from one body to the roster.
    /// </summary>
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
    /// The first tick one slot has a body standing in the world, reading the
    /// authoritative post-state rather than any event stream.
    /// </summary>
    private static int FirstStandingTick(
        GenericActorMatchChronology run,
        int teamId,
        int unitId) =>
        run.InitialFrame.State.ActiveLives.Any(life =>
            life.ActorId.TeamId == teamId && life.ActorId.UnitId == unitId)
            ? 0
            : run.Ticks
                .First(frame => frame.PostState.ActiveLives.Any(life =>
                    life.ActorId.TeamId == teamId
                    && life.ActorId.UnitId == unitId))
                .Tick;
}
