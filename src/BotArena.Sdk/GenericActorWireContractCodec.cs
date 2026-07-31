namespace BotArena.Sdk;

/// <summary>
/// Tagged binary envelope for generic-v2 MatchStart. The embedded static
/// contract is always the original Engine-validated canonical UTF-8 JSON,
/// never a second SDK serialization of the typed view.
/// </summary>
internal static class GenericActorWireContractCodec
{
    public static byte[] EncodeMatchStart(GenericActorMatchStart value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(value, decoding: false);

        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(2, ActorWireValue.Int32(value.RuntimeContractVersion));
        writer.Field(3, ActorWireContractCodec.EncodeIdentity(value.ActorId));
        writer.Field(4, ActorWireValue.Int32(value.ParticipantId));
        writer.Field(5, ActorWireValue.UInt64(value.ActorRandomSeed));
        writer.Field(6, EncodeOrigin(value.Origin));
        writer.Field(
            7,
            ActorWireValue.String(
                value.Contract.CanonicalJson,
                GenericActorContractVersions.MaxCanonicalContractBytes));
        // Trailing additive field (#156 discipline): an artifact compiled
        // before the team stream existed simply ignores field 8 and keeps
        // negotiating and running.
        writer.Field(8, ActorWireValue.UInt64(value.TeamRandomSeed));
        return writer.ToArray();
    }

