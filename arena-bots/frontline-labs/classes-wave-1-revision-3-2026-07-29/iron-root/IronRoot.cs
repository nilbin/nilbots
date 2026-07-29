using BotArena.Sdk;

/// <summary>
/// IRON ROOT — the FORTRESS ROTATOR doctrine, revision 3 (RATCHET CLOCK).
///
/// One body fortifies forward: it walks to a legal covering tile beside the
/// active objective and commits to the transform windup only in a window where
/// nothing can punish it. Every other body stays mobile, screens the scoring
/// surface, and contests rather than concedes. When the front rotates and the
/// lanes stop crossing anything, the fortress spends its single return, walks
/// to the new line, and roots again.
///
/// Revision 2 priced that shape in <em>tenure</em>: a root had to buy a full
/// declared capture window of covering, relieved presence. That is still true,
/// and it was authored for a frontline that always reverts to the mean.
///
/// Revision 3 asks a question revision 2 could not: <b>whose presence counts
/// right now?</b> A capture definition may declare a hold, after which the
/// loser's captures are spent — the claim resets and the objective does not
/// move — and a lifecycle definition may place returns beside the fight instead
/// of at home. Under those, presence is not one currency but two, alternating:
/// inside our own hold a body on the surface buys ground and a zero-weight gun
/// buys nothing, and inside theirs it is exactly reversed. So the fortress is
/// now scheduled against the hold clock, the screen's depth is set by the
/// declared control arithmetic, and every trade of health for ground is priced
/// in the ticks a death actually costs — which the contract also declares, in
/// the return delay and in where the returning body appears.
///
/// Nothing below names a rule or an arm. Forms, routes, windups, reach,
/// cadence, objectives, unlock ticks, tile legality, movement/facing coupling,
/// hold ticks, control arithmetic, decay clock, return placement, and action
/// codes are all read from the resolved contract and the current legality mask,
/// so one artifact plays the unmodified contract, both structural levels, and
/// the numbers-only level without re-authoring.
/// </summary>
public sealed class IronRoot : IGenericActorBot
{
    /// <summary>Ticks of clear air that count as simply "safe".</summary>
    private const int SafeHorizon = 4;

    private ContractLens? _lens;
    private readonly RatchetClock _clock = new();
    private readonly Dictionary<string, (Position Tile, int Tick)> _seen =
        new(StringComparer.Ordinal);

    /// <summary>This tick's hold phase; see <see cref="RatchetClock"/>.</summary>
    private HoldPhase _phase = HoldPhase.None;
    private int _phaseRemaining;

    /// <summary>
    /// Whether the phase was watched rather than guessed. Two gates, not one,
    /// because the two kinds of decision have wildly different costs: a
    /// reversible preference (which tile to prefer, whether to refuse a root)
    /// may act on the displacement prior, since being wrong there costs at most
    /// one revision-2 decision. A one-use irreversible route may not.
    /// </summary>
    private bool _phaseCertain = true;

    /// <summary>The phase resolved to a side at all, guess or not.</summary>
    private bool _phaseTrusted;

    /// <summary>
    /// True when a death returns this body materially nearer the front than its
    /// authored spawn would. Recomputed per tick because it depends on where
    /// the front currently is.
    /// </summary>
    private bool _forwardReturn;

    private List<Position> _sites = [];
    private int _planIndex = -1;
    private int _planRange = -1;
    private int _bestCoverage;

    private int _siteFloor = 1;
    private int _lastHealth = -1;
    private int _lastDamageTick = int.MinValue / 4;
    private int _staticSinceTick = int.MaxValue;
    private int _blindSinceTick = int.MaxValue;
    private string? _veto;
    private Position? _blockedTile;
    private int _blockedThroughTick = -1;
    private Position? _dodgeOrigin;
    private int _dodgeThroughTick = -1;
    private readonly Dictionary<Position, int> _refusals = [];
    private readonly HashSet<Position> _denied = [];
    private int _refusalsClearedTick;

    /// <summary>
    /// This tick's mirror-fair direction order, taken once from the template
    /// helper so every tie-break in the tick — pathing, evasion, station
    /// choice — agrees with itself. An absolute preference is a measured
    /// team-side bias on a mirror-symmetric map; re-drawing it per call would
    /// also make one decision disagree with the next.
    /// </summary>
    private Direction[] _order = ArenaGeometry.Cardinals;

    private GenericActorRulesContract.MovementFacingCoupling _coupling
        = GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;

    public void StartLife(GenericActorMatchStart start)
    {
        _lens = new ContractLens(start);
        _seen.Clear();
        _sites = [];
        _planIndex = -1;
        _planRange = -1;
        _bestCoverage = 0;
        _lastHealth = -1;
        _lastDamageTick = int.MinValue / 4;
        _staticSinceTick = int.MaxValue;
        _blindSinceTick = int.MaxValue;
        _veto = null;
        _blockedTile = null;
        _blockedThroughTick = -1;
        _dodgeOrigin = null;
        _dodgeThroughTick = -1;
        _refusals.Clear();
        _denied.Clear();
        _refusalsClearedTick = 0;
        _siteFloor = 1;
        _order = ArenaGeometry.Cardinals;
        _coupling =
            GenericActorRulesContract.MovementFacingCoupling.PreserveFacing;
        _clock.Reset();
        _phase = HoldPhase.None;
        _phaseRemaining = 0;
        _phaseCertain = true;
        _phaseTrusted = false;
        _forwardReturn = false;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        try
        {
            return Decide(context);
        }
        catch (Exception)
        {
            // A bounded legal action always beats a fault.
            return SafeAction(context, "falling back to a legal action");
        }
    }

    private GenericActorDecision Decide(GenericActorContext context)
    {
        ContractLens? lens = _lens;
        if (lens is null)
            return SafeAction(context, "no contract");

        Observe(context);
        _order = ArenaBasics.OrderedDirections(lens.Contract, context);
        _coupling = lens.Coupling(context.Self.FormId);

        // A committed windup is wait-only by declaration; do not fight it.
        if (context.Self.PendingSameLifeTransition is not null)
            return SafeAction(context, "riding out the transform windup");

        GenericActorRulesContract.Form? form = lens.Form(context.Self.FormId);
        var mode = context.Mode
            as GenericActorContext.ModeObservationState.Frontline;
        int activeIndex = mode?.ActivePositionIndex ?? -1;

        // Whose progress is real this tick, and what a death costs. Both are
        // derived, both are inert on a contract that declares neither, and
        // everything below reads them instead of assuming the mean-reverting
        // world revision 2 was authored for.
        _clock.Update(lens, context, mode);
        _phase = _clock.Phase;
        _phaseRemaining = _clock.Remaining;
        _phaseCertain = _clock.Certain;
        _phaseTrusted =
            _phase is HoldPhase.Ours or HoldPhase.Theirs;
        _forwardReturn =
            lens.ForwardReturn(context.Self.ActorId.UnitId, activeIndex);

        Position[] active = lens.ObjectiveTiles(activeIndex);
        List<Gunnery.Target> targets = BuildTargets(lens, context, active);

        return lens.IsStatic(context.Self.FormId)
            ? FortressTick(lens, context, form, mode, active, targets)
            : FieldTick(lens, context, form, mode, active, targets, activeIndex);
    }

    // ---------------------------------------------------------------- rooted

