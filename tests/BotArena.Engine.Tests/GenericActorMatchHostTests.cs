using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorMatchHostTests
{
    [Fact]
    public void ConstructorSnapshotsParticipantsOnceForCanonicalDescriptor()
    {
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition("head-to-head");
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(definition);
        ImmutableArray<GenericActorParticipantConfiguration> configurations =
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories,
                reverse: true);
        int enumerationCount = 0;

        IEnumerable<GenericActorParticipantConfiguration> EnumerateOnce()
        {
            enumerationCount++;
            if (enumerationCount > 1)
            {
                throw new InvalidOperationException(
                    "Participant input was enumerated more than once.");
            }
            foreach (GenericActorParticipantConfiguration configuration in
                     configurations)
            {
                yield return configuration;
            }
        }

        using var host = new GenericActorMatchHost(
            definition,
            EnumerateOnce(),
            matchSeed: 991);

        Assert.Equal(1, enumerationCount);
        Assert.Equal(991UL, host.Descriptor.MatchSeed);
        Assert.Equal(
            definition.Topology.Participants
                .Select(participant => participant.ParticipantId)
                .Order(),
            host.Descriptor.Participants
                .Select(participant => participant.ParticipantId));
    }

    [Fact]
    public void OwnsSeedIsolatedRuntimeLifecycleAndChronology()
    {
        const ulong matchSeed = 9_007_199_254_740_993UL;
        ActorResolvedMatchDefinition definition =
            GenericDeathmatchSessionTestFixture.Definition(
                "head-to-head",
                new GenericDeathmatchSessionTestFixture.Options
                {
                    MaxTicks = 1,
                });
        GenericDeathmatchTickStart sourceTick;
        GenericActorMatchChronology sourceChronology;
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory>
            sourceFactories =
                GenericDeathmatchSessionTestFixture.Factories(definition);
        using (var source = new GenericDeathmatchSession(
                   definition,
                   GenericDeathmatchSessionTestFixture.Configurations(
                       definition,
                       sourceFactories),
                   matchSeed))
        {
            sourceTick = source.PrepareTick();
            source.Step();
            sourceChronology = source.Chronology;
        }

        GenericActorMatchHost? host = null;
        Exception? descriptorAccessFailure = null;
        Dictionary<
            int,
            GenericDeathmatchSessionTestFixture.RecordingFactory> factories =
            GenericDeathmatchSessionTestFixture.Factories(
                definition,
                (_, _) =>
                {
                    descriptorAccessFailure ??= Record.Exception(
                        () => _ = host!.Descriptor);
                    return GenericDeathmatchSessionTestFixture.Wait();
                });
        host = new GenericActorMatchHost(
            definition,
            GenericDeathmatchSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed);
        try
        {
            ImmutableArray<GenericActorLifeStart> initialStarts =
                StartInitialLives(host, definition);
            Assert.All(
                factories.Values,
                factory => Assert.Equal(0, factory.CreateCount));

            host.RecordInitial(sourceChronology.InitialFrame);
            host.RecordResolvedTick(sourceChronology.Ticks.Single());
            host.RecordCompleted(sourceChronology.Result!);
            Assert.Same(host.Descriptor, host.Chronology.Descriptor);
            Assert.False(host.Chronology.Partial);

            using (host.EnterOperation("collect tick"))
            {
                GenericActorRuntimeTickResult result =
                    host.CollectTickDecisions(
                        tick: 0,
                        sourceTick.Observations);
                Assert.Equal(initialStarts.Length, result.Turns.Length);
            }

            Assert.IsType<InvalidOperationException>(
                descriptorAccessFailure);
            foreach (GenericActorLifeStart evidence in initialStarts)
            {
                ulong expectedSeed = SeedDerivation.DeriveActorSeed(
                    matchSeed,
                    evidence.ActorId,
                    definition.Rules.SeedMechanics.SeedProfileId);
                Assert.Equal(expectedSeed, evidence.ActorRandomSeed);
                GenericActorRuntimeStart received =
                    factories[evidence.ParticipantId].Starts.Single();
                Assert.Equal(evidence.ActorId, received.ActorId);
                Assert.Equal(evidence.ActorRandomSeed, received.ActorRandomSeed);
                Assert.Equal(evidence.Origin, received.Origin);
            }

            GenericActorLifeStart retired = initialStarts[0];
            using (host.EnterOperation("retire life"))
            {
                host.RetireLife(retired.ActorId);
            }
            Assert.Equal(
                1,
                factories[retired.ParticipantId].DisposedRuntimeCount);

            var replacementId = new ActorIdentity(
                retired.ActorId.TeamId,
                retired.ActorId.UnitId,
                lifeId: 1);
            GenericActorLifeStart replacement;
            using (host.EnterOperation("start replacement"))
            {
                replacement = host.StartLife(
                    replacementId,
                    retired.ParticipantId,
                    new GenericActorRuntimeStart.LifeOrigin(
                        GenericActorRuntimeStart.SpawnReason.AutomaticReturn,
                        retired.Origin.Generation,
                        retired.ActorId,
                        SourceTransitionId: null,
                        SourceOperationId: null));
            }

            Assert.NotEqual(
                retired.ActorRandomSeed,
                replacement.ActorRandomSeed);
            Assert.Equal(
                SeedDerivation.DeriveActorSeed(
                    matchSeed,
                    replacementId,
                    definition.Rules.SeedMechanics.SeedProfileId),
                replacement.ActorRandomSeed);
            Assert.Equal(
                1,
                factories[retired.ParticipantId].CreateCount);
        }
        finally
        {
            host.Dispose();
        }

        Assert.Equal(2, factories.Values.Sum(value =>
            value.DisposedRuntimeCount));
    }

    private static ImmutableArray<GenericActorLifeStart> StartInitialLives(
        GenericActorMatchHost host,
        ActorResolvedMatchDefinition definition)
    {
        Dictionary<(int TeamId, int UnitId), PublicUnitSlot> slots =
            definition.Topology.UnitSlots.ToDictionary(
                slot => (slot.TeamId, slot.UnitId));
        Dictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments =
            definition.LifecycleAssignments.ToDictionary(
                assignment => (assignment.TeamId, assignment.UnitId));
        return definition.Topology.InitialLives
            .OrderBy(life => life.TeamId)
            .ThenBy(life => life.UnitId)
            .Select(life =>
            {
                PublicUnitSlot slot = slots[(life.TeamId, life.UnitId)];
                int generation = assignments[
                    (life.TeamId, life.UnitId)].InitialGeneration!.Value;
                return host.StartLife(
                    new ActorIdentity(
                        life.TeamId,
                        life.UnitId,
                        life.LifeId),
                    slot.ControllerParticipantId,
                    new GenericActorRuntimeStart.LifeOrigin(
                        GenericActorRuntimeStart.SpawnReason.Initial,
                        generation,
                        ParentActorId: null,
                        SourceTransitionId: null,
                        SourceOperationId: null));
            })
            .ToImmutableArray();
    }
}
