namespace BotArena.Engine.Tests;

/// <summary>
/// P0's ONE irreversible decision, pinned
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §4.5, §9, §10, §11, §12;
/// DECISIONS #191). Everything else in the build plan can be built late; a
/// reused field ID cannot be taken back, because RUNTIME-PROTOCOL.md's
/// versioning rule is explicit that reusing a field ID or changing its meaning
/// requires a new version.
/// <para>
/// These are golden-value tests in the same sense as
/// <c>RandomTests.GoldenValues_PinTheAlgorithm</c>: if one fails you changed an
/// allocation, which is a version bump rather than a test update.
/// </para>
/// </summary>
public sealed class GenericMindReservedIdentifierTests
{
    [Fact]
    public void TheMindObservationFrameFieldIdsAreReservedExactly()
    {
        Assert.Equal(
            1,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation
                .SchemaVersion);
        Assert.Equal(
            2,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.Tick);
        Assert.Equal(
            3,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation
                .MatchContractFingerprint);

        // The 10..18 block is delivered ONCE per participant per tick instead
        // of once per life. It is 71% of an observation by measured share, and
        // the reason the payload falls 6-8x.
        Assert.Equal(
            10,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.Allies);
        Assert.Equal(
            11,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.Enemies);
        Assert.Equal(
            12,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.VisibleTiles);
        Assert.Equal(
            13,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation
                .VisibleProjectiles);
        Assert.Equal(
            14,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation
                .VisibleEvents);
        Assert.Equal(
            15,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.HeardSounds);
        Assert.Equal(
            16,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.Scoreboard);
        Assert.Equal(
            17,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.Mode);
        Assert.Equal(
            18,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.Participants);

        Assert.Equal(
            20,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.Bodies);
        Assert.Equal(
            21,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation.Slots);
        Assert.Equal(
            30,
            BotArena.Sdk.GenericMindWireFieldIds.MindObservation
                .AlliedIntents);
    }

    [Fact]
    public void TheMindDecisionFrameFieldIdsAreReservedExactly()
    {
        Assert.Equal(
            1,
            BotArena.Sdk.GenericMindWireFieldIds.MindDecisions.SchemaVersion);
        Assert.Equal(
            2,
            BotArena.Sdk.GenericMindWireFieldIds.MindDecisions.Tick);
        Assert.Equal(
            10,
            BotArena.Sdk.GenericMindWireFieldIds.MindDecisions.Commands);
        Assert.Equal(
            20,
            BotArena.Sdk.GenericMindWireFieldIds.MindDecisions.Intents);

        Assert.Equal(
            1,
            BotArena.Sdk.GenericMindWireFieldIds.MindCommand.UnitId);
        Assert.Equal(
            2,
            BotArena.Sdk.GenericMindWireFieldIds.MindCommand.LifeId);
        Assert.Equal(
            3,
            BotArena.Sdk.GenericMindWireFieldIds.MindCommand.ActionId);
        Assert.Equal(
            4,
            BotArena.Sdk.GenericMindWireFieldIds.MindCommand.ActionCode);
        Assert.Equal(
            5,
            BotArena.Sdk.GenericMindWireFieldIds.MindCommand.Arguments);
        // ROLE TAGS (§12): the field ID is spent now even though SetRole
        // ships in P2.
        Assert.Equal(
            6,
            BotArena.Sdk.GenericMindWireFieldIds.MindCommand.RoleTag);
        Assert.Equal(
            7,
            BotArena.Sdk.GenericMindWireFieldIds.MindCommand.DebugMessage);
    }

    [Fact]
    public void TheBodyAndSlotFieldIdsAreReservedExactly()
    {
        // Fields 1..13 are the EXISTING shared body encoding, in the existing
        // order, so a mind body and a per-life self encode the same facts the
        // same way — which is what makes the null pin checkable field by
        // field and the P2 wrap adapter a projection rather than a
        // translation.
        Assert.Equal(
            1,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState.ActorId);
        Assert.Equal(
            8,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState.Energy);
        Assert.Equal(
            11,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState.ClassId);
        Assert.Equal(
            13,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState.CarriedScrap);

        // 14..19 are the facts a per-life bot was not entitled to.
        Assert.Equal(
            14,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState
                .PreviousPosition);
        Assert.Equal(
            15,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState.MovedLastTick);
        Assert.Equal(
            16,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState
                .LifeStartedTick);
        Assert.Equal(
            17,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState.Origin);
        Assert.Equal(
            18,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState.RoleTag);
        Assert.Equal(
            19,
            BotArena.Sdk.GenericMindWireFieldIds.MindBodyState
                .ActionLegalities);

        Assert.Equal(
            1,
            BotArena.Sdk.GenericMindWireFieldIds.MindSlotState.UnitId);
        Assert.Equal(
            2,
            BotArena.Sdk.GenericMindWireFieldIds.MindSlotState.State);
        // PER-SLOT CHASSIS (§9) and CANDIDATE CHASSIS (§10).
        Assert.Equal(
            3,
            BotArena.Sdk.GenericMindWireFieldIds.MindSlotState.ClassId);
        Assert.Equal(
            4,
            BotArena.Sdk.GenericMindWireFieldIds.MindSlotState
                .CandidateClassIds);
        Assert.Equal(
            5,
            BotArena.Sdk.GenericMindWireFieldIds.MindSlotState
                .SelectedClassId);
    }

