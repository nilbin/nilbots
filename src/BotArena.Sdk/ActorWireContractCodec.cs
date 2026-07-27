using System.Collections.Immutable;

namespace BotArena.Sdk;

/// <summary>Tagged binary codec for MatchStart and its immutable contract.</summary>
internal static class ActorWireContractCodec
{
    public static byte[] EncodeMatchStart(ActorMatchStart value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(2, ActorWireValue.Int32(value.RuntimeContractVersion));
        writer.Field(3, EncodeIdentity(value.ActorId));
        writer.Field(4, ActorWireValue.Int32(value.ParticipantId));
        writer.Field(5, ActorWireValue.UInt64(value.ActorRandomSeed));
        writer.Field(6, ActorWireValue.Enum(value.SpawnReason));
        writer.Field(7, EncodeContract(value.Contract));
        return writer.ToArray();
    }

    public static ActorMatchStart DecodeMatchStart(byte[] bytes, int depth = 0)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ActorMatchStart
        {
            SchemaVersion = Int(reader, 1),
            RuntimeContractVersion = Int(reader, 2),
            ActorId = DecodeIdentity(reader.Required(3), depth + 1),
            ParticipantId = Int(reader, 4),
            ActorRandomSeed = ActorWireValue.UInt64(reader.Required(5)),
            SpawnReason = ActorWireValue.Enum<ActorSpawnReason>(
                reader.Required(6)),
            Contract = DecodeContract(reader.Required(7), depth + 1),
        };
    }

    internal static byte[] EncodeIdentity(ActorIdentity value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, ActorWireValue.Int32(value.UnitId));
        writer.Field(3, ActorWireValue.Int32(value.LifeId));
        return writer.ToArray();
    }

    internal static ActorIdentity DecodeIdentity(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ActorIdentity(Int(reader, 1), Int(reader, 2), Int(reader, 3));
    }

    internal static byte[] EncodePosition(Position value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.X));
        writer.Field(2, ActorWireValue.Int32(value.Y));
        return writer.ToArray();
    }

    internal static Position DecodePosition(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new Position(Int(reader, 1), Int(reader, 2));
    }

    internal static byte[] EncodeShotProgram(ShotProgram value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.InitialAimOffset));
        writer.Field(2, ActorWireValue.Int32(value.BendDirection));
        writer.Field(3, ActorWireValue.Int32(value.BendAfterTiles));
        writer.Field(4, ActorWireValue.Int32(value.BendEveryTiles));
        writer.Field(5, ActorWireValue.Int32(value.BendCount));
        return writer.ToArray();
    }

    internal static ShotProgram DecodeShotProgram(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new ShotProgram(
            Int(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5));
    }

    private static byte[] EncodeContract(PublicMatchContractManifest value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(2, Id(value.MatchContractFingerprint, 64));
        writer.Field(3, EncodeRules(value.Rules));
        writer.Field(4, EncodeMap(value.Map));
        writer.Field(5, EncodeTopology(value.Topology));
        return writer.ToArray();
    }

    private static PublicMatchContractManifest DecodeContract(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicMatchContractManifest
        {
            SchemaVersion = Int(reader, 1),
            MatchContractFingerprint = Text(reader, 2, 64),
            Rules = DecodeRules(reader.Required(3), depth + 1),
            Map = DecodeMap(reader.Required(4), depth + 1),
            Topology = DecodeTopology(reader.Required(5), depth + 1),
        };
    }

    private static byte[] EncodeTopology(PublicMatchTopology value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, Array(value.Teams, team =>
        {
            var item = new ActorWireObjectWriter();
            item.Field(1, ActorWireValue.Int32(team.TeamId));
            return item.ToArray();
        }));
        writer.Field(2, Array(value.Participants, participant =>
        {
            var item = new ActorWireObjectWriter();
            item.Field(1, ActorWireValue.Int32(participant.ParticipantId));
            item.Field(2, ActorWireValue.Int32(participant.TeamId));
            return item.ToArray();
        }));
        writer.Field(3, Array(value.UnitSlots, unit =>
        {
            var item = new ActorWireObjectWriter();
            item.Field(1, ActorWireValue.Int32(unit.TeamId));
            item.Field(2, ActorWireValue.Int32(unit.UnitId));
            item.Field(3, ActorWireValue.Int32(unit.ControllerParticipantId));
            return item.ToArray();
        }));
        writer.Field(4, Array(value.InitialLives, life =>
        {
            var item = new ActorWireObjectWriter();
            item.Field(1, ActorWireValue.Int32(life.TeamId));
            item.Field(2, ActorWireValue.Int32(life.UnitId));
            item.Field(3, ActorWireValue.Int32(life.LifeId));
            item.Field(4, SemanticId(life.FormId));
            return item.ToArray();
        }));
        return writer.ToArray();
    }

    private static PublicMatchTopology DecodeTopology(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicMatchTopology
        {
            Teams = DecodeArray(reader, 1, itemBytes =>
            {
                var item = new ActorWireObjectReader(itemBytes, depth + 1);
                return new PublicScoringTeam(Int(item, 1));
            }),
            Participants = DecodeArray(reader, 2, itemBytes =>
            {
                var item = new ActorWireObjectReader(itemBytes, depth + 1);
                return new PublicParticipant(Int(item, 1), Int(item, 2));
            }),
            UnitSlots = DecodeArray(reader, 3, itemBytes =>
            {
                var item = new ActorWireObjectReader(itemBytes, depth + 1);
                return new PublicUnitSlot(
                    Int(item, 1),
                    Int(item, 2),
                    Int(item, 3));
            }),
            InitialLives = DecodeArray(reader, 4, itemBytes =>
            {
                var item = new ActorWireObjectReader(itemBytes, depth + 1);
                return new PublicInitialLife(
                    Int(item, 1),
                    Int(item, 2),
                    Int(item, 3),
                    SemanticText(item, 4));
            }),
        };
    }

    private static byte[] EncodeMap(PublicMapManifest value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(2, Id(value.MapId));
        writer.Field(3, ActorWireValue.Int32(value.MapVersion));
        writer.Field(4, Id(value.MapFingerprint, 64));
        writer.Field(5, ActorWireValue.Int32(value.FormatVersion));
        writer.Field(6, ActorWireValue.Int32(value.Width));
        writer.Field(7, ActorWireValue.Int32(value.Height));
        writer.Field(8, Array(value.TileRows, row =>
            ActorWireValue.String(row, 4096)));
        writer.Field(9, Array(value.Spawns, EncodeMapSpawn));
        writer.Field(10, Array(value.ObjectiveTiles, EncodePosition));
        writer.Optional(
            11,
            value.Frontline is { } frontline
                ? EncodeFrontlineMap(frontline)
                : null);
        return writer.ToArray();
    }

    private static PublicMapManifest DecodeMap(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? frontline = reader.Optional(11);
        return new PublicMapManifest
        {
            SchemaVersion = Int(reader, 1),
            MapId = Text(reader, 2),
            MapVersion = Int(reader, 3),
            MapFingerprint = Text(reader, 4, 64),
            FormatVersion = Int(reader, 5),
            Width = Int(reader, 6),
            Height = Int(reader, 7),
            TileRows = DecodeArray(reader, 8, row =>
                ActorWireValue.String(row, 4096)),
            Spawns = DecodeArray(reader, 9, item =>
                DecodeMapSpawn(item, depth + 1)),
            ObjectiveTiles = DecodeArray(reader, 10, item =>
                DecodePosition(item, depth + 1)),
            Frontline = frontline is null
                ? null
                : DecodeFrontlineMap(frontline, depth + 1),
        };
    }

    private static byte[] EncodeMapSpawn(PublicMapSpawn value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamId));
        writer.Field(2, EncodePosition(value.Position));
        writer.Field(3, ActorWireValue.Enum(value.Facing));
        return writer.ToArray();
    }

    private static PublicMapSpawn DecodeMapSpawn(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicMapSpawn(
            Int(reader, 1),
            DecodePosition(reader.Required(2), depth + 1),
            ActorWireValue.Enum<Direction>(reader.Required(3)));
    }

    private static byte[] EncodeFrontlineMap(
        PublicFrontlineMapDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, Array(value.Positions, position =>
        {
            var item = new ActorWireObjectWriter();
            item.Field(
                1,
                ActorWireValue.Int32(position.PositionIndex));
            item.Field(2, Array(position.Tiles, EncodePosition));
            return item.ToArray();
        }));
        writer.Field(2, Array(value.TeamHomes, home =>
        {
            var item = new ActorWireObjectWriter();
            item.Field(1, ActorWireValue.Int32(home.TeamId));
            item.Field(2, EncodePosition(home.PrimeSpawnPosition));
            item.Field(3, ActorWireValue.Enum(home.PrimeSpawnFacing));
            item.Field(
                4,
                Array(home.ProtectedSpawnPad, EncodePosition));
            return item.ToArray();
        }));
        writer.Field(
            3,
            Array(value.AnchorForbiddenTiles, EncodePosition));
        return writer.ToArray();
    }

    private static PublicFrontlineMapDefinition DecodeFrontlineMap(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineMapDefinition(
            DecodeArray(reader, 1, itemBytes =>
            {
                var item = new ActorWireObjectReader(
                    itemBytes,
                    depth + 1);
                return new PublicFrontlinePosition(
                    Int(item, 1),
                    ActorWireValue.Array(
                        item.Required(2),
                        tile => DecodePosition(tile, depth + 2)));
            }),
            DecodeArray(reader, 2, itemBytes =>
            {
                var item = new ActorWireObjectReader(
                    itemBytes,
                    depth + 1);
                return new PublicFrontlineTeamHome(
                    Int(item, 1),
                    DecodePosition(item.Required(2), depth + 2),
                    ActorWireValue.Enum<Direction>(item.Required(3)),
                    ActorWireValue.Array(
                        item.Required(4),
                        tile => DecodePosition(tile, depth + 2)));
            }),
            DecodeArray(reader, 3, tile =>
                DecodePosition(tile, depth + 1)));
    }

    private static byte[] EncodeRules(PublicRulesManifest value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.SchemaVersion));
        writer.Field(2, Id(value.RulesetId));
        writer.Field(3, Id(value.RulesFingerprint, 64));
        writer.Field(4, EncodeLimits(value.Limits));
        writer.Field(5, EncodeObjective(value.Objective));
        writer.Optional(
            6,
            value.Frontline is { } frontline
                ? EncodeFrontline(frontline)
                : null);
        writer.Field(7, EncodeEnergy(value.Energy));
        writer.Field(8, Array(value.Forms, EncodeForm));
        writer.Field(9, Array(value.Actions, EncodeAction));
        writer.Field(10, EncodeProjectiles(value.Projectiles));
        writer.Field(11, EncodeShotPrograms(value.ShotPrograms));
        writer.Field(12, EncodeVision(value.Vision));
        writer.Field(13, EncodeCollisions(value.Collisions));
        writer.Field(14, EncodeTickResolution(value.TickResolution));
        return writer.ToArray();
    }

    private static PublicRulesManifest DecodeRules(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? frontline = reader.Optional(6);
        return new PublicRulesManifest
        {
            SchemaVersion = Int(reader, 1),
            RulesetId = Text(reader, 2),
            RulesFingerprint = Text(reader, 3, 64),
            Limits = DecodeLimits(reader.Required(4), depth + 1),
            Objective = DecodeObjective(reader.Required(5), depth + 1),
            Frontline = frontline is null
                ? null
                : DecodeFrontline(frontline, depth + 1),
            Energy = DecodeEnergy(reader.Required(7), depth + 1),
            Forms = DecodeArray(reader, 8, item =>
                DecodeForm(item, depth + 1)),
            Actions = DecodeArray(reader, 9, item =>
                DecodeAction(item, depth + 1)),
            Projectiles = DecodeProjectiles(
                reader.Required(10),
                depth + 1),
            ShotPrograms = DecodeShotPrograms(
                reader.Required(11),
                depth + 1),
            Vision = DecodeVision(reader.Required(12), depth + 1),
            Collisions = DecodeCollisions(
                reader.Required(13),
                depth + 1),
            TickResolution = DecodeTickResolution(
                reader.Required(14),
                depth + 1),
        };
    }

    private static byte[] EncodeLimits(PublicMatchLimits value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.MaxTicks));
        writer.Field(2, ActorWireValue.Int32(value.FaultLimit));
        writer.Field(3, ActorWireValue.Int32(value.TeamCount));
        writer.Field(4, ActorWireValue.Int32(value.ParticipantCount));
        writer.Field(5, ActorWireValue.Int32(value.UnitSlotCount));
        writer.Field(6, ActorWireValue.Int32(value.InitialUnitsPerTeam));
        writer.Field(7, ActorWireValue.Int32(value.MaxUnitsPerTeam));
        writer.Field(8, ActorWireValue.Boolean(value.DestructionEndsMatch));
        writer.Field(9, ActorWireValue.Boolean(value.RespawnsEnabled));
        return writer.ToArray();
    }

    private static PublicMatchLimits DecodeLimits(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicMatchLimits(
            Int(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5),
            Int(reader, 6),
            Int(reader, 7),
            Bool(reader, 8),
            Bool(reader, 9));
    }

    private static byte[] EncodeObjective(PublicObjectiveRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Enum(value.Mode));
        writer.Field(2, ActorWireValue.Boolean(value.ZoneControlEnabled));
        writer.Field(3, ActorWireValue.Int32(value.ZoneDominationTicks));
        writer.Field(4, ActorWireValue.Boolean(value.ZoneExclusiveAccrual));
        writer.Field(5, ActorWireValue.Boolean(value.SharedPressureEnabled));
        writer.Field(6, ActorWireValue.Boolean(value.ControlBySoleOccupancy));
        writer.Field(7, ActorWireValue.Int32(value.ControlPressureLimit));
        writer.Field(8, ActorWireValue.Int32(value.ControlPressureGain));
        writer.Field(
            9,
            ActorWireValue.Int32(value.ControlPressureDecayInterval));
        writer.Field(10, EncodeOvertime(value.Overtime));
        writer.Field(
            11,
            Array(value.MaxTickTiebreakers, ActorWireValue.Enum));
        return writer.ToArray();
    }

    private static PublicObjectiveRules DecodeObjective(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicObjectiveRules(
            ActorWireValue.Enum<PublicObjectiveMode>(reader.Required(1)),
            Bool(reader, 2),
            Int(reader, 3),
            Bool(reader, 4),
            Bool(reader, 5),
            Bool(reader, 6),
            Int(reader, 7),
            Int(reader, 8),
            Int(reader, 9),
            DecodeOvertime(reader.Required(10), depth + 1),
            DecodeArray(reader, 11, ActorWireValue.Enum<PublicScoreMetric>));
    }

    private static byte[] EncodeOvertime(PublicObjectiveOvertimeRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.StartTick));
        writer.Field(2, ActorWireValue.Int32(value.PressureLimit));
        writer.Field(3, ActorWireValue.Int32(value.PressureGain));
        writer.Field(4, ActorWireValue.Boolean(value.StopsDecay));
        return writer.ToArray();
    }

    private static PublicObjectiveOvertimeRules DecodeOvertime(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicObjectiveOvertimeRules(
            Int(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Bool(reader, 4));
    }

    private static byte[] EncodeEnergy(PublicEnergyRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Boolean(value.Enabled));
        writer.Field(2, ActorWireValue.Int32(value.MaxEnergy));
        writer.Field(3, ActorWireValue.Int32(value.ShotEnergyCost));
        writer.Field(
            4,
            ActorWireValue.Int32(value.RegenerationIntervalTicks));
        writer.Field(5, ActorWireValue.Int32(value.RegenerationAmount));
        return writer.ToArray();
    }

    private static PublicEnergyRules DecodeEnergy(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicEnergyRules(
            Bool(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5));
    }

    private static byte[] EncodeForm(PublicFormDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, SemanticId(value.Id));
        writer.Field(2, ActorWireValue.Int32(value.MaxHealth));
        writer.Field(3, ActorWireValue.Int32(value.VisionRange));
        writer.Field(4, ActorWireValue.Int32(value.ShootCooldownTicks));
        writer.Field(5, ActorWireValue.Boolean(value.OmnidirectionalVision));
        writer.Field(
            6,
            ActorWireValue.Boolean(value.OmnidirectionalShooting));
        writer.Field(7, ActorWireValue.Enum(value.MovementLayer));
        writer.Field(8, ActorWireValue.Int32(value.ObjectiveWeight));
        writer.Field(9, ActorWireValue.Boolean(value.CanMove));
        writer.Field(10, ActorWireValue.Boolean(value.CanShoot));
        writer.Field(11, ActorWireValue.Boolean(value.AllowsProgrammedShots));
        writer.Field(
            12,
            Array(value.AllowedActionIds, SemanticId));
        return writer.ToArray();
    }

    private static PublicFormDefinition DecodeForm(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFormDefinition(
            SemanticText(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Bool(reader, 5),
            Bool(reader, 6),
            ActorWireValue.Enum<PublicMovementLayer>(reader.Required(7)),
            Int(reader, 8),
            Bool(reader, 9),
            Bool(reader, 10),
            Bool(reader, 11),
            DecodeArray(reader, 12, item =>
                ActorWireValue.String(
                    item,
                    ActorWireProtocol.MaxSemanticIdBytes)));
    }

    private static byte[] EncodeAction(PublicActionDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, SemanticId(value.Id));
        writer.Field(2, ActorWireValue.Int32(value.Code));
        writer.Field(3, ActorWireValue.Enum(value.Kind));
        writer.Field(4, Array(value.ParameterKinds, ActorWireValue.Enum));
        writer.Field(5, ActorWireValue.Boolean(value.Enabled));
        return writer.ToArray();
    }

    private static PublicActionDefinition DecodeAction(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicActionDefinition(
            SemanticText(reader, 1),
            Int(reader, 2),
            ActorWireValue.Enum<PublicActionKind>(reader.Required(3)),
            DecodeArray(
                reader,
                4,
                ActorWireValue.Enum<PublicActionParameterKind>),
            Bool(reader, 5));
    }

    private static byte[] EncodeProjectiles(PublicProjectileRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Enum(value.Mode));
        writer.Field(2, ActorWireValue.Int32(value.DamagePerHit));
        writer.Field(3, ActorWireValue.Int32(value.MaxTravelTiles));
        writer.Field(4, ActorWireValue.Int32(value.ShootCooldownTicks));
        writer.Field(5, ActorWireValue.Int32(value.TicksPerAdvance));
        writer.Field(6, ActorWireValue.Int32(value.TilesPerAdvance));
        writer.Field(7, ActorWireValue.Int32(value.LaunchTiles));
        writer.Field(8, ActorWireValue.Boolean(value.AdvancesOnLaunchTick));
        writer.Field(
            9,
            ActorWireValue.Boolean(value.DamageAppliedSimultaneously));
        return writer.ToArray();
    }

    private static PublicProjectileRules DecodeProjectiles(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicProjectileRules(
            ActorWireValue.Enum<PublicProjectileMode>(reader.Required(1)),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5),
            Int(reader, 6),
            Int(reader, 7),
            Bool(reader, 8),
            Bool(reader, 9));
    }

    private static byte[] EncodeShotPrograms(PublicShotProgramRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Boolean(value.Enabled));
        writer.Field(2, ActorWireValue.Int32(value.HeadingSectors));
        writer.Field(3, ActorWireValue.Int32(value.BendStepOctants));
        writer.Field(4, ActorWireValue.Int32(value.MinInitialAimOctants));
        writer.Field(5, ActorWireValue.Int32(value.MaxInitialAimOctants));
        writer.Field(6, EncodeAimOnly(value.AimOnlyProgram));
        writer.Field(
            7,
            Array(
                value.AllowedCurvedBendDirections,
                ActorWireValue.Int32));
        writer.Field(8, ActorWireValue.Int32(value.MinBendAfterTiles));
        writer.Field(9, ActorWireValue.Int32(value.MaxBendAfterTiles));
        writer.Field(10, ActorWireValue.Int32(value.MinBendEveryTiles));
        writer.Field(11, ActorWireValue.Int32(value.MaxBendEveryTiles));
        writer.Field(12, ActorWireValue.Int32(value.MinBendCount));
        writer.Field(13, ActorWireValue.Int32(value.MaxBendCount));
        writer.Field(14, ActorWireValue.Int32(value.LaunchTiles));
        writer.Field(15, ActorWireValue.Boolean(value.PayloadOptional));
        writer.Field(16, EncodeShotProgramValue(value.DefaultProgram));
        writer.Optional(
            17,
            value.InvalidPayloadResult is { } invalid
                ? ActorWireValue.Enum(invalid)
                : null);
        writer.Field(
            18,
            ActorWireValue.Enum(value.UnsupportedPayloadResult));
        writer.Field(
            19,
            ActorWireValue.Boolean(value.DiagonalCornersMustBeClear));
        return writer.ToArray();
    }

    private static PublicShotProgramRules DecodeShotPrograms(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        byte[]? invalid = reader.Optional(17);
        return new PublicShotProgramRules(
            Bool(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5),
            DecodeAimOnly(reader.Required(6), depth + 1),
            DecodeArray(reader, 7, ActorWireValue.Int32),
            Int(reader, 8),
            Int(reader, 9),
            Int(reader, 10),
            Int(reader, 11),
            Int(reader, 12),
            Int(reader, 13),
            Int(reader, 14),
            Bool(reader, 15),
            DecodeShotProgramValue(reader.Required(16), depth + 1),
            invalid is null
                ? null
                : ActorWireValue.Enum<PublicActionRejectionResult>(invalid),
            ActorWireValue.Enum<PublicActionRejectionResult>(
                reader.Required(18)),
            Bool(reader, 19));
    }

    private static byte[] EncodeAimOnly(
        PublicAimOnlyShotProgramRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.BendDirection));
        writer.Field(2, ActorWireValue.Int32(value.BendAfterTiles));
        writer.Field(3, ActorWireValue.Int32(value.BendEveryTiles));
        writer.Field(4, ActorWireValue.Int32(value.BendCount));
        return writer.ToArray();
    }

    private static PublicAimOnlyShotProgramRules DecodeAimOnly(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicAimOnlyShotProgramRules(
            Int(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4));
    }

    private static byte[] EncodeShotProgramValue(
        PublicShotProgramValue value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.InitialAimOffset));
        writer.Field(2, ActorWireValue.Int32(value.BendDirection));
        writer.Field(3, ActorWireValue.Int32(value.BendAfterTiles));
        writer.Field(4, ActorWireValue.Int32(value.BendEveryTiles));
        writer.Field(5, ActorWireValue.Int32(value.BendCount));
        return writer.ToArray();
    }

    private static PublicShotProgramValue DecodeShotProgramValue(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicShotProgramValue(
            Int(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5));
    }

    private static byte[] EncodeVision(PublicVisionRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.Range));
        writer.Field(2, ActorWireValue.Enum(value.DistanceMetric));
        writer.Field(3, ActorWireValue.Enum(value.Shape));
        writer.Field(
            4,
            ActorWireValue.Int32(value.OmnidirectionalProximityRange));
        writer.Field(5, ActorWireValue.Enum(value.LineOfSight));
        writer.Field(6, ActorWireValue.Int32(value.HearingRadius));
        writer.Field(
            7,
            ActorWireValue.Int32(value.HearingBearingSectors));
        writer.Field(
            8,
            Array(
                value.HearingDistanceBandUpperBounds,
                ActorWireValue.Int32));
        writer.Field(9, Array(value.LoudEventTypes, ActorWireValue.Enum));
        return writer.ToArray();
    }

    private static PublicVisionRules DecodeVision(byte[] bytes, int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicVisionRules(
            Int(reader, 1),
            ActorWireValue.Enum<PublicDistanceMetric>(reader.Required(2)),
            ActorWireValue.Enum<PublicVisionShape>(reader.Required(3)),
            Int(reader, 4),
            ActorWireValue.Enum<PublicLineOfSightModel>(
                reader.Required(5)),
            Int(reader, 6),
            Int(reader, 7),
            DecodeArray(reader, 8, ActorWireValue.Int32),
            DecodeArray(
                reader,
                9,
                ActorWireValue.Enum<ObservedMatchEventType>));
    }

    private static byte[] EncodeCollisions(PublicCollisionRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Boolean(value.UnitsBlockWalls));
        writer.Field(2, ActorWireValue.Boolean(value.UnitsBlockUnits));
        writer.Field(
            3,
            ActorWireValue.Boolean(value.SameDestinationMovesBlockAll));
        writer.Field(4, ActorWireValue.Boolean(value.SwapMovesBlocked));
        writer.Field(
            5,
            ActorWireValue.Boolean(value.FollowingVacatedUnitAllowed));
        writer.Field(
            6,
            ActorWireValue.Boolean(value.ProjectilesBlockMovement));
        writer.Field(
            7,
            ActorWireValue.Boolean(value.MovingOntoProjectileCausesHit));
        writer.Field(
            8,
            ActorWireValue.Boolean(value.WallsConsumeProjectiles));
        writer.Field(
            9,
            ActorWireValue.Boolean(value.ProjectilesIgnoreOwner));
        writer.Field(
            10,
            ActorWireValue.Boolean(
                value.ProjectilesStopOnFirstNonOwnerUnit));
        writer.Field(
            11,
            ActorWireValue.Boolean(value.ProjectilesCollideWithProjectiles));
        return writer.ToArray();
    }

    private static PublicCollisionRules DecodeCollisions(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicCollisionRules(
            Bool(reader, 1),
            Bool(reader, 2),
            Bool(reader, 3),
            Bool(reader, 4),
            Bool(reader, 5),
            Bool(reader, 6),
            Bool(reader, 7),
            Bool(reader, 8),
            Bool(reader, 9),
            Bool(reader, 10),
            Bool(reader, 11));
    }

    private static byte[] EncodeTickResolution(
        PublicTickResolutionRules value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            ActorWireValue.Boolean(value.ObservationsUsePreTickState));
        writer.Field(
            2,
            ActorWireValue.Boolean(value.DecisionsResolveAsJointStep));
        writer.Field(3, Array(value.Phases, ActorWireValue.Enum));
        return writer.ToArray();
    }

    private static PublicTickResolutionRules DecodeTickResolution(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicTickResolutionRules(
            Bool(reader, 1),
            Bool(reader, 2),
            DecodeArray(
                reader,
                3,
                ActorWireValue.Enum<PublicTickResolutionPhase>));
    }

    private static byte[] EncodeFrontline(PublicFrontlineDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.TeamCount));
        writer.Field(2, ActorWireValue.Int32(value.ParticipantsPerTeam));
        writer.Field(3, ActorWireValue.Int32(value.FrontlinePositionCount));
        writer.Field(4, ActorWireValue.Int32(value.InitialUnitsPerTeam));
        writer.Field(5, ActorWireValue.Int32(value.MaxUnitsPerTeam));
        writer.Field(6, ActorWireValue.Enum(value.TeamPerception));
        writer.Field(7, EncodeCapture(value.Capture));
        writer.Field(8, EncodeVictory(value.Victory));
        writer.Field(9, EncodeLifecycle(value.Lifecycle));
        writer.Field(10, EncodeDeployment(value.Deployment));
        writer.Field(11, EncodeFabrication(value.Fabrication));
        writer.Field(12, EncodeAnchor(value.Anchor));
        writer.Field(13, EncodeAlliedCombat(value.AlliedCombat));
        writer.Field(14, EncodeTurretFire(value.TurretFire));
        return writer.ToArray();
    }

    private static PublicFrontlineDefinition DecodeFrontline(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineDefinition(
            Int(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5),
            ActorWireValue.Enum<TeamPerceptionMode>(reader.Required(6)),
            DecodeCapture(reader.Required(7), depth + 1),
            DecodeVictory(reader.Required(8), depth + 1),
            DecodeLifecycle(reader.Required(9), depth + 1),
            DecodeDeployment(reader.Required(10), depth + 1),
            DecodeFabrication(reader.Required(11), depth + 1),
            DecodeAnchor(reader.Required(12), depth + 1),
            DecodeAlliedCombat(reader.Required(13), depth + 1))
        {
            TurretFire = DecodeTurretFire(
                reader.Required(14),
                depth + 1),
        };
    }

    private static byte[] EncodeCapture(
        PublicFrontlineCaptureDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.Threshold));
        writer.Field(2, ActorWireValue.Int32(value.GainPerSoleTeamTick));
        writer.Field(3, ActorWireValue.Int32(value.DecayAmount));
        writer.Field(4, ActorWireValue.Int32(value.DecayIntervalTicks));
        writer.Field(5, ActorWireValue.Int32(value.RedeployPauseTicks));
        writer.Field(6, ActorWireValue.Int32(value.PushesToBreach));
        writer.Field(7, ActorWireValue.Enum(value.Presence));
        writer.Field(8, ActorWireValue.Enum(value.NonSolePresence));
        writer.Field(9, ActorWireValue.Enum(value.CounterCapture));
        return writer.ToArray();
    }

    private static PublicFrontlineCaptureDefinition DecodeCapture(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineCaptureDefinition(
            Int(reader, 1),
            Int(reader, 2),
            Int(reader, 3),
            Int(reader, 4),
            Int(reader, 5),
            Int(reader, 6),
            ActorWireValue.Enum<PublicFrontlineCapturePresencePolicy>(
                reader.Required(7)),
            ActorWireValue.Enum<PublicFrontlineNonSolePresencePolicy>(
                reader.Required(8)),
            ActorWireValue.Enum<PublicFrontlineCounterCapturePolicy>(
                reader.Required(9)));
    }

    private static byte[] EncodeVictory(
        PublicFrontlineVictoryDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Enum(value.InitialPosition));
        writer.Field(2, Array(value.TeamAdvances, advance =>
        {
            var item = new ActorWireObjectWriter();
            item.Field(1, ActorWireValue.Int32(advance.TeamId));
            item.Field(
                2,
                ActorWireValue.Int32(advance.PositionIndexDelta));
            return item.ToArray();
        }));
        writer.Field(3, ActorWireValue.Enum(value.CompletionPrecedence));
        writer.Field(4, ActorWireValue.Enum(value.TimeoutResolution));
        return writer.ToArray();
    }

    private static PublicFrontlineVictoryDefinition DecodeVictory(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineVictoryDefinition(
            ActorWireValue.Enum<PublicFrontlineInitialPositionPolicy>(
                reader.Required(1)),
            DecodeArray(reader, 2, itemBytes =>
            {
                var item = new ActorWireObjectReader(
                    itemBytes,
                    depth + 1);
                return new PublicFrontlineTeamAdvance(
                    Int(item, 1),
                    Int(item, 2));
            }),
            ActorWireValue.Enum<PublicFrontlineCompletionPrecedence>(
                reader.Required(3)),
            ActorWireValue.Enum<PublicFrontlineTimeoutResolution>(
                reader.Required(4)));
    }

    private static byte[] EncodeLifecycle(
        PublicFrontlineLifecycleDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.PrimeRespawnTicks));
        writer.Field(2, ActorWireValue.Int32(value.ChildRebuildTicks));
        writer.Field(
            3,
            Array(value.FabricationUnlockTicks, ActorWireValue.Int32));
        return writer.ToArray();
    }

    private static PublicFrontlineLifecycleDefinition DecodeLifecycle(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineLifecycleDefinition(
            Int(reader, 1),
            Int(reader, 2),
            DecodeArray(reader, 3, ActorWireValue.Int32));
    }

    private static byte[] EncodeDeployment(
        PublicFrontlineDeploymentDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, SemanticId(value.PrimeDefaultFormId));
        writer.Field(2, SemanticId(value.ChildDefaultFormId));
        writer.Field(3, ActorWireValue.Enum(value.DestructionTransitionClock));
        writer.Field(4, ActorWireValue.Enum(value.PrimeReturn));
        writer.Field(5, ActorWireValue.Enum(value.ChildReturn));
        writer.Field(6, ActorWireValue.Enum(value.NewLife));
        writer.Field(7, ActorWireValue.Enum(value.PrimeSpawnReservation));
        writer.Field(8, ActorWireValue.Enum(value.ProtectedPad));
        return writer.ToArray();
    }

    private static PublicFrontlineDeploymentDefinition DecodeDeployment(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineDeploymentDefinition(
            SemanticText(reader, 1),
            SemanticText(reader, 2),
            ActorWireValue.Enum<PublicFrontlineDestructionTransitionClock>(
                reader.Required(3)),
            ActorWireValue.Enum<PublicFrontlinePrimeReturnPolicy>(
                reader.Required(4)),
            ActorWireValue.Enum<PublicFrontlineChildReturnPolicy>(
                reader.Required(5)),
            ActorWireValue.Enum<PublicFrontlineNewLifePolicy>(
                reader.Required(6)),
            ActorWireValue.Enum<PublicFrontlinePrimeSpawnReservationPolicy>(
                reader.Required(7)),
            ActorWireValue.Enum<PublicFrontlineProtectedPadPolicy>(
                reader.Required(8)));
    }

    private static byte[] EncodeFabrication(
        PublicFrontlineFabricationDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Boolean(value.Enabled));
        writer.Field(2, SemanticId(value.ActionId));
        writer.Field(3, ActorWireValue.Int32(value.FabricatorUnitId));
        writer.Field(4, SemanticId(value.FabricatorFormId));
        writer.Field(5, ActorWireValue.Enum(value.TargetPolicy));
        writer.Field(6, ActorWireValue.Enum(value.ActivationRegion));
        writer.Field(7, ActorWireValue.Boolean(value.ConsumesTick));
        writer.Field(8, ActorWireValue.Int32(value.SpawnDelayTicks));
        writer.Field(9, ActorWireValue.Enum(value.CapacityEvaluation));
        writer.Field(10, ActorWireValue.Enum(value.SpawnRegion));
        writer.Field(11, ActorWireValue.Enum(value.SpawnSelection));
        writer.Field(12, ActorWireValue.Enum(value.SpawnFacing));
        writer.Field(13, ActorWireValue.Enum(value.UnavailableSpawnResult));
        writer.Field(
            14,
            ActorWireValue.Boolean(
                value.RequiresExplicitRefabricationAfterRebuild));
        return writer.ToArray();
    }

    private static PublicFrontlineFabricationDefinition DecodeFabrication(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineFabricationDefinition(
            Bool(reader, 1),
            SemanticText(reader, 2),
            Int(reader, 3),
            SemanticText(reader, 4),
            ActorWireValue.Enum<PublicFrontlineFabricationTargetPolicy>(
                reader.Required(5)),
            ActorWireValue.Enum<PublicFrontlineFabricationActivationRegion>(
                reader.Required(6)),
            Bool(reader, 7),
            Int(reader, 8),
            ActorWireValue.Enum<
                PublicFrontlineFabricationCapacityEvaluation>(
                reader.Required(9)),
            ActorWireValue.Enum<PublicFrontlineFabricationSpawnRegion>(
                reader.Required(10)),
            ActorWireValue.Enum<PublicFrontlineFabricationSpawnSelection>(
                reader.Required(11)),
            ActorWireValue.Enum<PublicFrontlineFabricationSpawnFacing>(
                reader.Required(12)),
            ActorWireValue.Enum<PublicActionRejectionResult>(
                reader.Required(13)),
            Bool(reader, 14));
    }

    private static byte[] EncodeAnchor(
        PublicFrontlineAnchorDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, ActorWireValue.Int32(value.WindupTicks));
        writer.Field(2, ActorWireValue.Int32(value.HealthGain));
        writer.Field(3, ActorWireValue.Boolean(value.IrreversibleForLife));
        writer.Field(4, SemanticId(value.ActionId));
        writer.Field(5, SemanticId(value.SourceFormId));
        writer.Field(6, SemanticId(value.TargetFormId));
        writer.Field(7, ActorWireValue.Boolean(value.ConsumesTick));
        writer.Field(8, ActorWireValue.Enum(value.Completion));
        writer.Field(9, ActorWireValue.Enum(value.PendingActions));
        writer.Field(10, ActorWireValue.Enum(value.SurvivingDamage));
        writer.Field(11, ActorWireValue.Enum(value.Death));
        writer.Field(12, ActorWireValue.Enum(value.ForbiddenTiles));
        writer.Field(13, ActorWireValue.Enum(value.PendingForm));
        writer.Field(14, ActorWireValue.Enum(value.Health));
        writer.Field(15, ActorWireValue.Enum(value.StateContinuity));
        writer.Field(16, ActorWireValue.Enum(value.Terminal));
        return writer.ToArray();
    }

    private static PublicFrontlineAnchorDefinition DecodeAnchor(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineAnchorDefinition(
            Int(reader, 1),
            Int(reader, 2),
            Bool(reader, 3))
        {
            ActionId = SemanticText(reader, 4),
            SourceFormId = SemanticText(reader, 5),
            TargetFormId = SemanticText(reader, 6),
            ConsumesTick = Bool(reader, 7),
            Completion = ActorWireValue.Enum<
                PublicFrontlineAnchorCompletionPolicy>(
                reader.Required(8)),
            PendingActions = ActorWireValue.Enum<
                PublicFrontlineAnchorPendingActionPolicy>(
                reader.Required(9)),
            SurvivingDamage = ActorWireValue.Enum<
                PublicFrontlineAnchorSurvivingDamagePolicy>(
                reader.Required(10)),
            Death = ActorWireValue.Enum<PublicFrontlineAnchorDeathPolicy>(
                reader.Required(11)),
            ForbiddenTiles = ActorWireValue.Enum<
                PublicFrontlineAnchorForbiddenTilePolicy>(
                reader.Required(12)),
            PendingForm = ActorWireValue.Enum<
                PublicFrontlineAnchorPendingFormPolicy>(
                reader.Required(13)),
            Health = ActorWireValue.Enum<
                PublicFrontlineAnchorHealthPolicy>(
                reader.Required(14)),
            StateContinuity = ActorWireValue.Enum<
                PublicFrontlineAnchorStateContinuityPolicy>(
                reader.Required(15)),
            Terminal = ActorWireValue.Enum<
                PublicFrontlineAnchorTerminalPolicy>(
                reader.Required(16)),
        };
    }

    private static byte[] EncodeAlliedCombat(
        PublicFrontlineAlliedCombatDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(
            1,
            ActorWireValue.Boolean(value.FriendlyFireEnabled));
        writer.Field(
            2,
            ActorWireValue.Boolean(value.AlliedProjectilesBlock));
        writer.Field(3, ActorWireValue.Enum(value.ProjectileAttribution));
        return writer.ToArray();
    }

    private static PublicFrontlineAlliedCombatDefinition DecodeAlliedCombat(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineAlliedCombatDefinition(
            Bool(reader, 1),
            Bool(reader, 2),
            ActorWireValue.Enum<PublicFrontlineProjectileAttributionPolicy>(
                reader.Required(3)));
    }

    private static byte[] EncodeTurretFire(
        PublicFrontlineTurretFireDefinition value)
    {
        var writer = new ActorWireObjectWriter();
        writer.Field(1, SemanticId(value.ActionId));
        writer.Field(2, SemanticId(value.FormId));
        writer.Field(
            3,
            Array(
                value.AllowedProjectileHeadings,
                ActorWireValue.Enum));
        writer.Field(4, ActorWireValue.Enum(value.Aim));
        writer.Field(5, ActorWireValue.Enum(value.Projectile));
        writer.Field(6, ActorWireValue.Enum(value.Facing));
        writer.Field(7, ActorWireValue.Enum(value.Range));
        writer.Field(8, ActorWireValue.Enum(value.Resources));
        writer.Field(9, ActorWireValue.Enum(value.Traversal));
        return writer.ToArray();
    }

    private static PublicFrontlineTurretFireDefinition DecodeTurretFire(
        byte[] bytes,
        int depth)
    {
        var reader = new ActorWireObjectReader(bytes, depth);
        return new PublicFrontlineTurretFireDefinition(
            SemanticText(reader, 1),
            SemanticText(reader, 2),
            DecodeArray(
                reader,
                3,
                ActorWireValue.Enum<ProjectileHeading>),
            ActorWireValue.Enum<PublicFrontlineTurretFireAimPolicy>(
                reader.Required(4)),
            ActorWireValue.Enum<PublicFrontlineTurretFireProjectilePolicy>(
                reader.Required(5)),
            ActorWireValue.Enum<PublicFrontlineTurretFireFacingPolicy>(
                reader.Required(6)),
            ActorWireValue.Enum<PublicFrontlineTurretFireRangePolicy>(
                reader.Required(7)),
            ActorWireValue.Enum<PublicFrontlineTurretFireResourcePolicy>(
                reader.Required(8)),
            ActorWireValue.Enum<PublicFrontlineTurretFireTraversalPolicy>(
                reader.Required(9)));
    }

    private static byte[] Array<T>(
        IEnumerable<T> values,
        Func<T, byte[]> encode) =>
        ActorWireValue.Array(values, encode);

    private static ImmutableArray<T> DecodeArray<T>(
        ActorWireObjectReader reader,
        ushort fieldId,
        Func<byte[], T> decode) =>
        ActorWireValue.Array(reader.Required(fieldId), decode);

    private static byte[] Id(string value, int maxBytes = 256) =>
        ActorWireValue.String(value, maxBytes);

    private static byte[] SemanticId(string value) =>
        ActorWireValue.String(value, ActorWireProtocol.MaxSemanticIdBytes);

    private static int Int(ActorWireObjectReader reader, ushort fieldId) =>
        ActorWireValue.Int32(reader.Required(fieldId));

    private static bool Bool(
        ActorWireObjectReader reader,
        ushort fieldId) =>
        ActorWireValue.Boolean(reader.Required(fieldId));

    private static string Text(
        ActorWireObjectReader reader,
        ushort fieldId,
        int maxBytes = 256) =>
        ActorWireValue.String(reader.Required(fieldId), maxBytes);

    private static string SemanticText(
        ActorWireObjectReader reader,
        ushort fieldId) =>
        ActorWireValue.String(
            reader.Required(fieldId),
            ActorWireProtocol.MaxSemanticIdBytes);
}
