using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Exact runtime input, raw reply and admission verdicts for ONE SUBMITTED
/// PARTICIPANT in one joint tick under the mind profile
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §5.1).
/// <para>
/// This is the chronology's mind-era replacement for N per-life
/// <see cref="GenericActorMatchActorTurn"/> records: the union-once observation
/// is stored ONCE per participant per tick instead of once per body, which is
/// the ~7x cut on the chronology term and the ~3x cut on the document (§5.2).
/// The per-body resolutions the format still owes — every own live body's
/// accepted action, including the bodies the mind never named — travel on the
/// frame's actor turns and are projected into the mind turn's
/// <c>resolutions[]</c> at document time.
/// </para>
/// <para>
/// The distinction this record exists to keep sharp is
/// engine-refused-at-runtime versus document-malformed (§5.3). A
/// <see cref="GenericMindCommandOutcome.Rejected"/> command naming a dead body
/// is legitimate, recorded and replayable; a shape the engine could not have
/// produced — two commands for one body, a resolution set that is not the
/// participant's own live bodies, a fuel budget off the formula — is refused.
/// </para>
/// </summary>
public sealed record GenericActorMatchMindTurn
{
    public GenericActorMatchMindTurn(
        int tick,
        int participantId,
        int teamId,
        long tickFuelBudget,
        int liveOwnBodyCount,
        GenericMindRuntimeObservation observation,
        IReadOnlyCollection<GenericMindCommandResolution> commands,
        IReadOnlyCollection<ActorIdentity> resolvedBodies,
        IReadOnlyCollection<GenericMindDeclaredIntent> rejectedIntents,
        GenericMindRuntimeFault? runtimeFault,
        string? debugMessage = null)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        if (participantId < 0)
            throw new ArgumentOutOfRangeException(nameof(participantId));
        if (teamId < 0)
            throw new ArgumentOutOfRangeException(nameof(teamId));
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(resolvedBodies);
        ArgumentNullException.ThrowIfNull(rejectedIntents);

        if (observation.Tick != tick
            || observation.ParticipantId != participantId
            || observation.TeamId != teamId)
        {
            throw new ArgumentException(
                "Mind-turn observation identity does not match the turn.",
                nameof(observation));
        }

        // The budget is a pure function of authoritative tick-start state, so
        // a document whose budget is off the formula could not have been
        // written by this engine (§4.2).
        if (tickFuelBudget != GenericMindTickBudget.TickFuel(
                liveOwnBodyCount switch
                {
                    < 0 => throw new ArgumentOutOfRangeException(
                        nameof(liveOwnBodyCount)),
                    _ => liveOwnBodyCount,
                }))
        {
            throw new ArgumentException(
                "Mind-turn fuel budget must be exactly the live-body formula.",
                nameof(tickFuelBudget));
        }

        ActorIdentity[] bodySnapshot = [.. resolvedBodies];
        if (bodySnapshot.Any(body => body is null)
            || bodySnapshot.Distinct().Count() != bodySnapshot.Length)
        {
            throw new ArgumentException(
                "Mind-turn resolved bodies must be non-null and unique.",
                nameof(resolvedBodies));
        }
        if (bodySnapshot.Length != liveOwnBodyCount
            || observation.Bodies.Length != liveOwnBodyCount)
        {
            throw new ArgumentException(
                "Mind-turn resolutions must cover exactly the participant's own live bodies.",
                nameof(resolvedBodies));
        }
        if (!bodySnapshot.Order().SequenceEqual(
                observation.Bodies
                    .Select(body => body.ActorId)
                    .Order()))
        {
            throw new ArgumentException(
                "Mind-turn resolved bodies must exactly match the observed bodies.",
                nameof(resolvedBodies));
        }
        if (bodySnapshot.Any(body => body.TeamId != teamId))
        {
            throw new ArgumentException(
                "A mind can only resolve bodies on its own scoring team.",
                nameof(resolvedBodies));
        }

        GenericMindCommandResolution[] commandSnapshot = [.. commands];
        ValidateCommands(
            commandSnapshot,
            bodySnapshot,
            runtimeFault is not null);
        GenericMindDeclaredIntent[] intentSnapshot = [.. rejectedIntents];
        ValidateRejectedIntents(intentSnapshot);
        ValidateRuntimeFault(participantId, teamId, runtimeFault);
        if (debugMessage is not null)
        {
            if (runtimeFault is not null)
            {
                throw new ArgumentException(
                    "A faulted mind turn carries no diagnostic text: the reply "
                    + "that would have carried it never parsed.",
                    nameof(debugMessage));
            }
            if (System.Text.Encoding.UTF8.GetByteCount(debugMessage)
                > MaxDebugMessageBytes)
            {
                throw new ArgumentException(
                    "A mind turn's diagnostic text is bounded at "
                    + $"{MaxDebugMessageBytes} UTF-8 bytes.",
                    nameof(debugMessage));
            }
        }