    private GenericActorDecision FortressTick(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        GenericActorContext.ModeObservationState.Frontline? mode,
        Position[] active,
        List<Gunnery.Target> targets)
    {
        if (_staticSinceTick == int.MaxValue)
            _staticSinceTick = context.Tick;

        int reach = lens.Reach(context.Self.FormId);
        bool strict = lens.Attack(form)?.Projectile.DiagonalCornersMustBeClear
            ?? true;
        int coverage = FortressPlan.Coverage(
            lens.Map,
            context.Self.Position,
            active,
            reach,
            strict);

        // A single tick of zero coverage is a front that is about to rotate
        // back, an objective that momentarily has no tiles, or an observation
        // gap. Only a sustained blindness is the front actually moving, and
        // that is what the one-use return is for.
        if (coverage > 0)
            _blindSinceTick = int.MaxValue;
        else if (_blindSinceTick == int.MaxValue)
            _blindSinceTick = context.Tick;

        GenericActorDecision? mobilize = TryMobilize(lens, context, mode, coverage);
        if (mobilize is not null)
            return mobilize;

        GenericActorDecision? shot = Gunnery.TryFire(lens, context, form, targets);
        if (shot is not null)
            return shot;

        // Suppression beats concession: an idle gun that covers the scoring
        // surface keeps firing down it while the objective is not ours.
        bool pressed = mode is not null
            && (mode.ClaimingTeamId is int claimant && claimant != lens.TeamId
                || mode.CaptureProgress > 0 && mode.ClaimingTeamId != lens.TeamId);
        if (pressed || context.Enemies.Length > 0 || HeardTrouble(context, 3))
        {
            GenericActorDecision? suppress =
                Gunnery.TrySuppress(lens, context, form, active);
            if (suppress is not null)
                return suppress;
        }
        return SafeAction(context, "rooted and watching");
    }

    private GenericActorDecision? TryMobilize(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode,
        int coverage)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.ReverseRoute(context.Self.FormId);
        if (route is null)
            return null;

        int rooted = context.Tick - _staticSinceTick;
        int blind = _blindSinceTick == int.MaxValue
            ? 0
            : context.Tick - _blindSinceTick + 1;
        int mobileAllies = MobileAllyCount(lens, context);

        // The return is a rotation, not a reflex. Wave 1 spent it on transient
        // relief gaps — four fifths of all roots ended that way within a
        // couple of dozen ticks, and because the route is irreversible for the
        // life, the body could never root again. It is now spent on exactly
        // two things.
        //
        // One: the front has genuinely moved. Coverage has to have been zero
        // for the whole declared redeploy pause, which is the shortest time a
        // real advance can keep it that way — unless our own hold is live and
        // certain, in which case the reason coverage went to zero is not in
        // doubt at all: we advanced, and for the next Remaining ticks nothing
        // can bring the front back to these lanes. Waiting out the pause to
        // confirm what the clock already says is pure tempo thrown away.
        bool advanced = _phase == HoldPhase.Ours && _phaseCertain;
        bool rotate = coverage == 0
            && (advanced
                || (blind >= Math.Max(2, lens.RedeployPauseTicks)
                    && rooted >= Math.Max(2, lens.RedeployPauseTicks)));

        // Two: the clock has run down far enough that only a body physically
        // standing on the surface can still change the result, and there is no
        // other body to send. Walking there plus one capture window is the
        // honest cost of that errand — and a slot that is due back beside the
        // fight is a body to send, so this errand is not ours to run.
        int errand = lens.CaptureWindowTicks
            + Math.Max(1, route.Windup.DurationTicks)
            + ArenaGeometry.NearestDistance(
                context.Self.Position,
                lens.ObjectiveTiles(
                    mode?.ActivePositionIndex
                    ?? lens.NextObjectiveIndex(0)));
        bool lastCall = mobileAllies == 0
            && !ReliefDueForward(lens, context, errand)
            && lens.MaxTicks - context.Tick <= errand
            && rooted >= 2;

        // Their hold makes our presence worth nothing, so a return spent to
        // supply presence inside it is a one-use route spent on a reset.
        if (_phase == HoldPhase.Theirs
            && _phaseTrusted
            && _phaseRemaining > lens.CaptureThreshold)
        {
            lastCall = false;
        }

        if (!rotate && !lastCall)
            return null;

