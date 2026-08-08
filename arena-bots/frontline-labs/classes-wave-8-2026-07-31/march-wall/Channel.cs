using BotArena.Sdk;

/// <summary>
/// THE CHANNEL. What taking ground IS, on a ruleset whose control policy counts
/// only the bodies that DID NOT CHANGE TILE this tick.
///
/// <para>Every previous revision of this lineage priced a push as "stand on the
/// point and outlast them". That is now wrong in three separate ways and the
/// contract says so in fields that are simply absent everywhere else:</para>
///
/// <list type="bullet">
/// <item><b>Claim weight is stationary weight; denial weight is everybody.</b> A
/// defender who keeps moving still subtracts from our total, and an attacker who
/// takes a step contributes nothing that tick. Two kiting defenders therefore hold
/// three stationary attackers, and a raw push stalls forever.</item>
/// <item><b>Damage to a controlling body ON the objective reverts the whole run</b>
/// by the damage taken. Damage off the objective reverts nothing — which is the
/// entire reason a SCREEN exists, and why this class's shell (weight 1, immobile,
/// deflects its facing quadrant) is the best channeler in the game.</item>
/// <item><b>Retaking is a channel too</b>, at the declared erosion multiple. A
/// standing enemy claim is cleared by control, not by presence, so a body that
/// steps on and off the point erodes nothing.</item>
/// </list>
///
/// <para>Nothing here names a class, a form, or an arm. When
/// <c>controlPolicy</c> is not the channel, <see cref="Channels"/> is false and
/// every doctrine step that consults this object falls straight back to the
/// wave-6 behaviour — which is what lets one artifact play the swell cell and the
/// siege cell from the same source.</para>
/// </summary>
internal sealed class Channel
{
    /// <summary>
    /// The channel-era doctrine clauses, one switch each, in the same shape
    /// <see cref="Column.Rules"/> uses. An ablation is a one-line edit, so the
    /// artifact under measurement differs from the shipped one by exactly one
    /// decision. They are properties rather than constants so that flipping one
    /// does not make the code it guards unreachable.
    /// </summary>
    internal static class Rules
    {
        /// <summary>
        /// R1. STILLNESS. While standing on the active objective and our not
        /// moving is what buys the tick, the body does not step — not for
        /// spacing, not to close a lane, not to yield a tile. It may still
        /// rotate, shoot, raise a guard, and dodge a bolt that would revert
        /// the run.
        /// </summary>
        public static bool Stillness => true;

        /// <summary>
        /// R2. ESCORT. Against a live defence a spare body screens the
        /// channeler's firing line from OFF the objective instead of stacking
        /// onto the point; against a dead one it stacks, because surplus
        /// stationary weight scales the gain to the declared cap.
        /// </summary>
        public static bool Escort => true;

        /// <summary>
        /// R3. INTERRUPT FIRE. When the other team is the controller, a body of
        /// theirs standing on the active objective is worth strictly more than
        /// any other target: every point of damage it takes reverts a point of
        /// their run, and the run is the whole capture.
        /// </summary>
        public static bool InterruptFire => true;
    }

    private Channel(
        bool channels,
        int stationaryCap,
        int erosionMultiplier,
        int revertPerDamagePoint,
        bool interruptOnObjectiveOnly)
    {
        Channels = channels;
        StationaryCap = stationaryCap;
        ErosionMultiplier = erosionMultiplier;
        RevertPerDamagePoint = revertPerDamagePoint;
        InterruptOnObjectiveOnly = interruptOnObjectiveOnly;
    }

    /// <summary>
    /// True when the declared control policy scores stationary claim weight
    /// against total denial weight. Read from the policy ID rather than from the
    /// presence of the companion fields, because the fields are what the policy
    /// implies and the policy is what it IS.
    /// </summary>
    public bool Channels { get; }

    /// <summary>Ceiling on the stationary-surplus gain multiplier; 0 means none.</summary>
    public int StationaryCap { get; }

    /// <summary>How many times faster an enemy claim erodes than ours builds.</summary>
    public int ErosionMultiplier { get; }

    /// <summary>Progress reverted per point of health removed; 0 means no interrupt.</summary>
    public int RevertPerDamagePoint { get; }

