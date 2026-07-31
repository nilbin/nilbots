using BotArena.Engine;
using BotArena.Sdk;

namespace BotArena.Runtime.Wasm.Tests;

/// <summary>
/// The payload budget at a full LEGION roster. The roster arm puts up to
/// seventeen bodies in one match, and every one of them receives the whole
/// canonical team observation each tick — allies, visible enemies, projectiles,
/// team slot states, the mode block with its scrap piles — through the host
/// frame the wire protocol bounds. This measures the real encoder on the real
/// fullest tick of a real match rather than arguing about it.
/// </summary>
public sealed class GenericActorLegionObservationSizeTests
{
    [Fact]
    public void AFullRosterObservationFitsTheHostPayloadBudget()
    {
        ActorResolvedMatchDefinition legion =
            FrontlineLabsDefinition.CreatePendulumExperiment(
                // The next round's package: the keel without its rally, so
                // every body walks home, on the 750-tick horizon.
                FrontlineLabsPendulumArm.StickyFrontline
                | FrontlineLabsPendulumArm.ContestMajority
                | FrontlineLabsPendulumArm.EnemySoleDecay,
                (FrontlineLabsClassDefinition.Fabricator,
                    FrontlineLabsClassDefinition.Striker),
                movementCoupling: ActorMovementFacingCoupling.FacingLocked,
                skills: FrontlineLabsSkillKit.StrikerVolley
                    | FrontlineLabsSkillKit.BulwarkAegisShell
                    | FrontlineLabsSkillKit.FabricatorFiveSlots,
                bendEnvelope: FrontlineLabsBendEnvelopeArm.Universal,
                fiveSlots: FrontlineLabsFiveSlotVariant.Wane,
                stanceGround: FrontlineLabsStanceGroundArm.Open,
                aim: FrontlineLabsAimArm.Offset,
                cooldown: FrontlineLabsCooldownArm.Ticking,
                volley: FrontlineLabsVolleyArm.Salvo,
                capture: FrontlineLabsCaptureArm.Channel,
                economy: FrontlineLabsEconomyArm.Scrap,
                roster: FrontlineLabsRosterArm.Legion,
                horizon: FrontlineLabsHorizonArm.Long);

        var factories = legion.Topology.Participants.ToDictionary(
            participant => participant.ParticipantId,
            participant => new CrowdFactory());
        using var session = new GenericActorMatchSession(
            legion,
            [
                .. legion.Topology.Participants.Select(participant =>
                    new GenericActorParticipantConfiguration
                    {
                        ParticipantId = participant.ParticipantId,
                        TeamId = participant.TeamId,
                        Name = $"participant-{participant.ParticipantId}",
                        RuntimeFactory = factories[participant.ParticipantId],
                    }),
            ],
            matchSeed: 7UL);
        session.Run();

        GenericActorMatchTickFrame fullest = session.Chronology.Ticks
            .OrderByDescending(frame => frame.PostState.ActiveLives.Length)
            .ThenBy(frame => frame.Tick)
            .First();
        Assert.True(
            fullest.PostState.ActiveLives.Length >= 16,
            $"the roster never filled: {fullest.PostState.ActiveLives.Length}");

        int largest = 0;
        foreach (GenericActorMatchActorTurn turn in fullest.ActorTurns)
        {
            largest = Math.Max(
                largest,
                GenericActorWasmProtocol
                    .FormatObservation(turn.Observation)
                    .Length);
        }

        int budget = ActorWireProtocol.MaxHostFrameBytes;
        Assert.True(
            largest < budget,
            $"a full-roster observation frame is {largest} bytes of {budget}");
        // Measured: ~14 KB at seventeen bodies on the fullest tick, which is
        // 1.3% of the 1 MiB host frame. The assertion above is the contract;
        // this one is the tripwire that fires if the observation ever grows
        // by a factor of two.
        Assert.True(
            largest < 32 * 1024,
            $"full-roster observation frame grew to {largest} bytes");
    }

    /// <summary>
    /// Every body fabricates when it can and holds otherwise: the shortest
    /// script from an opening roster to a full one.
    /// </summary>
    private sealed class CrowdFactory : IGenericActorRuntimeFactory
    {
        public IGenericActorRuntime CreateRuntime() => new CrowdRuntime();

        private sealed class CrowdRuntime : IGenericActorRuntime
        {
            public void StartLife(GenericActorRuntimeStart start)
            {
            }

            public GenericActorRuntimeDecision ExecuteTick(
                GenericActorRuntimeObservation observation)
            {
                GenericActorRuntimeObservation.ObservedUnitSlot? ready =
                    observation.TeamUnits
                        .Where(slot => slot.State
                            is GenericActorRuntimeObservation.UnitSlotState
                                .Ready)
                        .OrderBy(slot => slot.UnitId)
                        .FirstOrDefault();
                bool mayFabricate = observation.ActionLegalities.Any(
                    legality => legality.ActionId == "fabricate"
                        && legality.Available);
                return ready is not null && mayFabricate
                    ? new GenericActorRuntimeDecision(
                        "fabricate",
                        PublicActionCodes.Fabricate,
                        [
                            new GenericActorRuntimeActionArgument
                                .UnitTargetArgument(
                                    new GenericActorRuntimeActionArgument
                                        .UnitTarget(
                                            ready.TeamId,
                                            ready.UnitId)),
                        ],
                        null)
                    : new GenericActorRuntimeDecision("wait", 0, [], null);
            }
        }
    }
}
