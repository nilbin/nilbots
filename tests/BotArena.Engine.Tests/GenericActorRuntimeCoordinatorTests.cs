using System.Collections.Immutable;

namespace BotArena.Engine.Tests;

public sealed class GenericActorRuntimeCoordinatorTests
{
    [Fact]
    public void ConstructorAndLifeStart_RequireExactTopologyOwnership()
    {
        ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        var first = new RecordingFactory();
        var second = new RecordingFactory();

        Assert.Throws<ArgumentException>(() =>
            new GenericActorRuntimeCoordinator(
                contract,
                [
                    Configuration(10, 0, first),
                ]));
        Assert.Throws<ArgumentException>(() =>
            new GenericActorRuntimeCoordinator(
                contract,
                [
                    Configuration(10, 1, first),
                    Configuration(20, 1, second),
                ]));

        using var coordinator = new GenericActorRuntimeCoordinator(
            contract,
            [
                Configuration(10, 0, first),
                Configuration(20, 1, second),
            ]);
        Assert.Throws<ArgumentException>(() =>
            coordinator.StartLife(
                Start(
                    contract,
                    participantId: 10,
                    new ActorIdentity(1, 0, 0))));

        coordinator.StartLife(
            Start(
                contract,
                participantId: 10,
                new ActorIdentity(0, 0, 0)));
        Assert.Throws<ArgumentException>(() =>
            coordinator.StartLife(
                Start(
                    contract,
                    participantId: 10,
                    new ActorIdentity(0, 0, 1))));
    }