        Tick = tick;
        ParticipantId = participantId;
        TeamId = teamId;
        TickFuelBudget = tickFuelBudget;
        LiveOwnBodyCount = liveOwnBodyCount;
        Observation = observation;
        Commands = commandSnapshot.ToImmutableArray();
        ResolvedBodies = bodySnapshot.Order().ToImmutableArray();
        RejectedIntents = intentSnapshot.ToImmutableArray();
        RuntimeFault = runtimeFault;
        DebugMessage = debugMessage;
    }

    /// <summary>
    /// The same 4 KiB text bound the per-life decision carries. A mind reasons
    /// once per tick rather than once per body, so its diagnostics are not nine
    /// times larger than a per-life bot's — they are one ninth.
    /// </summary>
    public const int MaxDebugMessageBytes = 4096;

    public int Tick { get; }
    public int ParticipantId { get; }
    public int TeamId { get; }

    /// <summary><c>250M + 200M x liveOwnBodies</c> (§4.2).</summary>
    public long TickFuelBudget { get; }

    /// <summary>Authoritative tick-start own live body count.</summary>
    public int LiveOwnBodyCount { get; }

    /// <summary>
    /// The exact immutable observation instance delivered to the runtime, with
    /// the team-shared union carried ONCE.
    /// </summary>
    public GenericMindRuntimeObservation Observation { get; }

    /// <summary>
    /// The commands the mind wrote, in submission order, each with the host's
    /// admission verdict. Shorter than <see cref="ResolvedBodies"/> whenever
    /// the mind left bodies on the pre-filled Wait, and it may name bodies
    /// that no longer exist. Both are recorded; neither is elided.
    /// </summary>
    public ImmutableArray<GenericMindCommandResolution> Commands { get; }

    /// <summary>
    /// Every own live body this tick, canonical order. The mind-era equivalent
    /// of the per-life "exactly those keys" rule, living where it belongs — in
    /// the document validator rather than in the player's face.
    /// </summary>
    public ImmutableArray<ActorIdentity> ResolvedBodies { get; }

    /// <summary>RESERVED (§11.3): recorded and Rejected, never Faulted.</summary>
    public ImmutableArray<GenericMindDeclaredIntent> RejectedIntents { get; }

    /// <summary>
    /// The participant-scoped fault, if the mind trapped. Its actor identity is
    /// null when the participant held no live body — a mind that faults on a
    /// tick it owns nothing still faults, and the fact must be visible.
    /// </summary>
    public GenericMindRuntimeFault? RuntimeFault { get; }

    /// <summary>
    /// The mind's own diagnostic text for this tick, or <see langword="null"/>.
    /// The MIND's diagnostics home: a mind decides once per tick over the whole
    /// army, so the sentence that explains a tick belongs to nobody's command —
    /// and on a tick with no live bodies there is no command to hang it on at
    /// all.
    /// </summary>
    public string? DebugMessage { get; }

    /// <summary>
    /// Re-derives the tag each body carries after this turn, given the tags
    /// they carried before it. Only an ACCEPTED command can move a tag: a
    /// Rejected command named a body the mind does not own or that is not
    /// live, and a faulted turn's whole decision map was discarded.
    /// </summary>
    public void ApplyRoleTags(
        IDictionary<ActorIdentity, string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (RuntimeFault is not null)
            return;
        foreach (GenericMindCommandResolution resolution in Commands)
        {
            if (resolution.Outcome != GenericMindCommandOutcome.Accepted)
                continue;
            var actorId = new ActorIdentity(
                TeamId,
                resolution.Command.UnitId,
                resolution.Command.LifeId);
            string? applied = GenericMindRoleTag.Apply(
                tags.TryGetValue(actorId, out string? remembered)
                    ? remembered
                    : null,
                resolution.Command.RoleTag);
            if (applied is null)
                tags.Remove(actorId);
            else
                tags[actorId] = applied;
        }
    }

    internal void ValidateAgainst(GenericActorMatchDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ActorResolvedMatchDefinition definition = descriptor.Definition;
        if (definition.CapabilityVersions.ObservationSchemaVersion
            != Observation.SchemaVersion)
        {
            throw InvalidEvidence(
                "mind observation schema version does not match the descriptor");
        }
        if (!definition.CapabilityVersions.IsMindProfile)
        {
            throw InvalidEvidence(
                "a mind turn cannot appear on a per-life contract profile");
        }
        PublicParticipant? participant =
            definition.Topology.Participants.SingleOrDefault(
                candidate => candidate.ParticipantId == ParticipantId);
        if (participant is null || participant.TeamId != TeamId)
        {
            throw InvalidEvidence(
                "mind participant and team do not match topology ownership");
        }

        var owned = definition.Topology.UnitSlots
            .Where(slot => slot.ControllerParticipantId == ParticipantId)
            .Select(slot => (slot.TeamId, slot.UnitId))
            .ToHashSet();
        if (ResolvedBodies.Any(body =>
                !owned.Contains((body.TeamId, body.UnitId))))
        {
            throw InvalidEvidence(
                "a mind resolved a body it does not own");
        }
        if (!Observation.Slots
            .Select(slot => (slot.TeamId, slot.UnitId))
            .Order()
            .SequenceEqual(owned.Order()))
        {
            throw InvalidEvidence(
                "the published slot table is not exactly the participant's own slots");
        }
        if (Observation.Allies.Any(ally =>
                ally.ActorId.TeamId != TeamId
                || owned.Contains(
                    (ally.ActorId.TeamId, ally.ActorId.UnitId))))
        {
            throw InvalidEvidence(
                "allied bodies must be on this team and must not be this mind's own");
        }
        if (!Observation.AlliedIntents.IsEmpty)
        {
            throw InvalidEvidence(
                "allied intents are reserved and must be empty");
        }
    }

    private static void ValidateCommands(
        IReadOnlyList<GenericMindCommandResolution> commands,
        IReadOnlyCollection<ActorIdentity> resolvedBodies,
        bool faulted)
    {
        var keys = new HashSet<(int UnitId, int LifeId)>();
        var live = resolvedBodies
            .Select(body => (body.UnitId, body.LifeId))
            .ToHashSet();
        foreach (GenericMindCommandResolution resolution in commands)
        {
            if (resolution is null || resolution.Command is null)
            {
                throw new ArgumentException(
                    "Mind-turn commands must be non-null.",
                    nameof(commands));
            }
            GenericMindCommand command = resolution.Command;
            if (!keys.Add((command.UnitId, command.LifeId)) && !faulted)
            {
                // Two entries for one body is a contradiction the host cannot
                // resolve, and on a healthy turn the engine never wrote one:
                // it faults the decision map at submission (§5.3). The one
                // history in which a duplicate pair legitimately appears is
                // the FAULTED turn that the duplicate itself caused, where the
                // raw submission is preserved verbatim as evidence and every
                // entry is Rejected — nothing was routed, so the document
                // claims nothing about which of the two won.
                throw new ArgumentException(
                    "Mind-turn commands cannot name the same body twice.",
                    nameof(commands));
            }
            if (!Enum.IsDefined(resolution.Outcome)
                || string.IsNullOrWhiteSpace(command.ActionId)
                || command.ActionCode < 0
                || command.Arguments.IsDefault
                || command.Arguments.Any(argument => argument is null))
            {
                throw new ArgumentException(
                    "Mind-turn command evidence is malformed.",
                    nameof(commands));
            }
            if (command.RoleTag is not null
                && !GenericMindRoleTag.IsValid(command.RoleTag))
            {
                throw new ArgumentException(
                    "A role tag must be a canonical kebab label within the 24-byte cap.",
                    nameof(commands));
            }
            bool namesLiveOwnBody =
                live.Contains((command.UnitId, command.LifeId));
            if (faulted)
            {
                // A trapped mind loses its whole decision map; nothing it sent
                // may be recorded as routed.
                if (resolution.Outcome != GenericMindCommandOutcome.Rejected)
                {
                    throw new ArgumentException(
                        "A faulted mind turn cannot record an accepted command.",
                        nameof(commands));
                }
                continue;
            }
            if (resolution.Outcome == GenericMindCommandOutcome.Accepted
                != namesLiveOwnBody)
            {
                throw new ArgumentException(
                    resolution.Outcome == GenericMindCommandOutcome.Accepted
                        ? "An accepted command must name an own live body."
                        : "A rejected command must name a body that is not an own live body.",
                    nameof(commands));
            }
        }
    }

    private static void ValidateRejectedIntents(
        IReadOnlyList<GenericMindDeclaredIntent> intents)
    {
        if (intents.Count > GenericMindContractReservations
            .MaxDeclaredIntentsPerTick)
        {
            throw new ArgumentException(
                "A mind may declare at most eight intents per tick.",
                nameof(intents));
        }
        foreach (GenericMindDeclaredIntent intent in intents)
        {
            if (intent is null
                || string.IsNullOrEmpty(intent.TagId)
                || System.Text.Encoding.UTF8.GetByteCount(intent.TagId)
                    > GenericMindContractReservations.MaxIntentTagUtf8Bytes)
            {
                throw new ArgumentException(
                    "Mind-turn declared intent evidence is malformed.",
                    nameof(intents));
            }
        }
    }

    private static void ValidateRuntimeFault(
        int participantId,
        int teamId,
        GenericMindRuntimeFault? fault)
    {
        if (fault is null)
            return;
        if (fault.ParticipantId != participantId
            || fault.TeamId != teamId
            || !Enum.IsDefined(fault.Stage)
            || string.IsNullOrWhiteSpace(fault.FaultCode)
            || fault.CumulativeFaultCount <= 0
            || (fault.ActorId is not null && fault.ActorId.TeamId != teamId))
        {
            throw new ArgumentException(
                "Mind runtime fault evidence does not match its turn.",
                nameof(fault));
        }
    }

    private ArgumentException InvalidEvidence(string reason) =>
        new(
            $"Mind-turn evidence for participant {ParticipantId} is invalid: {reason}.",
            nameof(Observation));
}
