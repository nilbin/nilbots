using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Headless Prime-only Frontline simulation. This session deliberately does
/// not own runtimes, observations, replay, fabrication, or Anchor. Callers
/// prepare a tick to discover the exact active life identities, then submit
/// one keyed joint decision for that frozen actor set.
/// </summary>
public sealed class FrontlineMatchSession
{
    private readonly ResolvedMatchDefinition _definition;
    private readonly FrontlineRules _frontlineRules;
    private readonly FrontlineMapProfile _profile;
    private FrontlineTickStart? _preparedTick;

    public FrontlineMatchSession(ResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definition = MatchDefinitionResolver.Resolve(
            definition.Rules,
            definition.Map,
            definition.Topology);
        _frontlineRules = _definition.FrontlineRules
            ?? throw new ArgumentException(
                "FrontlineMatchSession requires a Frontline definition.",
                nameof(definition));
        _profile = _definition.FrontlineMapProfile
            ?? throw new ArgumentException(
                "FrontlineMatchSession requires a Frontline map profile.",
                nameof(definition));
        ValidateSupportedDefinition();
        State = null!;
        Reset();
    }

    public FrontlineMatchState State { get; private set; }
    public bool IsCompleted => State.IsCompleted;
    public FrontlineMatchResult? Result => State.Result;

    /// <summary>Restores authored Prime lives, tick zero, and centre control.</summary>
    public FrontlineResetResult Reset()
    {
        ImmutableArray<FrontlineTeamState> teams = _profile.TeamHomes
            .OrderBy(home => home.TeamId)
            .Select(home =>
            {
                var actorId = new FrontlineActorId(
                    home.TeamId,
                    UnitId: 0,
                    LifeId: 0);
                var life = new FrontlineLifeState(
                    actorId,
                    new Position(home.PrimeSpawn.X, home.PrimeSpawn.Y),
                    home.PrimeSpawn.Facing,
                    _frontlineRules.PrimeForm.MaxHealth,
                    spawnedAtTick: 0,
                    energy: _definition.Rules.MaxEnergy);
                var prime = new FrontlineUnitState(
                    home.TeamId,
                    unitId: 0,
                    _frontlineRules.PrimeForm.FormId,
                    life,
                    nextLifeId: 1);
                return new FrontlineTeamState(home.TeamId, [prime]);
            })
            .ToImmutableArray();

        State = new FrontlineMatchState(
            _definition,
            teams,
            FrontlineControlSystem.CreateInitial(_frontlineRules));
        _preparedTick = null;
        return new FrontlineResetResult(State, ActiveActorIds());
    }

    /// <summary>
    /// Applies due tick-start respawns once, then freezes the exact actor keys
    /// required by <see cref="Step"/>. Repeated calls before Step are idempotent.
    /// </summary>
    public FrontlineTickStart PrepareTick()
    {
        if (IsCompleted)
            throw new InvalidOperationException("Frontline match already completed.");
        if (_preparedTick is not null)
            return _preparedTick;

        var events = new List<FrontlineMatchEvent>();
        var respawned = new List<FrontlineActorId>();
        foreach (FrontlineTeamState team in State.Teams.OrderBy(team => team.TeamId))
        {
            foreach (FrontlineUnitState unit in team.Units.OrderBy(unit => unit.UnitId))
            {
                if (unit.RespawnAtTick is not int dueTick)
                    continue;
                if (dueTick < State.Tick)
                {
                    throw new InvalidOperationException(
                        $"Unit {team.TeamId}:{unit.UnitId} missed respawn tick {dueTick}.");
                }
                if (dueTick != State.Tick)
                    continue;

                FrontlineTeamHome home = Home(team.TeamId);
                var actorId = new FrontlineActorId(
                    team.TeamId,
                    unit.UnitId,
                    unit.NextLifeId);
                unit.NextLifeId++;
                unit.ActiveLife = new FrontlineLifeState(
                    actorId,
                    new Position(home.PrimeSpawn.X, home.PrimeSpawn.Y),
                    home.PrimeSpawn.Facing,
                    _frontlineRules.PrimeForm.MaxHealth,
                    State.Tick,
                    _definition.Rules.MaxEnergy);
                unit.LifecycleStatus = FrontlineLifecycleStatus.Active;
                unit.RespawnAtTick = null;
                respawned.Add(actorId);
                events.Add(new FrontlineMatchEvent
                {
                    Tick = State.Tick,
                    Type = FrontlineMatchEventType.Respawned,
                    TeamId = team.TeamId,
                    ActorId = actorId,
                    To = unit.ActiveLife.Position,
                    ToFacing = unit.ActiveLife.Facing,
                    NewHealth = unit.ActiveLife.Health,
                    LifecycleStatus = FrontlineLifecycleStatus.Active,
                });
            }
        }

        _preparedTick = new FrontlineTickStart(
            State.Tick,
            ActiveActorIds(),
            respawned.Order().ToImmutableArray(),
            events.ToImmutableArray());
        return _preparedTick;
    }

