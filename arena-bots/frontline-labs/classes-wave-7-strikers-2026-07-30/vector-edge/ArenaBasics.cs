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

    // The scaffold's four composite decision helpers — TryFabricateReady,
    // TryDodge, TryDirectShot and TryAdvanceToActiveObjective — are deleted
    // here rather than left unused. They are 368 lines this bot never calls,
    // and the controlled builder caps submitted sources at 256 KB, of which
    // this one generated file was a fifth. What remains is exactly the set of
    // contract readers the doctrine does call. See DX.md.

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

    private static int SignedHeadingDifference(
        ProjectileHeading from,
        ProjectileHeading to)
    {
        int difference = ((int)to - (int)from + 8) % 8;
        return difference > 4 ? difference - 8 : difference;
    }

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
