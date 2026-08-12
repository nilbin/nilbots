using System.Collections.Immutable;
using System.Security.Cryptography;
using BotArena.Engine;
using BotArena.Runtime;

namespace BotArena.Cli.Tests;

/// <summary>
/// Fast product-scene coverage for the real ATL2 executor. Small pure tests in
/// <see cref="TacticalMovementGovernorTests"/> pin arbitration mechanics; this
/// fixture catches the integration failures those cannot: bodies queueing in
/// the reactor, a fight/recovery/carrier plan never publishing traffic, empty
/// reasons, or high-level intent disappearing during clearance.
/// </summary>
public sealed class OrdersMovementSceneTests
{
    [Fact]
    public void TaplineExercisesUrgencyAndClearsBothReactorDeployments()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunter.Composition,
            wellwright.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline1);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, hunter), Participant(1, wellwright)],
            matchSeed: 9001,
            recordChronology: true);

        Dictionary<ActorIdentity, Position> initial = session.ActiveLives
            .ToDictionary(life => life.ActorId, life => life.Position);
        var movedInitialLives = new HashSet<ActorIdentity>();
        var reasons = new List<string>();
        var banksByTeam = new Dictionary<int, int>();
        var runs = new Dictionary<ActorIdentity, (Position Position, int Ticks)>();
        int longestSingleTileRun = 0;
        GenericActorMatchStepResult? last = null;

        while (!session.IsCompleted)
        {
            last = session.Step();
            foreach (GenericMindRuntimeTurn turn in last.MindTurns)
            {
                Assert.Null(turn.RuntimeFault);
                foreach (GenericMindCommandResolution resolution in turn.Commands)
                {
                    Assert.Equal(GenericMindCommandOutcome.Accepted,
                        resolution.Outcome);
                    string reason = resolution.Command.DebugMessage ?? "";
                    Assert.False(string.IsNullOrWhiteSpace(reason));
                    reasons.Add(reason);
                }
            }
            foreach (GenericActorWorldSnapshot.LifeSnapshot life
                     in last.PostState.ActiveLives)
            {
                if (initial.TryGetValue(life.ActorId, out Position start)
                    && life.Position != start)
                {
                    movedInitialLives.Add(life.ActorId);
                }
                (Position Position, int Ticks) prior =
                    runs.GetValueOrDefault(life.ActorId);
                int ticks = prior.Position == life.Position
                    ? prior.Ticks + 1
                    : 0;
                runs[life.ActorId] = (life.Position, ticks);
                longestSingleTileRun = Math.Max(longestSingleTileRun, ticks);
            }
            foreach (GenericActorAuthoritativeEvent fact
                     in last.AuthoritativeEvents)
            {
                if (fact.Payload is GenericActorRuntimeObservation.EventPayload
                    .ArcRelay { Fact: ArcRelayEvent.CoreBanked banked })
                {
                    banksByTeam[banked.TeamId] =
                        banksByTeam.GetValueOrDefault(banked.TeamId) + 1;
                }
            }
        }

        Assert.NotNull(last);
        Assert.True(last.IsCompleted);
        Assert.Equal(initial.Count, movedInitialLives.Count);
        Assert.True(longestSingleTileRun <= 100,
            $"A real Tapline body stayed on one tile for "
            + $"{longestSingleTileRun} ticks.");
        Assert.All(new[] { 0, 1 }, team =>
            Assert.True(banksByTeam.GetValueOrDefault(team) > 0,
                $"Team {team} never banked a Core."));

        // These are semantic scenes, not hashes or outcome expectations. The
        // same match must exercise all three authored weights, carrier return,
        // recovery traffic, queue clearance, and combat approach while keeping
        // the selected order path in every reason. Detour/rejoin is pinned by
        // the smaller route-governor scenes; whether this seed needs one is an
        // emergent traffic fact and therefore not an integration invariant.
        Assert.Contains(reasons, reason => reason.Contains(
            "urgency-routine", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "urgency-normal", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "urgency-urgent", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            ":march@", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "score-a-core", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "recover-health", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            ":make-way:", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "fight-visible-enemy", StringComparison.Ordinal));
        Assert.DoesNotContain(reasons, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public void FacingLockedBodyAtDestinationClearsAStarvingMover()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunter.Composition,
            wellwright.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline1);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, hunter), Participant(1, wellwright)],
            matchSeed: 9002,
            recordChronology: true);

        int currentBlockedTicks = 0;
        int longestBlockedRun = 0;
        int builderClearanceTurns = 0;
        int builderClearanceSteps = 0;

        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn hunterTurn = Assert.Single(
                step.MindTurns, turn => turn.TeamId == 0);
            GenericMindCommandResolution? hauler = hunterTurn.Commands
                .SingleOrDefault(resolution => resolution.Command.UnitId == 2);
            if (hauler is null)
            {
                currentBlockedTicks = 0;
                continue;
            }
            string haulerReason = hauler.Command.DebugMessage ?? "";
            bool centreWellBlocked = haulerReason.Contains(
                "work-centre-well", StringComparison.Ordinal)
                && haulerReason.Contains(
                    "move-blocked:well:centre", StringComparison.Ordinal);
            currentBlockedTicks = centreWellBlocked
                ? currentBlockedTicks + 1
                : 0;
            longestBlockedRun = Math.Max(
                longestBlockedRun, currentBlockedTicks);

            GenericMindCommandResolution? builder = hunterTurn.Commands
                .SingleOrDefault(resolution => resolution.Command.UnitId == 7);
            if (builder is null)
                continue;
            string builderReason = builder.Command.DebugMessage ?? "";
            if (builderReason.Contains(
                    "hold-centre", StringComparison.Ordinal)
                && builderReason.Contains(
                    ":make-way:u2:", StringComparison.Ordinal))
            {
                if (string.Equals(
                        builder.Command.ActionId, "rotate",
                        StringComparison.Ordinal))
                {
                    builderClearanceTurns++;
                }
                if (string.Equals(
                        builder.Command.ActionId, "move-eight-way",
                        StringComparison.Ordinal))
                {
                    builderClearanceSteps++;
                }
            }
        }

        // Owner-replay catch (Tapline seed 9002): a normal hauler trying to
        // enter the centre Well accrued 39 debt ticks behind an at-target
        // Mason. The plane asked the Mason to clear, but the greedy chooser
        // proposed a diagonal that a facing-locked body cannot execute and
        // never tried a cardinal alternative. Clearance must include both
        // the preparation turn and the actual step; the mover may wait for
        // that bounded two-tick action, not starve behind it.
        // A clear route is even better than exercising clearance. When this
        // owner scene does need the at-target Mason to move, the two-step
        // rotate/step pair must remain intact; when it does not, neither half
        // is required merely to preserve an implementation-shaped trace.
        Assert.Equal(builderClearanceTurns > 0, builderClearanceSteps > 0);
        Assert.True(longestBlockedRun <= 6,
            $"The centre-Well mover remained blocked for "
            + $"{longestBlockedRun} consecutive ticks.");
    }

    [Fact]
    public void PassageWinnerCannotBeImmediatelyRotatedBackIntoTheG3Cycle()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            wellwright.Composition,
            hunter.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline1);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, wellwright), Participant(1, hunter)],
            matchSeed: 9005,
            recordChronology: true);

        ActorIdentity[] actors =
        [
            new(0, 3, 1),
            new(0, 4, 1),
            new(0, 6, 1),
        ];
        var frames = new List<PassageFrame>();
        int passageReasons = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeObservation observation = step.TickStart
                .MindObservations.Single(value => value.TeamId == 0);
            Dictionary<ActorIdentity, Position> positions = observation.Bodies
                .ToDictionary(value => value.ActorId, value => value.Position);
            if (actors.All(positions.ContainsKey))
            {
                GenericMindRuntimeTurn turn = step.MindTurns.Single(
                    value => value.TeamId == 0);
                bool madeWay = turn.Commands.Any(resolution =>
                    actors.Contains(resolution.Command.ActorIdIn(turn.TeamId))
                    && (resolution.Command.DebugMessage ?? "").Contains(
                        ":make-way:", StringComparison.Ordinal));
                passageReasons += turn.Commands.Count(resolution =>
                    (resolution.Command.DebugMessage ?? "").Contains(
                        "+passage", StringComparison.Ordinal));
                frames.Add(new PassageFrame(
                    step.Tick,
                    positions[actors[0]],
                    positions[actors[1]],
                    positions[actors[2]],
                    madeWay));
            }
        }

        int sustainedReturns = 0;
        foreach (IGrouping<(Position, Position, Position), PassageFrame> visits
                 in frames.GroupBy(frame => (
                     frame.First, frame.Second, frame.Third)))
        {
            PassageFrame[] ordered = visits.OrderBy(frame => frame.Tick).ToArray();
            for (int index = 2; index < ordered.Length; index++)
            {
                PassageFrame first = ordered[index - 2];
                PassageFrame middle = ordered[index - 1];
                PassageFrame last = ordered[index];
                int period = middle.Tick - first.Tick;
                if (period < 2
                    || period > 12
                    || last.Tick - middle.Tick != period)
                {
                    continue;
                }
                bool firstLapMadeWay = frames.Any(frame =>
                    frame.Tick > first.Tick
                    && frame.Tick <= middle.Tick
                    && frame.MadeWay);
                bool secondLapMadeWay = frames.Any(frame =>
                    frame.Tick > middle.Tick
                    && frame.Tick <= last.Tick
                    && frame.MadeWay);
                if (firstLapMadeWay && secondLapMadeWay)
                    sustainedReturns++;
            }
        }

        // Owner catch, gallery G3: these exact three live bodies rotated one
        // contested opening every six ticks from t138 through t164. Every
        // move succeeded, but debt payment made the winner immediately yield
        // back to the next waiter. The real scene must exercise the bounded
        // lease and must not complete even one sustained assigned-position
        // traffic circuit.
        Assert.True(passageReasons > 0,
            "The motivating scene never exercised passage ownership.");
        Assert.Equal(0, sustainedReturns);
    }

    private static GenericActorParticipantConfiguration Participant(
        int participantId,
        TacticalPlaybookCompilation playbook) => new()
    {
        ParticipantId = participantId,
        TeamId = participantId,
        Name = participantId == 0 ? "Hunter" : "Wellwright",
        MindRuntimeFactory = new InProcessGenericMindRuntimeFactory(
            static () => new global::ArcRelayTacticalPlaybookMind()),
        RuntimeKind = "orders-movement-scene",
        ArtifactHash = "orders-movement-scene",
        MindDataHash = Convert.ToHexStringLower(
            SHA256.HashData(playbook.LinkedData)),
        MindEvaluationData = ImmutableArray.Create(playbook.LinkedData),
        Accent = participantId == 0 ? "#22d3ee" : "#fb5360",
        LookId = "arc-relay-sheet",
        ProjectileLookId = ArcRelayH0ReplayPresentation.ProjectileLookId,
    };

    private static TacticalPlaybookCompilation Compile(
        string root,
        string fileName) => ArcRelayTacticalPlaybookCompiler.Compile(
        Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "playbooks",
            fileName));

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root is missing.");
    }

    private sealed record PassageFrame(
        int Tick,
        Position First,
        Position Second,
        Position Third,
        bool MadeWay);
}