        return BuildTransition(
            context,
            route,
            rotate
                ? $"front rotated: unrooting after {rooted} rooted ticks"
                : "last call: unrooting to put a body on the surface");
    }

    // ---------------------------------------------------------------- mobile

    private GenericActorDecision FieldTick(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        GenericActorContext.ModeObservationState.Frontline? mode,
        Position[] active,
        List<Gunnery.Target> targets,
        int activeIndex)
    {
        _staticSinceTick = int.MaxValue;
        _blindSinceTick = int.MaxValue;

        // Union requirement: an explicitly fabricated companion is taken the
        // moment the mask offers one, exactly like a declared automatic one.
        GenericActorDecision? fabricate = TryFabricate(lens, context);
        if (fabricate is not null)
            return fabricate;

        var objective = new HashSet<Position>(active);
        bool holding = objective.Contains(context.Self.Position);
        EnsurePlan(lens, context, activeIndex);

        // A fortress has objective weight zero, so the role only exists while
        // the root can buy a tenure: relief already standing on the surface,
        // and a front that will still be this front for a full capture window.
        ActorIdentity? fortressActor = FortressActor(lens, context);
        bool worth = TenureAvailable(lens, context, mode, objective);
        bool fortress = worth
            && fortressActor is not null
            && fortressActor == context.Self.ActorId;
        HashSet<Position> goals = SelectGoals(
            lens,
            context,
            fortress,
            objective,
            active,
            activeIndex,
            worth ? fortressActor : null);
        bool stationed = goals.Count == 0 || goals.Contains(context.Self.Position);

        GenericActorDecision? shot = Gunnery.TryFire(lens, context, form, targets);

        // Respond to a bolt that lands before this body could get out of its
        // way — or to one that is further out while this tile has no exit at
        // all. Leaving costs one tick under a free-strafe profile and two under
        // a facing-locked one, so the trigger is the coupling's own arithmetic
        // rather than a fixed radius: a walled duel lane kills on the tick you
        // run out of room, not the tick the bolt gets close.
        int evade = Kinematics.EvadeCost(_coupling);
        int clock = TicksToImpact(lens, context, context.Self.Position);
        bool trapped = clock <= SafeHorizon
            && Outs(lens, context, context.Self.Position) == 0;
        if (clock <= evade + 1 || trapped)
        {
            // Objective-preserving response: while we are the body on the
            // scoring surface and can answer, we answer instead of stepping off.
            bool answer = holding && context.Self.Health > 1 && shot is not null;
            if (!answer)
            {
                GenericActorDecision? dodge =
                    TryDodge(lens, context, objective, goals, holding);
                if (dodge is not null)
                    return dodge;
            }
        }
        if (shot is not null)
            return shot;

        if (fortress)
        {
            GenericActorDecision? anchor = TryAnchor(lens, context, active);
            if (anchor is not null)
                return anchor;
        }

        // Under a profile where the step sets the facing, a rotation spent on a
        // body that is about to walk is a wasted tick: the next move overwrites
        // it. Travel is aiming there, so only a stationed body turns.
        bool aimByWalking = Kinematics.StepAlsoAims(_coupling) && !stationed;
        if (!aimByWalking)
        {
            GenericActorDecision? turn = TryAlign(
                lens,
                context,
                form,
                targets,
                active,
                activeIndex,
                stationed);
            if (turn is not null)
                return turn;
        }

        if (!stationed)
        {
            GenericActorDecision? step = TryStep(lens, context, goals);
            if (step is not null)
                return step;
        }
        _ = mode;
        return SafeAction(
            context,
            holding
                ? "holding the scoring surface"
                : fortress
                    ? $"on overwatch: {_veto ?? "waiting"}"
                    : "screening");
    }

    private static int AnchorWindup(
        ContractLens lens,
        GenericActorContext context)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.AnchorRoute(context.Self.FormId);
        return route is null ? 1 : Math.Max(1, route.Windup.DurationTicks);
    }

    private static GenericActorDecision? TryFabricate(
        ContractLens lens,
        GenericActorContext context)
    {
        foreach (GenericActorActionLegality action in lens.Available(
                     context,
                     GenericActorRulesContract.ActionKind.Fabrication))
        {
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in action.Constraints)
            {
                if (constraint is not GenericActorActionLegality
                        .ArgumentConstraint.UnitTargetConstraint units
                    || units.AllowedValues.IsEmpty)
                {
                    continue;
                }
                GenericActorActionArgument.UnitTarget target =
                    units.AllowedValues[0];
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [new GenericActorActionArgument.UnitTargetArgument(target)],
                    $"raising companion {target.TeamId}:{target.UnitId}");
            }
        }
        return null;
    }

    private GenericActorDecision? TryAnchor(
        ContractLens lens,
        GenericActorContext context,
        Position[] active)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.AnchorRoute(context.Self.FormId);
        if (route is null || active.Length == 0)
            return null;
        if (lens.TransitionForbidden.Contains(context.Self.Position))
            return null;

        int reach = lens.Reach(route.TargetFormId);
        bool strict =
            lens.Attack(lens.Form(route.TargetFormId))
                ?.Projectile.DiagonalCornersMustBeClear
            ?? true;
        int coverage = FortressPlan.Coverage(
            lens.Map,
            context.Self.Position,
            active,
            reach,
            strict);
        if (coverage <= 0 || coverage < _siteFloor)
        {
            _veto = "not a covering tile";
            return null;
        }

        var mode = context.Mode
            as GenericActorContext.ModeObservationState.Frontline;

        // Never root into a line that is about to move. Whoever completes the
        // capture, the scoring surface rotates away and the lanes stop meaning
        // anything, which is how a one-use return gets spent on nothing. The
        // tenure test above already refuses a front that moves inside a capture
        // window; this is the same refusal at the moment of commitment.
        //
        // Revision 3 asks the second half of that question, which only exists
        // once a hold is declared: an imminent completion moves the front only
        // if it is not about to be spent. A capture finishing inside the hold
        // that protects the other side resets and moves nothing, so refusing to
        // commit because of it is refusing for a reason that will not happen.
        bool completionWouldMove = mode?.ClaimingTeamId is not int claiming
            || _phase == HoldPhase.None
            || !_phaseTrusted
            || (claiming == lens.TeamId
                ? _phase != HoldPhase.Theirs
                : _phase != HoldPhase.Ours);
        if (mode is not null
            && completionWouldMove
            && lens.CaptureThreshold > 0
            && mode.CaptureProgress
                >= lens.CaptureThreshold - Math.Max(3, route.Windup.DurationTicks))
        {
            _veto = "front about to rotate";
            return null;
        }

        // While the objective is not accruing for us, a slightly riskier window
        // is the better trade: the stalemate itself is the thing being paid for.
        bool gaining = mode is not null && mode.ClaimingTeamId == lens.TeamId;
        int margin = gaining ? 2 : 1;

        int windup = Math.Max(1, route.Windup.DurationTicks);
        int hits = ExpectedWindupHits(lens, context, windup);
        if (context.Self.Health - hits < margin)
        {
            _veto = $"windup would cost {hits}";
            return null;
        }

        _veto = null;
        return BuildTransition(
            context,
            route,
            $"rooting: {coverage} covered objective tiles");
    }

    /// <summary>
    /// The windup is a visible, punishable commitment, so it is priced rather
    /// than merely feared: how much damage can actually land on this tile before
    /// the transition completes?
    ///
    /// <para>A muzzle only counts when it can occupy a tile with a real firing
    /// lane onto us <em>and be pointed down it</em> in time, at its own declared
    /// cadence. That second clause is what the movement/facing coupling
    /// changes. Where a step turns the body, an enemy that walks into a lane
    /// arrives facing its own travel direction and owes a rotation before it
    /// can fire; where a body may only move where it faces, it owes one turn to
    /// start travelling and another to aim on arrival. The punisher pays for
    /// its approach in the same currency the windup is spent in, so a forward
    /// root is genuinely cheaper on the coupled arms — and the arithmetic says
    /// so rather than the doctrine assuming it.</para>
    /// </summary>
    private int ExpectedWindupHits(
        ContractLens lens,
        GenericActorContext context,
        int windup)
    {
        int hits = 0;
        if (context.Tick - _lastDamageTick <= 2)
            hits++;
        if (HeardTrouble(context, 2))
            hits++;

        if (Threatened(lens, context, context.Self.Position))
            hits++;

        // Damage lands up to the tick before completion; a hit on the
        // completing tick no longer cancels anything.
        int exposure = Math.Max(0, windup - 1);

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? attack =
                lens.Attack(lens.Form(enemy.FormId));
            if (attack is null)
                continue;
            HashSet<Position> lanes = FortressPlan.FiringTilesOn(
                lens.Map,
                context.Self.Position,
                attack.Projectile.MaxTravelTiles,
                attack.Projectile.DiagonalCornersMustBeClear);

            int steps;
            if (lens.IsStatic(enemy.FormId))
            {
                // A rooted gun answers from where it stands or not at all.
                steps = lanes.Contains(enemy.Position) ? 0 : int.MaxValue;
            }
            else
            {
                steps = int.MaxValue;
                foreach (Position lane in lanes)
                {
                    steps = Math.Min(
                        steps,
                        enemy.Position.ChebyshevDistance(lane));
                }
            }

            bool aimed =
                ArenaGeometry.TryRay(
                    enemy.Position,
                    context.Self.Position,
                    out ProjectileHeading heading,
                    out _)
                && enemy.Facing.ToProjectileHeading() == heading;
            int ready = Kinematics.TicksToFirstShot(
                lens.Coupling(enemy.FormId),
                steps,
                attack.OmnidirectionalAim,
                aimed);
            if (ready > exposure)
                continue;

            int shots = 1
                + (exposure - ready) / Math.Max(1, attack.CooldownTicks);
            hits += shots * Math.Max(1, attack.Projectile.DamagePerHit);
        }
        return hits;
    }

    /// <summary>
    /// THE REVISION. A fortress cannot capture, so a root is only worth taking
    /// when it buys a <em>tenure</em>: a stretch of covering fire long enough
    /// for somebody else's presence to turn into territory.
    ///
    /// <para>Wave 1 asked a much weaker question — "does a companion exist, or
    /// is one declared to arrive soon?" — and it was answered yes at the moment
    /// the first companion unlocked, tick after tick, for the rest of the
    /// match. The measured result was a median rooted tenure of 12–25 ticks
    /// worth 0.4 kills, with the relief usually not on the surface at all.</para>
    ///
    /// <para>Three conditions, all read from the contract:</para>
    /// <list type="number">
    /// <item>relief is <em>in place</em> — an allied mobile body already on the
    /// scoring surface, or one step from it — not merely alive, and not merely
    /// due;</item>
    /// <item>the front will still be this front for the whole tenure: whoever
    /// is claiming, the remaining capture distance must exceed the windup plus
    /// a capture window;</item>
    /// <item>the clock leaves room to actually serve the tenure.</item>
    /// </list>
    ///
    /// <para>A contract that declares no companion slots at all never passes
    /// condition one, which is the correct answer: a lone body that fortifies
    /// has conceded the match.</para>
    /// </summary>
    private bool TenureAvailable(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode,
        HashSet<Position> objective)
    {
        int windup = AnchorWindup(lens, context);

        if (lens.MaxTicks - context.Tick < windup + lens.CaptureWindowTicks)
        {
            _veto = "no time left to serve a tenure";
            return false;
        }

        // THE REVISION, at the moment it decides something — and the place the
        // two counterweights turned out to interact.
        //
        // Our own hold is live: for the next Remaining ticks their captures are
        // spent and ours advance the front. It is tempting to read that as
        // "presence is doubled, never convert a body to zero weight here", and
        // that is exactly what the first draft did. Ablated on the plain-ratchet
        // cells it was worth **−21 points of territory**: removing the veto
        // recovered them. The same draft's contest cells were the ones gaining.
        // That is the arithmetic telling the doctrine something it had not
        // asked: *how much a second body is worth is not a property of the
        // hold.*
        //
        // Under binary control one body of positive weight nulls any number, so
        // the second and third bodies add no capture rate at all — during our
        // own hold the fortress is free suppression standing over ground that
        // cannot be lost, and refusing to build it is refusing the doctrine.
        // Under a net-weight policy every body is pressure, the hold doubles
        // what that pressure buys, and removing one from the count is a real
        // subtraction from a number that is briefly worth twice as much.
        //
        // So the veto is the control policy's, not the hold's.
        if (_phase == HoldPhase.Ours
            && _phaseTrusted
            && lens.SurplusWeightScalesGain)
        {
            _veto = $"our hold holds for {_phaseRemaining}: weight is pressure";
            return false;
        }

        // Their hold is live: our captures are spent for as long as it runs, so
        // weight on the surface buys only denial — and this class's answer to
        // "deny without weight" is a gun with three times the cadence, twice
        // the reach and no facing at all. That makes the windup cheap, and the
        // hold is what pays for it.
        //
        // It has to be a genuinely free window, not a guessed one. Measured the
        // permissive way — any trusted phase, any remainder — this rule doubled
        // the roots, halved sole presence and cost 37 points of territory in
        // its worst cell, because a losing position reads as "their hold" to
        // the displacement prior almost permanently. So: watched evidence only,
        // a remainder that outlasts the windup AND a whole capture window
        // (otherwise the ground is about to matter again before the turret is
        // even useful), and relief that is still standing either way.
        bool freeWindow = _phase == HoldPhase.Theirs
            && _phaseCertain
            && _phaseRemaining >= windup + lens.CaptureWindowTicks
            && MobileAllyCount(lens, context) > 0;

        if (!freeWindow && !ReliefInPlace(lens, context, objective, windup))
        {
            _veto = "no relief on the surface";
            return false;
        }
        if (freeWindow)
        {
            _veto = null;
            return true;
        }

        // Contest arithmetic changes what "relief" has to be. Where surplus
        // weight scales capture pressure, one body no longer nulls two: an ally
        // standing on a surface the opposition outweighs is not holding
        // anything, and removing this body's own weight from the count makes it
        // worse rather than free. Root only from a position that is already
        // net-positive without us.
        if (lens.SurplusWeightScalesGain)
        {
            (int own, int enemy, bool self) =
                ArenaBasics.ObjectivePresence(lens.Contract, context);
            int relief = own - (self ? SelfWeight(lens, context) : 0);
            if (relief <= enemy)
            {
                _veto = $"contest arithmetic: relief {relief} vs {enemy}";
                return false;
            }
        }

        // A front that is about to move *toward us* strands the fortress behind
        // the line; a front about to move *away* is the rotation the doctrine is
        // named for, and the return exists precisely to follow it. So only the
        // opponent's imminent capture is a veto.
        if (mode is not null
            && lens.CaptureThreshold > 0
            && mode.ClaimingTeamId is int claimant
            && claimant != lens.TeamId)
        {
            int remaining = lens.CaptureThreshold - mode.CaptureProgress;
            if (remaining <= windup + Math.Max(2, lens.RedeployPauseTicks))
            {
                _veto = $"they take the line in {remaining}";
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// An allied mobile body that is already holding the scoring surface, or
    /// close enough to be holding it before the windup ends. The fortress is
    /// the screen's asset, so the screen has to exist first.
    ///
    /// <para>Revision 2 refused to count a body that was merely <em>due</em>,
    /// and it was right to on a contract that returns bodies to a home pad: due
    /// there means due plus a walk across the map, which is how wave 1 rooted
    /// at the first unlock tick and stood alone for the rest of the match.
    /// Where the lifecycle definition places arrivals by the objective chain
    /// the same word means something else entirely — the body appears beside
    /// the front — so a return due inside the windup is relief that will
    /// genuinely be standing there, and refusing to count it throws away the
    /// counterweight's whole point.</para>
    /// </summary>
    private bool ReliefInPlace(
        ContractLens lens,
        GenericActorContext context,
        HashSet<Position> objective,
        int windup)
    {
        if (objective.Count == 0)
            return false;

        // How near "in place" has to be is a placement question. Under a
        // spawn-anchored return, a body two tiles off the surface may be two
        // tiles off for a long time, so revision 2 demanded one. Under a
        // chain-derived one an arriving body materialises on the own-side
        // objective — a fixed short walk from the front — so a reliever inside
        // the windup's own travel budget genuinely will be standing there.
        int radius = _forwardReturn ? Math.Max(1, windup) : 1;
        Position[] tiles = [.. objective];
        int mobile = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsStatic(ally.FormId)
                || ally.PendingSameLifeTransition is not null)
            {
                continue;
            }
            mobile++;
            if (ArenaGeometry.NearestDistance(ally.Position, tiles) <= radius)
                return true;
        }

        // A slot due back beside the front is relief — but only ever as a
        // *reinforcement*. The last mobile body on the team may not root
        // against a promise: that is precisely the wave-1 mistake, and letting
        // a due unlock license it put this doctrine's only scoring body into a
        // turret at tick 118 of a 500-tick match.
        if (!_forwardReturn || mobile == 0)
            return false;
        return ReliefDueForward(lens, context, windup + 1);
    }

    /// <summary>
    /// True when an allied slot is due back inside <paramref name="within"/>
    /// ticks <em>and</em> will appear beside the front rather than at home.
    /// Only then is "somebody else will be there" a plan rather than a hope.
    /// </summary>
    private bool ReliefDueForward(
        ContractLens lens,
        GenericActorContext context,
        int within)
    {
        if (!_forwardReturn)
            return false;
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.TeamId != lens.TeamId)
                continue;
            int due = slot.State switch
            {
                GenericActorContext.UnitSlotState.AutomaticReturnPending pending
                    => pending.DueTick,
                GenericActorContext.UnitSlotState.AvailabilityPending unlock
                    => unlock.DueTick,
                _ => int.MaxValue,
            };
            if (due != int.MaxValue && due - context.Tick <= within)
                return true;
        }
        return false;
    }

    /// <summary>This body's own objective weight, from its declared form.</summary>
    private static int SelfWeight(
        ContractLens lens,
        GenericActorContext context) =>
        lens.Form(context.Self.FormId)?.ObjectiveWeight ?? 0;

    private static GenericActorDecision? TryAlign(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form? form,
        List<Gunnery.Target> targets,
        Position[] active,
        int activeIndex,
        bool stationed)
    {
        List<GenericActorActionLegality> rotations = lens.Available(
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        if (rotations.Count == 0)
            return null;

        Direction? desired =
            Gunnery.AlignmentTurn(lens, context, form, targets)
            ?? (stationed ? IdleFacing(lens, context, active, activeIndex) : null);
        if (desired is not Direction direction || direction == context.Self.Facing)
            return null;

        foreach (GenericActorActionLegality rotation in rotations)
        {
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in rotation.Constraints)
            {
                if (constraint is GenericActorActionLegality.ArgumentConstraint
                        .DirectionConstraint directions
                    && directions.AllowedValues.Contains(direction))
                {
                    return new GenericActorDecision(
                        rotation.ActionId,
                        rotation.ActionCode,
                        [
                            new GenericActorActionArgument.DirectionArgument(
                                direction),
                        ],
                        $"laying the muzzle {direction}");
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Standing on station with nothing to shoot, a facing-locked gun is laid
    /// on the lane that matters: from the scoring surface, toward the direction
    /// the opponent must advance from; from overwatch, onto the surface itself.
    /// </summary>
    private static Direction? IdleFacing(
        ContractLens lens,
        GenericActorContext context,
        Position[] active,
        int activeIndex)
    {
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(lens.Form(context.Self.FormId));
        if (attack is null || attack.OmnidirectionalAim || active.Length == 0)
            return null;

        Position[] ahead = Contains(active, context.Self.Position)
            ? lens.ObjectiveTiles(lens.NextObjectiveIndex(activeIndex))
            : active;
        Position focus = ArenaGeometry.Centroid(
            ahead.Length > 0 ? ahead : active);
        int dx = focus.X - context.Self.Position.X;
        int dy = focus.Y - context.Self.Position.Y;
        if (dx == 0 && dy == 0)
            return null;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
    }

    /// <summary>
    /// Stations, not a scrum. Allied bodies would otherwise all path onto the
    /// same scoring tiles, block each other every tick, and screen nothing, so
    /// each body takes a distinct post derived from the same frozen observation:
    /// the fortress takes a covering tile, the senior screen takes the surface,
    /// and the rest take ranked overwatch.
    /// </summary>
    private HashSet<Position> SelectGoals(
        ContractLens lens,
        GenericActorContext context,
        bool fortress,
        HashSet<Position> objective,
        Position[] active,
        int activeIndex,
        ActorIdentity? fortressActor)
    {
        if (fortress && _sites.Count > 0)
        {
            var sites = new HashSet<Position>();
            foreach (Position site in _sites)
                sites.Add(site);
            return sites;
        }

        HashSet<Position> source =
            lens.FabricationSourceTiles(context.Self.FormId);
        if (source.Count > 0
            && ReadySlotExists(context)
            && !UrgentHold(lens, context, objective))
        {
            return source;
        }

        if (objective.Count == 0)
            return [];

        List<Position> stations = Stations(lens, context, active, activeIndex);
        if (stations.Count == 0)
            return objective;
        int rank = ScreenRank(lens, context, fortressActor);
        return [stations[Math.Min(rank, stations.Count - 1)]];
    }

    /// <summary>
    /// Distinct posts for the mobile bodies, best first, in an order every
    /// allied life derives identically from the same frozen observation.
    ///
    /// <para>Scoring tiles come first and relief posts one step off the surface
    /// come second. Revision 1 sent everything but the senior screen to distant
    /// overwatch tiles that merely <em>saw</em> the surface, and the replays
    /// measured the cost: nearly three hundred ticks a match of bodies standing
    /// somewhere with nothing to do, while every death of the one holder opened
    /// a presence gap the width of a walk back. A post beside the surface is an
    /// overwatch post that can also be standing on the objective next tick.</para>
    ///
    /// <para>Within the surface, a tile an allied fortress actually covers
    /// outranks one it does not — suppression only becomes territory when
    /// somebody is standing under it — and a tile no enemy gun sweeps outranks
    /// one that is swept.</para>
    /// </summary>
    private static List<Position> Stations(
        ContractLens lens,
        GenericActorContext context,
        Position[] active,
        int activeIndex)
    {
        HashSet<Position> hot = FortressPlan.HotTiles(lens, context);
        HashSet<Position> covered = CoveredSurface(lens, context, active);
        var surface = new HashSet<Position>(active);

        // TRIED AND REVERTED, with the measurement, because the reasoning was
        // good and the result was not. "Nearer home" is revision 2's last
        // tie-break, and under a contract that rallies arrivals forward the
        // home spawn is a corner of the map nothing will walk out of again — so
        // this ranked posts by distance from the own-side objective instead,
        // where reinforcements actually appear. Measured on the plain-ratchet
        // cells with the hold veto out of the way, it cost **52 points of
        // territory across ten cells** (−4 → −56). The home anchor is not
        // really "home": it is the rearmost point of our own approach, and
        // ranking posts by it puts bodies on the side of the objective the
        // opponent has to walk past. Where the reinforcements land is a fact
        // about the future; which side of the ground you stand on is a fact
        // about this tick, and the tie-break wanted the second one.
        Position? rally = lens.HomeAnchor;
        _ = activeIndex;

        var ranked = new List<(Position Tile, int Onto, int Cover, int Cool, int Home)>();
        void Offer(Position tile, int onto)
        {
            if (!ArenaGeometry.IsOpen(lens.Map, tile)
                || lens.SpawnProtected.Contains(tile))
            {
                return;
            }
            ranked.Add((
                tile,
                onto,
                covered.Contains(tile) ? 0 : 1,
                hot.Contains(tile) ? 1 : 0,
                rally is Position anchor
                    ? tile.ChebyshevDistance(anchor)
                    : 0));
        }

        foreach (Position tile in active)
            Offer(tile, 0);

        var ring = new HashSet<Position>();
        foreach (Position tile in active)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    Position neighbour = tile.Offset(dx, dy);
                    if (!surface.Contains(neighbour))
                        ring.Add(neighbour);
                }
            }
        }
        foreach (Position tile in ring)
            Offer(tile, 1);

        ranked.Sort(static (left, right) =>
        {
            int onto = left.Onto.CompareTo(right.Onto);
            if (onto != 0)
                return onto;
            int cover = left.Cover.CompareTo(right.Cover);
            if (cover != 0)
                return cover;
            int cool = left.Cool.CompareTo(right.Cool);
            if (cool != 0)
                return cool;
            int home = left.Home.CompareTo(right.Home);
            if (home != 0)
                return home;
            int x = left.Tile.X.CompareTo(right.Tile.X);
            return x != 0 ? x : left.Tile.Y.CompareTo(right.Tile.Y);
        });

        // Fill the surface before the ring, and measurably so. Interleaving the
        // two — one holder, one body a step off, one holder — was tried and
        // cost nine wins across the sparring sweep, because bodies standing on
        // the scoring tiles are not only presence: actors block actors, so a
        // full surface is ground the opponent cannot walk onto at all. Spread
        // exposure is worth less than denied entry.
        var posts = new List<Position>();
        foreach ((Position tile, int _, int _, int _, int _) in ranked)
            posts.Add(tile);
        return posts;
    }

    /// <summary>
    /// The subset of the active scoring surface that an allied fortress
    /// currently covers with its own declared reach. Empty when no ally is
    /// rooted, which is the ordinary case and falls back to the old rule.
    /// </summary>
    private static HashSet<Position> CoveredSurface(
        ContractLens lens,
        GenericActorContext context,
        Position[] active)
    {
        var covered = new HashSet<Position>();
        if (active.Length == 0)
            return covered;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!lens.IsStatic(ally.FormId))
                continue;
            GenericActorRulesContract.AttackProfile? attack =
                lens.Attack(lens.Form(ally.FormId));
            if (attack is null)
                continue;
            covered.UnionWith(FortressPlan.CoveredTiles(
                lens.Map,
                ally.Position,
                active,
                attack.Projectile.MaxTravelTiles,
                attack.Projectile.DiagonalCornersMustBeClear));
        }
        return covered;
    }

    /// <summary>
    /// This body's index among the allied bodies that are currently screening,
    /// in canonical identity order. Rank zero holds the scoring surface.
    /// </summary>
    private static int ScreenRank(
        ContractLens lens,
        GenericActorContext context,
        ActorIdentity? fortressActor)
    {
        int rank = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsStatic(ally.FormId)
                || ally.PendingSameLifeTransition is not null
                || ally.ActorId == fortressActor)
            {
                continue;
            }
            if (ally.ActorId.CompareTo(context.Self.ActorId) < 0)
                rank++;
        }
        return rank;
    }

    private static bool UrgentHold(
        ContractLens lens,
        GenericActorContext context,
        HashSet<Position> objective)
    {
        if (!objective.Contains(context.Self.Position))
            return false;
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode)
        {
            return false;
        }
        return mode.ClaimingTeamId == lens.TeamId && mode.CaptureProgress > 0;
    }

    private static bool ReadySlotExists(GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.State is GenericActorContext.UnitSlotState.Ready)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Walk the shortest route toward the station — turning onto it first when
    /// the contract says a body may only move where it faces.
    ///
    /// <para>The route is searched over every cardinal, not over the legality
    /// mask. A facing-locked profile offers exactly one movement direction, and
    /// pruning the search to it throws away every route that is not already
    /// straight ahead: the body then finds no step, falls through to a wait,
    /// and stands there. That is not a hypothetical — the unrepaired policy
    /// waited 78% of its ticks and never reached an objective on that arm.</para>
    /// </summary>
    private GenericActorDecision? TryStep(
        ContractLens lens,
        GenericActorContext context,
        HashSet<Position> goals)
    {
        if (!TryMovement(lens, context, out GenericActorActionLegality? move,
                out GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint? directions))
        {
            return null;
        }

        HashSet<Position> blocked = OccupiedTiles(lens, context);
        Direction? step = ArenaGeometry.FirstStep(
            lens.Map,
            context.Self.Position,
            goals,
            blocked,
            _order);
        if (step is not Direction direction)
            return null;

        if (directions!.AllowedValues.Contains(direction))
        {
            return new GenericActorDecision(
                move!.ActionId,
                move.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(direction)],
                $"advancing {direction}");
        }

        return Kinematics.TryTurnToTravel(
            lens,
            context,
            direction,
            $"turning to travel {direction}");
    }

    private GenericActorDecision? TryDodge(
        ContractLens lens,
        GenericActorContext context,
        HashSet<Position> objective,
        HashSet<Position> goals,
        bool holding)
    {
        if (!TryMovement(lens, context, out GenericActorActionLegality? move,
                out GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint? directions))
        {
            return null;
        }

        HashSet<Position> blocked = OccupiedTiles(lens, context);
        HashSet<Position> hot = FortressPlan.HotTiles(lens, context);
        Position[] posts = [.. goals];
        var legal = new HashSet<Direction>(directions!.AllowedValues);

        // Durability is this chassis's currency and territory is the only
        // thing worth spending it on. A body standing on the scoring surface
        // therefore eats the bolt and keeps the tile; it only steps off when
        // the hit would leave it too thin to hold anything at all. Sidestepping
        // *within* the surface is always allowed — that is evasion that costs
        // no ground.
        int incoming = IncomingDamage(
            lens,
            context,
            context.Self.Position,
            Kinematics.EvadeCost(_coupling));

        // How thin is too thin is a contract question, and the contract answers
        // it in where a dead body comes back. Under a spawn-anchored return a
        // death costs the walk across the map, so the last point of health is
        // worth more than the tile. Under a chain-derived one the replacement
        // appears beside the front, so the body is renewable and the ground is
        // not: hold until the hit is actually lethal.
        int floor = _forwardReturn ? 1 : 2;

        // …and where surplus weight scales capture pressure, "am I the margin?"
        // is a question with an arithmetic answer. Under binary control one body
        // nulls any number, so a second holder is only a blocker; under a
        // net-weight policy the body whose departure takes the difference from
        // positive to zero is personally holding the claim up. That body does
        // not step off a lane for anything short of a lethal hit.
        (int ownWeight, int enemyWeight, bool onSurface) =
            ArenaBasics.ObjectivePresence(lens.Contract, context);
        bool decisive = lens.SurplusWeightScalesGain
            && onSurface
            && ownWeight - enemyWeight > 0
            && ownWeight - enemyWeight - SelfWeight(lens, context) <= 0;
        if (decisive)
            floor = 1;
        bool desperate = context.Self.Health - incoming < floor;

        // …unless the mode says presence pays nothing right now. During the
        // declared pause after an advance the surface accrues for nobody, so
        // that is exactly the window to spend on getting out of a lane.
        bool paused = context.Mode
                is GenericActorContext.ModeObservationState.Frontline pause
            && pause.ControlResumesAtTick > context.Tick;
        bool surfaceOnly =
            holding && !desperate && !paused && objective.Count > 0;
        Direction? best = null;
        int bestScore = Score(
            lens,
            context,
            context.Self.Position,
            objective,
            posts,
            hot,
            holding,
            standingStill: true);

        // Consider every cardinal, not only the ones the mask offers. Under a
        // facing-locked profile the mask offers one, and the other three are
        // reachable next tick through a rotation — expensive, and sometimes
        // still the only way out of a walled lane, so they are priced rather
        // than hidden.
        foreach (Direction direction in _order)
        {
            Position destination =
                ArenaGeometry.Step(context.Self.Position, direction);
            if (!ArenaGeometry.IsOpen(lens.Map, destination)
                || blocked.Contains(destination))
            {
                continue;
            }
            if (surfaceOnly && !objective.Contains(destination))
                continue;
            int cost = Kinematics.EvadeCost(
                _coupling,
                context.Self.Facing,
                direction);
            if (cost > 1 && !legal.Contains(direction))
            {
                // The turn only helps if the bolt has not already arrived.
                int clock = TicksToImpact(lens, context, context.Self.Position);
                if (clock < cost)
                    continue;
            }
            int score = Score(
                lens,
                context,
                destination,
                objective,
                posts,
                hot,
                holding,
                standingStill: false)
                - 60 * (cost - 1);
            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        if (best is not Direction chosen)
            return null;

        if (!legal.Contains(chosen))
        {
            return Kinematics.TryTurnToTravel(
                lens,
                context,
                chosen,
                $"turning to slip the shot {chosen}");
        }

        // Do not walk straight back into the tile the shot was aimed at.
        _dodgeOrigin = context.Self.Position;
        _dodgeThroughTick = context.Tick + 1;
        return new GenericActorDecision(
            move!.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(chosen)],
            $"slipping the shot {chosen}");
    }

    /// <summary>
    /// Ranks a tile under fire. Time on the clock dominates, then having
    /// somewhere left to go — a tile inside a walled lane with two on-lane
    /// neighbours is a trap even when this tick is survivable — and only then
    /// the errand, the scoring surface, and whether the gun would bear.
    /// </summary>
    private int Score(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        HashSet<Position> objective,
        Position[] posts,
        HashSet<Position> hot,
        bool holding,
        bool standingStill)
    {
        // Safety saturates: three ticks of clear air is as good as ten, so the
        // errand still decides between two survivable tiles. Evasion that also
        // abandons the scoring surface is how a duel is won and a match lost.
        int clock = TicksToImpact(lens, context, tile);
        int safety = Math.Min(clock, 3);
        int score = 400 * safety
            + 40 * Math.Min(Outs(lens, context, tile), 3);

        // What a scoring tile is worth depends on whose progress is real. Our
        // own live hold is the one window in which standing there both builds
        // toward the next advance and cannot lose the last one, so it is worth
        // strictly more than the mean-reverting default; theirs is the window
        // in which it is only worth what denial is worth.
        int ground = _phase switch
        {
            HoldPhase.Ours when _phaseTrusted => 300,
            HoldPhase.Theirs when _phaseTrusted => 150,
            _ => 200,
        };
        if (objective.Contains(tile))
            score += ground;
        else if (holding)
            score -= 60;
        if (!hot.Contains(tile))
            score += 20;
        score -= 20 * ArenaGeometry.NearestDistance(tile, posts);
        score += 15 * Bearing(lens, context, tile);
        if (standingStill)
            score -= 5;   // break exact ties toward actually moving
        return score;
    }

    /// <summary>
    /// How many visible enemies this tile could be shot from, in the sense the
    /// body's own gun understands. A straight-only chassis fires along a
    /// cardinal facing, so an enemy sitting on a diagonal is simply not a
    /// target from here — a quarter of this doctrine's idle ticks in wave 1
    /// were exactly that, standing beside an enemy it could never point at.
    /// Cardinal alignment is therefore worth a small, explicit tie-break.
    /// </summary>
    private static int Bearing(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(lens.Form(context.Self.FormId));
        if (attack is null || context.Enemies.IsEmpty)
            return 0;
        int reach = attack.Projectile.MaxTravelTiles;
        bool strict = attack.Projectile.DiagonalCornersMustBeClear;
        int bearing = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (!ArenaGeometry.TryRay(
                    tile,
                    enemy.Position,
                    out ProjectileHeading heading,
                    out int distance)
                || distance > reach
                || !ArenaGeometry.ClearRay(lens.Map, tile, enemy.Position, strict))
            {
                continue;
            }
            if (attack.OmnidirectionalAim)
            {
                bearing++;
                continue;
            }
            foreach (Direction direction in ArenaGeometry.Cardinals)
            {
                if (direction.ToProjectileHeading() == heading)
                {
                    bearing++;
                    break;
                }
            }
        }
        return Math.Min(bearing, 2);
    }

    /// <summary>
    /// Open neighbours of a tile that no bolt is about to sweep, and that this
    /// body could actually reach in time. Under a facing-locked profile only
    /// the tile ahead is one tick away, so a corridor with its only exit behind
    /// the body is a trap even though it looks like an exit on the map.
    /// </summary>
    private int Outs(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        int outs = 0;
        foreach (Direction direction in _order)
        {
            Position neighbour = ArenaGeometry.Step(tile, direction);
            if (!ArenaGeometry.IsOpen(lens.Map, neighbour))
                continue;
            int cost = Kinematics.EvadeCost(
                _coupling,
                context.Self.Facing,
                direction);
            if (TicksToImpact(lens, context, neighbour) > SafeHorizon
                && TicksToImpact(lens, context, tile) >= cost)
            {
                outs++;
            }
        }
        return outs;
    }

    private static bool TryMovement(
        ContractLens lens,
        GenericActorContext context,
        out GenericActorActionLegality? move,
        out GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions)
    {
        move = null;
        directions = null;
        foreach (GenericActorActionLegality candidate in lens.Available(
                     context,
                     GenericActorRulesContract.ActionKind.Movement))
        {
            foreach (GenericActorActionLegality.ArgumentConstraint constraint
                     in candidate.Constraints)
            {
                if (constraint is GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint allowed)
                {
                    move = candidate;
                    directions = allowed;
                    return !allowed.AllowedValues.IsEmpty;
                }
            }
        }
        return false;
    }

    // ------------------------------------------------------------- awareness

    private void Observe(GenericActorContext context)
    {
        if (_lastHealth >= 0 && context.Self.Health < _lastHealth)
            _lastDamageTick = context.Tick;
        _lastHealth = context.Self.Health;

        // A tile can be individually legal and still never enterable — a
        // permanently reserved return spawn is the classic case. Counting
        // refusals turns "blocked again" into "stop routing through here",
        // and the periodic reset keeps a transient body from closing a lane
        // for the rest of the life.
        if (context.Tick - _refusalsClearedTick >= 50)
        {
            _refusals.Clear();
            _denied.Clear();
            _refusalsClearedTick = context.Tick;
        }
        if (context.Self.PreviousActionResolution
            is { Outcome: GenericActorActionResolution.ActionOutcome.Blocked }
                prior)
        {
            foreach (GenericActorActionArgument argument
                     in prior.AcceptedAction.Arguments)
            {
                if (argument
                    is not GenericActorActionArgument.DirectionArgument direction)
                {
                    continue;
                }
                Position refused = ArenaGeometry.Step(
                    context.Self.Position,
                    direction.Value);
                _blockedTile = refused;
                _blockedThroughTick = context.Tick;
                _refusals.TryGetValue(refused, out int count);
                _refusals[refused] = count + 1;
                if (count + 1 >= 3)
                    _denied.Add(refused);
            }
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            _seen[enemy.ActorId.ToString()] = (enemy.Position, context.Tick);
    }

    private List<Gunnery.Target> BuildTargets(
        ContractLens lens,
        GenericActorContext context,
        Position[] active)
    {
        var targets = new List<Gunnery.Target>();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            bool onSurface = Contains(active, enemy.Position);
            int near = active.Length == 0
                ? 99
                : ArenaGeometry.NearestDistance(enemy.Position, active);
            int weight = onSurface ? 6 : near <= 2 ? 4 : 2;
            if (lens.IsStatic(enemy.FormId))
                weight -= 3;
            if (enemy.PendingSameLifeTransition is not null)
                weight += 4;   // a visible windup is the cheapest kill available
            targets.Add(new Gunnery.Target(
                enemy.Position,
                Drift(enemy, context.Tick),
                enemy.Health,
                weight));
        }
        return targets;
    }

    private (int Dx, int Dy) Drift(
        GenericActorContext.ObservedEnemyState enemy,
        int tick)
    {
        if (!_seen.TryGetValue(enemy.ActorId.ToString(), out var previous)
            || previous.Tick != tick - 1)
        {
            return (0, 0);
        }
        return (
            Math.Clamp(enemy.Position.X - previous.Tile.X, -1, 1),
            Math.Clamp(enemy.Position.Y - previous.Tile.Y, -1, 1));
    }

    /// <summary>
    /// Ticks until the soonest hostile projectile occupies this tile, or
    /// <see cref="int.MaxValue"/> when none ever does. Counting the clock
    /// instead of testing a fixed radius is what stops a body walking two tiles
    /// deeper into a walled lane because the bolt was not "close enough" yet.
    /// </summary>
    private static int TicksToImpact(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        int soonest = int.MaxValue;
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == lens.TeamId)
                continue;
            if (projectile.Position == tile)
                return 0;
            if (!ArenaGeometry.TryRay(
                    projectile.Position,
                    tile,
                    out ProjectileHeading heading,
                    out int distance)
                || heading != projectile.Heading)
            {
                continue;
            }
            if (projectile.RemainingTiles >= 0
                && distance > projectile.RemainingTiles)
            {
                continue;
            }
            if (!ArenaGeometry.ClearRay(
                    lens.Map,
                    projectile.Position,
                    tile,
                    true))
            {
                continue;
            }
            int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
            int advances = (distance + perAdvance - 1) / perAdvance;
            int ticks = Math.Max(1, projectile.TicksUntilAdvance)
                + (advances - 1) * lens.FastestProjectileCadence;
            soonest = Math.Min(soonest, ticks);
        }
        return soonest;
    }

    private static bool Threatened(
        ContractLens lens,
        GenericActorContext context,
        Position tile) =>
        TicksToImpact(lens, context, tile) <= 2;

    /// <summary>
    /// Damage that lands on a tile within the next few ticks, at the heaviest
    /// declared hit. This is what a body decides to absorb rather than concede
    /// ground for, so it is counted rather than guessed at.
    /// </summary>
    private static int IncomingDamage(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        int withinTicks)
    {
        int hits = 0;
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == lens.TeamId)
                continue;
            if (!ArenaGeometry.TryRay(
                    projectile.Position,
                    tile,
                    out ProjectileHeading heading,
                    out int distance)
                || heading != projectile.Heading)
            {
                continue;
            }
            if (projectile.RemainingTiles >= 0
                && distance > projectile.RemainingTiles)
            {
                continue;
            }
            if (!ArenaGeometry.ClearRay(
                    lens.Map,
                    projectile.Position,
                    tile,
                    true))
            {
                continue;
            }
            int perAdvance = Math.Max(1, projectile.TilesPerAdvance);
            int advances = (distance + perAdvance - 1) / perAdvance;
            int ticks = Math.Max(1, projectile.TicksUntilAdvance)
                + (advances - 1) * lens.FastestProjectileCadence;
            if (ticks <= withinTicks)
                hits += lens.HeaviestHit;
        }
        return hits;
    }

    private static bool HeardTrouble(GenericActorContext context, int window)
    {
        foreach (GenericActorContext.ObservedSound sound
                 in context.HeardSounds ?? [])
        {
            bool violent = sound.Kind
                is GenericActorContext.EventKind.Attack
                or GenericActorContext.EventKind.Damage
                or GenericActorContext.EventKind.Destruction;
            if (violent
                && sound.Distance <= 1
                && context.Tick - sound.SourceTick <= window)
            {
                return true;
            }
        }
        return false;
    }

    private static int MobileAllyCount(
        ContractLens lens,
        GenericActorContext context)
    {
        int count = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!lens.IsStatic(ally.FormId))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Which body holds the fortress role, decided identically by every allied
    /// life from the same frozen observation: a body that is already rooted or
    /// mid-windup keeps it, otherwise it goes to the cheapest body to commit.
    ///
    /// <para>"Cheapest" is read from the contract, and it is the opposite of
    /// what revision 1 assumed. The windup is the exposure, so the shortest
    /// declared windup roots first. Then the body that is <em>not</em> the
    /// tick-zero slot roots, because that slot is the one the contract renews
    /// automatically — the renewable body is worth more standing on the
    /// scoring surface than standing still beside it. On a contract where only
    /// one form has an anchor route at all, both terms are moot and the route
    /// decides.</para>
    /// </summary>
    private static ActorIdentity? FortressActor(
        ContractLens lens,
        GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsStatic(ally.FormId)
                || ally.PendingSameLifeTransition is not null)
            {
                return ally.ActorId;
            }
        }

        ActorIdentity? best = null;
        int bestWindup = int.MaxValue;
        int bestRenewable = int.MaxValue;
        void Consider(ActorIdentity actor, string formId)
        {
            GenericActorRulesContract.FormTransition? route =
                lens.AnchorRoute(formId);
            if (route is null)
                return;
            int windup = Math.Max(1, route.Windup.DurationTicks);
            // Prefer the slot that is NOT automatically renewed.
            int renewable = actor.UnitId == lens.PrimeUnitId ? 1 : 0;
            if (best is null
                || windup < bestWindup
                || windup == bestWindup && renewable < bestRenewable
                || windup == bestWindup
                    && renewable == bestRenewable
                    && actor.CompareTo(best) < 0)
            {
                best = actor;
                bestWindup = windup;
                bestRenewable = renewable;
            }
        }

        Consider(context.Self.ActorId, context.Self.FormId);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            Consider(ally.ActorId, ally.FormId);
        return best;
    }

    private void EnsurePlan(
        ContractLens lens,
        GenericActorContext context,
        int activeIndex)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.AnchorRoute(context.Self.FormId);
        int reach = route is null
            ? lens.Reach(context.Self.FormId)
            : lens.Reach(route.TargetFormId);
        if (_planIndex == activeIndex && _planRange == reach)
            return;

        Position[] active = lens.ObjectiveTiles(activeIndex);
        bool strict = route is null
            || (lens.Attack(lens.Form(route.TargetFormId))
                ?.Projectile.DiagonalCornersMustBeClear ?? true);
        List<Position> ranked = FortressPlan.RankSites(
            lens,
            active,
            reach,
            strict,
            lens.HomeAnchor);
        _bestCoverage = ranked.Count == 0
            ? 0
            : FortressPlan.Coverage(lens.Map, ranked[0], active, reach, strict);

        // Only the top coverage tier is a fortress site. Walking to a tile that
        // sees half the surface and rooting there is how a fortress becomes
        // scenery; the body would rather keep walking to a real one.
        int floor = Math.Max(1, _bestCoverage - 1);
        _sites = [];
        foreach (Position site in ranked)
        {
            if (FortressPlan.Coverage(lens.Map, site, active, reach, strict)
                >= floor)
            {
                _sites.Add(site);
            }
        }
        _siteFloor = floor;
        _planIndex = activeIndex;
        _planRange = reach;
    }

    private HashSet<Position> OccupiedTiles(
        ContractLens lens,
        GenericActorContext context)
    {
        var blocked = new HashSet<Position>();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            blocked.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            blocked.Add(enemy.Position);
        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId != lens.TeamId)
                blocked.Add(projectile.Position);
        }
        if (_blockedTile is Position tile
            && context.Tick <= _blockedThroughTick + 1)
        {
            blocked.Add(tile);
        }
        if (_dodgeOrigin is Position vacated && context.Tick <= _dodgeThroughTick)
            blocked.Add(vacated);
        foreach (Position denied in _denied)
            blocked.Add(denied);
        return blocked;
    }

    // -------------------------------------------------------------- plumbing

    private static bool Contains(Position[] tiles, Position tile)
    {
        foreach (Position candidate in tiles)
        {
            if (candidate == tile)
                return true;
        }
        return false;
    }

    private static GenericActorDecision? BuildTransition(
        GenericActorContext context,
        GenericActorRulesContract.FormTransition route,
        string reason)
    {
        GenericActorActionLegality? action = context.Action(route.ActionId);
        if (action is null || !action.Available)
            return null;

        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            if (constraint is not GenericActorActionLegality.ArgumentConstraint
                .FormTargetConstraint forms)
            {
                continue;
            }
            if (!forms.AllowedFormIds.Contains(route.TargetFormId))
                return null;
            return new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [
                    new GenericActorActionArgument.FormTargetArgument(
                        route.TargetFormId),
                ],
                reason);
        }
        return action.Constraints.Length == 0
            ? GenericActorDecision.WithoutArguments(
                action.ActionId,
                action.ActionCode,
                reason)
            : null;
    }

    /// <summary>
    /// A guaranteed bounded, legal reply, following the template helper's
    /// tiering: an available wait, then an unavailable one, then any available
    /// parameterless action, then any available action whose arguments can be
    /// satisfied from its own mask, and finally the first declared entry.
    /// A deliberate fault is a protocol violation; a Blocked outcome is safe,
    /// so this never throws.
    /// </summary>
    private GenericActorDecision SafeAction(
        GenericActorContext context,
        string reason)
    {
        GenericActorActionLegality? anyWait = null;
        GenericActorActionLegality? fallback = null;
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            bool isWait = _lens?.KindOf(action.ActionId)
                    == GenericActorRulesContract.ActionKind.Wait
                || string.Equals(action.ActionId, "wait", StringComparison.Ordinal);
            if (isWait && action.Constraints.Length == 0)
            {
                if (action.Available)
                {
                    return GenericActorDecision.WithoutArguments(
                        action.ActionId,
                        action.ActionCode,
                        reason);
                }
                anyWait ??= action;
            }
            if (fallback is null && action.Available && action.Constraints.IsEmpty)
                fallback = action;
        }
        GenericActorActionLegality? chosen = anyWait ?? fallback;
        if (chosen is not null)
        {
            return GenericActorDecision.WithoutArguments(
                chosen.ActionId,
                chosen.ActionCode,
                reason);
        }

        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (!action.Available)
                continue;
            List<GenericActorActionArgument>? arguments = Satisfy(action);
            if (arguments is not null)
            {
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    arguments,
                    reason);
            }
        }

        GenericActorActionLegality last = context.ActionLegalities[0];
        return GenericActorDecision.WithoutArguments(
            last.ActionId,
            last.ActionCode,
            reason);
    }

    private static List<GenericActorActionArgument>? Satisfy(
        GenericActorActionLegality action)
    {
        var arguments = new List<GenericActorActionArgument>();
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            switch (constraint)
            {
                case GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint directions:
                    if (directions.AllowedValues.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument.DirectionArgument(
                            directions.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint units:
                    if (units.AllowedValues.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument.UnitTargetArgument(
                            units.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint forms:
                    if (forms.AllowedFormIds.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument.FormTargetArgument(
                            forms.AllowedFormIds[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint headings:
                    if (headings.AllowedValues.IsEmpty)
                        return null;
                    arguments.Add(
                        new GenericActorActionArgument
                            .ProjectileHeadingArgument(
                                headings.AllowedValues[0]));
                    break;
                default:
                    break;   // shot programs are optional payloads
            }
        }
        return arguments;
    }
}
