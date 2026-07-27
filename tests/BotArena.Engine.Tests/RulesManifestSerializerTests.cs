using System.Collections.Immutable;
using System.Text.Json;

namespace BotArena.Engine.Tests;

public class RulesManifestSerializerTests
{
    [Fact]
    public void CanonicalSerialization_HasExplicitStablePropertyAndCollectionOrder()
    {
        ArenaMap map = ArenaMap.Create(
            "canonical",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ],
            zone: [new Position(3, 1), new Position(1, 1)]);
        PublicMatchContractManifest manifest =
            PublicRulesManifestFactory.CreateMatchContract(GameRules.Current, map);

        string json = RulesManifestSerializer.ToCanonicalJson(manifest);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal(
            ["schemaVersion", "matchContractFingerprint", "rules", "map", "topology"],
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "schemaVersion", "rulesetId", "rulesFingerprint", "limits", "objective",
                "energy", "forms", "actions", "projectiles", "shotPrograms", "vision",
                "collisions", "tickResolution",
            ],
            root.GetProperty("rules").EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "schemaVersion", "mapId", "mapVersion", "mapFingerprint", "formatVersion",
                "width", "height", "tileRows", "spawns", "objectiveTiles",
            ],
            root.GetProperty("map").EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "teamCount", "participantCount", "unitSlotCount", "initialLifeCount",
                "teams", "participants", "unitSlots", "initialLives",
            ],
            root.GetProperty("topology")
                .EnumerateObject()
                .Select(property => property.Name));
        Assert.Equal(2, root.GetProperty("topology").GetProperty("teamCount").GetInt32());
        Assert.Equal(
            2,
            root.GetProperty("topology").GetProperty("participantCount").GetInt32());
        Assert.Equal(
            2,
            root.GetProperty("topology").GetProperty("unitSlotCount").GetInt32());
        Assert.Equal(
            Enum.GetValues<BotAction>().Select(action => (int)action),
            root.GetProperty("rules").GetProperty("actions")
                .EnumerateArray()
                .Select(action => action.GetProperty("code").GetInt32()));
        Assert.All(
            root.GetProperty("rules").GetProperty("actions").EnumerateArray(),
            action => Assert.Equal(
                ["id", "code", "kind", "parameterKinds", "enabled"],
                action.EnumerateObject().Select(property => property.Name)));
        Assert.Empty(
            root.GetProperty("rules").GetProperty("actions")[0]
                .GetProperty("parameterKinds")
                .EnumerateArray());
        Assert.Equal(
            ["shot-program"],
            root.GetProperty("rules").GetProperty("actions")[4]
                .GetProperty("parameterKinds")
                .EnumerateArray()
                .Select(kind => kind.GetString()));
        Assert.Equal(
            [
                "id", "maxHealth", "visionRange", "shootCooldownTicks",
                "omnidirectionalVision", "omnidirectionalShooting",
                "movementLayer", "objectiveWeight", "canMove", "canShoot",
                "allowsProgrammedShots", "allowedActionIds",
            ],
            root.GetProperty("rules").GetProperty("forms")[0]
                .EnumerateObject()
                .Select(property => property.Name));
        Assert.Equal(
            [[3, 1], [1, 1]],
            root.GetProperty("map").GetProperty("objectiveTiles")
                .EnumerateArray()
                .Select(tile => tile.EnumerateArray().Select(value => value.GetInt32()).ToArray()));
        Assert.Equal(
            [
                "enabled", "headingSectors", "bendStepOctants",
                "minInitialAimOctants", "maxInitialAimOctants",
                "aimOnlyProgram", "allowedCurvedBendDirections",
                "minBendAfterTiles", "maxBendAfterTiles",
                "minBendEveryTiles", "maxBendEveryTiles", "minBendCount",
                "maxBendCount", "launchTiles", "payloadOptional", "defaultProgram",
                "invalidPayloadResult", "unsupportedPayloadResult",
                "diagonalCornersMustBeClear",
            ],
            root.GetProperty("rules").GetProperty("shotPrograms")
                .EnumerateObject()
                .Select(property => property.Name));
    }

    [Fact]
    public void CanonicalSerialization_IsByteStableAcrossEquivalentInstances()
    {
        ArenaMap firstMap = ArenaMap.Create(
            "first-alias",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        ArenaMap secondMap = ArenaMap.Create(
            "second-alias",
            ["#####", "#...#", "#####"],
            [
                new Spawn(1, 1, Direction.East),
                new Spawn(3, 1, Direction.West),
            ]);
        PublicRulesManifest firstRules = PublicRulesManifestFactory.CreateRules(GameRules.Current);
        PublicRulesManifest secondRules = PublicRulesManifestFactory.CreateRules(GameRules.Current);

        Assert.Equal(
            RulesManifestSerializer.ToCanonicalJson(firstRules),
            RulesManifestSerializer.ToCanonicalJson(secondRules));
        Assert.Equal(
            PublicRulesManifestFactory.CreateMap(firstMap).MapFingerprint,
            PublicRulesManifestFactory.CreateMap(secondMap).MapFingerprint);
    }

    [Fact]
    public void ActionParameterKinds_SerializeEveryKindInCanonicalEnumOrder()
    {
        PublicRulesManifest manifest =
            PublicRulesManifestFactory.CreateRules(GameRules.Current);
        PublicActionDefinition shoot =
            manifest.Actions.Single(action => action.Id == PublicActionIds.Shoot);
        PublicRulesManifest allParameterKinds = manifest with
        {
            Actions = manifest.Actions
                .Select(action => action.Id == shoot.Id
                    ? action with
                    {
                        ParameterKinds = Enum
                            .GetValues<PublicActionParameterKind>()
                            .ToImmutableArray(),
                    }
                    : action)
                .ToImmutableArray(),
        };

        using JsonDocument document = JsonDocument.Parse(
            RulesManifestSerializer.ToCanonicalJson(allParameterKinds));

        Assert.Equal(
            ["shot-program", "direction", "unit-target", "form-target"],
            document.RootElement.GetProperty("actions")
                .EnumerateArray()
                .Single(action =>
                    action.GetProperty("id").GetString() == PublicActionIds.Shoot)
                .GetProperty("parameterKinds")
                .EnumerateArray()
                .Select(kind => kind.GetString()));
    }

    [Theory]
    [MemberData(nameof(InvalidActionParameterKinds))]
    public void ActionParameterKinds_RejectInvalidCanonicalCollections(
        ImmutableArray<PublicActionParameterKind> parameterKinds)
    {
        PublicRulesManifest manifest =
            PublicRulesManifestFactory.CreateRules(GameRules.Current);
        PublicRulesManifest invalid = manifest with
        {
            Actions = manifest.Actions
                .Select(action => action.Id == PublicActionIds.Shoot
                    ? action with { ParameterKinds = parameterKinds }
                    : action)
                .ToImmutableArray(),
        };

        Assert.ThrowsAny<ArgumentException>(
            () => RulesManifestSerializer.ToCanonicalJson(invalid));
    }

    [Fact]
    public void FrontlineSerialization_AddsTypedDefinitionAndMapGeometryOnlyWhenPresent()
    {
        GameRules rules = GameRules.V0_1 with
        {
            RulesVersion = "frontline-serializer-test",
            Frontline = new FrontlineRules(),
        };
        ArenaMap map = ArenaMap.FromJson(File.ReadAllText(
            Path.Combine(
                FindRepoRoot(),
                "maps",
                "experimental",
                "frontline-01.json")));

        using JsonDocument rulesDocument = JsonDocument.Parse(
            RulesManifestSerializer.ToCanonicalJson(
                PublicRulesManifestFactory.CreateRules(rules)));
        using JsonDocument mapDocument = JsonDocument.Parse(
            RulesManifestSerializer.ToCanonicalJson(
                PublicRulesManifestFactory.CreateMap(map)));
        JsonElement rulesRoot = rulesDocument.RootElement;
        JsonElement mapRoot = mapDocument.RootElement;

        Assert.Equal(
            [
                "schemaVersion", "rulesetId", "rulesFingerprint", "limits", "objective",
                "frontlineDefinition", "energy", "forms", "actions", "projectiles",
                "shotPrograms", "vision", "collisions", "tickResolution",
            ],
            rulesRoot.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                "teamCount", "participantsPerTeam", "frontlinePositionCount",
                "initialUnitsPerTeam", "maxUnitsPerTeam", "teamPerception",
                "capture", "victory", "lifecycle", "deployment",
                "fabrication", "anchor", "alliedCombat",
            ],
            rulesRoot.GetProperty("frontlineDefinition")
                .EnumerateObject()
                .Select(property => property.Name));
        JsonElement capture =
            rulesRoot.GetProperty("frontlineDefinition")
                .GetProperty("capture");
        Assert.Equal(
            [
                "threshold", "gainPerSoleTeamTick", "decayAmount",
                "decayIntervalTicks", "redeployPauseTicks", "pushesToBreach",
                "presence", "nonSolePresence", "counterCapture",
            ],
            capture.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            "binary-positive-weight-per-team-no-stacking",
            capture.GetProperty("presence").GetString());
        Assert.Equal(
            "decay-existing-claim",
            capture.GetProperty("nonSolePresence").GetString());
        Assert.Equal(
            "erode-to-neutral-before-claim",
            capture.GetProperty("counterCapture").GetString());
        JsonElement victory =
            rulesRoot.GetProperty("frontlineDefinition")
                .GetProperty("victory");
        Assert.Equal(
            [
                "initialPosition", "teamAdvances", "completionPrecedence",
                "timeoutResolution",
            ],
            victory.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            "centre-position-index",
            victory.GetProperty("initialPosition").GetString());
        Assert.Equal(
            [(0, 1), (1, -1)],
            victory.GetProperty("teamAdvances")
                .EnumerateArray()
                .Select(value => (
                    value.GetProperty("teamId").GetInt32(),
                    value.GetProperty("positionIndexDelta").GetInt32())));
        Assert.Equal(
            "base-breach-before-max-ticks",
            victory.GetProperty("completionPrecedence").GetString());
        Assert.Equal(
            "signed-position-threshold-plus-claim-zero-draw-no-tiebreakers",
            victory.GetProperty("timeoutResolution").GetString());
        JsonElement deployment =
            rulesRoot.GetProperty("frontlineDefinition")
                .GetProperty("deployment");
        Assert.Equal(
            [
                "primeDefaultFormId", "childDefaultFormId",
                "destructionTransitionClock", "primeReturn", "childReturn",
                "newLife", "primeSpawnReservation", "protectedPad",
            ],
            deployment.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            "prime-mobile",
            deployment.GetProperty("primeDefaultFormId").GetString());
        Assert.Equal(
            "child-mobile",
            deployment.GetProperty("childDefaultFormId").GetString());
        Assert.Equal(
            "tick-start-at-destroyed-tick-plus-one-plus-delay",
            deployment.GetProperty("destructionTransitionClock").GetString());
        Assert.Equal(
            "automatic-at-authored-prime-spawn",
            deployment.GetProperty("primeReturn").GetString());
        Assert.Equal(
            "ready-then-explicit-fabrication",
            deployment.GetProperty("childReturn").GetString());
        Assert.Equal(
            "fresh-runtime-form-defaults-home-facing-can-act-on-creation-tick",
            deployment.GetProperty("newLife").GetString());
        Assert.Equal(
            "permanent-against-own-children",
            deployment.GetProperty("primeSpawnReservation").GetString());
        Assert.Equal(
            "enemy-ground-entry-blocked-no-damage-immunity-no-projectile-blocking",
            deployment.GetProperty("protectedPad").GetString());
        Assert.Equal(
            ["child-mobile", "prime-mobile", "turret"],
            rulesRoot.GetProperty("forms")
                .EnumerateArray()
                .Select(form => form.GetProperty("id").GetString()));
        Assert.Equal(
            [
                "id", "maxHealth", "visionRange", "shootCooldownTicks",
                "omnidirectionalVision", "omnidirectionalShooting",
                "movementLayer", "objectiveWeight", "canMove", "canShoot",
                "allowsProgrammedShots", "allowedActionIds",
            ],
            rulesRoot.GetProperty("forms")[0]
                .EnumerateObject()
                .Select(property => property.Name));
        JsonElement fabrication =
            rulesRoot.GetProperty("frontlineDefinition")
                .GetProperty("fabrication");
        Assert.Equal(
            [
                "enabled", "actionId", "fabricatorUnitId", "fabricatorFormId",
                "targetPolicy", "activationRegion", "consumesTick",
                "spawnDelayTicks", "capacityEvaluation", "spawnRegion",
                "spawnSelection",
                "spawnFacing", "unavailableSpawnResult",
                "requiresExplicitRefabricationAfterRebuild",
            ],
            fabrication.EnumerateObject().Select(property => property.Name));
        Assert.True(fabrication.GetProperty("enabled").GetBoolean());
        Assert.Equal(
            "fabricate",
            fabrication.GetProperty("actionId").GetString());
        Assert.Equal(0, fabrication.GetProperty("fabricatorUnitId").GetInt32());
        Assert.Equal(
            "prime-mobile",
            fabrication.GetProperty("fabricatorFormId").GetString());
        Assert.Equal(
            "own-ready-child-slot",
            fabrication.GetProperty("targetPolicy").GetString());
        Assert.Equal(
            "own-protected-spawn-pad",
            fabrication.GetProperty("activationRegion").GetString());
        Assert.True(fabrication.GetProperty("consumesTick").GetBoolean());
        Assert.Equal(1, fabrication.GetProperty("spawnDelayTicks").GetInt32());
        Assert.Equal(
            "post-movement-during-queue-fabrications",
            fabrication.GetProperty("capacityEvaluation").GetString());
        Assert.Equal(
            "own-protected-spawn-pad-excluding-prime-spawn",
            fabrication.GetProperty("spawnRegion").GetString());
        Assert.Equal(
            "first-unoccupied-unreserved-canonical-y-x",
            fabrication.GetProperty("spawnSelection").GetString());
        Assert.Equal(
            "own-prime-spawn-facing",
            fabrication.GetProperty("spawnFacing").GetString());
        Assert.Equal(
            "blocked",
            fabrication.GetProperty("unavailableSpawnResult").GetString());
        Assert.True(
            fabrication
                .GetProperty("requiresExplicitRefabricationAfterRebuild")
                .GetBoolean());
        Assert.Contains(
            rulesRoot.GetProperty("actions").EnumerateArray(),
            action =>
                action.GetProperty("id").GetString() == "fabricate"
                && action.GetProperty("code").GetInt32()
                    == PublicActionCodes.Fabricate
                && action.GetProperty("kind").GetString() == "fabrication"
                && action.GetProperty("parameterKinds")[0].GetString()
                    == "unit-target");
        Assert.DoesNotContain(
            rulesRoot.GetProperty("actions").EnumerateArray(),
            action => action.GetProperty("id").GetString() == "anchor");
        JsonElement alliedCombat =
            rulesRoot.GetProperty("frontlineDefinition")
                .GetProperty("alliedCombat");
        Assert.Equal(
            [
                "friendlyFireEnabled", "alliedProjectilesBlock",
                "projectileAttribution",
            ],
            alliedCombat.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            "exact-firing-life-persists-credits-stable-unit-by-actual-health-removed",
            alliedCombat.GetProperty("projectileAttribution").GetString());

        Assert.Equal(
            [
                "schemaVersion", "mapId", "mapVersion", "mapFingerprint", "formatVersion",
                "width", "height", "tileRows", "spawns", "objectiveTiles", "frontline",
            ],
            mapRoot.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["positions", "teamHomes", "anchorForbiddenTiles"],
            mapRoot.GetProperty("frontline")
                .EnumerateObject()
                .Select(property => property.Name));
        Assert.Empty(mapRoot.GetProperty("objectiveTiles").EnumerateArray());
    }

    [Fact]
    public void FrontlineSemanticEnums_RejectUnknownValues()
    {
        PublicRulesManifest manifest = PublicRulesManifestFactory.CreateRules(
            GameRules.V0_1 with
            {
                RulesVersion = "frontline-invalid-semantics-test",
                Frontline = new FrontlineRules(),
            });
        PublicFrontlineDefinition frontline =
            Assert.IsType<PublicFrontlineDefinition>(manifest.Frontline);
        const int unknown = int.MaxValue;
        PublicRulesManifest[] invalid =
        [
            WithFrontline(manifest, frontline with
            {
                Capture = frontline.Capture with
                {
                    Presence =
                        (PublicFrontlineCapturePresencePolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Capture = frontline.Capture with
                {
                    NonSolePresence =
                        (PublicFrontlineNonSolePresencePolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Capture = frontline.Capture with
                {
                    CounterCapture =
                        (PublicFrontlineCounterCapturePolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Victory = frontline.Victory with
                {
                    InitialPosition =
                        (PublicFrontlineInitialPositionPolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Victory = frontline.Victory with
                {
                    CompletionPrecedence =
                        (PublicFrontlineCompletionPrecedence)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Victory = frontline.Victory with
                {
                    TimeoutResolution =
                        (PublicFrontlineTimeoutResolution)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Deployment = frontline.Deployment with
                {
                    DestructionTransitionClock =
                        (PublicFrontlineDestructionTransitionClock)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Deployment = frontline.Deployment with
                {
                    PrimeReturn =
                        (PublicFrontlinePrimeReturnPolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Deployment = frontline.Deployment with
                {
                    ChildReturn =
                        (PublicFrontlineChildReturnPolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Deployment = frontline.Deployment with
                {
                    NewLife =
                        (PublicFrontlineNewLifePolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Deployment = frontline.Deployment with
                {
                    PrimeSpawnReservation =
                        (PublicFrontlinePrimeSpawnReservationPolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Deployment = frontline.Deployment with
                {
                    ProtectedPad =
                        (PublicFrontlineProtectedPadPolicy)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                Fabrication = frontline.Fabrication with
                {
                    CapacityEvaluation =
                        (PublicFrontlineFabricationCapacityEvaluation)unknown,
                },
            }),
            WithFrontline(manifest, frontline with
            {
                AlliedCombat = frontline.AlliedCombat with
                {
                    ProjectileAttribution =
                        (PublicFrontlineProjectileAttributionPolicy)unknown,
                },
            }),
        ];

        Assert.All(
            invalid,
            value => Assert.Throws<ArgumentOutOfRangeException>(() =>
                RulesManifestSerializer.ToCanonicalJson(value)));
    }

    [Fact]
    public void FrontlineTeamAdvances_AreCanonicalAndRequireUniqueTeamIds()
    {
        PublicRulesManifest manifest = PublicRulesManifestFactory.CreateRules(
            GameRules.V0_1 with
            {
                RulesVersion = "frontline-team-advance-test",
                Frontline = new FrontlineRules(),
            });
        PublicFrontlineDefinition frontline =
            Assert.IsType<PublicFrontlineDefinition>(manifest.Frontline);
        PublicRulesManifest reversed = WithFrontline(
            manifest,
            frontline with
            {
                Victory = frontline.Victory with
                {
                    TeamAdvances =
                        frontline.Victory.TeamAdvances
                            .Reverse()
                            .ToImmutableArray(),
                },
            });
        Assert.Equal(
            RulesManifestSerializer.ToCanonicalJson(manifest),
            RulesManifestSerializer.ToCanonicalJson(reversed));

        PublicRulesManifest duplicate = WithFrontline(
            manifest,
            frontline with
            {
                Victory = frontline.Victory with
                {
                    TeamAdvances =
                    [
                        new PublicFrontlineTeamAdvance(0, 1),
                        new PublicFrontlineTeamAdvance(0, -1),
                    ],
                },
            });
        Assert.Throws<ArgumentException>(() =>
            RulesManifestSerializer.ToCanonicalJson(duplicate));

        PublicRulesManifest invalidDelta = WithFrontline(
            manifest,
            frontline with
            {
                Victory = frontline.Victory with
                {
                    TeamAdvances =
                    [
                        new PublicFrontlineTeamAdvance(0, 1),
                        new PublicFrontlineTeamAdvance(1, 1),
                    ],
                },
            });
        Assert.Throws<ArgumentException>(() =>
            RulesManifestSerializer.ToCanonicalJson(invalidDelta));
    }

    [Fact]
    public void FrontlineDeployment_DefaultFormsMustReferenceCatalog()
    {
        PublicRulesManifest manifest = PublicRulesManifestFactory.CreateRules(
            GameRules.V0_1 with
            {
                RulesVersion = "frontline-default-form-test",
                Frontline = new FrontlineRules(),
            });
        PublicFrontlineDefinition frontline =
            Assert.IsType<PublicFrontlineDefinition>(manifest.Frontline);
        PublicRulesManifest invalid = WithFrontline(
            manifest,
            frontline with
            {
                Deployment = frontline.Deployment with
                {
                    ChildDefaultFormId = "missing-form",
                },
            });

        Assert.Throws<ArgumentException>(() =>
            RulesManifestSerializer.ToCanonicalJson(invalid));
    }

    private static PublicRulesManifest WithFrontline(
        PublicRulesManifest manifest,
        PublicFrontlineDefinition frontline) =>
        manifest with { Frontline = frontline };

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "BotArena.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new InvalidOperationException(
                "BotArena.sln not found above the test directory.");
    }

    public static TheoryData<ImmutableArray<PublicActionParameterKind>>
        InvalidActionParameterKinds
    {
        get
        {
            var cases =
                new TheoryData<ImmutableArray<PublicActionParameterKind>>();
            cases.Add(default);
            cases.Add(
            [
                PublicActionParameterKind.Direction,
                PublicActionParameterKind.Direction,
            ]);
            cases.Add(
            [
                PublicActionParameterKind.FormTarget,
                PublicActionParameterKind.UnitTarget,
            ]);
            cases.Add([(PublicActionParameterKind)int.MaxValue]);
            return cases;
        }
    }
}
