using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Complete immutable world and public mode state at one chronology boundary.
/// It contains enough information to render or train without replaying rules.
/// </summary>
public sealed class GenericActorWorldSnapshot
{
    public GenericActorWorldSnapshot(
        ActorResolvedMatchDefinition definition,
        int nextTick,
        long nextProjectileId,
        IReadOnlyCollection<
            GenericActorRuntimeObservation.ObservedParticipantStatus>
            participants,
        IReadOnlyCollection<SlotSnapshot> slots,
        IReadOnlyCollection<LifeSnapshot> activeLives,
        IReadOnlyCollection<SplitReplicationReservation>
            pendingReplications,
        IReadOnlyCollection<ProjectileSnapshot> projectiles,
        GenericActorRuntimeObservation.ScoreboardState scoreboard,
        GenericActorRuntimeObservation.ModeObservationState mode)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (nextTick < 0)
            throw new ArgumentOutOfRangeException(nameof(nextTick));
        if (nextProjectileId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextProjectileId));
        }
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(activeLives);
        ArgumentNullException.ThrowIfNull(pendingReplications);
        ArgumentNullException.ThrowIfNull(projectiles);
        ArgumentNullException.ThrowIfNull(scoreboard);
        ArgumentNullException.ThrowIfNull(mode);

        GenericActorRuntimeObservation.ObservedParticipantStatus[]
            participantSnapshot = [.. participants];
        SlotSnapshot[] slotSnapshot = [.. slots];
        LifeSnapshot[] lifeSnapshot = [.. activeLives];
        SplitReplicationReservation[] replicationSnapshot =
            [.. pendingReplications];
        ProjectileSnapshot[] projectileSnapshot = [.. projectiles];

        ValidateParticipants(definition, participantSnapshot);
        ValidateSlots(definition, slotSnapshot);
        ValidateSlotStates(definition, nextTick, slotSnapshot);
        ValidateLives(
            definition,
            nextTick,
            slotSnapshot,
            lifeSnapshot);
        ValidateReplications(
            definition,
            slotSnapshot,
            lifeSnapshot,
            replicationSnapshot);
        ValidateProjectiles(
            definition,
            slotSnapshot,
            nextTick,
            nextProjectileId,
            projectileSnapshot);
        ValidateScoreboard(definition, scoreboard);
        ValidateParticipantEligibility(
            definition,
            participantSnapshot,
            slotSnapshot,
            lifeSnapshot,
            replicationSnapshot,
            projectileSnapshot,
            scoreboard);
        if (!ModeMatchesDefinition(definition.Rules.GameMode, mode))
        {
            throw new ArgumentException(
                "Mode state does not match the resolved game mode.",
                nameof(mode));
        }

        MatchContractFingerprint =
            ActorContractFingerprint.ComputeMatch(definition);
        NextTick = nextTick;
        NextProjectileId = nextProjectileId;
        Participants = participantSnapshot
            .OrderBy(value => value.ParticipantId)
            .ToImmutableArray();
        Slots = slotSnapshot
            .OrderBy(value => value.TeamId)
            .ThenBy(value => value.UnitId)
            .ToImmutableArray();
        ActiveLives = lifeSnapshot
            .OrderBy(value => value.ActorId)
            .ToImmutableArray();
        PendingReplications = replicationSnapshot
            .OrderBy(value => value.SourceActorId)
            .ThenBy(value => value.TransitionId, StringComparer.Ordinal)
            .ThenBy(value => value.OperationId, StringComparer.Ordinal)
            .ToImmutableArray();
        Projectiles = projectileSnapshot
            .OrderBy(value => value.ProjectileId)
            .ToImmutableArray();
        Scoreboard = scoreboard;
        Mode = mode;
    }

    public string MatchContractFingerprint { get; }
    public int NextTick { get; }
    public long NextProjectileId { get; }
    public ImmutableArray<
        GenericActorRuntimeObservation.ObservedParticipantStatus>
        Participants { get; }
    public ImmutableArray<SlotSnapshot> Slots { get; }
    public ImmutableArray<LifeSnapshot> ActiveLives { get; }
    public ImmutableArray<SplitReplicationReservation> PendingReplications
    {
        get;
    }
    public ImmutableArray<ProjectileSnapshot> Projectiles { get; }
    public GenericActorRuntimeObservation.ScoreboardState Scoreboard { get; }
    public GenericActorRuntimeObservation.ModeObservationState Mode { get; }

    public sealed record SlotSnapshot
    {
        public SlotSnapshot(
            int teamId,
            int unitId,
            int participantId,
            int nextLifeId,
            GenericActorRuntimeObservation.UnitSlotState state,
            ActorIdentity? pendingParentActorId,
            SplitReplicationReservation? splitReservation)
        {
            if (teamId < 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            if (unitId < 0)
                throw new ArgumentOutOfRangeException(nameof(unitId));
            if (participantId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(participantId));
            }
            if (nextLifeId < 0)
                throw new ArgumentOutOfRangeException(nameof(nextLifeId));
            ArgumentNullException.ThrowIfNull(state);
            if (state is GenericActorRuntimeObservation.UnitSlotState.Active
                    active
                && (active.ActorId.TeamId != teamId
                    || active.ActorId.UnitId != unitId
                    || active.ActorId.LifeId != nextLifeId - 1))
            {
                throw new ArgumentException(
                    "Active slot state and stable-slot identity disagree.",
                    nameof(state));
            }
            if (splitReservation is not null
                && state is not GenericActorRuntimeObservation.UnitSlotState
                    .ReplicationPending)
            {
                throw new ArgumentException(
                    "Only a replication-pending slot may retain a Split reservation.",
                    nameof(splitReservation));
            }
            if (state is GenericActorRuntimeObservation.UnitSlotState
                    .ReplicationPending
                && splitReservation is null)
            {
                throw new ArgumentException(
                    "A replication-pending slot must retain its Split reservation.",
                    nameof(splitReservation));
            }
            if (pendingParentActorId is not null
                && state is not GenericActorRuntimeObservation.UnitSlotState
                    .AutomaticReturnPending)
            {
                throw new ArgumentException(
                    "Only an automatic-return clock may retain a parent life.",
                    nameof(pendingParentActorId));
            }

            TeamId = teamId;
            UnitId = unitId;
            ParticipantId = participantId;
            NextLifeId = nextLifeId;
            State = state;
            PendingParentActorId = pendingParentActorId;
            SplitReservation = splitReservation;
        }

        public int TeamId { get; }
        public int UnitId { get; }
        public int ParticipantId { get; }
        public int NextLifeId { get; }
        public GenericActorRuntimeObservation.UnitSlotState State { get; }
        public ActorIdentity? PendingParentActorId { get; }
        public SplitReplicationReservation? SplitReservation { get; }
    }

    public sealed record LifeSnapshot
    {
        public LifeSnapshot(
            ActorIdentity actorId,
            int participantId,
            int generation,
            string formId,
            Position position,
            Direction facing,
            int health,
            int cooldown,
            int? energy,
            int spawnedAtTick,
            GenericActorRuntimeStart.SpawnReason spawnReason,
            ActorIdentity? parentActorId,
            string? sourceTransitionId,
            string? sourceOperationId,
            GenericActorRuntimeActionResolution? previousActionResolution,
            GenericActorRuntimeObservation.PendingSameLifeTransition?
                pendingSameLifeTransition)
        {
            if (participantId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(participantId));
            }
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation));
            ArgumentException.ThrowIfNullOrWhiteSpace(formId);
            if (!Enum.IsDefined(facing))
                throw new ArgumentOutOfRangeException(nameof(facing));
            if (health < 0)
                throw new ArgumentOutOfRangeException(nameof(health));
            if (cooldown < 0)
                throw new ArgumentOutOfRangeException(nameof(cooldown));
            if (energy < 0)
                throw new ArgumentOutOfRangeException(nameof(energy));
            if (spawnedAtTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spawnedAtTick));
            }
            if (!Enum.IsDefined(spawnReason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spawnReason));
            }

            ActorId = actorId;
            ParticipantId = participantId;
            Generation = generation;
            FormId = formId;
            Position = position;
            Facing = facing;
            Health = health;
            Cooldown = cooldown;
            Energy = energy;
            SpawnedAtTick = spawnedAtTick;
            SpawnReason = spawnReason;
            ParentActorId = parentActorId;
            SourceTransitionId = sourceTransitionId;
            SourceOperationId = sourceOperationId;
            PreviousActionResolution = previousActionResolution;
            PendingSameLifeTransition = pendingSameLifeTransition;
        }

        public ActorIdentity ActorId { get; }
        public int ParticipantId { get; }
        public int Generation { get; }
        public string FormId { get; }
        public Position Position { get; }
        public Direction Facing { get; }
        public int Health { get; }
        public int Cooldown { get; }
        public int? Energy { get; }
        public int SpawnedAtTick { get; }
        public GenericActorRuntimeStart.SpawnReason SpawnReason { get; }
        public ActorIdentity? ParentActorId { get; }
        public string? SourceTransitionId { get; }
        public string? SourceOperationId { get; }
        public GenericActorRuntimeActionResolution?
            PreviousActionResolution { get; }
        public GenericActorRuntimeObservation.PendingSameLifeTransition?
            PendingSameLifeTransition { get; }
    }

    public sealed record ProjectileSnapshot
    {
        public ProjectileSnapshot(
            long projectileId,
            int ownerParticipantId,
            int ownerTeamId,
            ActorIdentity ownerActorId,
            string attackProfileId,
            int spawnedAtTick,
            Position origin,
            Position position,
            ProjectileHeading launchHeading,
            ProjectileHeading heading,
            ShotProgram? shotProgram,
            IReadOnlyList<Position> committedPath,
            int nextPathIndex,
            int remainingTiles,
            int ticksUntilAdvance)
        {
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
                    "Projectile owner team and actor identity disagree.",
                    nameof(ownerActorId));
            }
            ArgumentException.ThrowIfNullOrWhiteSpace(attackProfileId);
            if (spawnedAtTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spawnedAtTick));
            }
            if (!Enum.IsDefined(launchHeading))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(launchHeading));
            }
            if (!Enum.IsDefined(heading))
                throw new ArgumentOutOfRangeException(nameof(heading));
            ArgumentNullException.ThrowIfNull(committedPath);
            Position[] pathSnapshot = [.. committedPath];
            if (nextPathIndex < 0 || nextPathIndex > pathSnapshot.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nextPathIndex));
            }
            if (remainingTiles < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingTiles));
            }
            if (ticksUntilAdvance <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ticksUntilAdvance));
            }

            ProjectileId = projectileId;
            OwnerParticipantId = ownerParticipantId;
            OwnerTeamId = ownerTeamId;
            OwnerActorId = ownerActorId;
            AttackProfileId = attackProfileId;
            SpawnedAtTick = spawnedAtTick;
            Origin = origin;
            Position = position;
            LaunchHeading = launchHeading;
            Heading = heading;
            ShotProgram = shotProgram;
            CommittedPath = pathSnapshot.ToImmutableArray();
            NextPathIndex = nextPathIndex;
            RemainingTiles = remainingTiles;
            TicksUntilAdvance = ticksUntilAdvance;
        }

        public long ProjectileId { get; }
        public int OwnerParticipantId { get; }
        public int OwnerTeamId { get; }
        public ActorIdentity OwnerActorId { get; }
        public string AttackProfileId { get; }
        public int SpawnedAtTick { get; }
        public Position Origin { get; }
        public Position Position { get; }
        public ProjectileHeading LaunchHeading { get; }
        public ProjectileHeading Heading { get; }
        public ShotProgram? ShotProgram { get; }
        public ImmutableArray<Position> CommittedPath { get; }
        public int NextPathIndex { get; }
        public int RemainingTiles { get; }
        public int TicksUntilAdvance { get; }
    }

    private static void ValidateParticipants(
        ActorResolvedMatchDefinition definition,
        IReadOnlyList<
            GenericActorRuntimeObservation.ObservedParticipantStatus>
            participants)
    {
        if (participants.Any(value => value is null)
            || participants.Select(value => value.ParticipantId)
                .Distinct().Count() != participants.Count)
        {
            throw new ArgumentException(
                "World participant states must be non-null and unique.",
                nameof(participants));
        }
        Dictionary<int, int> expected = definition.Topology.Participants
            .ToDictionary(value => value.ParticipantId, value => value.TeamId);
        if (participants.Count != expected.Count
            || participants.Any(value =>
                value.ParticipantId < 0
                || value.TeamId < 0
                || value.RuntimeFaultCount < 0
                || !expected.TryGetValue(
                    value.ParticipantId,
                    out int teamId)
                || teamId != value.TeamId))
        {
            throw new ArgumentException(
                "World participant states must exactly match match topology.",
                nameof(participants));
        }
    }

    private static void ValidateSlots(
        ActorResolvedMatchDefinition definition,
        IReadOnlyList<SlotSnapshot> slots)
    {
        if (slots.Any(value => value is null)
            || slots.Select(value => (value.TeamId, value.UnitId))
                .Distinct().Count() != slots.Count)
        {
            throw new ArgumentException(
                "World slot snapshots must be non-null and unique.",
                nameof(slots));
        }
        Dictionary<(int TeamId, int UnitId), int> expected =
            definition.Topology.UnitSlots.ToDictionary(
                value => (value.TeamId, value.UnitId),
                value => value.ControllerParticipantId);
        if (slots.Count != expected.Count
            || slots.Any(value =>
                !expected.TryGetValue(
                    (value.TeamId, value.UnitId),
                    out int participantId)
                || participantId != value.ParticipantId))
        {
            throw new ArgumentException(
                "World slots must exactly match match topology.",
                nameof(slots));
        }
    }

    private static void ValidateLives(
        ActorResolvedMatchDefinition definition,
        int nextTick,
        IReadOnlyList<SlotSnapshot> slots,
        IReadOnlyList<LifeSnapshot> lives)
    {
        if (lives.Any(value => value is null)
            || lives.Select(value => value.ActorId).Distinct().Count()
                != lives.Count)
        {
            throw new ArgumentException(
                "Active lives must be non-null and identity-unique.",
                nameof(lives));
        }
        Dictionary<(int TeamId, int UnitId), SlotSnapshot> slotsById =
            slots.ToDictionary(value => (value.TeamId, value.UnitId));
        Dictionary<string, ActorFormDefinition> forms =
            definition.Rules.Forms.ToDictionary(
                value => value.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorAttackProfileDefinition> attacks =
            definition.Rules.AttackProfiles.ToDictionary(
                value => value.Id,
                StringComparer.Ordinal);
        Dictionary<(int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments =
            definition.LifecycleAssignments.ToDictionary(
                value => (value.TeamId, value.UnitId));
        var occupiedPositions = new HashSet<Position>();
        foreach (LifeSnapshot life in lives)
        {
            forms.TryGetValue(
                life.FormId,
                out ActorFormDefinition? form);
            if (!slotsById.TryGetValue(
                    (life.ActorId.TeamId, life.ActorId.UnitId),
                    out SlotSnapshot? slot)
                || slot.ParticipantId != life.ParticipantId
                || slot.State is not
                    GenericActorRuntimeObservation.UnitSlotState.Active active
                || active.ActorId != life.ActorId
                || active.Generation != life.Generation
                || !string.Equals(
                    active.FormId,
                    life.FormId,
                    StringComparison.Ordinal)
                || form is null
                || !assignments[
                        (life.ActorId.TeamId, life.ActorId.UnitId)]
                    .AllowedFormIds.Contains(
                        life.FormId,
                        StringComparer.Ordinal)
                || life.SpawnedAtTick > nextTick
                || !TryReserveActorOccupancy(
                    definition,
                    form,
                    life.Position,
                    occupiedPositions)
                || !IsPendingSameLifeTransitionValid(
                    definition,
                    assignments[
                        (life.ActorId.TeamId, life.ActorId.UnitId)],
                    nextTick,
                    life))
            {
                throw new ArgumentException(
                    "Every active life must exactly match one active slot, placement, and transition state.",
                    nameof(lives));
            }
            if (life.Health <= 0 || life.Health > form.MaxHealth)
            {
                throw new ArgumentException(
                    "An active life's health must be within its form maximum.",
                    nameof(lives));
            }

            ActorAttackProfileDefinition? attack =
                form.AttackProfileId is string attackProfileId
                    ? attacks[attackProfileId]
                    : null;
            // A same-life form transition deliberately preserves remaining
            // cooldown even when the target form cannot attack or has a
            // shorter cadence. Energy, unlike cooldown, is normalized to the
            // target form's pool.
            if ((attack is null || attack.MaxEnergy == 0)
                    && life.Energy is not null
                || attack is { MaxEnergy: > 0 }
                && (life.Energy is null
                    || life.Energy > attack.MaxEnergy))
            {
                throw new ArgumentException(
                    "An active life's resource state does not match its attack profile.",
                    nameof(lives));
            }
            ValidateLifeOrigin(life, lives);
        }
        if (slots.Count(slot =>
                slot.State is
                    GenericActorRuntimeObservation.UnitSlotState.Active)
            != lives.Count)
        {
            throw new ArgumentException(
                "Every active slot must have exactly one active life.",
                nameof(lives));
        }
    }

    private static void ValidateReplications(
        ActorResolvedMatchDefinition definition,
        IReadOnlyList<SlotSnapshot> slots,
        IReadOnlyList<LifeSnapshot> lives,
        IReadOnlyList<SplitReplicationReservation> replications)
    {
        if (replications.Any(value => value is null)
            || replications.Select(value => value.OperationId)
                .Distinct(StringComparer.Ordinal).Count()
                != replications.Count
            || replications.SelectMany(value => value.Descendants)
                .Select(value => value.Position)
                .Distinct().Count()
                != replications.Sum(value => value.Descendants.Length))
        {
            throw new ArgumentException(
                "Pending replications must be non-null and operation-unique.",
                nameof(replications));
        }
        HashSet<(int TeamId, int UnitId)> slotIds = slots
            .Select(value => (value.TeamId, value.UnitId))
            .ToHashSet();
        Dictionary<(int TeamId, int UnitId), SlotSnapshot> slotsById =
            slots.ToDictionary(value => (value.TeamId, value.UnitId));
        Dictionary<ActorIdentity, LifeSnapshot> livesById =
            lives.ToDictionary(value => value.ActorId);
        Dictionary<Position, LifeSnapshot> livesByPosition =
            lives.ToDictionary(value => value.Position);
        var splitKernel = new SplitReplicationKernel(definition);
        foreach (SplitReplicationReservation reservation in replications)
        {
            splitKernel.ValidateReservationEvidence(reservation);
            if (string.IsNullOrWhiteSpace(reservation.OperationId)
                || string.IsNullOrWhiteSpace(reservation.TransitionId)
                || reservation.QueuedTick < 0
                || reservation.DueTick < reservation.QueuedTick
                || reservation.Descendants.IsDefaultOrEmpty
                || reservation.Descendants
                    .Select(value => (value.TeamId, value.UnitId))
                    .Distinct().Count() != reservation.Descendants.Length
                || reservation.Descendants.Any(value =>
                    !slotIds.Contains((value.TeamId, value.UnitId))
                    || !IsSupportedActorPlacement(
                        definition,
                        value.FormId,
                        value.Position)
                    || livesByPosition.TryGetValue(
                        value.Position,
                        out LifeSnapshot? occupyingLife)
                    && occupyingLife.ActorId
                        != reservation.SourceActorId))
            {
                throw new ArgumentException(
                    "Pending replication state is malformed.",
                    nameof(replications));
            }
            if (!livesById.TryGetValue(
                    reservation.SourceActorId,
                    out LifeSnapshot? source)
                || source.ParticipantId != reservation.ParticipantId
                || source.Generation != reservation.SourceGeneration
                || !string.Equals(
                    source.FormId,
                    reservation.SourceFormId,
                    StringComparison.Ordinal)
                || source.Position != reservation.SourcePosition
                || source.Facing != reservation.SourceFacing)
            {
                throw new ArgumentException(
                    "A pending replication must retain the exact active source life.",
                    nameof(replications));
            }
            foreach (SplitReplicationReservedDescendant descendant in
                     reservation.Descendants)
            {
                SlotSnapshot slot =
                    slotsById[(descendant.TeamId, descendant.UnitId)];
                bool sourceSlot =
                    descendant.TeamId == reservation.SourceActorId.TeamId
                    && descendant.UnitId
                    == reservation.SourceActorId.UnitId;
                if (sourceSlot)
                    continue;
                if (slot.State is not
                        GenericActorRuntimeObservation.UnitSlotState
                            .ReplicationPending pending
                    || slot.SplitReservation is null
                    || !ReplicationReservationsSemanticallyEqual(
                        slot.SplitReservation,
                        reservation)
                    || pending.SourceActorId
                        != reservation.SourceActorId
                    || !string.Equals(
                        pending.TransitionId,
                        reservation.TransitionId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        pending.OperationId,
                        reservation.OperationId,
                        StringComparison.Ordinal)
                    || pending.DueTick != reservation.DueTick
                    || !string.Equals(
                        pending.TargetFormId,
                        descendant.FormId,
                        StringComparison.Ordinal)
                    || pending.ReservedPosition != descendant.Position)
                {
                    throw new ArgumentException(
                        "Every non-source Split output must exactly match its reserved slot.",
                        nameof(replications));
                }
            }
        }
        if (slots.Count(value => value.State is
                GenericActorRuntimeObservation.UnitSlotState
                    .ReplicationPending)
            != replications.Sum(value =>
                value.Descendants.Count(descendant =>
                    descendant.TeamId != value.SourceActorId.TeamId
                    || descendant.UnitId != value.SourceActorId.UnitId)))
        {
            throw new ArgumentException(
                "Every replication-pending slot must belong to exactly one pending replication.",
                nameof(replications));
        }
        HashSet<string> reservationOperations = replications
            .Select(value => value.OperationId)
            .ToHashSet(StringComparer.Ordinal);
        if (slots.Any(value =>
                value.SplitReservation is not null
                && (!reservationOperations.Contains(
                        value.SplitReservation.OperationId)
                    || !replications.Any(reservation =>
                        ReplicationReservationsSemanticallyEqual(
                            value.SplitReservation,
                            reservation)))))
        {
            throw new ArgumentException(
                "Every slot-level replication reservation must be present in the world reservation set.",
                nameof(replications));
        }
    }

    private static void ValidateProjectiles(
        ActorResolvedMatchDefinition definition,
        IReadOnlyList<SlotSnapshot> slots,
        int nextTick,
        long nextProjectileId,
        IReadOnlyList<ProjectileSnapshot> projectiles)
    {
        Dictionary<string, ActorAttackProfileDefinition> profiles =
            definition.Rules.AttackProfiles.ToDictionary(
                value => value.Id,
                StringComparer.Ordinal);
        Dictionary<(int TeamId, int UnitId), SlotSnapshot> slotsById =
            slots.ToDictionary(value => (value.TeamId, value.UnitId));
        if (projectiles.Any(value => value is null)
            || projectiles.Select(value => value.ProjectileId)
                .Distinct().Count() != projectiles.Count
            || projectiles.Any(value =>
                value.ProjectileId >= nextProjectileId
                || !profiles.ContainsKey(value.AttackProfileId)
                || !IsCurrentProjectileTileLegal(
                    definition,
                    value.Position)
                || !IsCurrentProjectileTileLegal(
                    definition,
                    value.Origin)
                || !slotsById.TryGetValue(
                    (value.OwnerActorId.TeamId,
                        value.OwnerActorId.UnitId),
                    out SlotSnapshot? ownerSlot)
                || ownerSlot.ParticipantId != value.OwnerParticipantId
                || value.OwnerActorId.LifeId >= ownerSlot.NextLifeId
                || value.SpawnedAtTick >= nextTick
                || profiles[value.AttackProfileId].Projectile.Mode
                    != ActorProjectileMode.Discrete
                || profiles[value.AttackProfileId].ShotProgram.Enabled
                    != value.ShotProgram.HasValue
                || value.ShotProgram is ShotProgram program
                    && !profiles[value.AttackProfileId]
                        .ShotProgram.IsValid(program)
                || value.CommittedPath.IsEmpty
                || value.CommittedPath.Length
                    > profiles[value.AttackProfileId]
                        .Projectile.MaxTravelTiles
                || value.NextPathIndex <= 0
                || value.NextPathIndex >= value.CommittedPath.Length
                || value.Position
                    != value.CommittedPath[value.NextPathIndex - 1]
                || value.RemainingTiles
                    > profiles[value.AttackProfileId]
                        .Projectile.MaxTravelTiles
                || value.RemainingTiles
                    != profiles[value.AttackProfileId]
                        .Projectile.MaxTravelTiles
                        - value.NextPathIndex
                || value.TicksUntilAdvance
                    > profiles[value.AttackProfileId]
                        .Projectile.TicksPerAdvance))
        {
            throw new ArgumentException(
                "Projectile snapshots must have unique issued IDs and valid profiles/tiles.",
                nameof(projectiles));
        }

        foreach (ProjectileSnapshot projectile in projectiles)
        {
            ActorAttackProfileDefinition profile =
                profiles[projectile.AttackProfileId];
            ImmutableArray<Position> expectedPath =
                GenericActorProjectilePath.Trace(
                    definition.Map,
                    projectile.Origin,
                    projectile.LaunchHeading,
                    profile,
                    projectile.ShotProgram);
            if (!projectile.CommittedPath.SequenceEqual(expectedPath))
            {
                throw new ArgumentException(
                    "A retained projectile must preserve its exact resolved committed path.",
                    nameof(projectiles));
            }

            Position previous = projectile.Origin;
            foreach (Position position in projectile.CommittedPath)
            {
                if (!IsCurrentProjectileTileLegal(definition, position)
                    || previous.ChebyshevDistance(position) != 1)
                {
                    throw new ArgumentException(
                        "A committed projectile path must contain contiguous traversable tiles.",
                        nameof(projectiles));
                }

                previous = position;
            }
            Position traversedFrom = projectile.NextPathIndex == 1
                ? projectile.Origin
                : projectile.CommittedPath[
                    projectile.NextPathIndex - 2];
            ProjectileHeading expectedHeading =
                ProjectileHeadingExtensions.Between(
                    traversedFrom,
                    projectile.CommittedPath[
                        projectile.NextPathIndex - 1]);
            if (projectile.Heading != expectedHeading)
            {
                throw new ArgumentException(
                    "A retained projectile heading must match its most recently traversed committed-path edge.",
                    nameof(projectiles));
            }
        }
    }

    private static void ValidateScoreboard(
        ActorResolvedMatchDefinition definition,
        GenericActorRuntimeObservation.ScoreboardState scoreboard)
    {
        if (scoreboard.Teams.IsDefault
            || scoreboard.Teams.Any(value => value is null))
        {
            throw new ArgumentException(
                "Scoreboard teams must be initialized and non-null.",
                nameof(scoreboard));
        }
        int[] expectedTeams = definition.Topology.Teams
            .Select(value => value.TeamId)
            .Order()
            .ToArray();
        int[] actualTeams = scoreboard.Teams
            .Select(value => value.TeamId)
            .ToArray();
        string[] expectedChannels = definition.Rules.GameMode.ScoreCatalog
            .Select(value => ActorContractCanonicalIds.Id(value.Channel))
            .ToArray();
        Dictionary<string, ScoreChannelDefinition.ValueDomain> domains =
            definition.Rules.GameMode.ScoreCatalog.ToDictionary(
                value => ActorContractCanonicalIds.Id(value.Channel),
                value => value.Domain,
                StringComparer.Ordinal);
        if (!actualTeams.SequenceEqual(expectedTeams)
            || scoreboard.Teams.Any(team =>
                team.Scores.IsDefault
                || team.Scores.Any(value =>
                    value is null
                    || string.IsNullOrWhiteSpace(value.Channel))
                || team.Scores.Select(value => value.Channel)
                    .Distinct(StringComparer.Ordinal).Count()
                    != team.Scores.Length
                || !team.Scores.Select(value => value.Channel)
                    .SequenceEqual(expectedChannels)
                || team.Scores.Any(value =>
                    domains[value.Channel]
                        == ScoreChannelDefinition.ValueDomain.NonNegative
                    && value.Value < 0)))
        {
            throw new ArgumentException(
                "Scoreboard must exactly match topology teams and the declared score catalog.",
                nameof(scoreboard));
        }
    }

    private static void ValidateSlotStates(
        ActorResolvedMatchDefinition definition,
        int nextTick,
        IReadOnlyList<SlotSnapshot> slots)
    {
        Dictionary<(int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments =
            definition.LifecycleAssignments.ToDictionary(
                value => (value.TeamId, value.UnitId));
        Dictionary<(int TeamId, int UnitId), SlotSnapshot> slotsById =
            slots.ToDictionary(value => (value.TeamId, value.UnitId));
        Dictionary<string, BoundedChildFabricationDefinition> fabrications =
            definition.Rules.FabricationTransitions
                .OfType<BoundedChildFabricationDefinition>()
                .ToDictionary(
                    value => value.TransitionId,
                    StringComparer.Ordinal);
        Dictionary<string, ActorLifecycleProfileDefinition>
            lifecycleProfiles = definition.Rules.Lifecycle.Profiles
                .ToDictionary(
                    value => value.ProfileId,
                    StringComparer.Ordinal);
        var fabricationOperations = new HashSet<string>(
            StringComparer.Ordinal);
        var fabricationPositions = new HashSet<Position>();
        foreach (SlotSnapshot slot in slots)
        {
            ActorUnitSlotLifecycleAssignmentDefinition assignment =
                assignments[(slot.TeamId, slot.UnitId)];
            switch (slot.State)
            {
                case GenericActorRuntimeObservation.UnitSlotState.Active:
                case GenericActorRuntimeObservation.UnitSlotState.Ready:
                case GenericActorRuntimeObservation.UnitSlotState
                    .PermanentlyDormant:
                    break;
                case GenericActorRuntimeObservation.UnitSlotState
                    .AvailabilityPending pending
                    when Enum.IsDefined(pending.Reason)
                         && pending.DueTick >= nextTick:
                    break;
                case GenericActorRuntimeObservation.UnitSlotState
                    .AutomaticReturnPending pending
                    when pending.DueTick >= nextTick
                         && pending.Generation >= 0
                         && assignment.AllowedFormIds.Contains(
                             pending.TargetFormId,
                             StringComparer.Ordinal)
                         && slot.PendingParentActorId is not null:
                    break;
                case GenericActorRuntimeObservation.UnitSlotState
                    .FabricationPending pending
                    when IsFabricationPendingValid(
                        definition,
                        nextTick,
                        slot,
                        pending,
                        assignment,
                        slotsById,
                        assignments,
                        lifecycleProfiles,
                        fabrications,
                        fabricationOperations,
                        fabricationPositions):
                    break;
                case GenericActorRuntimeObservation.UnitSlotState
                    .ReplicationPending pending
                    when pending.DueTick >= nextTick
                         && !string.IsNullOrWhiteSpace(
                             pending.TransitionId)
                         && !string.IsNullOrWhiteSpace(
                             pending.OperationId)
                         && !string.IsNullOrWhiteSpace(
                             pending.TargetFormId)
                         && assignment.AllowedFormIds.Contains(
                             pending.TargetFormId,
                             StringComparer.Ordinal)
                         && IsSupportedActorPlacement(
                             definition,
                             pending.TargetFormId,
                             pending.ReservedPosition):
                    break;
                default:
                    throw new ArgumentException(
                        "A stable-slot lifecycle state is malformed for this chronology boundary.",
                        nameof(slots));
            }
        }
    }

    private static bool IsFabricationPendingValid(
        ActorResolvedMatchDefinition definition,
        int nextTick,
        SlotSnapshot targetSlot,
        GenericActorRuntimeObservation.UnitSlotState.FabricationPending pending,
        ActorUnitSlotLifecycleAssignmentDefinition targetAssignment,
        IReadOnlyDictionary<(int TeamId, int UnitId), SlotSnapshot> slotsById,
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments,
        IReadOnlyDictionary<string, ActorLifecycleProfileDefinition>
            lifecycleProfiles,
        IReadOnlyDictionary<string, BoundedChildFabricationDefinition>
            fabrications,
        ISet<string> operationIds,
        ISet<Position> reservedPositions)
    {
        if (pending.DueTick < nextTick
            || string.IsNullOrWhiteSpace(pending.TransitionId)
            || string.IsNullOrWhiteSpace(pending.OperationId)
            || string.IsNullOrWhiteSpace(pending.TargetFormId)
            || !operationIds.Add(pending.OperationId)
            || !reservedPositions.Add(pending.ReservedPosition)
            || !fabrications.TryGetValue(
                pending.TransitionId,
                out BoundedChildFabricationDefinition? transition)
            || !string.Equals(
                pending.TargetFormId,
                transition.OutputFormId,
                StringComparison.Ordinal)
            || !targetAssignment.AllowedFormIds.Contains(
                transition.OutputFormId,
                StringComparer.Ordinal)
            || !CanBecomeReadyForExplicitCreation(
                targetAssignment,
                lifecycleProfiles)
            || !IsSupportedActorPlacement(
                definition,
                transition.OutputFormId,
                pending.ReservedPosition)
            || !IsFabricationOutputPositionValid(
                definition,
                targetSlot.ParticipantId,
                transition,
                pending.ReservedPosition)
            || !slotsById.TryGetValue(
                (pending.SourceActorId.TeamId,
                    pending.SourceActorId.UnitId),
                out SlotSnapshot? sourceSlot)
            || !assignments.TryGetValue(
                (pending.SourceActorId.TeamId,
                    pending.SourceActorId.UnitId),
                out ActorUnitSlotLifecycleAssignmentDefinition?
                    sourceAssignment)
            || pending.SourceActorId.LifeId >= sourceSlot.NextLifeId
            || sourceSlot.TeamId != targetSlot.TeamId
            || sourceSlot.ParticipantId != targetSlot.ParticipantId
            || (sourceSlot.TeamId, sourceSlot.UnitId)
                == (targetSlot.TeamId, targetSlot.UnitId)
            || !sourceAssignment.AllowedFormIds.Any(sourceFormId =>
                transition.SourceFormIds.Contains(
                    sourceFormId,
                    StringComparer.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private static bool IsFabricationOutputPositionValid(
        ActorResolvedMatchDefinition definition,
        int participantId,
        BoundedChildFabricationDefinition transition,
        Position position)
    {
        ActorParticipantRegionAssignmentDefinition? regionAssignment =
            definition.ParticipantRegionAssignments.SingleOrDefault(
                value => value.ParticipantId == participantId
                    && string.Equals(
                        value.RegionRoleId,
                        transition.OutputRegionRoleId,
                        StringComparison.Ordinal));
        ActorMapRegionDefinition? region = regionAssignment is null
            ? null
            : definition.Map.Regions.SingleOrDefault(value =>
                string.Equals(
                    value.RegionId,
                    regionAssignment.MapRegionId,
                    StringComparison.Ordinal));
        return region is not null
            && region.Kind
                == ActorMapRegionDefinition.RegionKind.TransitionPlacement
            && region.Tiles.Contains(position)
            && TileSatisfiesTags(
                definition.Map,
                position,
                transition.RequiredOutputTileTags,
                transition.ForbiddenOutputTileTags);
    }

    private static bool TileSatisfiesTags(
        ActorMapDefinition map,
        Position position,
        IReadOnlyCollection<
            ActorMapTileTagDefinition.TileTagKind> required,
        IReadOnlyCollection<
            ActorMapTileTagDefinition.TileTagKind> forbidden)
    {
        HashSet<ActorMapTileTagDefinition.TileTagKind> actual = map.TileTags
            .Where(value => value.Tiles.Contains(position))
            .Select(value => value.Kind)
            .ToHashSet();
        return required.All(actual.Contains)
            && !forbidden.Any(actual.Contains);
    }

    private static bool CanBecomeReadyForExplicitCreation(
        ActorUnitSlotLifecycleAssignmentDefinition assignment,
        IReadOnlyDictionary<string, ActorLifecycleProfileDefinition>
            lifecycleProfiles) =>
        assignment.InitialAvailability
            == ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.DormantUnlockAtTick
        || lifecycleProfiles.TryGetValue(
                assignment.LifecycleProfileId,
                out ActorLifecycleProfileDefinition? profile)
            && profile.DestructionPolicy
                == ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .ReadyForExplicitFabrication;

    private static bool IsPendingSameLifeTransitionValid(
        ActorResolvedMatchDefinition definition,
        ActorUnitSlotLifecycleAssignmentDefinition assignment,
        int nextTick,
        LifeSnapshot life)
    {
        GenericActorRuntimeObservation.PendingSameLifeTransition? pending =
            life.PendingSameLifeTransition;
        if (pending is null)
            return true;
        if (string.IsNullOrWhiteSpace(pending.TransitionId)
            || string.IsNullOrWhiteSpace(pending.OperationId)
            || string.IsNullOrWhiteSpace(pending.TargetFormId)
            || pending.StartedTick < 0
            || pending.StartedTick >= nextTick
            || pending.DueTick < nextTick)
        {
            return false;
        }

        ActorSameLifeTransitionDefinition? transition =
            definition.Rules.SameLifeTransitions.SingleOrDefault(value =>
                string.Equals(
                    value.TransitionId,
                    pending.TransitionId,
                    StringComparison.Ordinal));
        if (transition is null
            || !string.Equals(
                life.FormId,
                transition.SourceFormId,
                StringComparison.Ordinal)
            || !string.Equals(
                pending.TargetFormId,
                transition.TargetFormId,
                StringComparison.Ordinal)
            || !assignment.AllowedFormIds.Contains(
                transition.TargetFormId,
                StringComparer.Ordinal))
        {
            return false;
        }

        long dueOffset = transition.Windup.Completion switch
        {
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration =>
                transition.Windup.DurationTicks,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate =>
                transition.Windup.DurationTicks - 1L,
            _ => -1,
        };
        return dueOffset >= 0
            && (long)pending.StartedTick + dueOffset == pending.DueTick;
    }

    private static bool TryReserveActorOccupancy(
        ActorResolvedMatchDefinition definition,
        ActorFormDefinition form,
        Position position,
        ISet<Position> occupied)
    {
        ActorMovementLayer? layer = SupportedActorMovementLayer(
            definition,
            form);
        return layer is not null
            && IsActorTileLegal(definition, layer.Value, position)
            && occupied.Add(position);
    }

    private static bool IsSupportedActorPlacement(
        ActorResolvedMatchDefinition definition,
        string formId,
        Position position)
    {
        ActorFormDefinition? form = definition.Rules.Forms.SingleOrDefault(
            value => string.Equals(
                value.Id,
                formId,
                StringComparison.Ordinal));
        ActorMovementLayer? layer = form is null
            ? null
            : SupportedActorMovementLayer(definition, form);
        return layer is not null
            && IsActorTileLegal(definition, layer.Value, position);
    }

    private static ActorMovementLayer? SupportedActorMovementLayer(
        ActorResolvedMatchDefinition definition,
        ActorFormDefinition form)
    {
        ActorMovementProfileDefinition? movement =
            definition.Rules.MovementProfiles.SingleOrDefault(value =>
                string.Equals(
                    value.Id,
                    form.MovementProfileId,
                    StringComparison.Ordinal));
        return movement?.MovementLayer switch
        {
            ActorMovementLayer.Ground => ActorMovementLayer.Ground,
            _ => null,
        };
    }

    private static bool IsActorTileLegal(
        ActorResolvedMatchDefinition definition,
        ActorMovementLayer layer,
        Position position) =>
        layer switch
        {
            ActorMovementLayer.Ground =>
                IsCurrentGroundTileLegal(definition, position),
            _ => false,
        };

    private static bool IsCurrentProjectileTileLegal(
        ActorResolvedMatchDefinition definition,
        Position position) =>
        IsCurrentGroundTileLegal(definition, position);

    private static bool IsCurrentGroundTileLegal(
        ActorResolvedMatchDefinition definition,
        Position position) =>
        !definition.Map.IsWall(position);

    private static bool ReplicationReservationsSemanticallyEqual(
        SplitReplicationReservation left,
        SplitReplicationReservation right) =>
        left.SourceActorId == right.SourceActorId
        && left.ParticipantId == right.ParticipantId
        && left.SourceGeneration == right.SourceGeneration
        && string.Equals(
            left.SourceFormId,
            right.SourceFormId,
            StringComparison.Ordinal)
        && left.SourcePosition == right.SourcePosition
        && left.SourceFacing == right.SourceFacing
        && string.Equals(
            left.TransitionId,
            right.TransitionId,
            StringComparison.Ordinal)
        && string.Equals(
            left.OperationId,
            right.OperationId,
            StringComparison.Ordinal)
        && left.QueuedTick == right.QueuedTick
        && left.DueTick == right.DueTick
        && left.Descendants.SequenceEqual(right.Descendants);

    private static void ValidateParticipantEligibility(
        ActorResolvedMatchDefinition definition,
        IReadOnlyCollection<
            GenericActorRuntimeObservation.ObservedParticipantStatus>
            participants,
        IReadOnlyCollection<SlotSnapshot> slots,
        IReadOnlyCollection<LifeSnapshot> lives,
        IReadOnlyCollection<SplitReplicationReservation> replications,
        IReadOnlyCollection<ProjectileSnapshot> projectiles,
        GenericActorRuntimeObservation.ScoreboardState scoreboard)
    {
        HashSet<int> disqualified = participants
            .Where(value => value.Disqualified)
            .Select(value => value.ParticipantId)
            .ToHashSet();
        if (slots.Any(value =>
                disqualified.Contains(value.ParticipantId)
                && value.State is not
                    GenericActorRuntimeObservation.UnitSlotState
                        .PermanentlyDormant)
            || lives.Any(value =>
                disqualified.Contains(value.ParticipantId))
            || replications.Any(value =>
                disqualified.Contains(value.ParticipantId))
            || projectiles.Any(value =>
                disqualified.Contains(value.OwnerParticipantId)))
        {
            throw new ArgumentException(
                "A disqualified participant cannot retain active or pending world state.",
                nameof(participants));
        }

        Dictionary<int, bool> expectedEligibility =
            definition.Topology.Teams.ToDictionary(
                value => value.TeamId,
                team => participants.Any(participant =>
                    participant.TeamId == team.TeamId
                    && !participant.Disqualified));
        if (scoreboard.Teams.Any(team =>
                team.Eligible != expectedEligibility[team.TeamId]))
        {
            throw new ArgumentException(
                "Scoreboard eligibility must match participant disqualification state.",
                nameof(scoreboard));
        }
    }

    private static void ValidateLifeOrigin(
        LifeSnapshot life,
        IReadOnlyCollection<LifeSnapshot> activeLives)
    {
        bool hasTransition =
            life.SourceTransitionId is not null
            || life.SourceOperationId is not null;
        if ((life.SourceTransitionId is null)
                != (life.SourceOperationId is null)
            || life.SourceTransitionId is not null
            && string.IsNullOrWhiteSpace(life.SourceTransitionId)
            || life.SourceOperationId is not null
            && string.IsNullOrWhiteSpace(life.SourceOperationId)
            || life.ParentActorId == life.ActorId)
        {
            throw new ArgumentException(
                "An active life's lineage is malformed.",
                nameof(activeLives));
        }

        bool valid = life.SpawnReason switch
        {
            GenericActorRuntimeStart.SpawnReason.Initial =>
                life.ParentActorId is null && !hasTransition,
            GenericActorRuntimeStart.SpawnReason.AutomaticReturn =>
                life.ParentActorId is not null && !hasTransition,
            GenericActorRuntimeStart.SpawnReason.Fabrication =>
                life.ParentActorId is not null && hasTransition,
            GenericActorRuntimeStart.SpawnReason.Replication =>
                life.ParentActorId is not null && hasTransition,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException(
                "An active life's lineage does not match its spawn reason.",
                nameof(activeLives));
        }
    }

    private static bool ModeMatchesDefinition(
        GameModeDefinition definition,
        GenericActorRuntimeObservation.ModeObservationState mode) =>
        string.Equals(
            mode.ModeId,
            definition.ModeId,
            StringComparison.Ordinal)
        && (definition, mode) switch
        {
            (DeathmatchGameModeDefinition,
                GenericActorRuntimeObservation.ModeObservationState
                    .Deathmatch) => true,
            (FrontlineGameModeDefinition,
                GenericActorRuntimeObservation.ModeObservationState
                    .Frontline) => true,
            _ => false,
        };
}
