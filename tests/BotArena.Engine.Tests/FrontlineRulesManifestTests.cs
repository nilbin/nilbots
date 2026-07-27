namespace BotArena.Engine.Tests;

public class FrontlineRulesManifestTests
{
    [Fact]
    public void FrontlineRules_ProjectCompleteDefinitionWithoutInventingActions()
    {
        FrontlineRules source = new();

        PublicRulesManifest manifest =
            PublicRulesManifestFactory.CreateRules(CreateRules(source));

        Assert.Equal(PublicObjectiveMode.Frontline, manifest.Objective.Mode);
        Assert.Equal(
            [PublicScoreMetric.Objective],
            manifest.Objective.MaxTickTiebreakers.ToArray());
        Assert.Equal(
            source.PrimeForm.ShootCooldownTicks,
            manifest.Projectiles.ShootCooldownTicks);
        Assert.Equal(
            [
                PublicTickResolutionPhase.ApplyTickStartLifecycle,
                PublicTickResolutionPhase.FreezeObservations,
                PublicTickResolutionPhase.CollectJointDecisions,
                PublicTickResolutionPhase.ValidateActions,
                PublicTickResolutionPhase.Rotate,
                PublicTickResolutionPhase.Move,
                PublicTickResolutionPhase.AdvanceExistingProjectiles,
                PublicTickResolutionPhase.LaunchShotsAndApplyDamage,
                PublicTickResolutionPhase.QueueDestroyedLives,
                PublicTickResolutionPhase.UpdateCooldownsAndEnergy,
                PublicTickResolutionPhase.UpdateObjective,
                PublicTickResolutionPhase.ResolveMatchCompletion,
            ],
            manifest.TickResolution.Phases.ToArray());
        Assert.Equal(source.TeamCount, manifest.Limits.TeamCount);
        Assert.Equal(
            source.TeamCount * source.ParticipantsPerTeam,
            manifest.Limits.ParticipantCount);
        Assert.Equal(
            source.TeamCount * source.MaxUnitsPerTeam,
            manifest.Limits.UnitSlotCount);
        Assert.Equal(source.InitialUnitsPerTeam, manifest.Limits.InitialUnitsPerTeam);
        Assert.Equal(source.MaxUnitsPerTeam, manifest.Limits.MaxUnitsPerTeam);
        Assert.False(manifest.Limits.DestructionEndsMatch);
        Assert.True(manifest.Limits.RespawnsEnabled);
        Assert.Equal(0, manifest.Limits.FaultLimit);
        Assert.Null(manifest.ShotPrograms.InvalidPayloadResult);
        Assert.True(manifest.Collisions.ProjectilesStopOnFirstNonOwnerUnit);

        PublicFrontlineDefinition definition = Assert.IsType<PublicFrontlineDefinition>(
            manifest.Frontline);
        Assert.Equal(source.TeamCount, definition.TeamCount);
        Assert.Equal(source.ParticipantsPerTeam, definition.ParticipantsPerTeam);
        Assert.Equal(source.FrontlinePositionCount, definition.FrontlinePositionCount);
        Assert.Equal(source.InitialUnitsPerTeam, definition.InitialUnitsPerTeam);
        Assert.Equal(source.MaxUnitsPerTeam, definition.MaxUnitsPerTeam);
        Assert.Equal(TeamPerceptionMode.ImmediateUnion, definition.TeamPerception);
        Assert.Equal(
            new PublicFrontlineCaptureDefinition(
                source.CaptureThreshold,
                source.CaptureGainPerSoleTeamTick,
                source.CaptureDecayAmount,
                source.CaptureDecayIntervalTicks,
                source.RedeployPauseTicks,
                source.PushesToBreach),
            definition.Capture);
        Assert.Equal(source.PrimeRespawnTicks, definition.Lifecycle.PrimeRespawnTicks);
        Assert.Equal(source.ChildRebuildTicks, definition.Lifecycle.ChildRebuildTicks);
        Assert.Equal(
            source.FabricationUnlockTicks.ToArray(),
            definition.Lifecycle.FabricationUnlockTicks.ToArray());
        Assert.Equal(
            ["child-mobile", "prime-mobile", "turret"],
            manifest.Forms.Select(form => form.Id).ToArray());
        AssertForm(
            source.ChildForm,
            manifest.Forms.Single(form => form.Id == source.ChildForm.FormId));
        AssertForm(
            source.PrimeForm,
            manifest.Forms.Single(form => form.Id == source.PrimeForm.FormId));
        AssertForm(
            source.TurretForm,
            manifest.Forms.Single(form => form.Id == source.TurretForm.FormId));
        Assert.Equal(
            new PublicFrontlineAnchorDefinition(
                source.AnchorWindupTicks,
                source.AnchorHealthGain,
                source.AnchorIrreversibleForLife),
            definition.Anchor);
        Assert.Equal(
            new PublicFrontlineAlliedCombatDefinition(
                source.FriendlyFireEnabled,
                source.AlliedProjectilesBlock),
            definition.AlliedCombat);

        Assert.Equal(3, manifest.Forms.Length);
        Assert.All(
            manifest.Forms,
            form => Assert.Equal(PublicMovementLayer.Ground, form.MovementLayer));
        Assert.Equal(
            ["shoot", "turn-left", "turn-right", "wait"],
            manifest.Forms.Single(form => form.Id == source.TurretForm.FormId)
                .AllowedActionIds.ToArray());
        Assert.DoesNotContain(
            manifest.Actions,
            action => action.Id is "fabricate" or "anchor");
    }