    /// <summary>Resolves one prepared, stable-keyed joint action.</summary>
    public FrontlineStepResult Step(
        IReadOnlyDictionary<FrontlineActorId, BotDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        if (IsCompleted)
            throw new InvalidOperationException("Frontline match already completed.");
        FrontlineTickStart tickStart = _preparedTick
            ?? throw new InvalidOperationException(
                "PrepareTick must be called before Step.");
        Dictionary<FrontlineActorId, BotDecision> frozenDecisions =
            decisions.ToDictionary(entry => entry.Key, entry => entry.Value);
        ValidateDecisionKeys(tickStart, frozenDecisions);

        var resolutions = ValidateActions(
            tickStart.ActiveActors,
            frozenDecisions);
        // Tick-start lifecycle facts remain phase-distinct on TickStart. The
        // resolution list contains only facts produced after decisions are
        // accepted, preventing replay-v2 from duplicating respawn events.
        var events = new List<FrontlineMatchEvent>();
        var traversals = new List<FrontlineProjectileTraversal>();
        int executedTick = State.Tick;

        ResolveTurns(resolutions, events);
        ResolveMovement(resolutions, events);

        var pendingHits = new List<PendingHit>();
        AdvanceProjectiles(pendingHits, traversals);
        HashSet<FrontlineActorId> shotActors = ResolveShooting(
            resolutions,
            frozenDecisions,
            pendingHits,
            events,
            traversals);
        ApplyDamageAndQueueRespawns(pendingHits, events);

        foreach (FrontlineActionResolution resolution in resolutions.Values)
        {
            if (TryGetActiveLife(resolution.ActorId) is { } life)
                life.LastActionResult = resolution.Result;
        }
        UpdateCooldownsAndEnergy(shotActors, executedTick);
        ResolveObjective(events);

        State.Tick = executedTick + 1;
        FrontlineMatchResult? result = null;
        if (State.Control.WinnerTeamId is int)
        {
            result = Complete(
                FrontlineMatchEndReason.BaseBreach,
                executedTick);
        }
        else if (State.Tick >= _definition.Rules.MaxTicks)
        {
            result = Complete(
                FrontlineMatchEndReason.MaxTicks,
                executedTick);
        }

        ImmutableArray<FrontlineActionResolution> orderedResolutions =
            resolutions.Values
            .OrderBy(resolution => resolution.ActorId)
            .ToImmutableArray();
        var stepResult = new FrontlineStepResult(
            executedTick,
            tickStart,
            orderedResolutions,
            events.ToImmutableArray(),
            traversals.ToImmutableArray(),
            State.Control,
            State.IsCompleted,
            result);
        _preparedTick = null;
        return stepResult;
    }

    private void ValidateSupportedDefinition()
    {
        GameRules rules = _definition.Rules;
        if (_frontlineRules.InitialUnitsPerTeam != 1
            || _frontlineRules.MaxUnitsPerTeam != 1
            || _frontlineRules.FabricationUnlockTicks.IsDefaultOrEmpty is false)
        {
            throw new NotSupportedException(
                "The headless Package 3 session supports exactly one Prime per team " +
                "and no fabrication unlocks.");
        }
        if (rules.ProjectileTicksPerTile <= 0
            || rules.ProjectileTilesPerAdvance <= 0)
        {
            throw new NotSupportedException(
                "The headless Frontline session requires positive discrete-projectile cadence.");
        }
        if (rules.DamagePerHit <= 0)
            throw new NotSupportedException("Frontline projectile damage must be positive.");
        if (rules.AllowProgrammedShots
            && _frontlineRules.PrimeForm.AllowsProgrammedShots
            && (rules.ShotRange <= 0 || rules.ProgrammedShotLaunchTiles <= 0))
        {
            throw new NotSupportedException(
                "Programmed Frontline shots require positive range and launch distance.");
        }
        if (rules.MaxEnergy < 0
            || rules.ShotEnergyCost < 0
            || rules.EnergyRegenTicks < 0)
        {
            throw new NotSupportedException(
                "Frontline energy values cannot be negative.");
        }
        if (_frontlineRules.PrimeForm.ObjectiveWeight <= 0)
        {
            throw new NotSupportedException(
                "The Prime-only session requires a positive Prime objective weight.");
        }
        if (_frontlineRules.PrimeForm.OmnidirectionalShooting)
        {
            throw new NotSupportedException(
                "Prime-only Package 3 has no omnidirectional shooting action.");
        }
    }

