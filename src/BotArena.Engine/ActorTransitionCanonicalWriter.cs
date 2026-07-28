using System.Text.Json;

namespace BotArena.Engine;

/// <summary>Canonical rules-schema writers for all lifecycle action unions.</summary>
internal static class ActorTransitionCanonicalWriter
{
    public static void WriteFabricationTransitions(
        Utf8JsonWriter writer,
        IEnumerable<ActorFabricationTransitionDefinition> transitions)
    {
        writer.WritePropertyName("fabricationTransitions");
        writer.WriteStartArray();
        foreach (ActorFabricationTransitionDefinition transition in transitions)
        {
            switch (transition)
            {
                case BoundedChildFabricationDefinition bounded:
                    WriteBoundedFabrication(writer, bounded);
                    break;
                default:
                    throw Unsupported(transition);
            }
        }
        writer.WriteEndArray();
    }

    public static void WriteSameLifeTransitions(
        Utf8JsonWriter writer,
        IEnumerable<ActorSameLifeTransitionDefinition> transitions)
    {
        writer.WritePropertyName("sameLifeTransitions");
        writer.WriteStartArray();
        foreach (ActorSameLifeTransitionDefinition transition in transitions)
        {
            switch (transition)
            {
                case ActorFormTransitionDefinition form:
                    WriteFormTransition(writer, form);
                    break;
                default:
                    throw Unsupported(transition);
            }
        }
        writer.WriteEndArray();
    }

    public static void WriteReplicationTransitions(
        Utf8JsonWriter writer,
        IEnumerable<ActorReplicationTransitionDefinition> transitions)
    {
        writer.WritePropertyName("replicationTransitions");
        writer.WriteStartArray();
        foreach (ActorReplicationTransitionDefinition transition in transitions)
        {
            switch (transition)
            {
                case SplitReplicationTransitionDefinition split:
                    WriteSplit(writer, split);
                    break;
                default:
                    throw Unsupported(transition);
            }
        }
        writer.WriteEndArray();
    }

