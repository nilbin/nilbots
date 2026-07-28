using System.Globalization;

namespace BotArena.Engine.Tests;

public sealed class ReplayV3ProjectionTests
{
    [Fact]
    public void Project_RealProjectileMatchPreservesEnvelopeAndExactFacts()
    {
        const ulong unsafeJavaScriptSeed = 9_007_199_254_740_993UL;
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) => observation.Tick == 0
                    ? GenericDeathmatchSessionTestFixture.Shoot()
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories,
                reverse: true),
            unsafeJavaScriptSeed);

        ReplayV3 partial =
            ReplayV3Projection.Project(session.Chronology);

        Assert.True(partial.Partial);
        Assert.Null(partial.Result);
        Assert.Null(partial.ReplayHash);
        Assert.Empty(partial.Ticks);
        Assert.Equal(0, partial.InitialFrame.State.NextTick);
        Assert.NotEmpty(partial.InitialFrame.LifeStarts);

        session.Run();
        GenericActorMatchChronology chronology = session.Chronology;
        ReplayV3 replay = ReplayV3Projection.Project(chronology);

        Assert.False(replay.Partial);
        Assert.Null(replay.ReplayHash);
        Assert.NotNull(replay.Result);
        Assert.Equal(3, replay.Header.ReplayVersion);
        Assert.Equal(
            unsafeJavaScriptSeed.ToString(CultureInfo.InvariantCulture),
            replay.Header.Seed);
        Assert.Equal(
            chronology.Descriptor.MatchContractFingerprint,
            replay.Header.Contract.MatchContractFingerprint);
        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(definition),
            replay.Header.Contract.CanonicalJson);
        Assert.Equal(
            definition.CapabilityVersions.ContractProfileId,
            replay.Header.Runtime.ContractProfileId);
        Assert.Equal(
            definition.CapabilityVersions.RuntimeContractVersion,
            replay.Header.Runtime.RuntimeContractVersion);
        Assert.Equal(
            chronology.Descriptor.Participants
                .Select(value => value.ParticipantId),
            replay.Header.Provenance!.Participants
                .Select(value => value.ParticipantId));
        Assert.All(
            replay.InitialFrame.LifeStarts,
            start => Assert.Equal(
                chronology.InitialFrame.LifeStarts
                    .Single(value =>
                        value.ActorId.TeamId == start.ActorId.TeamId
                        && value.ActorId.UnitId == start.ActorId.UnitId
                        && value.ActorId.LifeId == start.ActorId.LifeId)
                    .ActorRandomSeed
                    .ToString(CultureInfo.InvariantCulture),
                start.ActorRandomSeed));

        GenericActorMatchActorTurn sourceTurn =
            chronology.Ticks[0].ActorTurns[0];
        ReplayV3.ActorTurn projectedTurn =
            replay.Ticks[0].ActorTurns[0];
        Assert.Equal(sourceTurn.Tick, projectedTurn.Tick);
        Assert.Equal(
            sourceTurn.Observation.MatchContractFingerprint,
            projectedTurn.Observation.MatchContractFingerprint);
        Assert.NotNull(projectedTurn.SubmittedDecision);
        Assert.IsType<ReplayV3.RawActionArgument.ShotProgram>(
            Assert.Single(
                projectedTurn.SubmittedDecision.Arguments!.Value));
        Assert.IsType<ReplayV3.ActionArgument.ShotProgram>(
            Assert.Single(
                projectedTurn.ActionResolution
                    .SubmittedAction!.Arguments));
        Assert.IsType<ReplayV3.ActionArgument.ShotProgram>(
            Assert.Single(
                projectedTurn.ActionResolution
                    .AcceptedAction.Arguments));
        Assert.IsType<ReplayV3.ActionArgument.ShotProgram>(
            Assert.Single(
                projectedTurn.ActionResolution
                    .ValidatedAction.Arguments));
        Assert.Null(projectedTurn.Observation.HeardSounds);
        Assert.True(
            projectedTurn.Observation.VisibleProjectiles.HasValue);
        Assert.Empty(
            projectedTurn.Observation.VisibleProjectiles.Value);

        Assert.Equal(2, replay.Ticks[0].PostState.Projectiles.Length);
        Assert.All(
            replay.Ticks[0].Traversals,
            traversal =>
            {
                Assert.Equal("resolution", traversal.Phase);
                Assert.Equal("attack-launch", traversal.Trigger);
                Assert.IsType<ReplayV3.TraversalTerminal.Retained>(
                    traversal.Terminal);
                Assert.True(
                    long.TryParse(
                        traversal.ProjectileId,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out _));
            });
        Assert.All(
            replay.Ticks[1].Traversals,
            traversal =>
            {
                Assert.Equal("scheduled-advance", traversal.Trigger);
                Assert.IsType<
                    ReplayV3.TraversalTerminal.ActorContact>(
                    traversal.Terminal);
            });

        Assert.Equal(
            SourceFactOrdinals(chronology)
                .Select(value =>
                    value.ToString(CultureInfo.InvariantCulture)),
            ReplayFactOrdinals(replay));
        Assert.Equal(
            chronology.Ticks
                .SelectMany(frame => frame.Traversals)
                .Select(value => value.ProjectileId.ToString(
                    CultureInfo.InvariantCulture)),
            replay.Ticks
                .SelectMany(frame => frame.Traversals)
                .Select(value => value.ProjectileId));

        ReplayV3.ModeResult.Deathmatch mode =
            Assert.IsType<ReplayV3.ModeResult.Deathmatch>(
                replay.Result.Mode);
        Assert.Equal("max-ticks", mode.Reason);
        Assert.All(
            mode.Scores,
            score =>
            {
                Assert.True(IsInvariantInteger(score.Kills));
                Assert.True(IsInvariantInteger(score.Deaths));
                Assert.True(IsInvariantInteger(score.DamageDealt));
            });
        Assert.All(
            replay.Result.Standings.Teams
                .SelectMany(team => team.Scores),
            score => Assert.True(IsInvariantInteger(score.Value)));
    }

    [Fact]
    public void Project_RealSplitPreservesReservationAndLifeLineage()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 3,
                    MaxHealth = 4,
                    IncludeSplit = true,
                    SplitDurationTicks = 1,
                });
        var sourceActor = new ActorIdentity(0, 0, 0);
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (start, observation) =>
                    start.ActorId == sourceActor
                    && observation.Tick == 0
                        ? GenericDeathmatchSessionTestFixture.Split()
                        : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 801);

        session.Run();
        ReplayV3 replay =
            ReplayV3Projection.Project(session.Chronology);

        ReplayV3.TickFrame queued = replay.Ticks[0];
        ReplayV3.PendingReplication reservation =
            Assert.Single(queued.PostState.PendingReplications);
        Assert.Equal(new ReplayV3.ActorId(0, 0, 0), reservation.SourceActorId);
        Assert.Equal("split-mobile", reservation.TransitionId);
        Assert.Equal(2, reservation.Descendants.Length);
        Assert.All(
            reservation.Descendants,
            descendant => Assert.Equal(1, descendant.Generation));
        Assert.Equal(
            session.Chronology.Ticks[0].PostState.Slots.Count(slot =>
                slot.State is GenericActorRuntimeObservation.UnitSlotState
                    .ReplicationPending),
            queued.PostState.Slots.Count(slot =>
                slot.State is
                    ReplayV3.UnitSlotState.ReplicationPending));
        Assert.Equal(
            session.Chronology.Ticks[0].PostState.Slots.Count(slot =>
                slot.SplitReservation is not null),
            queued.PostState.Slots.Count(slot =>
                slot.SplitReservation is not null));

        ReplayV3.ActorTurn sourceTurn = queued.ActorTurns.Single(turn =>
            turn.ActorId == new ReplayV3.ActorId(0, 0, 0));
        Assert.Equal("split", sourceTurn.SubmittedDecision!.ActionId);
        Assert.Empty(sourceTurn.SubmittedDecision.Arguments!.Value);
        Assert.Equal(
            "split",
            sourceTurn.ActionResolution.AcceptedAction.ActionId);
        Assert.Equal(
            "split",
            sourceTurn.ActionResolution.ValidatedAction.ActionId);

        ReplayV3.TickFrame completed = replay.Ticks[1];
        Assert.Equal(2, completed.TickStart.LifeStarts.Length);
        Assert.All(
            completed.TickStart.LifeStarts,
            start =>
            {
                Assert.Equal("replication", start.Origin.Reason);
                Assert.Equal(
                    new ReplayV3.ActorId(0, 0, 0),
                    start.Origin.ParentActorId);
                Assert.Equal(1, start.Origin.Generation);
                Assert.Equal(
                    replay.Header.Contract.MatchContractFingerprint,
                    start.MatchContractFingerprint);
            });
        Assert.Contains(
            completed.TickStart.Events,
            value => value.Kind == "life-retired");
        Assert.Equal(
            2,
            completed.TickStart.Events.Count(value =>
                value.Kind == "life-spawned"));
        Assert.Equal(
            definition.Topology.UnitSlots.Length,
            replay.Result!.Units.Length);
    }

    [Fact]
    public void Project_RuntimeFaultPreservesNullReplyAndTypedFault()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 2,
                    FaultsAllowedBeforeDisqualification = 1,
                });
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, observation) => observation.Tick == 0
                    ? throw new InvalidOperationException(
                        "fixture runtime fault")
                    : GenericDeathmatchSessionTestFixture.Wait());
        using var session = new GenericDeathmatchSession(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 802);

        session.Run();
        ReplayV3 replay =
            ReplayV3Projection.Project(session.Chronology);

        ReplayV3.ActorTurn faulted = replay.Ticks[0].ActorTurns[0];
        Assert.Null(faulted.SubmittedDecision);
        Assert.Null(faulted.ActionResolution.SubmittedAction);
        Assert.Equal("wait", faulted.ActionResolution.AcceptedAction.ActionId);
        Assert.Equal(
            "wait",
            faulted.ActionResolution.ValidatedAction.ActionId);
        Assert.Equal("faulted", faulted.ActionResolution.Outcome);
        ReplayV3.RuntimeFault fault =
            Assert.IsType<ReplayV3.RuntimeFault>(
                faulted.ActionResolution.RuntimeFault);
        Assert.Equal("tick-execution", fault.Stage);
        Assert.Equal("1", fault.CumulativeFaultCount);

        ReplayV3.ObservedSelf nextSelf = replay.Ticks[1]
            .ActorTurns[0]
            .Observation
            .Self;
        Assert.Equal(
            "faulted",
            nextSelf.PreviousActionResolution!.Outcome);
        Assert.Equal(
            fault.CumulativeFaultCount,
            nextSelf.PreviousActionResolution.RuntimeFault!
                .CumulativeFaultCount);
    }

    private static IEnumerable<long> SourceFactOrdinals(
        GenericActorMatchChronology chronology)
    {
        foreach (GenericActorAuthoritativeEvent value in
                 chronology.InitialFrame.Events)
        {
            yield return value.GlobalOrdinal;
        }

        foreach (GenericActorMatchTickFrame frame in chronology.Ticks)
        {
            foreach (long value in frame.TickStart.Events
                         .Select(item => item.GlobalOrdinal)
                         .Concat(frame.TickStart.Traversals.Select(
                             item => item.GlobalOrdinal))
                         .Order())
            {
                yield return value;
            }
            foreach (long value in frame.Events
                         .Select(item => item.GlobalOrdinal)
                         .Concat(frame.Traversals.Select(
                             item => item.GlobalOrdinal))
                         .Order())
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<string> ReplayFactOrdinals(
        ReplayV3 replay)
    {
        foreach (ReplayV3.AuthoritativeEvent value in
                 replay.InitialFrame.Events)
        {
            yield return value.GlobalOrdinal;
        }

        foreach (ReplayV3.TickFrame frame in replay.Ticks)
        {
            foreach (string value in frame.TickStart.Events
                         .Select(item => item.GlobalOrdinal)
                         .Concat(frame.TickStart.Traversals.Select(
                             item => item.GlobalOrdinal))
                         .OrderBy(item => long.Parse(
                             item,
                             CultureInfo.InvariantCulture)))
            {
                yield return value;
            }
            foreach (string value in frame.Events
                         .Select(item => item.GlobalOrdinal)
                         .Concat(frame.Traversals.Select(
                             item => item.GlobalOrdinal))
                         .OrderBy(item => long.Parse(
                             item,
                             CultureInfo.InvariantCulture)))
            {
                yield return value;
            }
        }
    }

    private static bool IsInvariantInteger(string value) =>
        long.TryParse(
            value,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out _);
}