    private void ValidateDecisionKeys(
        FrontlineTickStart tickStart,
        IReadOnlyDictionary<FrontlineActorId, BotDecision> decisions)
    {
        FrontlineActorId[] actual = decisions.Keys.Order().ToArray();
        if (!actual.SequenceEqual(tickStart.ActiveActors))
        {
            throw new ArgumentException(
                "Decisions must contain exactly the actors returned by PrepareTick.",
                nameof(decisions));
        }

        foreach (FrontlineActorId actorId in tickStart.ActiveActors)
        {
            BotDecision? decision = decisions[actorId];
            if (decision is null)
            {
                throw new ArgumentException(
                    $"Decision for actor {actorId} cannot be null.",
                    nameof(decisions));
            }
            if (decision.Faulted)
            {
                throw new ArgumentException(
                    "Runtime faults are outside the Package 3 headless session.",
                    nameof(decisions));
            }
            if (!Enum.IsDefined(decision.Action))
            {
                throw new ArgumentException(
                    $"Actor {actorId} submitted an unknown action.",
                    nameof(decisions));
            }
            if (decision.ShotProgram is not null
                && decision.Action != BotAction.Shoot)
            {
                throw new ArgumentException(
                    "A shot program is valid only on Shoot.",
                    nameof(decisions));
            }
            if (decision.ShotProgram is ShotProgram program
                && _definition.Rules.AllowProgrammedShots
                && _frontlineRules.PrimeForm.AllowsProgrammedShots
                && !_definition.Rules.IsValidShotProgram(program))
            {
                throw new ArgumentException(
                    $"Actor {actorId} submitted an invalid shot program.",
                    nameof(decisions));
            }
        }
    }

    private Dictionary<FrontlineActorId, FrontlineActionResolution> ValidateActions(
        IReadOnlyList<FrontlineActorId> actors,
        IReadOnlyDictionary<FrontlineActorId, BotDecision> decisions)
    {
        var resolutions = new Dictionary<
            FrontlineActorId,
            FrontlineActionResolution>();
        UnitFormRules form = _frontlineRules.PrimeForm;
        GameRules rules = _definition.Rules;
        foreach (FrontlineActorId actorId in actors)
        {
            BotDecision decision = decisions[actorId];
            FrontlineLifeState life = State.GetActiveLife(actorId);
            BotAction validated = decision.Action;
            ActionResult result = ActionResult.Success;
            ShotProgram? validatedProgram = decision.ShotProgram;

            bool movement = decision.Action is
                BotAction.MoveForward or
                BotAction.StrafeLeft or
                BotAction.StrafeRight;
            if (movement && !form.CanMove)
            {
                validated = BotAction.Wait;
                result = ActionResult.Blocked;
            }
            else if (decision.Action is BotAction.StrafeLeft or BotAction.StrafeRight
                     && !rules.AllowStrafe)
            {
                validated = BotAction.Wait;
                result = ActionResult.Blocked;
            }
            else if (decision.Action == BotAction.Shoot && !form.CanShoot)
            {
                validated = BotAction.Wait;
                result = ActionResult.Blocked;
                validatedProgram = null;
            }
            else if (decision.ShotProgram is not null
                     && (!rules.AllowProgrammedShots
                         || !form.AllowsProgrammedShots))
            {
                validated = BotAction.Wait;
                result = ActionResult.Blocked;
                validatedProgram = null;
            }
            else if (decision.Action == BotAction.Shoot && life.Cooldown > 0)
            {
                validated = BotAction.Wait;
                result = ActionResult.OnCooldown;
                validatedProgram = null;
            }
            else if (decision.Action == BotAction.Shoot
                     && rules.MaxEnergy > 0
                     && life.Energy < rules.ShotEnergyCost)
            {
                validated = BotAction.Wait;
                result = ActionResult.OnCooldown;
                validatedProgram = null;
            }

            resolutions.Add(
                actorId,
                new FrontlineActionResolution(
                    actorId,
                    decision.Action,
                    validated,
                    result,
                    decision.ShotProgram,
                    validatedProgram));
        }
        return resolutions;
    }

    private void ResolveTurns(
        IReadOnlyDictionary<FrontlineActorId, FrontlineActionResolution> resolutions,
        List<FrontlineMatchEvent> events)
    {
        foreach (FrontlineActionResolution resolution in resolutions.Values
                     .OrderBy(resolution => resolution.ActorId))
        {
            if (resolution.ValidatedAction is not
                (BotAction.TurnLeft or BotAction.TurnRight))
            {
                continue;
            }

            FrontlineLifeState life = State.GetActiveLife(resolution.ActorId);
            Direction before = life.Facing;
            life.Facing = resolution.ValidatedAction == BotAction.TurnLeft
                ? before.TurnedLeft()
                : before.TurnedRight();
            events.Add(new FrontlineMatchEvent
            {
                Tick = State.Tick,
                Type = FrontlineMatchEventType.Turn,
                TeamId = resolution.ActorId.TeamId,
                ActorId = resolution.ActorId,
                From = life.Position,
                To = life.Position,
                FromFacing = before,
                ToFacing = life.Facing,
                Action = resolution.ValidatedAction,
            });
        }
    }

