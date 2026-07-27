namespace BotArena.Engine;

/// <summary>
/// Performs generation-3 cross-catalog and match-local validation before a
/// resolved actor match can be serialized, fingerprinted, or executed.
/// </summary>
public static class ActorResolvedMatchDefinitionValidator
{
    public static void Validate(
        ActorRulesDefinition rules,
        ActorMapDefinition map,
        MatchFormatDefinition format,
        PublicMatchTopology topology,
        InitialDeploymentDefinition initialDeployment,
        IReadOnlyList<ActorUnitSlotLifecycleAssignmentDefinition>
            lifecycleAssignments,
        IReadOnlyList<ActorParticipantRegionAssignmentDefinition>
            participantRegionAssignments,
        ActorModeMapBindingDefinition modeMapBinding)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(initialDeployment);
        ArgumentNullException.ThrowIfNull(lifecycleAssignments);
        ArgumentNullException.ThrowIfNull(participantRegionAssignments);
        ArgumentNullException.ThrowIfNull(modeMapBinding);

        var errors = new List<string>();
        bool topologyValid = ValidateFormatTopology(
            format,
            topology,
            errors);

        Dictionary<string, ActorFormDefinition> forms = rules.Forms
            .ToDictionary(form => form.Id, StringComparer.Ordinal);
        Dictionary<string, ActorMovementProfileDefinition> movementProfiles =
            rules.MovementProfiles.ToDictionary(
                profile => profile.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorVisionProfileDefinition> visionProfiles =
            rules.VisionProfiles.ToDictionary(
                profile => profile.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorAttackProfileDefinition> attackProfiles =
            rules.AttackProfiles.ToDictionary(
                profile => profile.Id,
                StringComparer.Ordinal);
        Dictionary<string, ActorActionDefinition> actions = rules.Actions
            .ToDictionary(action => action.Id, StringComparer.Ordinal);
        Dictionary<string, ActorLifecycleProfileDefinition> lifecycleProfiles =
            rules.Lifecycle.Profiles.ToDictionary(
                profile => profile.ProfileId,
                StringComparer.Ordinal);
        Dictionary<string, ActorMapSpawnAnchorDefinition> mapSpawns =
            map.SpawnAnchors.ToDictionary(
                anchor => anchor.Spawn.SpawnId,
                StringComparer.Ordinal);
        Dictionary<string, ActorMapRegionDefinition> mapRegions = map.Regions
            .ToDictionary(region => region.RegionId, StringComparer.Ordinal);

        ValidateRuleCatalogReferences(
            rules,
            forms,
            movementProfiles,
            visionProfiles,
            attackProfiles,
            actions,
            errors);
        ValidateCheckedTickArithmetic(rules, errors);
        ValidateModeMapBinding(
            rules.GameMode,
            modeMapBinding,
            topologyValid ? topology : null,
            mapRegions,
            errors);
        ValidateSameLifeMapFeasibility(
            rules.SameLifeTransitions,
            map,
            errors);

        if (topologyValid)
        {
            ValidateScoreAccumulatorBounds(rules, topology, errors);
            try
            {
                initialDeployment.ValidateTopology(topology);
            }
            catch (ArgumentException)
            {
                errors.Add(
                    "Initial deployment lives must exactly match topology initial lives.");
            }

            ValidateMatchLocalBindings(
                rules,
                map,
                topology,
                initialDeployment,
                lifecycleAssignments,
                participantRegionAssignments,
                forms,
                movementProfiles,
                lifecycleProfiles,
                mapSpawns,
                mapRegions,
                errors);
        }

        if (errors.Count > 0)
            throw new ActorResolvedMatchValidationException(errors);
    }

    private static bool ValidateFormatTopology(
        MatchFormatDefinition format,
        PublicMatchTopology topology,
        List<string> errors)
    {
        try
        {
            format.ValidateTopology(topology);
            return true;
        }
        catch (MatchFormatValidationException exception)
        {
            errors.AddRange(exception.Errors.Select(
                error => $"Format/topology: {error}"));
            return false;
        }
    }

    private static void ValidateRuleCatalogReferences(
        ActorRulesDefinition rules,
        IReadOnlyDictionary<string, ActorFormDefinition> forms,
        IReadOnlyDictionary<string, ActorMovementProfileDefinition>
            movementProfiles,
        IReadOnlyDictionary<string, ActorVisionProfileDefinition> visionProfiles,
        IReadOnlyDictionary<string, ActorAttackProfileDefinition> attackProfiles,
        IReadOnlyDictionary<string, ActorActionDefinition> actions,
        List<string> errors)
    {
        foreach (ActorFormDefinition form in rules.Forms)
        {
            if (!movementProfiles.ContainsKey(form.MovementProfileId))
            {
                errors.Add(
                    $"Form '{form.Id}' references unknown movement profile " +
                    $"'{form.MovementProfileId}'.");
            }
            if (!visionProfiles.ContainsKey(form.VisionProfileId))
            {
                errors.Add(
                    $"Form '{form.Id}' references unknown vision profile " +
                    $"'{form.VisionProfileId}'.");
            }
            if (form.AttackProfileId is string attackProfileId
                && !attackProfiles.ContainsKey(attackProfileId))
            {
                errors.Add(
                    $"Form '{form.Id}' references unknown attack profile " +
                    $"'{attackProfileId}'.");
            }
            foreach (string actionId in form.AllowedActionIds)
            {
                if (!actions.ContainsKey(actionId))
                {
                    errors.Add(
                        $"Form '{form.Id}' allows unknown action '{actionId}'.");
                }
            }
        }

        foreach (ActorLifecycleProfileDefinition profile
                 in rules.Lifecycle.Profiles)
        {
            if (profile.AutomaticReturnFormId is string returnFormId
                && !forms.ContainsKey(returnFormId))
            {
                errors.Add(
                    $"Lifecycle profile '{profile.ProfileId}' references unknown " +
                    $"automatic-return form '{returnFormId}'.");
            }
        }

        foreach (ActorSameLifeTransitionDefinition transition
                 in rules.SameLifeTransitions)
        {
            if (transition is not ActorFormTransitionDefinition)
            {
                errors.Add(
                    $"Same-life transition '{transition.TransitionId}' uses an " +
                    "unsupported semantic variant.");
                continue;
            }

            ValidateTransitionAction(
                transition.TransitionId,
                transition.ActionId,
                ActorActionKind.SameLifeTransition,
                actions,
                errors);
            ValidateSourceFormAction(
                transition.TransitionId,
                [transition.SourceFormId],
                transition.ActionId,
                forms,
                errors);
            if (!forms.ContainsKey(transition.TargetFormId))
            {
                errors.Add(
                    $"Same-life transition '{transition.TransitionId}' references " +
                    $"unknown target form '{transition.TargetFormId}'.");
            }
        }

        foreach (ActorFabricationTransitionDefinition transition
                 in rules.FabricationTransitions)
        {
            if (transition is not BoundedChildFabricationDefinition bounded)
            {
                errors.Add(
                    $"Fabrication transition '{transition.TransitionId}' uses an " +
                    "unsupported semantic variant.");
                continue;
            }

            ValidateTransitionAction(
                transition.TransitionId,
                transition.ActionId,
                ActorActionKind.Fabrication,
                actions,
                errors);
            ValidateSourceFormAction(
                transition.TransitionId,
                transition.SourceFormIds,
                transition.ActionId,
                forms,
                errors);
            if (!forms.ContainsKey(bounded.OutputFormId))
            {
                errors.Add(
                    $"Fabrication transition '{transition.TransitionId}' references " +
                    $"unknown output form '{bounded.OutputFormId}'.");
            }
        }

        foreach (ActorReplicationTransitionDefinition transition
                 in rules.ReplicationTransitions)
        {
            if (transition is not SplitReplicationTransitionDefinition split)
            {
                errors.Add(
                    $"Replication transition '{transition.TransitionId}' uses an " +
                    "unsupported semantic variant.");
                continue;
            }

            ValidateTransitionAction(
                transition.TransitionId,
                transition.ActionId,
                ActorActionKind.Replication,
                actions,
                errors);
            ValidateSourceFormAction(
                transition.TransitionId,
                transition.SourceFormIds,
                transition.ActionId,
                forms,
                errors);
            if (!forms.ContainsKey(split.OutputFormId))
            {
                errors.Add(
                    $"Replication transition '{transition.TransitionId}' references " +
                    $"unknown output form '{split.OutputFormId}'.");
            }
        }
    }

    private static void ValidateTransitionAction(
        string transitionId,
        string actionId,
        ActorActionKind expectedKind,
        IReadOnlyDictionary<string, ActorActionDefinition> actions,
        List<string> errors)
    {
        if (!actions.TryGetValue(actionId, out ActorActionDefinition? action))
        {
            errors.Add(
                $"Transition '{transitionId}' references unknown action '{actionId}'.");
        }
        else if (action.Kind != expectedKind)
        {
            errors.Add(
                $"Transition '{transitionId}' action '{actionId}' must use " +
                $"semantic kind '{expectedKind}'.");
        }
    }

    private static void ValidateSourceFormAction(
        string transitionId,
        IEnumerable<string> sourceFormIds,
        string actionId,
        IReadOnlyDictionary<string, ActorFormDefinition> forms,
        List<string> errors)
    {
        foreach (string sourceFormId in sourceFormIds)
        {
            if (!forms.TryGetValue(
                    sourceFormId,
                    out ActorFormDefinition? sourceForm))
            {
                errors.Add(
                    $"Transition '{transitionId}' references unknown source form " +
                    $"'{sourceFormId}'.");
            }
            else if (!sourceForm.AllowedActionIds.Contains(
                         actionId,
                         StringComparer.Ordinal))
            {
                errors.Add(
                    $"Transition '{transitionId}' source form '{sourceFormId}' " +
                    $"does not allow action '{actionId}'.");
            }
        }
    }

    private static void ValidateCheckedTickArithmetic(
        ActorRulesDefinition rules,
        List<string> errors)
    {
        int maxTicks = rules.Limits.MaxTicks;
        foreach (ActorLifecycleProfileDefinition profile
                 in rules.Lifecycle.Profiles)
        {
            ValidateMaximumDueTick(
                $"Lifecycle profile '{profile.ProfileId}'",
                maxTicks,
                1L + profile.DelayTicks,
                errors);
        }
        foreach (ActorSameLifeTransitionDefinition transition
                 in rules.SameLifeTransitions)
        {
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
        foreach (ActorFabricationTransitionDefinition transition
                 in rules.FabricationTransitions)
        {
            if (transition is BoundedChildFabricationDefinition bounded)
            {
                ValidateMaximumDueTick(
                    $"Fabrication transition '{transition.TransitionId}'",
                    maxTicks,
                    bounded.Delay.DurationTicks,
                    errors);
            }
        }
        foreach (ActorReplicationTransitionDefinition transition
                 in rules.ReplicationTransitions)
        {
            if (transition is SplitReplicationTransitionDefinition split)
            {
                ValidateMaximumDueTick(
                    $"Replication transition '{transition.TransitionId}'",
                    maxTicks,
                    split.Windup.DurationTicks,
                    errors);
            }
        }
        if (rules.GameMode is FrontlineGameModeDefinition frontline)
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
        long maximumSourceTick = maxTicks - 1L;
        if (maximumSourceTick + dueTickOffset > int.MaxValue)
        {
            errors.Add(
                $"{owner} can schedule a tick beyond the supported 32-bit range.");
        }
    }

    private static void ValidateModeMapBinding(
        GameModeDefinition gameMode,
        ActorModeMapBindingDefinition modeMapBinding,
        PublicMatchTopology? topology,
        IReadOnlyDictionary<string, ActorMapRegionDefinition> mapRegions,
        List<string> errors)
    {
        switch (gameMode)
        {
            case DeathmatchGameModeDefinition:
                if (modeMapBinding
                    is not DeathmatchActorModeMapBindingDefinition)
                {
                    errors.Add(
                        "Deathmatch requires the empty Deathmatch mode-map binding.");
                }
                break;

            case FrontlineGameModeDefinition frontline:
                if (modeMapBinding
                    is FrontlineActorModeMapBindingDefinition binding)
                {
                    ValidateFrontlineBinding(
                        frontline,
                        binding,
                        topology,
                        mapRegions,
                        errors);
                }
                else
                {
                    errors.Add(
                        "Frontline requires a Frontline mode-map binding.");
                }
                break;

            default:
                errors.Add(
                    $"Game mode '{gameMode.ModeId}' uses an unsupported semantic variant.");
                break;
        }
    }

    private static void ValidateFrontlineBinding(
        FrontlineGameModeDefinition frontline,
        FrontlineActorModeMapBindingDefinition binding,
        PublicMatchTopology? topology,
        IReadOnlyDictionary<string, ActorMapRegionDefinition> mapRegions,
        List<string> errors)
    {
        if (binding.OrderedObjectiveRegionIds.Length
            != frontline.FrontlinePositionCount)
        {
            errors.Add(
                $"Frontline requires exactly {frontline.FrontlinePositionCount} " +
                "ordered objective regions.");
        }
        var objectiveTiles = new Dictionary<Position, string>();
        foreach (string regionId in binding.OrderedObjectiveRegionIds)
        {
            if (!mapRegions.TryGetValue(
                    regionId,
                    out ActorMapRegionDefinition? region))
            {
                errors.Add(
                    $"Frontline references unknown objective region '{regionId}'.");
            }
            else if (region.Kind
                     != ActorMapRegionDefinition.RegionKind.Objective)
            {
                errors.Add(
                    $"Frontline region '{regionId}' must be an Objective region.");
            }
            else
            {
                foreach (Position tile in region.Tiles)
                {
                    if (objectiveTiles.TryGetValue(
                            tile,
                            out string? existingRegionId))
                    {
                        errors.Add(
                            $"Frontline objective regions '{existingRegionId}' " +
                            $"and '{regionId}' overlap at {tile}.");
                    }
                    else
                    {
                        objectiveTiles.Add(tile, regionId);
                    }
                }
            }
        }

        if (topology is null)
            return;

        int[] expectedTeamIds = topology.Teams
            .Select(team => team.TeamId)
            .Order()
            .ToArray();
        int[] actualTeamIds = binding.TeamAdvances
            .Select(advance => advance.TeamId)
            .Order()
            .ToArray();
        if (!expectedTeamIds.SequenceEqual(actualTeamIds))
        {
            errors.Add(
                "Frontline team advances must exactly cover the topology scoring teams.");
        }
    }

    private static void ValidateMatchLocalBindings(
        ActorRulesDefinition rules,
        ActorMapDefinition map,
        PublicMatchTopology topology,
        InitialDeploymentDefinition initialDeployment,
        IReadOnlyList<ActorUnitSlotLifecycleAssignmentDefinition>
            lifecycleAssignments,
        IReadOnlyList<ActorParticipantRegionAssignmentDefinition>
            participantRegionAssignments,
        IReadOnlyDictionary<string, ActorFormDefinition> forms,
        IReadOnlyDictionary<string, ActorMovementProfileDefinition>
            movementProfiles,
        IReadOnlyDictionary<string, ActorLifecycleProfileDefinition>
            lifecycleProfiles,
        IReadOnlyDictionary<string, ActorMapSpawnAnchorDefinition> mapSpawns,
        IReadOnlyDictionary<string, ActorMapRegionDefinition> mapRegions,
        List<string> errors)
    {
        Dictionary<(int TeamId, int UnitId), PublicUnitSlot> slots =
            topology.UnitSlots.ToDictionary(
                slot => (slot.TeamId, slot.UnitId));
        Dictionary<(int TeamId, int UnitId), PublicInitialLife> initialLives =
            topology.InitialLives.ToDictionary(
                life => (life.TeamId, life.UnitId));
        HashSet<int> participantIds = topology.Participants
            .Select(participant => participant.ParticipantId)
            .ToHashSet();

        Dictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments =
            ValidateLifecycleAssignments(
                rules.Limits.MaxTicks,
                lifecycleAssignments,
                slots,
                initialLives,
                forms,
                lifecycleProfiles,
                mapSpawns,
                movementProfiles,
                errors);

        ValidateInitialDeploymentMapBindings(
            initialDeployment,
            forms,
            movementProfiles,
            mapSpawns,
            errors);

        Dictionary<
            (int ParticipantId, string RegionRoleId),
            ActorParticipantRegionAssignmentDefinition> regionAssignments =
            ValidateParticipantRegionAssignments(
                participantRegionAssignments,
                participantIds,
                mapRegions,
                rules.FabricationTransitions,
                errors);
        HashSet<Position> reservedRespawnTiles =
            ValidateReservedRespawnSpawns(
                assignments,
                lifecycleProfiles,
                mapSpawns,
                initialDeployment,
                errors);

        ValidateSameLifeSlotCompatibility(
            rules.SameLifeTransitions,
            assignments,
            errors);
        ValidateFabricationBindingsAndCapacity(
            rules.FabricationTransitions,
            slots,
            assignments,
            lifecycleProfiles,
            regionAssignments,
            mapRegions,
            map,
            reservedRespawnTiles,
            errors);
        ValidateReplicationCapacity(
            rules.ReplicationTransitions,
            slots,
            assignments,
            lifecycleProfiles,
            map,
            reservedRespawnTiles,
            errors);
    }

    private static HashSet<Position> ValidateReservedRespawnSpawns(
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments,
        IReadOnlyDictionary<string, ActorLifecycleProfileDefinition>
            lifecycleProfiles,
        IReadOnlyDictionary<string, ActorMapSpawnAnchorDefinition> mapSpawns,
        InitialDeploymentDefinition initialDeployment,
        List<string> errors)
    {
        Dictionary<string, InitialLifeDeployment> initialLifeBySpawn =
            initialDeployment.Lives.ToDictionary(
                life => life.SpawnId,
                StringComparer.Ordinal);
        var reservedTiles = new HashSet<Position>();
        foreach (ActorUnitSlotLifecycleAssignmentDefinition assignment
                 in assignments.Values)
        {
            if (!lifecycleProfiles.TryGetValue(
                    assignment.LifecycleProfileId,
                    out ActorLifecycleProfileDefinition? profile)
                || profile.DestructionPolicy
                    != ActorLifecycleProfileDefinition.DestructionPolicyKind
                        .AutomaticRespawn
                || assignment.AssignedRespawnSpawnId is not string spawnId)
            {
                continue;
            }

            if (mapSpawns.TryGetValue(
                    spawnId,
                    out ActorMapSpawnAnchorDefinition? spawn))
            {
                reservedTiles.Add(spawn.Spawn.Position);
            }
            if (initialLifeBySpawn.TryGetValue(
                    spawnId,
                    out InitialLifeDeployment? life)
                && (life.TeamId, life.UnitId)
                    != (assignment.TeamId, assignment.UnitId))
            {
                errors.Add(
                    $"Automatic-respawn spawn '{spawnId}' reserved for stable " +
                    $"slot {assignment.TeamId}:{assignment.UnitId} is initially " +
                    $"occupied by {life.TeamId}:{life.UnitId}.");
            }
        }
        return reservedTiles;
    }

    private static Dictionary<
        (int TeamId, int UnitId),
        ActorUnitSlotLifecycleAssignmentDefinition>
        ValidateLifecycleAssignments(
            int maxTicks,
            IReadOnlyList<ActorUnitSlotLifecycleAssignmentDefinition>
                lifecycleAssignments,
            IReadOnlyDictionary<(int TeamId, int UnitId), PublicUnitSlot> slots,
            IReadOnlyDictionary<(int TeamId, int UnitId), PublicInitialLife>
                initialLives,
            IReadOnlyDictionary<string, ActorFormDefinition> forms,
            IReadOnlyDictionary<string, ActorLifecycleProfileDefinition>
                lifecycleProfiles,
            IReadOnlyDictionary<string, ActorMapSpawnAnchorDefinition> mapSpawns,
            IReadOnlyDictionary<string, ActorMovementProfileDefinition>
                movementProfiles,
            List<string> errors)
    {
        var assignments = new Dictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition>();
        var automaticRespawnSpawnOwners = new Dictionary<
            string,
            (int TeamId, int UnitId)>(StringComparer.Ordinal);
        foreach (ActorUnitSlotLifecycleAssignmentDefinition? assignment
                 in lifecycleAssignments)
        {
            if (assignment is null)
            {
                errors.Add("Lifecycle assignments cannot contain null entries.");
                continue;
            }

            var slotKey = (assignment.TeamId, assignment.UnitId);
            if (!assignments.TryAdd(slotKey, assignment))
            {
                errors.Add(
                    $"Stable slot {assignment.TeamId}:{assignment.UnitId} has " +
                    "more than one lifecycle assignment.");
                continue;
            }
            if (!slots.ContainsKey(slotKey))
            {
                errors.Add(
                    $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                    "does not reference a topology unit slot.");
                continue;
            }

            bool hasInitialLife = initialLives.TryGetValue(
                slotKey,
                out PublicInitialLife? initialLife);
            bool declaredActive = assignment.InitialAvailability
                == ActorUnitSlotLifecycleAssignmentDefinition
                    .InitialAvailabilityKind.ActiveAtTickZero;
            if (declaredActive != hasInitialLife)
            {
                errors.Add(
                    $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                    "active/dormant state must match topology initial-life presence.");
            }

            foreach (string formId in assignment.AllowedFormIds)
            {
                if (!forms.ContainsKey(formId))
                {
                    errors.Add(
                        $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                        $"allows unknown form '{formId}'.");
                }
            }
            if (initialLife is not null)
            {
                if (initialLife.LifeId != 0
                    || assignment.InitialGeneration != 0)
                {
                    errors.Add(
                        $"Initial life {assignment.TeamId}:{assignment.UnitId} " +
                        "must use life ID 0 and lineage generation 0.");
                }
                if (!forms.ContainsKey(initialLife.FormId))
                {
                    errors.Add(
                        $"Initial life {assignment.TeamId}:{assignment.UnitId} " +
                        $"uses unknown form '{initialLife.FormId}'.");
                }
                if (!assignment.AllowedFormIds.Contains(
                        initialLife.FormId,
                        StringComparer.Ordinal))
                {
                    errors.Add(
                        $"Initial life {assignment.TeamId}:{assignment.UnitId} form " +
                        $"'{initialLife.FormId}' is not allowed by its lifecycle assignment.");
                }
            }

            if (assignment.UnlockTick >= maxTicks)
            {
                errors.Add(
                    $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                    "must unlock before the match tick limit.");
            }

            if (!lifecycleProfiles.TryGetValue(
                    assignment.LifecycleProfileId,
                    out ActorLifecycleProfileDefinition? profile))
            {
                errors.Add(
                    $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                    $"references unknown profile '{assignment.LifecycleProfileId}'.");
                continue;
            }

            ValidateAssignmentRespawn(
                assignment,
                profile,
                forms,
                mapSpawns,
                movementProfiles,
                automaticRespawnSpawnOwners,
                errors);
        }

        foreach ((int teamId, int unitId) in slots.Keys.Order())
        {
            if (!assignments.ContainsKey((teamId, unitId)))
            {
                errors.Add(
                    $"Stable slot {teamId}:{unitId} has no lifecycle assignment.");
            }
        }

        return assignments;
    }

    private static void ValidateAssignmentRespawn(
        ActorUnitSlotLifecycleAssignmentDefinition assignment,
        ActorLifecycleProfileDefinition profile,
        IReadOnlyDictionary<string, ActorFormDefinition> forms,
        IReadOnlyDictionary<string, ActorMapSpawnAnchorDefinition> mapSpawns,
        IReadOnlyDictionary<string, ActorMovementProfileDefinition>
            movementProfiles,
        Dictionary<string, (int TeamId, int UnitId)>
            automaticRespawnSpawnOwners,
        List<string> errors)
    {
        bool automatic = profile.DestructionPolicy
            == ActorLifecycleProfileDefinition.DestructionPolicyKind
                .AutomaticRespawn;
        if (!automatic)
        {
            if (assignment.AssignedRespawnSpawnId is not null)
            {
                errors.Add(
                    $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                    "must not assign a respawn spawn for a non-automatic profile.");
            }
            return;
        }

        if (assignment.AssignedRespawnSpawnId is not string spawnId)
        {
            errors.Add(
                $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                "must assign a respawn spawn for automatic respawn.");
            return;
        }
        if (!automaticRespawnSpawnOwners.TryAdd(
                spawnId,
                (assignment.TeamId, assignment.UnitId)))
        {
            (int ownerTeamId, int ownerUnitId) =
                automaticRespawnSpawnOwners[spawnId];
            errors.Add(
                $"Automatic-respawn spawn '{spawnId}' is shared by stable slots " +
                $"{ownerTeamId}:{ownerUnitId} and " +
                $"{assignment.TeamId}:{assignment.UnitId}.");
        }
        if (!mapSpawns.TryGetValue(
                spawnId,
                out ActorMapSpawnAnchorDefinition? spawn))
        {
            errors.Add(
                $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                $"references unknown respawn spawn '{spawnId}'.");
            return;
        }

        string? returnFormId = profile.AutomaticReturnFormId;
        if (returnFormId is null
            || !forms.TryGetValue(
                returnFormId,
                out ActorFormDefinition? returnForm))
        {
            return;
        }
        if (!assignment.AllowedFormIds.Contains(
                returnFormId,
                StringComparer.Ordinal))
        {
            errors.Add(
                $"Lifecycle assignment {assignment.TeamId}:{assignment.UnitId} " +
                $"does not allow automatic-return form '{returnFormId}'.");
        }
        ValidateSpawnLayerCompatibility(
            $"Respawn spawn '{spawnId}' for slot " +
            $"{assignment.TeamId}:{assignment.UnitId}",
            spawn,
            returnForm,
            movementProfiles,
            errors);
    }

    private static void ValidateInitialDeploymentMapBindings(
        InitialDeploymentDefinition initialDeployment,
        IReadOnlyDictionary<string, ActorFormDefinition> forms,
        IReadOnlyDictionary<string, ActorMovementProfileDefinition>
            movementProfiles,
        IReadOnlyDictionary<string, ActorMapSpawnAnchorDefinition> mapSpawns,
        List<string> errors)
    {
        Dictionary<string, InitialSpawnDefinition> resolvedSpawns =
            initialDeployment.Spawns.ToDictionary(
                spawn => spawn.SpawnId,
                StringComparer.Ordinal);
        foreach (InitialSpawnDefinition resolvedSpawn
                 in initialDeployment.Spawns)
        {
            if (!mapSpawns.TryGetValue(
                    resolvedSpawn.SpawnId,
                    out ActorMapSpawnAnchorDefinition? mapSpawn))
            {
                errors.Add(
                    $"Initial deployment references unknown map spawn " +
                    $"'{resolvedSpawn.SpawnId}'.");
            }
            else if (resolvedSpawn.Position != mapSpawn.Spawn.Position
                     || resolvedSpawn.Facing != mapSpawn.Spawn.Facing)
            {
                errors.Add(
                    $"Initial deployment spawn '{resolvedSpawn.SpawnId}' must " +
                    "exactly match its map anchor position and facing.");
            }
        }

        foreach (InitialLifeDeployment life in initialDeployment.Lives)
        {
            if (!resolvedSpawns.ContainsKey(life.SpawnId)
                || !mapSpawns.TryGetValue(
                    life.SpawnId,
                    out ActorMapSpawnAnchorDefinition? mapSpawn)
                || !forms.TryGetValue(
                    life.FormId,
                    out ActorFormDefinition? form))
            {
                continue;
            }
            ValidateSpawnLayerCompatibility(
                $"Initial spawn '{life.SpawnId}' for life " +
                $"{life.TeamId}:{life.UnitId}:{life.LifeId}",
                mapSpawn,
                form,
                movementProfiles,
                errors);
        }
    }

    private static void ValidateSpawnLayerCompatibility(
        string owner,
        ActorMapSpawnAnchorDefinition spawn,
        ActorFormDefinition form,
        IReadOnlyDictionary<string, ActorMovementProfileDefinition>
            movementProfiles,
        List<string> errors)
    {
        if (!movementProfiles.TryGetValue(
                form.MovementProfileId,
                out ActorMovementProfileDefinition? movement))
        {
            return;
        }
        if (!spawn.CompatibleMovementLayers.Contains(movement.MovementLayer))
        {
            errors.Add(
                $"{owner} is incompatible with form '{form.Id}' movement layer " +
                $"'{movement.MovementLayer}'.");
        }
    }

    private static Dictionary<
        (int ParticipantId, string RegionRoleId),
        ActorParticipantRegionAssignmentDefinition>
        ValidateParticipantRegionAssignments(
            IReadOnlyList<ActorParticipantRegionAssignmentDefinition>
                participantRegionAssignments,
            IReadOnlySet<int> participantIds,
            IReadOnlyDictionary<string, ActorMapRegionDefinition> mapRegions,
            IEnumerable<ActorFabricationTransitionDefinition>
                fabricationTransitions,
            List<string> errors)
    {
        HashSet<string> knownRoleIds = fabricationTransitions
            .OfType<BoundedChildFabricationDefinition>()
            .SelectMany(transition => new[]
            {
                transition.SourceRegionRoleId,
                transition.OutputRegionRoleId,
            })
            .ToHashSet(StringComparer.Ordinal);
        var assignments = new Dictionary<
            (int ParticipantId, string RegionRoleId),
            ActorParticipantRegionAssignmentDefinition>();

        foreach (ActorParticipantRegionAssignmentDefinition? assignment
                 in participantRegionAssignments)
        {
            if (assignment is null)
            {
                errors.Add(
                    "Participant-region assignments cannot contain null entries.");
                continue;
            }

            var key = (assignment.ParticipantId, assignment.RegionRoleId);
            if (!assignments.TryAdd(key, assignment))
            {
                errors.Add(
                    $"Participant {assignment.ParticipantId} region role " +
                    $"'{assignment.RegionRoleId}' is assigned more than once.");
            }
            if (!participantIds.Contains(assignment.ParticipantId))
            {
                errors.Add(
                    $"Region assignment references unknown participant " +
                    $"{assignment.ParticipantId}.");
            }
            if (!knownRoleIds.Contains(assignment.RegionRoleId))
            {
                errors.Add(
                    $"Region assignment uses unknown transition role " +
                    $"'{assignment.RegionRoleId}'.");
            }
            if (!mapRegions.TryGetValue(
                    assignment.MapRegionId,
                    out ActorMapRegionDefinition? region))
            {
                errors.Add(
                    $"Region assignment references unknown map region " +
                    $"'{assignment.MapRegionId}'.");
            }
            else if (region.Kind
                     != ActorMapRegionDefinition.RegionKind.TransitionPlacement)
            {
                errors.Add(
                    $"Region assignment '{assignment.MapRegionId}' must reference " +
                    "a TransitionPlacement region.");
            }
        }

        return assignments;
    }

    private static void ValidateSameLifeSlotCompatibility(
        IEnumerable<ActorSameLifeTransitionDefinition> transitions,
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments,
        List<string> errors)
    {
        foreach (ActorSameLifeTransitionDefinition transition in transitions)
        {
            if (transition is not ActorFormTransitionDefinition)
                continue;

            foreach (ActorUnitSlotLifecycleAssignmentDefinition assignment
                     in assignments.Values)
            {
                if (assignment.AllowedFormIds.Contains(
                        transition.SourceFormId,
                        StringComparer.Ordinal)
                    && !assignment.AllowedFormIds.Contains(
                        transition.TargetFormId,
                        StringComparer.Ordinal))
                {
                    errors.Add(
                        $"Stable slot {assignment.TeamId}:{assignment.UnitId} " +
                        $"allows same-life transition '{transition.TransitionId}' " +
                        $"source form but not target form '{transition.TargetFormId}'.");
                }
            }
        }
    }

    private static void ValidateFabricationBindingsAndCapacity(
        IEnumerable<ActorFabricationTransitionDefinition> transitions,
        IReadOnlyDictionary<(int TeamId, int UnitId), PublicUnitSlot> slots,
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments,
        IReadOnlyDictionary<string, ActorLifecycleProfileDefinition>
            lifecycleProfiles,
        IReadOnlyDictionary<
            (int ParticipantId, string RegionRoleId),
            ActorParticipantRegionAssignmentDefinition> regionAssignments,
        IReadOnlyDictionary<string, ActorMapRegionDefinition> mapRegions,
        ActorMapDefinition map,
        IReadOnlySet<Position> reservedRespawnTiles,
        List<string> errors)
    {
        foreach (ActorFabricationTransitionDefinition transition in transitions)
        {
            if (transition is not BoundedChildFabricationDefinition bounded)
                continue;

            PublicUnitSlot[] sourceSlots = FindSourceSlots(
                slots,
                assignments,
                transition.SourceFormIds);
            foreach (PublicUnitSlot sourceSlot in sourceSlots)
            {
                PublicUnitSlot[] compatibleTargets = slots.Values
                    .Where(slot =>
                        slot.TeamId == sourceSlot.TeamId
                        && slot.ControllerParticipantId
                            == sourceSlot.ControllerParticipantId
                        && (slot.TeamId, slot.UnitId)
                            != (sourceSlot.TeamId, sourceSlot.UnitId)
                        && assignments.TryGetValue(
                            (slot.TeamId, slot.UnitId),
                            out ActorUnitSlotLifecycleAssignmentDefinition?
                                assignment)
                        && assignment.AllowedFormIds.Contains(
                            bounded.OutputFormId,
                            StringComparer.Ordinal)
                        && CanBecomeReadyForExplicitCreation(
                            assignment,
                            lifecycleProfiles))
                    .ToArray();
                if (compatibleTargets.Length < bounded.OutputCount)
                {
                    errors.Add(
                        $"Fabrication transition '{transition.TransitionId}' has " +
                        $"insufficient same-controller output slots for source " +
                        $"{sourceSlot.TeamId}:{sourceSlot.UnitId}.");
                }
            }

            int[] capableParticipants = sourceSlots
                .Select(slot => slot.ControllerParticipantId)
                .Distinct()
                .Order()
                .ToArray();
            foreach (int participantId in capableParticipants)
            {
                ValidateTransitionRegionRole(
                    participantId,
                    bounded.SourceRegionRoleId,
                    bounded.RequiredSourceTileTags,
                    [],
                    regionAssignments,
                    mapRegions,
                    map,
                    transition.TransitionId,
                    errors);
                ValidateTransitionRegionRole(
                    participantId,
                    bounded.OutputRegionRoleId,
                    bounded.RequiredOutputTileTags,
                    bounded.ForbiddenOutputTileTags,
                    regionAssignments,
                    mapRegions,
                    map,
                    transition.TransitionId,
                    errors);
                ValidateFabricationPlacementFeasibility(
                    participantId,
                    bounded,
                    regionAssignments,
                    mapRegions,
                    map,
                    reservedRespawnTiles,
                    errors);
            }
        }
    }

    private static void ValidateFabricationPlacementFeasibility(
        int participantId,
        BoundedChildFabricationDefinition transition,
        IReadOnlyDictionary<
            (int ParticipantId, string RegionRoleId),
            ActorParticipantRegionAssignmentDefinition> regionAssignments,
        IReadOnlyDictionary<string, ActorMapRegionDefinition> mapRegions,
        ActorMapDefinition map,
        IReadOnlySet<Position> reservedRespawnTiles,
        List<string> errors)
    {
        if (!regionAssignments.TryGetValue(
                (participantId, transition.SourceRegionRoleId),
                out ActorParticipantRegionAssignmentDefinition?
                    sourceAssignment)
            || !regionAssignments.TryGetValue(
                (participantId, transition.OutputRegionRoleId),
                out ActorParticipantRegionAssignmentDefinition?
                    outputAssignment)
            || !mapRegions.TryGetValue(
                sourceAssignment.MapRegionId,
                out ActorMapRegionDefinition? sourceRegion)
            || !mapRegions.TryGetValue(
                outputAssignment.MapRegionId,
                out ActorMapRegionDefinition? outputRegion)
            || sourceRegion.Kind
                != ActorMapRegionDefinition.RegionKind.TransitionPlacement
            || outputRegion.Kind
                != ActorMapRegionDefinition.RegionKind.TransitionPlacement)
        {
            return;
        }

        HashSet<Position> outputTiles = outputRegion.Tiles.ToHashSet();
        bool feasible = sourceRegion.Tiles
            .Where(tile => TileSatisfiesTags(
                tile,
                transition.RequiredSourceTileTags,
                [],
                map.TileTags))
            .Any(sourceTile => Enum.GetValues<Direction>().Any(facing =>
                transition.CandidateOffsets.Any(offset =>
                    TryApplyRelativeOffset(
                        sourceTile,
                        facing,
                        offset,
                        out Position outputTile)
                    && !map.IsWall(outputTile)
                    && !reservedRespawnTiles.Contains(outputTile)
                    && outputTiles.Contains(outputTile)
                    && TileSatisfiesTags(
                        outputTile,
                        transition.RequiredOutputTileTags,
                        transition.ForbiddenOutputTileTags,
                        map.TileTags))));
        if (!feasible)
        {
            errors.Add(
                $"Participant {participantId} fabrication transition " +
                $"'{transition.TransitionId}' has no source-region tile and " +
                "rotated candidate offset reaching an eligible output-region floor tile.");
        }
    }

    private static void ValidateTransitionRegionRole(
        int participantId,
        string regionRoleId,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind> requiredTags,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind> forbiddenTags,
        IReadOnlyDictionary<
            (int ParticipantId, string RegionRoleId),
            ActorParticipantRegionAssignmentDefinition> regionAssignments,
        IReadOnlyDictionary<string, ActorMapRegionDefinition> mapRegions,
        ActorMapDefinition map,
        string transitionId,
        List<string> errors)
    {
        if (!regionAssignments.TryGetValue(
                (participantId, regionRoleId),
                out ActorParticipantRegionAssignmentDefinition? assignment))
        {
            errors.Add(
                $"Participant {participantId} needs region role '{regionRoleId}' " +
                $"for transition '{transitionId}'.");
            return;
        }
        if (!mapRegions.TryGetValue(
                assignment.MapRegionId,
                out ActorMapRegionDefinition? region)
            || region.Kind
                != ActorMapRegionDefinition.RegionKind.TransitionPlacement)
        {
            return;
        }

        if (!RegionHasEligibleTile(
                region,
                requiredTags,
                forbiddenTags,
                map.TileTags))
        {
            errors.Add(
                $"Participant {participantId} region role '{regionRoleId}' has no " +
                $"tile satisfying transition '{transitionId}' tag requirements.");
        }
    }

    private static bool RegionHasEligibleTile(
        ActorMapRegionDefinition region,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind> requiredTags,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind> forbiddenTags,
        IEnumerable<ActorMapTileTagDefinition> mapTags)
    {
        return region.Tiles.Any(tile =>
            TileSatisfiesTags(
                tile,
                requiredTags,
                forbiddenTags,
                mapTags));
    }

    private static void ValidateReplicationCapacity(
        IEnumerable<ActorReplicationTransitionDefinition> transitions,
        IReadOnlyDictionary<(int TeamId, int UnitId), PublicUnitSlot> slots,
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments,
        IReadOnlyDictionary<string, ActorLifecycleProfileDefinition>
            lifecycleProfiles,
        ActorMapDefinition map,
        IReadOnlySet<Position> reservedRespawnTiles,
        List<string> errors)
    {
        foreach (ActorReplicationTransitionDefinition transition in transitions)
        {
            if (transition is not SplitReplicationTransitionDefinition split)
                continue;

            PublicUnitSlot[] sourceSlots = FindSourceSlots(
                slots,
                assignments,
                transition.SourceFormIds);
            foreach (PublicUnitSlot sourceSlot in sourceSlots)
            {
                bool sourceSlotCompatible = assignments.TryGetValue(
                        (sourceSlot.TeamId, sourceSlot.UnitId),
                        out ActorUnitSlotLifecycleAssignmentDefinition?
                            sourceAssignment)
                    && sourceAssignment.AllowedFormIds.Contains(
                        split.OutputFormId,
                        StringComparer.Ordinal);
                if (!sourceSlotCompatible)
                {
                    errors.Add(
                        $"Split transition '{transition.TransitionId}' reused " +
                        $"source slot {sourceSlot.TeamId}:{sourceSlot.UnitId} " +
                        $"does not allow output form '{split.OutputFormId}'.");
                }
                int compatibleAdditionalSlots = slots.Values.Count(slot =>
                    slot.TeamId == sourceSlot.TeamId
                    && slot.ControllerParticipantId
                        == sourceSlot.ControllerParticipantId
                    && (slot.TeamId, slot.UnitId)
                        != (sourceSlot.TeamId, sourceSlot.UnitId)
                    && assignments.TryGetValue(
                        (slot.TeamId, slot.UnitId),
                        out ActorUnitSlotLifecycleAssignmentDefinition?
                            assignment)
                    && assignment.AllowedFormIds.Contains(
                        split.OutputFormId,
                        StringComparer.Ordinal)
                    && CanBecomeReadyForExplicitCreation(
                        assignment,
                        lifecycleProfiles));
                int compatibleSlotCount =
                    (sourceSlotCompatible ? 1 : 0)
                    + compatibleAdditionalSlots;
                if (compatibleSlotCount < split.DescendantCount)
                {
                    errors.Add(
                        $"Split transition '{transition.TransitionId}' has " +
                        $"insufficient same-controller output slots for source " +
                        $"{sourceSlot.TeamId}:{sourceSlot.UnitId}; needs " +
                        $"{split.DescendantCount}.");
                }
            }
            if (!SplitHasFeasiblePlacement(
                    split,
                    map,
                    reservedRespawnTiles))
            {
                errors.Add(
                    $"Split transition '{transition.TransitionId}' has no map " +
                    "floor pose with enough in-bounds candidate offsets for all descendants.");
            }
        }
    }

    private static bool CanBecomeReadyForExplicitCreation(
        ActorUnitSlotLifecycleAssignmentDefinition assignment,
        IReadOnlyDictionary<string, ActorLifecycleProfileDefinition>
            lifecycleProfiles)
    {
        if (assignment.InitialAvailability
            == ActorUnitSlotLifecycleAssignmentDefinition
                .InitialAvailabilityKind.DormantUnlockAtTick)
        {
            return true;
        }
        return lifecycleProfiles.TryGetValue(
                assignment.LifecycleProfileId,
                out ActorLifecycleProfileDefinition? profile)
            && profile.DestructionPolicy
                == ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .ReadyForExplicitFabrication;
    }

    private static bool SplitHasFeasiblePlacement(
        SplitReplicationTransitionDefinition split,
        ActorMapDefinition map,
        IReadOnlySet<Position> reservedRespawnTiles)
    {
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var source = new Position(x, y);
                if (map.IsWall(source))
                    continue;

                foreach (Direction facing in Enum.GetValues<Direction>())
                {
                    int eligibleCount = split.CandidateOffsets.Count(offset =>
                        TryApplyRelativeOffset(
                            source,
                            facing,
                            offset,
                            out Position output)
                        && !map.IsWall(output)
                        && !reservedRespawnTiles.Contains(output));
                    if (eligibleCount >= split.DescendantCount)
                        return true;
                }
            }
        }
        return false;
    }

