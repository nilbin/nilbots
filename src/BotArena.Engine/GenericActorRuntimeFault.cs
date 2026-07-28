namespace BotArena.Engine;

/// <summary>Stable participant-scoped evidence for one runtime fault.</summary>
public sealed record GenericActorRuntimeFault(
    int ParticipantId,
    ActorIdentity ActorId,
    GenericActorRuntimeFault.FaultStage Stage,
    string FaultCode,
    long CumulativeFaultCount,
    bool DisqualificationTriggered)
{
    public enum FaultStage
    {
        RuntimeCreate = 0,
        LifeStart = 1,
        TickExecution = 2,
        DecisionValidation = 3,
    }
}
