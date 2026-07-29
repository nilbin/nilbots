namespace BotArena.Engine;

/// <summary>
/// Cross-catalog generation-3 actor-rules validation. Standalone definitions
/// validate local shape; this boundary proves that every reference resolves
/// to one compatible, admitted semantic definition.
/// </summary>
public static class ActorRulesDefinitionValidator
{
    private const int MaxSemanticIdLength = 64;
    private const string FabricateActionId = "fabricate";
    private const int FabricateActionCode = 100;
    private const string SplitActionId = "split";
    private const int SplitActionCode = 103;

    public static void Validate(ActorRulesDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateComponents(
            definition.RulesetId,
            definition.Limits,
            definition.SeedMechanics,
            definition.GameMode,
            definition.Lifecycle,
            definition.Forms,
            definition.MovementProfiles,
            definition.VisionProfiles,
            definition.AttackProfiles,
            definition.Actions,
            definition.FabricationTransitions,
            definition.SameLifeTransitions,
            definition.ReplicationTransitions);
    }

    internal static void ValidateComponents(
        string rulesetId,
        ActorRulesLimits limits,
        ActorSeedMechanicsDefinition seedMechanics,
        GameModeDefinition gameMode,
        ActorLifecycleDefinition lifecycle,
        IReadOnlyList<ActorFormDefinition> forms,
        IReadOnlyList<ActorMovementProfileDefinition> movementProfiles,
        IReadOnlyList<ActorVisionProfileDefinition> visionProfiles,
        IReadOnlyList<ActorAttackProfileDefinition> attackProfiles,
        IReadOnlyList<ActorActionDefinition> actions,
        IReadOnlyList<ActorFabricationTransitionDefinition>
            fabricationTransitions,
        IReadOnlyList<ActorSameLifeTransitionDefinition> sameLifeTransitions,
        IReadOnlyList<ActorReplicationTransitionDefinition>
            replicationTransitions)
    {
        var errors = new List<string>();
        ValidateCanonicalId(rulesetId, "Ruleset ID", errors);
        ValidateCanonicalId(
            seedMechanics.SeedProfileId,
            "Seed profile ID",
            errors);
        ValidateCanonicalId(gameMode.ModeId, "Game mode ID", errors);
        if (gameMode is FrontlineGameModeDefinition frontline)
        {
            foreach (
                FrontlineCaptureGainPhaseDefinition phase
                in frontline.Capture.GainSchedule)
            {
                ValidateCanonicalId(
                    phase.PhaseId,
                    "Frontline capture gain phase ID",
                    errors);
                if (phase.StartsAtTick >= limits.MaxTicks)
                {
                    errors.Add(
                        $"Frontline capture gain phase '{phase.PhaseId}' must start before MaxTicks.");
                }
            }
        }

        Dictionary<string, ActorFormDefinition> formsById = IndexCatalog(
            forms,
            form => form.Id,
            "form",
            required: true,
            errors);
        Dictionary<string, ActorMovementProfileDefinition> movementById =
            IndexCatalog(
                movementProfiles,
                profile => profile.Id,
                "movement profile",
                required: true,
                errors);
        Dictionary<string, ActorVisionProfileDefinition> visionById =
            IndexCatalog(
                visionProfiles,
                profile => profile.Id,
                "vision profile",
                required: true,
                errors);
        Dictionary<string, ActorAttackProfileDefinition> attackById =
            IndexCatalog(
                attackProfiles,
                profile => profile.Id,
                "attack profile",
                required: true,
                errors);
        Dictionary<string, ActorActionDefinition> actionsById = IndexCatalog(
            actions,
            action => action.Id,
            "action",
            required: true,
            errors);

        ValidateActionCodes(actions, errors);
        ValidateGenericActionShapes(actions, errors);
        ValidateLifecycle(lifecycle, formsById, errors);
        ValidateGroundAdmission(movementProfiles, errors);
        ValidateFacingCouplingAdmission(movementProfiles, errors);
        ValidateCombatAdmission(attackProfiles, errors);
        ValidateObjectiveWeights(gameMode, forms, errors);
        ValidateCheckedTickArithmetic(
            limits,
            lifecycle,
            fabricationTransitions,
            sameLifeTransitions,
            replicationTransitions,
            gameMode,
            errors);

        var usedMovementProfiles = new HashSet<string>(
            StringComparer.Ordinal);
        var usedVisionProfiles = new HashSet<string>(StringComparer.Ordinal);
        var usedAttackProfiles = new HashSet<string>(StringComparer.Ordinal);
        var usedActions = new HashSet<string>(StringComparer.Ordinal);
        ValidateForms(
            forms,
            movementById,
            visionById,
            attackById,
            actionsById,
            usedMovementProfiles,
            usedVisionProfiles,
            usedAttackProfiles,
            usedActions,
            errors);

        ValidateWaitActions(forms, actions, actionsById, errors);
        ValidateUnusedCatalogEntries(
            movementProfiles,
            profile => profile.Id,
            "Movement profile",
            usedMovementProfiles,
            errors);
        ValidateUnusedCatalogEntries(
            visionProfiles,
            profile => profile.Id,
            "Vision profile",
            usedVisionProfiles,
            errors);
        ValidateUnusedCatalogEntries(
            attackProfiles,
            profile => profile.Id,
            "Attack profile",
            usedAttackProfiles,
            errors);
        ValidateUnusedCatalogEntries(
            actions,
            action => action.Id,
            "Action",
            usedActions,
            errors);

        ValidateSharedTransitionIds(
            fabricationTransitions,
            sameLifeTransitions,
            replicationTransitions,
            errors);

        var fabricationRoutes = new HashSet<(string Source, string Action)>();
        var sameLifeRoutes = new HashSet<
            (string Source, string Action, string Target)>();
        var replicationRoutes = new HashSet<(string Source, string Action)>();
        ValidateFabricationTransitions(
            fabricationTransitions,
            formsById,
            actionsById,
            fabricationRoutes,
            errors);
        ValidateSameLifeTransitions(
            sameLifeTransitions,
            formsById,
            actionsById,
            sameLifeRoutes,
            errors);
        ValidateReplicationTransitions(
            replicationTransitions,
            formsById,
            actionsById,
            replicationRoutes,
            errors);
        ValidateLifecycleActionRoutes(
            forms,
            actionsById,
            fabricationRoutes,
            sameLifeRoutes,
            replicationRoutes,
            errors);

        if (errors.Count > 0)
            throw new ActorRulesValidationException(errors);
    }

