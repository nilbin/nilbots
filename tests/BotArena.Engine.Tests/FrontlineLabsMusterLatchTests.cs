using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

/// <summary>
/// The MUSTER latch and its one effect, driven through the real arm rather
/// than a synthetic contract: capture by sole objective weight, an owner that
/// survives an empty and a contested site, a claim that any interruption puts
/// back to zero, re-capture by the other team, and a Prime respawn that lands
/// forward exactly while the flag is held.
/// </summary>
public sealed class FrontlineLabsMusterLatchTests
{
    /// <summary>The base contract carrying only the side objective.</summary>
    private static ActorResolvedMatchDefinition Arm() =>
        FrontlineLabsDefinition.CreatePendulumExperiment(
            FrontlineLabsPendulumArm.None,
            classes: null,
            sideObjective: FrontlineLabsSideObjectiveArm.Muster);

    private static FrontlineModeKernel Kernel(
        ActorResolvedMatchDefinition definition) =>
        new(
            definition.Topology,
            (FrontlineGameModeDefinition)definition.Rules.GameMode,
            (FrontlineActorModeMapBindingDefinition)
                definition.ModeMapBinding);

    private static readonly ImmutableDictionary<int, int> Empty =
        ImmutableDictionary<int, int>.Empty;

    private static ImmutableDictionary<int, int> Weight(
        params (int TeamId, int Weight)[] entries) =>
        entries.ToImmutableDictionary(
            entry => entry.TeamId,
            entry => entry.Weight);

    /// <summary>
    /// The state machine, exercised exactly: a claim needs
    /// <see cref="FrontlineLabsMusterSite.LatchTicks"/> CONSECUTIVE sole
    /// ticks, an empty or contested tick resets it to zero, the completed
    /// claim latches through both, and only a full counter-claim takes it.
    /// </summary>
    [Fact]
    public void TheLatchCapturesHoldsStallsAndRecaptures()
    {
        ActorResolvedMatchDefinition definition = Arm();
        FrontlineModeKernel kernel = Kernel(definition);
        FrontlineControlState state = kernel.CreateInitialState();
        int tick = 0;
        int threshold = FrontlineLabsMusterSite.LatchTicks;

        Assert.NotNull(state.SecondaryControl);
        Assert.Null(state.SecondaryControl!.OwnerTeamId);

        // One tick short of the threshold is still neutral, and the running
        // claim is visible the whole way up.
        for (int held = 1; held < threshold; held++)
        {
            state = Step(kernel, ref tick, state, Weight((0, 1)));
            Assert.Null(state.SecondaryControl!.OwnerTeamId);
            Assert.Equal(0, state.SecondaryControl.ClaimingTeamId);
            Assert.Equal(held, state.SecondaryControl.ClaimTicks);
        }

        // Contested: the claim does not merely stall, it resets. One body
        // walking in is a real denial rather than a pause.
        state = Step(kernel, ref tick, state, Weight((0, 1), (1, 1)));
        Assert.Null(state.SecondaryControl!.OwnerTeamId);
        Assert.Null(state.SecondaryControl.ClaimingTeamId);
        Assert.Equal(0, state.SecondaryControl.ClaimTicks);

        // Empty resets it too.
        state = Step(kernel, ref tick, state, Weight((0, 1)));
        Assert.Equal(1, state.SecondaryControl!.ClaimTicks);
        state = Step(kernel, ref tick, state, Empty);
        Assert.Equal(0, state.SecondaryControl!.ClaimTicks);

        // A clean run of exactly the threshold latches on the last tick.
        for (int held = 1; held <= threshold; held++)
            state = Step(kernel, ref tick, state, Weight((0, 1)));
        Assert.Equal(0, state.SecondaryControl!.OwnerTeamId);
        Assert.Null(state.SecondaryControl.ClaimingTeamId);
        Assert.Equal(0, state.SecondaryControl.ClaimTicks);

        // The owner keeps it while the site is empty, while the owner itself
        // stands on it, and while the site is contested.
        foreach (ImmutableDictionary<int, int> presence in new[]
                 {
                     Empty,
                     Weight((0, 1)),
                     Weight((0, 1), (1, 2)),
                 })
        {
            state = Step(kernel, ref tick, state, presence);
            Assert.Equal(0, state.SecondaryControl!.OwnerTeamId);
            Assert.Equal(0, state.SecondaryControl.ClaimTicks);
        }

        // And the enemy takes it only by completing a claim of its own,
        // never by simply arriving.
        for (int held = 1; held < threshold; held++)
        {
            state = Step(kernel, ref tick, state, Weight((1, 1)));
            Assert.Equal(0, state.SecondaryControl!.OwnerTeamId);
            Assert.Equal(1, state.SecondaryControl.ClaimingTeamId);
            Assert.Equal(held, state.SecondaryControl.ClaimTicks);
        }
        state = Step(kernel, ref tick, state, Weight((1, 1)));
        Assert.Equal(1, state.SecondaryControl!.OwnerTeamId);
        Assert.Null(state.SecondaryControl.ClaimingTeamId);
    }

