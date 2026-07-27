using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace BotArena.Engine.Tests;

public sealed class ActorContractManifestSerializerTests
{
    [Fact]
    public void EveryReachableContractEnumValueHasAnExplicitCanonicalId()
    {
        Type[] enumTypes = DiscoverReachableContractEnumTypes();
        var errors = new List<string>();

        foreach (Type enumType in enumTypes)
        {
            foreach (object rawValue in Enum.GetValues(enumType))
            {
                var value = (Enum)rawValue;
                try
                {
                    string id = ActorContractCanonicalIds.Id(value);
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        errors.Add(
                            $"{enumType.FullName} value {rawValue} maps to a blank ID.");
                    }
                    else if (!IsCanonicalKebabId(id))
                    {
                        errors.Add(
                            $"{enumType.FullName} value {rawValue} maps to non-canonical ID '{id}'.");
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    errors.Add(
                        $"{enumType.FullName} value {rawValue} has no canonical ID.");
                }
            }
        }

        Assert.NotEmpty(enumTypes);
        Assert.True(
            errors.Count == 0,
            "Generation-3 contract enum mapping gaps: "
            + string.Join("; ", errors));
    }

    [Fact]
    public void SeparatesRulesMapFormatTopologyAndExactMatchFingerprints()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorMapDefinition map = CreateMap();
        ActorResolvedMatchDefinition headToHead = Resolve(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            CreateTopology([[10], [20]]),
            ["west", "east"]);
        ActorResolvedMatchDefinition freeForAll = Resolve(
            rules,
            map,
            new FreeForAllMatchFormatDefinition(4),
            CreateTopology([[10], [20], [30], [40]]),
            ["west", "east", "north", "south"]);
        ActorResolvedMatchDefinition twoByTwo = Resolve(
            rules,
            map,
            new TeamsMatchFormatDefinition(2, 2),
            CreateTopology([[10, 11], [20, 21]]),
            ["west", "north", "east", "south"]);