    private static void ValidateCombatAdmission(
        IEnumerable<ActorAttackProfileDefinition> attackProfiles,
        List<string> errors)
    {
        foreach (ActorAttackProfileDefinition? profile in attackProfiles)
        {
            if (profile is null)
                continue;
            ActorProjectileDefinition projectile = profile.Projectile;
            if (!projectile.DamageAppliedSimultaneously)
            {
                errors.Add(
                    $"Attack profile '{profile.Id}' contradicts the shared " +
                    "joint-damage batch.");
            }
            if (projectile.Mode == ActorProjectileMode.InstantRay
                && profile.ShotProgram.Enabled)
            {
                errors.Add(
                    $"Attack profile '{profile.Id}' cannot combine instant rays " +
                    "with programmed paths in schema 3.");
            }

            ActorShotProgramDefinition program = profile.ShotProgram;
            if (!program.Enabled
                && (program.MinInitialAimSteps != 0
                    || program.MaxInitialAimSteps != 0
                    || program.AimOnlyProgram
                        != new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0)
                    || program.AllowedCurvedBendDirections
                        .SequenceEqual([-1, 1]) is false
                    || program.MinBendAfterTiles != 1
                    || program.MaxBendAfterTiles != 1
                    || program.MinBendEveryTiles != 1
                    || program.MaxBendEveryTiles != 1
                    || program.MinBendCount != 1
                    || program.MaxBendCount != 1
                    || program.PayloadOptional
                    || program.DefaultProgram
                        != new ActorShotProgramValue(0, 0, 0, 1, 0)))
            {
                errors.Add(
                    $"Disabled shot program '{profile.Id}' must use the canonical " +
                    "inert bounds and straight default.");
            }
        }
    }

    private static void ValidateObjectiveWeights(
        GameModeDefinition gameMode,
        IEnumerable<ActorFormDefinition> forms,
        List<string> errors)
    {
        foreach (ActorFormDefinition? form in forms)
        {
            if (form is null)
                continue;
            if (gameMode is DeathmatchGameModeDefinition
                && form.ObjectiveWeight != 0)
            {
                errors.Add(
                    $"Deathmatch form '{form.Id}' must use objective weight zero.");
            }
            else if (gameMode is FrontlineGameModeDefinition
                     && form.ObjectiveWeight is not (0 or 1))
            {
                errors.Add(
                    $"Frontline form '{form.Id}' objective weight must be zero " +
                    "or one because positive presence does not stack.");
            }
        }
    }

    private static void ValidateCheckedTickArithmetic(
        ActorRulesLimits limits,
        ActorLifecycleDefinition lifecycle,
        IEnumerable<ActorFabricationTransitionDefinition>
            fabricationTransitions,
        IEnumerable<ActorSameLifeTransitionDefinition> sameLifeTransitions,
        IEnumerable<ActorReplicationTransitionDefinition>
            replicationTransitions,
        GameModeDefinition gameMode,
        List<string> errors)
    {
        int maxTicks = limits.MaxTicks;
        foreach (ActorLifecycleProfileDefinition profile
                 in lifecycle.Profiles)
        {
            ValidateMaximumDueTick(
                $"Lifecycle profile '{profile.ProfileId}'",
                maxTicks,
                1L + profile.DelayTicks,
                errors);
        }
        foreach (ActorSameLifeTransitionDefinition? transition
                 in sameLifeTransitions)
        {
            if (transition is null)
                continue;
            long offset = transition.Windup.Completion switch
            {
                ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .TickStartAfterDuration =>
                    transition.Windup.DurationTicks,
                ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                    .EndOfStartedTickPlusDurationMinusOneAfterModeUpdate =>
                    transition.Windup.DurationTicks - 1L,
                _ => -1,
            };
            if (offset < 0)
            {
                errors.Add(
                    $"Same-life transition '{transition.TransitionId}' uses an " +
                    "unsupported completion clock.");
            }
            else
            {
                ValidateMaximumDueTick(
                    $"Same-life transition '{transition.TransitionId}'",
                    maxTicks,
                    offset,
                    errors);
            }
        }
        foreach (ActorFabricationTransitionDefinition? transition
                 in fabricationTransitions)
        {
            if (transition is null)
                continue;
            if (transition is BoundedChildFabricationDefinition bounded)
            {
                ValidateMaximumDueTick(
                    $"Fabrication transition '{transition.TransitionId}'",
                    maxTicks,
                    bounded.Delay.DurationTicks,
                    errors);
            }
        }
        foreach (ActorReplicationTransitionDefinition? transition
                 in replicationTransitions)
        {
            if (transition is null)
                continue;
            if (transition is SplitReplicationTransitionDefinition split)
            {
                ValidateMaximumDueTick(
                    $"Replication transition '{transition.TransitionId}'",
                    maxTicks,
                    split.Windup.DurationTicks,
                    errors);
            }
        }
        if (gameMode is FrontlineGameModeDefinition frontline)
        {
            ValidateMaximumDueTick(
                "Frontline redeploy pause",
                maxTicks,
                1L + frontline.Capture.RedeployPauseTicks,
                errors);
        }
    }

    private static void ValidateMaximumDueTick(
        string owner,
        int maxTicks,
        long dueTickOffset,
        List<string> errors)
    {
        if (maxTicks - 1L + dueTickOffset > int.MaxValue)
        {
            errors.Add(
                $"{owner} can schedule a tick beyond the supported 32-bit range.");
        }
    }

    private static Dictionary<string, T> IndexCatalog<T>(
        IReadOnlyList<T> items,
        Func<T, string> idSelector,
        string catalogName,
        bool required,
        List<string> errors)
        where T : class
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        if (required && items.Count == 0)
        {
            errors.Add(
                $"The {catalogName} catalog must be initialized and non-empty.");
            return result;
        }

        foreach (T? item in items)
        {
            if (item is null)
            {
                errors.Add(
                    $"The {catalogName} catalog cannot contain null entries.");
                continue;
            }

            string id = idSelector(item);
            ValidateCanonicalId(
                id,
                $"{UppercaseFirst(catalogName)} ID",
                errors);
            if (!result.TryAdd(id, item))
            {
                errors.Add(
                    $"{UppercaseFirst(catalogName)} ID '{id}' is declared more than once.");
            }
        }

        return result;
    }

    private static void ValidateActionCodes(
        IReadOnlyList<ActorActionDefinition> actions,
        List<string> errors)
    {
        var codes = new Dictionary<int, string>();
        foreach (ActorActionDefinition? action in actions)
        {
            if (action is null)
                continue;
            if (!codes.TryAdd(action.Code, action.Id))
            {
                errors.Add(
                    $"Action code {action.Code} is shared by '{codes[action.Code]}' " +
                    $"and '{action.Id}'.");
            }
        }
    }

    private static void ValidateLifecycle(
        ActorLifecycleDefinition lifecycle,
        IReadOnlyDictionary<string, ActorFormDefinition> formsById,
        List<string> errors)
    {
        if (lifecycle.Profiles.IsDefaultOrEmpty)
        {
            errors.Add(
                "The lifecycle profile catalog must be initialized and non-empty.");
            return;
        }

        foreach (ActorLifecycleProfileDefinition? profile in
                 lifecycle.Profiles)
        {
            if (profile is null)
            {
                errors.Add(
                    "The lifecycle profile catalog cannot contain null entries.");
                continue;
            }

            ValidateCanonicalId(
                profile.ProfileId,
                "Lifecycle profile ID",
                errors);
            if (profile.AutomaticReturnFormId is not string returnFormId)
                continue;

            ValidateCanonicalId(
                returnFormId,
                $"Lifecycle profile '{profile.ProfileId}' return-form ID",
                errors);
            if (!formsById.ContainsKey(returnFormId))
            {
                errors.Add(
                    $"Lifecycle profile '{profile.ProfileId}' references unknown " +
                    $"return form '{returnFormId}'.");
            }
        }
    }

    private static void ValidateGroundAdmission(
        IReadOnlyList<ActorMovementProfileDefinition> movementProfiles,
        List<string> errors)
    {
        foreach (ActorMovementProfileDefinition? profile in movementProfiles)
        {
            if (profile is null)
                continue;
            if (profile.MovementLayer != ActorMovementLayer.Ground)
            {
                errors.Add(
                    $"Movement profile '{profile.Id}' selects '{profile.MovementLayer}', " +
                    "but actor rules schema 3 admits only implemented Ground semantics.");
            }
        }
    }

    private static void ValidateFacingCouplingAdmission(
        IReadOnlyList<ActorMovementProfileDefinition> movementProfiles,
        List<string> errors)
    {
        foreach (ActorMovementProfileDefinition? profile in movementProfiles)
        {
            if (profile is null)
                continue;
            if (!Enum.IsDefined(profile.FacingCoupling))
            {
                errors.Add(
                    $"Movement profile '{profile.Id}' selects undefined facing " +
                    $"coupling '{profile.FacingCoupling}'.");
            }
        }
    }

    private static void ValidateForms(
        IReadOnlyList<ActorFormDefinition> forms,
        IReadOnlyDictionary<string, ActorMovementProfileDefinition>
            movementById,
        IReadOnlyDictionary<string, ActorVisionProfileDefinition> visionById,
        IReadOnlyDictionary<string, ActorAttackProfileDefinition> attackById,
        IReadOnlyDictionary<string, ActorActionDefinition> actionsById,
        HashSet<string> usedMovementProfiles,
        HashSet<string> usedVisionProfiles,
        HashSet<string> usedAttackProfiles,
        HashSet<string> usedActions,
        List<string> errors)
    {
        foreach (ActorFormDefinition? form in forms)
        {
            if (form is null)
                continue;

            ValidateReference(
                form.MovementProfileId,
                $"Form '{form.Id}' movement-profile ID",
                movementById,
                usedMovementProfiles,
                errors);
            ValidateReference(
                form.VisionProfileId,
                $"Form '{form.Id}' vision-profile ID",
                visionById,
                usedVisionProfiles,
                errors);

            ActorAttackProfileDefinition? attackProfile = null;
            if (form.AttackProfileId is string attackProfileId)
            {
                ValidateReference(
                    attackProfileId,
                    $"Form '{form.Id}' attack-profile ID",
                    attackById,
                    usedAttackProfiles,
                    errors);
                attackById.TryGetValue(attackProfileId, out attackProfile);
            }

            bool allowsAttack = false;
            foreach (string actionId in form.AllowedActionIds)
            {
                ValidateCanonicalId(
                    actionId,
                    $"Form '{form.Id}' allowed-action ID",
                    errors);
                if (!actionsById.TryGetValue(actionId, out
                        ActorActionDefinition? action))
                {
                    errors.Add(
                        $"Form '{form.Id}' references unknown action '{actionId}'.");
                    continue;
                }

                usedActions.Add(actionId);
                if (action.Kind != ActorActionKind.Attack)
                    continue;

                allowsAttack = true;
                if (attackProfile is null)
                {
                    errors.Add(
                        $"Form '{form.Id}' permits Attack action '{actionId}' " +
                        "without an attack profile.");
                    continue;
                }
                ValidateAttackActionShape(
                    form,
                    action,
                    attackProfile,
                    errors);
            }

            if (attackProfile is not null && !allowsAttack)
            {
                errors.Add(
                    $"Form '{form.Id}' references attack profile " +
                    $"'{attackProfile.Id}' but permits no Attack action.");
            }
        }
    }

    private static void ValidateWaitActions(
        IReadOnlyList<ActorFormDefinition> forms,
        IReadOnlyList<ActorActionDefinition> actions,
        IReadOnlyDictionary<string, ActorActionDefinition> actionsById,
        List<string> errors)
    {
        ActorActionDefinition[] waits = actions
            .Where(action => action is not null
                && action.Kind == ActorActionKind.Wait)
            .ToArray();
        if (waits.Length == 0)
            errors.Add("Actor rules must declare at least one Wait action.");

        foreach (ActorFormDefinition? form in forms)
        {
            if (form is null)
                continue;
            bool permitsWait = form.AllowedActionIds.Any(
                actionId =>
                    actionsById.TryGetValue(
                        actionId,
                        out ActorActionDefinition? action)
                    && action.Kind == ActorActionKind.Wait);
            if (!permitsWait)
            {
                errors.Add(
                    $"Form '{form.Id}' must permit at least one Wait-kind action.");
            }
        }
    }

    private static void ValidateGenericActionShapes(
        IReadOnlyList<ActorActionDefinition> actions,
        List<string> errors)
    {
        foreach (ActorActionDefinition? action in actions)
        {
            if (action is null)
                continue;
            switch (action.Kind)
            {
                case ActorActionKind.Wait
                    when !action.ParameterKinds.IsEmpty:
                    errors.Add(
                        $"Wait action '{action.Id}' must be parameterless.");
                    break;
                case ActorActionKind.Movement
                    when !HasExactly(
                        action.ParameterKinds,
                        ActorActionParameterKind.Direction):
                    errors.Add(
                        $"Movement action '{action.Id}' must declare exactly " +
                        "Direction under actor rules schema 3.");
                    break;
                case ActorActionKind.Rotation
                    when !HasExactly(
                        action.ParameterKinds,
                        ActorActionParameterKind.Direction):
                    errors.Add(
                        $"Rotation action '{action.Id}' must declare exactly " +
                        "Direction under actor rules schema 3.");
                    break;
                case ActorActionKind.Attack
                    when !action.ParameterKinds.IsEmpty
                        && !HasExactly(
                            action.ParameterKinds,
                            ActorActionParameterKind.ShotProgram)
                        && !HasExactly(
                            action.ParameterKinds,
                            ActorActionParameterKind.ProjectileHeading):
                    errors.Add(
                        $"Attack action '{action.Id}' has no supported schema-3 " +
                        "payload shape.");
                    break;
            }
        }
    }

    private static void ValidateAttackActionShape(
        ActorFormDefinition form,
        ActorActionDefinition action,
        ActorAttackProfileDefinition attackProfile,
        List<string> errors)
    {
        ActorShotProgramDefinition shotProgram = attackProfile.ShotProgram;
        bool valid;
        string expected;
        if (attackProfile.OmnidirectionalAim)
        {
            valid = !shotProgram.Enabled
                && HasExactly(
                    action.ParameterKinds,
                    ActorActionParameterKind.ProjectileHeading);
            expected =
                "an omnidirectional profile with disabled shot programs and " +
                "exactly ProjectileHeading";
        }
        else if (shotProgram.Enabled)
        {
            valid = HasExactly(
                action.ParameterKinds,
                ActorActionParameterKind.ShotProgram);
            expected =
                "a facing-relative programmed profile with exactly ShotProgram";
        }
        else
        {
            valid = action.ParameterKinds.IsEmpty;
            expected =
                "a facing-relative non-programmed profile with no parameters";
        }

        if (!valid)
        {
            errors.Add(
                $"Form '{form.Id}' Attack action '{action.Id}' is incompatible " +
                $"with attack profile '{attackProfile.Id}'; schema 3 requires " +
                $"{expected}.");
        }
    }

    private static void ValidateUnusedCatalogEntries<T>(
        IReadOnlyList<T> items,
        Func<T, string> idSelector,
        string itemName,
        IReadOnlySet<string> usedIds,
        List<string> errors)
        where T : class
    {
        foreach (T? item in items)
        {
            if (item is null)
                continue;
            string id = idSelector(item);
            if (!usedIds.Contains(id))
                errors.Add($"{itemName} '{id}' is not used by any form.");
        }
    }

    private static void ValidateSharedTransitionIds(
        IReadOnlyList<ActorFabricationTransitionDefinition>
            fabricationTransitions,
        IReadOnlyList<ActorSameLifeTransitionDefinition> sameLifeTransitions,
        IReadOnlyList<ActorReplicationTransitionDefinition>
            replicationTransitions,
        List<string> errors)
    {
        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        AddTransitionIds(
            fabricationTransitions,
            transition => transition.TransitionId,
            "fabrication",
            transitionIds,
            errors);
        AddTransitionIds(
            sameLifeTransitions,
            transition => transition.TransitionId,
            "same-life",
            transitionIds,
            errors);
        AddTransitionIds(
            replicationTransitions,
            transition => transition.TransitionId,
            "replication",
            transitionIds,
            errors);
    }

    private static void AddTransitionIds<T>(
        IReadOnlyList<T> transitions,
        Func<T, string> idSelector,
        string family,
        HashSet<string> transitionIds,
        List<string> errors)
        where T : class
    {
        foreach (T? transition in transitions)
        {
            if (transition is null)
            {
                errors.Add(
                    $"The {family} transition catalog cannot contain null entries.");
                continue;
            }

            string transitionId = idSelector(transition);
            ValidateCanonicalId(
                transitionId,
                $"{UppercaseFirst(family)} transition ID",
                errors);
            if (!transitionIds.Add(transitionId))
            {
                errors.Add(
                    $"Transition ID '{transitionId}' is shared across transition definitions.");
            }
        }
    }

    private static void ValidateFabricationTransitions(
        IReadOnlyList<ActorFabricationTransitionDefinition> transitions,
        IReadOnlyDictionary<string, ActorFormDefinition> formsById,
        IReadOnlyDictionary<string, ActorActionDefinition> actionsById,
        HashSet<(string Source, string Action)> routes,
        List<string> errors)
    {
        foreach (ActorFabricationTransitionDefinition? transition in
                 transitions)
        {
            if (transition is null)
                continue;

            ValidateCanonicalId(
                transition.ActionId,
                $"Fabrication transition '{transition.TransitionId}' action ID",
                errors);
            if (transition is not BoundedChildFabricationDefinition bounded)
            {
                errors.Add(
                    $"Fabrication transition '{transition.TransitionId}' uses an " +
                    "unsupported schema-3 variant.");
                continue;
            }

            ValidateCanonicalId(
                bounded.OutputFormId,
                $"Fabrication transition '{bounded.TransitionId}' output-form ID",
                errors);
            ValidateCanonicalId(
                bounded.SourceRegionRoleId,
                $"Fabrication transition '{bounded.TransitionId}' source-region role ID",
                errors);
            ValidateCanonicalId(
                bounded.OutputRegionRoleId,
                $"Fabrication transition '{bounded.TransitionId}' output-region role ID",
                errors);
            RequireForm(
                bounded.OutputFormId,
                $"Fabrication transition '{bounded.TransitionId}' output",
                formsById,
                errors);

            ActorActionDefinition? action = RequireAction(
                bounded.ActionId,
                $"Fabrication transition '{bounded.TransitionId}'",
                actionsById,
                errors);
            if (action is not null
                && (action.Id != FabricateActionId
                    || action.Code != FabricateActionCode
                    || action.Kind != ActorActionKind.Fabrication
                    || !HasExactly(
                        action.ParameterKinds,
                        ActorActionParameterKind.UnitTarget)))
            {
                errors.Add(
                    $"Bounded fabrication transition '{bounded.TransitionId}' " +
                    "requires action 'fabricate'/100 of kind Fabrication with " +
                    "exactly UnitTarget.");
            }

            foreach (string sourceFormId in bounded.SourceFormIds)
            {
                ValidateCanonicalId(
                    sourceFormId,
                    $"Fabrication transition '{bounded.TransitionId}' source-form ID",
                    errors);
                RequireSourceFormAllowsAction(
                    sourceFormId,
                    bounded.ActionId,
                    $"Fabrication transition '{bounded.TransitionId}'",
                    formsById,
                    errors);
                if (!routes.Add((sourceFormId, bounded.ActionId)))
                {
                    errors.Add(
                        $"Fabrication route '{sourceFormId}' + " +
                        $"'{bounded.ActionId}' is ambiguous.");
                }
            }
        }
    }

    private static void ValidateSameLifeTransitions(
        IReadOnlyList<ActorSameLifeTransitionDefinition> transitions,
        IReadOnlyDictionary<string, ActorFormDefinition> formsById,
        IReadOnlyDictionary<string, ActorActionDefinition> actionsById,
        HashSet<(string Source, string Action, string Target)> routes,
        List<string> errors)
    {
        var routeCounts = new Dictionary<(string Source, string Action), int>();
        foreach (ActorSameLifeTransitionDefinition? transition in transitions)
        {
            if (transition is null)
                continue;

            ValidateCanonicalId(
                transition.ActionId,
                $"Same-life transition '{transition.TransitionId}' action ID",
                errors);
            ValidateCanonicalId(
                transition.SourceFormId,
                $"Same-life transition '{transition.TransitionId}' source-form ID",
                errors);
            ValidateCanonicalId(
                transition.TargetFormId,
                $"Same-life transition '{transition.TransitionId}' target-form ID",
                errors);
            RequireSourceFormAllowsAction(
                transition.SourceFormId,
                transition.ActionId,
                $"Same-life transition '{transition.TransitionId}'",
                formsById,
                errors);
            RequireForm(
                transition.TargetFormId,
                $"Same-life transition '{transition.TransitionId}' target",
                formsById,
                errors);

            ActorActionDefinition? action = RequireAction(
                transition.ActionId,
                $"Same-life transition '{transition.TransitionId}'",
                actionsById,
                errors);
            if (action is not null
                && (action.Kind != ActorActionKind.SameLifeTransition
                    || (!action.ParameterKinds.IsEmpty
                        && !HasExactly(
                            action.ParameterKinds,
                            ActorActionParameterKind.FormTarget))))
            {
                errors.Add(
                    $"Same-life transition '{transition.TransitionId}' requires " +
                    "a SameLifeTransition action with either no parameters or " +
                    "exactly FormTarget.");
            }

            var route = (
                transition.SourceFormId,
                transition.ActionId,
                transition.TargetFormId);
            if (!routes.Add(route))
            {
                errors.Add(
                    $"Same-life route '{route.SourceFormId}' + " +
                    $"'{route.ActionId}' + '{route.TargetFormId}' is ambiguous.");
            }
            var routeKey = (
                transition.SourceFormId,
                transition.ActionId);
            routeCounts.TryGetValue(routeKey, out int routeCount);
            routeCounts[routeKey] = routeCount + 1;
        }

        foreach (((string source, string actionId), int routeCount) in
                 routeCounts)
        {
            if (routeCount <= 1
                || !actionsById.TryGetValue(
                    actionId,
                    out ActorActionDefinition? action)
                || !action.ParameterKinds.IsEmpty)
            {
                continue;
            }

            errors.Add(
                $"Parameterless same-life action '{actionId}' on form " +
                $"'{source}' resolves {routeCount} targets; it must declare " +
                "exactly FormTarget.");
        }
    }

    private static void ValidateReplicationTransitions(
        IReadOnlyList<ActorReplicationTransitionDefinition> transitions,
        IReadOnlyDictionary<string, ActorFormDefinition> formsById,
        IReadOnlyDictionary<string, ActorActionDefinition> actionsById,
        HashSet<(string Source, string Action)> routes,
        List<string> errors)
    {
        foreach (ActorReplicationTransitionDefinition? transition in
                 transitions)
        {
            if (transition is null)
                continue;

            ValidateCanonicalId(
                transition.ActionId,
                $"Replication transition '{transition.TransitionId}' action ID",
                errors);
            if (transition is not SplitReplicationTransitionDefinition split)
            {
                errors.Add(
                    $"Replication transition '{transition.TransitionId}' uses an " +
                    "unsupported schema-3 variant.");
                continue;
            }

            ValidateCanonicalId(
                split.OutputFormId,
                $"Split transition '{split.TransitionId}' output-form ID",
                errors);
            if (formsById.TryGetValue(
                    split.OutputFormId,
                    out ActorFormDefinition? outputForm))
            {
                if (outputForm.MaxHealth
                    < split.Health.MinimumHealthPerDescendant)
                {
                    errors.Add(
                        $"Split transition '{split.TransitionId}' output form " +
                        $"'{outputForm.Id}' has maximum health " +
                        $"{outputForm.MaxHealth}, below the required descendant " +
                        $"minimum {split.Health.MinimumHealthPerDescendant}.");
                }
            }
            else
            {
                errors.Add(
                    $"Split transition '{split.TransitionId}' output references " +
                    $"unknown form '{split.OutputFormId}'.");
            }

            ActorActionDefinition? action = RequireAction(
                split.ActionId,
                $"Split transition '{split.TransitionId}'",
                actionsById,
                errors);
            if (action is not null
                && (action.Id != SplitActionId
                    || action.Code != SplitActionCode
                    || action.Kind != ActorActionKind.Replication
                    || !action.ParameterKinds.IsEmpty))
            {
                errors.Add(
                    $"Split transition '{split.TransitionId}' requires " +
                    "parameterless action 'split'/103 of kind Replication.");
            }

            foreach (string sourceFormId in split.SourceFormIds)
            {
                ValidateCanonicalId(
                    sourceFormId,
                    $"Split transition '{split.TransitionId}' source-form ID",
                    errors);
                ActorFormDefinition? source = RequireSourceFormAllowsAction(
                    sourceFormId,
                    split.ActionId,
                    $"Split transition '{split.TransitionId}'",
                    formsById,
                    errors);
                if (source is not null
                    && source.MaxHealth < split.MinimumSourceHealth)
                {
                    errors.Add(
                        $"Split transition '{split.TransitionId}' needs source " +
                        $"health {split.MinimumSourceHealth}, but form " +
                        $"'{source.Id}' has maximum health {source.MaxHealth}.");
                }
                if (!routes.Add((sourceFormId, split.ActionId)))
                {
                    errors.Add(
                        $"Replication route '{sourceFormId}' + " +
                        $"'{split.ActionId}' is ambiguous.");
                }
            }
        }
    }

    private static void ValidateLifecycleActionRoutes(
        IReadOnlyList<ActorFormDefinition> forms,
        IReadOnlyDictionary<string, ActorActionDefinition> actionsById,
        IReadOnlySet<(string Source, string Action)> fabricationRoutes,
        IReadOnlySet<(string Source, string Action, string Target)>
            sameLifeRoutes,
        IReadOnlySet<(string Source, string Action)> replicationRoutes,
        List<string> errors)
    {
        foreach (ActorFormDefinition? form in forms)
        {
            if (form is null)
                continue;
            foreach (string actionId in form.AllowedActionIds)
            {
                if (!actionsById.TryGetValue(
                        actionId,
                        out ActorActionDefinition? action))
                {
                    continue;
                }

                bool hasRoute = action.Kind switch
                {
                    ActorActionKind.Fabrication =>
                        fabricationRoutes.Contains((form.Id, actionId)),
                    ActorActionKind.SameLifeTransition =>
                        sameLifeRoutes.Any(
                            route => route.Source == form.Id
                                && route.Action == actionId),
                    ActorActionKind.Replication =>
                        replicationRoutes.Contains((form.Id, actionId)),
                    _ => true,
                };
                if (!hasRoute)
                {
                    errors.Add(
                        $"Form '{form.Id}' permits lifecycle action " +
                        $"'{actionId}' without a matching source transition.");
                }
            }
        }
    }

    private static void ValidateReference<T>(
        string id,
        string referenceName,
        IReadOnlyDictionary<string, T> catalog,
        HashSet<string> usedIds,
        List<string> errors)
    {
        ValidateCanonicalId(id, referenceName, errors);
        if (!catalog.ContainsKey(id))
        {
            errors.Add($"{referenceName} references unknown ID '{id}'.");
            return;
        }
        usedIds.Add(id);
    }

    private static ActorActionDefinition? RequireAction(
        string actionId,
        string owner,
        IReadOnlyDictionary<string, ActorActionDefinition> actionsById,
        List<string> errors)
    {
        if (actionsById.TryGetValue(
                actionId,
                out ActorActionDefinition? action))
        {
            return action;
        }
        errors.Add($"{owner} references unknown action '{actionId}'.");
        return null;
    }

    private static ActorFormDefinition? RequireSourceFormAllowsAction(
        string sourceFormId,
        string actionId,
        string owner,
        IReadOnlyDictionary<string, ActorFormDefinition> formsById,
        List<string> errors)
    {
        if (!formsById.TryGetValue(
                sourceFormId,
                out ActorFormDefinition? source))
        {
            errors.Add($"{owner} references unknown source form '{sourceFormId}'.");
            return null;
        }
        if (!source.AllowedActionIds.Contains(actionId, StringComparer.Ordinal))
        {
            errors.Add(
                $"{owner} action '{actionId}' is not allowed by source form " +
                $"'{sourceFormId}'.");
        }
        return source;
    }

    private static void RequireForm(
        string formId,
        string owner,
        IReadOnlyDictionary<string, ActorFormDefinition> formsById,
        List<string> errors)
    {
        if (!formsById.ContainsKey(formId))
            errors.Add($"{owner} references unknown form '{formId}'.");
    }

    private static bool HasExactly(
        IReadOnlyList<ActorActionParameterKind> parameters,
        ActorActionParameterKind expected) =>
        parameters.Count == 1 && parameters[0] == expected;

    private static void ValidateCanonicalId(
        string? value,
        string owner,
        List<string> errors)
    {
        if (!IsCanonicalId(value))
            errors.Add($"{owner} '{value}' is not a lowercase-kebab ID.");
    }

    private static bool IsCanonicalId(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length > MaxSemanticIdLength)
        {
            return false;
        }

        bool needsSegmentStart = true;
        foreach (char character in value)
        {
            if (character == '-')
            {
                if (needsSegmentStart)
                    return false;
                needsSegmentStart = true;
                continue;
            }
            if (character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9'))
            {
                return false;
            }
            needsSegmentStart = false;
        }
        return !needsSegmentStart;
    }

    private static string UppercaseFirst(string value) =>
        char.ToUpperInvariant(value[0]) + value[1..];
}
