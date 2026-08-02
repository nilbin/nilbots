using BotArena.Sdk;

internal enum OperationTruth { False, True, Unknown }
internal enum OperationPhase { Dormant, Prepare, Commit, Recover }
internal enum ParticipantResilience { Essential, Replaceable, Optional }
internal enum RecoveryKind { Success, Abort }

internal sealed record OperationCondition(
    string Fact,
    string Operator,
    int Value,
    string Zone,
    string Subject,
    int FreshnessTicks,
    string[] ClassIds);

internal sealed record OperationConditionGroup(
    OperationCondition[] All,
    OperationCondition[] Any);

internal sealed record OperationTask(
    string Id,
    ParticipantResilience Resilience,
    int Minimum,
    int[] CandidateUnitIds,
    string[] CandidateRoles,
    string[] CandidateClassIds,
    bool PermitsCarrying,
    bool RequiresCarrying,
    PositionIntent Position,
    string RoleOverride,
    string EngagementIntent,
    string SignatureIntent);

internal sealed record OperationBranch(
    string Id,
    OperationConditionGroup CommitWhen,
    OperationTask[] Tasks,
    OperationCondition[] SuccessAny,
    OperationCondition[] AbortAny,
    int DeadlineTicks);

internal sealed record OperationRecovery(
    int DeadlineTicks,
    OperationCondition[] CompleteAll,
    OperationTask[] OnSuccess,
    OperationTask[] OnAbort);

internal sealed record IntelligentOperationPlan(
    int Priority,
    string Id,
    int PrepareDeadlineTicks,
    int CooldownTicks,
    OperationConditionGroup PrepareWhen,
    OperationCondition[] PrepareAbortAny,
    OperationTask[] PrepareTasks,
    OperationBranch[] Branches,
    OperationRecovery Recovery);

internal sealed record OperationActor(
    int UnitId,
    string LifeKey,
    string ClassId,
    string BaselineRole,
    bool CarriesCore,
    Position Position);

internal sealed record OperationAssignment(
    string TaskId,
    int UnitId,
    string LifeKey);

internal sealed record OperationStateView(
    string OperationId,
    OperationPhase Phase,
    string? BranchId,
    RecoveryKind? RecoveryKind,
    int PhaseStartedTick,
    IReadOnlyList<OperationAssignment> Assignments);

internal sealed record OperationDirective(
    string OperationId,
    OperationPhase Phase,
    string? BranchId,
    OperationTask Task);

internal sealed record OperationTrace(
    int Tick,
    string OperationId,
    OperationPhase From,
    OperationPhase To,
    string Reason,
    string? BranchId);
