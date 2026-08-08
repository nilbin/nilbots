using BotArena.Sdk;

/// <summary>
/// Contract-driven tactical building blocks for the generated starter.
/// Keep or replace them as the bot develops; strategy belongs in BOTNAME.Tick.
/// </summary>
internal static class ArenaBasics
{
    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];
    private static readonly HashSet<Position> NoPositions = [];

    /// <summary>
    /// Tiles of the objective the match is currently fought over, taken from
    /// the observed active index rather than any fixed position. Empty when
    /// the mode declares no ordered objectives.
    /// </summary>
    public static Position[] ActiveObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? ObjectiveTiles(contract, mode.ActivePositionIndex)
            : [];

    /// <summary>
    /// Which team's advance the territory ratchet is protecting right now, and
    /// the tick that protection lifts — READ from the observation, not inferred
    /// from it. Null when no hold binds this tick, which includes every
    /// contract whose capture definition declares no hold at all.
    ///
    /// <para>This used to be a derivation, and the derivation was expensive
    /// and partly wrong. The start was recoverable as
    /// <c>ControlResumesAtTick − RedeployPauseTicks</c>; the OWNER had no
    /// derivation at all, only a guess from the signed displacement of the
    /// front, which is wrong the first time an opponent regresses from a lead
    /// and is unavailable to a life born inside the hold, because private
    /// memory is life-scoped. Both facts are now published, so ask.</para>
    ///
    /// <para>Inside a live hold the two sides are playing opposite games: the
    /// owner's presence buys ground and the opponent's presence buys nothing,
    /// because a capture completed inside another team's hold is SPENT — the
    /// claim resets exactly as a successful capture does and the objective does
    /// not move. Compare <see cref="Hold.EndsAtTick"/> with
    /// <c>context.Tick</c> the same way you compare
    /// <c>ControlResumesAtTick</c>: the hold binds while the tick is strictly
    /// below it.</para>
    /// </summary>
    public static Hold? LiveHold(GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline
            {
                HoldOwnerTeamId: int owner,
                HoldEndsAtTick: int endsAt,
            }
            ? new Hold(
                owner,
                owner == context.Self.ActorId.TeamId,
                endsAt,
                endsAt - context.Tick)
            : null;

    /// <summary>
    /// A live territory-ratchet hold as this life sees it.
    /// </summary>
    /// <param name="OwnerTeamId">Team whose advance is protected.</param>
    /// <param name="Mine">
    /// True when this life's team owns the hold — the side for which standing
    /// on the objective is worth something.
    /// </param>
    /// <param name="EndsAtTick">
    /// First tick on which the hold no longer denies regression.
    /// </param>
    /// <param name="RemainingTicks">
    /// Ticks the hold still has to run from this observation's tick.
    /// </param>
    public sealed record Hold(
        int OwnerTeamId,
        bool Mine,
        int EndsAtTick,
        int RemainingTicks);

    /// <summary>
    /// Tiles of one objective in the ordered chain. Empty for an index outside
    /// the chain, which is the honest answer for "one step past the end" —
    /// callers walking the chain do not need their own bounds check.
    /// </summary>
    public static Position[] ObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        int positionIndex)
    {
        if (contract.ModeMapBinding
                is not GenericActorResolvedMatchContract
                    .FrontlineModeMapBinding binding
            || positionIndex < 0
            || positionIndex >= binding.OrderedObjectiveRegionIds.Length)
        {
            return [];
        }

        string regionId = binding.OrderedObjectiveRegionIds[positionIndex];
        return contract.Map.Regions
            .FirstOrDefault(region =>
                string.Equals(
                    region.RegionId,
                    regionId,
                    StringComparison.Ordinal))
            ?.Tiles
            .ToArray()
            ?? [];
    }

    /// <summary>
    /// Tiles of the objective one step BEHIND this team's advance — the ground
    /// it already took. Derived from the chain and the team's declared index
    /// delta, never from a spawn, so it moves with the front. Empty when the
    /// team is already at its end of the chain or the mode declares no ordered
    /// objectives.
    /// </summary>
    public static Position[] OwnSideObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? OwnSideObjectiveTiles(
                contract,
                context.Self.ActorId.TeamId,
                mode.ActivePositionIndex)
            : [];

    /// <summary>
    /// Any team's own-side objective, so the same question can be asked about
    /// where the opposition falls back to.
    /// </summary>
    public static Position[] OwnSideObjectiveTiles(
        GenericActorResolvedMatchContract contract,
        int teamId,
        int activePositionIndex)
    {
        if (contract.ModeMapBinding
            is not GenericActorResolvedMatchContract
                .FrontlineModeMapBinding binding)
        {
            return [];
        }
        var advance = binding.TeamAdvances.FirstOrDefault(entry =>
            entry.TeamId == teamId);
        return advance is null
            ? []
            : ObjectiveTiles(
                contract,
                activePositionIndex - advance.ObjectiveIndexDelta);
    }

    /// <summary>
    /// Whether this contract places automatic returns and activations by the
    /// objective chain instead of by the slot's spawn anchor. Read
    /// <c>lifecycle.automaticReturnPlacement</c> — it is a policy ID, and the
    /// chain-derived value names the own-side chain-adjacent objective.
    /// Nothing in the observation schema changes with it, so a bot that never
    /// reads it simply believes the wrong thing about where it will reappear.
    /// </summary>
    public static bool ArrivalsRallyForward(
        GenericActorResolvedMatchContract contract) =>
        contract.Rules.Lifecycle.AutomaticReturnPlacement.Contains(
            "own-side-chain-adjacent-objective",
            StringComparison.Ordinal);

    /// <summary>
    /// Where this life's slot can expect its next automatic arrival: the
    /// own-side chain-adjacent objective region when the contract rallies
    /// arrivals forward, otherwise the slot's declared spawn anchor. This is
    /// the contract's INTENT — a chain-deriving host still falls back to the
    /// anchor when the derived region offers no free tile, and only
    /// <c>context.Self.Position</c> on a life's first tick is the fact. Use
    /// this to price a death before it happens ("how far from the fight does
    /// dying put me?"); use the observed position to plan once it has.
    /// Empty when the contract declares neither an anchor nor a chain.
    /// </summary>
    public static Position[] ExpectedArrivalTiles(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? ExpectedArrivalTiles(
                contract,
                context.Self.ActorId.TeamId,
                context.Self.ActorId.UnitId,
                mode.ActivePositionIndex)
            : ExpectedArrivalTiles(
                contract,
                context.Self.ActorId.TeamId,
                context.Self.ActorId.UnitId,
                activePositionIndex: -1);

    /// <summary>
    /// Any slot's expected arrival, so the same question can be asked about an
    /// enemy slot whose reinforcement timing you already read from the
    /// lifecycle assignments.
    /// </summary>
    public static Position[] ExpectedArrivalTiles(
        GenericActorResolvedMatchContract contract,
        int teamId,
        int unitId,
        int activePositionIndex)
    {
        Position[] rally = ArrivalsRallyForward(contract)
            ? OwnSideObjectiveTiles(contract, teamId, activePositionIndex)
            : [];
        if (rally.Length > 0)
            return rally;

        string? spawnId = contract.LifecycleAssignments
            .FirstOrDefault(assignment =>
                assignment.TeamId == teamId
                && assignment.UnitId == unitId)
            ?.AssignedRespawnSpawnId;
        if (spawnId is null)
            return [];
        return contract.InitialDeployment.Spawns
            .Where(spawn =>
                string.Equals(
                    spawn.SpawnId,
                    spawnId,
                    StringComparison.Ordinal))
            .Select(spawn => spawn.Position)
            .ToArray();
    }

    /// <summary>
    /// Objective weight standing on the active objective right now, split by
    /// side, plus whether this life is one of the bodies counted. Weight comes
    /// from each observed body's form, because a form may declare weight zero
    /// and hold ground for nothing.
    /// <para>
    /// What the numbers are worth is a contract fact, not a constant: read
    /// <see cref="CaptureRules.SurplusWeightScalesGain"/> to learn whether a
    /// second body adds pressure or is merely a second body, and
    /// <see cref="CaptureRules.OnlyEnemySolePresenceDecays"/> to learn whether
    /// standing contested is costing your claim or preserving it.
    /// </para>
    /// <para>
    /// Enemy weight counts only what your team currently SEES. An unobserved
    /// body on the objective still contests it, so treat this as a lower
    /// bound on the opposition.
    /// </para>
    /// </summary>
    public static (int OwnWeight, int EnemyWeight, bool SelfPresent)
        ObjectivePresence(
            GenericActorResolvedMatchContract contract,
            GenericActorContext context)
    {
        HashSet<Position> tiles =
            ActiveObjectiveTiles(contract, context).ToHashSet();
        if (tiles.Count == 0)
            return (0, 0, false);

        int Weight(string formId) => contract.Rules.Forms
            .FirstOrDefault(form =>
                string.Equals(form.Id, formId, StringComparison.Ordinal))
            ?.ObjectiveWeight
            ?? 0;

        bool selfPresent = tiles.Contains(context.Self.Position);
        int own = selfPresent ? Weight(context.Self.FormId) : 0;
        own += context.Allies
            .Where(ally => tiles.Contains(ally.Position))
            .Sum(ally => Weight(ally.FormId));
        int enemy = context.Enemies
            .Where(enemy => tiles.Contains(enemy.Position))
            .Sum(enemy => Weight(enemy.FormId));
        return (own, enemy, selfPresent);
    }

    /// <summary>
    /// The capture policy VALUES this contract plays by, gathered so a
    /// doctrine prices pushes from the contract instead of from habit. None of
    /// these fields changes the observation schema, so reading them is the
    /// only way to tell one capture policy from another.
    /// Null when the mode is not objective-based — a deathmatch contract
    /// carries no capture definition at all.
    /// </summary>
    public static CaptureRules? Capture(
        GenericActorResolvedMatchContract contract)
    {
        if (contract.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline)
        {
            return null;
        }

        GenericActorRulesContract.FrontlineCapture capture = frontline.Capture;
        return new CaptureRules(
            capture.Threshold,
            capture.GainPerSoleTeamTick,
            capture.DecayAmount,
            capture.DecayIntervalTicks,
            capture.RedeployPauseTicks,
            capture.RatchetHoldTicks > 0 ? capture.RatchetHoldTicks : null,
            capture.ControlPolicy.Contains(
                "net-positive-objective-weight-difference",
                StringComparison.Ordinal),
            capture.DecayClock.Contains(
                "enemy-sole-erosion-only",
                StringComparison.Ordinal));
    }

    /// <summary>
    /// What one objective push costs and what protects it, read from the
    /// capture definition. Canonical contracts omit inert fields, so an
    /// ABSENT value is a real answer rather than a gap.
    /// </summary>
    /// <param name="Threshold">
    /// Progress required to complete one capture.
    /// </param>
    /// <param name="GainPerSoleTeamTick">
    /// Base progress per tick of control. The control policy decides whether
    /// that base is multiplied by surplus weight; see
    /// <paramref name="SurplusWeightScalesGain"/>.
    /// </param>
    /// <param name="DecayAmount">Progress removed at each decay application.</param>
    /// <param name="DecayIntervalTicks">Ticks between decay applications.</param>
    /// <param name="RedeployPauseTicks">
    /// Ticks after an advance during which control cannot resume. The
    /// observation's <c>ControlResumesAtTick</c> is the live clock.
    /// </param>
    /// <param name="HoldTicks">
    /// How long a completed advance is protected from being pushed back, or
    /// <see langword="null"/> when the capture definition declares no hold at
    /// all — an absent hold field means captures never lock, and the front can
    /// come straight back. When a hold IS declared, a capture completed inside
    /// another team's live hold is SPENT: the claim resets exactly as a
    /// successful capture does and the objective does not move. Pricing a push
    /// as if every capture advances the front is the mistake this field exists
    /// to prevent. This is the contract's DURATION; for the hold that is
    /// running right now — whose it is and when it lifts — call
    /// <see cref="LiveHold"/>, which reads both from the observation instead
    /// of reconstructing them from the advance you happened to witness.
    /// </param>
    /// <param name="SurplusWeightScalesGain">
    /// True when net objective weight scales capture pressure, so a second
    /// body on the objective is worth more than the first body's presence.
    /// False when control is binary: one body of positive weight nulls any
    /// number of opposing bodies, and reinforcing a contested objective buys
    /// nothing but survivability.
    /// </param>
    /// <param name="OnlyEnemySolePresenceDecays">
    /// True when empty and contested ticks preserve a claim and only an enemy
    /// standing alone erodes it — leaving an objective is then cheap, and
    /// contesting one is a full stop rather than a slow bleed. False when the
    /// decay clock also runs while the objective is empty or contested.
    /// </param>
    public sealed record CaptureRules(
        int Threshold,
        int GainPerSoleTeamTick,
        int DecayAmount,
        int DecayIntervalTicks,
        int RedeployPauseTicks,
        int? HoldTicks,
        bool SurplusWeightScalesGain,
        bool OnlyEnemySolePresenceDecays);

    /// <summary>
    /// "Should I eat this?" — what one hostile bolt costs and how long you have
    /// to decide, read from the bolt rather than reverse-engineered from the
    /// attack profile that fired it (which a redacted owner may not even name).
    /// Null when the bolt cannot reach <paramref name="target"/> on its current
    /// heading and remaining range at all.
    ///
    /// <para>The arithmetic is the contract's own: the bolt crosses
    /// <c>TilesPerAdvance</c> tiles every <c>TicksPerAdvance</c> ticks and the
    /// next advance is <c>TicksUntilAdvance</c> away, so an exact tick of
    /// arrival exists — and <c>DamagePerHit</c> says whether arriving matters.
    /// Both new fields are per projectile: a volley bolt and an ordinary bolt
    /// need not agree on either.</para>
    /// </summary>
    public static Incoming? Threat(
        GenericActorContext.ObservedProjectile projectile,
        Position target)
    {
        if (!TryRay(
                projectile.Position,
                target,
                out ProjectileHeading heading,
                out int distance)
            || heading != projectile.Heading
            || distance > projectile.RemainingTiles)
        {
            return null;
        }

        // The first advance is TicksUntilAdvance away; each later one adds a
        // full cadence. Integer ceiling, because a partial advance does not
        // move the bolt.
        int advances = (distance + projectile.TilesPerAdvance - 1)
            / projectile.TilesPerAdvance;
        int ticks = projectile.TicksUntilAdvance
            + (advances - 1) * projectile.TicksPerAdvance;
        return new Incoming(ticks, projectile.DamagePerHit, distance);
    }

    /// <summary>One hostile bolt's exact bill and deadline.</summary>
    /// <param name="TicksUntilArrival">
    /// Ticks until the bolt reaches the target tile.
    /// </param>
    /// <param name="Damage">Health one contact removes.</param>
    /// <param name="Tiles">Tiles between the bolt and the target.</param>
    public sealed record Incoming(
        int TicksUntilArrival,
        int Damage,
        int Tiles);

    private static bool ReachesWithinAdvances(
        GenericActorContext.ObservedProjectile projectile,
        Position target,
        int maxAdvances)
    {
        if (!TryRay(
                projectile.Position,
                target,
                out ProjectileHeading heading,
                out int distance)
            || heading != projectile.Heading)
        {
            return false;
        }
        return distance <= Math.Min(
            projectile.TilesPerAdvance * maxAdvances,
            projectile.RemainingTiles);
    }

    private static bool TryRay(
        Position source,
        Position target,
        out ProjectileHeading heading,
        out int distance)
    {
        int dx = target.X - source.X;
        int dy = target.Y - source.Y;
        distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance == 0
            || dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
        {
            heading = default;
            return false;
        }

        (int StepX, int StepY) step = (Math.Sign(dx), Math.Sign(dy));
        heading = step switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            (-1, -1) => ProjectileHeading.NorthWest,
            _ => default,
        };
        return true;
    }

    private static bool ClearRay(
        GenericActorMapContract map,
        Position source,
        Position target,
        bool strictDiagonalCorners)
    {
        int stepX = Math.Sign(target.X - source.X);
        int stepY = Math.Sign(target.Y - source.Y);
        Position cursor = source;
        while (cursor != target)
        {
            Position next = cursor.Offset(stepX, stepY);
            if (next != target && !CanEnter(map, next, NoPositions))
                return false;
            if (strictDiagonalCorners
                && stepX != 0
                && stepY != 0
                && (
                    !CanEnter(
                        map,
                        cursor.Offset(stepX, 0),
                        NoPositions)
                    || !CanEnter(
                        map,
                        cursor.Offset(0, stepY),
                        NoPositions)
                ))
            {
                return false;
            }
            cursor = next;
        }
        return true;
    }

    private static bool CanEnter(
        GenericActorMapContract map,
        Position position,
        IReadOnlySet<Position> occupied) =>
        position.X >= 0
        && position.Y >= 0
        && position.X < map.Width
        && position.Y < map.Height
        && map.TileRows[position.Y][position.X] != '#'
        && !occupied.Contains(position);

    private static Position Offset(
        Position position,
        Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return position.Offset(dx, dy);
    }

    private static int DistanceToObjective(
        Position position,
        IReadOnlyCollection<Position> objectiveTiles) =>
        objectiveTiles.Count == 0
            ? 0
            : objectiveTiles.Min(position.ChebyshevDistance);

    /// <summary>
    /// This team's advance direction as a map heading, derived from the
    /// ordered objective regions and the team's declared index direction.
    /// Chain-derived on purpose: "away from my spawn" agrees with it only on
    /// contracts that put spawns behind the chain, and a bot that reappears
    /// beside the fight has no home vector to reason from.
    /// Null when the mode declares no advance (deathmatch, or degenerate
    /// geometry).
    /// </summary>
    public static Direction? AdvanceDirection(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context) =>
        AdvanceDirection(contract, context.Self.ActorId.TeamId);

    /// <summary>
    /// Any team's advance direction, so a doctrine can reason about where the
    /// opposition is pushing from as easily as about its own front.
    /// </summary>
    public static Direction? AdvanceDirection(
        GenericActorResolvedMatchContract contract,
        int teamId)
    {
        if (contract.ModeMapBinding
            is not GenericActorResolvedMatchContract
                .FrontlineModeMapBinding binding
            || binding.OrderedObjectiveRegionIds.Length < 2)
        {
            return null;
        }
        var advance = binding.TeamAdvances.FirstOrDefault(entry =>
            entry.TeamId == teamId);
        if (advance is null)
            return null;

        (double X, double Y)? Centroid(string regionId)
        {
            var region = contract.Map.Regions.FirstOrDefault(entry =>
                string.Equals(
                    entry.RegionId, regionId, StringComparison.Ordinal));
            if (region is null || region.Tiles.IsEmpty)
                return null;
            return (
                region.Tiles.Average(tile => (double)tile.X),
                region.Tiles.Average(tile => (double)tile.Y));
        }

        (double X, double Y)? first =
            Centroid(binding.OrderedObjectiveRegionIds[0]);
        (double X, double Y)? last = Centroid(
            binding.OrderedObjectiveRegionIds[^1]);
        if (first is not { } from || last is not { } to)
            return null;
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        if (advance.ObjectiveIndexDelta < 0)
        {
            dx = -dx;
            dy = -dy;
        }
        if (dx == 0 && dy == 0)
            return null;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Direction.East : Direction.West
            : dy > 0 ? Direction.South : Direction.North;
    }

    /// <summary>
    /// A mirror-fair direction preference: advance first, retreat last, and
    /// the two perpendiculars ordered by the per-life deterministic random
    /// stream. An absolute order (always prefer East) gives the east-advancing
    /// team a systematic edge on a mirror-symmetric map — measured as a
    /// 40-of-40 side sweep in the wave-1 factorial — because both teams share
    /// the same absolute preference. Randomizing residual ties converts that
    /// bias into seed noise, which mirrored accounting can wash out.
    /// </summary>
    public static Direction[] OrderedDirections(
        GenericActorResolvedMatchContract contract,
        GenericActorContext context)
    {
        Direction forward =
            AdvanceDirection(contract, context) ?? context.Self.Facing;
        Direction backward = Opposite(forward);
        Direction[] laterals =
            Directions.Where(direction =>
                direction != forward && direction != backward)
            .ToArray();
        if (laterals.Length == 2 && context.Random.NextBool())
            (laterals[0], laterals[1]) = (laterals[1], laterals[0]);
        return [forward, .. laterals, backward];
    }

    private static Direction Opposite(Direction direction) =>
        direction switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            _ => Direction.East,
        };

}
