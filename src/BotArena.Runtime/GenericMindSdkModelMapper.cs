using System.Collections.Immutable;
using BotArena.Engine;
using Sdk = BotArena.Sdk;

namespace BotArena.Runtime;

/// <summary>
/// Explicit adapter between the deliberately independent Engine and SDK MIND
/// DTOs, following the same discipline as
/// <see cref="GenericActorSdkModelMapper"/>: static contracts cross as
/// canonical Engine-authored JSON, and dynamic unions are mapped case by case
/// so additions FAIL CLOSED rather than silently dropping a case.
///
/// <para>Every team-shared collection is projected by the ACTOR mapper,
/// unchanged. That reuse is the point: a mind's enemies, tiles, events, sounds,
/// scoreboard and mode are the same objects the per-life profile would have
/// delivered, so the two profiles cannot drift apart in a field nobody is
/// looking at.</para>
/// </summary>
internal static class GenericMindSdkModelMapper
{
    public static Sdk.MindStart ToSdk(GenericMindRuntimeStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return new Sdk.MindStart
        {
            SchemaVersion = start.SchemaVersion,
            RuntimeContractVersion = start.RuntimeContractVersion,
            ParticipantId = start.ParticipantId,
            TeamId = start.TeamId,
            AlliedParticipantIds = start.AlliedParticipantIds,
            MindRandomSeed = start.MindRandomSeed,
            TeamRandomSeed = start.TeamRandomSeed,
            Contract = Sdk.ActorCanonicalContractReader.Parse(
                ActorContractManifestSerializer.ToCanonicalJson(
                    start.Contract)),
            EvaluationData = start.EvaluationData,
        };
    }

    /// <summary>
    /// Resolves the contract's wait action once, so every
    /// <see cref="Sdk.MindBody"/> can hold itself without searching the action
    /// catalog on every tick of every body.
    /// </summary>
    public static Sdk.MindWaitAction WaitActionOf(
        Sdk.GenericActorResolvedMatchContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        Sdk.GenericActorRulesContract.ActionDefinition? wait =
            contract.Rules.Actions.FirstOrDefault(action =>
                action.Kind == Sdk.GenericActorRulesContract.ActionKind.Wait);
        return wait is null
            ? new Sdk.MindWaitAction(null, 0)
            : new Sdk.MindWaitAction(wait.Id, wait.Code);
    }

    public static Sdk.MindContext ToSdk(
        GenericMindRuntimeObservation observation,
        Sdk.MindWaitAction waitAction)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new Sdk.MindContext(
            observation.SchemaVersion,
            observation.Tick,
            observation.MatchContractFingerprint,
            observation.Bodies.Select(body => ToSdk(body, waitAction)),
            observation.Slots.Select(ToSdk),
            observation.Allies.Select(GenericActorSdkModelMapper.ToSdk),
            observation.Enemies.Select(GenericActorSdkModelMapper.ToSdk),
            observation.Team.VisibleTiles.Select(
                GenericActorSdkModelMapper.ToSdk),
            observation.Team.VisibleProjectiles?.Select(
                GenericActorSdkModelMapper.ToSdk),
            observation.Team.VisibleEvents.Select(
                GenericActorSdkModelMapper.ToSdk),
            observation.Team.HeardSounds?.Select(
                GenericActorSdkModelMapper.ToSdk),
            GenericActorSdkModelMapper.ToSdk(observation.Team.Scoreboard),
            GenericActorSdkModelMapper.ToSdk(observation.Team.Mode),
            observation.Team.Participants.Select(
                GenericActorSdkModelMapper.ToSdk),
            observation.AlliedIntents.Select(ToSdk));
    }

    /// <summary>
    /// Exact-use projection for the frozen first-party Arc Relay stock mind.
    /// That algorithm never reads slot state, public event history, sounds,
    /// participant status or unreserved visible tiles. Omitting those unused
    /// SDK allocations changes no decision and is admitted only on the
    /// trusted product lane; audit/WASM and general in-process runtimes retain
    /// the complete contract projection above.
    /// </summary>
    public static Sdk.MindContext ToTrustedArcRelayStockSdk(
        GenericMindRuntimeObservation observation,
        Sdk.MindWaitAction waitAction)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new Sdk.MindContext(
            observation.SchemaVersion,
            observation.Tick,
            observation.MatchContractFingerprint,
            observation.Bodies.Select(body => ToSdk(body, waitAction)),
            [],
            observation.Allies.Select(GenericActorSdkModelMapper.ToSdk),
            observation.Enemies.Select(GenericActorSdkModelMapper.ToSdk),
            observation.Team.VisibleTiles
                .Where(value => value.SpawnReservation is not null)
                .Select(GenericActorSdkModelMapper.ToSdk),
            observation.Team.VisibleProjectiles?.Select(
                GenericActorSdkModelMapper.ToSdk),
            [],
            null,
            GenericActorSdkModelMapper.ToSdk(observation.Team.Scoreboard),
            GenericActorSdkModelMapper.ToSdk(observation.Team.Mode),
            [],
            []);
    }

    public static GenericMindRuntimeDecisions ToEngine(
        Sdk.MindDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return new GenericMindRuntimeDecisions(
            [.. decisions.Commands.Select(ToEngine)],
            [.. decisions.Intents.Select(ToEngine)],
            decisions.DebugMessage);
    }

    private static Sdk.MindBody ToSdk(
        GenericMindRuntimeObservation.ObservedBodyState value,
        Sdk.MindWaitAction waitAction) =>
        new(
            GenericActorSdkModelMapper.ToSdk(value.ActorId),
            value.Generation,
            value.FormId,
            GenericActorSdkModelMapper.ToSdk(value.Position),
            GenericActorSdkModelMapper.ToSdk(value.Facing),
            value.Health,
            value.Cooldown,
            value.Energy,
            GenericActorSdkModelMapper.ToSdk(value.PreviousActionResolution),
            GenericActorSdkModelMapper.ToSdk(value.PendingSameLifeTransition),
            value.ClassId,
            GenericActorSdkModelMapper.ToSdk(value.RouteCooldowns),
            value.CarriedScrap,
            value.PreviousPosition is { } previous
                ? GenericActorSdkModelMapper.ToSdk(previous)
                : null,
            value.MovedLastTick,
            value.LifeStartedTick,
            GenericActorSdkModelMapper.ToSdk(value.Origin),
            value.RoleTag,
            value.BodyRandomSeed,
            [
                .. value.ActionLegalities.Select(
                    GenericActorSdkModelMapper.ToSdk),
            ],
            waitAction);

    private static Sdk.MindSlot ToSdk(
        GenericMindRuntimeObservation.ObservedOwnSlot value) =>
        new(
            value.UnitId,
            GenericActorSdkModelMapper.ToSdk(value.State),
            value.ClassId,
            value.CandidateClassIds,
            value.SelectedClassId);

    private static Sdk.MindContext.AlliedIntent ToSdk(
        GenericMindRuntimeObservation.AlliedIntent value) =>
        new(value.ParticipantId, value.TagId, value.Value);

    private static GenericMindCommand ToEngine(Sdk.MindCommand value) =>
        new(
            value.UnitId,
            value.LifeId,
            value.ActionId,
            value.ActionCode,
            [.. value.Arguments.Select(GenericActorSdkModelMapper.ToEngine)],
            value.RoleTag,
            value.DebugMessage);

    private static GenericMindDeclaredIntent ToEngine(
        Sdk.MindDeclaredIntent value) =>
        new(value.TagId, value.Value);
}