    /// <summary>
    /// The latch reads objective WEIGHT, not bodies, which is the one rule
    /// the whole class slate rests on: a fortified turret declares weight
    /// zero, so it can neither hold a side site nor contest one. The kernel
    /// only ever sees positive weights, so this is pinned where the driver
    /// computes them — a zero-weight form never enters the dictionary.
    /// </summary>
    [Fact]
    public void ZeroWeightFormsNeitherHoldNorContestTheSite()
    {
        ActorResolvedMatchDefinition definition = Arm();
        Assert.Equal(
            0,
            definition.Rules.Forms
                .Single(form => form.Id == "turret")
                .ObjectiveWeight);

        FrontlineModeKernel kernel = Kernel(definition);
        FrontlineControlState state = kernel.CreateInitialState();
        int tick = 0;
        // A turret parked beside the claiming body contributes nothing, so
        // the sole claim completes exactly as if it were alone.
        for (int held = 0; held < FrontlineLabsMusterSite.LatchTicks; held++)
            state = Step(kernel, ref tick, state, Weight((0, 1)));
        Assert.Equal(0, state.SecondaryControl!.OwnerTeamId);
    }

    /// <summary>
    /// The published surface: the owner and the running claim reach the mode
    /// observation, and the claim's SIGN names the claiming team so two
    /// facts fit in two fields.
    /// </summary>
    [Fact]
    public void TheOwnerAndTheSignedClaimAreProjectedIntoTheObservation()
    {
        ActorResolvedMatchDefinition definition = Arm();
        FrontlineModeKernel kernel = Kernel(definition);
        FrontlineControlState state = kernel.CreateInitialState();
        int tick = 0;

        Assert.Null(Project(definition, state).SecondaryOwnerTeamId);
        Assert.Equal(0, Project(definition, state).SecondaryClaimProgress);

        state = Step(kernel, ref tick, state, Weight((1, 1)));
        state = Step(kernel, ref tick, state, Weight((1, 1)));
        Assert.Null(Project(definition, state).SecondaryOwnerTeamId);
        Assert.Equal(-2, Project(definition, state).SecondaryClaimProgress);

        for (int held = 2; held < FrontlineLabsMusterSite.LatchTicks; held++)
            state = Step(kernel, ref tick, state, Weight((1, 1)));
        Assert.Equal(1, Project(definition, state).SecondaryOwnerTeamId);
        Assert.Equal(0, Project(definition, state).SecondaryClaimProgress);

        // A ruleset with no side objective publishes the neutral pair
        // forever, so a bot reading these fields never has to branch on
        // whether the mechanic exists.
        ActorResolvedMatchDefinition plain = FrontlineLabsDefinition.Create();
        FrontlineModeKernel plainKernel = Kernel(plain);
        GenericActorRuntimeObservation.ModeObservationState.Frontline neutral =
            Project(plain, plainKernel.CreateInitialState());
        Assert.Null(neutral.SecondaryOwnerTeamId);
        Assert.Equal(0, neutral.SecondaryClaimProgress);
    }

    /// <summary>
    /// The effect. The same forward-rally derivation the keel hands both
    /// teams unconditionally now answers to the flag: the owner's PRIME
    /// lands on its own-side chain-adjacent objective, everyone else lands
    /// on the reserved home spawn, and both teams' rally tiles stay exact
    /// reflections.
    /// </summary>
    [Fact]
    public void OnlyTheOwningTeamsPrimeRalliesForward()
    {
        ActorResolvedMatchDefinition definition = Arm();
        ActorUnitSlotLifecycleAssignmentDefinition prime =
            definition.LifecycleAssignments.Single(assignment =>
                assignment.TeamId == 0 && assignment.UnitId == 0);
        ActorUnitSlotLifecycleAssignmentDefinition child =
            definition.LifecycleAssignments.Single(assignment =>
                assignment.TeamId == 0 && assignment.UnitId == 1);
        var home = new Position(2, 7);
        ImmutableHashSet<Position> free = [];

        Position Resolve(
            ActorUnitSlotLifecycleAssignmentDefinition assignment,
            int teamId,
            Position spawn,
            int? owner) =>
            FrontlineForwardRallyPlacement.Resolve(
                definition,
                teamId,
                spawn,
                activePositionIndex: 2,
                free,
                assignment,
                owner);

        // Neutral flag, enemy-held flag, and a child slot all walk home.
        Assert.Equal(home, Resolve(prime, 0, home, null));
        Assert.Equal(home, Resolve(prime, 0, home, 1));
        Assert.Equal(home, Resolve(child, 0, home, 0));

        // Owning the flag moves the Prime to the rear-most free tile of the
        // own-side chain-adjacent objective, measured along its own advance
        // direction.
        Position forward = Resolve(prime, 0, home, 0);
        Assert.NotEqual(home, forward);
        Assert.Equal(new Position(6, 5), forward);

        // The mirror: team 1's rally tile is the exact reflection of team
        // 0's across the map's fairness axis.
        ActorUnitSlotLifecycleAssignmentDefinition enemyPrime =
            definition.LifecycleAssignments.Single(assignment =>
                assignment.TeamId == 1 && assignment.UnitId == 0);
        var enemyHome = new Position(20, 7);
        Position enemyForward = Resolve(enemyPrime, 1, enemyHome, 1);
        Assert.Equal(
            new Position(definition.Map.Width - 1 - forward.X, forward.Y),
            enemyForward);
    }

    private static FrontlineControlState Step(
        FrontlineModeKernel kernel,
        ref int tick,
        FrontlineControlState state,
        ImmutableDictionary<int, int> siteWeightByTeam)
    {
        FrontlineControlState next = kernel.ApplyJointTick(
            state,
            tick,
            ImmutableDictionary<int, int>.Empty,
            siteWeightByTeam).State;
        tick++;
        return next;
    }

    private static GenericActorRuntimeObservation.ModeObservationState
        .Frontline Project(
            ActorResolvedMatchDefinition definition,
            FrontlineControlState state) =>
        FrontlineControlProjection.Project(
            definition.Rules.GameMode.ModeId,
            state);
}