    private static bool TileSatisfiesTags(
        Position tile,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind> requiredTags,
        IReadOnlyCollection<ActorMapTileTagDefinition.TileTagKind> forbiddenTags,
        IEnumerable<ActorMapTileTagDefinition> mapTags)
    {
        HashSet<ActorMapTileTagDefinition.TileTagKind> tileTagKinds = mapTags
            .Where(tag => tag.Tiles.Contains(tile))
            .Select(tag => tag.Kind)
            .ToHashSet();
        return requiredTags.All(tileTagKinds.Contains)
            && forbiddenTags.All(kind => !tileTagKinds.Contains(kind));
    }

    private static void ValidateSameLifeMapFeasibility(
        IEnumerable<ActorSameLifeTransitionDefinition> transitions,
        ActorMapDefinition map,
        List<string> errors)
    {
        foreach (ActorSameLifeTransitionDefinition transition in transitions)
        {
            bool hasLegalTile = false;
            for (int y = 0; y < map.Height && !hasLegalTile; y++)
            {
                for (int x = 0; x < map.Width && !hasLegalTile; x++)
                {
                    var tile = new Position(x, y);
                    hasLegalTile = !map.IsWall(tile)
                        && TileSatisfiesTags(
                            tile,
                            transition.Placement.RequiredTileTags,
                            transition.Placement.ForbiddenTileTags,
                            map.TileTags);
                }
            }
            if (!hasLegalTile)
            {
                errors.Add(
                    $"Same-life transition '{transition.TransitionId}' has no " +
                    "floor tile satisfying its placement-tag policy.");
            }
        }
    }

