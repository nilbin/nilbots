using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotArena.Engine.Tests.Support;

namespace BotArena.Engine.Tests;

public sealed class ReplayV2SerializerTests
{
    private const ulong ExactJsUnsafeSeed = 9_007_199_254_740_993UL;
    private const long ExactJsUnsafeProjectileId = 9_007_199_254_740_993L;

    [Fact]
    public void CanonicalCodec_IsDeterministicAcrossInsertionOrder()
    {
        ReplayV2 forward = CreateReplay(reverseInsertionOrder: false);
        ReplayV2 reverse = CreateReplay(reverseInsertionOrder: true);

        string forwardPayload = ReplayV2Serializer.ToCanonicalJson(forward);
        string reversePayload = ReplayV2Serializer.ToCanonicalJson(reverse);
        string expectedHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(forwardPayload)));
        Assert.Equal(forwardPayload, reversePayload);
        Assert.Equal(
            "cb5b2ded89f3785e46f596746fd51066997d21ce32a5e31f702153ac83df0866",
            ReplayV2Serializer.ComputeHash(forward));
        Assert.Equal(expectedHash, ReplayV2Serializer.ComputeHash(forward));
        Assert.Equal(
            ReplayV2Serializer.ComputeHash(forward),
            ReplayV2Serializer.ComputeHash(reverse));
        Assert.Equal(
            ReplayV2Serializer.ToJson(forward),
            ReplayV2Serializer.ToJson(reverse));
        Assert.True(ReplayV2Serializer.VerifyHash(
            ReplayV2Serializer.ToJson(forward)));
    }

    [Fact]
    public void CanonicalCodec_EmbedsExactManifestAndUsesSafeWireIds()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: true);
        string json = ReplayV2Serializer.ToJson(replay);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement header = root.GetProperty("header");
        JsonElement tick = root.GetProperty("ticks")[0];

        Assert.Equal(
            ExactJsUnsafeSeed.ToString(),
            header.GetProperty("seed").GetString());
        Assert.Equal(
            JsonValueKind.String,
            header.GetProperty("seed").ValueKind);
        Assert.Equal(
            RulesManifestSerializer.ToCanonicalJson(replay.Header.Contract),
            header.GetProperty("contract").GetRawText());
        Assert.Equal(
            [0, 1],
            header.GetProperty("participants")
                .EnumerateArray()
                .Select(value => value.GetProperty("participantId").GetInt32())
                .ToArray());
        JsonElement actorTurn = tick.GetProperty("actors")[0];
        JsonElement enemyReference = actorTurn
            .GetProperty("observation")
            .GetProperty("enemies")[0]
            .GetProperty("actor");
        Assert.Equal(
            "enemy-life-0",
            enemyReference.GetProperty("lifeHandle").GetString());
        Assert.False(enemyReference.TryGetProperty("lifeId", out _));
        JsonElement enemyAlias = actorTurn
            .GetProperty("aliases")
            .GetProperty("enemyLives")[0];
        Assert.Equal(
            "enemy-life-0",
            enemyAlias.GetProperty("lifeHandle").GetString());
        Assert.Equal(
            0,
            enemyAlias.GetProperty("actorId")
                .GetProperty("lifeId")
                .GetInt32());

        Assert.Equal(
            [],
            tick.GetProperty("tickStart")
                .GetProperty("lifecycleEvents")
                .EnumerateArray()
                .Select(value => value.GetProperty("eventId").GetString()!)
                .ToArray());
        JsonElement resolutionEvent = Assert.Single(
            tick.GetProperty("resolution")
                .GetProperty("events")
                .EnumerateArray());
        Assert.Equal(
            "resolution:0:0",
            resolutionEvent.GetProperty("eventId").GetString());
        Assert.Equal(
            ExactJsUnsafeProjectileId.ToString(),
            resolutionEvent.GetProperty("projectileId").GetString());
        Assert.Equal(
            0,
            resolutionEvent.GetProperty("sourceActorId")
                .GetProperty("teamId")
                .GetInt32());
        Assert.Equal(
            1,
            resolutionEvent.GetProperty("targetActorId")
                .GetProperty("teamId")
                .GetInt32());

        Assert.Equal(
            ["2", "10", ExactJsUnsafeProjectileId.ToString()],
            tick.GetProperty("postState")
                .GetProperty("projectiles")
                .EnumerateArray()
                .Select(value =>
                {
                    JsonElement id = value.GetProperty("projectileId");
                    Assert.Equal(JsonValueKind.String, id.ValueKind);
                    return id.GetString()!;
                })
                .ToArray());
        Assert.False(root.GetProperty("partial").GetBoolean());
    }

    [Fact]
    public void CanonicalCodec_PreservesEveryInt64MetricPastJsSafeRange()
    {
        const string damageDealt = "9007199254740993";
        const string creditedDamageDealt = "9007199254740994";
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        PublicFrontlineDefinition frontline =
            replay.Header.Contract.Rules.Frontline!;
        PublicRulesManifest wideRules = replay.Header.Contract.Rules with
        {
            RulesFingerprint = "",
            Frontline = frontline with
            {
                FrontlinePositionCount = int.MaxValue,
                Capture = frontline.Capture with
                {
                    Threshold = int.MaxValue,
                },
            },
        };
        wideRules = wideRules with
        {
            RulesFingerprint = MatchContractFingerprint.ComputeRules(
                wideRules,
                FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1)),
        };
        PublicMatchContractManifest wideContract =
            replay.Header.Contract with
            {
                Rules = wideRules,
                MatchContractFingerprint = "",
            };
        wideContract = wideContract with
        {
            MatchContractFingerprint =
                MatchContractFingerprint.ComputeMatch(wideContract),
        };
        string territorialScore = (
            (long)(replay.Result.Control.ActivePositionIndex
                - (int.MaxValue / 2))
            * int.MaxValue).ToString(CultureInfo.InvariantCulture);
        Assert.True(
            Math.Abs(long.Parse(
                territorialScore,
                CultureInfo.InvariantCulture))
            > 9_007_199_254_740_991L);
        replay = replay with
        {
            Header = replay.Header with
            {
                Contract = wideContract,
            },
            Ticks = replay.Ticks
                .Select(tick => tick with
                {
                    Actors = tick.Actors
                        .Select(actor => actor with
                        {
                            LifeStart = actor.LifeStart is { } lifeStart
                                ? lifeStart with
                                {
                                    MatchContractFingerprint =
                                        wideContract
                                            .MatchContractFingerprint,
                                }
                                : null,
                            Observation = actor.Observation with
                            {
                                MatchContractFingerprint =
                                    wideContract.MatchContractFingerprint,
                            },
                        })
                        .ToImmutableArray(),
                })
                .ToImmutableArray(),
            Result = replay.Result with
            {
                WinnerTeamId = 1,
                Teams = replay.Result.Teams
                    .Select(team => team with
                    {
                        Outcome = team.TeamId == 1
                            ? FrontlineTeamOutcome.Win
                            : FrontlineTeamOutcome.Loss,
                    })
                    .ToImmutableArray(),
            },
        };
        replay = WithTick(replay, tick => tick with
        {
            TickStart = tick.TickStart with
            {
                State = WithDamageDealt(
                    tick.TickStart.State,
                    damageDealt),
            },
            PostState = WithDamageDealt(
                tick.PostState,
                damageDealt) with
            {
                Teams = WithDamageDealt(
                        tick.PostState,
                        damageDealt)
                    .Teams
                    .Select(team => team.TeamId != 0
                        ? team
                        : team with
                        {
                            DamageDealt = creditedDamageDealt,
                            Units = team.Units
                                .Select(unit => unit with
                                {
                                    DamageDealt =
                                        creditedDamageDealt,
                                    ActiveLife =
                                        unit.ActiveLife! with
                                        {
                                            DamageDealt =
                                                creditedDamageDealt,
                                        },
                                })
                                .ToImmutableArray(),
                        })
                    .ToImmutableArray(),
            },
        });
        replay = replay with
        {
            Result = replay.Result with
            {
                TerritorialScore = territorialScore,
                Teams = replay.Result.Teams
                    .Select(team => team with
                    {
                        DamageDealt = team.TeamId == 0
                            ? creditedDamageDealt
                            : damageDealt,
                        Units = team.Units
                            .Select(unit => unit with
                            {
                                DamageDealt = team.TeamId == 0
                                    ? creditedDamageDealt
                                    : damageDealt,
                            })
                            .ToImmutableArray(),
                    })
                    .ToImmutableArray(),
            },
        };

        string json = ReplayV2Serializer.ToJson(replay);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement[] damageValues = FindProperties(
                document.RootElement,
                "damageDealt")
            .ToArray();
        JsonElement score = Assert.Single(FindProperties(
            document.RootElement,
            "territorialScore"));

        Assert.Equal(16, damageValues.Length);
        Assert.All(damageValues, value =>
        {
            Assert.Equal(JsonValueKind.String, value.ValueKind);
            Assert.True(long.Parse(
                    value.GetString()!,
                    CultureInfo.InvariantCulture)
                > 9_007_199_254_740_991L);
        });
        Assert.Contains(damageValues, value =>
            value.GetString() == damageDealt);
        Assert.Contains(damageValues, value =>
            value.GetString() == creditedDamageDealt);
        Assert.Equal(JsonValueKind.String, score.ValueKind);
        Assert.Equal(territorialScore, score.GetString());
        Assert.True(ReplayV2Serializer.VerifyHash(json));
    }

    [Fact]
    public void ObservationProjection_PreservesNullVersusEmptyCapabilities()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        using JsonDocument document =
            JsonDocument.Parse(ReplayV2Serializer.ToJson(replay));
        JsonElement[] actors = document.RootElement
            .GetProperty("ticks")[0]
            .GetProperty("actors")
            .EnumerateArray()
            .ToArray();

        JsonElement first = actors[0].GetProperty("observation");
        JsonElement second = actors[1].GetProperty("observation");
        Assert.Equal(
            JsonValueKind.Null,
            first.GetProperty("visibleProjectiles").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            first.GetProperty("heardSounds").ValueKind);
        Assert.Empty(
            second.GetProperty("visibleProjectiles").EnumerateArray());
        Assert.Empty(second.GetProperty("heardSounds").EnumerateArray());
    }

    [Fact]
    public void Validation_RejectsImpossibleDamageHealthChain()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        replay = WithTick(replay, tick => tick with
        {
            Resolution = tick.Resolution with
            {
                Events = tick.Resolution.Events
                    .Select(value =>
                        value.Type == FrontlineMatchEventType.Damage
                            ? value with
                            {
                                NewHealth = value.NewHealth + 1,
                            }
                            : value)
                    .ToImmutableArray(),
            },
        });

        AssertInvalid(replay, "health chain");
    }

    [Fact]
    public void Validation_RejectsProjectileOwnerOutsideStableUnitTopology()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        var undeclaredOwner = new ReplayV2ActorId(99, 99, 0);
        replay = WithTick(replay, tick =>
        {
            ReplayV2Event damage = Assert.Single(
                tick.Resolution.Events,
                value => value.Type == FrontlineMatchEventType.Damage);
            var traversal = new ReplayV2ProjectileTraversal(
                "999999",
                undeclaredOwner,
                Direction.East,
                damage.From!.Value,
                [damage.To!.Value],
                ProjectileHeading.East,
                ShotProgram: null,
                ProgrammedPath: null);
            return tick with
            {
                Resolution = tick.Resolution with
                {
                    Events = tick.Resolution.Events
                        .Select(value => value.EventId != damage.EventId
                            ? value
                            : value with
                            {
                                SourceActorId = undeclaredOwner,
                                ProjectileId = traversal.ProjectileId,
                            })
                        .ToImmutableArray(),
                    ProjectileTraversals =
                        tick.Resolution.ProjectileTraversals.Add(
                            traversal),
                },
            };
        });

        AssertInvalid(replay, "contract topology");
    }

    [Fact]
    public void Validation_AllowsOldLifeOwnerAndCreditsOnlyStableUnit()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        var oldLifeOwner = new ReplayV2ActorId(0, 0, 999);
        replay = WithTick(replay, tick => tick with
        {
            TickStart = tick.TickStart with
            {
                State = tick.TickStart.State with
                {
                    Projectiles = tick.TickStart.State.Projectiles
                        .Select(projectile =>
                            projectile.ProjectileId
                                == ExactJsUnsafeProjectileId.ToString(
                                    CultureInfo.InvariantCulture)
                                ? projectile with
                                {
                                    OwnerActorId = oldLifeOwner,
                                }
                                : projectile)
                        .ToImmutableArray(),
                },
            },
            Resolution = tick.Resolution with
            {
                Events = tick.Resolution.Events
                    .Select(value =>
                        value.Type == FrontlineMatchEventType.Damage
                            ? value with
                            {
                                SourceActorId = oldLifeOwner,
                            }
                            : value)
                    .ToImmutableArray(),
            },
            PostState = tick.PostState with
            {
                Teams = tick.PostState.Teams
                    .Select(team => team.TeamId != 0
                        ? team
                        : team with
                        {
                            Units = team.Units
                                .Select(unit => unit.UnitId != 0
                                    ? unit
                                    : unit with
                                    {
                                        ActiveLife =
                                            unit.ActiveLife! with
                                            {
                                                DamageDealt = "0",
                                            },
                                    })
                                .ToImmutableArray(),
                        })
                    .ToImmutableArray(),
                Projectiles = tick.PostState.Projectiles
                    .Select(projectile =>
                        projectile.ProjectileId
                            == ExactJsUnsafeProjectileId.ToString(
                                CultureInfo.InvariantCulture)
                            ? projectile with
                            {
                                OwnerActorId = oldLifeOwner,
                            }
                            : projectile)
                    .ToImmutableArray(),
            },
        });

        string json = ReplayV2Serializer.ToJson(replay);
        Assert.True(ReplayV2Serializer.VerifyHash(json));
    }

    [Fact]
    public void TickCarriesPreparedAndPostResolutionWorldSnapshots()
    {
        ReplayV2Tick tick = Assert.Single(
            CreateReplay(reverseInsertionOrder: false).Ticks);
        ReplayV2LifeState preparedLife = tick.TickStart.State.Teams[0]
            .Units[0]
            .ActiveLife!;
        ReplayV2LifeState postLife = tick.PostState.Teams[0]
            .Units[0]
            .ActiveLife!;

        Assert.Equal(0, tick.TickStart.State.Control.NextTick);
        Assert.Equal(1, tick.PostState.Control.NextTick);
        Assert.Equal(ActionResult.None, preparedLife.PreviousActionResult);
        Assert.Equal(ActionResult.Success, postLife.PreviousActionResult);
    }

    [Fact]
    public void VerifyHash_RejectsTampering()
    {
        string json = ReplayV2Serializer.ToJson(
            CreateReplay(reverseInsertionOrder: false));
        string tampered = json.Replace(
            "\"captureProgress\":0",
            "\"captureProgress\":1",
            StringComparison.Ordinal);

        Assert.NotEqual(json, tampered);
        Assert.True(ReplayV2Serializer.VerifyHash(json));
        Assert.False(ReplayV2Serializer.VerifyHash(
            tampered,
            out string? failure));
        Assert.Equal("Replay-v2 hash mismatch.", failure);
    }

    [Fact]
    public void VersionProbe_DispatchesWithoutChangingLegacySerializer()
    {
        string v2 = ReplayV2Serializer.ToJson(
            CreateReplay(reverseInsertionOrder: false));

        Assert.Equal(
            ReplayV2DocumentFormat.LegacyV1,
            ReplayV2VersionProbe.Probe(
                """{"header":{"replayVersion":1}}"""));
        Assert.Equal(
            ReplayV2DocumentFormat.EntityV2,
            ReplayV2VersionProbe.Probe(v2));
        Assert.Throws<NotSupportedException>(() =>
            ReplayV2VersionProbe.Probe(
                """{"header":{"replayVersion":3}}"""));
        Assert.Throws<InvalidDataException>(() =>
            ReplayV2VersionProbe.Probe("""{"header":{}}"""));
        Assert.Equal(1, BotArenaVersions.ReplayFormatVersion);
        Assert.Equal(2, BotArenaVersions.EntityReplayFormatVersion);
    }

    [Fact]
    public void PartialCodec_RepresentsZeroTickLiveDocument()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        string json = ReplayV2Serializer.ToPartialJson(
            replay.Header,
            []);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Empty(root.GetProperty("ticks").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("result").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("replayHash").ValueKind);
        Assert.True(root.GetProperty("partial").GetBoolean());
        Assert.False(ReplayV2Serializer.VerifyHash(json));
    }

    [Fact]
    public void Projection_CanonicalizesDtoCollectionsBeforeSerialization()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: true);

        Assert.Equal(
            [0, 1],
            replay.Header.Participants
                .Select(participant => participant.ParticipantId)
                .ToArray());
        ReplayV2MapPresentation presentation =
            replay.Header.Presentation!.Map!;
        Assert.Equal(
            ["alpha", "zeta"],
            presentation.WallGroups
                .Select(group => group.Family)
                .ToArray());
        Assert.Equal(
            [new Position(3, 1), new Position(4, 1)],
            presentation.WallGroups[0].Tiles.ToArray());

        ReplayV2ActorTurn actor = replay.Ticks[0].Actors
            .Single(turn => turn.ActorId.TeamId == 0);
        Assert.Equal(
            actor.Observation.Actions
                .OrderBy(action => action.ActionCode)
                .ThenBy(action => action.ActionId, StringComparer.Ordinal),
            actor.Observation.Actions);
        Assert.Equal(
            actor.Observation.VisibleTiles
                .OrderBy(tile => tile.Position.Y)
                .ThenBy(tile => tile.Position.X),
            actor.Observation.VisibleTiles);
        Assert.Equal("future-flight", actor.RuntimeReply.ActionId);
        Assert.Equal(9_007, actor.RuntimeReply.ActionCode);

        var control = new FrontlineControlState(
            NextTick: 1,
            ActivePositionIndex: 1,
            ClaimingTeamId: null,
            CaptureProgress: 0,
            DecayTicksElapsed: 0,
            ControlResumesAtTick: 0,
            WinnerTeamId: null);
        ReplayV2Result projectedResult = ReplayV2Projection.Result(
            new FrontlineMatchResult(
                WinnerTeamId: null,
                Reason: FrontlineMatchEndReason.MaxTicks,
                EndTick: 0,
                TerritorialScore: long.MinValue,
                Control: control,
                Teams:
                [
                    new(
                        1,
                        FrontlineTeamOutcome.Draw,
                        3,
                        long.MaxValue,
                        [new(
                            1,
                            0,
                            "prime-mobile",
                            FrontlineLifecycleStatus.Active,
                            new FrontlineActorId(1, 0, 0),
                            3,
                            long.MaxValue)]),
                    new(
                        0,
                        FrontlineTeamOutcome.Draw,
                        3,
                        long.MaxValue,
                        [new(
                            0,
                            0,
                            "prime-mobile",
                            FrontlineLifecycleStatus.Active,
                            new FrontlineActorId(0, 0, 0),
                            3,
                            long.MaxValue)]),
                ]));
        Assert.Equal(
            [0, 1],
            projectedResult.Teams.Select(team => team.TeamId).ToArray());
        Assert.Equal(
            long.MinValue.ToString(CultureInfo.InvariantCulture),
            projectedResult.TerritorialScore);
        Assert.All(
            projectedResult.Teams,
            team => Assert.Equal(
                long.MaxValue.ToString(CultureInfo.InvariantCulture),
                team.DamageDealt));
    }

    [Fact]
    public void Validation_RejectsNonCanonicalInt64MetricStringsAndNumbers()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        string[] invalidScores =
        [
            "01",
            "+1",
            "-0",
            " 1",
            "9223372036854775808",
        ];
        foreach (string value in invalidScores)
        {
            AssertInvalid(
                replay with
                {
                    Result = replay.Result with
                    {
                        TerritorialScore = value,
                    },
                },
                "canonical");
        }

        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    Teams = replay.Result.Teams
                        .Select((team, index) => index == 0
                            ? team with { DamageDealt = "-1" }
                            : team)
                        .ToImmutableArray(),
                },
            },
            "non-negative");

        string json = ReplayV2Serializer.ToJson(replay);
        string numericScore = json.Replace(
            "\"territorialScore\":\"0\"",
            "\"territorialScore\":0",
            StringComparison.Ordinal);
        Assert.NotEqual(json, numericScore);
        Assert.False(ReplayV2Serializer.VerifyHash(
            numericScore,
            out string? failure));
        Assert.Contains(
            "Int64",
            failure,
            StringComparison.OrdinalIgnoreCase);

        string nonCanonicalActorSeed = json.Replace(
            "\"actorRandomSeed\":\"0\"",
            "\"actorRandomSeed\":\"00\"",
            StringComparison.Ordinal);
        Assert.NotEqual(json, nonCanonicalActorSeed);
        Assert.False(ReplayV2Serializer.VerifyHash(
            nonCanonicalActorSeed,
            out failure));
        Assert.Contains(
            "actor seeds",
            failure,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_RejectsNonContiguousOrMisalignedTickChronology()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        ReplayV2Tick tick = Assert.Single(replay.Ticks);

        AssertInvalid(
            replay with { Ticks = [tick with { Tick = 1 }] },
            "contiguous from zero");
        AssertInvalid(
            WithTick(replay, value => value with
            {
                TickStart = value.TickStart with
                {
                    State = value.TickStart.State with
                    {
                        Control = value.TickStart.State.Control with
                        {
                            NextTick = 1,
                        },
                    },
                },
            }),
            "tick-start objective");
        AssertInvalid(
            WithTick(replay, value => value with
            {
                PostState = value.PostState with
                {
                    Control = value.PostState.Control with
                    {
                        NextTick = 2,
                    },
                },
            }),
            "post-state objective");
        Assert.Throws<ArgumentException>(() =>
            ReplayV2Serializer.ToPartialJson(
                replay.Header,
                [tick with { Tick = 1 }]));
    }

    [Fact]
    public void Validation_RejectsTickZeroDeploymentDefaultFormDrift()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);

        AssertInvalid(
            WithTickStartUnit(
                replay,
                teamId: 0,
                unitId: 0,
                unit => unit with { DefaultFormId = "child-mobile" }),
            "deployment default form");
        AssertInvalid(
            WithTickStartUnit(
                replay,
                teamId: 0,
                unitId: 0,
                unit => unit with
                {
                    ActiveLife = unit.ActiveLife! with
                    {
                        Health = unit.ActiveLife.Health - 1,
                    },
                }),
            "exact initial-life topology");
    }

    [Fact]
    public void Validation_RejectsFinalPostStateDefaultFormDrift()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        ReplayV2 mutated = WithTick(replay, tick => tick with
        {
            PostState = tick.PostState with
            {
                Teams = tick.PostState.Teams
                    .Select(team => team.TeamId != 0
                        ? team
                        : team with
                        {
                            Units = team.Units
                                .Select(unit => unit.UnitId != 0
                                    ? unit
                                    : unit with
                                    {
                                        DefaultFormId = "child-mobile",
                                    })
                                .ToImmutableArray(),
                        })
                    .ToImmutableArray(),
            },
        });
        mutated = mutated with
        {
            Result = mutated.Result with
            {
                Teams = mutated.Result.Teams
                    .Select(team => team.TeamId != 0
                        ? team
                        : team with
                        {
                            Units = team.Units
                                .Select(unit => unit.UnitId != 0
                                    ? unit
                                    : unit with
                                    {
                                        DefaultFormId = "child-mobile",
                                    })
                                .ToImmutableArray(),
                        })
                    .ToImmutableArray(),
            },
        };

        AssertInvalid(mutated, "deployment default form");
    }

    [Fact]
    public void Validation_RejectsHeaderContractMismatches()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);

        AssertInvalid(
            replay with
            {
                Header = replay.Header with
                {
                    GameRulesVersion = "not-the-contract-ruleset",
                },
            },
            "gameRulesVersion");
        PublicMatchContractManifest invalidContract =
            replay.Header.Contract with
            {
                MatchContractFingerprint = new string('0', 64),
            };
        AssertInvalid(
            replay with
            {
                Header = replay.Header with
                {
                    Contract = invalidContract,
                },
            },
            "fingerprint");
        AssertInvalid(
            replay with
            {
                Header = replay.Header with
                {
                    ActorRuntime = replay.Header.ActorRuntime with
                    {
                        ObservationSchemaVersion =
                            replay.Header.ActorRuntime
                                .ObservationSchemaVersion + 1,
                    },
                },
            },
            "actorRuntime");
    }

    [Fact]
    public void Validation_RejectsObservationSelfOrResolutionDrift()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);

        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                Observation = actor.Observation with
                {
                    Self = actor.Observation.Self with
                    {
                        Health = actor.Observation.Self.Health + 1,
                    },
                },
            }),
            "observation self");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                ActionResolution = actor.ActionResolution with
                {
                    ChosenActionId = "future-flight",
                },
            }),
            "action ID/code");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                ActionResolution = actor.ActionResolution with
                {
                    ValidatedActionCode = (int)BotAction.MoveForward,
                },
            }),
            "action ID/code");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                LifeStart = null,
            }),
            "first turn");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                LifeStart = actor.LifeStart! with
                {
                    ActorRandomSeed = "01",
                },
            }),
            "lifeStart");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                AcceptedDecision = actor.AcceptedDecision with
                {
                    Payload = new ReplayV2ActionPayload(
                        ShotProgram: null,
                        Direction: null,
                        UnitTarget: null,
                        FormTargetId: null),
                },
            }),
            "empty action payload");
    }

    [Fact]
    public void Validation_RejectsObservationActionCatalogDrift()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        ReplayV2ActorTurn first = replay.Ticks[0].Actors
            .OrderBy(actor => actor.ActorId)
            .First();
        ReplayV2ObservedActionAvailability wait =
            first.Observation.Actions.Single(
                action => action.ActionId == PublicActionIds.Wait);
        ReplayV2ObservedActionAvailability shoot =
            first.Observation.Actions.Single(
                action => action.ActionId == PublicActionIds.Shoot);

        AssertInvalid(
            WithFirstObservationActions(
                replay,
                first.Observation.Actions.Add(wait)),
            "action IDs");
        AssertInvalid(
            WithFirstObservationActions(
                replay,
                first.Observation.Actions
                    .Where(action =>
                        action.ActionId != PublicActionIds.Wait)
                    .ToImmutableArray()),
            "complete contract catalog");
        AssertInvalid(
            WithFirstObservationActions(
                replay,
                first.Observation.Actions
                    .Select(action =>
                        action.ActionId == PublicActionIds.Wait
                            ? action with { ActionId = "future-flight" }
                            : action)
                    .ToImmutableArray()),
            "action ID/code");
        AssertInvalid(
            WithFirstObservationActions(
                replay,
                ReplaceAction(
                    first.Observation.Actions,
                    shoot with { ParameterKinds = default })),
            "must be initialized");
        AssertInvalid(
            WithFirstObservationActions(
                replay,
                ReplaceAction(
                    first.Observation.Actions,
                    shoot with
                    {
                        ParameterKinds =
                        [
                            (PublicActionParameterKind)99,
                        ],
                    })),
            "known, unique, and canonical");
        AssertInvalid(
            WithFirstObservationActions(
                replay,
                ReplaceAction(
                    first.Observation.Actions,
                    shoot with
                    {
                        ParameterKinds =
                        [
                            PublicActionParameterKind.ShotProgram,
                            PublicActionParameterKind.ShotProgram,
                        ],
                    })),
            "known, unique, and canonical");
        AssertInvalid(
            WithFirstObservationActions(
                replay,
                ReplaceAction(
                    first.Observation.Actions,
                    shoot with
                    {
                        ParameterKinds =
                        [
                            PublicActionParameterKind.Direction,
                            PublicActionParameterKind.ShotProgram,
                        ],
                    })),
            "known, unique, and canonical");
    }

    [Fact]
    public void Validation_RequiresExactAudienceAliasJoins()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);
        ReplayV2ActorTurn first = replay.Ticks[0].Actors
            .OrderBy(actor => actor.ActorId)
            .First();
        ReplayV2EnemyLifeAlias enemyAlias =
            Assert.Single(first.Aliases.EnemyLives);
        ReplayV2ObservedEnemy enemy =
            Assert.Single(first.Observation.Enemies);

        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                Aliases = actor.Aliases with { EnemyLives = [] },
            }),
            "does not resolve");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                Aliases = actor.Aliases with
                {
                    EnemyLives =
                    [
                        enemyAlias with
                        {
                            LifeHandle = "enemy-life-1",
                        },
                    ],
                },
            }),
            "densely");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                Aliases = actor.Aliases with
                {
                    EnemyLives =
                    [
                        enemyAlias with
                        {
                            ActorId = actor.ActorId,
                        },
                    ],
                },
            }),
            "authoritative");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                Observation = actor.Observation with
                {
                    Enemies =
                    [
                        enemy with
                        {
                            Actor = enemy.Actor with
                            {
                                LifeHandle = "enemy-life-99",
                            },
                        },
                    ],
                },
            }),
            "does not resolve");
        AssertInvalid(
            WithFirstActor(replay, actor => actor with
            {
                Aliases = actor.Aliases with
                {
                    Events =
                    [
                        new ReplayV2EventAlias(
                            "event-0",
                            "lifecycle:0:0"),
                    ],
                },
            }),
            "does not resolve");
    }

    [Fact]
    public void Validation_RejectsTerminalResultDrift()
    {
        ReplayV2 replay = CreateReplay(reverseInsertionOrder: false);

        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    EndTick = replay.Result.EndTick + 1,
                },
            },
            "endTick");
        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    Control = replay.Result.Control with
                    {
                        CaptureProgress =
                            replay.Result.Control.CaptureProgress + 1,
                    },
                },
            },
            "final post-state objective");
        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    Teams = replay.Result.Teams
                        .Select((team, index) => index == 0
                            ? team with { TeamId = 99 }
                            : team)
                        .ToImmutableArray(),
                },
            },
            "team IDs");
        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    TerritorialScore = "1",
                },
            },
            "territorialScore");
        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    WinnerTeamId = 0,
                },
            },
            "winnerTeamId");
        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    Teams = replay.Result.Teams
                        .Select((team, index) => index == 0
                            ? team with
                            {
                                Outcome = FrontlineTeamOutcome.Win,
                            }
                            : team)
                        .ToImmutableArray(),
                },
            },
            "outcomes");
        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    Reason = FrontlineMatchEndReason.BaseBreach,
                },
            },
            "reason");
        PublicFrontlineDefinition frontline =
            replay.Header.Contract.Rules.Frontline!;
        PublicRulesManifest invalidAdvanceRules =
            replay.Header.Contract.Rules with
            {
                Frontline = frontline with
                {
                    Victory = frontline.Victory with
                    {
                        TeamAdvances =
                        [
                            new PublicFrontlineTeamAdvance(0, 1),
                            new PublicFrontlineTeamAdvance(1, 1),
                        ],
                    },
                },
            };
        AssertInvalid(
            replay with
            {
                Header = replay.Header with
                {
                    Contract = replay.Header.Contract with
                    {
                        Rules = invalidAdvanceRules,
                    },
                },
            },
            "team advances");
    }

    [Fact]
    public void Validation_RecomputesBaseBreachTerminalSemantics()
    {
        ReplayV2 replay = AsTeamZeroBaseBreach(
            CreateReplay(reverseInsertionOrder: false));

        _ = ReplayV2Serializer.ComputeHash(replay);

        AssertInvalid(
            WithTick(replay, tick => tick with
            {
                Resolution = tick.Resolution with
                {
                    Events = tick.Resolution.Events
                        .Where(value =>
                            value.Type
                                != FrontlineMatchEventType.BaseBreached)
                        .ToImmutableArray(),
                },
            }),
            "breach event");
        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    Reason = FrontlineMatchEndReason.MaxTicks,
                },
            },
            "precedence");
        AssertInvalid(
            replay with
            {
                Result = replay.Result with
                {
                    WinnerTeamId = 1,
                },
            },
            "winnerTeamId");
    }

    private static void AssertInvalid(
        ReplayV2 replay,
        string expectedMessage)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            ReplayV2Serializer.ComputeHash(replay));
        Assert.Contains(
            expectedMessage,
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static ReplayV2 WithTick(
        ReplayV2 replay,
        Func<ReplayV2Tick, ReplayV2Tick> replace)
    {
        ReplayV2Tick tick = Assert.Single(replay.Ticks);
        return replay with { Ticks = [replace(tick)] };
    }

    private static ReplayV2 WithTickStartUnit(
        ReplayV2 replay,
        int teamId,
        int unitId,
        Func<ReplayV2UnitState, ReplayV2UnitState> replace) =>
        WithTick(replay, tick => tick with
        {
            TickStart = tick.TickStart with
            {
                State = tick.TickStart.State with
                {
                    Teams = tick.TickStart.State.Teams
                        .Select(team => team.TeamId != teamId
                            ? team
                            : team with
                            {
                                Units = team.Units
                                    .Select(unit => unit.UnitId != unitId
                                        ? unit
                                        : replace(unit))
                                    .ToImmutableArray(),
                            })
                        .ToImmutableArray(),
                },
            },
        });

    private static ReplayV2 AsTeamZeroBaseBreach(ReplayV2 replay)
    {
        ReplayV2Tick tick = Assert.Single(replay.Ticks);
        PublicFrontlineDefinition frontline =
            replay.Header.Contract.Rules.Frontline!;
        int breachedPosition = frontline.FrontlinePositionCount - 1;
        var control = tick.PostState.Control with
        {
            ActivePositionIndex = breachedPosition,
            ClaimingTeamId = null,
            CaptureProgress = 0,
            DecayTicksElapsed = 0,
            WinnerTeamId = 0,
        };
        ReplayV2Event breach = ReplayV2Projection.Event(
            "resolution:0:base-breach",
            new FrontlineMatchEvent
            {
                Tick = tick.Tick,
                Type = FrontlineMatchEventType.BaseBreached,
                TeamId = 0,
                FromPositionIndex = breachedPosition,
                ToPositionIndex = breachedPosition,
                ClaimingTeamId = null,
                CaptureProgress = 0,
            });
        return replay with
        {
            Ticks =
            [
                tick with
                {
                    Resolution = tick.Resolution with
                    {
                        Events = tick.Resolution.Events.Add(breach),
                    },
                    PostState = tick.PostState with
                    {
                        Control = control,
                    },
                },
            ],
            Result = replay.Result with
            {
                WinnerTeamId = 0,
                Reason = FrontlineMatchEndReason.BaseBreach,
                TerritorialScore =
                    frontline.Capture.Threshold.ToString(
                        CultureInfo.InvariantCulture),
                Control = control,
                Teams = replay.Result.Teams
                    .Select(team => team with
                    {
                        Outcome = team.TeamId == 0
                            ? FrontlineTeamOutcome.Win
                            : FrontlineTeamOutcome.Loss,
                    })
                    .ToImmutableArray(),
            },
        };
    }

    private static ReplayV2WorldState WithDamageDealt(
        ReplayV2WorldState state,
        string damageDealt) =>
        state with
        {
            Teams = state.Teams
                .Select(team => team with
                {
                    DamageDealt = damageDealt,
                    Units = team.Units
                        .Select(unit => unit with
                        {
                            DamageDealt = damageDealt,
                            ActiveLife = unit.ActiveLife is { } life
                                ? life with
                                {
                                    DamageDealt = damageDealt,
                                }
                                : null,
                        })
                        .ToImmutableArray(),
                })
                .ToImmutableArray(),
        };

    private static ReplayV2 WithFirstActor(
        ReplayV2 replay,
        Func<ReplayV2ActorTurn, ReplayV2ActorTurn> replace) =>
        WithTick(replay, tick =>
        {
            ReplayV2ActorId firstId = tick.Actors
                .Select(actor => actor.ActorId)
                .Order()
                .First();
            return tick with
            {
                Actors = tick.Actors
                    .Select(actor => actor.ActorId == firstId
                        ? replace(actor)
                        : actor)
                    .ToImmutableArray(),
            };
        });

    private static ReplayV2 WithFirstObservationActions(
        ReplayV2 replay,
        ImmutableArray<ReplayV2ObservedActionAvailability> actions) =>
        WithFirstActor(replay, actor => actor with
        {
            Observation = actor.Observation with { Actions = actions },
        });

    private static ImmutableArray<ReplayV2ObservedActionAvailability>
        ReplaceAction(
            ImmutableArray<ReplayV2ObservedActionAvailability> actions,
            ReplayV2ObservedActionAvailability replacement) =>
        actions
            .Select(action =>
                action.ActionId == replacement.ActionId
                    ? replacement
                    : action)
            .ToImmutableArray();

    private static IEnumerable<JsonElement> FindProperties(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.Ordinal))
                {
                    yield return property.Value;
                }
                foreach (JsonElement nested in FindProperties(
                             property.Value,
                             propertyName))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach (JsonElement nested in FindProperties(
                             item,
                             propertyName))
                {
                    yield return nested;
                }
            }
        }
    }

    private static ReplayV2 CreateReplay(bool reverseInsertionOrder)
    {
        GameRules rules = FrontlineTestDefinitions.PrimeOnlyRules(maxTicks: 1);
        ArenaMap map = FrontlineTestDefinitions.OpenMapV2();
        ResolvedMatchDefinition definition =
            MatchDefinitionResolver.Resolve(rules, map);
        PublicMatchContractManifest contract =
            PublicRulesManifestFactory.CreateMatchContract(rules, map);
        var session = new FrontlineMatchSession(definition);
        FrontlineTickStart prepared = session.PrepareTick();
        FrontlineActorId[] actorIds = prepared.ActiveActors.Order().ToArray();

        ReplayV2TickStart tickStart = ReplayV2Projection.TickStart(
            prepared,
            session.State);
        tickStart = tickStart with
        {
            State = AddWireIdProjectiles(
                tickStart.State,
                actorIds[0],
                reverseInsertionOrder),
            ActiveActors = MaybeReverse(
                tickStart.ActiveActors,
                reverseInsertionOrder),
        };

        Dictionary<FrontlineActorId, BotDecision> decisions = actorIds
            .ToDictionary(
                actorId => actorId,
                _ => BotDecision.Of(BotAction.Wait));
        FrontlineStepResult step = session.Step(decisions);
        var damage = new FrontlineMatchEvent
        {
            Tick = step.Tick,
            Type = FrontlineMatchEventType.Damage,
            TeamId = actorIds[1].TeamId,
            ActorId = actorIds[1],
            OtherActorId = actorIds[0],
            ProjectileId = ExactJsUnsafeProjectileId,
            From = session.State.GetActiveLife(actorIds[1]).Position,
            To = session.State.GetActiveLife(actorIds[1]).Position,
            Amount = 1,
            NewHealth =
                session.State.GetActiveLife(actorIds[1]).Health - 1,
        };
        ReplayV2AuthoritativeResolution resolution =
            ReplayV2Projection.Resolution(step with { Events = [damage] });
        ReplayV2WorldState postState = AddWireIdProjectiles(
            ReplayV2Projection.WorldState(session.State),
            actorIds[0],
            reverseInsertionOrder);
        postState = postState with
        {
            Teams = postState.Teams
                .Select(team => team with
                {
                    DamageDealt = team.TeamId == actorIds[0].TeamId
                        ? "1"
                        : team.DamageDealt,
                    Units = team.Units
                        .Select(unit =>
                        {
                            if (unit.TeamId == actorIds[0].TeamId
                                && unit.UnitId == actorIds[0].UnitId)
                            {
                                return unit with
                                {
                                    DamageDealt = "1",
                                    ActiveLife = unit.ActiveLife! with
                                    {
                                        DamageDealt = "1",
                                    },
                                };
                            }
                            if (unit.TeamId == actorIds[1].TeamId
                                && unit.UnitId == actorIds[1].UnitId)
                            {
                                return unit with
                                {
                                    ActiveLife = unit.ActiveLife! with
                                    {
                                        Health =
                                            unit.ActiveLife.Health - 1,
                                    },
                                };
                            }
                            return unit;
                        })
                        .ToImmutableArray(),
                })
                .ToImmutableArray(),
        };

        IEnumerable<FrontlineActorId> actorOrder = reverseInsertionOrder
            ? actorIds.Reverse()
            : actorIds;
        ImmutableArray<ReplayV2ActorTurn> turns = actorOrder
            .Select(actorId =>
            {
                ActorObservation observation = CreateObservation(
                    actorId,
                    actorIds.Single(value => value.TeamId != actorId.TeamId),
                    tickStart.State,
                    contract,
                    reverseInsertionOrder);
                ActorDecision submitted = actorId.TeamId == 0
                    ? ActorDecision.Of(
                        "future-flight",
                        9_007,
                        new ActorActionPayload
                        {
                            Direction = Direction.North,
                            FormTargetId = "flight",
                        },
                        debug: "raw reply")
                    : ActorDecision.Wait();
                return new ReplayV2ActorTurn(
                    new ReplayV2ActorId(
                        actorId.TeamId,
                        actorId.UnitId,
                        actorId.LifeId),
                    new ReplayV2LifeStart(
                        BotArenaVersions.ActorMatchStartSchemaVersion,
                        BotArenaVersions.ActorRuntimeContractVersion,
                        new ReplayV2ActorId(
                            actorId.TeamId,
                            actorId.UnitId,
                            actorId.LifeId),
                        actorId.TeamId,
                        actorId.TeamId.ToString(
                            CultureInfo.InvariantCulture),
                        ActorSpawnReason.Initial,
                        contract.MatchContractFingerprint),
                    ReplayV2Projection.Observation(observation),
                    ReplayV2Projection.ObservationAliases(
                        new ActorObservationReplayAliases(
                            ActorIdentity.FromFrontline(actorId),
                            [
                                new ActorObservationEnemyLifeAlias(
                                    "enemy-life-0",
                                    ActorIdentity.FromFrontline(
                                        actorIds.Single(value =>
                                            value.TeamId
                                            != actorId.TeamId))),
                            ],
                            [],
                            [])),
                    ReplayV2Projection.Decision(submitted),
                    ReplayV2Projection.Decision(ActorDecision.Wait()),
                    ReplayV2Projection.ActionResolution(
                        step.ActionResolutions.Single(
                            value => value.ActorId == actorId)));
            })
            .ToImmutableArray();

        ReplayV2Result result = ReplayV2Projection.Result(step.Result!);
        result = result with
        {
            Teams = result.Teams
                .Select(team => team with
                {
                    ActiveHealth = team.TeamId == actorIds[1].TeamId
                        ? team.ActiveHealth - 1
                        : team.ActiveHealth,
                    DamageDealt = team.TeamId == actorIds[0].TeamId
                        ? "1"
                        : team.DamageDealt,
                    Units = team.Units
                        .Select(unit => unit with
                        {
                            Health =
                                unit.TeamId == actorIds[1].TeamId
                                && unit.UnitId == actorIds[1].UnitId
                                    ? unit.Health - 1
                                    : unit.Health,
                            DamageDealt =
                                unit.TeamId == actorIds[0].TeamId
                                && unit.UnitId == actorIds[0].UnitId
                                    ? "1"
                                    : unit.DamageDealt,
                        })
                        .ToImmutableArray(),
                })
                .ToImmutableArray(),
        };
        if (reverseInsertionOrder)
            result = result with { Teams = result.Teams.Reverse().ToImmutableArray() };

        ReplayV2Header header = ReplayV2Projection.Header(
            ExactJsUnsafeSeed,
            contract,
            "test-theme",
            CreatePresentation(reverseInsertionOrder),
            ParticipantConfigurations(reverseInsertionOrder));
        return new ReplayV2(
            header,
            [new ReplayV2Tick(
                step.Tick,
                tickStart,
                turns,
                resolution,
                postState)],
            result);
    }

    private static ActorObservation CreateObservation(
        FrontlineActorId actorId,
        FrontlineActorId enemyId,
        ReplayV2WorldState preparedState,
        PublicMatchContractManifest contract,
        bool reverseInsertionOrder)
    {
        ReplayV2UnitState selfUnit = FindUnit(preparedState, actorId);
        ReplayV2UnitState enemyUnit = FindUnit(preparedState, enemyId);
        ReplayV2LifeState self = selfUnit.ActiveLife!;
        ReplayV2LifeState enemy = enemyUnit.ActiveLife!;
        var observer = ActorIdentity.FromFrontline(actorId);
        ImmutableArray<ObservedMapTile> tiles =
        [
            new(
                new Position(2, 2),
                IsWall: false,
                [observer]),
            new(
                new Position(1, 1),
                IsWall: false,
                [observer]),
        ];
        ImmutableArray<ObservedActionAvailability> actions =
            contract.Rules.Actions
                .Select(action =>
                {
                    bool isTransform = string.Equals(
                        action.Id,
                        PublicActionIds.Transform,
                        StringComparison.Ordinal);
                    bool isDirectionalShot = string.Equals(
                        action.Id,
                        PublicActionIds.ShootDirection,
                        StringComparison.Ordinal);
                    return new ObservedActionAvailability(
                        action.Id,
                        action.Code,
                        action.ParameterKinds,
                        action.Enabled,
                        action.Enabled
                            && !isTransform
                            && !isDirectionalShot,
                        action.ParameterKinds.Contains(
                            PublicActionParameterKind.ShotProgram),
                        AllowedDirections: null,
                        AllowedUnitTargets: null,
                        AllowedFormTargets: isTransform ? [] : null)
                    {
                        AllowedProjectileHeadings =
                            isDirectionalShot ? [] : null,
                    };
                })
                .ToImmutableArray();
        if (reverseInsertionOrder)
        {
            tiles = tiles.Reverse().ToImmutableArray();
            actions = actions.Reverse().ToImmutableArray();
        }

        return new ActorObservation
        {
            SchemaVersion = BotArenaVersions.ActorObservationSchemaVersion,
            Tick = 0,
            MatchContractFingerprint = contract.MatchContractFingerprint,
            TeamPerception = TeamPerceptionMode.ImmediateUnion,
            Self = new ObservedSelf(
                observer,
                self.FormId,
                self.Position,
                self.Facing,
                self.Health,
                self.Cooldown,
                self.Energy,
                self.PreviousActionResult),
            TeamUnits =
            [
                new ObservedUnitSlot(
                    actorId.TeamId,
                    actorId.UnitId,
                    self.FormId,
                    FrontlineLifecycleStatus.Active,
                    observer,
                    RespawnAtTick: null),
            ],
            Allies = [],
            Enemies =
            [
                new ObservedEnemy(
                    new ObservedEnemyActorRef(
                        enemyId.TeamId,
                        enemyId.UnitId,
                        "enemy-life-0"),
                    enemy.FormId,
                    enemy.Position,
                    enemy.Facing,
                    enemy.Health,
                    [observer]),
            ],
            VisibleTiles = tiles,
            VisibleProjectiles = actorId.TeamId == 0 ? null : [],
            VisibleEvents = [],
            HeardSounds = actorId.TeamId == 0 ? null : [],
            FrontlineObjective = new ObservedFrontlineObjective(
                preparedState.Control.ActivePositionIndex,
                preparedState.Control.ClaimingTeamId,
                preparedState.Control.CaptureProgress,
                preparedState.Control.DecayTicksElapsed,
                preparedState.Control.ControlResumesAtTick),
            Actions = actions,
        };
    }

    private static ReplayV2LifeState FindLife(
        ReplayV2WorldState state,
        FrontlineActorId actorId) =>
        FindUnit(state, actorId).ActiveLife!;

    private static ReplayV2UnitState FindUnit(
        ReplayV2WorldState state,
        FrontlineActorId actorId) =>
        state.Teams
            .Single(team => team.TeamId == actorId.TeamId)
            .Units
            .Single(unit => unit.UnitId == actorId.UnitId);

    private static ReplayV2WorldState AddWireIdProjectiles(
        ReplayV2WorldState state,
        FrontlineActorId owner,
        bool reverseInsertionOrder)
    {
        var replayOwner = new ReplayV2ActorId(
            owner.TeamId,
            owner.UnitId,
            owner.LifeId);
        ImmutableArray<ReplayV2ProjectileState> projectiles =
        [
            Projectile("10", replayOwner),
            Projectile("2", replayOwner),
            Projectile(ExactJsUnsafeProjectileId.ToString(), replayOwner),
        ];
        if (reverseInsertionOrder)
            projectiles = projectiles.Reverse().ToImmutableArray();
        ImmutableArray<ReplayV2TeamState> teams = reverseInsertionOrder
            ? state.Teams
                .Reverse()
                .Select(team => team with
                {
                    Units = team.Units.Reverse().ToImmutableArray(),
                })
                .ToImmutableArray()
            : state.Teams;
        return state with { Teams = teams, Projectiles = projectiles };
    }

    private static ReplayV2ProjectileState Projectile(
        string id,
        ReplayV2ActorId owner) =>
        new(
            id,
            owner,
            new Position(3, 2),
            Direction.East,
            ProjectileHeading.East,
            ShotProgram.Straight,
            [new Position(4, 2), new Position(5, 2)],
            NextProgrammedPathIndex: 0,
            TilesTraveled: 1,
            Phase: 0);

    private static ImmutableArray<T> MaybeReverse<T>(
        ImmutableArray<T> values,
        bool reverse) =>
        reverse ? values.Reverse().ToImmutableArray() : values;

    private static MapPresentation CreatePresentation(bool reverse)
    {
        MapWallGroup[] groups =
        [
            new("zeta", [new Position(2, 1), new Position(1, 1)]),
            new("alpha", [new Position(4, 1), new Position(3, 1)]),
        ];
        return new MapPresentation(
            "boundary",
            "interior",
            reverse ? groups.Reverse().ToArray() : groups);
    }

    private static IEnumerable<ActorParticipantConfiguration>
        ParticipantConfigurations(bool reverse)
    {
        ActorParticipantConfiguration[] participants =
        [
            Participant(0, 0, "alpha"),
            Participant(1, 1, "beta"),
        ];
        return reverse ? participants.Reverse() : participants;
    }

    private static ActorParticipantConfiguration Participant(
        int participantId,
        int teamId,
        string name) =>
        new()
        {
            ParticipantId = participantId,
            TeamId = teamId,
            Name = name,
            RuntimeFactory = new UnusedActorRuntimeFactory(),
            RuntimeKind = "test",
            ArtifactHash = $"hash-{name}",
            Accent = teamId == 0 ? "#000001" : "#000002",
            LookId = $"look-{name}",
            ProjectileLookId = $"projectile-{name}",
        };

    private sealed class UnusedActorRuntimeFactory : IActorRuntimeFactory
    {
        public IActorRuntime CreateRuntime() =>
            throw new InvalidOperationException("Test factory is metadata-only.");
    }
}
