namespace BotArena.Engine.Tests;

/// <summary>
/// One honest, completed mind-profile document, built once and shared by every
/// forgery test. Building it is the expensive part; doctoring it is not.
/// <para>
/// It is deliberately a match with role tags, a body that dies and returns, and
/// commands the host both accepted and rejected, so the forgeries have real
/// evidence to doctor rather than a degenerate one-tick fixture.
/// </para>
/// </summary>
internal static class GenericMindForgeryFixture
{
    public static Lazy<string> Document { get; } = new(Build);

    private static string Build()
    {
        ActorResolvedMatchDefinition definition =
            GenericMindSessionTestFixture.OnMindProfile(
                FrontlineLabsDefinition
                    .CreateAutomaticCompanionsExperiment());
        Dictionary<int, GenericMindSessionTestFixture.RecordingMindFactory>
            factories = GenericMindSessionTestFixture.Factories(
                definition,
                (_, observation) => Think(definition, observation));
        using var session = new GenericActorMatchSession(
            definition,
            GenericMindSessionTestFixture.Configurations(
                definition,
                factories),
            matchSeed: 4_919);
        session.Run();
        return GenericActorReplayDocument.Create(session.Chronology)
            .CanonicalJson;
    }

    /// <summary>
    /// The scripted doctrine plus role assignment, plus one deliberately
    /// doomed command every tick: naming a body this mind does not own is the
    /// forgivable mistake §2.4 says must be Rejected rather than Faulted, and
    /// recording it is what the "a Rejected command is legitimate evidence"
    /// rule means.
    /// </summary>
    private static GenericMindRuntimeDecisions Think(
        ActorResolvedMatchDefinition definition,
        GenericMindRuntimeObservation observation)
    {
        ActorActionDefinition wait = definition.Rules.Actions
            .First(action => action.Kind == ActorActionKind.Wait);
        return new GenericMindRuntimeDecisions(
        [
            .. observation.Bodies.Select((body, index) =>
            {
                GenericActorRuntimeDecision decision =
                    GenericMindSessionTestFixture.Script(
                        definition,
                        body.ActorId,
                        observation.Tick);
                return new GenericMindCommand(
                    body.ActorId.UnitId,
                    body.ActorId.LifeId,
                    decision.ActionId,
                    decision.ActionCode,
                    decision.Arguments,
                    body.RoleTag is null
                        ? index == 0 ? "channeler" : "screen"
                        : null);
            }),
            // A body that has never existed: Rejected, recorded, non-fatal.
            new GenericMindCommand(
                UnitId: 31,
                LifeId: 0,
                wait.Id,
                wait.Code,
                []),
        ]);
    }
}