    private static void ValidateScoreAccumulatorBounds(
        ActorRulesDefinition rules,
        PublicMatchTopology topology,
        List<string> errors)
    {
        int maximumLifeHealth = rules.Forms
            .Select(form => form.MaxHealth)
            .DefaultIfEmpty(0)
            .Max();
        decimal maximumTotalDamage =
            (decimal)rules.Limits.MaxTicks
            * topology.UnitSlots.Length
            * maximumLifeHealth;
        if (maximumTotalDamage > long.MaxValue)
        {
            errors.Add(
                "MaxTicks, stable-slot count, and damage per hit can overflow " +
                "the signed 64-bit damage/score accumulator.");
        }
    }

    private static bool TryApplyRelativeOffset(
        Position source,
        Direction facing,
        ActorRelativePositionOffset offset,
        out Position output)
    {
        (long dx, long dy) = facing switch
        {
            Direction.North => (offset.Right, -(long)offset.Forward),
            Direction.East => (offset.Forward, offset.Right),
            Direction.South => (-(long)offset.Right, offset.Forward),
            Direction.West => (-(long)offset.Forward, -(long)offset.Right),
            _ => (long.MaxValue, long.MaxValue),
        };
        long x = source.X + dx;
        long y = source.Y + dy;
        if (x < int.MinValue
            || x > int.MaxValue
            || y < int.MinValue
            || y > int.MaxValue)
        {
            output = default;
            return false;
        }

        output = new Position((int)x, (int)y);
        return true;
    }

    private static PublicUnitSlot[] FindSourceSlots(
        IReadOnlyDictionary<(int TeamId, int UnitId), PublicUnitSlot> slots,
        IReadOnlyDictionary<
            (int TeamId, int UnitId),
            ActorUnitSlotLifecycleAssignmentDefinition> assignments,
        IEnumerable<string> sourceFormIds)
    {
        HashSet<string> sourceForms = sourceFormIds.ToHashSet(
            StringComparer.Ordinal);
        return slots.Values
            .Where(slot =>
                assignments.TryGetValue(
                    (slot.TeamId, slot.UnitId),
                    out ActorUnitSlotLifecycleAssignmentDefinition? assignment)
                && assignment.AllowedFormIds.Any(sourceForms.Contains))
            .OrderBy(slot => slot.TeamId)
            .ThenBy(slot => slot.UnitId)
            .ToArray();
    }
}