    /// <summary>
    /// True when only controlling-team bodies standing on the active objective
    /// region revert anything — the fact that makes a screening body free.
    /// </summary>
    public bool InterruptOnObjectiveOnly { get; }

    /// <summary>
    /// Reads the channel out of the resolved contract. The three companion
    /// fields and the interrupt block are absent on every ruleset that does not
    /// channel, exactly like a ratchet hold on a ruleset without a ratchet, so
    /// an absent value is a real answer.
    /// </summary>
    public static Channel Read(ContractView view)
    {
        GenericActorRulesContract.FrontlineCapture? capture =
            view.Frontline?.Capture;
        if (capture is null)
            return new Channel(false, 0, 0, 0, false);

        bool channels = capture.ControlPolicy.Contains(
            "stationary-claim-weight",
            StringComparison.Ordinal);
        return new Channel(
            channels,
            Math.Max(0, capture.StationaryGainMultiplierCap),
            Math.Max(0, capture.OpposingErosionMultiplier),
            capture.ClaimInterrupt?.RevertPerDamagePoint ?? 0,
            capture.ClaimInterrupt?.Scope.Contains(
                "on-active-objective-region",
                StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// What the channel looks like on one tick, from this body's seat. Every
    /// field is derived from the frozen shared observation plus the contract, so
    /// two lives of the same team compute the same numbers and therefore agree
    /// about who holds and who screens without a message passing between them.
    /// </summary>
    internal sealed record State(
        bool Live,
        bool SelfOnObjective,
        int AlliedStationaryWeight,
        int EnemyDenialWeight,
        int Progress,
        int? ClaimingTeamId,
        bool WeControl,
        bool TheyControl,
        bool Paused,
        bool DefenceIsLive)
    {
        /// <summary>Claim weight if this body holds its tile this tick.</summary>
        public int ClaimIfHeld =>
            AlliedStationaryWeight + (SelfOnObjective ? SelfWeight : 0);

        /// <summary>Claim weight if this body steps.</summary>
        public int ClaimIfMoved => AlliedStationaryWeight;

        /// <summary>This body's own declared objective weight.</summary>
        public int SelfWeight { get; init; }

        /// <summary>Declared ceiling on the surplus multiplier.</summary>
        public int Cap { get; init; } = 1;

        /// <summary>Gain multiplier for a given claim weight, capped as declared.</summary>
        public int Multiplier(int claim)
        {
            int surplus = claim - EnemyDenialWeight;
            if (surplus <= 0)
                return 0;
            return Cap > 0 ? Math.Min(Cap, surplus) : surplus;
        }

        /// <summary>
        /// True when this body's stillness is the difference between a tick that
        /// pays and a tick that does not. This is the whole of R1: it binds only
        /// while the body actually stands on the point, only while the capture
        /// clock is running, and only while holding buys strictly more than
        /// stepping — so a body whose presence cannot break the enemy's denial
        /// weight is released to manoeuvre rather than frozen for nothing.
        /// </summary>
        public bool StillnessPays =>
            Live
            && SelfOnObjective
            && SelfWeight > 0
            && !Paused
            && Multiplier(ClaimIfHeld) > Multiplier(ClaimIfMoved);

        /// <summary>
        /// True when adding one more stationary body to the point would buy
        /// strictly more speed. False at the declared cap, and false while the
        /// enemy's denial weight is already beating us — in both cases the extra
        /// body is better spent screening.
        /// </summary>
        public bool StackingBuysSpeed =>
            Live
            && Multiplier(AlliedStationaryWeight + 1)
                > Multiplier(AlliedStationaryWeight);
    }

    /// <summary>
    /// One reading of the channel for this tick.
    ///
    /// <para>The allied stationary weight is an estimate and is deliberately the
    /// optimistic one: an ally already standing on the objective is counted as
    /// holding, because every life on this team runs R1 and R1 says a body on the
    /// point holds. That is the same substrate the march order runs on — no
    /// channel to negotiate over, so the plan is re-derived rather than told —
    /// and the cost of being wrong is one tick of a slightly optimistic
    /// arithmetic, never an illegal action.</para>
    /// </summary>
    public State Read(
        ContractView view,
        GenericActorContext context,
        IReadOnlyCollection<Position> objectiveTiles)
    {
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return new State(false, false, 0, 0, 0, null, false, false, true, false);
        }

        HashSet<Position> tiles = objectiveTiles.ToHashSet();
        bool selfOn = tiles.Contains(context.Self.Position);
        int selfWeight = view.ObjectiveWeight(context.Self.FormId);

        int allied = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!tiles.Contains(ally.Position))
                continue;
            allied += view.ObjectiveWeight(ally.FormId);
        }

        int denial = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (!tiles.Contains(enemy.Position))
                continue;
            denial += view.ObjectiveWeight(enemy.FormId);
        }