    private void ResolveMovement(
        Dictionary<FrontlineActorId, FrontlineActionResolution> resolutions,
        List<FrontlineMatchEvent> events)
    {
        FrontlineActorId[] actors = resolutions.Keys.Order().ToArray();
        var targets = new Dictionary<FrontlineActorId, Position>();
        var blocked = new HashSet<FrontlineActorId>();
        foreach (FrontlineActorId actorId in actors)
        {
            FrontlineActionResolution resolution = resolutions[actorId];
            FrontlineLifeState life = State.GetActiveLife(actorId);
            Direction? direction = resolution.ValidatedAction switch
            {
                BotAction.MoveForward => life.Facing,
                BotAction.StrafeLeft => life.Facing.TurnedLeft(),
                BotAction.StrafeRight => life.Facing.TurnedRight(),
                _ => null,
            };
            if (direction is null)
                continue;

            var (dx, dy) = direction.Value.Vector();
            Position target = life.Position.Offset(dx, dy);
            targets.Add(actorId, target);
            if (_definition.Map.IsWall(target)
                || IsOpposingProtectedPad(actorId.TeamId, target))
            {
                blocked.Add(actorId);
            }
        }

        FrontlineActorId[] movers = targets.Keys.Order().ToArray();
        for (int leftIndex = 0; leftIndex < movers.Length; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1;
                 rightIndex < movers.Length;
                 rightIndex++)
            {
                FrontlineActorId left = movers[leftIndex];
                FrontlineActorId right = movers[rightIndex];
                if (targets[left] == targets[right])
                {
                    blocked.Add(left);
                    blocked.Add(right);
                }
                else if (targets[left] == State.GetActiveLife(right).Position
                         && targets[right] == State.GetActiveLife(left).Position)
                {
                    blocked.Add(left);
                    blocked.Add(right);
                }
            }
        }

        for (int pass = 0; pass < actors.Length; pass++)
        {
            foreach (FrontlineActorId actorId in movers)
            {
                if (blocked.Contains(actorId))
                    continue;
                foreach (FrontlineActorId otherId in actors)
                {
                    if (otherId == actorId
                        || State.GetActiveLife(otherId).Position != targets[actorId])
                    {
                        continue;
                    }

                    bool vacating =
                        targets.ContainsKey(otherId)
                        && !blocked.Contains(otherId);
                    if (!vacating)
                        blocked.Add(actorId);
                }
            }
        }

