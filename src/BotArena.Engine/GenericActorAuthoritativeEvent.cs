namespace BotArena.Engine;

/// <summary>
/// One unredacted, globally ordered authoritative match event. Observation
/// projectors apply its closed audience policy before allocating any
/// audience-local handles or ordinals.
/// </summary>
public sealed record GenericActorAuthoritativeEvent
{
    public GenericActorAuthoritativeEvent(
        string eventHandle,
        int tick,
        long globalOrdinal,
        int sourceOrdinal,
        GenericActorRuntimeObservation.EventKind kind,
        GenericActorRuntimeObservation.EventPayload unredactedPayload,
        Audience audience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventHandle);
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }
        if (globalOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(globalOrdinal));
        }
        if (sourceOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOrdinal));
        }
        ArgumentNullException.ThrowIfNull(unredactedPayload);
        ArgumentNullException.ThrowIfNull(audience);

        ValidatePayloadIdentityValues(unredactedPayload);
        if (!PayloadMatchesKind(kind, unredactedPayload))
        {
            throw new ArgumentException(
                $"Event kind '{kind}' does not match payload variant " +
                $"'{unredactedPayload.GetType().Name}'.",
                nameof(unredactedPayload));
        }

        EventHandle = eventHandle;
        Tick = tick;
        GlobalOrdinal = globalOrdinal;
        SourceOrdinal = sourceOrdinal;
        Kind = kind;
        UnredactedPayload = unredactedPayload;
        EventAudience = audience;
    }

    /// <summary>
    /// Compatibility constructor for hand-authored fixtures whose event-only
    /// ordinal and global fact ordinal are intentionally identical.
    /// Engine-produced matches use the explicit source-ordinal overload.
    /// </summary>
    public GenericActorAuthoritativeEvent(
        string eventHandle,
        int tick,
        long globalOrdinal,
        GenericActorRuntimeObservation.EventKind kind,
        GenericActorRuntimeObservation.EventPayload unredactedPayload,
        Audience audience)
        : this(
            eventHandle,
            tick,
            globalOrdinal,
            checked((int)globalOrdinal),
            kind,
            unredactedPayload,
            audience)
    {
    }

    public string EventHandle { get; }
    public int Tick { get; }
    public long GlobalOrdinal { get; }
    public int SourceOrdinal { get; }
    public GenericActorRuntimeObservation.EventKind Kind { get; }
    public GenericActorRuntimeObservation.EventPayload UnredactedPayload
    {
        get;
    }
    public Audience EventAudience { get; }

    /// <summary>Global ordering shared by every authoritative fact.</summary>
    public long Ordinal => GlobalOrdinal;

    /// <summary>Convenience alias for chronology containers.</summary>
    public GenericActorRuntimeObservation.EventPayload Payload =>
        UnredactedPayload;

    public abstract record Audience
    {
        private Audience()
        {
        }

        public sealed record Public : Audience;

        public sealed record Spatial(
            Position PrimaryPosition) : Audience;

        public sealed record TeamPrivate : Audience
        {
            public TeamPrivate(int teamId)
            {
                if (teamId < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(teamId));
                }

                TeamId = teamId;
            }

            public int TeamId { get; }
        }
    }

    private static bool PayloadMatchesKind(
        GenericActorRuntimeObservation.EventKind kind,
        GenericActorRuntimeObservation.EventPayload payload) =>
        kind switch
        {
            GenericActorRuntimeObservation.EventKind.Rotation =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .Rotation,
            GenericActorRuntimeObservation.EventKind.Movement =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .Movement,
            GenericActorRuntimeObservation.EventKind.MovementBlocked =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .MovementBlocked,
            GenericActorRuntimeObservation.EventKind.Attack =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .Attack,
            GenericActorRuntimeObservation.EventKind.Damage =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .Damage,
            GenericActorRuntimeObservation.EventKind.Destruction =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .Destruction,
            GenericActorRuntimeObservation.EventKind.LifeSpawned =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .LifeSpawned,
            GenericActorRuntimeObservation.EventKind.LifeRetired =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .LifeRetired,
            GenericActorRuntimeObservation.EventKind.RuntimeFault =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .RuntimeFault,
            GenericActorRuntimeObservation.EventKind
                    .ParticipantDisqualified =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .Participant,
            GenericActorRuntimeObservation.EventKind.LifecycleQueued
                or GenericActorRuntimeObservation.EventKind
                    .LifecycleCancelled
                or GenericActorRuntimeObservation.EventKind
                    .LifecycleCompleted =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .Lifecycle,
            GenericActorRuntimeObservation.EventKind
                    .FormTransitionStarted
                or GenericActorRuntimeObservation.EventKind
                    .FormTransitionCompleted
                or GenericActorRuntimeObservation.EventKind
                    .FormTransitionCancelled =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .FormTransition,
            GenericActorRuntimeObservation.EventKind.ScoreChanged =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .ScoreChanged,
            GenericActorRuntimeObservation.EventKind.ModeChanged =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .ModeChanged,
            GenericActorRuntimeObservation.EventKind
                    .LifecycleClockCancelled =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .LifecycleClockCancelled,
            GenericActorRuntimeObservation.EventKind.ProjectileDeflected =>
                payload is GenericActorRuntimeObservation.EventPayload
                    .ProjectileDeflected,
            _ => false,
        };

    private static void ValidatePayloadIdentityValues(
        GenericActorRuntimeObservation.EventPayload payload)
    {
        switch (payload)
        {
            case GenericActorRuntimeObservation.EventPayload.Attack attack:
                RequireNonnegative(
                    attack.ProjectileId,
                    nameof(attack.ProjectileId));
                break;
            case GenericActorRuntimeObservation.EventPayload.Damage damage:
                RequireNonnegative(
                    damage.SourceTeamId,
                    nameof(damage.SourceTeamId));
                RequireNonnegative(
                    damage.ProjectileId,
                    nameof(damage.ProjectileId));
                break;
            case GenericActorRuntimeObservation.EventPayload
                .ProjectileDeflected deflected:
                RequireNonnegative(
                    deflected.SourceTeamId,
                    nameof(deflected.SourceTeamId));
                RequireNonnegative(
                    deflected.ProjectileId,
                    nameof(deflected.ProjectileId));
                RequireNonnegative(
                    deflected.DeflectedProjectileId,
                    nameof(deflected.DeflectedProjectileId));
                break;
            case GenericActorRuntimeObservation.EventPayload.Destruction
                destruction:
                RequireNonnegative(
                    destruction.SourceTeamId,
                    nameof(destruction.SourceTeamId));
                RequireNonnegative(
                    destruction.ProjectileId,
                    nameof(destruction.ProjectileId));
                break;
            case GenericActorRuntimeObservation.EventPayload.LifeSpawned
                spawned:
                RequireNonnegative(
                    spawned.ParticipantId,
                    nameof(spawned.ParticipantId));
                break;
            case GenericActorRuntimeObservation.EventPayload.RuntimeFault
                runtimeFault:
                RequireNonnegative(
                    runtimeFault.Fault.ParticipantId,
                    nameof(runtimeFault.Fault.ParticipantId));
                break;
            case GenericActorRuntimeObservation.EventPayload.Participant
                participant:
                RequireNonnegative(
                    participant.ParticipantId,
                    nameof(participant.ParticipantId));
                RequireNonnegative(
                    participant.TeamId,
                    nameof(participant.TeamId));
                break;
            case GenericActorRuntimeObservation.EventPayload.Lifecycle
                lifecycle:
                RequireNonnegative(
                    lifecycle.TargetTeamId,
                    nameof(lifecycle.TargetTeamId));
                RequireNonnegative(
                    lifecycle.TargetUnitId,
                    nameof(lifecycle.TargetUnitId));
                break;
            case GenericActorRuntimeObservation.EventPayload.ScoreChanged
                score:
                RequireNonnegative(score.TeamId, nameof(score.TeamId));
                break;
            case GenericActorRuntimeObservation.EventPayload.ModeChanged
                {
                    State: GenericActorRuntimeObservation
                        .ModeObservationState.Frontline frontline,
                }:
                RequireNonnegative(
                    frontline.ClaimingTeamId,
                    nameof(frontline.ClaimingTeamId));
                break;
        }
    }

    private static void RequireNonnegative(
        int value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireNonnegative(
        long value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireNonnegative(
        int? value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void RequireNonnegative(
        long? value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