        bool paused = context.Tick < mode.ControlResumesAtTick;
        bool we = mode.ClaimingTeamId == view.MyTeamId && mode.CaptureProgress > 0;
        bool they = mode.ClaimingTeamId is int claimant
            && claimant != view.MyTeamId
            && mode.CaptureProgress > 0;

        return new State(
            Channels,
            selfOn,
            allied,
            denial,
            mode.CaptureProgress,
            mode.ClaimingTeamId,
            we,
            they,
            paused,
            LiveDefence(view, context, tiles))
        {
            SelfWeight = selfWeight,
            Cap = StationaryCap,
        };
    }

    /// <summary>
    /// Whether anything over there can put damage on the point. "Stack against a
    /// broken defence, screen against a live one" is a read on published state,
    /// and this is that read: any hostile body — turret included, because a
    /// weight-zero gun contributes no denial weight but reverts a point of
    /// progress every time it fires — whose declared travel envelope covers a
    /// tile of the contested region.
    /// </summary>
    private static bool LiveDefence(
        ContractView view,
        GenericActorContext context,
        IReadOnlySet<Position> objectiveTiles)
    {
        if (objectiveTiles.Count == 0)
            return false;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            int reach =
                view.Attack(enemy.FormId)?.Projectile.MaxTravelTiles ?? 0;
            if (reach <= 0)
                continue;
            foreach (Position tile in objectiveTiles)
            {
                if (Geometry.Chebyshev(enemy.Position, tile) <= reach + 1)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Screening tiles for one channeler: the tiles a hostile bolt aimed at it
    /// has to cross, OFF the contested region so that eating one costs the run
    /// nothing. Derived from the collision policy rather than invented — bolts
    /// stop on the first enemy body in their path and pass through allies, so a
    /// body standing on that line absorbs for free and still shoots back.
    ///
    /// <para>Only the two tiles nearest the channeler on each hostile bearing are
    /// offered. Further out is a body doing its own duel somewhere; a screen is
    /// specifically the tile the bolt would otherwise reach the point through.</para>
    /// </summary>
    public static List<Position> ScreenTiles(
        ContractView view,
        GenericActorContext context,
        Position channeler,
        IReadOnlySet<Position> objectiveTiles)
    {
        var tiles = new List<Position>();
        HashSet<Position> occupied = context.Allies
            .Select(ally => ally.Position)
            .Concat(context.Enemies.Select(enemy => enemy.Position))
            .ToHashSet();

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            int reach = view.Attack(enemy.FormId)?.Projectile.MaxTravelTiles ?? 0;
            if (reach <= 0)
                continue;
            if (!Geometry.TryBearing(channeler, enemy.Position, out int dx, out int dy))
                continue;
            int distance = Math.Max(
                Math.Abs(enemy.Position.X - channeler.X),
                Math.Abs(enemy.Position.Y - channeler.Y));
            if (distance > reach + 1 || distance < 2)
                continue;

            for (int step = 1; step <= 2 && step < distance; step++)
            {
                var tile = new Position(
                    channeler.X + (dx * step),
                    channeler.Y + (dy * step));
                if (view.IsWall(tile)
                    || objectiveTiles.Contains(tile)
                    || occupied.Contains(tile)
                    || view.ReservedSpawnTiles.Contains(tile)
                    || tiles.Contains(tile))
                {
                    continue;
                }
                // A diagonal screen has to be a legal bolt path, and strict
                // diagonal corners are a declared collision rule.
                if (dx != 0
                    && dy != 0
                    && (view.IsWall(new Position(tile.X - dx, tile.Y))
                        || view.IsWall(new Position(tile.X, tile.Y - dy))))
                {
                    continue;
                }
                tiles.Add(tile);
            }
        }
        return tiles;
    }
}