    [Fact]
    public void EnabledFrontlineShotPrograms_RejectInvalidPayloadAtHostBoundary()
    {
        GameRules rules = CreateRules(new FrontlineRules()) with
        {
            AllowProgrammedShots = true,
            ProjectileTicksPerTile = 1,
        };

        PublicRulesManifest manifest =
            PublicRulesManifestFactory.CreateRules(rules);

        Assert.Equal(
            PublicActionRejectionResult.Rejected,
            manifest.ShotPrograms.InvalidPayloadResult);
    }

    [Fact]
    public void FormCatalog_DerivesAllowedActionsFromGlobalAndFormCapabilities()
    {
        FrontlineRules defaults = new();
        FrontlineRules source = defaults with
        {
            ChildForm = defaults.ChildForm with { CanShoot = false },
        };
        GameRules rules = CreateRules(source) with
        {
            AllowStrafe = true,
            AllowProgrammedShots = true,
            ProjectileTicksPerTile = 1,
        };

        PublicRulesManifest manifest = PublicRulesManifestFactory.CreateRules(rules);

        Assert.Equal(
            [
                "move-forward", "shoot", "strafe-left", "strafe-right",
                "turn-left", "turn-right", "wait",
            ],
            manifest.Forms.Single(form => form.Id == source.PrimeForm.FormId)
                .AllowedActionIds.ToArray());
        Assert.Equal(
            [
                "move-forward", "strafe-left", "strafe-right",
                "turn-left", "turn-right", "wait",
            ],
            manifest.Forms.Single(form => form.Id == source.ChildForm.FormId)
                .AllowedActionIds.ToArray());
        Assert.Equal(
            ["shoot", "turn-left", "turn-right", "wait"],
            manifest.Forms.Single(form => form.Id == source.TurretForm.FormId)
                .AllowedActionIds.ToArray());

        string[] definedActionIds = manifest.Actions
            .Select(action => action.Id)
            .ToArray();
        Assert.All(
            manifest.Forms.SelectMany(form => form.AllowedActionIds),
            actionId => Assert.Contains(actionId, definedActionIds));
    }

    [Fact]
    public void MalformedNullFrontlineForm_FailsDeliberately()
    {
        GameRules malformed = CreateRules(new FrontlineRules
        {
            PrimeForm = null!,
        });

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            PublicRulesManifestFactory.CreateRules(malformed));

