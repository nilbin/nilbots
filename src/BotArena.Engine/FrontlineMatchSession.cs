using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Headless Frontline simulation. The session owns stable unit slots,
/// lifecycle, and entity-action resolution but not runtimes, observations, or
/// replay. Callers prepare a tick to discover the exact active life
/// identities, then submit one keyed joint decision for that frozen actor set.
/// </summary>
public sealed class FrontlineMatchSession
{
    private readonly ResolvedMatchDefinition _definition;
    private readonly FrontlineRules _frontlineRules;
    private readonly FrontlineMapProfile _profile;
    private readonly PublicMatchContractManifest _contract;
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
        _contract = PublicRulesManifestFactory.CreateMatchContract(
            _definition.Rules,
            _definition.Map,
            _definition.Topology);
        ValidateSupportedDefinition();
        State = null!;
        Reset();
    }

    public FrontlineMatchState State { get; private set; }
    public bool IsCompleted => State.IsCompleted;
    public FrontlineMatchResult? Result => State.Result;

    /// <summary>
    /// Restores authored Prime lives, locked child slots, tick zero, and
    /// centre control.
    /// </summary>
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
                var units = new List<FrontlineUnitState>
                {
                    new(
                        home.TeamId,
                        unitId: 0,
                        _frontlineRules.PrimeForm.FormId,
                        life,
                        nextLifeId: 1),
                };
                for (int unitId = 1;
                     unitId < _frontlineRules.MaxUnitsPerTeam;
                     unitId++)
                {
                    units.Add(new FrontlineUnitState(
                        home.TeamId,
                        unitId,
                        _frontlineRules.ChildForm.FormId,
                        activeLife: null,
                        nextLifeId: 0,
                        lifecycleStatus: FrontlineLifecycleStatus.Locked)
                    {
                        UnlockAtTick =
                            _frontlineRules.FabricationUnlockTicks[unitId - 1],
                    });
                }
                return new FrontlineTeamState(home.TeamId, units);
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
    /// Applies due tick-start unlock, rebuild-ready, fabrication, and respawn
    /// transitions once, then freezes the exact actor keys required by
    /// <see cref="StepActors"/>.
    /// Repeated calls before Step are idempotent.
    /// </summary>
    public FrontlineTickStart PrepareTick()
    {
        if (IsCompleted)
            throw new InvalidOperationException("Frontline match already completed.");
        if (_preparedTick is not null)
            return _preparedTick;

        var events = new List<FrontlineMatchEvent>();
        var respawned = new List<FrontlineActorId>();
        var spawned = new List<FrontlineLifeSpawn>();
        foreach (FrontlineTeamState team in State.Teams.OrderBy(team => team.TeamId))
        {
            foreach (FrontlineUnitState unit in team.Units.OrderBy(unit => unit.UnitId))
            {
                ApplyTickStartLifecycle(
                    unit,
                    respawned,
                    spawned,
                    events);
            }
        }

        _preparedTick = new FrontlineTickStart(
            State.Tick,
            ActiveActorIds(),
            respawned.Order().ToImmutableArray(),
            events.ToImmutableArray())
        {
            SpawnedLives = spawned
                .OrderBy(item => item.ActorId)
                .ToImmutableArray(),
        };
        return _preparedTick;
    }

    /// <summary>
    /// Compatibility adapter for Package 3 callers. Entity actions use the
    /// <see cref="ActorDecision"/> overload and never extend
    /// <see cref="BotAction"/>.
    /// </summary>
    public FrontlineStepResult Step(
        IReadOnlyDictionary<FrontlineActorId, BotDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return StepActors(decisions.ToDictionary(
            pair => pair.Key,
            pair => ToActorDecision(pair.Value)));
    }

    /// <summary>Resolves one prepared, stable-keyed joint entity action.</summary>
    public FrontlineStepResult StepActors(
        IReadOnlyDictionary<FrontlineActorId, ActorDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        if (IsCompleted)
            throw new InvalidOperationException("Frontline match already completed.");
        FrontlineTickStart tickStart = _preparedTick
            ?? throw new InvalidOperationException(
                "PrepareTick must be called before Step.");
        Dictionary<FrontlineActorId, ActorDecision> frozenDecisions =
            decisions.ToDictionary(entry => entry.Key, entry => entry.Value);
        ValidateDecisionKeys(tickStart, frozenDecisions);
        Dictionary<FrontlineActorId, ActorDecision> canonicalDecisions =
            frozenDecisions.ToDictionary(
                pair => pair.Key,
                pair => ActorDecisionAdapter.Normalize(
                    pair.Value,
                    _contract,
                    ActorIdentity.FromFrontline(pair.Key)));

        var resolutions = ValidateActions(
            tickStart.ActiveActors,
            canonicalDecisions);
        // Tick-start lifecycle facts remain phase-distinct on TickStart. The
        // resolution list contains only facts produced after decisions are
        // accepted, preventing replay-v2 from duplicating respawn events.
        var events = new List<FrontlineMatchEvent>();
        var traversals = new List<FrontlineProjectileTraversal>();
        int executedTick = State.Tick;

        ResolveTurns(resolutions, events);
        ResolveMovement(resolutions, events);
        ResolveFabrication(resolutions, events);

        var pendingHits = new List<PendingHit>();
        AdvanceProjectiles(pendingHits, traversals);
        HashSet<FrontlineActorId> shotActors = ResolveShooting(
            resolutions,
            canonicalDecisions,
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

    private void ApplyTickStartLifecycle(
        FrontlineUnitState unit,
        ICollection<FrontlineActorId> respawned,
        ICollection<FrontlineLifeSpawn> spawned,
        ICollection<FrontlineMatchEvent> events)
    {
        if (unit.LifecycleStatus == FrontlineLifecycleStatus.Locked)
        {
            int unlockAtTick = unit.UnlockAtTick
                ?? throw new InvalidOperationException(
                    $"Locked unit {unit.TeamId}:{unit.UnitId} has no unlock tick.");
            EnsureLifecycleTickNotMissed(unit, unlockAtTick, "unlock");
            if (unlockAtTick == State.Tick)
            {
                unit.LifecycleStatus = FrontlineLifecycleStatus.Ready;
                events.Add(new FrontlineMatchEvent
                {
                    Tick = State.Tick,
                    Type = FrontlineMatchEventType.FabricationUnlocked,
                    TeamId = unit.TeamId,
                    UnitId = unit.UnitId,
                    LifecycleStatus = FrontlineLifecycleStatus.Ready,
                    UnlockAtTick = unlockAtTick,
                });
            }
            return;
        }

        if (unit.LifecycleStatus == FrontlineLifecycleStatus.Rebuilding)
        {
            int readyAtTick = unit.RebuildReadyAtTick
                ?? throw new InvalidOperationException(
                    $"Rebuilding unit {unit.TeamId}:{unit.UnitId} has no ready tick.");
            EnsureLifecycleTickNotMissed(unit, readyAtTick, "rebuild");
            if (readyAtTick == State.Tick)
            {
                unit.LifecycleStatus = FrontlineLifecycleStatus.Ready;
                unit.RebuildReadyAtTick = null;
                events.Add(new FrontlineMatchEvent
                {
                    Tick = State.Tick,
                    Type = FrontlineMatchEventType.RebuildReady,
                    TeamId = unit.TeamId,
                    UnitId = unit.UnitId,
                    LifecycleStatus = FrontlineLifecycleStatus.Ready,
                    RebuildReadyAtTick = readyAtTick,
                });
            }
            return;
        }

        if (unit.LifecycleStatus == FrontlineLifecycleStatus.FabricationQueued)
        {
            int fabricationAtTick = unit.FabricationAtTick
                ?? throw new InvalidOperationException(
                    $"Queued unit {unit.TeamId}:{unit.UnitId} has no fabrication tick.");
            EnsureLifecycleTickNotMissed(unit, fabricationAtTick, "fabrication");
            if (fabricationAtTick == State.Tick)
            {
                Position spawn = unit.ReservedSpawn
                    ?? throw new InvalidOperationException(
                        $"Queued unit {unit.TeamId}:{unit.UnitId} has no reserved spawn.");
                ActorSpawnReason reason = unit.PendingSpawnReason
                    ?? throw new InvalidOperationException(
                        $"Queued unit {unit.TeamId}:{unit.UnitId} has no spawn reason.");
                CreateLife(unit, spawn, Home(unit.TeamId).PrimeSpawn.Facing);
                unit.FabricationAtTick = null;
                unit.ReservedSpawn = null;
                unit.PendingSpawnReason = null;
                unit.HasSpawned = true;
                spawned.Add(new FrontlineLifeSpawn(
                    unit.ActiveLife!.ActorId,
                    reason));
                events.Add(new FrontlineMatchEvent
                {
                    Tick = State.Tick,
                    Type = FrontlineMatchEventType.Fabricated,
                    TeamId = unit.TeamId,
                    UnitId = unit.UnitId,
                    ActorId = unit.ActiveLife.ActorId,
                    To = unit.ActiveLife.Position,
                    ToFacing = unit.ActiveLife.Facing,
                    NewHealth = unit.ActiveLife.Health,
                    LifecycleStatus = FrontlineLifecycleStatus.Active,
                    SpawnReason = reason,
                    FabricationAtTick = fabricationAtTick,
                });
            }
            return;
        }

        if (unit.LifecycleStatus != FrontlineLifecycleStatus.Respawning)
            return;

        int dueTick = unit.RespawnAtTick
            ?? throw new InvalidOperationException(
                $"Respawning unit {unit.TeamId}:{unit.UnitId} has no respawn tick.");
        EnsureLifecycleTickNotMissed(unit, dueTick, "respawn");
        if (dueTick != State.Tick)
            return;

        FrontlineTeamHome home = Home(unit.TeamId);
        CreateLife(
            unit,
            new Position(home.PrimeSpawn.X, home.PrimeSpawn.Y),
            home.PrimeSpawn.Facing);
        unit.RespawnAtTick = null;
        respawned.Add(unit.ActiveLife!.ActorId);
        spawned.Add(new FrontlineLifeSpawn(
            unit.ActiveLife.ActorId,
            ActorSpawnReason.Respawn));
        events.Add(new FrontlineMatchEvent
        {
            Tick = State.Tick,
            Type = FrontlineMatchEventType.Respawned,
            TeamId = unit.TeamId,
            UnitId = unit.UnitId,
            ActorId = unit.ActiveLife.ActorId,
            To = unit.ActiveLife.Position,
            ToFacing = unit.ActiveLife.Facing,
            NewHealth = unit.ActiveLife.Health,
            LifecycleStatus = FrontlineLifecycleStatus.Active,
            SpawnReason = ActorSpawnReason.Respawn,
        });
    }

    private void EnsureLifecycleTickNotMissed(
        FrontlineUnitState unit,
        int dueTick,
        string transition)
    {
        if (dueTick < State.Tick)
        {
            throw new InvalidOperationException(
                $"Unit {unit.TeamId}:{unit.UnitId} missed {transition} tick {dueTick}.");
        }
    }

    private void CreateLife(
        FrontlineUnitState unit,
        Position position,
        Direction facing)
    {
        if (unit.ActiveLife is not null)
        {
            throw new InvalidOperationException(
                $"Unit {unit.TeamId}:{unit.UnitId} already has an active life.");
        }
        if (ActiveLives().Any(life => life.Position == position))
        {
            throw new InvalidOperationException(
                $"Lifecycle spawn tile {position} is occupied.");
        }
        var actorId = new FrontlineActorId(
            unit.TeamId,
            unit.UnitId,
            unit.NextLifeId);
        unit.NextLifeId++;
        UnitFormRules form = FormFor(unit);
        unit.ActiveLife = new FrontlineLifeState(
            actorId,
            position,
            facing,
            form.MaxHealth,
            State.Tick,
            _definition.Rules.MaxEnergy);
        unit.LifecycleStatus = FrontlineLifecycleStatus.Active;
    }

    private static ActorDecision ToActorDecision(BotDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Faulted)
            return ActorDecision.Fault(decision.FaultMessage ?? "fault");
        ActorActionPayload? payload = decision.ShotProgram is null
            ? null
            : new ActorActionPayload { ShotProgram = decision.ShotProgram };
        string actionId = decision.Action switch
        {
            BotAction.Wait => PublicActionIds.Wait,
            BotAction.MoveForward => PublicActionIds.MoveForward,
            BotAction.TurnLeft => PublicActionIds.TurnLeft,
            BotAction.TurnRight => PublicActionIds.TurnRight,
            BotAction.Shoot => PublicActionIds.Shoot,
            BotAction.StrafeLeft => PublicActionIds.StrafeLeft,
            BotAction.StrafeRight => PublicActionIds.StrafeRight,
            _ => throw new ArgumentException(
                "Decision contains an unknown historical action.",
                nameof(decision)),
        };
        return ActorDecision.Of(
            actionId,
            (int)decision.Action,
            payload,
            decision.DebugMessage);
    }

    private void ValidateSupportedDefinition()
    {
        GameRules rules = _definition.Rules;
        if (_frontlineRules.InitialUnitsPerTeam != 1
            || _frontlineRules.MaxUnitsPerTeam < 1)
        {
            throw new NotSupportedException(
                "Frontline replication requires one initial Prime per team.");
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
            && (_frontlineRules.PrimeForm.AllowsProgrammedShots
                || _frontlineRules.ChildForm.AllowsProgrammedShots)
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
                "The Frontline session requires a positive Prime objective weight.");
        }
        if (_frontlineRules.PrimeForm.OmnidirectionalShooting)
        {
            throw new NotSupportedException(
                "Frontline Package 5 has no omnidirectional shooting action.");
        }
        if (!_frontlineRules.ChildForm.CanMove
            || !_frontlineRules.ChildForm.CanShoot
            || _frontlineRules.ChildForm.OmnidirectionalShooting)
        {
            throw new NotSupportedException(
                "Package 5 supports only the mobile child form; Anchor remains out of scope.");
        }
    }

    private void ValidateDecisionKeys(
        FrontlineTickStart tickStart,
        IReadOnlyDictionary<FrontlineActorId, ActorDecision> decisions)
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
            ActorDecision? decision = decisions[actorId];
            if (decision is null)
            {
                throw new ArgumentException(
                    $"Decision for actor {actorId} cannot be null.",
                    nameof(decisions));
            }
            if (decision.Faulted)
            {
                throw new ArgumentException(
                    "Runtime faults are outside the headless Frontline session.",
                    nameof(decisions));
            }
        }
    }

    private Dictionary<FrontlineActorId, FrontlineActionResolution> ValidateActions(
        IReadOnlyList<FrontlineActorId> actors,
        IReadOnlyDictionary<FrontlineActorId, ActorDecision> decisions)
    {
        var resolutions = new Dictionary<
            FrontlineActorId,
            FrontlineActionResolution>();
        GameRules rules = _definition.Rules;
        foreach (FrontlineActorId actorId in actors)
        {
            ActorDecision decision = decisions[actorId];
            string chosenActionId = decision.ActionId!;
            int chosenActionCode = decision.ActionCode!.Value;
            ActorActionPayload? chosenPayload = decision.Payload;
            FrontlineLifeState life = State.GetActiveLife(actorId);
            FrontlineUnitState unit = State.GetUnit(
                actorId.TeamId,
                actorId.UnitId);
            UnitFormRules form = FormFor(unit);
            string validatedActionId = chosenActionId;
            int validatedActionCode = chosenActionCode;
            ActorActionPayload? validatedPayload = chosenPayload;
            ActionResult result = ActionResult.Success;
            BotAction? legacyAction =
                Enum.IsDefined(typeof(BotAction), chosenActionCode)
                    ? (BotAction)chosenActionCode
                    : null;

            void Block(ActionResult blockedResult = ActionResult.Blocked)
            {
                validatedActionId = PublicActionIds.Wait;
                validatedActionCode = (int)BotAction.Wait;
                validatedPayload = null;
                result = blockedResult;
            }

            bool movement = legacyAction is
                BotAction.MoveForward or
                BotAction.StrafeLeft or
                BotAction.StrafeRight;
            if (movement && !form.CanMove)
            {
                Block();
            }
            else if (legacyAction is BotAction.StrafeLeft or BotAction.StrafeRight
                     && !rules.AllowStrafe)
            {
                Block();
            }
            else if (legacyAction == BotAction.Shoot && !form.CanShoot)
            {
                Block();
            }
            else if (chosenPayload?.ShotProgram is not null
                     && (!rules.AllowProgrammedShots
                         || !form.AllowsProgrammedShots))
            {
                Block();
            }
            else if (legacyAction == BotAction.Shoot && life.Cooldown > 0)
            {
                Block(ActionResult.OnCooldown);
            }
            else if (legacyAction == BotAction.Shoot
                     && rules.MaxEnergy > 0
                     && life.Energy < rules.ShotEnergyCost)
            {
                Block(ActionResult.OnCooldown);
            }
            else if (string.Equals(
                         chosenActionId,
                         PublicActionIds.Fabricate,
                         StringComparison.Ordinal))
            {
                PublicFrontlineFabricationDefinition fabrication =
                    _contract.Rules.Frontline!.Fabrication;
                ObservedUnitTarget target = chosenPayload!.UnitTarget!.Value;
                if (target.TeamId != actorId.TeamId
                    || target.UnitId == fabrication.FabricatorUnitId
                    || !State.GetTeam(actorId.TeamId).Units.Any(
                        candidate => candidate.UnitId == target.UnitId))
                {
                    throw new ArgumentException(
                        $"Actor {actorId} submitted an invalid fabrication target.");
                }

                FrontlineUnitState targetUnit = State.GetUnit(
                    target.TeamId,
                    target.UnitId);
                if (actorId.UnitId != fabrication.FabricatorUnitId
                    || !string.Equals(
                        unit.FormId,
                        fabrication.FabricatorFormId,
                        StringComparison.Ordinal)
                    || targetUnit.LifecycleStatus
                        != FrontlineLifecycleStatus.Ready)
                {
                    Block();
                }
                else if (!Home(actorId.TeamId).ProtectedSpawnPad.Contains(
                             life.Position))
                {
                    Block();
                }
            }
            else if (legacyAction is null)
            {
                throw new InvalidOperationException(
                    $"Action '{chosenActionId}' has no Package 5 resolver.");
            }

            resolutions.Add(
                actorId,
                new FrontlineActionResolution(
                    actorId,
                    chosenActionId,
                    chosenActionCode,
                    chosenPayload,
                    validatedActionId,
                    validatedActionCode,
                    validatedPayload,
                    result));
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
                || IsOpposingProtectedPad(actorId.TeamId, target)
                || IsReservedPrimeSpawn(actorId, target))
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

    private void ResolveFabrication(
        Dictionary<FrontlineActorId, FrontlineActionResolution> resolutions,
        List<FrontlineMatchEvent> events)
    {
        foreach (FrontlineActorId actorId in resolutions.Keys.Order())
        {
            FrontlineActionResolution resolution = resolutions[actorId];
            if (!string.Equals(
                    resolution.ValidatedActionId,
                    PublicActionIds.Fabricate,
                    StringComparison.Ordinal))
            {
                continue;
            }

            ObservedUnitTarget target =
                resolution.ValidatedPayload!.UnitTarget!.Value;
            FrontlineUnitState targetUnit = State.GetUnit(
                target.TeamId,
                target.UnitId);
            Position? spawn = SelectFabricationSpawn(actorId.TeamId);
            if (spawn is null)
            {
                resolutions[actorId] = resolution with
                {
                    ValidatedActionId = PublicActionIds.Wait,
                    ValidatedActionCode = (int)BotAction.Wait,
                    ValidatedPayload = null,
                    Result = ActionResult.Blocked,
                };
                continue;
            }

            int fabricationAtTick = checked(State.Tick + 1);
            ActorSpawnReason reason = targetUnit.HasSpawned
                ? ActorSpawnReason.Rebuild
                : ActorSpawnReason.Fabrication;
            targetUnit.LifecycleStatus =
                FrontlineLifecycleStatus.FabricationQueued;
            targetUnit.FabricationAtTick = fabricationAtTick;
            targetUnit.ReservedSpawn = spawn;
            targetUnit.PendingSpawnReason = reason;
            events.Add(new FrontlineMatchEvent
            {
                Tick = State.Tick,
                Type = FrontlineMatchEventType.FabricationQueued,
                TeamId = actorId.TeamId,
                UnitId = targetUnit.UnitId,
                ActorId = actorId,
                To = spawn,
                ActionId = PublicActionIds.Fabricate,
                ActionCode = PublicActionCodes.Fabricate,
                ActionPayload = resolution.ValidatedPayload,
                ActionResult = ActionResult.Success,
                LifecycleStatus =
                    FrontlineLifecycleStatus.FabricationQueued,
                SpawnReason = reason,
                FabricationAtTick = fabricationAtTick,
            });
        }
    }

    private Position? SelectFabricationSpawn(int teamId)
    {
        FrontlineTeamHome home = Home(teamId);
        var primeSpawn = new Position(
            home.PrimeSpawn.X,
            home.PrimeSpawn.Y);
        HashSet<Position> unavailable = ActiveLives()
            .Select(life => life.Position)
            .Concat(State.Teams
                .SelectMany(team => team.Units)
                .Where(unit => unit.ReservedSpawn is not null)
                .Select(unit => unit.ReservedSpawn!.Value))
            .ToHashSet();
        foreach (Position position in home.ProtectedSpawnPad
                     .OrderBy(position => position.Y)
                     .ThenBy(position => position.X))
        {
            if (position != primeSpawn
                && !_definition.Map.IsWall(position)
                && !unavailable.Contains(position))
            {
                return position;
            }
        }
        return null;
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
        IReadOnlyDictionary<FrontlineActorId, ActorDecision> decisions,
        List<PendingHit> pendingHits,
        List<FrontlineMatchEvent> events,
        List<FrontlineProjectileTraversal> traversals)
    {
        var shotActors = new HashSet<FrontlineActorId>();
        foreach (FrontlineActionResolution resolution in resolutions.Values
                     .OrderBy(resolution => resolution.ActorId))
        {
            if (!string.Equals(
                    resolution.ValidatedActionId,
                    PublicActionIds.Shoot,
                    StringComparison.Ordinal))
                continue;

            shotActors.Add(resolution.ActorId);
            FrontlineLifeState shooter = State.GetActiveLife(resolution.ActorId);
            UnitFormRules form = FormFor(State.GetUnit(
                resolution.ActorId.TeamId,
                resolution.ActorId.UnitId));
            if (_definition.Rules.AllowProgrammedShots
                && form.AllowsProgrammedShots)
            {
                ResolveProgrammedShot(
                    shooter,
                    decisions[resolution.ActorId].Payload?.ShotProgram
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

                bool isPrime = unit.UnitId == 0;
                int readyAtTick = checked(
                    State.Tick
                    + 1
                    + (isPrime
                        ? _frontlineRules.PrimeRespawnTicks
                        : _frontlineRules.ChildRebuildTicks));
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
                    UnitId = unit.UnitId,
                    ActorId = life.ActorId,
                    OtherActorId = sourceActorId,
                    ProjectileId = sourceProjectileId,
                    From = life.Position,
                    To = life.Position,
                    NewHealth = 0,
                    LifecycleStatus = isPrime
                        ? FrontlineLifecycleStatus.Respawning
                        : FrontlineLifecycleStatus.Rebuilding,
                    RespawnAtTick = isPrime ? readyAtTick : null,
                    RebuildReadyAtTick = isPrime ? null : readyAtTick,
                });
                unit.ActiveLife = null;
                unit.LifecycleStatus = isPrime
                    ? FrontlineLifecycleStatus.Respawning
                    : FrontlineLifecycleStatus.Rebuilding;
                unit.RespawnAtTick = isPrime ? readyAtTick : null;
                unit.RebuildReadyAtTick = isPrime ? null : readyAtTick;
                unit.FabricationAtTick = null;
                unit.ReservedSpawn = null;
                unit.PendingSpawnReason = null;
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
            UnitFormRules form = FormFor(State.GetUnit(
                life.ActorId.TeamId,
                life.ActorId.UnitId));
            life.Cooldown = shot
                ? form.ShootCooldownTicks
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
                        FormFor(State.GetUnit(
                                life.ActorId.TeamId,
                                life.ActorId.UnitId))
                            .ObjectiveWeight > 0
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
                > 0 => TeamAdvancing(1),
                < 0 => TeamAdvancing(-1),
                _ => null,
            };
        ImmutableArray<FrontlineTeamMatchResult> teams = State.Teams
            .OrderBy(team => team.TeamId)
            .Select(team =>
            {
                ImmutableArray<FrontlineUnitMatchResult> units = team.Units
                    .OrderBy(unit => unit.UnitId)
                    .Select(unit => new FrontlineUnitMatchResult(
                        team.TeamId,
                        unit.UnitId,
                        unit.FormId,
                        unit.LifecycleStatus,
                        unit.ActiveLife?.ActorId,
                        unit.ActiveLife?.Health ?? 0,
                        unit.DamageDealt))
                    .ToImmutableArray();
                return new FrontlineTeamMatchResult(
                    team.TeamId,
                    winnerTeamId is null
                        ? FrontlineTeamOutcome.Draw
                        : team.TeamId == winnerTeamId
                            ? FrontlineTeamOutcome.Win
                            : FrontlineTeamOutcome.Loss,
                    units.Sum(unit => unit.Health),
                    team.DamageDealt,
                    units);
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
        int claimScore = State.Control.ClaimingTeamId is int claimingTeamId
            ? _contract.Rules.Frontline!.Victory.TeamAdvances
                .Single(advance => advance.TeamId == claimingTeamId)
                .PositionIndexDelta
                * State.Control.CaptureProgress
            : 0;
        return positionScore + claimScore;
    }

    private int TeamAdvancing(int positionIndexDelta) =>
        _contract.Rules.Frontline!.Victory.TeamAdvances
            .Single(advance =>
                advance.PositionIndexDelta == positionIndexDelta)
            .TeamId;

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

    private bool IsReservedPrimeSpawn(
        FrontlineActorId actorId,
        Position position)
    {
        if (actorId.UnitId == 0)
            return false;
        FrontlineTeamHome home = Home(actorId.TeamId);
        return position == new Position(
            home.PrimeSpawn.X,
            home.PrimeSpawn.Y);
    }

    private FrontlineTeamHome Home(int teamId) =>
        _profile.TeamHomes.Single(home => home.TeamId == teamId);

    private UnitFormRules FormFor(FrontlineUnitState unit)
    {
        if (string.Equals(
                unit.FormId,
                _frontlineRules.PrimeForm.FormId,
                StringComparison.Ordinal))
        {
            return _frontlineRules.PrimeForm;
        }
        if (string.Equals(
                unit.FormId,
                _frontlineRules.ChildForm.FormId,
                StringComparison.Ordinal))
        {
            return _frontlineRules.ChildForm;
        }
        if (string.Equals(
                unit.FormId,
                _frontlineRules.TurretForm.FormId,
                StringComparison.Ordinal))
        {
            return _frontlineRules.TurretForm;
        }
        throw new InvalidOperationException(
            $"Unit {unit.TeamId}:{unit.UnitId} uses unknown form '{unit.FormId}'.");
    }

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