    private static void WriteBoundedFabrication(
        Utf8JsonWriter writer,
        BoundedChildFabricationDefinition transition)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Id(transition.Kind));
        writer.WriteString("transitionId", transition.TransitionId);
        writer.WriteString("actionId", transition.ActionId);
        WriteStrings(writer, "sourceFormIds", transition.SourceFormIds);
        writer.WriteString("outputFormId", transition.OutputFormId);
        writer.WriteNumber("outputCount", transition.OutputCount);
        writer.WriteString(
            "sourceRegionRoleId",
            transition.SourceRegionRoleId);
        writer.WriteString(
            "outputRegionRoleId",
            transition.OutputRegionRoleId);
        WriteEnumIds(
            writer,
            "requiredSourceTileTags",
            transition.RequiredSourceTileTags);
        WriteEnumIds(
            writer,
            "requiredOutputTileTags",
            transition.RequiredOutputTileTags);
        WriteEnumIds(
            writer,
            "forbiddenOutputTileTags",
            transition.ForbiddenOutputTileTags);
        WriteOffsets(writer, transition.CandidateOffsets);
        writer.WritePropertyName("delay");
        WriteFabricationDelay(writer, transition.Delay);
        writer.WriteString(
            "unavailablePlacementResult",
            Id(transition.UnavailablePlacementResult));
        writer.WriteString("targetSlot", Id(transition.TargetSlot));
        writer.WriteString(
            "candidateSnapshot",
            Id(transition.CandidateSnapshot));
        writer.WriteString(
            "positionSelection",
            Id(transition.PositionSelection));
        writer.WriteString("claimScope", Id(transition.ClaimScope));
        writer.WriteString(
            "conflictResolution",
            Id(transition.ConflictResolution));
        writer.WriteString(
            "sourceDisposition",
            Id(transition.SourceDisposition));
        writer.WriteString(
            "childInitialState",
            Id(transition.ChildInitialState));
        writer.WriteString(
            "outputFacing",
            Id(transition.OutputFacing));
        writer.WriteString(
            "candidateReference",
            Id(transition.CandidateReference));
        writer.WriteString("lineage", Id(transition.Lineage));
        writer.WriteString("outputHealth", Id(transition.OutputHealth));
        writer.WriteString("spawnReason", Id(transition.SpawnReason));
        writer.WriteString(
            "offsetArithmetic",
            Id(transition.OffsetArithmetic));
        writer.WriteString(
            "outstandingBundles",
            Id(transition.OutstandingBundles));
        writer.WriteEndObject();
    }

    private static void WriteFabricationDelay(
        Utf8JsonWriter writer,
        ActorFabricationDelayDefinition delay)
    {
        writer.WriteStartObject();
        writer.WriteNumber("durationTicks", delay.DurationTicks);
        writer.WriteString("sourceBehavior", Id(delay.SourceBehavior));
        writer.WriteString("sourceDeath", Id(delay.SourceDeath));
        writer.WriteString(
            "sourceRetirement",
            Id(delay.SourceRetirement));
        writer.WriteString("reservation", Id(delay.Reservation));
        writer.WriteString("completion", Id(delay.Completion));
        writer.WriteString(
            "tickArithmetic",
            Id(delay.TickArithmetic));
        writer.WriteEndObject();
    }

    private static void WriteFormTransition(
        Utf8JsonWriter writer,
        ActorFormTransitionDefinition transition)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Id(transition.Kind));
        writer.WriteString("transitionId", transition.TransitionId);
        writer.WriteString("actionId", transition.ActionId);
        writer.WriteString("sourceFormId", transition.SourceFormId);
        writer.WriteString("targetFormId", transition.TargetFormId);
        writer.WritePropertyName("windup");
        WriteWindup(writer, transition.Windup);
        writer.WriteString(
            "memoryContinuity",
            Id(transition.MemoryContinuity));
        writer.WritePropertyName("health");
        WriteSameLifeHealth(writer, transition.Health);
        writer.WritePropertyName("combatState");
        WriteSameLifeCombatState(writer, transition.CombatState);
        writer.WritePropertyName("placement");
        WriteSameLifePlacement(writer, transition.Placement);
        writer.WriteBoolean(
            "irreversibleForLife",
            transition.IrreversibleForLife);
        writer.WriteEndObject();
    }

    private static void WriteSameLifeHealth(
        Utf8JsonWriter writer,
        ActorSameLifeHealthDefinition health)
    {
        writer.WriteStartObject();
        writer.WriteString("policy", Id(health.Policy));
        writer.WriteNumber("flatHealthGain", health.FlatHealthGain);
        writer.WriteString("evaluation", Id(health.Evaluation));
        writer.WriteString("arithmetic", Id(health.Arithmetic));
        writer.WriteString(
            "preserveRatioFormula",
            Id(health.PreserveRatioFormula));
        writer.WriteEndObject();
    }

    private static void WriteSameLifeCombatState(
        Utf8JsonWriter writer,
        ActorSameLifeCombatStateDefinition combat)
    {
        writer.WriteStartObject();
        writer.WriteString(
            "cooldownContinuity",
            Id(combat.CooldownContinuity));
        writer.WriteString(
            "energyContinuity",
            Id(combat.EnergyContinuity));
        writer.WriteEndObject();
    }

    private static void WriteSameLifePlacement(
        Utf8JsonWriter writer,
        ActorSameLifePlacementDefinition placement)
    {
        writer.WriteStartObject();
        writer.WriteString(
            "positionContinuity",
            Id(placement.PositionContinuity));
        writer.WriteString(
            "legalityEvaluation",
            Id(placement.LegalityEvaluation));
        WriteEnumIds(
            writer,
            "requiredTileTags",
            placement.RequiredTileTags);
        WriteEnumIds(
            writer,
            "forbiddenTileTags",
            placement.ForbiddenTileTags);
        writer.WriteString(
            "failedCompletion",
            Id(placement.FailedCompletion));
        writer.WriteEndObject();
    }

    private static void WriteSplit(
        Utf8JsonWriter writer,
        SplitReplicationTransitionDefinition transition)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Id(transition.Kind));
        writer.WriteString("transitionId", transition.TransitionId);
        writer.WriteString("actionId", transition.ActionId);
        WriteStrings(writer, "sourceFormIds", transition.SourceFormIds);
        writer.WriteString("outputFormId", transition.OutputFormId);
        writer.WriteNumber("descendantCount", transition.DescendantCount);
        writer.WriteNumber(
            "maxSourceGeneration",
            transition.MaxSourceGeneration);
        writer.WriteBoolean(
            "requireNoPriorSameLifeTransition",
            transition.RequireNoPriorSameLifeTransition);
        writer.WritePropertyName("health");
        WriteReplicationHealth(writer, transition.Health);
        writer.WriteNumber(
            "minimumSourceHealth",
            transition.MinimumSourceHealth);
        WriteOffsets(writer, transition.CandidateOffsets);
        writer.WritePropertyName("windup");
        WriteWindup(writer, transition.Windup);
        writer.WriteNumber(
            "descendantGenerationIncrement",
            transition.DescendantGenerationIncrement);
        writer.WriteBoolean(
            "reservationIsAtomic",
            transition.ReservationIsAtomic);
        writer.WriteBoolean(
            "conflictingBundlesAllBlock",
            transition.ConflictingBundlesAllBlock);
        writer.WriteBoolean(
            "insufficientHealthBlocks",
            transition.InsufficientHealthBlocks);
        writer.WriteBoolean(
            "reuseSourceSlotFirst",
            transition.ReuseSourceSlotFirst);
        writer.WriteBoolean(
            "additionalSlotsUseLowestCompatibleDormantId",
            transition.AdditionalSlotsUseLowestCompatibleDormantId);
        writer.WriteBoolean(
            "sourceRemainsTargetableUntilCompletion",
            transition.SourceRemainsTargetableUntilCompletion);
        writer.WriteBoolean(
            "lethalSourceDamageCancels",
            transition.LethalSourceDamageCancels);
        writer.WriteBoolean(
            "sourceRetirementCountsAsDestruction",
            transition.SourceRetirementCountsAsDestruction);
        writer.WriteBoolean(
            "descendantsUseFreshIsolatedRuntimes",
            transition.DescendantsUseFreshIsolatedRuntimes);
        writer.WriteBoolean(
            "descendantsInheritPrivateMemory",
            transition.DescendantsInheritPrivateMemory);
        writer.WriteString(
            "candidateSnapshot",
            Id(transition.CandidateSnapshot));
        writer.WriteString(
            "positionSelection",
            Id(transition.PositionSelection));
        writer.WriteString(
            "slotSelection",
            Id(transition.SlotSelection));
        writer.WriteString(
            "descendantAssignment",
            Id(transition.DescendantAssignment));
        writer.WriteString("claimScope", Id(transition.ClaimScope));
        writer.WriteString(
            "conflictResolution",
            Id(transition.ConflictResolution));
        writer.WriteString(
            "healthEvaluation",
            Id(transition.HealthEvaluation));
        writer.WriteString(
            "offsetArithmetic",
            Id(transition.OffsetArithmetic));
        writer.WriteEndObject();
    }

    private static void WriteReplicationHealth(
        Utf8JsonWriter writer,
        ActorReplicationHealthDefinition health)
    {
        writer.WriteStartObject();
        writer.WriteString("distribution", Id(health.Distribution));
        writer.WriteNumber(
            "minimumHealthPerDescendant",
            health.MinimumHealthPerDescendant);
        writer.WriteString("remainder", Id(health.Remainder));
        writer.WriteString("maximumHealth", Id(health.MaximumHealth));
        writer.WriteEndObject();
    }

    private static void WriteWindup(
        Utf8JsonWriter writer,
        ActorTransitionWindupDefinition windup)
    {
        writer.WriteStartObject();
        writer.WriteNumber("durationTicks", windup.DurationTicks);
        writer.WriteString("pendingAction", Id(windup.PendingAction));
        writer.WriteString("sourceForm", Id(windup.SourceForm));
        writer.WriteString("targetability", Id(windup.Targetability));
        writer.WriteString("lethalDamage", Id(windup.LethalDamage));
        writer.WriteString("completion", Id(windup.Completion));
        writer.WriteString(
            "placementReference",
            Id(windup.PlacementReference));
        writer.WriteEndObject();
    }

    private static void WriteOffsets(
        Utf8JsonWriter writer,
        IEnumerable<ActorRelativePositionOffset> offsets)
    {
        writer.WritePropertyName("candidateOffsets");
        writer.WriteStartArray();
        foreach (ActorRelativePositionOffset offset in offsets)
        {
            writer.WriteStartObject();
            writer.WriteNumber("forward", offset.Forward);
            writer.WriteNumber("right", offset.Right);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteStrings(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (string value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static void WriteEnumIds<T>(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<T> values)
        where T : struct, Enum
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (T value in values)
            writer.WriteStringValue(Id(value));
        writer.WriteEndArray();
    }

    private static string Id(Enum value) =>
        ActorContractCanonicalIds.Id(value);

    private static ArgumentOutOfRangeException Unsupported(object value) =>
        new(
            nameof(value),
            value,
            "The generation-3 writer does not support this transition variant.");
}