    [Fact]
    public void TheInterMindIntentFieldIdsAreReservedExactly()
    {
        Assert.Equal(
            1,
            BotArena.Sdk.GenericMindWireFieldIds.DeclaredIntent.TagId);
        Assert.Equal(
            2,
            BotArena.Sdk.GenericMindWireFieldIds.DeclaredIntent.Value);

        Assert.Equal(
            1,
            BotArena.Sdk.GenericMindWireFieldIds.AlliedIntent.ParticipantId);
        Assert.Equal(
            2,
            BotArena.Sdk.GenericMindWireFieldIds.AlliedIntent.TagId);
        Assert.Equal(
            3,
            BotArena.Sdk.GenericMindWireFieldIds.AlliedIntent.Value);

        Assert.Equal(
            8,
            BotArena.Sdk.GenericMindContractVersions
                .MaxDeclaredIntentsPerTick);
        Assert.Equal(
            32,
            BotArena.Sdk.GenericMindContractVersions.MaxIntentTagUtf8Bytes);
        // The one-tick delay is the design, not a limitation: it preserves
        // the frozen-observation invariant and makes intents
        // replay-verifiable.
        Assert.Equal(
            1,
            BotArena.Sdk.GenericMindContractVersions
                .AlliedIntentDeliveryDelayTicks);
    }

    [Fact]
    public void TheProfileTupleIsExactAndCarriesTheMatchContractSchema()
    {
        Assert.Equal(
            "generic-mind-match-1",
            BotArena.Sdk.ActorContractProfile.MindV1.ProfileId);
        Assert.Equal(
            1,
            BotArena.Sdk.ActorContractProfile.MindV1.RuntimeContractVersion);
        Assert.Equal(
            1,
            BotArena.Sdk.ActorContractProfile.MindV1.MatchStartSchemaVersion);
        Assert.Equal(
            1,
            BotArena.Sdk.ActorContractProfile.MindV1.ObservationSchemaVersion);
        Assert.Equal(
            1,
            BotArena.Sdk.ActorContractProfile.MindV1.DecisionSchemaVersion);
        // CARRIED, and load-bearing: the null pin depends on the resolved
        // match-contract schema being unchanged. The mind plays the same game.
        Assert.Equal(
            BotArena.Sdk.ActorContractProfile.GenericV2
                .MatchContractSchemaVersion,
            BotArena.Sdk.ActorContractProfile.MindV1
                .MatchContractSchemaVersion);

        // The Engine mirror agrees field for field.
        Assert.Equal(
            BotArenaVersions.GenericMindContractProfileId,
            BotArena.Sdk.ActorContractProfile.MindV1.ProfileId);
        Assert.Equal(
            BotArenaVersions.GenericMindRuntimeContractVersion,
            BotArena.Sdk.ActorContractProfile.MindV1.RuntimeContractVersion);
        Assert.Equal(
            BotArenaVersions.GenericMindMatchStartSchemaVersion,
            BotArena.Sdk.ActorContractProfile.MindV1.MatchStartSchemaVersion);
        Assert.Equal(
            BotArenaVersions.GenericMindObservationSchemaVersion,
            BotArena.Sdk.ActorContractProfile.MindV1.ObservationSchemaVersion);
        Assert.Equal(
            BotArenaVersions.GenericMindDecisionSchemaVersion,
            BotArena.Sdk.ActorContractProfile.MindV1.DecisionSchemaVersion);
        Assert.Equal(
            BotArenaVersions.GenericMindMatchContractSchemaVersion,
            BotArena.Sdk.ActorContractProfile.MindV1
                .MatchContractSchemaVersion);

        // Framing carries; runtime CONFIGURATION mints 2.0 because the fuel
        // formula, the memory ceiling and the instance topology all change.
        Assert.Equal(
            BotArenaVersions.GenericActorRuntimeProtocolVersion,
            BotArenaVersions.GenericMindRuntimeProtocolVersion);
        Assert.Equal(
            "2.0",
            BotArenaVersions.GenericMindRuntimeConfigurationVersion);
        Assert.NotEqual(
            BotArenaVersions.GenericActorRuntimeConfigurationVersion,
            BotArenaVersions.GenericMindRuntimeConfigurationVersion);
        // Replay 3 carries; it grows a mindTurns alternative rather than a
        // new format.
        Assert.Equal(
            BotArenaVersions.GenericActorReplayFormatVersion,
            BotArenaVersions.GenericMindReplayFormatVersion);
    }