    [Fact]
    public void ExactBatch_IsPrevalidatedBeforeAnyRuntimeInvocation()
    {
        ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        var first = new RecordingFactory();
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor0 = new(0, 0, 0);
        ActorIdentity actor1 = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, actor0));
        coordinator.StartLife(Start(contract, 20, actor1));

        Assert.Throws<ArgumentException>(() =>
            coordinator.CollectTickDecisions(
                0,
                [Observation(contract, actor0, tick: 0)]));

        Assert.Equal(0, first.CreateCount);
        Assert.Equal(0, second.CreateCount);
    }

    [Fact]
    public void RetiredActorIdentity_CannotBeIssuedAgain()
    {
        ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        var first = new RecordingFactory();
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 7);
        coordinator.StartLife(Start(contract, 10, actor));
        coordinator.RetireLife(actor);

        Assert.Throws<ArgumentException>(() =>
            coordinator.StartLife(Start(contract, 10, actor)));

        coordinator.StartLife(
            Start(contract, 10, new ActorIdentity(0, 0, 8)));
    }

    [Fact]
    public void InputOrder_DoesNotAffectCanonicalInvocationOrOutput()
    {
        ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("free-for-all");
        var invocationOrder = new List<int>();
        Dictionary<int, RecordingFactory> factories = contract.Topology
            .Participants.ToDictionary(
                participant => participant.ParticipantId,
                participant => new RecordingFactory(
                    execute: observation =>
                    {
                        invocationOrder.Add(participant.ParticipantId);
                        return Wait();
                    }));
        using var coordinator = Coordinator(
            contract,
            factories.Select(pair => (pair.Key, pair.Value)).ToArray());
        foreach (PublicInitialLife life in contract.Topology.InitialLives)
        {
            PublicUnitSlot slot = contract.Topology.UnitSlots.Single(value =>
                value.TeamId == life.TeamId
                && value.UnitId == life.UnitId);
            coordinator.StartLife(
                Start(
                    contract,
                    slot.ControllerParticipantId,
                    new ActorIdentity(
                        life.TeamId,
                        life.UnitId,
                        life.LifeId)));
        }

        GenericActorRuntimeObservation[] reversed = coordinator.ActiveActorIds
            .Select(actorId => Observation(contract, actorId, tick: 0))
            .Reverse()
            .ToArray();
        GenericActorRuntimeTickResult result =
            coordinator.CollectTickDecisions(0, reversed);

        Assert.Equal([10, 20, 30, 40], invocationOrder);
        Assert.Equal(
            [10, 20, 30, 40],
            result.Turns.Select(turn => turn.ParticipantId));
        Assert.Empty(result.Faults);
    }

    [Fact]
    public void MultiLifeFaults_ShareAndSaturateOneParticipantCounter()
    {
        ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.WithTransitions();
        var participant10 = new RecordingFactory(
            execute: _ => Unknown());
        var participant20 = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, participant10),
            (20, participant20));
        ActorIdentity first = new(0, 0, 4);
        ActorIdentity second = new(0, 1, 8);
        ActorIdentity opponent = new(1, 0, 3);
        coordinator.StartLife(Start(contract, 10, second));
        coordinator.StartLife(Start(contract, 20, opponent));
        coordinator.StartLife(Start(contract, 10, first));

        GenericActorRuntimeTickResult result =
            coordinator.CollectTickDecisions(
                0,
                [
                    Observation(contract, opponent, 0),
                    Observation(contract, second, 0),
                    Observation(contract, first, 0),
                ]);

        Assert.Equal(
            [first, second, opponent],
            result.Turns.Select(turn => turn.ActorId));
        Assert.Equal(
            [1L, 1L],
            result.Faults.Select(fault =>
                fault.CumulativeFaultCount));
        Assert.Equal(
            [true, false],
            result.Faults.Select(fault =>
                fault.DisqualificationTriggered));
        Assert.Equal([10], result.NewlyDisqualifiedParticipantIds.ToArray());
        Assert.Equal(1, participant20.ExecuteCount);
    }

    [Fact]
    public void TeamParticipants_HaveIndependentFaultCounters()
    {
        ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("teams");
        var participant10 = new RecordingFactory(
            execute: _ => Unknown());
        var participant11 = new RecordingFactory();
        var participant20 = new RecordingFactory();
        var participant21 = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, participant10),
            (11, participant11),
            (20, participant20),
            (21, participant21));
        StartInitialLives(coordinator, contract);

        GenericActorRuntimeTickResult result =
            coordinator.CollectTickDecisions(
                0,
                coordinator.ActiveActorIds
                    .Reverse()
                    .Select(actorId =>
                        Observation(contract, actorId, 0)));

        Assert.Equal([10], result.NewlyDisqualifiedParticipantIds.ToArray());
        Assert.Collection(
            coordinator.ParticipantStatuses,
            status =>
            {
                Assert.Equal(10, status.ParticipantId);
                Assert.Equal(1, status.RuntimeFaultCount);
            },
            status =>
            {
                Assert.Equal(11, status.ParticipantId);
                Assert.Equal(0, status.RuntimeFaultCount);
            },
            status => Assert.Equal(0, status.RuntimeFaultCount),
            status => Assert.Equal(0, status.RuntimeFaultCount));
    }

    [Theory]
    [MemberData(nameof(MalformedDecisions))]
    public void MalformedDecision_IsFaultedAndReplacedWithCatalogWait(
        Func<ActorResolvedMatchDefinition, GenericActorRuntimeDecision?>
            decisionFactory,
        string expectedFaultCode)
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.WithTransitions(),
            faultsAllowed: 5);
        var first = new RecordingFactory(
            execute: _ => decisionFactory(contract));
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, actor));
        coordinator.StartLife(Start(contract, 20, opponent));

        GenericActorRuntimeTickResult result =
            coordinator.CollectTickDecisions(
                0,
                [
                    Observation(contract, opponent, 0),
                    Observation(contract, actor, 0),
                ]);
        GenericActorRuntimeTurn turn = result.Turns.Single(value =>
            value.ActorId == actor);

        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
            turn.AdmissionOutcome);
        Assert.Equal("wait", turn.AcceptedDecision.ActionId);
        Assert.Equal(0, turn.AcceptedDecision.ActionCode);
        Assert.Empty(turn.AcceptedDecision.Arguments);
        Assert.Equal(expectedFaultCode, turn.RuntimeFault?.FaultCode);
    }

    public static TheoryData<
        Func<ActorResolvedMatchDefinition, GenericActorRuntimeDecision?>,
        string> MalformedDecisions =>
        new()
        {
            { _ => null, "malformed-decision" },
            { _ => Unknown(), "unknown-action" },
            {
                _ => new GenericActorRuntimeDecision("wait", 4, [], null),
                "action-selector-mismatch"
            },
            {
                _ => new GenericActorRuntimeDecision(
                    "fabricate",
                    100,
                    [],
                    null),
                "missing-argument"
            },
            {
                contract => new GenericActorRuntimeDecision(
                    "fabricate",
                    100,
                    [
                        new GenericActorRuntimeActionArgument
                            .UnitTargetArgument(new(99, 99)),
                    ],
                    null),
                "argument-out-of-domain"
            },
            {
                _ => new GenericActorRuntimeDecision(
                    "fabricate",
                    100,
                    [
                        new GenericActorRuntimeActionArgument
                            .UnitTargetArgument(new(0, 1)),
                        new GenericActorRuntimeActionArgument
                            .UnitTargetArgument(new(0, 1)),
                    ],
                    null),
                "duplicate-argument"
            },
        };

    [Fact]
    public void FormMaskPrecedesDynamicDomainWhileMalformedPayloadStillFaults()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.WithTransitions(),
            faultsAllowed: 5);
        int call = 0;
        var first = new RecordingFactory(
            execute: _ => ++call switch
            {
                1 => Fabricate(new(0, 1)),
                2 => Fabricate(new(0, 1)),
                _ => Fabricate(new(99, 99)),
            });
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, actor));
        coordinator.StartLife(Start(contract, 20, opponent));

        GenericActorRuntimeTurn formRejected =
            coordinator.CollectTickDecisions(
                0,
                [
                    Observation(
                        contract,
                        actor,
                        0,
                        formId: "child",
                        emptyDomainActionId: "fabricate"),
                    Observation(contract, opponent, 0),
                ]).Turns.Single(turn => turn.ActorId == actor);
        GenericActorRuntimeTurn dynamicDomainFault =
            coordinator.CollectTickDecisions(
                1,
                [
                    Observation(
                        contract,
                        actor,
                        1,
                        formId: "mobile",
                        emptyDomainActionId: "fabricate"),
                    Observation(contract, opponent, 1),
                ]).Turns.Single(turn => turn.ActorId == actor);
        GenericActorRuntimeTurn malformedTarget =
            coordinator.CollectTickDecisions(
                2,
                [
                    Observation(
                        contract,
                        actor,
                        2,
                        formId: "child",
                        emptyDomainActionId: "fabricate"),
                    Observation(contract, opponent, 2),
                ]).Turns.Single(turn => turn.ActorId == actor);

        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Success,
            formRejected.AdmissionOutcome);
        Assert.Null(formRejected.RuntimeFault);
        Assert.Equal("fabricate", formRejected.AcceptedDecision.ActionId);

        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
            dynamicDomainFault.AdmissionOutcome);
        Assert.Equal(
            GenericActorRuntimeFaultCodes.ArgumentOutOfDomain,
            dynamicDomainFault.RuntimeFault?.FaultCode);
        Assert.True(
            coordinator.TryProjectSubmittedAction(
                dynamicDomainFault.SubmittedDecision,
                out GenericActorRuntimeActionResolution.ResolvedAction?
                    projected));
        Assert.Equal("fabricate", projected?.ActionId);

        Assert.Equal(
            GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
            malformedTarget.AdmissionOutcome);
        Assert.Equal(
            GenericActorRuntimeFaultCodes.ArgumentOutOfDomain,
            malformedTarget.RuntimeFault?.FaultCode);
        Assert.False(
            coordinator.TryProjectSubmittedAction(
                malformedTarget.SubmittedDecision,
                out _));
    }

    [Fact]
    public void SubmittedActionProjection_IsTotalAndCatalogStructural()
    {
        ActorResolvedMatchDefinition contract = WithProjectionCatalog();
        var first = new RecordingFactory();
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));

        GenericActorRuntimeDecision[] representable =
        [
            Fabricate(new(0, 1)),
            new(
                "move",
                1,
                [
                    new GenericActorRuntimeActionArgument.DirectionArgument(
                        Direction.North),
                ],
                null),
            new(
                "anchor",
                101,
                [
                    new GenericActorRuntimeActionArgument.FormTargetArgument(
                        "turret"),
                ],
                null),
            new("wait", 0, [], new string('x', 4097)),
            new("shoot", 4, [], null),
            Shoot(new ShotProgram(99, 0, 0, 1, 0)),
        ];
        foreach (GenericActorRuntimeDecision decision in representable)
        {
            Assert.True(
                coordinator.TryProjectSubmittedAction(
                    decision,
                    out GenericActorRuntimeActionResolution.ResolvedAction?
                        action));
            Assert.NotNull(action);
        }

        GenericActorRuntimeDecision?[] notRepresentable =
        [
            null,
            new("wait", 0, default, null),
            new(
                "wait",
                0,
                ImmutableArray.CreateRange<
                    GenericActorRuntimeActionArgument>(
                        new GenericActorRuntimeActionArgument[] { null! }),
                null),
            new("not-catalogued", 999, [], null),
            new("wait", 4, [], null),
            new("fabricate", 100, [], null),
            new(
                "fabricate",
                100,
                [
                    new GenericActorRuntimeActionArgument
                        .UnitTargetArgument(new(0, 1)),
                    new GenericActorRuntimeActionArgument
                        .UnitTargetArgument(new(0, 1)),
                ],
                null),
            new(
                "fabricate",
                100,
                [
                    new GenericActorRuntimeActionArgument.DirectionArgument(
                        Direction.North),
                ],
                null),
            new(
                "move",
                1,
                [
                    new GenericActorRuntimeActionArgument.DirectionArgument(
                        (Direction)999),
                ],
                null),
            Fabricate(new(99, 99)),
            new(
                "anchor",
                101,
                [
                    new GenericActorRuntimeActionArgument.FormTargetArgument(
                        "not-a-form"),
                ],
                null),
        ];
        foreach (GenericActorRuntimeDecision? decision in notRepresentable)
        {
            Assert.False(
                coordinator.TryProjectSubmittedAction(
                    decision,
                    out GenericActorRuntimeActionResolution.ResolvedAction?
                        action));
            Assert.Null(action);
        }
    }

    [Fact]
    public void ValidationFault_RetainsHealthyRuntimeInstance()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            faultsAllowed: 2);
        int call = 0;
        var first = new RecordingFactory(
            execute: _ => ++call == 1 ? Unknown() : Wait());
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, actor));
        coordinator.StartLife(Start(contract, 20, opponent));

        GenericActorRuntimeTickResult firstTick =
            coordinator.CollectTickDecisions(
                0,
                [
                    Observation(contract, actor, 0),
                    Observation(contract, opponent, 0),
                ]);
        GenericActorRuntimeTickResult secondTick =
            coordinator.CollectTickDecisions(
                1,
                [
                    Observation(contract, actor, 1),
                    Observation(contract, opponent, 1),
                ]);

        Assert.Single(firstTick.Faults);
        Assert.Empty(secondTick.Faults);
        Assert.Equal(1, first.CreateCount);
        Assert.Equal(2, first.ExecuteCount);
        Assert.Equal(0, first.RuntimeDisposeCount);
    }

    [Fact]
    public void ShotProgramAdmission_UsesResolvedBoundsAndAimOnlyShape()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            faultsAllowed: 3);
        var first = new RecordingFactory(
            execute: observation => observation.Tick switch
            {
                0 => Shoot(new ShotProgram(99, 0, 0, 1, 0)),
                1 => Shoot(new ShotProgram(0, 0, 1, 1, 0)),
                _ => Shoot(ShotProgram.Straight),
            });
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, actor));
        coordinator.StartLife(Start(contract, 20, opponent));

        GenericActorRuntimeTurn[] turns = Enumerable.Range(0, 3)
            .Select(tick => coordinator.CollectTickDecisions(
                tick,
                [
                    Observation(contract, actor, tick),
                    Observation(contract, opponent, tick),
                ]).Turns.Single(turn => turn.ActorId == actor))
            .ToArray();

        Assert.Equal(
            [
                GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
                GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
                GenericActorRuntimeActionResolution.ActionOutcome.Success,
            ],
            turns.Select(turn => turn.AdmissionOutcome));
        Assert.Equal(
            ["argument-out-of-domain", "argument-out-of-domain"],
            turns.Take(2).Select(turn => turn.RuntimeFault?.FaultCode));
        Assert.Equal(1, first.CreateCount);
        Assert.Equal(3, first.ExecuteCount);
    }

    [Fact]
    public void ReusedRuntimeInstance_IsFaultedWithoutDisposingOtherLife()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.WithTransitions(),
            faultsAllowed: 3);
        var shared = new RecordingRuntime();
        var first = new RecordingFactory();
        first.CreateSteps.Enqueue(() => shared);
        first.CreateSteps.Enqueue(() => shared);
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity owner = new(0, 0, 0);
        ActorIdentity duplicate = new(0, 1, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, owner));
        coordinator.StartLife(Start(contract, 10, duplicate));
        coordinator.StartLife(Start(contract, 20, opponent));

        GenericActorRuntimeTickResult result =
            coordinator.CollectTickDecisions(
                0,
                coordinator.ActiveActorIds.Select(actorId =>
                    Observation(contract, actorId, 0)));

        GenericActorRuntimeTurn duplicateTurn = result.Turns.Single(turn =>
            turn.ActorId == duplicate);
        Assert.Equal(
            GenericActorRuntimeFault.FaultStage.RuntimeCreate,
            duplicateTurn.RuntimeFault?.Stage);
        Assert.Equal(
            "runtime-instance-reused",
            duplicateTurn.RuntimeFault?.FaultCode);
        Assert.False(shared.Disposed);
        Assert.Equal(1, shared.ExecuteCount);
        Assert.Equal(owner, shared.ReceivedStart?.ActorId);
    }

    [Fact]
    public void DiscardedRuntimeReference_CannotBeReturnedByRetry()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            faultsAllowed: 3);
        var shared = new RecordingRuntime
        {
            Execute = _ => throw new InvalidOperationException("tick"),
        };
        var first = new RecordingFactory();
        first.CreateSteps.Enqueue(() => shared);
        first.CreateSteps.Enqueue(() => shared);
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, actor));
        coordinator.StartLife(Start(contract, 20, opponent));

        GenericActorRuntimeTurn firstTurn =
            coordinator.CollectTickDecisions(
                0,
                [
                    Observation(contract, actor, 0),
                    Observation(contract, opponent, 0),
                ]).Turns.Single(turn => turn.ActorId == actor);
        GenericActorRuntimeTurn retryTurn =
            coordinator.CollectTickDecisions(
                1,
                [
                    Observation(contract, actor, 1),
                    Observation(contract, opponent, 1),
                ]).Turns.Single(turn => turn.ActorId == actor);

        Assert.Equal(
            GenericActorRuntimeFault.FaultStage.TickExecution,
            firstTurn.RuntimeFault?.Stage);
        Assert.True(shared.Disposed);
        Assert.Equal(
            "runtime-instance-reused",
            retryTurn.RuntimeFault?.FaultCode);
    }

    [Fact]
    public void RuntimeReferences_AreUniqueAcrossParticipantFactories()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            faultsAllowed: 3);
        var shared = new RecordingRuntime();
        var first = new RecordingFactory();
        first.CreateSteps.Enqueue(() => shared);
        var second = new RecordingFactory();
        second.CreateSteps.Enqueue(() => shared);
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, actor));
        coordinator.StartLife(Start(contract, 20, opponent));

        GenericActorRuntimeTickResult result =
            coordinator.CollectTickDecisions(
                0,
                [
                    Observation(contract, opponent, 0),
                    Observation(contract, actor, 0),
                ]);

        GenericActorRuntimeTurn duplicate = result.Turns.Single(turn =>
            turn.ActorId == opponent);
        Assert.Equal(
            "runtime-instance-reused",
            duplicate.RuntimeFault?.FaultCode);
        Assert.False(shared.Disposed);
        Assert.Equal(actor, shared.ReceivedStart?.ActorId);
    }

    [Fact]
    public void RuntimeCallback_CannotMutateTheJointBatch()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            faultsAllowed: 2);
        GenericActorRuntimeCoordinator? coordinator = null;
        ActorIdentity actor = new(0, 0, 0);
        var first = new RecordingFactory(
            execute: _ =>
            {
                coordinator!.RetireLife(actor);
                return Wait();
            });
        var second = new RecordingFactory();
        coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        using (coordinator)
        {
            ActorIdentity opponent = new(1, 0, 0);
            coordinator.StartLife(Start(contract, 10, actor));
            coordinator.StartLife(Start(contract, 20, opponent));

            GenericActorRuntimeTickResult result =
                coordinator.CollectTickDecisions(
                    0,
                    [
                        Observation(contract, actor, 0),
                        Observation(contract, opponent, 0),
                    ]);

            GenericActorRuntimeTurn turn = result.Turns.Single(value =>
                value.ActorId == actor);
            Assert.Equal(
                GenericActorRuntimeFault.FaultStage.TickExecution,
                turn.RuntimeFault?.Stage);
            Assert.Contains(actor, coordinator.ActiveActorIds);
            Assert.Equal(1, second.ExecuteCount);
        }
    }

    [Fact]
    public void CreateStartAndExecuteFailures_RetryFreshWithOriginalStart()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.Deathmatch("head-to-head"),
            faultsAllowed: 5);
        var first = new RecordingFactory();
        first.CreateSteps.Enqueue(
            () => throw new InvalidOperationException("create"));
        var startFailure = new RecordingRuntime
        {
            Start = _ => throw new InvalidOperationException("start"),
        };
        first.CreateSteps.Enqueue(() => startFailure);
        var executeFailure = new RecordingRuntime
        {
            Execute = _ => throw new InvalidOperationException("tick"),
        };
        first.CreateSteps.Enqueue(() => executeFailure);
        var recovered = new RecordingRuntime();
        first.CreateSteps.Enqueue(() => recovered);
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 0);
        ActorIdentity opponent = new(1, 0, 0);
        GenericActorRuntimeStart original = Start(contract, 10, actor);
        coordinator.StartLife(original);
        coordinator.StartLife(Start(contract, 20, opponent));

        var stages = new List<GenericActorRuntimeFault.FaultStage>();
        for (int tick = 0; tick < 4; tick++)
        {
            GenericActorRuntimeTickResult result =
                coordinator.CollectTickDecisions(
                    tick,
                    [
                        Observation(contract, opponent, tick),
                        Observation(contract, actor, tick),
                    ]);
            stages.AddRange(result.Faults.Select(fault => fault.Stage));
        }

        Assert.Equal(
            [
                GenericActorRuntimeFault.FaultStage.RuntimeCreate,
                GenericActorRuntimeFault.FaultStage.LifeStart,
                GenericActorRuntimeFault.FaultStage.TickExecution,
            ],
            stages);
        Assert.True(startFailure.Disposed);
        Assert.True(executeFailure.Disposed);
        Assert.Same(original, startFailure.ReceivedStart);
        Assert.Same(original, executeFailure.ReceivedStart);
        Assert.Same(original, recovered.ReceivedStart);
        Assert.Equal(1, recovered.ExecuteCount);
    }

    [Fact]
    public void Threshold_IsReportedAfterFullBatchAndAppliedExplicitly()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.WithTransitions(),
            faultsAllowed: 1);
        var participant10 = new RecordingFactory(
            execute: _ => Unknown());
        var participant20 = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, participant10),
            (20, participant20));
        ActorIdentity first = new(0, 0, 0);
        ActorIdentity second = new(0, 1, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, first));
        coordinator.StartLife(Start(contract, 10, second));
        coordinator.StartLife(Start(contract, 20, opponent));

        GenericActorRuntimeTickResult result =
            coordinator.CollectTickDecisions(
                0,
                coordinator.ActiveActorIds
                    .Select(actorId =>
                        Observation(contract, actorId, 0)));

        Assert.Equal(3, result.Turns.Length);
        Assert.Equal(1, participant20.ExecuteCount);
        Assert.Equal([10], result.NewlyDisqualifiedParticipantIds.ToArray());
        Assert.False(
            coordinator.ParticipantStatuses.Single(status =>
                status.ParticipantId == 10).Disqualified);
        Assert.Throws<InvalidOperationException>(() =>
            coordinator.CollectTickDecisions(1, []));

        ImmutableArray<ActorIdentity> retired =
            coordinator.ApplyDisqualification(10);

        Assert.Equal([first, second], retired.ToArray());
        Assert.True(
            coordinator.ParticipantStatuses.Single(status =>
                status.ParticipantId == 10).Disqualified);
        Assert.Equal([opponent], coordinator.ActiveActorIds.ToArray());
        GenericActorRuntimeTickResult next =
            coordinator.CollectTickDecisions(
                1,
                [Observation(contract, opponent, 1)]);
        Assert.Single(next.Turns);
    }

    [Fact]
    public void RetireDisqualifyAndDispose_OwnEveryInstanceExactlyOnce()
    {
        ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.WithTransitions();
        var participant10 = new RecordingFactory(
            execute: _ => Unknown());
        var participant20 = new RecordingFactory();
        var coordinator = Coordinator(
            contract,
            (10, participant10),
            (20, participant20));
        ActorIdentity retired = new(0, 0, 0);
        ActorIdentity disqualified = new(0, 1, 0);
        ActorIdentity remaining = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, retired));
        coordinator.StartLife(Start(contract, 10, disqualified));
        coordinator.StartLife(Start(contract, 20, remaining));
        coordinator.CollectTickDecisions(
            0,
            coordinator.ActiveActorIds
                .Select(actorId =>
                    Observation(contract, actorId, 0)));

        coordinator.RetireLife(retired);
        coordinator.ApplyDisqualification(10);
        coordinator.Dispose();
        coordinator.Dispose();

        Assert.Equal(2, participant10.RuntimeDisposeCount);
        Assert.Equal(1, participant20.RuntimeDisposeCount);
        Assert.Equal(1, participant10.DisposeCount);
        Assert.Equal(1, participant20.DisposeCount);
    }

    [Fact]
    public void RetireLife_DisposeFailureCannotSplitLifecycleState()
    {
        ActorResolvedMatchDefinition contract =
            GenericActorContractTestFixture.Deathmatch("head-to-head");
        var first = new RecordingFactory();
        var second = new RecordingFactory();
        var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity actor = new(0, 0, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, actor));
        coordinator.StartLife(Start(contract, 20, opponent));
        coordinator.CollectTickDecisions(
            0,
            [
                Observation(contract, actor, 0),
                Observation(contract, opponent, 0),
            ]);
        first.Runtimes.Single().DisposeFailure =
            new InvalidOperationException("host cleanup failed");

        Exception? retirementError = Record.Exception(
            () => coordinator.RetireLife(actor));

        Assert.Null(retirementError);
        Assert.Equal([opponent], coordinator.ActiveActorIds.ToArray());
        Assert.True(first.Runtimes.Single().Disposed);
        Assert.Equal(1, first.Runtimes.Single().DisposeCount);

        ActorIdentity replacement = new(0, 0, 1);
        coordinator.StartLife(Start(contract, 10, replacement));
        GenericActorRuntimeTickResult next =
            coordinator.CollectTickDecisions(
                1,
                [
                    Observation(contract, replacement, 1),
                    Observation(contract, opponent, 1),
                ]);
        Assert.Equal(
            [replacement, opponent],
            next.Turns.Select(turn => turn.ActorId));

        coordinator.Dispose();
        Assert.Equal(1, first.Runtimes[0].DisposeCount);
    }

    [Fact]
    public void ApplyDisqualification_DisposeFailuresCannotSplitBatchState()
    {
        ActorResolvedMatchDefinition contract = WithFaultAllowance(
            GenericActorContractTestFixture.WithTransitions(),
            faultsAllowed: 1);
        var first = new RecordingFactory(execute: _ => Unknown());
        var second = new RecordingFactory();
        using var coordinator = Coordinator(
            contract,
            (10, first),
            (20, second));
        ActorIdentity firstActor = new(0, 0, 0);
        ActorIdentity secondActor = new(0, 1, 0);
        ActorIdentity opponent = new(1, 0, 0);
        coordinator.StartLife(Start(contract, 10, secondActor));
        coordinator.StartLife(Start(contract, 20, opponent));
        coordinator.StartLife(Start(contract, 10, firstActor));
        GenericActorRuntimeTickResult faulted =
            coordinator.CollectTickDecisions(
                0,
                coordinator.ActiveActorIds.Select(actorId =>
                    Observation(contract, actorId, 0)));
        Assert.Equal([10], faulted.NewlyDisqualifiedParticipantIds.ToArray());
        foreach (RecordingRuntime runtime in first.Runtimes)
        {
            runtime.DisposeFailure =
                new InvalidOperationException("host cleanup failed");
        }

        ImmutableArray<ActorIdentity> retired = [];
        Exception? disqualificationError = Record.Exception(
            () => retired = coordinator.ApplyDisqualification(10));

        Assert.Null(disqualificationError);
        Assert.Equal([firstActor, secondActor], retired.ToArray());
        Assert.Equal([opponent], coordinator.ActiveActorIds.ToArray());
        Assert.True(
            coordinator.ParticipantStatuses.Single(status =>
                status.ParticipantId == 10).Disqualified);
        Assert.All(first.Runtimes, runtime =>
        {
            Assert.True(runtime.Disposed);
            Assert.Equal(1, runtime.DisposeCount);
        });

        GenericActorRuntimeTickResult next =
            coordinator.CollectTickDecisions(
                1,
                [Observation(contract, opponent, 1)]);
        Assert.Single(next.Turns);
    }

    private static GenericActorRuntimeCoordinator Coordinator(
        ActorResolvedMatchDefinition contract,
        params (int ParticipantId, RecordingFactory Factory)[] factories)
    {
        Dictionary<int, PublicParticipant> participants =
            contract.Topology.Participants.ToDictionary(
                participant => participant.ParticipantId);
        return new GenericActorRuntimeCoordinator(
            contract,
            factories.Select(pair =>
                Configuration(
                    pair.ParticipantId,
                    participants[pair.ParticipantId].TeamId,
                    pair.Factory)));
    }

    private static GenericActorParticipantConfiguration Configuration(
        int participantId,
        int teamId,
        IGenericActorRuntimeFactory factory) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = $"participant-{participantId}",
            RuntimeFactory = factory,
        };

    private static void StartInitialLives(
        GenericActorRuntimeCoordinator coordinator,
        ActorResolvedMatchDefinition contract)
    {
        foreach (PublicInitialLife life in contract.Topology.InitialLives)
        {
            PublicUnitSlot slot = contract.Topology.UnitSlots.Single(value =>
                value.TeamId == life.TeamId
                && value.UnitId == life.UnitId);
            coordinator.StartLife(
                Start(
                    contract,
                    slot.ControllerParticipantId,
                    new ActorIdentity(
                        life.TeamId,
                        life.UnitId,
                        life.LifeId)));
        }
    }

    private static GenericActorRuntimeStart Start(
        ActorResolvedMatchDefinition contract,
        int participantId,
        ActorIdentity actorId) =>
        new()
        {
            SchemaVersion =
                contract.CapabilityVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                contract.CapabilityVersions.RuntimeContractVersion,
            ActorId = actorId,
            ParticipantId = participantId,
            ActorRandomSeed = (ulong)(actorId.TeamId + actorId.UnitId + 17),
            Origin = new GenericActorRuntimeStart.LifeOrigin(
                GenericActorRuntimeStart.SpawnReason.Initial,
                Generation: 0,
                ParentActorId: null,
                SourceTransitionId: null,
                SourceOperationId: null),
            Contract = contract,
        };

    private static GenericActorRuntimeObservation Observation(
        ActorResolvedMatchDefinition contract,
        ActorIdentity actorId,
        int tick,
        string formId = "mobile",
        string? emptyDomainActionId = null)
    {
        ActorFormDefinition form = contract.Rules.Forms.Single(value =>
            value.Id == formId);
        ImmutableArray<GenericActorRuntimeActionLegality> legalities =
            contract.Rules.Actions.Select(action =>
            {
                bool allowed = form.AllowedActionIds.Contains(
                    action.Id,
                    StringComparer.Ordinal);
                bool domainAvailable =
                    allowed
                    && !string.Equals(
                        action.Id,
                        emptyDomainActionId,
                        StringComparison.Ordinal);
                return new GenericActorRuntimeActionLegality(
                    action.Id,
                    action.Code,
                    allowed,
                    Available: domainAvailable,
                    action.ParameterKinds.Select(kind =>
                        Constraint(
                            contract,
                            kind,
                            domainAvailable)).ToImmutableArray());
            }).ToImmutableArray();
        return new GenericActorRuntimeObservation(
            contract.CapabilityVersions.ObservationSchemaVersion,
            tick,
            ActorContractFingerprint.ComputeMatch(contract),
            new GenericActorRuntimeObservation.ObservedSelfState(
                actorId,
                Generation: 0,
                FormId: form.Id,
                new Position(0, 0),
                Direction.North,
                Health: form.MaxHealth,
                Cooldown: 0,
                Energy: 10,
                PreviousActionResolution: null,
                PendingSameLifeTransition: null),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            new GenericActorRuntimeObservation.ScoreboardState([]),
            new GenericActorRuntimeObservation.ModeObservationState.Deathmatch(
                contract.Rules.GameMode.ModeId),
            legalities);
    }

    private static GenericActorRuntimeActionLegality.ArgumentConstraint
        Constraint(
            ActorResolvedMatchDefinition contract,
            ActorActionParameterKind kind,
            bool domainAvailable) =>
        kind switch
        {
            ActorActionParameterKind.ShotProgram =>
                new GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ShotProgramConstraint(domainAvailable),
            ActorActionParameterKind.Direction =>
                new GenericActorRuntimeActionLegality.ArgumentConstraint
                    .DirectionConstraint(
                        domainAvailable
                            ? Enum.GetValues<Direction>().ToImmutableArray()
                            : []),
            ActorActionParameterKind.UnitTarget =>
                new GenericActorRuntimeActionLegality.ArgumentConstraint
                    .UnitTargetConstraint(
                        domainAvailable
                            ? contract.Topology.UnitSlots.Select(slot =>
                                new GenericActorRuntimeActionArgument.UnitTarget(
                                    slot.TeamId,
                                    slot.UnitId)).ToImmutableArray()
                            : []),
            ActorActionParameterKind.FormTarget =>
                new GenericActorRuntimeActionLegality.ArgumentConstraint
                    .FormTargetConstraint(
                        domainAvailable
                            ? contract.Rules.Forms.Select(form => form.Id)
                                .ToImmutableArray()
                            : []),
            ActorActionParameterKind.ProjectileHeading =>
                new GenericActorRuntimeActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint(
                        domainAvailable
                            ? Enum.GetValues<ProjectileHeading>()
                                .ToImmutableArray()
                            : []),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static ActorResolvedMatchDefinition WithProjectionCatalog()
    {
        ActorResolvedMatchDefinition source =
            GenericActorContractTestFixture.WithTransitions();
        ActorRulesDefinition rules = source.Rules;
        ActorActionDefinition targetedAnchor = new(
            "anchor",
            101,
            ActorActionKind.SameLifeTransition,
            [ActorActionParameterKind.FormTarget]);
        ActorActionDefinition move = new(
            "move",
            1,
            ActorActionKind.Movement,
            [ActorActionParameterKind.Direction]);
        ActorFormDefinition[] forms = rules.Forms.Select(form =>
            new ActorFormDefinition(
                form.Id,
                form.MaxHealth,
                form.MovementProfileId,
                form.VisionProfileId,
                form.AttackProfileId,
                form.ObjectiveWeight,
                form.Id == "mobile"
                    ? form.AllowedActionIds.Append(move.Id)
                    : form.AllowedActionIds)).ToArray();
        ActorActionDefinition[] actions =
        [
            .. rules.Actions.Select(action =>
                action.Id == targetedAnchor.Id
                    ? targetedAnchor
                    : action),
            move,
        ];
        var projectedRules = new ActorRulesDefinition(
            rules.RulesetId,
            rules.Limits,
            rules.SeedMechanics,
            rules.GameMode,
            rules.Lifecycle,
            forms,
            rules.MovementProfiles,
            rules.VisionProfiles,
            rules.AttackProfiles,
            actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            projectedRules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static ActorResolvedMatchDefinition WithFaultAllowance(
        ActorResolvedMatchDefinition source,
        int faultsAllowed)
    {
        ActorRulesDefinition rules = source.Rules;
        var clonedRules = new ActorRulesDefinition(
            rules.RulesetId,
            new ActorRulesLimits(
                rules.Limits.MaxTicks,
                new ActorRuntimeFaultDefinition(faultsAllowed)),
            rules.SeedMechanics,
            rules.GameMode,
            rules.Lifecycle,
            rules.Forms,
            rules.MovementProfiles,
            rules.VisionProfiles,
            rules.AttackProfiles,
            rules.Actions,
            rules.FabricationTransitions,
            rules.SameLifeTransitions,
            rules.ReplicationTransitions,
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);
        return new ActorResolvedMatchDefinition(
            clonedRules,
            source.Map,
            source.Format,
            source.Topology,
            source.InitialDeployment,
            source.LifecycleAssignments,
            source.ParticipantRegionAssignments,
            source.ModeMapBinding,
            source.CapabilityVersions);
    }

    private static GenericActorRuntimeDecision Wait() =>
        new("wait", 0, [], null);

    private static GenericActorRuntimeDecision Unknown() =>
        new("not-catalogued", 999, [], null);

    private static GenericActorRuntimeDecision Fabricate(
        GenericActorRuntimeActionArgument.UnitTarget target) =>
        new(
            "fabricate",
            100,
            [
                new GenericActorRuntimeActionArgument.UnitTargetArgument(
                    target),
            ],
            null);

    private static GenericActorRuntimeDecision Shoot(ShotProgram program) =>
        new(
            "shoot",
            4,
            [
                new GenericActorRuntimeActionArgument.ShotProgramArgument(
                    program),
            ],
            null);

    private sealed class RecordingFactory : IGenericActorRuntimeFactory
    {
        private readonly Func<
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision?> _execute;

        public RecordingFactory(
            Func<
                GenericActorRuntimeObservation,
                GenericActorRuntimeDecision?>? execute = null)
        {
            _execute = execute ?? (_ => Wait());
        }

        public Queue<Func<IGenericActorRuntime>> CreateSteps { get; } = [];
        public List<RecordingRuntime> Runtimes { get; } = [];
        public int CreateCount { get; private set; }
        public int ExecuteCount => Runtimes.Sum(runtime =>
            runtime.ExecuteCount);
        public int RuntimeDisposeCount => Runtimes.Count(runtime =>
            runtime.Disposed);
        public int DisposeCount { get; private set; }

        public IGenericActorRuntime CreateRuntime()
        {
            CreateCount++;
            RecordingRuntime runtime;
            if (CreateSteps.TryDequeue(
                    out Func<IGenericActorRuntime>? create))
            {
                IGenericActorRuntime created = create();
                runtime = Assert.IsType<RecordingRuntime>(created);
            }
            else
            {
                runtime = new RecordingRuntime
                {
                    Execute = _execute,
                };
            }
            Runtimes.Add(runtime);
            return runtime;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class RecordingRuntime : IGenericActorRuntime
    {
        public Action<GenericActorRuntimeStart>? Start { get; init; }
        public Func<
            GenericActorRuntimeObservation,
            GenericActorRuntimeDecision?>? Execute { get; init; }
        public GenericActorRuntimeStart? ReceivedStart { get; private set; }
        public int ExecuteCount { get; private set; }
        public bool Disposed { get; private set; }
        public int DisposeCount { get; private set; }
        public Exception? DisposeFailure { get; set; }

        public void StartLife(GenericActorRuntimeStart start)
        {
            ReceivedStart = start;
            Start?.Invoke(start);
        }

        public GenericActorRuntimeDecision ExecuteTick(
            GenericActorRuntimeObservation observation)
        {
            ExecuteCount++;
            if (Execute is null)
                return Wait();
            return Execute(observation)!;
        }

        public void Dispose()
        {
            Assert.False(Disposed);
            Disposed = true;
            DisposeCount++;
            if (DisposeFailure is not null)
                throw DisposeFailure;
        }
    }
}