        foreach (FrontlineActorId actorId in movers)
        {
            FrontlineLifeState life = State.GetActiveLife(actorId);
            Position from = life.Position;
            Position target = targets[actorId];
            if (blocked.Contains(actorId))
            {
                FrontlineActionResolution prior = resolutions[actorId];
                resolutions[actorId] = prior with { Result = ActionResult.Blocked };
                events.Add(new FrontlineMatchEvent
                {
                    Tick = State.Tick,
                    Type = FrontlineMatchEventType.MoveBlocked,
                    TeamId = actorId.TeamId,
                    ActorId = actorId,
                    From = from,
                    To = target,
                    Action = prior.ValidatedAction,
                    ActionResult = ActionResult.Blocked,
                });
                continue;
            }

            life.Position = target;
            events.Add(new FrontlineMatchEvent
            {
                Tick = State.Tick,
                Type = FrontlineMatchEventType.Move,
                TeamId = actorId.TeamId,
                ActorId = actorId,
                From = from,
                To = target,
                Action = resolutions[actorId].ValidatedAction,
                ActionResult = ActionResult.Success,
            });
        }
    }

    private void AdvanceProjectiles(
        List<PendingHit> pendingHits,
        List<FrontlineProjectileTraversal> traversals)
    {
        var surviving = new List<FrontlineProjectileState>();
        foreach (FrontlineProjectileState projectile in
                 State.Projectiles.OrderBy(projectile => projectile.Id))
        {
            ProjectileContact early = ContactAt(
                projectile.OwnerActorId,
                projectile.Position);
            if (early.Consumes)
            {
                AddPendingHit(
                    projectile.OwnerActorId,
                    projectile.Id,
                    early,
                    pendingHits);
                continue;
            }

            projectile.Phase++;
            if (projectile.Phase < _definition.Rules.ProjectileTicksPerTile)
            {
                surviving.Add(projectile);
                continue;
            }

            projectile.Phase = 0;
            if (projectile.ProgrammedPath is { } programmedPath)
            {
                AdvanceProgrammedProjectile(
                    projectile,
                    programmedPath,
                    pendingHits,
                    traversals,
                    surviving);
            }
            else
            {
                AdvanceStraightProjectile(
                    projectile,
                    pendingHits,
                    traversals,
                    surviving);
            }
        }

        State.MutableProjectiles.Clear();
        State.MutableProjectiles.AddRange(surviving);
    }

    private void AdvanceProgrammedProjectile(
        FrontlineProjectileState projectile,
        IReadOnlyList<Position> programmedPath,
        List<PendingHit> pendingHits,
        List<FrontlineProjectileTraversal> traversals,
        List<FrontlineProjectileState> surviving)
    {
        Position from = projectile.Position;
        var path = new List<Position>();
        bool consumed = false;
        bool despawned = false;
        for (int step = 0;
             step < _definition.Rules.ProjectileTilesPerAdvance;
             step++)
        {
            if (projectile.NextProgrammedPathIndex >= programmedPath.Count)
            {
                despawned = true;
                break;
            }

            Position next = programmedPath[projectile.NextProgrammedPathIndex++];
            projectile.Heading = ProjectileHeadingExtensions.Between(
                projectile.Position,
                next);
            projectile.Position = next;
            projectile.TilesTraveled++;
            path.Add(next);

            ProjectileContact contact = ContactAt(
                projectile.OwnerActorId,
                projectile.Position);
            if (contact.Consumes)
            {
                AddPendingHit(
                    projectile.OwnerActorId,
                    projectile.Id,
                    contact,
                    pendingHits);
                consumed = true;
                break;
            }
            if (projectile.NextProgrammedPathIndex >= programmedPath.Count)
            {
                despawned = true;
                break;
            }
        }

        if (path.Count > 0)
        {
            traversals.Add(new FrontlineProjectileTraversal(
                projectile.Id,
                projectile.OwnerActorId,
                projectile.Direction,
                from,
                path.ToImmutableArray(),
                projectile.Heading,
                projectile.ShotProgram,
                programmedPath));
        }
        if (!consumed && !despawned)
            surviving.Add(projectile);
    }

    private void AdvanceStraightProjectile(
        FrontlineProjectileState projectile,
        List<PendingHit> pendingHits,
        List<FrontlineProjectileTraversal> traversals,
        List<FrontlineProjectileState> surviving)
    {
        Position from = projectile.Position;
        var path = new List<Position>();
        bool consumed = false;
        bool despawned = false;
        var (dx, dy) = projectile.Direction.Vector();
        for (int step = 0;
             step < _definition.Rules.ProjectileTilesPerAdvance;
             step++)
        {
            Position next = projectile.Position.Offset(dx, dy);
            if (_definition.Map.IsWall(next))
            {
                despawned = true;
                break;
            }

            projectile.Position = next;
            projectile.TilesTraveled++;
            path.Add(next);
            ProjectileContact contact = ContactAt(
                projectile.OwnerActorId,
                projectile.Position);
            if (contact.Consumes)
            {
                AddPendingHit(
                    projectile.OwnerActorId,
                    projectile.Id,
                    contact,
                    pendingHits);
                consumed = true;
                break;
            }
            if (_definition.Rules.ShotRange > 0
                && projectile.TilesTraveled >= _definition.Rules.ShotRange)
            {
                despawned = true;
                break;
            }
        }

        if (path.Count > 0)
        {
            traversals.Add(new FrontlineProjectileTraversal(
                projectile.Id,
                projectile.OwnerActorId,
                projectile.Direction,
                from,
                path.ToImmutableArray()));
        }
        if (!consumed && !despawned)
            surviving.Add(projectile);
    }

    private HashSet<FrontlineActorId> ResolveShooting(
        IReadOnlyDictionary<FrontlineActorId, FrontlineActionResolution> resolutions,
        IReadOnlyDictionary<FrontlineActorId, BotDecision> decisions,
        List<PendingHit> pendingHits,
        List<FrontlineMatchEvent> events,
        List<FrontlineProjectileTraversal> traversals)
    {
        var shotActors = new HashSet<FrontlineActorId>();
        foreach (FrontlineActionResolution resolution in resolutions.Values
                     .OrderBy(resolution => resolution.ActorId))
        {
            if (resolution.ValidatedAction != BotAction.Shoot)
                continue;

            shotActors.Add(resolution.ActorId);
            FrontlineLifeState shooter = State.GetActiveLife(resolution.ActorId);
            if (_definition.Rules.AllowProgrammedShots
                && _frontlineRules.PrimeForm.AllowsProgrammedShots)
            {
                ResolveProgrammedShot(
                    shooter,
                    decisions[resolution.ActorId].ShotProgram
                        ?? ShotProgram.Straight,
                    pendingHits,
                    events,
                    traversals);
            }
            else
            {
                ResolveStraightShot(
                    shooter,
                    pendingHits,
                    events,
                    traversals);
            }
        }
        return shotActors;
    }

    private void ResolveStraightShot(
        FrontlineLifeState shooter,
        List<PendingHit> pendingHits,
        List<FrontlineMatchEvent> events,
        List<FrontlineProjectileTraversal> traversals)
    {
        var (dx, dy) = shooter.Facing.Vector();
        Position spawn = shooter.Position.Offset(dx, dy);
        if (_definition.Map.IsWall(spawn))
        {
            events.Add(ShotEvent(
                shooter,
                spawn,
                projectileId: null,
                target: null,
                heading: null,
                program: null));
            return;
        }

        ProjectileContact contact = ContactAt(shooter.ActorId, spawn);
        long projectileId = State.NextProjectileId++;
        events.Add(ShotEvent(
            shooter,
            spawn,
            projectileId,
            contact.ActorId,
            heading: null,
            program: null));
        traversals.Add(new FrontlineProjectileTraversal(
            projectileId,
            shooter.ActorId,
            shooter.Facing,
            shooter.Position,
            ImmutableArray.Create(spawn)));
        if (contact.Consumes)
        {
            AddPendingHit(
                shooter.ActorId,
                projectileId,
                contact,
                pendingHits);
            return;
        }
        if (_definition.Rules.ShotRange == 1)
            return;

        State.MutableProjectiles.Add(new FrontlineProjectileState(
            projectileId,
            shooter.ActorId,
            spawn,
            shooter.Facing)
        {
            TilesTraveled = 1,
        });
    }

    private void ResolveProgrammedShot(
        FrontlineLifeState shooter,
        ShotProgram program,
        List<PendingHit> pendingHits,
        List<FrontlineMatchEvent> events,
        List<FrontlineProjectileTraversal> traversals)
    {
        ImmutableArray<Position> fullPath = ProgrammedProjectilePath.Trace(
                _definition.Map,
                shooter.Position,
                shooter.Facing,
                program,
                _definition.Rules)
            .ToImmutableArray();
        ProjectileHeading heading = shooter.Facing
            .ToProjectileHeading()
            .Turned(program.InitialAimOffset);
        var (dx, dy) = heading.Vector();
        Position desiredFirstTile = shooter.Position.Offset(dx, dy);
        if (fullPath.Length == 0)
        {
            events.Add(ShotEvent(
                shooter,
                desiredFirstTile,
                projectileId: null,
                target: null,
                heading,
                program));
            return;
        }

        int enteredCount = Math.Min(
            _definition.Rules.ProgrammedShotLaunchTiles,
            fullPath.Length);
        var traversed = new List<Position>();
        Position current = shooter.Position;
        ProjectileContact contact = ProjectileContact.None;
        foreach (Position next in fullPath.Take(enteredCount))
        {
            heading = ProjectileHeadingExtensions.Between(current, next);
            current = next;
            traversed.Add(next);
            contact = ContactAt(shooter.ActorId, current);
            if (contact.Consumes)
                break;
        }

        long projectileId = State.NextProjectileId++;
        events.Add(ShotEvent(
            shooter,
            current,
            projectileId,
            contact.ActorId,
            heading,
            program));
        traversals.Add(new FrontlineProjectileTraversal(
            projectileId,
            shooter.ActorId,
            shooter.Facing,
            shooter.Position,
            traversed.ToImmutableArray(),
            heading,
            program,
            fullPath));
        if (contact.Consumes)
        {
            AddPendingHit(
                shooter.ActorId,
                projectileId,
                contact,
                pendingHits);
            return;
        }
        if (traversed.Count >= fullPath.Length)
            return;

        State.MutableProjectiles.Add(new FrontlineProjectileState(
            projectileId,
            shooter.ActorId,
            current,
            shooter.Facing,
            heading,
            program,
            fullPath)
        {
            NextProgrammedPathIndex = traversed.Count,
            TilesTraveled = traversed.Count,
        });
    }

    private void ApplyDamageAndQueueRespawns(
        IReadOnlyList<PendingHit> pendingHits,
        List<FrontlineMatchEvent> events)
    {
        var destructionCauses = new Dictionary<
            FrontlineActorId,
            (FrontlineActorId SourceActorId, long ProjectileId)>();
        foreach (IGrouping<FrontlineActorId, PendingHit> targetGroup in
                 pendingHits
                     .OrderBy(hit => hit.TargetActorId)
                     .ThenBy(hit => hit.Sequence)
                     .GroupBy(hit => hit.TargetActorId))
        {
            FrontlineLifeState? target = TryGetActiveLife(targetGroup.Key);
            if (target is null)
                continue;

            int remainingHealth = target.Health;
            foreach (PendingHit hit in targetGroup.OrderBy(hit => hit.Sequence))
            {
                int actualDamage = Math.Min(
                    _definition.Rules.DamagePerHit,
                    remainingHealth);
                if (actualDamage <= 0)
                    continue;
                remainingHealth -= actualDamage;
                FrontlineUnitState sourceUnit = State.GetUnit(
                    hit.SourceActorId.TeamId,
                    hit.SourceActorId.UnitId);
                sourceUnit.DamageDealt += actualDamage;
                if (TryGetActiveLife(hit.SourceActorId) is { } sourceLife)
                    sourceLife.DamageDealt += actualDamage;
                destructionCauses[target.ActorId] =
                    (hit.SourceActorId, hit.ProjectileId);
                events.Add(new FrontlineMatchEvent
                {
                    Tick = State.Tick,
                    Type = FrontlineMatchEventType.Damage,
                    TeamId = target.ActorId.TeamId,
                    ActorId = target.ActorId,
                    OtherActorId = hit.SourceActorId,
                    ProjectileId = hit.ProjectileId,
                    From = target.Position,
                    To = target.Position,
                    Amount = actualDamage,
                    NewHealth = remainingHealth,
                });
            }
            target.Health = remainingHealth;
        }

        foreach (FrontlineTeamState team in State.Teams.OrderBy(team => team.TeamId))
        {
            foreach (FrontlineUnitState unit in team.Units.OrderBy(unit => unit.UnitId))
            {
                FrontlineLifeState? life = unit.ActiveLife;
                if (life is null || life.Health > 0)
                    continue;

                int respawnAtTick = checked(
                    State.Tick + 1 + _frontlineRules.PrimeRespawnTicks);
                FrontlineActorId? sourceActorId = null;
                long? sourceProjectileId = null;
                if (destructionCauses.TryGetValue(
                        life.ActorId,
                        out var cause))
                {
                    sourceActorId = cause.SourceActorId;
                    sourceProjectileId = cause.ProjectileId;
                }
                events.Add(new FrontlineMatchEvent
                {
                    Tick = State.Tick,
                    Type = FrontlineMatchEventType.Destroyed,
                    TeamId = team.TeamId,
                    ActorId = life.ActorId,
                    OtherActorId = sourceActorId,
                    ProjectileId = sourceProjectileId,
                    From = life.Position,
                    To = life.Position,
                    NewHealth = 0,
                    LifecycleStatus = FrontlineLifecycleStatus.Respawning,
                    RespawnAtTick = respawnAtTick,
                });
                unit.ActiveLife = null;
                unit.LifecycleStatus = FrontlineLifecycleStatus.Respawning;
                unit.RespawnAtTick = respawnAtTick;
            }
        }
    }

    private void UpdateCooldownsAndEnergy(
        IReadOnlySet<FrontlineActorId> shotActors,
        int executedTick)
    {
        GameRules rules = _definition.Rules;
        foreach (FrontlineLifeState life in ActiveLives())
        {
            bool shot = shotActors.Contains(life.ActorId);
            life.Cooldown = shot
                ? _frontlineRules.PrimeForm.ShootCooldownTicks
                : Math.Max(0, life.Cooldown - 1);
            if (rules.MaxEnergy <= 0)
                continue;
            if (shot)
                life.Energy -= rules.ShotEnergyCost;
            if (rules.EnergyRegenTicks > 0
                && (executedTick + 1) % rules.EnergyRegenTicks == 0
                && life.Energy < rules.MaxEnergy)
            {
                life.Energy++;
            }
        }
    }

    private void ResolveObjective(List<FrontlineMatchEvent> events)
    {
        FrontlineControlState before = State.Control;
        FrontlineRegion activeRegion =
            _profile.Positions[before.ActivePositionIndex];
        var activeTiles = activeRegion.Tiles.ToHashSet();
        FrontlineTeamPresence presence =
            FrontlineTeamPresence.FromOccupyingTeamIds(
                ActiveLives()
                    .Where(life =>
                        _frontlineRules.PrimeForm.ObjectiveWeight > 0
                        && activeTiles.Contains(life.Position))
                    .Select(life => life.ActorId.TeamId));
        FrontlineControlStepResult step = FrontlineControlSystem.Step(
            _frontlineRules,
            before,
            State.Tick,
            presence);
        State.Control = step.State;

        if (step.Transition is FrontlinePositionAdvanced advanced)
        {
            events.Add(new FrontlineMatchEvent
            {
                Tick = State.Tick,
                Type = FrontlineMatchEventType.FrontlinePositionAdvanced,
                TeamId = advanced.TeamId,
                FromPositionIndex = advanced.FromPositionIndex,
                ToPositionIndex = advanced.ToPositionIndex,
                ClaimingTeamId = null,
                CaptureProgress = 0,
                ControlResumesAtTick = step.State.ControlResumesAtTick,
            });
        }
        else if (step.Transition is FrontlineBaseBreached breached)
        {
            events.Add(new FrontlineMatchEvent
            {
                Tick = State.Tick,
                Type = FrontlineMatchEventType.BaseBreached,
                TeamId = breached.TeamId,
                FromPositionIndex = breached.BreachedFromPositionIndex,
                ToPositionIndex = breached.BreachedFromPositionIndex,
                ClaimingTeamId = null,
                CaptureProgress = 0,
            });
        }
        else if (before.ClaimingTeamId != step.State.ClaimingTeamId
                 || before.CaptureProgress != step.State.CaptureProgress
                 || before.DecayTicksElapsed != step.State.DecayTicksElapsed)
        {
            events.Add(new FrontlineMatchEvent
            {
                Tick = State.Tick,
                Type = FrontlineMatchEventType.FrontlineProgressChanged,
                TeamId = step.State.ClaimingTeamId,
                FromPositionIndex = step.State.ActivePositionIndex,
                ToPositionIndex = step.State.ActivePositionIndex,
                ClaimingTeamId = step.State.ClaimingTeamId,
                CaptureProgress = step.State.CaptureProgress,
                ControlResumesAtTick = step.State.ControlResumesAtTick,
            });
        }
    }

    private FrontlineMatchResult Complete(
        FrontlineMatchEndReason reason,
        int endTick)
    {
        long score = TerritorialScore();
        int? winnerTeamId = reason == FrontlineMatchEndReason.BaseBreach
            ? State.Control.WinnerTeamId
            : score switch
            {
                > 0 => 0,
                < 0 => 1,
                _ => null,
            };
        ImmutableArray<FrontlineTeamMatchResult> teams = State.Teams
            .OrderBy(team => team.TeamId)
            .Select(team =>
            {
                FrontlineUnitState prime = team.GetUnit(0);
                return new FrontlineTeamMatchResult(
                    team.TeamId,
                    winnerTeamId is null
                        ? FrontlineTeamOutcome.Draw
                        : team.TeamId == winnerTeamId
                            ? FrontlineTeamOutcome.Win
                            : FrontlineTeamOutcome.Loss,
                    prime.ActiveLife?.Health ?? 0,
                    prime.DamageDealt,
                    prime.LifecycleStatus);
            })
            .ToImmutableArray();
        var result = new FrontlineMatchResult(
            winnerTeamId,
            reason,
            endTick,
            score,
            State.Control,
            teams);
        State.Result = result;
        return result;
    }

    private long TerritorialScore()
    {
        int centre = _frontlineRules.FrontlinePositionCount / 2;
        long positionScore =
            (long)(State.Control.ActivePositionIndex - centre)
            * _frontlineRules.CaptureThreshold;
        int claimScore = State.Control.ClaimingTeamId switch
        {
            0 => State.Control.CaptureProgress,
            1 => -State.Control.CaptureProgress,
            _ => 0,
        };
        return positionScore + claimScore;
    }

    private ProjectileContact ContactAt(
        FrontlineActorId ownerActorId,
        Position position)
    {
        foreach (FrontlineLifeState life in ActiveLives())
        {
            if (life.Position != position || life.ActorId == ownerActorId)
                continue;
            if (life.ActorId.TeamId != ownerActorId.TeamId)
                return new ProjectileContact(life.ActorId, true, true);
            if (_frontlineRules.FriendlyFireEnabled)
                return new ProjectileContact(life.ActorId, true, true);
            if (_frontlineRules.AlliedProjectilesBlock)
                return new ProjectileContact(life.ActorId, false, true);
        }
        return ProjectileContact.None;
    }

    private static void AddPendingHit(
        FrontlineActorId ownerActorId,
        long projectileId,
        ProjectileContact contact,
        List<PendingHit> pendingHits)
    {
        if (!contact.CausesDamage || contact.ActorId is not FrontlineActorId target)
            return;
        pendingHits.Add(new PendingHit(
            target,
            ownerActorId,
            projectileId,
            pendingHits.Count));
    }

    private FrontlineMatchEvent ShotEvent(
        FrontlineLifeState shooter,
        Position to,
        long? projectileId,
        FrontlineActorId? target,
        ProjectileHeading? heading,
        ShotProgram? program) =>
        new()
        {
            Tick = State.Tick,
            Type = FrontlineMatchEventType.Shot,
            TeamId = shooter.ActorId.TeamId,
            ActorId = shooter.ActorId,
            OtherActorId = target,
            ProjectileId = projectileId,
            From = shooter.Position,
            To = to,
            FromFacing = shooter.Facing,
            ToFacing = shooter.Facing,
            ProjectileHeading = heading,
            ShotProgram = program,
            Action = BotAction.Shoot,
            ActionResult = ActionResult.Success,
        };

    private bool IsOpposingProtectedPad(int teamId, Position position) =>
        _profile.TeamHomes.Any(home =>
            home.TeamId != teamId
            && home.ProtectedSpawnPad.Contains(position));

    private FrontlineTeamHome Home(int teamId) =>
        _profile.TeamHomes.Single(home => home.TeamId == teamId);

    private IReadOnlyList<FrontlineActorId> ActiveActorIds() =>
        ActiveLives()
            .Select(life => life.ActorId)
            .ToImmutableArray();

    private IEnumerable<FrontlineLifeState> ActiveLives() =>
        State.Teams
            .OrderBy(team => team.TeamId)
            .SelectMany(team => team.Units.OrderBy(unit => unit.UnitId))
            .Select(unit => unit.ActiveLife)
            .Where(life => life is not null)
            .Select(life => life!)
            .OrderBy(life => life.ActorId);

    private FrontlineLifeState? TryGetActiveLife(FrontlineActorId actorId)
    {
        FrontlineUnitState unit;
        try
        {
            unit = State.GetUnit(actorId.TeamId, actorId.UnitId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        return unit.ActiveLife?.ActorId == actorId
            ? unit.ActiveLife
            : null;
    }

    private readonly record struct PendingHit(
        FrontlineActorId TargetActorId,
        FrontlineActorId SourceActorId,
        long ProjectileId,
        int Sequence);

    private readonly record struct ProjectileContact(
        FrontlineActorId? ActorId,
        bool CausesDamage,
        bool Consumes)
    {
        public static ProjectileContact None => new(null, false, false);
    }
}
