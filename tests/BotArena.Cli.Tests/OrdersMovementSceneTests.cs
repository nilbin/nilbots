using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    [Theory]
    [InlineData(16, 12)] // east/front of the authored resting facing
    [InlineData(14, 12)] // west/rear
    [InlineData(15, 11)] // north side
    [InlineData(15, 13)] // south side
    public void HoldDefenderTracksAndFiresWithoutLeavingItsPost(
        int hostileX,
        int hostileY)
    {
        string root = FindRepoRoot();
        var hostilePost = new Position(hostileX, hostileY);
        TacticalPlaybookCompilation defender = CompileHoldDefenceFixture(
            root, defender: true, hostilePost);
        TacticalPlaybookCompilation visitor = CompileHoldDefenceFixture(
            root, defender: false, hostilePost);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            defender.Composition,
            visitor.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline14);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, defender), Participant(1, visitor)],
            matchSeed: 9001,
            recordChronology: false);

        var post = new Position(15, 12);
        bool fired = false;
        bool targetDestroyed = false;
        bool resumedRestingFacing = false;
        int fightCommands = 0;
        int cadenceCommands = 0;
        Position? lastVisitor = null;
        Position? lastHolder = null;
        Direction? lastHolderFacing = null;
        string lastHolderReason = "";
        while (!session.IsCompleted && !resumedRestingFacing)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindCommandResolution holder = step.MindTurns
                .Single(turn => turn.TeamId == 0).Commands
                .Single(command => command.Command.UnitId == 0);
            string reason = holder.Command.DebugMessage ?? "";
            lastHolderReason = reason;
            GenericMindRuntimeObservation.ObservedBodyState before = step.TickStart
                .MindObservations.Single(value => value.TeamId == 0)
                .Bodies.Single(value => value.ActorId.UnitId == 0);
            if (reason.Contains(
                    "holder > hold-visible-hostile:",
                    StringComparison.Ordinal))
            {
                fightCommands++;
                if (reason.Contains(":cadence:", StringComparison.Ordinal))
                    cadenceCommands++;
                Assert.Equal(post, before.Position);
                Assert.DoesNotContain("move", holder.Command.ActionId,
                    StringComparison.OrdinalIgnoreCase);
                fired |= reason.Contains("; cover ", StringComparison.Ordinal);
            }
            targetDestroyed |= step.AuthoritativeEvents.Any(value =>
                value.Payload is GenericActorRuntimeObservation.EventPayload
                    .Destruction destroyed
                && destroyed.ActorId.TeamId == 1
                && destroyed.ActorId.UnitId == 0);
            if (targetDestroyed
                && (reason.Contains("face-anchor:rest-face",
                        StringComparison.Ordinal)
                    || reason.Contains("facing-anchor:rest-face",
                        StringComparison.Ordinal)))
            {
                GenericActorWorldSnapshot.LifeSnapshot after = step.PostState
                    .ActiveLives.Single(value =>
                        value.ActorId.TeamId == 0
                        && value.ActorId.UnitId == 0);
                resumedRestingFacing = after.Facing == Direction.East;
            }
            GenericActorWorldSnapshot.LifeSnapshot? visitorLife = step.PostState
                .ActiveLives.FirstOrDefault(value =>
                    value.ActorId.TeamId == 1 && value.ActorId.UnitId == 0);
            lastVisitor = visitorLife?.Position;
            GenericActorWorldSnapshot.LifeSnapshot? holderAfter = step.PostState
                .ActiveLives.FirstOrDefault(value =>
                    value.ActorId.TeamId == 0 && value.ActorId.UnitId == 0);
            lastHolder = holderAfter?.Position;
            lastHolderFacing = holderAfter?.Facing;
        }

        string diagnostic = $"visitor={lastVisitor},holder={lastHolder},"
            + $"facing={lastHolderFacing},reason={lastHolderReason}";
        Assert.True(fightCommands > 0,
            $"the Hold defender never selected Fight; {diagnostic}");
        Assert.True(fired, $"the Hold defender never fired; {diagnostic}");
        Assert.True(cadenceCommands > 0,
            $"the Hold defender never retained combat facing through cadence; {diagnostic}");
        Assert.True(targetDestroyed,
            $"the Hold defender never finished its target; {diagnostic}");
        Assert.True(resumedRestingFacing,
            $"the Hold defender did not restore its authored east facing; {diagnostic}");
    }

    [Fact]
    public void HoldDefenderClosesWithinItsLeashAgainstAnOutrangingAttacker()
    {
        string root = FindRepoRoot();
        var holderPost = new Position(12, 15);
        // Tapline-10's five-tile sight still exposes an attacker whose
        // six-tile Longshot gun outranges the Palisade's short gun.
        var hostilePost = new Position(17, 15);
        TacticalPlaybookCompilation defender = CompileHoldDefenceFixture(
            root,
            defender: true,
            hostilePost,
            defenderClass: "palisade",
            visitorClass: "longshot",
            chaseLimit: 5,
            visitorFights: true,
            enemyDistanceLimit: 7,
            holderPost: holderPost);
        TacticalPlaybookCompilation visitor = CompileHoldDefenceFixture(
            root,
            defender: false,
            hostilePost,
            defenderClass: "palisade",
            visitorClass: "longshot",
            chaseLimit: 5,
            visitorFights: true,
            enemyDistanceLimit: 7,
            holderPost: holderPost);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            defender.Composition,
            visitor.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline14);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, defender), Participant(1, visitor)],
            matchSeed: 9001,
            recordChronology: false);

        Position post = holderPost;
        bool counterRangeClose = false;
        bool holderFired = false;
        bool visitorDestroyed = false;
        bool holderReturned = false;
        int maximumExcursion = 0;
        string lastReason = "";
        while (!session.IsCompleted && !holderReturned)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindCommandResolution holder = step.MindTurns
                .Single(turn => turn.TeamId == 0).Commands
                .Single(command => command.Command.UnitId == 0);
            string holderReason = holder.Command.DebugMessage ?? "";
            lastReason = holderReason;
            counterRangeClose |= holderReason.Contains(
                "hold-counter-range-close", StringComparison.Ordinal);
            GenericActorWorldSnapshot.LifeSnapshot? holderLife = step.PostState
                .ActiveLives.FirstOrDefault(value =>
                    value.ActorId.TeamId == 0 && value.ActorId.UnitId == 0);
            if (counterRangeClose && !visitorDestroyed && holderLife is not null)
            {
                maximumExcursion = Math.Max(
                    maximumExcursion,
                    holderLife.Position.ChebyshevDistance(post));
            }
            holderFired |= holderReason.Contains(
                ":fight:", StringComparison.Ordinal);
            visitorDestroyed |= step.AuthoritativeEvents.Any(value =>
                value.Payload is GenericActorRuntimeObservation.EventPayload
                    .Destruction destroyed
                && destroyed.ActorId.TeamId == 1
                && destroyed.ActorId.UnitId == 0);
            holderReturned = visitorDestroyed
                && holderLife?.Position == post;
        }

        string diagnostic = $"close={counterRangeClose},"
            + $"fired={holderFired},destroyed={visitorDestroyed},"
            + $"returned={holderReturned},"
            + $"excursion={maximumExcursion},reason={lastReason}";
        Assert.True(counterRangeClose,
            $"Hold never closed on the unanswered ranged threat; {diagnostic}");
        Assert.True(holderFired,
            $"the Hold defender closed but never counter-fired; {diagnostic}");
        Assert.InRange(maximumExcursion, 1, 5);
        Assert.True(visitorDestroyed,
            $"the Hold defender never removed the ranged threat; {diagnostic}");
        Assert.True(holderReturned,
            $"the Hold defender never returned to its authored ground; {diagnostic}");
    }

    [Fact]
    public void HoldDefenderPursuesAnEscapingCarrierWithinItsLeashThenReturns()
    {
        string root = FindRepoRoot();
        var holderPost = new Position(11, 13);
        TacticalPlaybookCompilation defender = CompileHoldDefenceFixture(
            root,
            defender: true,
            hostilePost: new Position(20, 13),
            defenderClass: "kestrel",
            visitorClass: "palisade",
            chaseLimit: 5,
            enemyDistanceLimit: 7,
            holderPost: holderPost);
        TacticalPlaybookCompilation visitor = CompileCoreRunnerFixture(root);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            defender.Composition,
            visitor.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline14);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, defender), Participant(1, visitor)],
            matchSeed: 9001,
            recordChronology: false);

        bool pickedUp = false;
        bool carrierClose = false;
        bool holderFired = false;
        bool carrierDestroyed = false;
        bool holderReturned = false;
        int maximumExcursion = 0;
        int outerRingDeclarations = 0;
        int holderShots = 0;
        int carrierHits = 0;
        string lastReason = "";
        while (!session.IsCompleted && !holderReturned)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindCommandResolution holder = step.MindTurns
                .Single(turn => turn.TeamId == 0).Commands
                .Single(command => command.Command.UnitId == 0);
            string reason = holder.Command.DebugMessage ?? "";
            lastReason = reason;
            var arc = Assert.IsType<GenericActorRuntimeObservation
                .ModeObservationState.ArcRelay>(step.PostState.Mode);
            pickedUp |= arc.VisibleCores.Any(core =>
                core.CarrierActorId is { TeamId: 1, UnitId: 0 });
            carrierClose |= reason.Contains(
                "hold-carrier-close", StringComparison.Ordinal);
            holderFired |= carrierClose && reason.Contains(
                ":fight:", StringComparison.Ordinal);
            if (carrierClose
                && reason.Contains(":fight:", StringComparison.Ordinal))
            {
                GenericMindRuntimeObservation observation = step.TickStart
                    .MindObservations.Single(value => value.TeamId == 0);
                GenericMindRuntimeObservation.ObservedBodyState holderBefore =
                    observation.Bodies.Single(value => value.ActorId.UnitId == 0);
                GenericActorRuntimeObservation.ObservedEnemyState? carrierBefore =
                    observation.Enemies.FirstOrDefault(value =>
                        value.ActorId.TeamId == 1 && value.ActorId.UnitId == 0);
                if (carrierBefore is not null
                    && holderBefore.Position.ChebyshevDistance(
                        carrierBefore.Position) >= 3)
                {
                    outerRingDeclarations++;
                }
            }
            GenericActorWorldSnapshot.LifeSnapshot? holderLife = step.PostState
                .ActiveLives.FirstOrDefault(value =>
                    value.ActorId.TeamId == 0 && value.ActorId.UnitId == 0);
            if (carrierClose && !carrierDestroyed && holderLife is not null)
            {
                maximumExcursion = Math.Max(
                    maximumExcursion,
                    holderLife.Position.ChebyshevDistance(holderPost));
            }
            carrierDestroyed |= step.AuthoritativeEvents.Any(value =>
                value.Payload is GenericActorRuntimeObservation.EventPayload
                    .Destruction destroyed
                && destroyed.ActorId.TeamId == 1
                && destroyed.ActorId.UnitId == 0);
            holderShots += step.AuthoritativeEvents.Count(value =>
                value.Payload is GenericActorRuntimeObservation.EventPayload
                    .Attack attack
                && attack.ActorId.TeamId == 0
                && attack.ActorId.UnitId == 0);
            carrierHits += step.AuthoritativeEvents.Count(value =>
                value.Payload is GenericActorRuntimeObservation.EventPayload
                    .Damage damage
                && damage.TargetActorId.TeamId == 1
                && damage.TargetActorId.UnitId == 0);
            holderReturned = carrierDestroyed
                && holderLife?.Position == holderPost;
        }

        string diagnostic = $"pickup={pickedUp},close={carrierClose},"
            + $"fired={holderFired},destroyed={carrierDestroyed},"
            + $"returned={holderReturned},excursion={maximumExcursion},"
            + $"outer={outerRingDeclarations},shots={holderShots},"
            + $"hits={carrierHits},"
            + $"reason={lastReason}";
        Assert.True(pickedUp,
            $"the runner never became a Core carrier; {diagnostic}");
        Assert.True(carrierClose,
            $"Hold let the visible carrier escape without pursuit; {diagnostic}");
        Assert.True(holderFired,
            $"Hold pursued the carrier but never fired; {diagnostic}");
        Assert.Equal(0, outerRingDeclarations);
        Assert.True(holderShots > 0,
            $"Hold declared inside its windup margin but launched no shot; {diagnostic}");
        Assert.True(carrierHits > 0,
            $"Hold launched but never hit the escaping carrier; {diagnostic}");
        Assert.InRange(maximumExcursion, 1, 5);
        Assert.True(carrierDestroyed,
            $"the bounded defence never stopped the carrier; {diagnostic}");
        Assert.True(holderReturned,
            $"the defender never returned to its authored ground; {diagnostic}");
    }

    [Fact]
    public void CarrierDefenceTurnsButYieldsCadenceToBanking()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation carrier =
            CompileCarrierDefenceRunnerFixture(root);
        TacticalPlaybookCompilation visitor = CompileCarrierThreatFixture(root);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            carrier.Composition,
            visitor.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline14);
        int runnerUnit = Array.IndexOf(carrier.Composition, "palisade");
        Assert.True(runnerUnit >= 0);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, carrier), Participant(1, visitor)],
            matchSeed: 9001,
            recordChronology: false);

        bool carried = false;
        bool defended = false;
        bool movedOnCadence = false;
        bool banked = false;
        int stationary = 0;
        int longestStationary = 0;
        Position? prior = null;
        string lastReason = "";
        string lastThreats = "";
        string lastVisible = "";
        string firstCarry = "";
        var carryTrace = new List<string>();
        while (!session.IsCompleted && !banked)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn westTurn = step.MindTurns
                .Single(turn => turn.TeamId == 0);
            if (westTurn.RuntimeFault is not null)
                lastReason = westTurn.RuntimeFault.ToString();
            GenericMindCommandResolution? command = westTurn.Commands
                .FirstOrDefault(value => value.Command.UnitId == runnerUnit);
            string reason = command?.Command.DebugMessage ?? "";
            string actionId = command?.Command.ActionId ?? "";
            if (command is not null)
                lastReason = reason;
            var arc = Assert.IsType<GenericActorRuntimeObservation
                .ModeObservationState.ArcRelay>(step.PostState.Mode);
            GenericActorWorldSnapshot.LifeSnapshot? body = step.PostState
                .ActiveLives.FirstOrDefault(value =>
                    value.ActorId.TeamId == 0
                    && value.ActorId.UnitId == runnerUnit);
            bool carrying = arc.VisibleCores.Any(core =>
                core.CarrierActorId?.TeamId == 0
                && core.CarrierActorId.UnitId == runnerUnit);
            carried |= carrying;
            if (carrying && body is not null)
            {
                if (carryTrace.Count < 20)
                    carryTrace.Add($"{step.Tick}:{body.Position}:{actionId}:{reason}");
                if (firstCarry.Length == 0)
                {
                    var core = arc.VisibleCores.Single(value =>
                            value.CarrierActorId?.TeamId == 0
                            && value.CarrierActorId.UnitId == runnerUnit);
                    firstCarry = $"{core.CoreId.SourceWellId}@{body.Position}"
                        + $" facing-{body.Facing} visible-"
                        + string.Join(",", step.TickStart.MindObservations
                            .Single(value => value.TeamId == 0).Enemies
                            .Select(value =>
                                $"{value.ActorId.UnitId}@{value.Position}"));
                }
                lastThreats = string.Join(",", step.PostState.ActiveLives
                    .Where(value => value.ActorId.TeamId == 1)
                    .Select(value => $"{value.ActorId.UnitId}@{value.Position}"));
                lastVisible = string.Join(",", step.TickStart.MindObservations
                    .Single(value => value.TeamId == 0).Enemies
                    .Select(value => $"{value.ActorId.UnitId}@{value.Position}"));
                bool moved = prior is Position position
                    && position != body.Position;
                stationary = moved ? 0 : stationary + 1;
                longestStationary = Math.Max(longestStationary, stationary);
                defended |= reason.Contains(
                        "carrier-defence", StringComparison.Ordinal)
                    && actionId is "rotate" or "shoot-direction";
                movedOnCadence |= defended
                    && string.Equals(
                        actionId,
                        "move-eight-way",
                        StringComparison.Ordinal)
                    && reason.Contains("return-core", StringComparison.Ordinal);
                prior = body.Position;
            }
            else
            {
                stationary = 0;
                prior = body?.Position;
            }
            banked |= step.AuthoritativeEvents.Any(value =>
                value.Payload is GenericActorRuntimeObservation.EventPayload
                    .ArcRelay
                    { Fact: ArcRelayEvent.CoreBanked bank }
                && bank.CarrierActorId.TeamId == 0
                && bank.CarrierActorId.UnitId == runnerUnit);
        }

        string diagnostic = $"carried={carried},defended={defended},"
            + $"movedOnCadence={movedOnCadence},banked={banked},"
            + $"stationary={longestStationary},threats={lastThreats},"
            + $"visible={lastVisible},first={firstCarry},reason={lastReason}";
        diagnostic += $",trace={string.Join("|", carryTrace)}";
        Assert.True(carried, diagnostic);
        Assert.True(defended, diagnostic);
        Assert.True(movedOnCadence, diagnostic);
        Assert.True(banked, diagnostic);
        Assert.True(longestStationary < 8, diagnostic);
    }

    [Fact]
    public void PremiumCustodyHandoffRetargetsEscortAndBanksTheSameCore()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation transfer = CompileProgramCustodyFixture(
            root, "premium-transfer-proof");
        TacticalPlaybookCompilation idle = CompileHistoricalTapline6(
            root, "idle-field-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            transfer.Composition,
            idle.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline8);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, transfer), Participant(1, idle)],
            matchSeed: 9001,
            recordChronology: false);

        ArcRelayEvent.CoreHandedOff? handoff = null;
        ArcRelayEvent.CoreBanked? bank = null;
        int handoffTick = -1;
        int bankTick = -1;
        int escortSourceTick = -1;
        int escortReceiverTick = -1;
        int receiverReturnTick = -1;
        while (!session.IsCompleted && bank is null)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution command in step.MindTurns
                .Single(turn => turn.TeamId == 0).Commands)
            {
                string reason = command.Command.DebugMessage ?? "";
                Assert.False(string.IsNullOrWhiteSpace(reason));
                if (command.Command.UnitId == 2
                    && reason.Contains("escort-carrier", StringComparison.Ordinal))
                {
                    if (reason.Contains(
                            "move:body:0:0:0", StringComparison.Ordinal))
                    {
                        escortSourceTick = step.Tick;
                    }
                    if (escortReceiverTick < 0 && reason.Contains(
                            "move:body:0:1:0", StringComparison.Ordinal))
                    {
                        escortReceiverTick = step.Tick;
                    }
                }
                if (receiverReturnTick < 0
                    && command.Command.UnitId == 1
                    && reason.Contains(
                        "premium-four-carry-home", StringComparison.Ordinal))
                {
                    receiverReturnTick = step.Tick;
                }
            }
            foreach (GenericActorAuthoritativeEvent fact
                     in step.AuthoritativeEvents)
            {
                if (handoff is null
                    && fact.Payload is GenericActorRuntimeObservation
                        .EventPayload.ArcRelay
                        { Fact: ArcRelayEvent.CoreHandedOff transferred }
                    && transferred.SourceActorId.TeamId == 0)
                {
                    handoff = transferred;
                    handoffTick = step.Tick;
                }
                if (handoff is not null
                    && fact.Payload is GenericActorRuntimeObservation
                        .EventPayload.ArcRelay
                        { Fact: ArcRelayEvent.CoreBanked banked }
                    && banked.CoreId == handoff.CoreId)
                {
                    bank = banked;
                    bankTick = step.Tick;
                }
            }
        }

        Assert.NotNull(handoff);
        Assert.Equal(new ActorIdentity(0, 0, 0), handoff.SourceActorId);
        Assert.Equal(new ActorIdentity(0, 1, 0), handoff.TargetActorId);
        Assert.Equal(53, handoffTick);
        Assert.Equal(54, receiverReturnTick);
        Assert.True(escortSourceTick >= 0 && escortSourceTick <= handoffTick);
        Assert.Equal(54, escortReceiverTick);
        Assert.NotNull(bank);
        Assert.Equal(handoff.CoreId, bank.CoreId);
        Assert.Equal(handoff.TargetActorId, bank.CarrierActorId);
        Assert.Equal(4, bank.ChargeAwarded);
        Assert.Equal(171, bankTick);
    }

    [Fact]
    public void GeneratedRelayThrowsForwardReclaimsAndBanksWithoutAReceiver()
    {
        string root = FindRepoRoot();
        string fixture = Path.Combine(
            root, "tests", "BotArena.Cli.Tests", "Fixtures", "tactics",
            "core-raid.tactics.json");
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(fixture))!.AsObject();
        JsonObject tactic = source["tactics"]![0]!.AsObject();
        JsonArray groups = tactic["groups"]!.AsArray();
        groups[0]!["slots"] = new JsonArray(6);
        groups[0]!["stage"] = new JsonObject
        {
            ["kind"] = "approach",
            ["posture"] = "steal",
            ["recovery"] = "fight-on",
            ["point"] = new JsonArray(15, 13),
            ["via"] = new JsonArray(
                new JsonArray(6, 13),
                new JsonArray(10, 13)),
        };
        groups[1]!["slots"] = new JsonArray(0, 1, 2, 3, 4, 5, 7);
        tactic["release"] = new JsonObject
        {
            ["mode"] = "ready-when",
            ["conds"] = new JsonObject
            {
                ["coreLoose"] = new JsonObject { ["well"] = "centre" },
            },
        };
        tactic["finish"] = new JsonObject
        {
            ["outcomes"] = new JsonObject
            {
                ["coreBanked"] = true,
                ["coreLost"] = true,
            },
            ["maxAttempt"] = "none",
        };
        TacticalPlaybookCompilation relay =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(source.ToJsonString()));
        TacticalPlaybookCompilation idle = CompileHistoricalTapline6(
            root, "idle-field-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            relay.Composition,
            idle.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline8);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, relay), Participant(1, idle)],
            matchSeed: 9001,
            recordChronology: false);

        ArcRelayEvent.CoreDropped? departure = null;
        ArcRelayEvent.CorePickedUp? reclaim = null;
        ArcRelayEvent.CoreBanked? bank = null;
        int departureTick = -1;
        bool reclaimInstruction = false;
        var relayReasons = new Queue<string>();
        var coreFacts = new Queue<string>();
        Position? relayPosition = null;
        while (!session.IsCompleted && bank is null)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution command in step.MindTurns
                         .Single(turn => turn.TeamId == 0).Commands)
            {
                string reason = command.Command.DebugMessage ?? "";
                Assert.False(string.IsNullOrWhiteSpace(reason));
                if (command.Command.UnitId == 6)
                {
                    relayReasons.Enqueue($"{step.Tick}:{reason}");
                    if (relayReasons.Count > 8) relayReasons.Dequeue();
                }
                if (departure is not null
                    && command.Command.UnitId == 6
                    && reason.Contains("reclaim-toss", StringComparison.Ordinal)
                    && reason.Contains("reclaim-own-toss", StringComparison.Ordinal))
                {
                    reclaimInstruction = true;
                }
            }
            foreach (GenericActorAuthoritativeEvent fact in step.AuthoritativeEvents)
            {
                if (fact.Payload is not GenericActorRuntimeObservation
                    .EventPayload.ArcRelay mode)
                {
                    continue;
                }
                if (mode.Fact is ArcRelayEvent.CoreDropped
                    or ArcRelayEvent.CoreRelocated
                    or ArcRelayEvent.CorePickedUp
                    or ArcRelayEvent.CoreBanked)
                {
                    coreFacts.Enqueue($"{step.Tick}:{mode.Fact}");
                    if (coreFacts.Count > 20) coreFacts.Dequeue();
                }
                if (mode.Fact is ArcRelayEvent.CoreDropped
                    {
                        Kind: ArcRelayEvent.CoreDropKind.SignatureDeparture,
                        SourceActorId.TeamId: 0,
                        SourceActorId.UnitId: 6,
                    } dropped)
                {
                    departure = dropped;
                    departureTick = step.Tick;
                    reclaim = null;
                    reclaimInstruction = false;
                }
                else if (departure is not null
                    && mode.Fact is ArcRelayEvent.CorePickedUp picked
                    && picked.CoreId == departure!.CoreId
                    && picked.CarrierActorId.TeamId == 0
                    && picked.CarrierActorId.UnitId == 6)
                {
                    reclaim = picked;
                }
                else if (reclaim is not null
                    && mode.Fact is ArcRelayEvent.CoreBanked banked
                    && banked.CoreId == departure!.CoreId)
                {
                    bank = banked;
                }
            }
            GenericActorWorldSnapshot.LifeSnapshot? relayLife = step.PostState
                .ActiveLives.FirstOrDefault(value =>
                    value.ActorId.TeamId == 0 && value.ActorId.UnitId == 6);
            relayPosition = relayLife?.Position;
        }

        string diagnostic = $"departureTick={departureTick},relayPosition={relayPosition},"
            + $"reasons={string.Join(" | ", relayReasons)},"
            + $"coreFacts={string.Join(" | ", coreFacts)}";
        Assert.True(departure is not null, diagnostic);
        Assert.True(reclaimInstruction, diagnostic);
        Assert.True(reclaim is not null, diagnostic);
        Assert.True(bank is not null, diagnostic);
        Assert.Equal(new ActorIdentity(0, 6, 0), bank.CarrierActorId);
        Assert.NotEqual(departure.Position, reclaim.Position);
        Assert.True(
            reclaim.Position.ChebyshevDistance(bank.Position)
                < departure.Position.ChebyshevDistance(bank.Position),
            diagnostic);
    }

    [Fact]
    public void RoleScopedPassForwardSkipsCloserWrongRoleAndReceiverBanksSameCore()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation transfer = CompileProgramCustodyFixture(
            root, "premium-pass-forward-proof");
        TacticalPlaybookCompilation idle = CompileHistoricalTapline6(
            root, "idle-field-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            transfer.Composition,
            idle.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline8);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, transfer), Participant(1, idle)],
            matchSeed: 9001,
            recordChronology: false);

        ArcRelayEvent.CoreHandedOff? handoff = null;
        ArcRelayEvent.CoreBanked? bank = null;
        Position? sourceAtHandoff = null;
        Position? wrongRoleAtHandoff = null;
        Position? receiverAtHandoff = null;
        int handoffTick = -1;
        int receiverReturnTick = -1;
        int escortReceiverTick = -1;
        int bankTick = -1;
        while (!session.IsCompleted && bank is null)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution command in step.MindTurns
                .Single(turn => turn.TeamId == 0).Commands)
            {
                string reason = command.Command.DebugMessage ?? "";
                Assert.False(string.IsNullOrWhiteSpace(reason));
                if (handoff is not null && receiverReturnTick < 0
                    && command.Command.UnitId == 2
                    && reason.Contains(
                        "premium-four-carry-home", StringComparison.Ordinal))
                {
                    receiverReturnTick = step.Tick;
                }
                if (handoff is not null && escortReceiverTick < 0
                    && command.Command.UnitId == 3
                    && reason.Contains(
                        "move:body:0:2:0", StringComparison.Ordinal))
                {
                    escortReceiverTick = step.Tick;
                }
            }
            foreach (GenericActorAuthoritativeEvent fact
                     in step.AuthoritativeEvents)
            {
                if (handoff is null
                    && fact.Payload is GenericActorRuntimeObservation
                        .EventPayload.ArcRelay
                        { Fact: ArcRelayEvent.CoreHandedOff transferred }
                    && transferred.SourceActorId.TeamId == 0)
                {
                    handoff = transferred;
                    handoffTick = step.Tick;
                    sourceAtHandoff = PositionOf(step.PostState, 0);
                    wrongRoleAtHandoff = PositionOf(step.PostState, 1);
                    receiverAtHandoff = PositionOf(step.PostState, 2);
                }
                if (handoff is not null
                    && fact.Payload is GenericActorRuntimeObservation
                        .EventPayload.ArcRelay
                        { Fact: ArcRelayEvent.CoreBanked banked }
                    && banked.CoreId == handoff.CoreId)
                {
                    bank = banked;
                    bankTick = step.Tick;
                }
            }
        }

        Assert.NotNull(handoff);
        Assert.Equal(new ActorIdentity(0, 0, 0), handoff.SourceActorId);
        Assert.Equal(new ActorIdentity(0, 2, 0), handoff.TargetActorId);
        Assert.Equal(new Position(5, 16), sourceAtHandoff);
        Assert.Equal(new Position(5, 15), wrongRoleAtHandoff);
        Assert.Equal(new Position(4, 15), receiverAtHandoff);
        Assert.Equal(1, SquaredDistance(
            sourceAtHandoff!.Value, wrongRoleAtHandoff!.Value));
        Assert.Equal(2, SquaredDistance(
            sourceAtHandoff.Value, receiverAtHandoff!.Value));
        Assert.True(handoff.TargetActorId.UnitId > 1,
            "the lower-id adjacent wrong-role candidate must be ignored");
        Assert.Equal(147, handoffTick);
        Assert.True(receiverReturnTick > handoffTick);
        Assert.True(escortReceiverTick > handoffTick);
        Assert.NotNull(bank);
        Assert.Equal(handoff.CoreId, bank.CoreId);
        Assert.Equal(handoff.TargetActorId, bank.CarrierActorId);
        Assert.Equal(4, bank.ChargeAwarded);
        Assert.Equal(301, bankTick);
    }

    [Fact]
    public void PremiumCarrierRecoversThenResumesItsSameCustodyRouteAndBanks()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation recovery = CompileProgramCustodyFixture(
            root, "premium-carrier-recovery-proof");
        TacticalPlaybookCompilation drill = CompileProgramCustodyFixture(
            root, "premium-carrier-recovery-drill");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            recovery.Composition,
            drill.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline8);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, recovery), Participant(1, drill)],
            matchSeed: 9001,
            recordChronology: false);

        ArcRelayEvent.CoreHandedOff? handoff = null;
        ArcRelayEvent.CoreBanked? bank = null;
        int healStartTick = -1;
        int firstHealthGainTick = -1;
        int returnResumeTick = -1;
        int bankTick = -1;
        int minimumCarrierHealth = int.MaxValue;
        int previousCarrierHealth = int.MaxValue;
        while (!session.IsCompleted && bank is null)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindCommandResolution receiver = step.MindTurns
                .Single(turn => turn.TeamId == 0).Commands
                .Single(command => command.Command.UnitId == 1);
            string reason = receiver.Command.DebugMessage ?? "";
            Assert.False(string.IsNullOrWhiteSpace(reason));
            if (healStartTick < 0
                && reason.Contains("move:heal:", StringComparison.Ordinal))
            {
                healStartTick = step.Tick;
            }
            else if (healStartTick >= 0 && returnResumeTick < 0
                && reason.Contains("carry-home", StringComparison.Ordinal)
                && !reason.Contains("move:heal:", StringComparison.Ordinal))
            {
                returnResumeTick = step.Tick;
            }

            GenericActorWorldSnapshot.LifeSnapshot? carrier = step.PostState
                .ActiveLives.FirstOrDefault(life =>
                    life.ActorId.TeamId == 0 && life.ActorId.UnitId == 1);
            if (carrier is not null)
            {
                minimumCarrierHealth = Math.Min(
                    minimumCarrierHealth, carrier.Health);
                if (healStartTick >= 0 && firstHealthGainTick < 0
                    && previousCarrierHealth < int.MaxValue
                    && carrier.Health > previousCarrierHealth)
                {
                    firstHealthGainTick = step.Tick;
                }
                previousCarrierHealth = carrier.Health;
            }

            foreach (GenericActorAuthoritativeEvent fact
                     in step.AuthoritativeEvents)
            {
                if (handoff is null
                    && fact.Payload is GenericActorRuntimeObservation
                        .EventPayload.ArcRelay
                        { Fact: ArcRelayEvent.CoreHandedOff transferred }
                    && transferred.SourceActorId.TeamId == 0)
                {
                    handoff = transferred;
                }
                if (handoff is not null
                    && fact.Payload is GenericActorRuntimeObservation
                        .EventPayload.ArcRelay
                        { Fact: ArcRelayEvent.CoreBanked banked }
                    && banked.CoreId == handoff.CoreId)
                {
                    bank = banked;
                    bankTick = step.Tick;
                }
            }
        }

        Assert.NotNull(handoff);
        Assert.Equal(new ActorIdentity(0, 1, 0), handoff.TargetActorId);
        Assert.Equal(20, minimumCarrierHealth);
        Assert.Equal(108, healStartTick);
        Assert.Equal(134, firstHealthGainTick);
        Assert.Equal(138, returnResumeTick);
        Assert.NotNull(bank);
        Assert.Equal(handoff.CoreId, bank.CoreId);
        Assert.Equal(handoff.TargetActorId, bank.CarrierActorId);
        Assert.Equal(4, bank.ChargeAwarded);
        Assert.Equal(190, bankTick);
    }

    [Theory]
    [InlineData("hold", "wait")]
    [InlineData("aggressive", "wait")]
    [InlineData("hold", "hold")]
    [InlineData("aggressive", "aggressive")]
    public void GeneratedTravelCombatStopsTheMarchAndFires(
        string travel,
        string destinationPosture)
    {
        string root = FindRepoRoot();
        string fixture = Path.Combine(
            root, "tests", "BotArena.Cli.Tests", "Fixtures", "tactics",
            "pincer.tactics.json");
        JsonObject source = JsonNode.Parse(
            ArcRelayTacticalPlaybookCompiler.MigrateTactics(
                File.ReadAllBytes(fixture)))!.AsObject();
        source["mapId"] = "arc-relay-tapline-03";
        JsonObject tactic = source["tactics"]![0]!.AsObject();
        JsonObject phase = tactic["phases"]![0]!.AsObject();
        // Keep this phase active and make the six-body group cross the whole
        // centre lane. Demolition March approaches from the other side, so
        // the route necessarily produces real travel contact before arrival.
        phase.Remove("continue");
        phase["jobs"]!["flankers"] = new JsonObject
        {
            ["kind"] = "area",
            ["rect"] = new JsonArray(23, 11, 26, 15),
            ["travel"] = travel,
            ["posture"] = destinationPosture,
            ["recovery"] = "fight-on",
        };
        tactic["phases"] = new JsonArray(phase.DeepClone());
        TacticalPlaybookCompilation traveller =
            ArcRelayTacticalPlaybookCompiler.CompileTactics(
                Encoding.UTF8.GetBytes(source.ToJsonString()));
        TacticalPlaybookCompilation demolition = Compile(
            root, "demolition-march-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            traveller.Composition,
            demolition.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline14);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, traveller), Participant(1, demolition)],
            matchSeed: 9001,
            recordChronology: false);

        bool selectedTravelFight = false;
        bool firedFromTravelFight = false;
        int longestCommittedRun = 0;
        var committedTicks = new Dictionary<int, int>();
        var recent = new Queue<string>();
        int ticks = 0;
        int commandsSeen = 0;
        int ordinaryRouteMoves = 0;
        string events = "";
        while (!session.IsCompleted && ticks++ < 220
            && (!firedFromTravelFight || longestCommittedRun < 2))
        {
            GenericActorMatchStepResult step = session.Step();
            events = string.Join(" | ", step.AuthoritativeEvents.Select(
                value => value.Payload.ToString()));
            foreach (GenericMindCommandResolution command in step.MindTurns
                         .Single(turn => turn.TeamId == 0).Commands)
            {
                commandsSeen++;
                string reason = command.Command.DebugMessage ?? "";
                recent.Enqueue($"{step.Tick}:{command.Command.UnitId}:"
                    + $"{command.Command.ActionId}:{reason}");
                if (recent.Count > 24) recent.Dequeue();
                bool selected = reason.Contains(
                    "t1-p1-g2-travel-fight", StringComparison.Ordinal);
                int run = selected
                    ? committedTicks.GetValueOrDefault(command.Command.UnitId) + 1
                    : 0;
                committedTicks[command.Command.UnitId] = run;
                longestCommittedRun = Math.Max(longestCommittedRun, run);
                selectedTravelFight |= selected;
                firedFromTravelFight |= selected
                    && command.Command.ActionId == "shoot-direction";

                if (command.Command.ActionId == "move-eight-way"
                    && reason.Contains(":march@", StringComparison.Ordinal)
                    && !selected)
                {
                    GenericMindRuntimeObservation.ObservedBodyState before =
                        step.TickStart.MindObservations
                            .Single(value => value.TeamId == 0)
                            .Bodies.Single(value => value.ActorId.UnitId
                                == command.Command.UnitId);
                    GenericActorWorldSnapshot.LifeSnapshot? after =
                        step.PostState.ActiveLives.FirstOrDefault(value =>
                            value.ActorId == before.ActorId);
                    if (after is not null && after.Position != before.Position)
                    {
                        ordinaryRouteMoves++;
                        int dx = after.Position.X - before.Position.X;
                        int dy = after.Position.Y - before.Position.Y;
                        Assert.True(
                            Visibility.InQuadrant(dx, dy, after.Facing),
                            $"ordinary route move strafed sideways: "
                                + $"{before.Position}->{after.Position}, "
                                + $"facing={after.Facing}, reason={reason}");
                    }
                }
            }
        }

        Assert.True(selectedTravelFight,
            $"{travel}/{destinationPosture} never displaced the travel "
                + "Move with Fight. "
                + $"ticks={ticks},commands={commandsSeen},completed="
                + $"{session.IsCompleted}. events={events}. "
                + string.Join(" | ", recent));
        Assert.True(longestCommittedRun >= 2,
            $"{travel} did not remain committed across consecutive ticks.");
        Assert.True(firedFromTravelFight,
            $"{travel} selected Fight but never attacked from it.");
        Assert.True(ordinaryRouteMoves > 0,
            $"{travel}/{destinationPosture} produced no ordinary route move "
                + "to verify route-facing locomotion.");
    }

    [Fact]
    public void PracticeReasonNamesRejectedHigherProgramGate()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation program = Compile(
            root, "mid-overload-fork-v1.json");
        TacticalPlaybookCompilation drill = Compile(
            root, "mid-overload-drill-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            program.Composition,
            drill.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline8);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, program), Participant(1, drill)],
            matchSeed: 9001,
            recordChronology: false);
        GenericActorMatchStepResult step = session.Step();
        string reason = step.MindTurns.Single(turn => turn.TeamId == 0)
            .Commands.Single(command => command.Command.UnitId == 0)
            .Command.DebugMessage ?? "";

        Assert.Contains(":eligibility[", reason, StringComparison.Ordinal);
        Assert.Contains(
            "mid-fork-home-mason-respond~when-",
            reason,
            StringComparison.Ordinal);
        Assert.Contains(":march@", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void LockboxAmbushLineHasOneLegalSlotPerInterceptor()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        using JsonDocument playbook = JsonDocument.Parse(
            lockbox.NormalizedPlaybook);
        int interceptorCount = playbook.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Single(role => role.GetProperty("id").GetString()
                == "return-lane-interceptors")
            .GetProperty("classes")
            .GetArrayLength();
        using JsonDocument mapPlan = JsonDocument.Parse(
            lockbox.NormalizedLayout);
        JsonElement plan = mapPlan.RootElement;
        int[] rect = plan.GetProperty("zones")
            .GetProperty("enemy-return-ambush")
            .GetProperty("rect")
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();
        Position[] authored = (from y in Enumerable.Range(
                                   rect[1], rect[3] - rect[1] + 1)
                               from x in Enumerable.Range(
                                   rect[0], rect[2] - rect[0] + 1)
                               select new Position(x, y)).ToArray();
        int[] endpointValue = plan.GetProperty("routes")
            .GetProperty("north-centre-bypass")
            .GetProperty("waypoints")
            .EnumerateArray()
            .Last()
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();
        var endpoint = new Position(endpointValue[0], endpointValue[1]);

        ActorMapDefinition map = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline6).Map;
        Position[] mirrored = authored.Select(tile => new Position(
            map.Width - 1 - tile.X,
            map.Height - 1 - tile.Y)).ToArray();
        var mirroredEndpoint = new Position(
            map.Width - 1 - endpoint.X,
            map.Height - 1 - endpoint.Y);
        HashSet<Position> protectedHome = map.Regions
            .Where(region => region.RegionId is "home-west" or "home-east")
            .SelectMany(region => region.Tiles)
            .ToHashSet();

        // Four simultaneous interceptors used to target a wall-split box with
        // only three open squares. A later attempted repair accidentally put
        // the zone inside the enemy's unenterable home region. Keep the four
        // legal staging slots beyond, rather than on, the via endpoint.
        Assert.Equal(interceptorCount, authored.Length);
        Assert.All(authored.Concat(mirrored), tile =>
        {
            Assert.False(map.IsWall(tile));
            Assert.DoesNotContain(tile, protectedHome);
        });
        Assert.DoesNotContain(endpoint, authored);
        Assert.DoesNotContain(mirroredEndpoint, mirrored);
    }

    [Fact]
    public void LockboxInterceptorCarryUsesTheInnerFlankForItsWholeEpisode()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        using JsonDocument playbook = JsonDocument.Parse(
            lockbox.NormalizedPlaybook);
        JsonElement interceptor = playbook.RootElement.GetProperty("roles")
            .EnumerateArray()
            .Single(role => role.GetProperty("id").GetString()
                == "return-lane-interceptors");
        JsonElement safeReturn = interceptor.GetProperty("orders")
            .EnumerateArray()
            .Single(order => order.TryGetProperty("id", out JsonElement id)
                && id.GetString() == "return-intercepted-core-safely");
        JsonElement escape = safeReturn.GetProperty("orders")
            .EnumerateArray()
            .Single(order => order.GetProperty("id").GetString()
                == "escape-with-stolen-core");
        JsonElement stage = interceptor.GetProperty("orders")
            .EnumerateArray()
            .Single(order => order.TryGetProperty("id", out JsonElement id)
                && id.GetString() == "ambush-centre-carriers")
            .GetProperty("orders")
            .EnumerateArray()
            .Single(order => order.GetProperty("id").GetString()
                == "stage-return-lane-ambush");

        string[] startConditions = safeReturn.GetProperty("when")
            .GetProperty("all")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        Assert.Equal(
            ["carrying-core", "inside-enemy-return-pickup"],
            startConditions);
        Assert.Equal(
            "empty-handed",
            safeReturn.GetProperty("until").GetString());
        Assert.Equal("normal", stage.GetProperty("urgency").GetString());
        Assert.Equal(
            "inner-flank-extraction",
            escape.GetProperty("move").GetProperty("via").GetString());

        using JsonDocument mapPlan = JsonDocument.Parse(
            lockbox.NormalizedLayout);
        Assert.Equal(
            [24, 14, 27, 18],
            mapPlan.RootElement.GetProperty("zones")
                .GetProperty("enemy-return-pickup")
                .GetProperty("rect")
                .EnumerateArray()
                .Select(value => value.GetInt32())
                .ToArray());
        Position[] extraction = RoutePoints(
            mapPlan.RootElement.GetProperty("routes"),
            "inner-flank-extraction");

        // Pickup can resolve one tile outside the narrow ambush rectangle, so
        // the return belongs to the interceptor's entire carrying episode,
        // not to a coordinate snapshot. These west-authored points bind to an
        // immediate south turn for the east team: down to the nearby inner
        // horizontal lane, neither through centre nor around the perimeter.
        Assert.Equal(
            [
                new Position(25, 13),
                new Position(19, 7),
                new Position(19, 3),
                new Position(8, 3),
            ],
            extraction);

        ActorMapDefinition map = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline6).Map;
        Position[] eastBinding = extraction.Select(tile => new Position(
            map.Width - 1 - tile.X,
            map.Height - 1 - tile.Y)).ToArray();
        HashSet<Position> routeWaypointForbidden = map.TileTags
            .Where(tag => tag.Kind
                == ActorMapTileTagDefinition.TileTagKind.SpawnProtected)
            .SelectMany(tag => tag.Tiles)
            .Concat(map.Regions
                .Where(region => region.RegionId is "home-west" or "home-east")
                .SelectMany(region => region.Tiles))
            .ToHashSet();
        Assert.All(
            extraction.Concat(eastBinding),
            tile => Assert.DoesNotContain(tile, routeWaypointForbidden));
        HashSet<Position> enemyHome = map.Regions
            .Single(region => region.RegionId == "home-west")
            .Tiles
            .ToHashSet();
        var pickup = new Position(4, 11);
        var reactor = new Position(28, 21);
        var traversed = new List<Position> { pickup };
        Position from = pickup;
        foreach (Position destination in eastBinding.Append(reactor))
        {
            Position[] segment = ShortestCornerStrictTerrainPath(
                map,
                from,
                destination,
                enemyHome);
            Assert.NotEmpty(segment);
            traversed.AddRange(segment.Skip(1));
            from = destination;
        }

        Assert.Equal(new Position(5, 13), eastBinding[0]);
        Assert.Equal(32, traversed.Count - 1);
        Assert.DoesNotContain(traversed, enemyHome.Contains);
        Assert.DoesNotContain(
            traversed,
            tile => tile.X is >= 12 and <= 18
                && tile.Y is >= 11 and <= 15);
        Assert.DoesNotContain(traversed, tile => tile.Y >= 24);
    }

    [Fact]
    public void LockboxCentreScreenPocketIsOpenOnBothTeamBindings()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        using JsonDocument mapPlan = JsonDocument.Parse(
            lockbox.NormalizedLayout);
        int[] rect = mapPlan.RootElement
            .GetProperty("zones")
            .GetProperty("centre-screen-pocket")
            .GetProperty("rect")
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();
        Assert.Equal([12, 11, 14, 13], rect);

        ActorMapDefinition map = ArcRelayH0Definition.Create(
            loopProfile: ArcRelayLoopProfile.Tapline6).Map;
        Position[] west = (from y in Enumerable.Range(
                               rect[1], rect[3] - rect[1] + 1)
                           from x in Enumerable.Range(
                               rect[0], rect[2] - rect[0] + 1)
                           select new Position(x, y)).ToArray();
        Position[] east = west.Select(tile => new Position(
            map.Width - 1 - tile.X,
            map.Height - 1 - tile.Y)).ToArray();

        // Tapline-6 added a wall through the inherited Tapline-5 rectangle.
        // Zone expansion silently removed those cells and packed Lockbox's
        // centre screen into a choke. A formation pocket is authored floor,
        // not a rectangle whose useful shape changes when a map wall moves.
        Assert.Equal(9, west.Length);
        Assert.All(west.Concat(east), tile => Assert.False(map.IsWall(tile)));
    }

    [Fact]
    public void SquadPlannerConsumesOscillationAvoidanceBeforeSubmission()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation demolition = CompileHistoricalTapline6(
            root, "demolition-march-v1.json");
        TacticalPlaybookCompilation lockbox = CompileLockboxWithPocket(
            root, [10, 11, 12, 13]);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            demolition.Composition,
            lockbox.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, demolition), Participant(1, lockbox)],
            matchSeed: 9001,
            recordChronology: false);

        int plannerAvoidances = 0;
        var commandSeamDenials = new List<string>();
        while (!session.IsCompleted && session.Tick < 150)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn turn = Assert.Single(
                step.MindTurns, candidate => candidate.TeamId == 1);
            foreach (GenericMindCommandResolution command in turn.Commands)
            {
                string reason = command.Command.DebugMessage ?? "";
                if (reason.Contains(
                        ":planner-oscillation-yield@",
                        StringComparison.Ordinal))
                {
                    plannerAvoidances++;
                }
                if (reason.Contains(
                        ":oscillation-yield@",
                        StringComparison.Ordinal))
                {
                    commandSeamDenials.Add(
                        $"t{step.Tick}:u{command.Command.UnitId}:{reason}");
                }
            }
        }

        // Tapline-6 G1 owner catch: a respawned Repulsor walked A-B-A,
        // the oscillation governor denied B, and the squad planner selected B
        // again on every subsequent tick. Keep the old pocket as a fixture so
        // this match still exercises the planner/command seam independently
        // of the product geometry repair: avoidance must appear in planning
        // and must never leak through as a rejected move at submission. The
        // following product-scene test pins the alternative motion itself.
        Assert.True(plannerAvoidances > 0);
        Assert.Empty(commandSeamDenials);
    }

    [Fact]
    public void ReturnLaneAmbushStagingDoesNotWaitForever()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation demolition = CompileHistoricalTapline6(
            root, "demolition-march-v1.json");
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            demolition.Composition,
            lockbox.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, demolition), Participant(1, lockbox)],
            matchSeed: 9001,
            recordChronology: false);

        int[] watchedUnits = [4, 6];
        var previous = new Dictionary<int, Position>();
        var stationary = watchedUnits.ToDictionary(unit => unit, _ => 0);
        var worst = watchedUnits.ToDictionary(unit => unit, _ => 0);
        while (!session.IsCompleted && session.Tick < 95)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn lockboxTurn = Assert.Single(
                step.MindTurns, candidate => candidate.TeamId == 1);
            foreach (int unit in watchedUnits)
            {
                GenericActorWorldSnapshot.LifeSnapshot? life = step.PostState
                    .ActiveLives.SingleOrDefault(candidate =>
                        candidate.ActorId.TeamId == 1
                        && candidate.ActorId.UnitId == unit
                        && candidate.ActorId.LifeId == 0);
                GenericMindCommandResolution? command = lockboxTurn.Commands
                    .SingleOrDefault(candidate =>
                        candidate.Command.UnitId == unit
                        && candidate.Command.LifeId == 0);
                string reason = command?.Command.DebugMessage ?? "";
                bool staging = life is not null && reason.Contains(
                        "stage-return-lane-ambush", StringComparison.Ordinal)
                    && !reason.Contains(":at-target:", StringComparison.Ordinal);
                if (!staging)
                {
                    stationary[unit] = 0;
                    if (life is not null)
                        previous[unit] = life.Position;
                    continue;
                }
                stationary[unit] = previous.GetValueOrDefault(unit)
                        == life!.Position
                    ? stationary[unit] + 1
                    : 0;
                worst[unit] = Math.Max(worst[unit], stationary[unit]);
                previous[unit] = life.Position;
            }
        }

        // This scene originally caught u4 at (10,12) and u6 at (9,12)
        // waiting behind one another while an enemy occupied the remaining
        // mouth. The current geometry no longer guarantees that exact
        // congestion (and therefore must not be required to manufacture a
        // +b2 trace), but it remains a useful product-scene guard against the
        // failure the owner saw. Traffic-debt accrual and the resulting
        // detour/right-of-way choice are pinned directly by
        // TacticalMovementGovernorTests and SquadArenaPlannerTests.
        // Holding after reaching the authored ambush slot is success, not a
        // stationary movement failure.
        Assert.All(watchedUnits, unit => Assert.True(
            worst[unit] < 18,
            $"Lockbox u{unit} remained stationary under its ambush move for "
            + $"{worst[unit]} ticks."));
    }

    [Fact]
    public void UrgentCarrierKeepsCourseWhenLowerPriorityAllyMustYield()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation demolition = CompileHistoricalTapline6(
            root, "demolition-march-v1.json");
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            demolition.Composition,
            lockbox.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, demolition), Participant(1, lockbox)],
            matchSeed: 9001,
            recordChronology: false);

        int loadedReturnTicks = 0;
        int trafficDetourTicks = 0;
        int rightOfWayTicks = 0;
        Position? priorDistinctPosition = null;
        Position? lastDistinctPosition = null;
        int consecutiveReversals = 0;
        int worstConsecutiveReversals = 0;
        int purposelessTurnTicks = 0;
        while (!session.IsCompleted && session.Tick < 100)
        {
            GenericActorWorldSnapshot.LifeSnapshot? before =
                session.ActiveLives.SingleOrDefault(candidate =>
                    candidate.ActorId.TeamId == 0
                    && candidate.ActorId.UnitId == 2);
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn west = Assert.Single(
                step.MindTurns, candidate => candidate.TeamId == 0);
            GenericMindCommandResolution? carrier = west.Commands
                .SingleOrDefault(candidate => candidate.Command.UnitId == 2
                    && (candidate.Command.DebugMessage ?? "").Contains(
                        "return-to-reactor", StringComparison.Ordinal));
            if (carrier is null)
                continue;
            if (before is null || before.ActorId.LifeId
                != carrier.Command.LifeId)
                continue;

            loadedReturnTicks++;
            string reason = carrier.Command.DebugMessage ?? "";
            if (reason.Contains(":traffic-detour-", StringComparison.Ordinal))
                trafficDetourTicks++;
            if (reason.Contains(
                    ":traffic-right-of-way-", StringComparison.Ordinal))
            {
                rightOfWayTicks++;
            }
            int operationAt = reason.IndexOf(":op-", StringComparison.Ordinal);
            if (carrier.Command.ActionId == "rotate"
                && operationAt >= 0
                && !reason[(operationAt + 4)..].Split(':')[0]
                    .Contains('F'))
            {
                purposelessTurnTicks++;
            }
            GenericActorWorldSnapshot.LifeSnapshot? after = step.PostState
                .ActiveLives.SingleOrDefault(candidate =>
                    candidate.ActorId == before.ActorId);
            if (after is not null && after.Position != lastDistinctPosition)
            {
                bool reversal = priorDistinctPosition is Position prior
                    && after.Position == prior;
                consecutiveReversals = reversal
                    ? consecutiveReversals + 1
                    : 0;
                worstConsecutiveReversals = Math.Max(
                    worstConsecutiveReversals, consecutiveReversals);
                priorDistinctPosition = lastDistinctPosition;
                lastDistinctPosition = after.Position;
            }
        }

        // Tapline-6 owner catch: allied occupants became a migrating series of
        // route walls and walked a loaded body back toward its Well. This
        // current scene exercises the generic replacement: the urgent mover
        // is p0 because of authored urgency, keeps its course, and makes the
        // lower-ranked ally yield. Nothing in traffic ordering names carrier.
        Assert.True(loadedReturnTicks > 0);
        Assert.True(rightOfWayTicks > 0);
        Assert.Equal(0, trafficDetourTicks);
        Assert.Equal(0, purposelessTurnTicks);
        Assert.True(worstConsecutiveReversals <= 1,
            $"The loaded route reversed between one tile pair "
            + $"{worstConsecutiveReversals} consecutive times.");
    }

    [Fact]
    public void AlliedTrafficWithoutAnAlternateRouteWaitsInsteadOfOrbiting()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation demolition = CompileHistoricalTapline6(
            root, "demolition-march-v1.json");
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            demolition.Composition,
            lockbox.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, demolition), Participant(1, lockbox)],
            matchSeed: 9001,
            recordChronology: false);

        Position? previous = null;
        Position? current = null;
        int pairReversals = 0;
        int worstPairReversals = 0;
        while (!session.IsCompleted && session.Tick < 140)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn east = Assert.Single(
                step.MindTurns, candidate => candidate.TeamId == 1);
            GenericMindCommandResolution? command = east.Commands
                .SingleOrDefault(candidate => candidate.Command.UnitId == 5
                    && (candidate.Command.DebugMessage ?? "").Contains(
                        "stage-return-lane-ambush", StringComparison.Ordinal));
            GenericActorWorldSnapshot.LifeSnapshot? life = step.PostState
                .ActiveLives.SingleOrDefault(candidate =>
                    candidate.ActorId.TeamId == 1
                    && candidate.ActorId.UnitId == 5
                    && candidate.ActorId.LifeId == 0);
            if (command is null || life is null || life.Position == current)
                continue;
            bool reversal = previous is Position prior
                && life.Position == prior;
            pairReversals = reversal ? pairReversals + 1 : 0;
            worstPairReversals = Math.Max(worstPairReversals, pairReversals);
            previous = current;
            current = life.Position;
        }

        // Same G1 after the carrier-right-of-way correction: u5 was in a
        // one-tile corridor with an ally on the only forward exit. Treating
        // that occupant as a static obstacle made the goal unreachable; all
        // operation distances tied at infinity, so the action tie-break chose
        // A-B movement over waiting forever. A traffic obstacle is a detour
        // input only when a real alternate route exists.
        Assert.True(worstPairReversals < 4,
            $"The blocked body reversed across one tile "
            + $"{worstPairReversals} consecutive times.");
    }

    [Fact]
    public void LockboxViaIngressDoesNotBecomeAStationaryTurnLoop()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation demolition = CompileHistoricalTapline6(
            root, "demolition-march-v1.json");
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            demolition.Composition,
            lockbox.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, demolition), Participant(1, lockbox)],
            matchSeed: 9002,
            recordChronology: false);

        Position? stationaryTile = null;
        int stationaryTurnTicks = 0;
        int worstStationaryTurnTicks = 0;
        while (!session.IsCompleted && session.Tick < 160)
        {
            GenericActorWorldSnapshot.LifeSnapshot? before =
                session.ActiveLives.SingleOrDefault(candidate =>
                    candidate.ActorId.TeamId == 1
                    && candidate.ActorId.UnitId == 7);
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn east = Assert.Single(
                step.MindTurns, candidate => candidate.TeamId == 1);
            GenericMindCommandResolution? command = east.Commands
                .SingleOrDefault(candidate => candidate.Command.UnitId == 7);
            string reason = command?.Command.DebugMessage ?? "";
            bool preparingSameMove = before is not null
                && command?.Command.ActionId == "rotate"
                && reason.Contains(
                    "stage-return-lane-ambush", StringComparison.Ordinal);
            if (preparingSameMove && stationaryTile == before!.Position)
            {
                stationaryTurnTicks++;
            }
            else if (preparingSameMove)
            {
                stationaryTile = before!.Position;
                stationaryTurnTicks = 1;
            }
            else
            {
                stationaryTile = null;
                stationaryTurnTicks = 0;
            }
            worstStationaryTurnTicks = Math.Max(
                worstStationaryTurnTicks, stationaryTurnTicks);
        }

        // Tapline-6 review w-9002 exposed two faults in one visible scene.
        // The sheet sent four interceptors through a via endpoint into a
        // three-open-tile zone split by walls; that layout now provides four
        // open staging tiles beyond (not on) the via endpoint. Separately,
        // a facing-locked body must not keep selecting a new preparation turn
        // while its queue clears.
        // A three-action plan may spend at most its horizon preparing at one
        // tile; after that it must move, wait honestly, or choose a new goal.
        const int publishedOperationLength = 3;
        Assert.True(
            worstStationaryTurnTicks <= publishedOperationLength,
            $"The body prepared turns at one tile for "
            + $"{worstStationaryTurnTicks} consecutive ticks.");
    }

    [Fact]
    public void DirectMoveCourseDoesNotAlternateTurnsAtDeployment()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation lockbox = Compile(root, "lockbox-v1.json");
        TacticalPlaybookCompilation demolition = CompileHistoricalTapline6(
            root, "demolition-march-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            lockbox.Composition,
            demolition.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline6);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, lockbox), Participant(1, demolition)],
            matchSeed: 9002,
            recordChronology: false);

        const int watchedUnit = 3;
        Position start = session.ActiveLives.Single(life =>
            life.ActorId.TeamId == 1
            && life.ActorId.UnitId == watchedUnit).Position;
        var visited = new HashSet<Position>();
        int farthest = 0;
        while (!session.IsCompleted && session.Tick < 21)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericActorWorldSnapshot.LifeSnapshot? life = step.PostState
                .ActiveLives.SingleOrDefault(candidate =>
                    candidate.ActorId.TeamId == 1
                    && candidate.ActorId.UnitId == watchedUnit
                    && candidate.ActorId.LifeId == 0);
            if (life is null)
                continue;
            visited.Add(life.Position);
            farthest = Math.Max(
                farthest, start.ChebyshevDistance(life.Position));
        }

        // Cross-sheet catch from the G1 repair gate: a direct move (no
        // authored route) selected a valid persistent course around the
        // deployment reservations, but Chebyshev scoring treated its lateral
        // first step as worse than turning. The body alternated headings on
        // its spawn tile for 100 ticks. A direct course must be just as real
        // to EPIBT as an authored waypoint course.
        Assert.True(visited.Count >= 10,
            $"Demolition u{watchedUnit} visited only {visited.Count} tiles "
            + "during its opening march.");
        Assert.True(farthest >= 7,
            $"Demolition u{watchedUnit} never got more than {farthest} tiles "
            + "from its deployment anchor.");
    }

    [Fact]
    public void CrownedRailIngressDoesNotOrbitTheEastDeploymentRow()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation relay = CompileHistoricalTapline4(
            root, "relay-circuit-v1.json");
        TacticalPlaybookCompilation rail = CompileHistoricalTapline4(
            root, "crowned-rail-v1.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            relay.Composition,
            rail.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline4);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, relay), Participant(1, rail)],
            matchSeed: 9001,
            recordChronology: false);

        int[] watched = [3, 5];
        Dictionary<int, Position> starts = session.ActiveLives
            .Where(life => life.ActorId.TeamId == 1
                && watched.Contains(life.ActorId.UnitId))
            .ToDictionary(life => life.ActorId.UnitId, life => life.Position);
        Dictionary<int, HashSet<Position>> visited = watched.ToDictionary(
            unit => unit, _ => new HashSet<Position>());
        Dictionary<int, int> farthest = watched.ToDictionary(unit => unit, _ => 0);

        while (!session.IsCompleted && session.Tick < 31)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericActorWorldSnapshot.LifeSnapshot life in
                     step.PostState.ActiveLives.Where(life =>
                         life.ActorId.TeamId == 1
                         && life.ActorId.LifeId == 0
                         && watched.Contains(life.ActorId.UnitId)))
            {
                int unit = life.ActorId.UnitId;
                visited[unit].Add(life.Position);
                farthest[unit] = Math.Max(
                    farthest[unit],
                    starts[unit].ChebyshevDistance(life.Position));
            }
        }

        // Owner gallery catch, Tapline-4 G1: east Mason u3 alternated two
        // headings on its spawn tile for the whole match, while Lantern u5
        // orbited a handful of nearby cells. The held route had correctly
        // chosen a detour around the deployment reservations, but EPIBT's
        // terrain-only progress score rejected that necessary first step.
        // Both bodies must now commit real geographic progress through the
        // same authored ingress without naming either role in the executor.
        Assert.All(watched, unit =>
        {
            Assert.True(visited[unit].Count >= 10,
                $"East u{unit} visited only {visited[unit].Count} tiles "
                + "during its opening ingress.");
            Assert.True(farthest[unit] >= 5,
                $"East u{unit} never got more than {farthest[unit]} tiles "
                + "from its deployment anchor.");
        });
    }

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
            loopProfile: ArcRelayLoopProfile.Tapline3);

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
        // A body authored to farm a Well should hold that exact service tile
        // until a Core is born; a raw single-tile ceiling therefore labels
        // correct objective work as a deadlock (this seed legitimately holds
        // the south Well for 145 ticks). Mechanical immobility is covered by
        // the G3 closed-cycle/recovery-progress scene and the Core-level
        // gallery eligibility bar, not by punishing at-target waits.
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
            "epibt-", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            ":op-", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains(
            "fight-visible-enemy", StringComparison.Ordinal));
        Assert.DoesNotContain(reasons, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public void CentreWellMoverDoesNotStarveBehindAnAtTargetBody()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunter.Composition,
            wellwright.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, hunter), Participant(1, wellwright)],
            matchSeed: 9002,
            recordChronology: true);

        int currentBlockedTicks = 0;
        int longestBlockedRun = 0;

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

        }

        // Owner-replay catch (Tapline seed 9002): a normal hauler trying to
        // enter the centre Well accrued 39 debt ticks behind an at-target
        // Mason. The old one-step chooser proposed a diagonal that a
        // facing-locked body could not execute and never tried the cardinal
        // turn-and-vacate operation. The stable product invariant is that the
        // mover does not starve; whether this changing match scene needs a
        // turn, a step, both, or an already-clear route is emergent. The
        // planner's exact joint-operation mechanics have pure coverage.
        Assert.True(longestBlockedRun <= 6,
            $"The centre-Well mover remained blocked for "
            + $"{longestBlockedRun} consecutive ticks.");
    }

    [Fact]
    public void RemotePreferredTargetDoesNotParkG3BodiesAtTheFightLeash()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            wellwright.Composition,
            hunter.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, wellwright), Participant(1, hunter)],
            matchSeed: 9005,
            recordChronology: false);

        int actionableFallbacks = 0;
        int orderFallbacks = 0;
        int fightLeashHolds = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution resolution in
                     step.MindTurns.SelectMany(turn => turn.Commands))
            {
                string reason = resolution.Command.DebugMessage ?? "";
                if (reason.Contains(
                        "target-fallback-from-", StringComparison.Ordinal))
                    actionableFallbacks++;
                if (reason.Contains(
                        "after-fight-visible-enemy-no-actionable-target",
                        StringComparison.Ordinal))
                    orderFallbacks++;
                if (reason.Contains("fight-visible-enemy",
                        StringComparison.Ordinal)
                    && reason.Contains(":chase-limit:",
                        StringComparison.Ordinal))
                    fightLeashHolds++;
            }
        }

        // Owner catch, gallery G3: "weakest" chose a remote body before a
        // nearby contact and parked the west squad at its chase limit. The
        // same scene now demonstrates both deterministic escape paths: a
        // later actionable target wins, or the fight leaf yields to the next
        // authored order. It must never submit the old fight/leash hold.
        Assert.True(actionableFallbacks > 0,
            "The G3 scene never exercised a later actionable target.");
        Assert.True(orderFallbacks > 0,
            "The G3 scene never exercised order fallback after combat.");
        Assert.Equal(0, fightLeashHolds);
    }

    [Fact]
    public void G2BodiesWithoutReachableFiringGroundFallThroughInsteadOfHolding()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunter.Composition,
            wellwright.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, hunter), Participant(1, wellwright)],
            matchSeed: 9002,
            recordChronology: false);

        int orderFallbacks = 0;
        int noFiringGroundHolds = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution resolution in
                     step.MindTurns.SelectMany(turn => turn.Commands))
            {
                string reason = resolution.Command.DebugMessage ?? "";
                if (reason.Contains(
                        "after-fight-visible-enemy-no-actionable-target",
                        StringComparison.Ordinal))
                    orderFallbacks++;
                if (reason.Contains(
                        "fight-no-firing-ground", StringComparison.Ordinal))
                    noFiringGroundHolds++;
            }
        }

        // Owner replay catch, gallery G2 around t247: three east-side bodies
        // accepted the ordinary fight leaf, one closed while two stood in the
        // south Well under fight-no-firing-ground. Target selection and intent
        // planning now share exact reachable firing geometry, so such a body
        // runs its next authored order instead of entering a false fight.
        Assert.True(orderFallbacks > 0,
            "The G2 scene never exercised combat-to-order fallback.");
        Assert.Equal(0, noFiringGroundHolds);
    }

    [Fact]
    public void G3FollowerLeavesItsLeadersCourseClear()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            wellwright.Composition,
            hunter.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, wellwright), Participant(1, hunter)],
            matchSeed: 9005,
            recordChronology: false);

        int leaderEvictionsByFollower = 0;
        int longestLeaderEvictionRun = 0;
        int currentLeaderEvictionRun = 0;
        int leaderRouteSteps = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            GenericMindRuntimeTurn hunterTurn = Assert.Single(
                step.MindTurns, turn => turn.TeamId == 1);
            Dictionary<int, string> reasons = hunterTurn.Commands.ToDictionary(
                resolution => resolution.Command.UnitId,
                resolution => resolution.Command.DebugMessage ?? "");
            if (step.Tick is >= 0 and <= 45
                && (reasons.GetValueOrDefault(0) ?? "").Contains(
                    ":make-way:u1", StringComparison.Ordinal)
                && (reasons.GetValueOrDefault(1) ?? "").Contains(
                    "shadow-hunter", StringComparison.Ordinal))
            {
                leaderEvictionsByFollower++;
                currentLeaderEvictionRun++;
            }
            else
                currentLeaderEvictionRun = 0;
            longestLeaderEvictionRun = Math.Max(
                longestLeaderEvictionRun, currentLeaderEvictionRun);
            if (step.Tick is >= 0 and <= 45
                && (reasons.GetValueOrDefault(0) ?? "").Contains(
                    "shadow-hunt", StringComparison.Ordinal)
                && (reasons.GetValueOrDefault(0) ?? "").Contains(
                    ":march", StringComparison.Ordinal))
                leaderRouteSteps++;
        }

        // Owner catch, coordinated gallery G3 launch: follower u1 repeatedly
        // requested the route tile leader u0 was about to enter. Follow now
        // resolves an adjacent ring and excludes the leader's planned step.
        Assert.Equal(0, leaderEvictionsByFollower);
        Assert.Equal(0, longestLeaderEvictionRun);
        Assert.True(leaderRouteSteps > 0,
            "The leader never advanced under its authored route in the "
            + "opening scene.");
    }

    [Theory]
    [InlineData(9005, false)]
    [InlineData(9001, true)]
    public void GalleryScenesHaveNoClosedAlliedMovementCycleOrRecoveryOrbit(
        int seed,
        bool hunterIsWest)
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        GenericActorParticipantConfiguration[] participants = hunterIsWest
            ? [Participant(0, hunter), Participant(1, wellwright)]
            : [Participant(0, wellwright), Participant(1, hunter)];
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunterIsWest ? hunter.Composition : wellwright.Composition,
            hunterIsWest ? wellwright.Composition : hunter.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);
        using var session = new GenericActorMatchSession(
            definition,
            participants,
            matchSeed: (ulong)seed,
            recordChronology: true);

        int closedMovementCycles = 0;
        var recovery = new Dictionary<ActorIdentity, RecoveryProgress>();
        var carrierCourses = new Dictionary<ArcRelayCoreId, CarrierCourse>();
        int worstUncontestedRecoveryNoProgress = 0;
        int worstCarrierAlternation = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            Dictionary<ActorIdentity, GenericActorWorldSnapshot.LifeSnapshot>
                lives = step.PostState.ActiveLives.ToDictionary(
                    life => life.ActorId);
            foreach (GenericActorAuthoritativeEvent fact in
                     step.AuthoritativeEvents)
            {
                if (fact.Payload is not GenericActorRuntimeObservation.EventPayload
                    .MovementBlocked blocked)
                    continue;
                // The engine's event type intentionally exposes the same
                // actor/from/to triple as the replay. The generic cycle
                // helper below groups it after all facts for this tick.
            }
            closedMovementCycles += ClosedMovementCycles(
                step.AuthoritativeEvents);

            var arc = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                    step.PostState.Mode);
            HashSet<ArcRelayCoreId> carried = [];
            foreach (ArcRelayCoreState core in arc.VisibleCores.Where(core =>
                         core.CarrierActorId is not null))
            {
                carried.Add(core.CoreId);
                ActorIdentity carrier = core.CarrierActorId!;
                if (!carrierCourses.TryGetValue(
                        core.CoreId, out CarrierCourse? course)
                    || course.Carrier != carrier)
                {
                    carrierCourses[core.CoreId] = new CarrierCourse(
                        carrier, core.Position, null, 0);
                    continue;
                }
                if (core.Position == course.Current)
                    continue;
                int alternating = course.Previous is Position previous
                    && core.Position == previous
                        ? course.AlternatingSteps + 1
                        : 0;
                carrierCourses[core.CoreId] = course with
                {
                    Previous = course.Current,
                    Current = core.Position,
                    AlternatingSteps = alternating,
                };
                worstCarrierAlternation = Math.Max(
                    worstCarrierAlternation, alternating);
            }
            foreach (ArcRelayCoreId stale in carrierCourses.Keys
                         .Where(coreId => !carried.Contains(coreId))
                         .ToArray())
                carrierCourses.Remove(stale);

            foreach (GenericMindRuntimeTurn turn in step.MindTurns)
            {
                HashSet<ActorIdentity> seen = [];
                Position[] enemies = lives.Values
                    .Where(life => life.ActorId.TeamId != turn.TeamId)
                    .Select(life => life.Position)
                    .ToArray();
                foreach (GenericMindCommandResolution resolution in turn.Commands)
                {
                    string reason = resolution.Command.DebugMessage ?? "";
                    Position? goal = RecoveryGoal(reason);
                    ActorIdentity actor = new(
                        turn.TeamId,
                        resolution.Command.UnitId,
                        resolution.Command.LifeId);
                    if (goal is not Position healing
                        || !lives.TryGetValue(actor, out var life))
                        continue;
                    seen.Add(actor);
                    int distance = life.Position.ChebyshevDistance(healing);
                    bool contested = enemies.Any(enemy =>
                        enemy.ChebyshevDistance(healing) <= 1);
                    bool staged = reason.Contains(
                        ":at-contribution:heal:", StringComparison.Ordinal);
                    if (!recovery.TryGetValue(actor, out RecoveryProgress? prior)
                        || prior.Goal != healing
                        || life.Health > prior.Health
                        || distance < prior.BestDistance)
                    {
                        recovery[actor] = new RecoveryProgress(
                            healing, life.Health, distance, 0);
                    }
                    else
                    {
                        RecoveryProgress next = prior with
                        {
                            Health = life.Health,
                            UncontestedTicks = prior.UncontestedTicks
                                + (contested || staged ? 0 : 1),
                        };
                        recovery[actor] = next;
                        worstUncontestedRecoveryNoProgress = Math.Max(
                            worstUncontestedRecoveryNoProgress,
                            next.UncontestedTicks);
                    }
                }
                foreach (ActorIdentity stale in recovery.Keys
                             .Where(actor => actor.TeamId == turn.TeamId
                                 && !seen.Contains(actor))
                             .ToArray())
                    recovery.Remove(stale);
            }
        }

        Assert.Equal(0, closedMovementCycles);
        Assert.True(worstUncontestedRecoveryNoProgress < 18,
            $"A recovery order made no uncontested health/distance progress "
            + $"for {worstUncontestedRecoveryNoProgress} ticks.");
        Assert.True(worstCarrierAlternation < 8,
            $"A Core carrier alternated between the same two tiles for "
            + $"{worstCarrierAlternation} consecutive steps.");
    }

    [Fact]
    public void HunterMasonPlacesHardlightOnlyOnAuthoredEnemyLaneGate()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunter.Composition,
            wellwright.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, hunter), Participant(1, wellwright)],
            matchSeed: 9012,
            recordChronology: false);

        var operations = new HashSet<string>(StringComparer.Ordinal);
        var placements = new List<(Position Construct, Position Builder)>();
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            var arc = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                step.PostState.Mode);
            foreach (ArcRelaySignatureState signature in arc.VisibleSignatures)
            {
                if (signature.Kind
                        != ArcRelaySignatureDefinition.SignatureKind.HardlightBlock
                    || signature.OwnerTeamId != 0
                    || !operations.Add(signature.OperationId))
                    continue;
                Position builder = step.PostState.ActiveLives.Single(life =>
                    life.ActorId == signature.OwnerActorId).Position;
                placements.Add((Assert.Single(signature.Positions), builder));
            }
        }

        // One semantic anchor owns both halves: movement resolves a legal
        // adjacent stand tile, then use places on the exact enemy lane gate.
        // No companion "staging anchor" or duplicate coordinate is authored.
        Assert.NotEmpty(placements);
        Assert.All(placements, placement =>
        {
            Assert.Equal(new Position(25, 13), placement.Construct);
            Assert.Equal(1,
                placement.Builder.ChebyshevDistance(placement.Construct));
        });
    }

    [Fact]
    public void LoadedStandardCarrierDoesNotSelectAForbiddenSwiftStrafe()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation hunter = Compile(root, "hunter-tapline-v2.json");
        TacticalPlaybookCompilation wellwright = Compile(
            root, "wellwright-tapline-v2.json");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            hunter.Composition,
            wellwright.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, hunter), Participant(1, wellwright)],
            matchSeed: 9001,
            recordChronology: false);

        int loadedReturnTicks = 0;
        int submittedMovement = 0;
        int falseMoveTracesOnWait = 0;
        int explainedCadenceHolds = 0;
        int longestStationaryReturn = 0;
        var priorPositions = new Dictionary<ActorIdentity, Position>();
        var stationaryReturns = new Dictionary<ActorIdentity, int>();
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            var arc = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                    step.PostState.Mode);
            HashSet<ActorIdentity> carriers = arc.VisibleCores
                .Where(core => core.CarrierActorId is not null)
                .Select(core => core.CarrierActorId!)
                .ToHashSet();
            GenericMindRuntimeTurn hunterTurn = Assert.Single(
                step.MindTurns, turn => turn.TeamId == 0);
            var seenReturning = new HashSet<ActorIdentity>();
            foreach (GenericMindCommandResolution resolution in
                     hunterTurn.Commands)
            {
                GenericActorWorldSnapshot.LifeSnapshot? life =
                    step.PostState.ActiveLives.FirstOrDefault(candidate =>
                        candidate.ActorId.TeamId == 0
                        && candidate.ActorId.UnitId
                            == resolution.Command.UnitId
                        && candidate.ActorId.LifeId
                            == resolution.Command.LifeId);
                if (life is null || !carriers.Contains(life.ActorId))
                    continue;
                string reason = resolution.Command.DebugMessage ?? "";
                if (!reason.Contains(
                        "return-to-reactor", StringComparison.Ordinal))
                    continue;
                seenReturning.Add(life.ActorId);
                loadedReturnTicks++;
                bool moveTrace = reason.Contains(
                    ":epibt-move:", StringComparison.Ordinal);
                bool submitted = string.Equals(
                    resolution.Command.ActionId,
                    "move-eight-way",
                    StringComparison.Ordinal);
                if (submitted)
                    submittedMovement++;
                // A simultaneous collision can authoritatively block a real
                // submitted move; that is honest. The regression is a move
                // trace attached to a submitted Wait because an action row
                // with AllowedByForm=false was selected and then discarded.
                if (moveTrace && string.Equals(
                        resolution.Command.ActionId,
                        "wait",
                        StringComparison.Ordinal))
                    falseMoveTracesOnWait++;
                if (!submitted && reason.Contains(
                        "carry-relocation-cooldown-until-",
                        StringComparison.Ordinal))
                    explainedCadenceHolds++;
                bool moved = priorPositions.TryGetValue(
                        life.ActorId, out Position prior)
                    && prior != life.Position;
                int stationaryReturn = moved
                    ? 0
                    : stationaryReturns.GetValueOrDefault(life.ActorId) + 1;
                stationaryReturns[life.ActorId] = stationaryReturn;
                longestStationaryReturn = Math.Max(
                    longestStationaryReturn, stationaryReturn);
            }
            foreach (ActorIdentity stale in stationaryReturns.Keys
                         .Where(actor => !seenReturning.Contains(actor))
                         .ToArray())
                stationaryReturns.Remove(stale);
            foreach (GenericActorWorldSnapshot.LifeSnapshot life in
                     step.PostState.ActiveLives)
                priorPositions[life.ActorId] = life.Position;
        }

        // Owner catch, G1 around t30: west u6 held the centre Core at (13,11)
        // until destruction. EPIBT selected north-west every tick, but the
        // action chooser preferred strafe-eight-way solely because it would
        // preserve aim—even though Towline's legality row said
        // AllowedByForm=false. Submission then fell back to Wait while the
        // trace falsely retained epibt-move. A selected physical move must
        // become a real chassis-owned movement command.
        Assert.True(loadedReturnTicks > 0);
        Assert.True(submittedMovement > 0);
        Assert.Equal(0, falseMoveTracesOnWait);
        Assert.True(explainedCadenceHolds > 0);
        Assert.True(longestStationaryReturn < 8,
            $"A loaded returning carrier stayed still for "
            + $"{longestStationaryReturn} ticks.");
    }

    [Theory]
    [InlineData(
        "tapline-split-intercept-v1",
        "tapline-shadow-scissors-v2",
        9307)]
    [InlineData(
        "tapline-shadow-scissors-v2",
        "tapline-distributed-wells-v1",
        9306)]
    [InlineData(
        "tapline-split-intercept-v2",
        "tapline-shadow-scissors-v3",
        9403)]
    [InlineData(
        "tapline-distributed-wells-v2",
        "tapline-shadow-scissors-v3",
        9406)]
    public void HostileMoveContentionIsRememberedAcrossCarrierCadenceHold(
        string westStrategy,
        string eastStrategy,
        int seed)
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation west = CompileStrategy(
            root, westStrategy);
        TacticalPlaybookCompilation east = CompileStrategy(
            root, eastStrategy);
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            west.Composition,
            east.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, west), Participant(1, east)],
            matchSeed: (ulong)seed,
            recordChronology: false);

        var stationary = new Dictionary<ArcRelayCoreId, CarrierStation>();
        int longestStationaryCarrier = 0;
        int deniedStepTraces = 0;
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindCommandResolution resolution in
                     step.MindTurns.SelectMany(turn => turn.Commands))
            {
                if ((resolution.Command.DebugMessage ?? "").Contains(
                        ":avoid-denied-", StringComparison.Ordinal))
                    deniedStepTraces++;
            }

            var arc = Assert.IsType<
                GenericActorRuntimeObservation.ModeObservationState.ArcRelay>(
                    step.PostState.Mode);
            var carried = new HashSet<ArcRelayCoreId>();
            foreach (ArcRelayCoreState core in arc.VisibleCores.Where(core =>
                         core.CarrierActorId is not null))
            {
                carried.Add(core.CoreId);
                CarrierStation next = stationary.TryGetValue(
                        core.CoreId, out CarrierStation? prior)
                    && prior.Carrier == core.CarrierActorId
                    && prior.Position == core.Position
                        ? prior with { Ticks = prior.Ticks + 1 }
                        : new CarrierStation(
                            core.CarrierActorId!, core.Position, 1);
                stationary[core.CoreId] = next;
                longestStationaryCarrier = Math.Max(
                    longestStationaryCarrier, next.Ticks);
            }
            foreach (ArcRelayCoreId stale in stationary.Keys
                         .Where(core => !carried.Contains(core)).ToArray())
                stationary.Remove(stale);
        }

        // These two scenes independently produced the same failure: opposing
        // bodies repeatedly entered one visibly empty tile together. The
        // carrier's mandatory relocation wait then erased the generic
        // PreviousActionResolution memory, so it retried forever (59 and 32
        // ticks respectively). Two later three-step-planner scenes exposed
        // the second half of the same contract: making the denied tile illegal
        // without putting it in the carrier's distance field scored WAIT as
        // cheaper than every valid detour and produced 30/34-tick stalls.
        // The planner must carry that denial across the cadence hold and score
        // a genuinely different operation against the same obstacle set.
        Assert.True(deniedStepTraces > 0,
            "The scenes never exercised remembered denied-step routing.");
        Assert.True(longestStationaryCarrier < 30,
            $"A contested carrier stayed on one tile for "
            + $"{longestStationaryCarrier} ticks.");
    }

    [Fact]
    public void ShiftedSquadOperationNeverPublishesUnavailableFirstMove()
    {
        string root = FindRepoRoot();
        TacticalPlaybookCompilation west = CompileStrategy(
            root, "tapline-distributed-wells-v4");
        TacticalPlaybookCompilation east = CompileStrategy(
            root, "tapline-mid-rush-control-v3");
        ActorResolvedMatchDefinition definition = ArcRelayH0Definition.Create(
            west.Composition,
            east.Composition,
            loopProfile: ArcRelayLoopProfile.Tapline3);

        using var session = new GenericActorMatchSession(
            definition,
            [Participant(0, west), Participant(1, east)],
            matchSeed: 9012,
            recordChronology: false);

        var falseMoveTraces = new List<string>();
        while (!session.IsCompleted)
        {
            GenericActorMatchStepResult step = session.Step();
            foreach (GenericMindRuntimeTurn turn in step.MindTurns)
            foreach (GenericMindCommandResolution resolution in turn.Commands)
            {
                string reason = resolution.Command.DebugMessage ?? "";
                if (string.Equals(
                        resolution.Command.ActionId,
                        "wait",
                        StringComparison.Ordinal)
                    && reason.Contains(
                        ":epibt-move:", StringComparison.Ordinal)
                    && reason.Contains(
                        ":move-blocked:", StringComparison.Ordinal))
                {
                    falseMoveTraces.Add(
                        $"t{step.Tick}:team{turn.TeamId}:"
                        + $"u{resolution.Command.UnitId}:{reason}");
                }
            }
        }

        Assert.Empty(falseMoveTraces);
    }

    private static Position? RecoveryGoal(string reason)
    {
        const string marker = "recover-health";
        int recovery = reason.IndexOf(marker, StringComparison.Ordinal);
        int eligibility = reason.IndexOf(":eligibility[", StringComparison.Ordinal);
        if (recovery < 0)
            return null;
        // Rejected higher Instructions are now preserved for Practice after
        // the active order path. They explain fallthrough; they are not the
        // action this body is executing.
        if (eligibility >= 0 && recovery > eligibility)
            return null;
        int at = reason.IndexOf("march@", recovery, StringComparison.Ordinal);
        if (at < 0)
            return null;
        int comma = reason.IndexOf(',', at + 6);
        int colon = reason.IndexOf(':', comma + 1);
        return comma > at && colon > comma
            && int.TryParse(reason[(at + 6)..comma], out int x)
            && int.TryParse(reason[(comma + 1)..colon], out int y)
                ? new Position(x, y)
                : null;
    }

    private static int ClosedMovementCycles(
        IEnumerable<GenericActorAuthoritativeEvent> facts)
    {
        var events = facts.Select(fact => fact.Payload)
            .OfType<GenericActorRuntimeObservation.EventPayload.MovementBlocked>()
            .GroupBy(value => value.ActorId.TeamId);
        int result = 0;
        foreach (var team in events)
        {
            Dictionary<Position, int> occupant = team.ToDictionary(
                value => value.From,
                value => value.ActorId.UnitId);
            Dictionary<int, int> edges = team
                .Where(value => occupant.ContainsKey(value.AttemptedTo))
                .ToDictionary(value => value.ActorId.UnitId,
                    value => occupant[value.AttemptedTo]);
            foreach (int start in edges.Keys)
            {
                var seen = new HashSet<int>();
                int current = start;
                while (edges.TryGetValue(current, out int next))
                {
                    if (!seen.Add(current))
                    {
                        if (seen.Count >= 3)
                            result++;
                        break;
                    }
                    current = next;
                }
            }
        }
        return result;
    }

    private sealed record RecoveryProgress(
        Position Goal,
        int Health,
        int BestDistance,
        int UncontestedTicks);

    private sealed record CarrierCourse(
        ActorIdentity Carrier,
        Position Current,
        Position? Previous,
        int AlternatingSteps);
    private sealed record CarrierStation(
        ActorIdentity Carrier,
        Position Position,
        int Ticks);

    private static Position PositionOf(
        GenericActorWorldSnapshot state,
        int unitId) => state.ActiveLives.Single(life =>
            life.ActorId.TeamId == 0 && life.ActorId.UnitId == unitId).Position;

    private static int SquaredDistance(Position left, Position right)
    {
        int dx = left.X - right.X;
        int dy = left.Y - right.Y;
        return dx * dx + dy * dy;
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
        // Untagged ATL2 is the product form. If this scene ever silently
        // falls back to the historical greedy diagnostic, its EPIBT reason
        // assertions above fail rather than blessing the wrong planner.
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

    private static TacticalPlaybookCompilation CompileProgramCustodyFixture(
        string root,
        string fixtureId)
    {
        string directory = Path.Combine(
            root,
            "tests",
            "BotArena.Cli.Tests",
            "Fixtures",
            "ProgramCustody");
        string playbook = Path.Combine(directory, $"{fixtureId}.json");
        string mapPlan = Path.Combine(directory, $"{fixtureId}-map.json");
        return ArcRelayTacticalPlaybookCompiler.Compile(
            File.ReadAllBytes(playbook),
            File.ReadAllBytes(mapPlan),
            playbook,
            mapPlan);
    }

    private static TacticalPlaybookCompilation CompileHoldDefenceFixture(
        string root,
        bool defender,
        Position hostilePost,
        string defenderClass = "longshot",
        string visitorClass = "palisade",
        int chaseLimit = 0,
        bool visitorFights = false,
        int enemyDistanceLimit = 3,
        Position? holderPost = null)
    {
        string package = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        string sourcePath = Path.Combine(
            package, "playbooks", "idle-field-v1.json");
        string mapPath = Path.Combine(
            package, "map-plans", "tapline-9", "idle-field-v1.json");
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath))!
            .AsObject();
        JsonObject map = JsonNode.Parse(File.ReadAllBytes(mapPath))!.AsObject();
        string side = defender ? "defender" : "visitor";
        source["sheetId"] = $"hold-{side}-proof";
        source["mapPlan"] = "map-plan.json";
        map["mapPlanId"] = $"hold-{side}-proof";
        Position defencePost = holderPost ?? new Position(15, 12);
        Position authoredHostile = defender
            ? hostilePost
            : new Position(30 - hostilePost.X, 26 - hostilePost.Y);
        map["zones"] = defender
            ? new JsonObject
            {
                ["duel"] = new JsonObject
                {
                    ["rect"] = new JsonArray(
                        Math.Min(defencePost.X, hostilePost.X),
                        Math.Min(defencePost.Y, hostilePost.Y),
                        Math.Max(defencePost.X, hostilePost.X),
                        Math.Max(defencePost.Y, hostilePost.Y)),
                },
                ["holder-post"] = new JsonObject
                {
                    ["rect"] = new JsonArray(
                        defencePost.X, defencePost.Y,
                        defencePost.X, defencePost.Y),
                },
            }
            : visitorFights
                ? new JsonObject
            {
                ["visitor-post"] = new JsonObject
                {
                    ["rect"] = new JsonArray(
                        authoredHostile.X, authoredHostile.Y,
                    authoredHostile.X, authoredHostile.Y),
                },
            }
                : new JsonObject();
        map["anchors"] = defender
            ? new JsonObject
            {
                ["defence-post"] = new JsonArray(
                    defencePost.X, defencePost.Y),
                ["rest-face"] = new JsonArray(
                    Math.Min(29, defencePost.X + 3), defencePost.Y),
            }
            : new JsonObject
            {
                ["visit-post"] = new JsonArray(
                    authoredHostile.X, authoredHostile.Y),
            };
        source["conditions"] = defender
            ? new JsonObject
            {
                ["enemy-at-duel"] = new JsonObject
                {
                    ["fact"] = "visible-enemies-in-zone",
                    ["operator"] = "at-least",
                    ["value"] = 1,
                    ["zone"] = "duel",
                },
                ["enemy-close"] = new JsonObject
                {
                    ["fact"] = "self-nearest-visible-enemy-distance",
                    ["operator"] = "at-most",
                    ["value"] = enemyDistanceLimit,
                },
                ["enemy-gone"] = new JsonObject
                {
                    ["fact"] = "self-nearest-visible-enemy-distance",
                    ["operator"] = "greater-than",
                    ["value"] = enemyDistanceLimit,
                },
                ["holder-at-post"] = new JsonObject
                {
                    ["fact"] = "self-in-zone",
                    ["operator"] = "equals",
                    ["value"] = 1,
                    ["zone"] = "holder-post",
                },
                ["impossible"] = new JsonObject
                {
                    ["fact"] = "self-health-percent",
                    ["operator"] = "less-than",
                    ["value"] = 0,
                },
            }
            : visitorFights
                ? new JsonObject
                {
                    ["enemy-close"] = new JsonObject
                    {
                        ["fact"] = "self-nearest-visible-enemy-distance",
                        ["operator"] = "at-most",
                        ["value"] = enemyDistanceLimit,
                    },
                    ["visitor-at-post"] = new JsonObject
                    {
                        ["fact"] = "self-in-zone",
                        ["operator"] = "equals",
                        ["value"] = 1,
                        ["zone"] = "visitor-post",
                    },
                    ["impossible"] = new JsonObject
                    {
                        ["fact"] = "self-health-percent",
                        ["operator"] = "less-than",
                        ["value"] = 0,
                    },
                }
                : new JsonObject
            {
                ["impossible"] = new JsonObject
                {
                    ["fact"] = "self-health-percent",
                    ["operator"] = "less-than",
                    ["value"] = 0,
                },
            };
        var otherClasses = new JsonArray(
            "palisade", "towline", "patchbay", "lantern", "mortar",
            "minesmith", "hush");
        source["roles"] = defender
            ? new JsonArray(
                new JsonObject
                {
                    ["id"] = "holder",
                    ["classes"] = new JsonArray(defenderClass),
                    ["upgradePath"] = new JsonArray("damage", "reach"),
                    ["orders"] = new JsonArray(
                        new JsonObject
                        {
                            ["id"] = "hold-visible-hostile",
                            ["when"] = new JsonObject
                            {
                                ["all"] = new JsonArray(
                                    "enemy-at-duel", "enemy-close",
                                    "holder-at-post"),
                            },
                            ["until"] = "enemy-gone",
                            ["fight"] = new JsonObject
                            {
                                ["targets"] = new JsonArray("nearest"),
                                ["style"] = "hold",
                                ["chaseLimit"] = chaseLimit,
                                ["zone"] = "duel",
                            },
                        },
                        new JsonObject
                        {
                            ["id"] = "hold-post",
                            ["move"] = new JsonObject
                            {
                                ["to"] = new JsonObject
                                {
                                    ["anchor"] = "defence-post",
                                },
                                ["face"] = new JsonObject
                                {
                                    ["anchor"] = "rest-face",
                                },
                            },
                        }),
                },
                new JsonObject
                {
                    ["id"] = "observers",
                    ["classes"] = otherClasses,
                    ["upgradePath"] = new JsonArray("vitality"),
                    ["orders"] = new JsonArray(
                        new JsonObject
                        {
                            ["id"] = "remain-idle",
                            ["when"] = "impossible",
                            ["move"] = new JsonObject
                            {
                                ["to"] = "own-reactor",
                            },
                        }),
                })
            : new JsonArray(
                new JsonObject
                {
                    ["id"] = "visitor",
                    ["classes"] = new JsonArray(visitorClass),
                    ["upgradePath"] = new JsonArray(
                        "vitality", "vitality", "vitality"),
                    ["orders"] = visitorFights
                        ? new JsonArray(
                            new JsonObject
                            {
                                ["id"] = "attack-holder",
                                ["when"] = new JsonObject
                                {
                                    ["all"] = new JsonArray(
                                        "enemy-close", "visitor-at-post"),
                                },
                                ["fight"] = new JsonObject
                                {
                                    ["targets"] = new JsonArray("nearest"),
                                    ["style"] = "hold",
                                    ["chaseLimit"] = 0,
                                },
                            },
                            new JsonObject
                            {
                                ["id"] = "visit-holder",
                                ["move"] = new JsonObject
                                {
                                    ["to"] = new JsonObject
                                    {
                                        ["anchor"] = "visit-post",
                                    },
                                },
                            })
                        : new JsonArray(
                            new JsonObject
                        {
                            ["id"] = "visit-holder",
                            ["move"] = new JsonObject
                            {
                                ["to"] = new JsonObject
                                {
                                    ["anchor"] = "visit-post",
                                },
                            },
                        }),
                },
                new JsonObject
                {
                    ["id"] = "observers",
                    ["classes"] = new JsonArray(
                        "longshot", "towline", "patchbay", "lantern",
                        "mortar", "minesmith", "hush"),
                    ["upgradePath"] = new JsonArray("vitality"),
                    ["orders"] = new JsonArray(
                        new JsonObject
                        {
                            ["id"] = "remain-idle",
                            ["when"] = "impossible",
                            ["move"] = new JsonObject
                            {
                                ["to"] = "own-reactor",
                            },
                        }),
                });
        source["sharedOrders"] = new JsonObject();
        source["capacities"] = new JsonObject();
        return ArcRelayTacticalPlaybookCompiler.Compile(
            Encoding.UTF8.GetBytes(source.ToJsonString()),
            Encoding.UTF8.GetBytes(map.ToJsonString()),
            sourcePath + $"#{side}",
            mapPath + $"#{side}");
    }

    private static TacticalPlaybookCompilation CompileCoreRunnerFixture(
        string root)
    {
        string package = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        string sourcePath = Path.Combine(
            package, "playbooks", "idle-field-v1.json");
        string mapPath = Path.Combine(
            package, "map-plans", "tapline-9", "idle-field-v1.json");
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath))!
            .AsObject();
        JsonObject map = JsonNode.Parse(File.ReadAllBytes(mapPath))!.AsObject();
        source["sheetId"] = "hold-carrier-runner-proof";
        source["mapPlan"] = "map-plan.json";
        map["mapPlanId"] = "hold-carrier-runner-proof";
        source["conditions"] = new JsonObject
        {
            ["carrying"] = new JsonObject
            {
                ["fact"] = "self-carrying",
                ["operator"] = "equals",
                ["value"] = 1,
            },
            ["impossible"] = new JsonObject
            {
                ["fact"] = "self-health-percent",
                ["operator"] = "less-than",
                ["value"] = 0,
            },
        };
        source["roles"] = new JsonArray(
            new JsonObject
            {
                ["id"] = "runner",
                ["classes"] = new JsonArray("palisade"),
                // This fixture proves bounded carrier interception, not a
                // three-tier durability race. Tapline-10's shorter gun reach
                // leaves fewer firing beats inside the same five-tile leash.
                ["upgradePath"] = new JsonArray("vitality"),
                ["orders"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "return-core",
                        ["when"] = "carrying",
                        ["urgency"] = "urgent",
                        ["move"] = new JsonObject { ["to"] = "own-reactor" },
                    },
                    new JsonObject
                    {
                        ["id"] = "take-centre-core",
                        ["move"] = new JsonObject
                        {
                            ["to"] = new JsonObject { ["well"] = "centre" },
                        },
                    }),
            },
            new JsonObject
            {
                ["id"] = "observers",
                ["classes"] = new JsonArray(
                    "longshot", "towline", "patchbay", "lantern",
                    "mortar", "minesmith", "hush"),
                ["upgradePath"] = new JsonArray("vitality"),
                ["orders"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "remain-idle",
                        ["when"] = "impossible",
                        ["move"] = new JsonObject { ["to"] = "own-reactor" },
                    }),
            });
        source["sharedOrders"] = new JsonObject();
        source["capacities"] = new JsonObject();
        return ArcRelayTacticalPlaybookCompiler.Compile(
            Encoding.UTF8.GetBytes(source.ToJsonString()),
            Encoding.UTF8.GetBytes(map.ToJsonString()),
            sourcePath + "#carrier-runner",
            mapPath + "#carrier-runner");
    }

    private static TacticalPlaybookCompilation CompileCarrierDefenceRunnerFixture(
        string root)
    {
        string package = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        string sourcePath = Path.Combine(
            package, "playbooks", "idle-field-v1.json");
        string mapPath = Path.Combine(
            package, "map-plans", "tapline-9", "idle-field-v1.json");
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath))!
            .AsObject();
        JsonObject map = JsonNode.Parse(File.ReadAllBytes(mapPath))!.AsObject();
        source["sheetId"] = "carrier-defence-cadence-proof";
        source["mapPlan"] = "map-plan.json";
        map["mapPlanId"] = "carrier-defence-cadence-proof";
        source["conditions"] = new JsonObject
        {
            ["carrying"] = new JsonObject
            {
                ["fact"] = "self-carrying",
                ["operator"] = "equals",
                ["value"] = 1,
            },
            ["enemy-close"] = new JsonObject
            {
                ["fact"] = "self-nearest-visible-enemy-distance",
                ["operator"] = "at-most",
                ["value"] = 7,
            },
            ["enemy-gone"] = new JsonObject
            {
                ["fact"] = "self-nearest-visible-enemy-distance",
                ["operator"] = "greater-than",
                ["value"] = 7,
            },
            ["impossible"] = new JsonObject
            {
                ["fact"] = "self-health-percent",
                ["operator"] = "less-than",
                ["value"] = 0,
            },
        };
        source["roles"] = new JsonArray(
            new JsonObject
            {
                ["id"] = "runner",
                ["classes"] = new JsonArray("palisade"),
                ["upgradePath"] = new JsonArray("vitality"),
                ["orders"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "carrier-defence",
                        ["when"] = new JsonObject
                        {
                            ["all"] = new JsonArray("carrying", "enemy-close"),
                        },
                        ["until"] = "enemy-gone",
                        ["fight"] = new JsonObject
                        {
                            ["style"] = "hold",
                            ["targets"] = new JsonArray("nearest", "carrier"),
                            ["chaseLimit"] = 0,
                        },
                    },
                    new JsonObject
                    {
                        ["id"] = "return-core",
                        ["when"] = "carrying",
                        ["urgency"] = "urgent",
                        ["move"] = new JsonObject { ["to"] = "own-reactor" },
                    },
                    new JsonObject
                    {
                        ["id"] = "take-centre-core",
                        ["move"] = new JsonObject
                        {
                            ["to"] = new JsonObject { ["well"] = "centre" },
                        },
                    }),
            },
            new JsonObject
            {
                ["id"] = "observers",
                ["classes"] = new JsonArray(
                    "longshot", "towline", "patchbay", "lantern",
                    "mortar", "minesmith", "hush"),
                ["upgradePath"] = new JsonArray("vitality"),
                ["orders"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "remain-idle",
                        ["when"] = "impossible",
                        ["move"] = new JsonObject { ["to"] = "own-reactor" },
                    }),
            });
        source["sharedOrders"] = new JsonObject();
        source["capacities"] = new JsonObject();
        return ArcRelayTacticalPlaybookCompiler.Compile(
            Encoding.UTF8.GetBytes(source.ToJsonString()),
            Encoding.UTF8.GetBytes(map.ToJsonString()),
            sourcePath + "#carrier-defence",
            mapPath + "#carrier-defence");
    }

    private static TacticalPlaybookCompilation CompileCarrierThreatFixture(
        string root)
    {
        string package = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        string sourcePath = Path.Combine(
            package, "playbooks", "idle-field-v1.json");
        string mapPath = Path.Combine(
            package, "map-plans", "tapline-9", "idle-field-v1.json");
        JsonObject source = JsonNode.Parse(File.ReadAllBytes(sourcePath))!
            .AsObject();
        JsonObject map = JsonNode.Parse(File.ReadAllBytes(mapPath))!.AsObject();
        source["sheetId"] = "carrier-defence-threat-proof";
        source["mapPlan"] = "map-plan.json";
        map["mapPlanId"] = "carrier-defence-threat-proof";
        map["anchors"] = new JsonObject
        {
            ["left-threat"] = new JsonArray(13, 13),
            ["right-threat"] = new JsonArray(17, 13),
            ["north-threat"] = new JsonArray(15, 11),
            ["south-threat"] = new JsonArray(15, 15),
        };
        source["conditions"] = new JsonObject
        {
            ["impossible"] = new JsonObject
            {
                ["fact"] = "self-health-percent",
                ["operator"] = "less-than",
                ["value"] = 0,
            },
        };
        JsonObject HoldAt(string id, string anchor, string classId) => new()
        {
            ["id"] = id,
            ["classes"] = new JsonArray(classId),
            ["upgradePath"] = new JsonArray(
                "vitality", "vitality", "vitality"),
            ["orders"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = $"hold-{id}",
                    ["move"] = new JsonObject
                    {
                        ["to"] = new JsonObject { ["anchor"] = anchor },
                    },
                }),
        };
        source["roles"] = new JsonArray(
            HoldAt("left", "left-threat", "palisade"),
            HoldAt("right", "right-threat", "towline"),
            HoldAt("north", "north-threat", "patchbay"),
            HoldAt("south", "south-threat", "longshot"),
            new JsonObject
            {
                ["id"] = "observers",
                ["classes"] = new JsonArray(
                    "lantern", "mortar", "minesmith", "hush"),
                ["upgradePath"] = new JsonArray("vitality"),
                ["orders"] = new JsonArray(
                    new JsonObject
                    {
                        ["id"] = "remain-idle",
                        ["when"] = "impossible",
                        ["move"] = new JsonObject { ["to"] = "own-reactor" },
                    }),
            });
        source["sharedOrders"] = new JsonObject();
        source["capacities"] = new JsonObject();
        return ArcRelayTacticalPlaybookCompiler.Compile(
            Encoding.UTF8.GetBytes(source.ToJsonString()),
            Encoding.UTF8.GetBytes(map.ToJsonString()),
            sourcePath + "#carrier-threat",
            mapPath + "#carrier-threat");
    }

    private static Position[] RoutePoints(
        JsonElement routes,
        string routeId) => routes.GetProperty(routeId)
        .GetProperty("waypoints")
        .EnumerateArray()
        .Select(point => point.EnumerateArray()
            .Select(value => value.GetInt32()).ToArray())
        .Select(point => new Position(point[0], point[1]))
        .ToArray();

    private static Position[] ShortestCornerStrictTerrainPath(
        ActorMapDefinition map,
        Position start,
        Position destination,
        IReadOnlySet<Position> forbidden)
    {
        var frontier = new Queue<Position>();
        var previous = new Dictionary<Position, Position?>
        {
            [start] = null,
        };
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            Position current = frontier.Dequeue();
            if (current == destination)
                break;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var next = new Position(current.X + dx, current.Y + dy);
                    if (!Open(next) || previous.ContainsKey(next))
                        continue;
                    if (dx != 0 && dy != 0
                        && (!Open(new Position(current.X + dx, current.Y))
                            || !Open(new Position(current.X, current.Y + dy))))
                    {
                        continue;
                    }

                    previous[next] = current;
                    frontier.Enqueue(next);
                }
            }
        }

        if (!previous.ContainsKey(destination))
            return [];

        var reverse = new List<Position>();
        for (Position? cursor = destination;
             cursor is Position tile;
             cursor = previous[tile])
        {
            reverse.Add(tile);
        }
        reverse.Reverse();
        return [.. reverse];

        bool Open(Position tile) => tile.X >= 0
            && tile.X < map.Width
            && tile.Y >= 0
            && tile.Y < map.Height
            && !map.IsWall(tile)
            && !forbidden.Contains(tile);
    }

    private static TacticalPlaybookCompilation CompileHistoricalTapline4(
        string root,
        string fileName)
    {
        string package = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        string playbook = Path.Combine(package, "playbooks", fileName);
        string mapPlan = Path.Combine(package, "map-plans", fileName);
        return ArcRelayTacticalPlaybookCompiler.Compile(
            File.ReadAllBytes(playbook),
            File.ReadAllBytes(mapPlan),
            playbook,
            mapPlan);
    }

    private static TacticalPlaybookCompilation CompileHistoricalTapline6(
        string root,
        string fileName)
    {
        string package = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        string playbook = Path.Combine(package, "playbooks", fileName);
        string mapPlan = Path.Combine(
            package, "map-plans", "tapline-6", fileName);
        return ArcRelayTacticalPlaybookCompiler.Compile(
            File.ReadAllBytes(playbook),
            File.ReadAllBytes(mapPlan),
            playbook,
            mapPlan);
    }

    private static TacticalPlaybookCompilation CompileLockboxWithPocket(
        string root,
        int[] rect)
    {
        string package = Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03");
        string playbook = Path.Combine(package, "playbooks", "lockbox-v1.json");
        string mapPlan = Path.Combine(
            package, "map-plans", "tapline-6", "lockbox-v1.json");
        JsonObject map = JsonNode.Parse(File.ReadAllText(mapPlan))!.AsObject();
        map["zones"]!["centre-screen-pocket"]!["rect"] = new JsonArray(
            rect.Select(value => JsonValue.Create(value)).ToArray());
        return ArcRelayTacticalPlaybookCompiler.Compile(
            File.ReadAllBytes(playbook),
            Encoding.UTF8.GetBytes(map.ToJsonString()),
            playbook,
            mapPlan + "#test-pocket");
    }

    private static TacticalPlaybookCompilation CompileStrategy(
        string root,
        string strategyId) => ArcRelayTacticalPlaybookCompiler.Compile(
        Path.Combine(
            root,
            "arena-bots",
            "arc-relay",
            "tactical-playbook-v1-2026-08-03",
            "strategy-depth",
            strategyId,
            "orders.json"));

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

}
