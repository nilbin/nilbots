using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Transport-neutral owner of participant runtime factories and the ONE mind
/// instance each submitted participant owns for the whole match
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §2.4, §4.1, §4.7). It
/// collects one exact participant decision batch at a time and fans each mind's
/// decision MAP out across that participant's bodies; gameplay, damage,
/// lifecycle and mode state remain outside this boundary exactly as they do for
/// <see cref="GenericActorRuntimeCoordinator"/>.
/// <para>
/// Three rules define it, and all three are the memo's:
/// </para>
/// <list type="number">
/// <item><b>The mind ticks every tick.</b> Once per tick, unconditionally, from
/// tick 0 to the terminal tick, regardless of how many bodies it owns —
/// including zero. Going dark on a total body loss would lose the ability to
/// plan the return, accumulate silent memory staleness, and blind the mind
/// during the window its enemy beliefs decay fastest (§2.7).</item>
/// <item><b>Every own live body is pre-filled with <c>Wait</c>.</b> Forgetting a
/// body costs that body one tick, visibly, in the replay — not the match. A key
/// naming a body the participant does not own or one that is not live is
/// <c>Rejected</c>; two commands for one body, a malformed action, or a
/// malformed argument is <c>Faulted</c> (§2.4).</item>
/// <item><b>A fault is participant-scoped.</b> Not a new policy: the existing
/// policy applied to a coarser unit, using the same saturating counter and the
/// same allowance. Under the shipped <c>frontline-labs-1</c> allowance of zero
/// the blast radius is identical to a per-life trap's — the first fault
/// disqualifies the participant and every slot it controls (§4.7).</item>
/// </list>
/// </summary>
public sealed class GenericMindRuntimeCoordinator : IDisposable
{
    private readonly ActorResolvedMatchDefinition _contract;
    private readonly string _matchContractFingerprint;
    private readonly ulong _matchSeed;
    private readonly GenericActorDecisionAdmission _admission;
    private readonly Dictionary<int, ParticipantState> _participants;
    private readonly Dictionary<(int TeamId, int UnitId), PublicUnitSlot>
        _slots;
    private readonly Dictionary<string, ActorFormDefinition> _formsById;
    private readonly Dictionary<ActorIdentity, ActiveLifeState> _activeLives =
        [];
    private readonly HashSet<ActorIdentity> _issuedActorIds = [];
    private readonly HashSet<IGenericMindRuntime> _issuedRuntimes =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(int TeamId, int UnitId), ActorIdentity>
        _occupiedSlots = [];
    private readonly HashSet<int> _pendingDisqualifications = [];
    private int? _lastCollectedTick;
    private bool _collectingTick;
    private bool _disposed;

    /// <summary>
    /// Creates a match-scoped coordinator and takes ownership of every
    /// participant mind factory after successful validation.
    /// </summary>
    public GenericMindRuntimeCoordinator(
        ActorResolvedMatchDefinition contract,
        IEnumerable<GenericActorParticipantConfiguration> participants,
        ulong matchSeed)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(participants);
        if (!contract.CapabilityVersions.IsMindProfile)
        {
            throw new ArgumentException(
                "The mind coordinator only executes contracts resolved on the "
                + $"'{BotArenaVersions.GenericMindContractProfileId}' profile.",
                nameof(contract));
        }

        GenericActorParticipantConfiguration[] configurations =
            [.. participants];
        ValidateParticipantConfigurations(contract, configurations);