    [Fact]
    public void TheEngineAndSdkBudgetsAgree()
    {
        Assert.Equal(
            BotArena.Sdk.GenericMindContractVersions.BaseTickFuel,
            GenericMindTickBudget.BaseTickFuel);
        Assert.Equal(
            BotArena.Sdk.GenericMindContractVersions.PerBodyTickFuel,
            GenericMindTickBudget.PerBodyTickFuel);
        Assert.Equal(
            BotArena.Sdk.GenericMindContractVersions.StartupFuel,
            GenericMindTickBudget.StartupFuel);
        Assert.Equal(
            BotArena.Sdk.GenericMindContractVersions.LinearMemoryBytes,
            GenericMindTickBudget.LinearMemoryBytes);
        Assert.Equal(
            BotArena.Sdk.GenericMindContractVersions.TickFuel(9),
            GenericMindTickBudget.TickFuel(9));
        // Memory doubles because there is ONE instance where there were nine;
        // per-participant peak still falls 4.5x.
        Assert.Equal(
            128L * 1024 * 1024,
            GenericMindTickBudget.LinearMemoryBytes);
        Assert.Equal(16_384, GenericMindTickBudget.TableElements);
        Assert.Equal(30, GenericMindTickBudget.WallClockBackstopSeconds);
    }

    [Fact]
    public void RoleTagsAreCappedAtTwentyFourBytes()
    {
        // 24 rather than the 64-byte semantic-ID cap because this is a display
        // label sent per body per tick: 24 x 9 = 216 bytes worst case.
        Assert.Equal(
            24,
            BotArena.Sdk.GenericMindContractVersions.MaxRoleTagUtf8Bytes);
        Assert.True(
            BotArena.Sdk.GenericMindContractVersions.MaxRoleTagUtf8Bytes
                < 64);
    }

    [Fact]
    public void TheCandidateChassisShapeIsReservedAndNotShipped()
    {
        Assert.Equal(
            "classId",
            GenericMindContractReservations.UnitSlotClassIdProperty);
        Assert.Equal(
            "slotChassis",
            GenericMindContractReservations.SlotChassisProperty);
        Assert.Equal(
            "fixed",
            GenericMindContractReservations.SlotChassisFixedKind);
        Assert.Equal(
            "chosen-at-activation",
            GenericMindContractReservations.SlotChassisChosenAtActivationKind);
        Assert.Equal(
            "candidateClassIds",
            GenericMindContractReservations
                .SlotChassisCandidateClassIdsProperty);
        Assert.Equal(
            "selectionActionId",
            GenericMindContractReservations
                .SlotChassisSelectionActionIdProperty);

        // RESERVED, NOT SHIPPED: the ordinal is allocated so FOUNDRY is later
        // a numbers-and-switch change, and the enum member is deliberately
        // absent so nothing can encode it. This assertion is the reservation.
        Assert.Equal(
            6,
            GenericMindContractReservations
                .ReservedClassTargetParameterKindOrdinal);
        Assert.DoesNotContain(
            GenericMindContractReservations
                .ReservedClassTargetParameterKindOrdinal,
            Enum.GetValues<ActorActionParameterKind>()
                .Select(kind => (int)kind));
        Assert.Equal(
            "class-target",
            GenericMindContractReservations
                .ReservedClassTargetParameterKindCanonicalId);
    }

    [Fact]
    public void TheRegisteredCompositionSetIsFiveAndItsTokensAreShort()
    {
        // Five, not 6,561: free composition is combinatorially unreadable and
        // is a later LEVEL with its own evaluation policy (§9.5).
        Assert.Equal(
            5,
            GenericMindContractReservations.RegisteredCompositionTokens
                .Length);
        Assert.Equal(
            ["bulwark", "fabricator", "spearhead", "striker", "warden"],
            GenericMindContractReservations.RegisteredCompositionTokens
                .ToArray());
        // The three monos are the chassis IDs themselves, so their cells stay
        // byte-identical to today's class arms — the load-bearing property
        // that keeps the whole measured campaign comparable.
        Assert.Equal(
            ["spearhead", "warden"],
            GenericMindContractReservations.RegisteredMixedCompositionTokens
                .ToArray());
        Assert.All(
            GenericMindContractReservations.RegisteredCompositionTokens,
            token => Assert.True(
                token.Length
                    <= GenericMindContractReservations
                        .MaxCompositionTokenLength,
                $"'{token}' exceeds the topology profile ID budget."));
    }
}