    public static GenericActorMatchStart DecodeMatchStart(
        byte[] bytes,
        int depth = 0)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            var reader = new ActorWireObjectReader(bytes, depth);
            // Optional so a frame produced by an encoder that predates the
            // team stream still decodes; such a frame carries no team seed,
            // and zero is still identical for every life on the team.
            byte[]? teamRandomSeed = reader.Optional(8);
            var result = new GenericActorMatchStart
            {
                SchemaVersion = ActorWireValue.Int32(reader.Required(1)),
                RuntimeContractVersion =
                    ActorWireValue.Int32(reader.Required(2)),
                ActorId = ActorWireContractCodec.DecodeIdentity(
                    reader.Required(3),
                    depth + 1),
                ParticipantId = ActorWireValue.Int32(reader.Required(4)),
                ActorRandomSeed =
                    ActorWireValue.UInt64(reader.Required(5)),
                TeamRandomSeed = teamRandomSeed is null
                    ? 0
                    : ActorWireValue.UInt64(teamRandomSeed),
                Origin = DecodeOrigin(reader.Required(6), depth + 1),
                Contract = ActorCanonicalContractReader.ParseUtf8(
                    reader.Required(7)),
            };
            Validate(result, decoding: true);
            return result;
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                "The generic actor MatchStart envelope is invalid.",
                exception);
        }
    }

    internal static byte[] EncodeOrigin(
        GenericActorMatchStart.LifeOrigin origin)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Enum(origin.Reason));
        writer.Field(2, ActorWireValue.Int32(origin.Generation));
        writer.Optional(
            3,
            origin.ParentActorId is null
                ? null
                : ActorWireContractCodec.EncodeIdentity(
                    origin.ParentActorId));
        writer.Optional(
            4,
            origin.SourceTransitionId is null
                ? null
                : GenericActorWireCodecValues.SemanticId(
                    origin.SourceTransitionId));
        writer.Optional(
            5,
            origin.SourceOperationId is null
                ? null
                : GenericActorWireCodecValues.Handle(
                    origin.SourceOperationId));
        return writer.ToArray();
    }

    internal static GenericActorMatchStart.LifeOrigin DecodeOrigin(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? parent = reader.Optional(3);
        byte[]? sourceTransition = reader.Optional(4);
        byte[]? sourceOperation = reader.Optional(5);
        return new GenericActorMatchStart.LifeOrigin(
            ActorWireValue.Enum<GenericActorMatchStart.SpawnReason>(
                reader.Required(1)),
            ActorWireValue.Int32(reader.Required(2)),
            parent is null
                ? null
                : ActorWireContractCodec.DecodeIdentity(
                    parent,
                    depth + 1),
            sourceTransition is null
                ? null
                : GenericActorWireCodecValues.SemanticId(
                    sourceTransition),
            sourceOperation is null
                ? null
                : GenericActorWireCodecValues.Handle(sourceOperation));
    }

    private static void Validate(
        GenericActorMatchStart value,
        bool decoding)
    {
        string message = "";
        if (value.SchemaVersion
            != GenericActorContractVersions.MatchStartSchemaVersion)
        {
            message = "MatchStart schema is unsupported.";
        }
        else if (value.RuntimeContractVersion
            != GenericActorContractVersions.RuntimeContractVersion)
        {
            message = "Runtime contract version is unsupported.";
        }
        else if (value.ParticipantId < 0)
        {
            message = "Participant ID cannot be negative.";
        }
        else if (value.ActorId is null)
        {
            message = "Actor identity is required.";
        }
        else if (value.Origin is null)
        {
            message = "Life origin is required.";
        }
        else if (value.Origin.Generation < 0)
        {
            message = "Life generation cannot be negative.";
        }
        else if (value.Contract is null)
        {
            message = "Static match contract is required.";
        }
        else if (value.Contract.CapabilityVersions.RuntimeContractVersion
            != value.RuntimeContractVersion
            || value.Contract.CapabilityVersions.MatchStartSchemaVersion
                != value.SchemaVersion)
        {
            message =
                "MatchStart scalars disagree with the static capability tuple.";
        }
        else if (!HasOwnedSlot(value))
        {
            message =
                "Actor identity is not owned by the declared participant.";
        }
        else if (!HasValidOrigin(value))
        {
            message =
                "Life origin lineage is inconsistent with its spawn reason.";
        }

        if (message.Length == 0)
            return;
        if (decoding)
            throw new FormatException(message);
        throw new ArgumentException(message, nameof(value));
    }

    private static bool HasOwnedSlot(GenericActorMatchStart value)
    {
        GenericActorResolvedMatchContract.MatchTopology topology =
            value.Contract.Topology;
        PublicParticipant? participant = topology.Participants.FirstOrDefault(
            candidate =>
                candidate.ParticipantId == value.ParticipantId);
        if (participant is null || participant.TeamId != value.ActorId.TeamId)
            return false;
        return topology.UnitSlots.Any(
            slot =>
                slot.TeamId == value.ActorId.TeamId
                && slot.UnitId == value.ActorId.UnitId
                && slot.ControllerParticipantId == value.ParticipantId);
    }

    private static bool HasValidOrigin(GenericActorMatchStart value)
    {
        GenericActorMatchStart.LifeOrigin origin = value.Origin;
        if (origin.ParentActorId is not null
            && !IsOwnedActorSlot(value, origin.ParentActorId))
        {
            return false;
        }

        return origin.Reason switch
        {
            GenericActorMatchStart.SpawnReason.Initial =>
                origin.Generation == 0
                && value.Contract.Topology.InitialLives.Any(
                    life =>
                        life.TeamId == value.ActorId.TeamId
                        && life.UnitId == value.ActorId.UnitId
                        && life.LifeId == value.ActorId.LifeId)
                && origin.ParentActorId is null
                && origin.SourceTransitionId is null
                && origin.SourceOperationId is null,
            GenericActorMatchStart.SpawnReason.AutomaticReturn =>
                HasValidAutomaticReturn(value, origin),
            GenericActorMatchStart.SpawnReason.Fabrication =>
                HasValidFabrication(value, origin),
            GenericActorMatchStart.SpawnReason.Replication =>
                HasValidReplication(value, origin),
            GenericActorMatchStart.SpawnReason.AutomaticActivation =>
                HasValidAutomaticActivation(value, origin),
            GenericActorMatchStart.SpawnReason.RootFactorySeed =>
                HasValidRootFactorySeed(value, origin),
            _ => false,
        };
    }

    /// <summary>
    /// A root-factory seed is the base's own placement: a fresh lineage, no
    /// parent, no transition, on a slot whose profile actually declares the
    /// bootstrap and whose home spawn exists to receive it.
    /// </summary>
    private static bool HasValidRootFactorySeed(
        GenericActorMatchStart value,
        GenericActorMatchStart.LifeOrigin origin)
    {
        GenericActorResolvedMatchContract.LifecycleAssignment? assignment =
            FindAssignment(value);
        if (assignment is null)
            return false;
        GenericActorRulesContract.LifecycleProfile? profile =
            value.Contract.Rules.Lifecycle.Profiles.FirstOrDefault(
                candidate =>
                    candidate.ProfileId == assignment.LifecycleProfileId);
        return origin.Generation == 0
            && origin.ParentActorId is null
            && origin.SourceTransitionId is null
            && origin.SourceOperationId is null
            && assignment.AssignedRespawnSpawnId is not null
            && profile?.RootFactorySeedFormId is not null;
    }

    private static bool HasValidAutomaticActivation(
        GenericActorMatchStart value,
        GenericActorMatchStart.LifeOrigin origin)
    {
        GenericActorResolvedMatchContract.LifecycleAssignment? assignment =
            FindAssignment(value);
        if (assignment is null)
            return false;
        GenericActorRulesContract.LifecycleProfile? profile =
            value.Contract.Rules.Lifecycle.Profiles.FirstOrDefault(
                candidate =>
                    candidate.ProfileId == assignment.LifecycleProfileId);
        return value.ActorId.LifeId == 0
            && origin.Generation == assignment.InitialGeneration
            && origin.ParentActorId is null
            && origin.SourceTransitionId is null
            && origin.SourceOperationId is null
            && assignment.InitialAvailability
                == GenericActorResolvedMatchContract.InitialAvailability
                    .DormantAutomaticActivationAtTick
            && assignment.UnlockTick is not null
            && assignment.AssignedRespawnSpawnId is not null
            && profile?.DestructionPolicy == "automatic-respawn";
    }

    private static bool HasValidAutomaticReturn(
        GenericActorMatchStart value,
        GenericActorMatchStart.LifeOrigin origin)
    {
        GenericActorResolvedMatchContract.LifecycleAssignment? assignment =
            FindAssignment(value);
        if (assignment is null)
            return false;
        GenericActorRulesContract.LifecycleProfile? profile =
            value.Contract.Rules.Lifecycle.Profiles.FirstOrDefault(
                candidate =>
                    candidate.ProfileId
                        == assignment.LifecycleProfileId);
        return origin.ParentActorId is not null
            && origin.ParentActorId.TeamId == value.ActorId.TeamId
            && origin.ParentActorId.UnitId == value.ActorId.UnitId
            && origin.ParentActorId.LifeId < value.ActorId.LifeId
            && origin.SourceTransitionId is null
            && origin.SourceOperationId is null
            && profile?.DestructionPolicy == "automatic-respawn";
    }

    private static bool HasValidFabrication(
        GenericActorMatchStart value,
        GenericActorMatchStart.LifeOrigin origin)
    {
        if (!HasOperation(value, origin)
            || origin.Generation < 1
            || (
                origin.ParentActorId!.TeamId,
                origin.ParentActorId.UnitId)
                == (value.ActorId.TeamId, value.ActorId.UnitId))
        {
            return false;
        }
        GenericActorRulesContract.BoundedChildFabricationTransition?
            transition = FindUnique(
                value.Contract.Rules.FabricationTransitions
                    .OfType<
                        GenericActorRulesContract
                            .BoundedChildFabricationTransition>(),
                candidate =>
                    candidate.TransitionId
                        == origin.SourceTransitionId);
        return transition is not null
            && AssignmentAllows(value, transition.OutputFormId);
    }

    private static bool HasValidReplication(
        GenericActorMatchStart value,
        GenericActorMatchStart.LifeOrigin origin)
    {
        if (!HasOperation(value, origin)
            || origin.ParentActorId == value.ActorId
            || (origin.ParentActorId!.TeamId == value.ActorId.TeamId
                && origin.ParentActorId.UnitId == value.ActorId.UnitId
                && origin.ParentActorId.LifeId >= value.ActorId.LifeId))
        {
            return false;
        }
        GenericActorRulesContract.SplitReplicationTransition? transition =
            FindUnique(
                value.Contract.Rules.ReplicationTransitions
                    .OfType<
                        GenericActorRulesContract
                            .SplitReplicationTransition>(),
                candidate =>
                    candidate.TransitionId
                        == origin.SourceTransitionId);
        if (transition is null)
            return false;
        long maximumOutputGeneration =
            (long)transition.MaxSourceGeneration
            + transition.DescendantGenerationIncrement;
        return origin.Generation
                >= transition.DescendantGenerationIncrement
            && origin.Generation <= maximumOutputGeneration
            && AssignmentAllows(value, transition.OutputFormId);
    }

    private static bool HasOperation(
        GenericActorMatchStart value,
        GenericActorMatchStart.LifeOrigin origin) =>
        origin.ParentActorId is not null
        && IsOwnedActorSlot(value, origin.ParentActorId)
        && origin.ParentActorId.TeamId == value.ActorId.TeamId
        && IsSemanticId(origin.SourceTransitionId)
        && IsOperationHandle(origin.SourceOperationId);

    private static bool IsSemanticId(string? value)
    {
        if (value is null)
            return false;
        try
        {
            _ = GenericActorDynamicValueRules.SemanticId(
                value,
                nameof(value));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsOperationHandle(string? value)
    {
        if (value is null)
            return false;
        try
        {
            _ = GenericActorDynamicValueRules.Handle(
                value,
                nameof(value));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool AssignmentAllows(
        GenericActorMatchStart value,
        string formId) =>
        FindAssignment(value)?.AllowedFormIds.Contains(
            formId,
            StringComparer.Ordinal) == true;

    private static GenericActorResolvedMatchContract.LifecycleAssignment?
        FindAssignment(GenericActorMatchStart value) =>
        FindUnique(
            value.Contract.LifecycleAssignments,
            assignment =>
                assignment.TeamId == value.ActorId.TeamId
                && assignment.UnitId == value.ActorId.UnitId);

    private static T? FindUnique<T>(
        IEnumerable<T> values,
        Func<T, bool> predicate)
        where T : class
    {
        using IEnumerator<T> matches =
            values.Where(predicate).Take(2).GetEnumerator();
        if (!matches.MoveNext())
            return null;
        T result = matches.Current;
        return matches.MoveNext() ? null : result;
    }

    private static bool IsOwnedActorSlot(
        GenericActorMatchStart value,
        ActorIdentity identity) =>
        value.Contract.Topology.UnitSlots.Any(
            slot =>
                slot.TeamId == identity.TeamId
                && slot.UnitId == identity.UnitId
                && slot.ControllerParticipantId == value.ParticipantId);
}