        _contract = contract;
        _matchSeed = matchSeed;
        _admission = new GenericActorDecisionAdmission(contract);
        _matchContractFingerprint =
            ActorContractFingerprint.ComputeMatch(contract);
        _slots = contract.Topology.UnitSlots.ToDictionary(
            slot => (slot.TeamId, slot.UnitId));
        _formsById = contract.Rules.Forms.ToDictionary(
            form => form.Id,
            StringComparer.Ordinal);
        _participants = configurations.ToDictionary(
            configuration => configuration.ParticipantId,
            configuration => new ParticipantState(
                configuration,
                contract.Topology.Participants
                    .Where(participant =>
                        participant.TeamId == configuration.TeamId
                        && participant.ParticipantId
                            != configuration.ParticipantId)
                    .Select(participant => participant.ParticipantId)
                    .Order()
                    .ToImmutableArray()));
    }

    /// <summary>The immutable resolved match definition used by every mind.</summary>
    public ActorResolvedMatchDefinition Contract => _contract;

    /// <summary>Fingerprint joining every mind and observation to the match.</summary>
    public string MatchContractFingerprint => _matchContractFingerprint;

    /// <summary>Active runtime-layer actor identities in canonical order.</summary>
    public ImmutableArray<ActorIdentity> ActiveActorIds
    {
        get
        {
            ThrowIfDisposed();
            return _activeLives.Keys.Order().ToImmutableArray();
        }
    }

    /// <summary>
    /// Participants that must receive a mind observation this tick, ascending.
    /// Every non-disqualified participant is here on EVERY tick, whether or not
    /// it owns a live body — that is the tick invariant expressed as data.
    /// </summary>
    public ImmutableArray<int> TickingParticipantIds
    {
        get
        {
            ThrowIfDisposed();
            return
            [
                .. _participants.Values
                    .Where(participant => !participant.Disqualified)
                    .Select(participant => participant.ParticipantId)
                    .Order(),
            ];
        }
    }

    /// <inheritdoc cref="GenericActorRuntimeCoordinator.ParticipantStatuses"/>
    public ImmutableArray<
        GenericActorRuntimeObservation.ObservedParticipantStatus>
        ParticipantStatuses
    {
        get
        {
            ThrowIfDisposed();
            return _participants.Values
                .OrderBy(participant => participant.ParticipantId)
                .Select(participant =>
                    new GenericActorRuntimeObservation
                        .ObservedParticipantStatus(
                            participant.ParticipantId,
                            participant.TeamId,
                            participant.FaultCount,
                            participant.Disqualified)
                        {
                            ClassId = _contract.Topology.Participants
                                .Single(value =>
                                    value.ParticipantId
                                        == participant.ParticipantId)
                                .ClassId,
                        })
                .ToImmutableArray();
        }
    }

    /// <summary>Own live bodies for one participant, in canonical order.</summary>
    public ImmutableArray<ActorIdentity> LiveBodiesOf(int participantId)
    {
        ThrowIfDisposed();
        return
        [
            .. _activeLives.Values
                .Where(life => life.Start.ParticipantId == participantId)
                .Select(life => life.Start.ActorId)
                .Order(),
        ];
    }

    /// <inheritdoc cref="GenericActorRuntimeCoordinator.TryProjectSubmittedAction"/>
    internal bool TryProjectSubmittedAction(
        GenericActorRuntimeDecision? decision,
        out GenericActorRuntimeActionResolution.ResolvedAction?
            submittedAction) =>
        _admission.TryProjectSubmittedAction(decision, out submittedAction);

    /// <summary>
    /// Registers one authoritative active body. Unlike the per-life
    /// coordinator this creates NO runtime and starts NO instance: the mind
    /// that will command this body is already running, or will be created on
    /// the participant's first tick. A body's arrival is data.
    /// </summary>
    public void StartLife(GenericActorRuntimeStart start)
    {
        ThrowIfDisposed();
        ThrowIfCollectingTick();
        ArgumentNullException.ThrowIfNull(start);
        if (_pendingDisqualifications.Count != 0)
        {
            throw new InvalidOperationException(
                "Reported participant disqualifications must be applied before starting another life.");
        }

        ValidateLifeStart(start);
        if (_issuedActorIds.Contains(start.ActorId))
        {
            throw new ArgumentException(
                $"Actor life identity '{start.ActorId}' was already issued in this match.",
                nameof(start));
        }

        var slotKey = (start.ActorId.TeamId, start.ActorId.UnitId);
        if (_occupiedSlots.TryGetValue(
                slotKey,
                out ActorIdentity? occupyingActor))
        {
            throw new ArgumentException(
                $"Unit slot {slotKey.TeamId}:{slotKey.UnitId} is already " +
                $"occupied by actor life '{occupyingActor}'.",
                nameof(start));
        }

        _activeLives.Add(start.ActorId, new ActiveLifeState(start));
        _issuedActorIds.Add(start.ActorId);
        _occupiedSlots.Add(slotKey, start.ActorId);
    }

    /// <summary>
    /// Retires one body. It disposes NOTHING — the mind survives its bodies,
    /// which is the persistent-memory half of #190.
    /// </summary>
    public void RetireLife(ActorIdentity actorId)
    {
        ThrowIfDisposed();
        ThrowIfCollectingTick();
        ArgumentNullException.ThrowIfNull(actorId);
        if (!_activeLives.Remove(actorId))
        {
            throw new ArgumentException(
                $"Actor life '{actorId}' is not active.",
                nameof(actorId));
        }

        _occupiedSlots.Remove((actorId.TeamId, actorId.UnitId));
    }

    /// <summary>
    /// Invokes every and only non-disqualified participant once, in
    /// participant order, and fans each returned command set across that
    /// participant's own live bodies. The complete input is validated before
    /// the first runtime is touched.
    /// </summary>
    public GenericMindRuntimeTickResult CollectTickDecisions(
        int tick,
        IEnumerable<GenericMindRuntimeObservation> observations)
    {
        ThrowIfDisposed();
        ThrowIfCollectingTick();
        ArgumentNullException.ThrowIfNull(observations);
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        if (_lastCollectedTick is int previousTick
            && tick <= previousTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                "Decision ticks must increase strictly.");
        }
        if (_pendingDisqualifications.Count != 0)
        {
            throw new InvalidOperationException(
                "Reported participant disqualifications must be applied before collecting another tick.");
        }

        Dictionary<int, GenericMindRuntimeObservation> byParticipant =
            ValidateExactObservationBatch(tick, observations);

        _collectingTick = true;
        _lastCollectedTick = tick;
        try
        {
            var mindTurns = ImmutableArray.CreateBuilder<
                GenericMindRuntimeTurn>(byParticipant.Count);
            var bodyTurns =
                ImmutableArray.CreateBuilder<GenericActorRuntimeTurn>();
            var faults =
                ImmutableArray.CreateBuilder<GenericMindRuntimeFault>();
            foreach (int participantId in byParticipant.Keys.Order())
            {
                ParticipantState participant = _participants[participantId];
                GenericMindRuntimeObservation observation =
                    byParticipant[participantId];
                GenericMindRuntimeTurn turn = Invoke(
                    participant,
                    observation,
                    bodyTurns);
                mindTurns.Add(turn);
                if (turn.RuntimeFault is not null)
                    faults.Add(turn.RuntimeFault);
            }

            return new GenericMindRuntimeTickResult(
                tick,
                mindTurns.MoveToImmutable(),
                bodyTurns.ToImmutable(),
                faults.ToImmutable(),
                _pendingDisqualifications.Order().ToImmutableArray());
        }
        finally
        {
            _collectingTick = false;
        }
    }

    /// <inheritdoc cref="GenericActorRuntimeCoordinator.ApplyDisqualification"/>
    public ImmutableArray<ActorIdentity> ApplyDisqualification(
        int participantId)
    {
        ThrowIfDisposed();
        ThrowIfCollectingTick();
        if (!_participants.TryGetValue(
                participantId,
                out ParticipantState? participant))
        {
            throw new ArgumentOutOfRangeException(nameof(participantId));
        }
        if (!_pendingDisqualifications.Remove(participantId))
        {
            throw new InvalidOperationException(
                $"Participant {participantId} has no reported disqualification pending.");
        }

        participant.Disqualified = true;
        ActorIdentity[] actorIds = _activeLives.Values
            .Where(life => life.Start.ParticipantId == participantId)
            .Select(life => life.Start.ActorId)
            .Order()
            .ToArray();
        foreach (ActorIdentity actorId in actorIds)
        {
            _activeLives.Remove(actorId);
            _occupiedSlots.Remove((actorId.TeamId, actorId.UnitId));
        }
        DiscardRuntime(participant);
        return actorIds.ToImmutableArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        ThrowIfCollectingTick();
        _disposed = true;

        var errors = new List<Exception>();
        _activeLives.Clear();
        _occupiedSlots.Clear();
        foreach (ParticipantState participant in _participants.Values
                     .OrderBy(participant => participant.ParticipantId))
        {
            TryDisposeRuntime(participant, errors);
            try
            {
                participant.MindFactory.Dispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        if (errors.Count == 1)
            throw errors.Single();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    private GenericMindRuntimeTurn Invoke(
        ParticipantState participant,
        GenericMindRuntimeObservation observation,
        ImmutableArray<GenericActorRuntimeTurn>.Builder bodyTurns)
    {
        ImmutableArray<ActiveLifeState> ownBodies =
        [
            .. _activeLives.Values
                .Where(life =>
                    life.Start.ParticipantId == participant.ParticipantId)
                .OrderBy(life => life.Start.ActorId),
        ];
        long fuel = GenericMindTickBudget.TickFuel(ownBodies.Length);

        if (participant.Runtime is null)
        {
            IGenericMindRuntime created;
            try
            {
                created = participant.MindFactory.CreateRuntime()
                    ?? throw new InvalidOperationException(
                        "Mind runtime factory returned null.");
            }
            catch (Exception)
            {
                participant.Runtime = null;
                return FaultedTurn(
                    participant,
                    ownBodies,
                    fuel,
                    GenericActorRuntimeFault.FaultStage.RuntimeCreate,
                    GenericActorRuntimeFaultCodes.RuntimeCreateFailed,
                    bodyTurns,
                    submittedCommands: []);
            }

            if (!_issuedRuntimes.Add(created))
            {
                return FaultedTurn(
                    participant,
                    ownBodies,
                    fuel,
                    GenericActorRuntimeFault.FaultStage.RuntimeCreate,
                    GenericActorRuntimeFaultCodes.RuntimeInstanceReused,
                    bodyTurns,
                    submittedCommands: []);
            }
            participant.Runtime = created;

            try
            {
                participant.Runtime.StartMatch(MatchStartFor(participant));
            }
            catch (Exception)
            {
                DiscardRuntime(participant);
                return FaultedTurn(
                    participant,
                    ownBodies,
                    fuel,
                    GenericActorRuntimeFault.FaultStage.LifeStart,
                    GenericActorRuntimeFaultCodes.LifeStartFailed,
                    bodyTurns,
                    submittedCommands: []);
            }
        }

        GenericMindRuntimeDecisions? submitted;
        try
        {
            submitted = participant.Runtime.ExecuteTick(observation);
        }
        catch (Exception)
        {
            DiscardRuntime(participant);
            return FaultedTurn(
                participant,
                ownBodies,
                fuel,
                GenericActorRuntimeFault.FaultStage.TickExecution,
                GenericActorRuntimeFaultCodes.TickExecutionFailed,
                bodyTurns,
                submittedCommands: []);
        }

        return AdmitDecisionMap(
            participant,
            observation,
            ownBodies,
            fuel,
            submitted,
            bodyTurns);
    }

    private GenericMindRuntimeTurn AdmitDecisionMap(
        ParticipantState participant,
        GenericMindRuntimeObservation observation,
        ImmutableArray<ActiveLifeState> ownBodies,
        long fuel,
        GenericMindRuntimeDecisions? submitted,
        ImmutableArray<GenericActorRuntimeTurn>.Builder bodyTurns)
    {
        if (submitted is null
            || submitted.Commands.IsDefault
            || submitted.Intents.IsDefault
            || submitted.Commands.Any(command => command is null))
        {
            return FaultedTurn(
                participant,
                ownBodies,
                fuel,
                GenericActorRuntimeFault.FaultStage.DecisionValidation,
                GenericActorRuntimeFaultCodes.MalformedDecisionMap,
                bodyTurns,
                submittedCommands: []);
        }

        var bodiesByKey = ownBodies.ToDictionary(
            life => (life.Start.ActorId.UnitId, life.Start.ActorId.LifeId));
        var legalitiesByActor = observation.Bodies.ToDictionary(
            body => body.ActorId,
            body => body);
        var accepted =
            new Dictionary<ActorIdentity, GenericActorRuntimeDecision>();
        var submittedByActor =
            new Dictionary<ActorIdentity, GenericActorRuntimeDecision>();
        var commands = ImmutableArray.CreateBuilder<
            GenericMindCommandResolution>(submitted.Commands.Length);
        var commandedKeys = new HashSet<(int UnitId, int LifeId)>();

        foreach (GenericMindCommand command in submitted.Commands)
        {
            var key = (command.UnitId, command.LifeId);
            if (!commandedKeys.Add(key))
            {
                // Two commands for one body is a contradiction the host
                // cannot resolve, so it Faults — the memo is explicit that a
                // second Command on a handle must not silently win.
                return FaultedTurn(
                    participant,
                    ownBodies,
                    fuel,
                    GenericActorRuntimeFault.FaultStage.DecisionValidation,
                    GenericActorRuntimeFaultCodes.DuplicateBodyCommand,
                    bodyTurns,
                    submitted.Commands);
            }

            if (!bodiesByKey.TryGetValue(key, out ActiveLifeState? body))
            {
                // Not owned, or not live this tick. Forgivable under
                // persistent memory, so Rejected rather than Faulted.
                commands.Add(new GenericMindCommandResolution(
                    command,
                    GenericMindCommandOutcome.Rejected));
                continue;
            }

            GenericMindRuntimeObservation.ObservedBodyState state =
                legalitiesByActor[body.Start.ActorId];
            GenericActorRuntimeDecision decision = command.ToDecision();
            if (!_admission.TryAdmitDecision(
                    state.FormId,
                    state.ActionLegalities,
                    decision,
                    out GenericActorRuntimeDecision? admitted,
                    out string? faultCode))
            {
                return FaultedTurn(
                    participant,
                    ownBodies,
                    fuel,
                    GenericActorRuntimeFault.FaultStage.DecisionValidation,
                    faultCode!,
                    bodyTurns,
                    submitted.Commands);
            }

            accepted[body.Start.ActorId] = admitted!;
            submittedByActor[body.Start.ActorId] = decision;
            commands.Add(new GenericMindCommandResolution(
                command,
                GenericMindCommandOutcome.Accepted));
        }

        foreach (ActiveLifeState body in ownBodies)
        {
            ActorIdentity actorId = body.Start.ActorId;
            // Every live body the mind did not name waits. The pre-filled
            // Wait is what was submitted on that body's behalf — that is the
            // whole mechanism, not a fallback: the host fills, the mind
            // overwrites, and there is no key set for an author to get wrong.
            GenericActorRuntimeDecision decision =
                accepted.TryGetValue(
                    actorId,
                    out GenericActorRuntimeDecision? commanded)
                    ? commanded
                    : _admission.SyntheticWait(
                        legalitiesByActor[actorId].FormId);
            bodyTurns.Add(new GenericActorRuntimeTurn(
                participant.ParticipantId,
                actorId,
                submittedByActor.GetValueOrDefault(actorId, decision),
                decision,
                GenericActorRuntimeActionResolution.ActionOutcome.Success,
                RuntimeFault: null));
        }

        return new GenericMindRuntimeTurn(
            participant.ParticipantId,
            participant.TeamId,
            fuel,
            ownBodies.Length,
            commands.ToImmutable(),
            [.. ownBodies.Select(body => body.Start.ActorId)],
            // Reserved (§11.3): recorded and Rejected, never Faulted, until a
            // format with allied minds is admitted.
            submitted.Intents,
            RuntimeFault: null);
    }

    private GenericMindRuntimeTurn FaultedTurn(
        ParticipantState participant,
        ImmutableArray<ActiveLifeState> ownBodies,
        long fuel,
        GenericActorRuntimeFault.FaultStage stage,
        string faultCode,
        ImmutableArray<GenericActorRuntimeTurn>.Builder bodyTurns,
        ImmutableArray<GenericMindCommand> submittedCommands)
    {
        GenericMindRuntimeFault fault = RecordFault(
            participant,
            ownBodies.IsEmpty ? null : ownBodies[0].Start.ActorId,
            stage,
            faultCode);
        foreach (ActiveLifeState body in ownBodies)
        {
            bodyTurns.Add(new GenericActorRuntimeTurn(
                participant.ParticipantId,
                body.Start.ActorId,
                SubmittedDecision: null,
                _admission.SyntheticWait(body.CurrentFormId!),
                GenericActorRuntimeActionResolution.ActionOutcome.Faulted,
                fault.ToActorFault()));
        }

        return new GenericMindRuntimeTurn(
            participant.ParticipantId,
            participant.TeamId,
            fuel,
            ownBodies.Length,
            [
                .. submittedCommands.Select(command =>
                    new GenericMindCommandResolution(
                        command,
                        GenericMindCommandOutcome.Rejected)),
            ],
            [.. ownBodies.Select(body => body.Start.ActorId)],
            [],
            fault);
    }

    private GenericMindRuntimeFault RecordFault(
        ParticipantState participant,
        ActorIdentity? actorId,
        GenericActorRuntimeFault.FaultStage stage,
        string faultCode)
    {
        long threshold = _contract.Rules.Limits.RuntimeFaults
            .DisqualificationFaultCount;
        long previous = participant.FaultCount;
        if (previous < threshold)
            participant.FaultCount = previous + 1;
        bool triggered =
            previous < threshold && participant.FaultCount >= threshold;
        if (triggered)
            _pendingDisqualifications.Add(participant.ParticipantId);

        return new GenericMindRuntimeFault(
            participant.ParticipantId,
            participant.TeamId,
            actorId,
            stage,
            faultCode,
            participant.FaultCount,
            triggered);
    }

    private GenericMindRuntimeStart MatchStartFor(
        ParticipantState participant) =>
        new()
        {
            SchemaVersion =
                _contract.CapabilityVersions.MatchStartSchemaVersion,
            RuntimeContractVersion =
                _contract.CapabilityVersions.RuntimeContractVersion,
            ParticipantId = participant.ParticipantId,
            TeamId = participant.TeamId,
            MindRandomSeed = SeedDerivation.DeriveMindSeed(
                _matchSeed,
                participant.ParticipantId,
                _contract.Rules.SeedMechanics.SeedProfileId),
            TeamRandomSeed = SeedDerivation.DeriveTeamSeed(
                _matchSeed,
                participant.TeamId,
                _contract.Rules.SeedMechanics.SeedProfileId),
            AlliedParticipantIds = participant.AlliedParticipantIds,
            Contract = _contract,
        };

    private Dictionary<int, GenericMindRuntimeObservation>
        ValidateExactObservationBatch(
            int tick,
            IEnumerable<GenericMindRuntimeObservation> observations)
    {
        var byParticipant =
            new Dictionary<int, GenericMindRuntimeObservation>();
        foreach (GenericMindRuntimeObservation? observation in observations)
        {
            if (observation is null)
            {
                throw new ArgumentException(
                    "Observation batches cannot contain null entries.",
                    nameof(observations));
            }
            if (!byParticipant.TryAdd(
                    observation.ParticipantId,
                    observation))
            {
                throw new ArgumentException(
                    $"Observation batch contains participant {observation.ParticipantId} more than once.",
                    nameof(observations));
            }
        }

        ImmutableArray<int> expected = TickingParticipantIds;
        if (byParticipant.Count != expected.Length
            || expected.Any(
                participantId => !byParticipant.ContainsKey(participantId)))
        {
            throw new ArgumentException(
                "Observation batch must contain every and only ticking participant exactly once.",
                nameof(observations));
        }

        foreach ((int participantId,
                  GenericMindRuntimeObservation observation) in byParticipant)
        {
            ValidateObservation(
                tick,
                _participants[participantId],
                observation);
        }
        return byParticipant;
    }

    private void ValidateObservation(
        int tick,
        ParticipantState participant,
        GenericMindRuntimeObservation observation)
    {
        if (observation.SchemaVersion
            != _contract.CapabilityVersions.ObservationSchemaVersion)
        {
            throw InvalidObservation(
                participant.ParticipantId,
                "schema version does not match the resolved capability profile");
        }
        if (observation.Tick != tick)
        {
            throw InvalidObservation(
                participant.ParticipantId,
                $"tick {observation.Tick} does not match batch tick {tick}");
        }
        if (!string.Equals(
                observation.MatchContractFingerprint,
                _matchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw InvalidObservation(
                participant.ParticipantId,
                "match-contract fingerprint does not match");
        }
        if (observation.TeamId != participant.TeamId
            || observation.Team is null
            || observation.Team.TeamId != participant.TeamId)
        {
            throw InvalidObservation(
                participant.ParticipantId,
                "team identity does not match the registered participant");
        }
        if (observation.Bodies.IsDefault
            || observation.Slots.IsDefault
            || observation.Allies.IsDefault
            || observation.AlliedIntents.IsDefault)
        {
            throw InvalidObservation(
                participant.ParticipantId,
                "a required collection is uninitialized");
        }
        if (!observation.AlliedIntents.IsEmpty)
        {
            // §11.3: reserved and inert. The engine writes the empty
            // collection in v1, so a populated one is a host bug rather than a
            // bot's, and it fails closed.
            throw InvalidObservation(
                participant.ParticipantId,
                "allied intents are reserved and must be empty in this generation");
        }

        ValidateBodies(participant, observation);
        ValidateSlots(participant, observation);
    }

    private void ValidateBodies(
        ParticipantState participant,
        GenericMindRuntimeObservation observation)
    {
        ImmutableArray<ActorIdentity> expected =
            LiveBodiesOf(participant.ParticipantId);
        if (observation.Bodies.Length != expected.Length
            || observation.Bodies
                .Select(body => body.ActorId)
                .SequenceEqual(expected) is false)
        {
            throw InvalidObservation(
                participant.ParticipantId,
                "bodies must be exactly this participant's own live bodies in canonical order");
        }

        foreach (GenericMindRuntimeObservation.ObservedBodyState body in
                 observation.Bodies)
        {
            ActiveLifeState life = _activeLives[body.ActorId];
            if (body.Generation != life.Start.Origin.Generation)
            {
                throw GenericActorDecisionAdmission.InvalidObservation(
                    body.ActorId,
                    "generation does not match the registered life");
            }
            if (!_formsById.TryGetValue(
                    body.FormId,
                    out ActorFormDefinition? form))
            {
                throw GenericActorDecisionAdmission.InvalidObservation(
                    body.ActorId,
                    $"body form '{body.FormId}' is not in the rules catalog");
            }

            life.CurrentFormId = body.FormId;
            _admission.ValidateActionLegalities(
                body.ActorId,
                form,
                body.ActionLegalities);
        }
    }

    private void ValidateSlots(
        ParticipantState participant,
        GenericMindRuntimeObservation observation)
    {
        ImmutableArray<(int TeamId, int UnitId)> expected =
        [
            .. _slots.Values
                .Where(slot =>
                    slot.ControllerParticipantId
                        == participant.ParticipantId)
                .OrderBy(slot => slot.UnitId)
                .Select(slot => (slot.TeamId, slot.UnitId)),
        ];
        if (observation.Slots.Length != expected.Length
            || observation.Slots
                .Select(slot => (slot.TeamId, slot.UnitId))
                .SequenceEqual(expected) is false)
        {
            throw InvalidObservation(
                participant.ParticipantId,
                "slots must be exactly this participant's own slots in canonical order");
        }
        if (observation.Slots.Any(slot =>
                slot.State is null
                || slot.CandidateClassIds.IsDefault
                || !slot.CandidateClassIds.IsEmpty
                || slot.SelectedClassId is not null))
        {
            // §10.1: v1 hardcodes `fixed` on every slot and REFUSES
            // `chosen-at-activation`. The shape is reserved; nothing executes.
            throw InvalidObservation(
                participant.ParticipantId,
                "chassis chosen at activation is reserved and refused in this generation");
        }
    }

    private void ValidateLifeStart(GenericActorRuntimeStart start)
    {
        if (start.ActorId is null
            || start.Origin is null
            || start.Contract is null)
        {
            throw new ArgumentException(
                "Life start must contain actor identity, origin, and contract.",
                nameof(start));
        }
        if (!_participants.TryGetValue(
                start.ParticipantId,
                out ParticipantState? participant))
        {
            throw new ArgumentException(
                $"Life start references unknown participant {start.ParticipantId}.",
                nameof(start));
        }
        if (participant.Disqualified
            || _pendingDisqualifications.Contains(start.ParticipantId))
        {
            throw new InvalidOperationException(
                $"Disqualified participant {start.ParticipantId} cannot start another life.");
        }
        if (!_slots.TryGetValue(
                (start.ActorId.TeamId, start.ActorId.UnitId),
                out PublicUnitSlot? slot)
            || slot.ControllerParticipantId != start.ParticipantId
            || participant.TeamId != start.ActorId.TeamId)
        {
            throw new ArgumentException(
                $"Actor '{start.ActorId}' is not owned by participant {start.ParticipantId}.",
                nameof(start));
        }
        if (!string.Equals(
                ActorContractFingerprint.ComputeMatch(start.Contract),
                _matchContractFingerprint,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Life start contract does not match the coordinator contract.",
                nameof(start));
        }
    }

    private static void ValidateParticipantConfigurations(
        ActorResolvedMatchDefinition contract,
        IReadOnlyList<GenericActorParticipantConfiguration> configurations)
    {
        var expected = contract.Topology.Participants.ToDictionary(
            participant => participant.ParticipantId);
        var actual =
            new Dictionary<int, GenericActorParticipantConfiguration>();
        var factories = new HashSet<IGenericMindRuntimeFactory>(
            ReferenceEqualityComparer.Instance);
        foreach (GenericActorParticipantConfiguration? configuration in
                 configurations)
        {
            if (configuration is null
                || configuration.MindRuntimeFactory is null)
            {
                throw new ArgumentException(
                    "Every participant on the mind profile must configure a mind runtime factory.",
                    nameof(configurations));
            }
            if (!actual.TryAdd(
                    configuration.ParticipantId,
                    configuration))
            {
                throw new ArgumentException(
                    $"Participant {configuration.ParticipantId} is configured more than once.",
                    nameof(configurations));
            }
            if (!expected.TryGetValue(
                    configuration.ParticipantId,
                    out PublicParticipant? participant)
                || participant.TeamId != configuration.TeamId)
            {
                throw new ArgumentException(
                    $"Participant {configuration.ParticipantId} does not match the resolved topology.",
                    nameof(configurations));
            }
            if (!factories.Add(configuration.MindRuntimeFactory))
            {
                throw new ArgumentException(
                    "Each participant must own a distinct runtime factory instance.",
                    nameof(configurations));
            }
        }
        if (actual.Count != expected.Count
            || expected.Keys.Any(participantId =>
                !actual.ContainsKey(participantId)))
        {
            throw new ArgumentException(
                "Participant configurations must exactly match the resolved topology.",
                nameof(configurations));
        }
    }

    private static ArgumentException InvalidObservation(
        int participantId,
        string reason) =>
        new(
            $"Mind observation for participant {participantId} is invalid: {reason}.",
            "observations");

    private static void DiscardRuntime(ParticipantState participant)
    {
        IGenericMindRuntime? runtime = participant.Runtime;
        participant.Runtime = null;
        if (runtime is null)
            return;
        try
        {
            runtime.Dispose();
        }
        catch (Exception)
        {
            // Adapter cleanup is best-effort after authoritative runtime and
            // lifecycle state has already committed.
        }
    }

    private static void TryDisposeRuntime(
        ParticipantState participant,
        ICollection<Exception> errors)
    {
        IGenericMindRuntime? runtime = participant.Runtime;
        participant.Runtime = null;
        if (runtime is null)
            return;
        try
        {
            runtime.Dispose();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private void ThrowIfCollectingTick()
    {
        if (_collectingTick)
        {
            throw new InvalidOperationException(
                "The runtime coordinator cannot be mutated or invoked recursively while collecting a joint tick.");
        }
    }

    private sealed class ParticipantState
    {
        public ParticipantState(
            GenericActorParticipantConfiguration configuration,
            ImmutableArray<int> alliedParticipantIds)
        {
            ParticipantId = configuration.ParticipantId;
            TeamId = configuration.TeamId;
            MindFactory = configuration.MindRuntimeFactory!;
            AlliedParticipantIds = alliedParticipantIds;
        }

        public int ParticipantId { get; }
        public int TeamId { get; }
        public IGenericMindRuntimeFactory MindFactory { get; }
        public ImmutableArray<int> AlliedParticipantIds { get; }
        public IGenericMindRuntime? Runtime { get; set; }
        public long FaultCount { get; set; }
        public bool Disqualified { get; set; }
    }

    private sealed class ActiveLifeState
    {
        public ActiveLifeState(GenericActorRuntimeStart start)
        {
            Start = start;
        }

        public GenericActorRuntimeStart Start { get; }

        /// <summary>
        /// The form this body carried in the tick's frozen observation, which
        /// is the form its synthetic <c>Wait</c> must come from. Captured
        /// during batch validation, before any runtime is touched.
        /// </summary>
        public string? CurrentFormId { get; set; }
    }
}