        Assert.Equal(
            ActorContractFingerprint.ComputeRules(headToHead.Rules),
            ActorContractFingerprint.ComputeRules(freeForAll.Rules));
        Assert.Equal(
            ActorContractFingerprint.ComputeRules(headToHead.Rules),
            ActorContractFingerprint.ComputeRules(twoByTwo.Rules));
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(headToHead.Map),
            ActorContractFingerprint.ComputeMap(freeForAll.Map));
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(headToHead.Map),
            ActorContractFingerprint.ComputeMap(twoByTwo.Map));
        Assert.Equal(
            3,
            new[]
            {
                ActorContractFingerprint.ComputeFormat(headToHead.Format),
                ActorContractFingerprint.ComputeFormat(freeForAll.Format),
                ActorContractFingerprint.ComputeFormat(twoByTwo.Format),
            }.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            3,
            new[]
            {
                ActorContractFingerprint.ComputeTopology(
                    headToHead.Topology),
                ActorContractFingerprint.ComputeTopology(
                    freeForAll.Topology),
                ActorContractFingerprint.ComputeTopology(
                    twoByTwo.Topology),
            }.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            3,
            new[]
            {
                ActorContractFingerprint.ComputeMatch(headToHead),
                ActorContractFingerprint.ComputeMatch(freeForAll),
                ActorContractFingerprint.ComputeMatch(twoByTwo),
            }.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ReversedSetInputsProduceIdenticalCanonicalBytes()
    {
        ActorRulesDefinition orderedRules = CreateRules();
        ActorRulesDefinition reversedRules = ReverseRules(orderedRules);
        ActorMapDefinition orderedMap = CreateMap();
        ActorMapDefinition reversedMap = new(
            orderedMap.Id,
            orderedMap.Version,
            orderedMap.TileRows,
            orderedMap.SpawnAnchors.Reverse().ToImmutableArray(),
            orderedMap.Regions.Reverse().ToImmutableArray(),
            orderedMap.TileTags.Reverse().ToImmutableArray());
        PublicMatchTopology orderedTopology =
            CreateTopology([[10], [20], [30], [40]]);
        PublicMatchTopology reversedTopology = orderedTopology with
        {
            Teams = orderedTopology.Teams.Reverse().ToImmutableArray(),
            Participants = orderedTopology.Participants
                .Reverse()
                .ToImmutableArray(),
            UnitSlots = orderedTopology.UnitSlots.Reverse().ToImmutableArray(),
            InitialLives = orderedTopology.InitialLives
                .Reverse()
                .ToImmutableArray(),
        };
        var format = new FreeForAllMatchFormatDefinition(4);
        ActorResolvedMatchDefinition ordered = Resolve(
            orderedRules,
            orderedMap,
            format,
            orderedTopology,
            ["west", "east", "north", "south"]);
        ActorResolvedMatchDefinition reversed = Resolve(
            reversedRules,
            reversedMap,
            format,
            reversedTopology,
            ["west", "east", "north", "south"],
            reverseAssignments: true);

        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(orderedRules),
            ActorContractManifestSerializer.ToCanonicalJson(reversedRules));
        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(orderedMap),
            ActorContractManifestSerializer.ToCanonicalJson(reversedMap));
        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(orderedTopology),
            ActorContractManifestSerializer.ToCanonicalJson(
                reversedTopology));
        Assert.Equal(
            ActorContractManifestSerializer.ToCanonicalJson(ordered),
            ActorContractManifestSerializer.ToCanonicalJson(reversed));
        Assert.Equal(
            ActorContractFingerprint.ComputeMatch(ordered),
            ActorContractFingerprint.ComputeMatch(reversed));
    }

    [Fact]
    public void RulesMutationsAffectOnlyRulesAndAggregateComponents()
    {
        ActorMapDefinition map = CreateMap();
        var format = new HeadToHeadMatchFormatDefinition();
        PublicMatchTopology topology = CreateTopology([[10], [20]]);
        ActorRulesDefinition baseline = CreateRules();
        ActorRulesDefinition[] mutations =
        [
            CreateRules(maxTicks: 101),
            CreateRules(killsToWin: 11),
            CreateRules(respawnDelayTicks: 4),
        ];
        ActorResolvedMatchDefinition baselineMatch = Resolve(
            baseline,
            map,
            format,
            topology,
            ["west", "east"]);
        string baselineRules =
            ActorContractFingerprint.ComputeRules(baseline);
        string baselineMap = ActorContractFingerprint.ComputeMap(map);
        string baselineFormat =
            ActorContractFingerprint.ComputeFormat(format);
        string baselineTopology =
            ActorContractFingerprint.ComputeTopology(topology);
        string baselineAggregate =
            ActorContractFingerprint.ComputeMatch(baselineMatch);

        foreach (ActorRulesDefinition mutation in mutations)
        {
            ActorResolvedMatchDefinition mutatedMatch = Resolve(
                mutation,
                map,
                format,
                topology,
                ["west", "east"]);

            Assert.NotEqual(
                baselineRules,
                ActorContractFingerprint.ComputeRules(mutation));
            Assert.Equal(
                baselineMap,
                ActorContractFingerprint.ComputeMap(mutatedMatch.Map));
            Assert.Equal(
                baselineFormat,
                ActorContractFingerprint.ComputeFormat(mutatedMatch.Format));
            Assert.Equal(
                baselineTopology,
                ActorContractFingerprint.ComputeTopology(
                    mutatedMatch.Topology));
            Assert.NotEqual(
                baselineAggregate,
                ActorContractFingerprint.ComputeMatch(mutatedMatch));
        }
    }

    [Fact]
    public void ComponentContentHashesExcludeAliasesButMatchIncludesProvenance()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorRulesDefinition renamedRules = CreateRules(
            rulesetId: "deathmatch-contract-proof-renamed");
        ActorMapDefinition map = CreateMap();
        ActorMapDefinition renamedMap = CloneMap(
            map,
            "shared-arena-renamed",
            version: 7);
        var format = new HeadToHeadMatchFormatDefinition();
        PublicMatchTopology topology = CreateTopology([[10], [20]]);

        Assert.Equal(
            ActorContractFingerprint.ComputeRules(rules),
            ActorContractFingerprint.ComputeRules(renamedRules));
        Assert.Equal(
            ActorContractFingerprint.ComputeMap(map),
            ActorContractFingerprint.ComputeMap(renamedMap));
        Assert.NotEqual(
            ActorContractManifestSerializer.ToCanonicalJson(rules),
            ActorContractManifestSerializer.ToCanonicalJson(renamedRules));
        Assert.NotEqual(
            ActorContractManifestSerializer.ToCanonicalJson(map),
            ActorContractManifestSerializer.ToCanonicalJson(renamedMap));

        ActorResolvedMatchDefinition original = Resolve(
            rules,
            map,
            format,
            topology,
            ["west", "east"]);
        ActorResolvedMatchDefinition renamed = Resolve(
            renamedRules,
            renamedMap,
            format,
            topology,
            ["west", "east"]);
        Assert.NotEqual(
            ActorContractFingerprint.ComputeMatch(original),
            ActorContractFingerprint.ComputeMatch(renamed));
    }

    [Fact]
    public void WritesExplicitEnumsInt64StringsAndEveryTransitionFamily()
    {
        ActorRulesDefinition rules = CreateRules(
            includeTransitions: true,
            faultsAllowedBeforeDisqualification: int.MaxValue);

        string first =
            ActorContractManifestSerializer.ToCanonicalJson(rules);
        string second =
            ActorContractManifestSerializer.ToCanonicalJson(rules);
        string fingerprint = ActorContractFingerprint.ComputeRules(rules);

        Assert.Equal(first, second);
        AssertFingerprint(fingerprint);
        Assert.Contains(
            "\"disqualificationFaultCount\":\"2147483648\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"aimInterpretation\":\"current-facing-plus-relative-eight-way-shot-program\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"hearingBearingModel\":\"eight-octants-strict-two-to-one-cardinal-v1\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"sameTickDecisionSharing\":\"none\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"movementActionResolution\":\"submitted-absolute-cardinal-one-tile-facing-unchanged\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"outputTileProjectile\":\"due-creation-consumes-occupants-by-projectile-id-without-damage-before-spawn\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"faultCounterArithmetic\":\"signed-int64-saturating-at-allowed-plus-one\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"faultBatchEventOrder\":\"participant-then-actor-identity-then-create-start-tick-validation-stage\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"energyArithmetic\":\"checked-int64-then-clamp-to-maximum\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"preserveRatioFormula\":\"floor-current-times-target-maximum-divided-by-source-maximum-then-minimum-one\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"alliedMovementDestinationOverride\":\"pass-through-does-not-block-or-consume-otherwise-use-contact-policy\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"actionAdmission\":\"unknown-or-malformed-faulted-out-of-form-rejected-physical-blocked-explicit-overrides\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"sourceRetirement\":\"does-not-cancel-queued-fabrication-except-participant-disqualification\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"kind\":\"bounded-child\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"kind\":\"form-transition\"",
            first,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"kind\":\"split\"",
            first,
            StringComparison.Ordinal);

        using JsonDocument document = JsonDocument.Parse(first);
        JsonElement root = document.RootElement;
        Assert.Equal(
            fingerprint,
            root.GetProperty("rulesFingerprint").GetString());
        JsonElement split = root.GetProperty("replicationTransitions")[0];
        Assert.Equal(
            [
                (0, -1),
                (0, 1),
                (1, 0),
            ],
            split.GetProperty("candidateOffsets")
                .EnumerateArray()
                .Select(offset => (
                    offset.GetProperty("forward").GetInt32(),
                    offset.GetProperty("right").GetInt32()))
                .ToArray());
    }

    [Fact]
    public void WritesFrontlineCaptureAndOrderedModeMapBinding()
    {
        ActorRulesDefinition rules = CreateFrontlineRules();
        ActorMapDefinition map = CreateFrontlineMap();
        PublicMatchTopology topology = CreateTopology([[10], [20]]);
        InitialDeploymentDefinition deployment = CreateDeployment(
            topology,
            map,
            ["west", "east"]);
        var match = new ActorResolvedMatchDefinition(
            rules,
            map,
            new HeadToHeadMatchFormatDefinition(),
            topology,
            deployment,
            CreateAssignments(topology, deployment),
            [],
            new FrontlineActorModeMapBindingDefinition(
                [
                    "far-west",
                    "near-west",
                    "centre",
                    "near-east",
                    "far-east",
                ],
                [
                    new(
                        1,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardLowerIndex),
                    new(
                        0,
                        FrontlineTeamAdvanceDefinition
                            .ObjectiveAdvanceDirection.TowardHigherIndex),
                ]));

        string json =
            ActorContractManifestSerializer.ToCanonicalJson(match);

        Assert.Contains(
            "\"territorialProgressFormula\":\"per-team-advance-delta-times-index-offset-times-threshold-plus-signed-claim\"",
            json,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"captureArithmetic\":\"checked-int64-add-compare-threshold-completes-one-push-and-discards-overshoot\"",
            json,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"redeployTickArithmetic\":\"checked-int64-capture-tick-plus-one-plus-pause-require-int32\"",
            json,
            StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement binding =
            document.RootElement.GetProperty("modeMapBinding");
        Assert.Equal(
            [
                "far-west",
                "near-west",
                "centre",
                "near-east",
                "far-east",
            ],
            binding.GetProperty("orderedObjectiveRegionIds")
                .EnumerateArray()
                .Select(region => region.GetString()!)
                .ToArray());
        Assert.Equal(
            [0, 1],
            binding.GetProperty("teamAdvances")
                .EnumerateArray()
                .Select(advance =>
                    advance.GetProperty("teamId").GetInt32())
                .ToArray());
    }

    [Fact]
    public void GoldenComponentAndMatchFingerprintsPinCanonicalBytes()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorMapDefinition map = CreateMap();
        var format = new HeadToHeadMatchFormatDefinition();
        PublicMatchTopology topology = CreateTopology([[10], [20]]);
        ActorResolvedMatchDefinition match = Resolve(
            rules,
            map,
            format,
            topology,
            ["west", "east"]);
        string actual = string.Join(
            ",",
            ActorContractFingerprint.ComputeRules(rules),
            ActorContractFingerprint.ComputeMap(map),
            ActorContractFingerprint.ComputeFormat(format),
            ActorContractFingerprint.ComputeTopology(topology),
            ActorContractFingerprint.ComputeMatch(match));

        Assert.Equal(
            "0254de191bfe1e271557bd92f388b7460e1741a19595b60901f75da2f3e54c4b,"
            + "cf00b71f9074627bb4c4d972667e6d4d384674ebe3a109cdf27f439c3ea5d4e0,"
            + "dc81a4f285ada9baceba99751e2de2ede8247cd943ad5c2164368c2f55129463,"
            + "a83214570e1989e3bc170b80744a26b82d69abc272c6d0997789a11f26acd58a,"
            + "d12cefe463556b6028dcbcbfc395f9cef5b963aa8b16549568240ce2cedc74d5",
            actual);
    }

    [Fact]
    public void AggregateEmbedsCapturedCapabilitiesAndStablePropertyOrder()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorMapDefinition map = CreateMap();
        var format = new HeadToHeadMatchFormatDefinition();
        PublicMatchTopology topology = CreateTopology([[10], [20]]);
        var capabilities = new ActorMatchCapabilityVersions(
            "9.1",
            "9.2",
            runtimeContractVersion: 7,
            matchStartSchemaVersion: 8,
            observationSchemaVersion: 9,
            decisionSchemaVersion: 10);
        ActorResolvedMatchDefinition match = Resolve(
            rules,
            map,
            format,
            topology,
            ["west", "east"],
            capabilities: capabilities);

        string first =
            ActorContractManifestSerializer.ToCanonicalJson(match);
        string second =
            ActorContractManifestSerializer.ToCanonicalJson(match);
        string fingerprint = ActorContractFingerprint.ComputeMatch(match);

        Assert.Equal(first, second);
        AssertFingerprint(fingerprint);
        using JsonDocument document = JsonDocument.Parse(first);
        JsonElement root = document.RootElement;
        Assert.Equal(
            ActorContractManifestSerializer.MatchContractSchemaVersion,
            root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            [
                "schemaVersion",
                "matchContractFingerprint",
                "capabilityVersions",
                "rules",
                "map",
                "format",
                "topology",
                "initialDeployment",
                "lifecycleAssignments",
                "participantRegionAssignments",
                "modeMapBinding",
            ],
            root.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.Equal(
            fingerprint,
            root.GetProperty("matchContractFingerprint").GetString());
        JsonElement writtenCapabilities =
            root.GetProperty("capabilityVersions");
        Assert.Equal(
            "9.1",
            writtenCapabilities
                .GetProperty("runtimeProtocolVersion")
                .GetString());
        Assert.Equal(
            10,
            writtenCapabilities
                .GetProperty("decisionSchemaVersion")
                .GetInt32());
    }

    [Fact]
    public void InvalidTopologyAndAggregateFailBeforeFingerprinting()
    {
        ActorRulesDefinition rules = CreateRules();
        ActorMapDefinition map = CreateMap();
        var format = new HeadToHeadMatchFormatDefinition();
        PublicMatchTopology valid = CreateTopology([[10], [20]]);
        PublicMatchTopology invalid = valid with
        {
            Participants =
            [
                new(10, 0),
                new(10, 1),
            ],
        };

        Assert.Throws<ArgumentException>(
            () => ActorContractFingerprint.ComputeTopology(invalid));
        Assert.Throws<ArgumentException>(
            () => ActorContractManifestSerializer.ToCanonicalJson(invalid));

        InitialDeploymentDefinition deployment = CreateDeployment(
            valid,
            map,
            ["west", "east"]);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
            CreateAssignments(valid, deployment);
        Assert.Throws<ActorResolvedMatchValidationException>(() =>
            new ActorResolvedMatchDefinition(
                rules,
                map,
                format,
                invalid,
                deployment,
                assignments,
                [],
                new DeathmatchActorModeMapBindingDefinition()));
    }

    private static ActorResolvedMatchDefinition Resolve(
        ActorRulesDefinition rules,
        ActorMapDefinition map,
        MatchFormatDefinition format,
        PublicMatchTopology topology,
        IReadOnlyList<string> spawnIds,
        bool reverseAssignments = false,
        ActorMatchCapabilityVersions? capabilities = null)
    {
        InitialDeploymentDefinition deployment = CreateDeployment(
            topology,
            map,
            spawnIds);
        ActorUnitSlotLifecycleAssignmentDefinition[] assignments =
            CreateAssignments(topology, deployment);
        return new ActorResolvedMatchDefinition(
            rules,
            map,
            format,
            topology,
            deployment,
            reverseAssignments ? assignments.Reverse() : assignments,
            [],
            new DeathmatchActorModeMapBindingDefinition(),
            capabilities);
    }

    private static ActorRulesDefinition CreateRules(
        int maxTicks = 100,
        int killsToWin = 10,
        int respawnDelayTicks = 3,
        string rulesetId = "deathmatch-contract-proof",
        bool includeTransitions = false,
        int faultsAllowedBeforeDisqualification = 0)
    {
        var movement = new ActorMovementProfileDefinition(
            "ground",
            ActorMovementLayer.Ground);
        ActorVisionProfileDefinition mobileVision = Vision(
            "mobile-vision",
            omnidirectional: false);
        ActorVisionProfileDefinition turretVision = Vision(
            "turret-vision",
            omnidirectional: true);
        ActorAttackProfileDefinition mobileAttack = Attack(
            "mobile-bolt",
            omnidirectional: false);
        ActorAttackProfileDefinition turretAttack = Attack(
            "turret-bolt",
            omnidirectional: true);

        var actions = new List<ActorActionDefinition>
        {
            new("wait", 0, ActorActionKind.Wait, []),
            new(
                "shoot",
                4,
                ActorActionKind.Attack,
                [ActorActionParameterKind.ShotProgram]),
        };
        var forms = new List<ActorFormDefinition>
        {
            new(
                includeTransitions ? "prime-mobile" : "mobile",
                maxHealth: includeTransitions ? 6 : 3,
                movement.Id,
                mobileVision.Id,
                mobileAttack.Id,
                objectiveWeight: 0,
                includeTransitions
                    ?
                    [
                        "wait",
                        "move",
                        "rotate",
                        "shoot",
                        "fabricate",
                        "split",
                    ]
                    : ["wait", "shoot"]),
        };
        var lifecycleProfiles = new List<ActorLifecycleProfileDefinition>
        {
            new(
                "prime-respawn",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .AutomaticRespawn,
                respawnDelayTicks,
                automaticReturnFormId:
                    includeTransitions ? "prime-mobile" : "mobile"),
        };
        var fabrication =
            new List<ActorFabricationTransitionDefinition>();
        var sameLife = new List<ActorSameLifeTransitionDefinition>();
        var replication =
            new List<ActorReplicationTransitionDefinition>();

        if (includeTransitions)
        {
            actions.AddRange(
            [
                new(
                    "move",
                    1,
                    ActorActionKind.Movement,
                    [ActorActionParameterKind.Direction]),
                new(
                    "rotate",
                    2,
                    ActorActionKind.Rotation,
                    [ActorActionParameterKind.Direction]),
                new(
                    "fabricate",
                    100,
                    ActorActionKind.Fabrication,
                    [ActorActionParameterKind.UnitTarget]),
                new(
                    "anchor",
                    101,
                    ActorActionKind.SameLifeTransition,
                    []),
                new(
                    "shoot-direction",
                    102,
                    ActorActionKind.Attack,
                    [ActorActionParameterKind.ProjectileHeading]),
                new(
                    "split",
                    103,
                    ActorActionKind.Replication,
                    []),
            ]);
            forms.AddRange(
            [
                new(
                    "child-mobile",
                    maxHealth: 2,
                    movement.Id,
                    mobileVision.Id,
                    mobileAttack.Id,
                    objectiveWeight: 0,
                    ["wait", "move", "rotate", "shoot", "anchor"]),
                new(
                    "turret",
                    maxHealth: 5,
                    movement.Id,
                    turretVision.Id,
                    turretAttack.Id,
                    objectiveWeight: 0,
                    ["wait", "shoot-direction"]),
            ]);
            lifecycleProfiles.Add(new(
                "child-ready",
                ActorLifecycleProfileDefinition.DestructionPolicyKind
                    .ReadyForExplicitFabrication,
                delayTicks: 2,
                automaticReturnFormId: null));
            fabrication.Add(Fabrication());
            sameLife.Add(Anchor());
            replication.Add(Split());
        }

        return new ActorRulesDefinition(
            rulesetId,
            new ActorRulesLimits(
                maxTicks,
                new ActorRuntimeFaultDefinition(
                    faultsAllowedBeforeDisqualification)),
            new ActorSeedMechanicsDefinition(
                "contract-proof",
                ActorSeedMechanicsDefinition.SeedDerivationKind
                    .MatchSeedProfileTeamUnitLifeMix64V1,
                ActorSeedMechanicsDefinition.LifeIdentityAssignmentKind
                    .PerStableUnitMonotonicStartingAtZero,
                ActorSeedMechanicsDefinition.RuntimeLifetimeKind
                    .FreshRuntimePerLife,
                ActorSeedMechanicsDefinition.PrivateMemoryKind
                    .IsolatedPerRuntime),
            new DeathmatchGameModeDefinition(
                new DeathmatchVictoryDefinition(
                    killsToWin,
                    [
                        new(
                            ScoreChannelDefinition.ChannelKind.Kills,
                            ScoreRankingDefinition.SortDirection.HigherWins),
                    ]),
                [new(ScoreChannelDefinition.ChannelKind.Kills)],
                DeathmatchScoringDefinition.RawHostileKillV1),
            new ActorLifecycleDefinition(lifecycleProfiles),
            forms,
            [movement],
            includeTransitions
                ? [turretVision, mobileVision]
                : [mobileVision],
            includeTransitions
                ? [turretAttack, mobileAttack]
                : [mobileAttack],
            actions,
            fabrication,
            sameLife,
            replication,
            new ActorTeamPerceptionDefinition(
                ActorTeamPerceptionDefinition.PerceptionKind.ImmediateUnion),
            Collisions(),
            new ActorTickResolutionDefinition(
                observationsUsePreTickState: true,
                decisionsResolveAsJointStep: true,
                ActorDamageResolutionDefinition.CanonicalJointV1,
                ActorTickResolutionDefinition.CreateSupportedPhases()));
    }

    private static ActorRulesDefinition ReverseRules(
        ActorRulesDefinition rules) =>
        new(
            rules.RulesetId,
            rules.Limits,
            rules.SeedMechanics,
            rules.GameMode,
            new ActorLifecycleDefinition(
                rules.Lifecycle.Profiles.Reverse()),
            rules.Forms.Reverse(),
            rules.MovementProfiles.Reverse(),
            rules.VisionProfiles.Reverse(),
            rules.AttackProfiles.Reverse(),
            rules.Actions.Reverse(),
            rules.FabricationTransitions.Reverse(),
            rules.SameLifeTransitions.Reverse(),
            rules.ReplicationTransitions.Reverse(),
            rules.TeamPerception,
            rules.Collisions,
            rules.TickResolution);

    private static ActorRulesDefinition CreateFrontlineRules()
    {
        ActorRulesDefinition deathmatch = CreateRules();
        return new ActorRulesDefinition(
            "frontline-contract-proof",
            deathmatch.Limits,
            deathmatch.SeedMechanics,
            new FrontlineGameModeDefinition(
                new FrontlineVictoryDefinition(
                    pushesToBreach: 3,
                    [
                        new(
                            ScoreChannelDefinition.ChannelKind
                                .TerritorialProgress,
                            ScoreRankingDefinition.SortDirection.HigherWins),
                    ]),
                [
                    new(
                        ScoreChannelDefinition.ChannelKind
                            .TerritorialProgress),
                ],
                frontlinePositionCount: 5,
                new FrontlineCaptureDefinition(
                    threshold: 3,
                    gainPerSoleTeamTick: 1,
                    decayAmount: 1,
                    decayIntervalTicks: 2,
                    redeployPauseTicks: 1)),
            deathmatch.Lifecycle,
            deathmatch.Forms.Select(form =>
                new ActorFormDefinition(
                    form.Id,
                    form.MaxHealth,
                    form.MovementProfileId,
                    form.VisionProfileId,
                    form.AttackProfileId,
                    objectiveWeight: 1,
                    form.AllowedActionIds)),
            deathmatch.MovementProfiles,
            deathmatch.VisionProfiles,
            deathmatch.AttackProfiles,
            deathmatch.Actions,
            deathmatch.FabricationTransitions,
            deathmatch.SameLifeTransitions,
            deathmatch.ReplicationTransitions,
            deathmatch.TeamPerception,
            deathmatch.Collisions,
            deathmatch.TickResolution);
    }

    private static ActorVisionProfileDefinition Vision(
        string id,
        bool omnidirectional) =>
        new(
            id,
            range: omnidirectional ? 8 : 6,
            ActorVisionDistanceMetric.Chebyshev,
            omnidirectional
                ? ActorVisionShape.Omnidirectional
                : ActorVisionShape.FacingQuadrant,
            omnidirectionalProximityRange: omnidirectional ? 0 : 1,
            ActorLineOfSightModel.CornerStrictSupercover,
            hearingRadius: 8,
            hearingBearingSectors: 8,
            ActorHearingBearingModel
                .EightOctantsStrictTwoToOneCardinalV1,
            hearingDistanceBandUpperBounds: [2, 5],
            loudEventKinds:
            [
                ActorAudibleEventKind.Destruction,
                ActorAudibleEventKind.Damage,
                ActorAudibleEventKind.Attack,
            ]);

    private static ActorAttackProfileDefinition Attack(
        string id,
        bool omnidirectional)
    {
        bool programsEnabled = !omnidirectional;
        var projectile = new ActorProjectileDefinition(
            ActorProjectileMode.Discrete,
            damagePerHit: 1,
            maxTravelTiles: 8,
            ticksPerAdvance: 1,
            tilesPerAdvance: 2,
            launchTiles: 1,
            advancesOnLaunchTick: false,
            damageAppliedSimultaneously: true,
            diagonalCornersMustBeClear: true);
        var shotProgram = new ActorShotProgramDefinition(
            enabled: programsEnabled,
            headingSectors: 8,
            ActorShotHeadingModel.EightWayClockwiseModuloV1,
            bendStepSectors: 1,
            minInitialAimSteps: programsEnabled ? -1 : 0,
            maxInitialAimSteps: programsEnabled ? 1 : 0,
            new ActorAimOnlyShotProgramDefinition(0, 0, 1, 0),
            allowedCurvedBendDirections: [-1, 1],
            minBendAfterTiles: 1,
            maxBendAfterTiles: programsEnabled ? 4 : 1,
            minBendEveryTiles: 1,
            maxBendEveryTiles: programsEnabled ? 3 : 1,
            minBendCount: 1,
            maxBendCount: programsEnabled ? 3 : 1,
            launchTiles: 1,
            payloadOptional: programsEnabled,
            defaultProgram: new ActorShotProgramValue(0, 0, 0, 1, 0),
            invalidPayloadResult: programsEnabled
                ? ActorActionRejectionResult.Rejected
                : null,
            unsupportedPayloadResult: ActorActionRejectionResult.Blocked,
            diagonalCornersMustBeClear: true);
        return new ActorAttackProfileDefinition(
            id,
            omnidirectional,
            projectile,
            cooldownTicks: 3,
            maxEnergy: 10,
            attackEnergyCost: 5,
            energyRegenerationIntervalTicks: 2,
            energyRegenerationAmount: 1,
            shotProgram);
    }

    private static BoundedChildFabricationDefinition Fabrication() =>
        new(
            "fabricate-child",
            "fabricate",
            ["prime-mobile"],
            "child-mobile",
            "source-pad",
            "output-pad",
            requiredSourceTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
            ],
            requiredOutputTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind.SpawnProtected,
            ],
            forbiddenOutputTileTags:
            [
                ActorMapTileTagDefinition.TileTagKind
                    .TransitionPlacementForbidden,
            ],
            candidateOffsets: [new(0, -1), new(0, 1)],
            new ActorFabricationDelayDefinition(1),
            ActorActionRejectionResult.Blocked);

    private static ActorFormTransitionDefinition Anchor() =>
        new(
            "anchor-child",
            "anchor",
            "child-mobile",
            "turret",
            Windup(),
            ActorSameLifeTransitionDefinition.MemoryContinuityKind
                .PreservePrivateMemory,
            new ActorSameLifeHealthDefinition(
                ActorSameLifeHealthDefinition.HealthPolicyKind
                    .AddFlatCappedToTargetMaximum,
                flatHealthGain: 2),
            ActorSameLifeCombatStateDefinition.PreserveWithoutRefillV1,
            new ActorSameLifePlacementDefinition(
                ActorSameLifePlacementDefinition.PositionContinuityKind
                    .SameOccupiedGroundTile,
                ActorSameLifePlacementDefinition.LegalityEvaluationKind
                    .QueueAndCompletionTileTags,
                requiredTileTags: [],
                forbiddenTileTags:
                [
                    ActorMapTileTagDefinition.TileTagKind
                        .TransitionPlacementForbidden,
                ],
                ActorSameLifePlacementDefinition.FailedCompletionKind
                    .CancelAndRemainInSourceForm),
            irreversibleForLife: true);

    private static SplitReplicationTransitionDefinition Split() =>
        new(
            "split-prime",
            "split",
            ["prime-mobile"],
            "child-mobile",
            descendantCount: 2,
            maxSourceGeneration: 0,
            requireNoPriorSameLifeTransition: true,
            new ActorReplicationHealthDefinition(
                ActorReplicationHealthDefinition.DistributionKind
                    .DivideCurrentHealthEquallyFloor,
                minimumHealthPerDescendant: 1,
                ActorReplicationHealthDefinition.RemainderKind.Discard),
            candidateOffsets:
            [
                new(0, -1),
                new(0, 1),
                new(1, 0),
            ],
            Windup());

    private static ActorTransitionWindupDefinition Windup() =>
        new(
            durationTicks: 1,
            ActorTransitionWindupDefinition.PendingActionKind.WaitOnly,
            ActorTransitionWindupDefinition.SourceFormKind.RetainSourceForm,
            ActorTransitionWindupDefinition.TargetabilityKind
                .TargetableAndOccupiesTile,
            ActorTransitionWindupDefinition.LethalDamageKind.CancelTransition,
            ActorTransitionWindupDefinition.ActorTransitionCompletionKind
                .TickStartAfterDuration,
            ActorTransitionWindupDefinition.PlacementReferenceKind
                .QueueTimePose);

    private static ActorCollisionDefinition Collisions() =>
        new(
            actorsBlockWalls: true,
            actorsBlockActors: true,
            sameDestinationMovesBlockAll: true,
            swapMovesBlocked: true,
            followingVacatedActorAllowed: false,
            projectilesBlockMovement: true,
            movingOntoProjectileCausesHit: true,
            wallsConsumeProjectiles: true,
            projectilesIgnoreFiringLife: true,
            projectilesStopOnFirstEnemyActor: true,
            projectilesCollideWithProjectiles: false,
            ActorCollisionDefinition.AlliedProjectileContactKind.PassThrough);

    private static ActorMapDefinition CreateMap() =>
        new(
            "shared-arena",
            version: 1,
            [
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#########",
            ],
            [
                Spawn("west", 1, 3, Direction.East),
                Spawn("east", 7, 3, Direction.West),
                Spawn("north", 4, 1, Direction.South),
                Spawn("south", 4, 5, Direction.North),
            ],
            [],
            []);

    private static ActorMapDefinition CreateFrontlineMap() =>
        new(
            "frontline-arena",
            version: 1,
            [
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#########",
            ],
            [
                Spawn("west", 1, 3, Direction.East),
                Spawn("east", 7, 3, Direction.West),
            ],
            [
                Objective("far-west", 2),
                Objective("near-west", 3),
                Objective("centre", 4),
                Objective("near-east", 5),
                Objective("far-east", 6),
            ],
            []);

    private static ActorMapRegionDefinition Objective(string id, int x) =>
        new(
            id,
            ActorMapRegionDefinition.RegionKind.Objective,
            [new Position(x, 2)]);

    private static ActorMapDefinition CloneMap(
        ActorMapDefinition map,
        string id,
        int version) =>
        new(
            id,
            version,
            map.TileRows,
            map.SpawnAnchors,
            map.Regions,
            map.TileTags);

    private static ActorMapSpawnAnchorDefinition Spawn(
        string id,
        int x,
        int y,
        Direction facing) =>
        new(
            new InitialSpawnDefinition(id, new Position(x, y), facing),
            [ActorMovementLayer.Ground]);

    private static PublicMatchTopology CreateTopology(
        IReadOnlyList<IReadOnlyList<int>> participantIdsByTeam)
    {
        var teams = new List<PublicScoringTeam>();
        var participants = new List<PublicParticipant>();
        var slots = new List<PublicUnitSlot>();
        var lives = new List<PublicInitialLife>();
        for (int teamId = 0; teamId < participantIdsByTeam.Count; teamId++)
        {
            teams.Add(new(teamId));
            IReadOnlyList<int> teamParticipants =
                participantIdsByTeam[teamId];
            for (int unitId = 0; unitId < teamParticipants.Count; unitId++)
            {
                int participantId = teamParticipants[unitId];
                participants.Add(new(participantId, teamId));
                slots.Add(new(teamId, unitId, participantId));
                lives.Add(new(teamId, unitId, 0, "mobile"));
            }
        }

        return new PublicMatchTopology
        {
            Teams = teams.ToImmutableArray(),
            Participants = participants.ToImmutableArray(),
            UnitSlots = slots.ToImmutableArray(),
            InitialLives = lives.ToImmutableArray(),
        };
    }

    private static InitialDeploymentDefinition CreateDeployment(
        PublicMatchTopology topology,
        ActorMapDefinition map,
        IReadOnlyList<string> spawnIds)
    {
        PublicInitialLife[] lives = topology.InitialLives
            .OrderBy(life => life.TeamId)
            .ThenBy(life => life.UnitId)
            .ToArray();
        Dictionary<string, InitialSpawnDefinition> mapSpawns =
            map.SpawnAnchors.ToDictionary(
                anchor => anchor.Spawn.SpawnId,
                anchor => anchor.Spawn,
                StringComparer.Ordinal);
        return new InitialDeploymentDefinition(
            spawnIds
                .Select(spawnId => mapSpawns[spawnId])
                .ToImmutableArray(),
            lives.Select((life, index) =>
                    new InitialLifeDeployment(
                        life.TeamId,
                        life.UnitId,
                        life.LifeId,
                        life.FormId,
                        spawnIds[index]))
                .ToImmutableArray());
    }

    private static ActorUnitSlotLifecycleAssignmentDefinition[]
        CreateAssignments(
            PublicMatchTopology topology,
            InitialDeploymentDefinition deployment)
    {
        Dictionary<(int TeamId, int UnitId), string> spawnIds =
            deployment.Lives.ToDictionary(
                life => (life.TeamId, life.UnitId),
                life => life.SpawnId);
        return topology.UnitSlots
            .Select(slot =>
                new ActorUnitSlotLifecycleAssignmentDefinition(
                    slot.TeamId,
                    slot.UnitId,
                    "prime-respawn",
                    initialGeneration: 0,
                    allowedFormIds: ["mobile"],
                    ActorUnitSlotLifecycleAssignmentDefinition
                        .InitialAvailabilityKind.ActiveAtTickZero,
                    unlockTick: null,
                    assignedRespawnSpawnId:
                        spawnIds[(slot.TeamId, slot.UnitId)]))
            .ToArray();
    }

    private static void AssertFingerprint(string fingerprint)
    {
        Assert.Equal(64, fingerprint.Length);
        Assert.All(
            fingerprint,
            character => Assert.True(
                character is >= '0' and <= '9'
                    or >= 'a' and <= 'f'));
    }

    private static Type[] DiscoverReachableContractEnumTypes()
    {
        Type[] roots =
        [
            typeof(ActorRulesDefinition),
            typeof(ActorMapDefinition),
            typeof(MatchFormatDefinition),
            typeof(PublicMatchTopology),
            typeof(ActorResolvedMatchDefinition),
        ];
        Assembly engineAssembly = typeof(ActorRulesDefinition).Assembly;
        Type[] engineTypes = engineAssembly.GetTypes();
        var pending = new Stack<Type>(roots);
        var visited = new HashSet<Type>();
        var enumTypes = new HashSet<Type>();

        while (pending.TryPop(out Type? candidate))
        {
            Type type = Nullable.GetUnderlyingType(candidate) ?? candidate;
            if (type.IsArray)
            {
                pending.Push(type.GetElementType()!);
                continue;
            }
            if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                    pending.Push(argument);
            }
            if (type.IsEnum)
            {
                enumTypes.Add(type);
                continue;
            }
            if (type.Namespace != typeof(ActorRulesDefinition).Namespace
                || !visited.Add(type))
            {
                continue;
            }

            foreach (PropertyInfo property in type.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length == 0)
                    pending.Push(property.PropertyType);
            }

            if (type.IsAbstract)
            {
                foreach (Type variant in engineTypes)
                {
                    if (!variant.IsAbstract
                        && variant != type
                        && type.IsAssignableFrom(variant))
                    {
                        pending.Push(variant);
                    }
                }
            }
        }

        return enumTypes
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsCanonicalKebabId(string value)
    {
        bool expectsSegmentStart = true;
        foreach (char character in value)
        {
            if (character == '-')
            {
                if (expectsSegmentStart)
                    return false;
                expectsSegmentStart = true;
                continue;
            }
            if (character is not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9'))
            {
                return false;
            }
            expectsSegmentStart = false;
        }
        return !expectsSegmentStart;
    }
}