        Assert.Contains("form definitions are required", exception.Message);
    }

    public static IEnumerable<object[]> FrontlineGameplayMutations()
    {
        yield return Case(
            nameof(FrontlineRules.TeamCount),
            rules => rules with { TeamCount = rules.TeamCount + 1 });
        yield return Case(
            nameof(FrontlineRules.ParticipantsPerTeam),
            rules => rules with
            {
                ParticipantsPerTeam = rules.ParticipantsPerTeam + 1,
            });
        yield return Case(
            nameof(FrontlineRules.FrontlinePositionCount),
            rules => rules with
            {
                FrontlinePositionCount = rules.FrontlinePositionCount + 2,
            });
        yield return Case(
            nameof(FrontlineRules.InitialUnitsPerTeam),
            rules => rules with
            {
                InitialUnitsPerTeam = rules.InitialUnitsPerTeam + 1,
            });
        yield return Case(
            nameof(FrontlineRules.MaxUnitsPerTeam),
            rules => rules with
            {
                MaxUnitsPerTeam = rules.MaxUnitsPerTeam + 1,
            });
        yield return Case(
            nameof(FrontlineRules.CaptureThreshold),
            rules => rules with { CaptureThreshold = rules.CaptureThreshold + 1 });
        yield return Case(
            nameof(FrontlineRules.CaptureGainPerSoleTeamTick),
            rules => rules with
            {
                CaptureGainPerSoleTeamTick =
                    rules.CaptureGainPerSoleTeamTick + 1,
            });
        yield return Case(
            nameof(FrontlineRules.CaptureDecayAmount),
            rules => rules with
            {
                CaptureDecayAmount = rules.CaptureDecayAmount + 1,
            });
        yield return Case(
            nameof(FrontlineRules.CaptureDecayIntervalTicks),
            rules => rules with
            {
                CaptureDecayIntervalTicks =
                    rules.CaptureDecayIntervalTicks + 1,
            });
        yield return Case(
            nameof(FrontlineRules.RedeployPauseTicks),
            rules => rules with
            {
                RedeployPauseTicks = rules.RedeployPauseTicks + 1,
            });
        yield return Case(
            nameof(FrontlineRules.PushesToBreach),
            rules => rules with { PushesToBreach = rules.PushesToBreach + 1 });
        yield return Case(
            nameof(FrontlineRules.PrimeRespawnTicks),
            rules => rules with
            {
                PrimeRespawnTicks = rules.PrimeRespawnTicks + 1,
            });
        yield return Case(
            nameof(FrontlineRules.ChildRebuildTicks),
            rules => rules with
            {
                ChildRebuildTicks = rules.ChildRebuildTicks + 1,
            });
        yield return Case(
            nameof(FrontlineRules.FabricationUnlockTicks),
            rules => rules with { FabricationUnlockTicks = [121, 260] });

        foreach (object[] mutation in FormCases(
                     nameof(FrontlineRules.PrimeForm),
                     (rules, form) => rules with { PrimeForm = form }))
        {
            yield return mutation;
        }
        foreach (object[] mutation in FormCases(
                     nameof(FrontlineRules.ChildForm),
                     (rules, form) => rules with { ChildForm = form }))
        {
            yield return mutation;
        }
        foreach (object[] mutation in FormCases(
                     nameof(FrontlineRules.TurretForm),
                     (rules, form) => rules with { TurretForm = form }))
        {
            yield return mutation;
        }

        yield return Case(
            nameof(FrontlineRules.AnchorWindupTicks),
            rules => rules with
            {
                AnchorWindupTicks = rules.AnchorWindupTicks + 1,
            });
        yield return Case(
            nameof(FrontlineRules.AnchorHealthGain),
            rules => rules with { AnchorHealthGain = rules.AnchorHealthGain + 1 });
        yield return Case(
            nameof(FrontlineRules.AnchorIrreversibleForLife),
            rules => rules with
            {
                AnchorIrreversibleForLife = !rules.AnchorIrreversibleForLife,
            });
        yield return Case(
            nameof(FrontlineRules.FriendlyFireEnabled),
            rules => rules with
            {
                FriendlyFireEnabled = !rules.FriendlyFireEnabled,
            });
        yield return Case(
            nameof(FrontlineRules.AlliedProjectilesBlock),
            rules => rules with
            {
                AlliedProjectilesBlock = !rules.AlliedProjectilesBlock,
            });
    }

    [Theory]
    [MemberData(nameof(FrontlineGameplayMutations))]
    public void EveryFrontlineGameplayMutation_ChangesRulesFingerprint(
        string propertyPath,
        Func<FrontlineRules, FrontlineRules> mutate)
    {
        FrontlineRules baseline = new();

        string before = PublicRulesManifestFactory
            .CreateRules(CreateRules(baseline))
            .RulesFingerprint;
        string after = PublicRulesManifestFactory
            .CreateRules(CreateRules(mutate(baseline)))
            .RulesFingerprint;

        Assert.NotEqual(before, after);
        Assert.False(string.IsNullOrWhiteSpace(propertyPath));
    }

    [Fact]
    public void MutationCases_CoverEveryFrontlineAndUnitFormProperty()
    {
        string[] formPropertyNames = typeof(UnitFormRules)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        string[] formOwnerNames =
        [
            nameof(FrontlineRules.PrimeForm),
            nameof(FrontlineRules.ChildForm),
            nameof(FrontlineRules.TurretForm),
        ];
        string[] expected = typeof(FrontlineRules)
            .GetProperties()
            .Select(property => property.Name)
            .Except(formOwnerNames, StringComparer.Ordinal)
            .Concat(formOwnerNames.SelectMany(owner =>
                formPropertyNames.Select(property => $"{owner}.{property}")))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actual = FrontlineGameplayMutations()
            .Select(row => Assert.IsType<string>(row[0]))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static IEnumerable<object[]> FormCases(
        string owner,
        Func<FrontlineRules, UnitFormRules, FrontlineRules> replace)
    {
        yield return FormCase(
            owner,
            nameof(UnitFormRules.FormId),
            replace,
            form => form with { FormId = $"{form.FormId}-changed" });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.MaxHealth),
            replace,
            form => form with { MaxHealth = form.MaxHealth + 1 });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.VisionRange),
            replace,
            form => form with { VisionRange = form.VisionRange + 1 });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.ShootCooldownTicks),
            replace,
            form => form with
            {
                ShootCooldownTicks = form.ShootCooldownTicks + 1,
            });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.OmnidirectionalVision),
            replace,
            form => form with
            {
                OmnidirectionalVision = !form.OmnidirectionalVision,
            });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.OmnidirectionalShooting),
            replace,
            form => form with
            {
                OmnidirectionalShooting = !form.OmnidirectionalShooting,
            });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.ObjectiveWeight),
            replace,
            form => form with { ObjectiveWeight = form.ObjectiveWeight + 1 });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.CanMove),
            replace,
            form => form with { CanMove = !form.CanMove });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.CanShoot),
            replace,
            form => form with { CanShoot = !form.CanShoot });
        yield return FormCase(
            owner,
            nameof(UnitFormRules.AllowsProgrammedShots),
            replace,
            form => form with
            {
                AllowsProgrammedShots = !form.AllowsProgrammedShots,
            });
    }

    private static object[] FormCase(
        string owner,
        string property,
        Func<FrontlineRules, UnitFormRules, FrontlineRules> replace,
        Func<UnitFormRules, UnitFormRules> mutate) =>
    [
        $"{owner}.{property}",
        (Func<FrontlineRules, FrontlineRules>)(rules =>
        {
            UnitFormRules form = owner switch
            {
                nameof(FrontlineRules.PrimeForm) => rules.PrimeForm,
                nameof(FrontlineRules.ChildForm) => rules.ChildForm,
                nameof(FrontlineRules.TurretForm) => rules.TurretForm,
                _ => throw new ArgumentOutOfRangeException(nameof(owner)),
            };
            return replace(rules, mutate(form));
        }),
    ];

    private static object[] Case(
        string propertyPath,
        Func<FrontlineRules, FrontlineRules> mutate) =>
        [propertyPath, mutate];

    private static GameRules CreateRules(FrontlineRules frontline) =>
        GameRules.V0_1 with
        {
            RulesVersion = "frontline-manifest-test",
            Frontline = frontline,
        };

    private static void AssertForm(
        UnitFormRules expected,
        PublicFormDefinition actual)
    {
        Assert.Equal(expected.FormId, actual.Id);
        Assert.Equal(expected.MaxHealth, actual.MaxHealth);
        Assert.Equal(expected.VisionRange, actual.VisionRange);
        Assert.Equal(expected.ShootCooldownTicks, actual.ShootCooldownTicks);
        Assert.Equal(expected.OmnidirectionalVision, actual.OmnidirectionalVision);
        Assert.Equal(expected.OmnidirectionalShooting, actual.OmnidirectionalShooting);
        Assert.Equal(PublicMovementLayer.Ground, actual.MovementLayer);
        Assert.Equal(expected.ObjectiveWeight, actual.ObjectiveWeight);
        Assert.Equal(expected.CanMove, actual.CanMove);
        Assert.Equal(expected.CanShoot, actual.CanShoot);
        Assert.Equal(expected.AllowsProgrammedShots, actual.AllowsProgrammedShots);
        Assert.Equal(AllowedActionIds(expected).ToArray(), actual.AllowedActionIds.ToArray());
    }

    private static System.Collections.Immutable.ImmutableArray<string> AllowedActionIds(
        UnitFormRules form)
    {
        var ids = new List<string>
        {
            PublicActionIds.Wait,
            PublicActionIds.TurnLeft,
            PublicActionIds.TurnRight,
        };
        if (form.CanMove)
            ids.Add(PublicActionIds.MoveForward);
        if (form.CanShoot)
            ids.Add(PublicActionIds.Shoot);
        return [.. ids.Order(StringComparer.Ordinal)];
    }
}
