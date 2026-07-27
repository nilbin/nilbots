using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// One authoritative projectile transition. Ordered entered tiles are stored
/// directly; playback and datasets never reconstruct a trajectory.
/// </summary>
public sealed record GenericActorProjectileTraversal
{
    public GenericActorProjectileTraversal(
        int tick,
        long ordinal,
        TraversalPhase phase,
        TraversalTrigger trigger,
        long projectileId,
        int ownerParticipantId,
        int ownerTeamId,
        ActorIdentity ownerActorId,
        string attackProfileId,
        Position from,
        IReadOnlyList<Position> path,
        ProjectileHeading launchHeading,
        ProjectileHeading finalHeading,
        ShotProgram? shotProgram,
        TerminalDisposition terminal)
    {
        if (tick < 0)
            throw new ArgumentOutOfRangeException(nameof(tick));
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (!Enum.IsDefined(phase))
            throw new ArgumentOutOfRangeException(nameof(phase));
        if (!Enum.IsDefined(trigger))
            throw new ArgumentOutOfRangeException(nameof(trigger));
        if (projectileId < 0)
            throw new ArgumentOutOfRangeException(nameof(projectileId));
        if (ownerParticipantId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ownerParticipantId));
        }
        if (ownerTeamId < 0)
            throw new ArgumentOutOfRangeException(nameof(ownerTeamId));
        if (ownerActorId.TeamId != ownerTeamId)
        {
            throw new ArgumentException(
                "Projectile owner team and firing-life identity disagree.",
                nameof(ownerActorId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(attackProfileId);
        ArgumentNullException.ThrowIfNull(path);
        if (!Enum.IsDefined(launchHeading))
            throw new ArgumentOutOfRangeException(nameof(launchHeading));
        if (!Enum.IsDefined(finalHeading))
            throw new ArgumentOutOfRangeException(nameof(finalHeading));
        ArgumentNullException.ThrowIfNull(terminal);

        Position[] pathSnapshot = [.. path];
        if (from.X < 0
            || from.Y < 0
            || pathSnapshot.Any(position =>
                position.X < 0 || position.Y < 0))
        {
            throw new ArgumentException(
                "Projectile traversal positions cannot be negative.",
                nameof(path));
        }
        Position previous = from;
        foreach (Position position in pathSnapshot)
        {
            if (previous.ChebyshevDistance(position) != 1)
            {
                throw new ArgumentException(
                    "Projectile traversal paths must contain contiguous tiles.",
                    nameof(path));
            }
            previous = position;
        }
        if (pathSnapshot.Length != 0
            && ProjectileHeadingExtensions.Between(
                pathSnapshot.Length == 1
                    ? from
                    : pathSnapshot[^2],
                pathSnapshot[^1]) != finalHeading)
        {
            throw new ArgumentException(
                "Final projectile heading must match the final traversed edge.",
                nameof(finalHeading));
        }
        if (pathSnapshot.Length == 0
            && terminal is TerminalDisposition.Retained)
        {
            throw new ArgumentException(
                "A retained projectile transition must enter at least one tile.",
                nameof(path));
        }
        bool phaseMatchesTrigger = (phase, trigger) switch
        {
            (TraversalPhase.TickStart,
                TraversalTrigger.LifecyclePlacement) => true,
            (TraversalPhase.Resolution,
                TraversalTrigger.MovementContact
                    or TraversalTrigger.ScheduledAdvance
                    or TraversalTrigger.AttackLaunch
                    or TraversalTrigger.ParticipantDisqualification) => true,
            _ => false,
        };
        if (!phaseMatchesTrigger)
        {
            throw new ArgumentException(
                "Projectile traversal phase and trigger disagree.",
                nameof(trigger));
        }
        bool terminalMatchesTrigger = (trigger, terminal) switch
        {
            (TraversalTrigger.LifecyclePlacement,
                TerminalDisposition.LifecyclePlacementPurge) => true,
            (TraversalTrigger.MovementContact,
                TerminalDisposition.MovementContact) => true,
            (TraversalTrigger.ParticipantDisqualification,
                TerminalDisposition.ParticipantDisqualification) => true,
            (TraversalTrigger.ScheduledAdvance,
                TerminalDisposition.LifecyclePlacementPurge
                    or TerminalDisposition.MovementContact
                    or TerminalDisposition.ParticipantDisqualification) =>
                false,
            (TraversalTrigger.AttackLaunch,
                TerminalDisposition.LifecyclePlacementPurge
                    or TerminalDisposition.MovementContact
                    or TerminalDisposition.ParticipantDisqualification) =>
                false,
            _ => trigger is TraversalTrigger.ScheduledAdvance
                or TraversalTrigger.AttackLaunch,
        };
        if (!terminalMatchesTrigger)
        {
            throw new ArgumentException(
                "Projectile traversal trigger and terminal disposition disagree.",
                nameof(terminal));
        }
        bool terminalShapeIsValid = terminal switch
        {
            TerminalDisposition.ActorContact =>
                pathSnapshot.Length != 0,
            TerminalDisposition.MovementContact =>
                pathSnapshot.Length == 0,
            TerminalDisposition.LifecyclePlacementPurge purge =>
                pathSnapshot.Length == 0 && purge.Position == from,
            TerminalDisposition.ParticipantDisqualification value =>
                pathSnapshot.Length == 0 && value.ParticipantId >= 0,
            _ => true,
        };
        if (!terminalShapeIsValid)
        {
            throw new ArgumentException(
                "Projectile traversal terminal facts contradict their path.",
                nameof(terminal));
        }

        Tick = tick;
        GlobalOrdinal = ordinal;
        Phase = phase;
        Trigger = trigger;
        ProjectileId = projectileId;
        OwnerParticipantId = ownerParticipantId;
        OwnerTeamId = ownerTeamId;
        OwnerActorId = ownerActorId;
        AttackProfileId = attackProfileId;
        From = from;
        Path = pathSnapshot.ToImmutableArray();
        LaunchHeading = launchHeading;
        FinalHeading = finalHeading;
        ShotProgram = shotProgram;
        Terminal = terminal;
    }

    public int Tick { get; }
    public long GlobalOrdinal { get; }
    public long Ordinal => GlobalOrdinal;
    public TraversalPhase Phase { get; }
    public TraversalTrigger Trigger { get; }
    public long ProjectileId { get; }
    public int OwnerParticipantId { get; }
    public int OwnerTeamId { get; }
    public ActorIdentity OwnerActorId { get; }
    public string AttackProfileId { get; }
    public Position From { get; }
    public ImmutableArray<Position> Path { get; }
    public ProjectileHeading LaunchHeading { get; }
    public ProjectileHeading FinalHeading { get; }
    public ShotProgram? ShotProgram { get; }
    public TerminalDisposition Terminal { get; }

    public enum TraversalPhase
    {
        TickStart = 0,
        Resolution = 1,
    }

    public enum TraversalTrigger
    {
        LifecyclePlacement = 0,
        MovementContact = 1,
        ScheduledAdvance = 2,
        AttackLaunch = 3,
        ParticipantDisqualification = 4,
    }

    public abstract record TerminalDisposition
    {
        private TerminalDisposition()
        {
        }

        public sealed record Retained : TerminalDisposition;
        public sealed record WallOrPathExhausted : TerminalDisposition;
        public sealed record RangeExhausted : TerminalDisposition;

        public sealed record ActorContact(
            ActorIdentity TargetActorId,
            bool AppliedDamage) : TerminalDisposition;

        public sealed record MovementContact(
            ActorIdentity TargetActorId,
            bool AppliedDamage) : TerminalDisposition;

        public sealed record LifecyclePlacementPurge(
            Position Position) : TerminalDisposition;

        public sealed record ParticipantDisqualification(
            int ParticipantId) : TerminalDisposition;
    }
}
