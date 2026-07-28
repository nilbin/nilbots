namespace BotArena.Sdk;

/// <summary>
/// Stable, participant-scoped host fault evidence. Raw exception and guest
/// diagnostic text are deliberately excluded.
/// </summary>
public sealed record GenericActorRuntimeFaultContext
{
    /// <summary>Creates stable host fault evidence.</summary>
    /// <param name="participantId">Participant charged with the fault.</param>
    /// <param name="actorId">Life whose runtime operation faulted.</param>
    /// <param name="stage">Runtime lifecycle stage that faulted.</param>
    /// <param name="faultCode">Stable, non-diagnostic fault category.</param>
    /// <param name="cumulativeFaultCount">
    /// Participant's accumulated fault count after this fault.
    /// </param>
    /// <param name="disqualificationTriggered">
    /// Whether this fault crossed the contract's disqualification threshold.
    /// </param>
    public GenericActorRuntimeFaultContext(
        int participantId,
        ActorIdentity actorId,
        FaultStage stage,
        string faultCode,
        long cumulativeFaultCount,
        bool disqualificationTriggered)
    {
        if (participantId < 0)
            throw new ArgumentOutOfRangeException(nameof(participantId));
        ArgumentNullException.ThrowIfNull(actorId);
        if (cumulativeFaultCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cumulativeFaultCount));
        }

        ParticipantId = participantId;
        ActorId = actorId;
        Stage = GenericActorDynamicValueRules.EnumValue(stage, nameof(stage));
        FaultCode = GenericActorDynamicValueRules.SemanticId(
            faultCode,
            nameof(faultCode));
        CumulativeFaultCount = cumulativeFaultCount;
        DisqualificationTriggered = disqualificationTriggered;
    }

    /// <summary>Participant charged with the fault.</summary>
    public int ParticipantId { get; }
    /// <summary>Life whose runtime operation faulted.</summary>
    public ActorIdentity ActorId { get; }
    /// <summary>Runtime lifecycle stage that faulted.</summary>
    public FaultStage Stage { get; }
    /// <summary>
    /// Stable fault category. Raw exception and guest diagnostic text are
    /// deliberately not exposed.
    /// </summary>
    public string FaultCode { get; }
    /// <summary>Participant fault count after applying this fault.</summary>
    public long CumulativeFaultCount { get; }
    /// <summary>Whether this fault triggered participant disqualification.</summary>
    public bool DisqualificationTriggered { get; }

    /// <summary>Host stage in which a generic actor runtime fault occurred.</summary>
    public enum FaultStage
    {
        /// <summary>Creating or acquiring the runtime instance.</summary>
        RuntimeCreate = 0,
        /// <summary>Starting a newly spawned body life.</summary>
        LifeStart = 1,
        /// <summary>Executing the bot's tick callback.</summary>
        TickExecution = 2,
        /// <summary>Validating the returned decision.</summary>
        DecisionValidation = 3,
    }

    /// <summary>Stable fault-code constants defined by the generic profile.</summary>
    public static class Codes
    {
        /// <summary>The host could not create the participant runtime.</summary>
        public const string RuntimeCreateFailed = "runtime-create-failed";
        /// <summary>A runtime instance was illegally reused for another life.</summary>
        public const string RuntimeInstanceReused = "runtime-instance-reused";
        /// <summary>The runtime failed while starting a new life.</summary>
        public const string LifeStartFailed = "life-start-failed";
        /// <summary>The runtime failed while executing a tick.</summary>
        public const string TickExecutionFailed = "tick-execution-failed";
        /// <summary>The runtime returned an invalid decision.</summary>
        public const string DecisionValidationFailed =
            "decision-validation-failed";
    }
}
