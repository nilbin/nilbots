using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// SparkLine — TEMPO ENGINE.
///
/// The doctrine is bodies-per-tick. Every companion slot is queued the instant
/// the contract makes it legal, children are placed as far forward as the
/// declared placement offsets allow, the fragile prime is kept alive only to
/// the extent that keeps it spending fabrication actions, and the objective
/// clock is won by never leaving the active position uncontested.
///
/// Revision 2 sharpens that into one sentence: <b>presence only pays while it
/// is SOLE presence</b>, which splits the doctrine into three regimes read from
/// the contract rather than from the map.
///
/// <list type="number">
/// <item>Holding alone — stand on the least-exposed tile of the active region.
/// Exposure is a static count of the firing lines the walls allow onto a tile
/// given the widest gun the ruleset declares, so a body can move out of a
/// sniping lane it cannot even see down.</item>
/// <item>Holding alone and safe — never spend a tile. A step from outside every
/// declared reach into one inside it buys a shot the opponent can answer, and
/// the capture clock was already mine. Declared remaining travel, not apparent
/// heading, decides whether a bolt in flight can actually arrive.</item>
/// <item>Contested — the clock is dead for both sides, so tiles are cheap and
/// only a firing line is expensive. Then, and only then, the body spends steps
/// to align its gun and accepts survivable damage to do it.</item>
/// </list>
///
/// Revision 4 is the kit pass, and its sentence is: <b>under keel the only
/// currency that cannot be taken from you is SURVIVING objective weight, and
/// the kit is what buys it — so spend the extra slot on a bearing, never on a
/// lane.</b> Four contract facts carry it, and each one switches itself off
/// where the contract omits it:
///
/// <list type="number">
/// <item><b>The hold is published now, so every body can price it.</b>
/// <c>holdOwnerTeamId</c>/<c>holdEndsAtTick</c> replace revision 3's
/// witnessed-advance inference. That inference was unavailable to a life born
/// inside a hold, which for a fabricator is most lives; asking instead turns a
/// behaviour a minority of bodies could reach into one all of them can.</item>
/// <item><b>Weight is targeted, not maximised.</b> Where surplus weight scales
/// gain, the objective wants exactly one more body than the enemy has on it;
/// where only enemy SOLE presence erodes a claim, matching his weight is a
/// full stop. Inside my own hold the ground behind me cannot be lost, so the
/// surplus walks forward; inside his, my capture would be deleted, so the
/// surplus neutralises and waits for the lapse.</item>
/// <item><b>Slots are priced by their rebuild clock.</b> A five-slot arm gives
/// the late slots a slower clock, so those bodies are genuinely dearer than
/// the early ones and the cheap body takes the exposed ground. On a flat-clock
/// arm the spread is zero and the term vanishes.</item>
/// <item><b>Bodies converge on distinct bearings.</b> A volley fan covers
/// <c>projectileCount</c> contiguous headings from one tile, an aegis shell
/// covers one facing quadrant, and a bolt stops on the first enemy body —
/// all three are beaten by two bodies whose bearings differ by more than the
/// widest fan the contract declares, and all three are fed by a queue.</item>
/// </list>
///
/// Every capability is discovered from the resolved contract and the per-tick
/// legality mask: fabrication route and placement offsets, unlock and rebuild
/// clocks, shot envelope and bend depth, per-projectile cadence and damage,
/// projectile guards, volley fans, stance routes and their automatic-return
/// budgets, objective regions, pad geometry, spawn reservations, and the
/// movement profile's facing coupling. The same policy therefore runs
/// unchanged on an explicit-fabricate contract, a pad-bound one, one where
/// companions activate automatically, on each movement-kinematics arm, and on
/// a chassis whose kit it does not own.
/// </summary>
public sealed class SparkLine : IGenericActorBot
{
    // The dodge horizon, in TICKS. Revision 3 counted projectile advances,
    // which is the same number only while every profile advances once per tick.
    // Ticks are what the clock is denominated in, and every bolt publishes the
    // cadence needed to convert.
    private const int ThreatHorizonTicks = 3;
    private const int PlacementRotateCooldown = 6;
    private const int PlacementRotateGain = 2;
    private const int CloseContactRange = 3;
    private const int MaxPrograms = 320;
    private const int BlockedMemoryTicks = 5;
    private const int SoleControlHoldTicks = 2;

    private readonly Dictionary<string, ShotProgram[]> _programCache =
        new(StringComparer.Ordinal);
    private readonly HashSet<Position> _occupied = [];
    private readonly List<GenericActorContext.ObservedProjectile> _hostile = [];
    private readonly Dictionary<ActorIdentity, (int Dx, int Dy)> _drift = [];

    // Tiles a declared lifecycle claim already owns. The observation publishes
    // these per tile now, so a body neither walks into a claim it cannot enter
    // nor queues a child onto a tile another bundle has already reserved.
    private readonly HashSet<Position> _reserved = [];

    // Visible bodies whose form deflects contacts inside its facing quadrant,
    // and visible bodies whose gun launches a fan. Both are read from the
    // contract by form, so both lists are empty on every arm without the kit.
    private readonly List<GenericActorContext.ObservedEnemyState> _guards = [];
    private readonly List<GenericActorContext.ObservedEnemyState> _fanners = [];

    private ContractLens? _lens;
    private Position[] _path = new Position[16];
    private int _placementRotateTick = -PlacementRotateCooldown - 1;
    private Position? _dodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;
    private readonly Position[] _blockedTile = new Position[4];
    private readonly int[] _blockedUntilTick = new int[4];
    private int _blockedSlot;
    private int _holdTicks;

    private Position[] _objective = [];
    private int[] _objectiveField = [];
    private Position[] _goals = [];
    private Direction[] _order = [];
    private bool _holdsObjective;
    private bool _atGoal;
    private bool _contested;
    private GenericActorRulesContract.MovementFacingCoupling _coupling;

    // Capture-clock state, all derived from the contract's capture definition
    // and the mode observation. None of it exists on a contract that declares
    // no hold and no weight scaling, which is exactly how the same artifact
    // plays the structural arms and the unmodified control.
    private int _lastKnownIndex = -1;
    private int _attributedAdvanceTick = int.MinValue;
    private bool _attributedAdvanceWasMine;
    private int _holdEndsAtTick = -1;
    private bool _holdIsMine;
    private bool _holdIsTheirs;
    private int _captureProgress;
    private int _gainIfHolding;
    private int _returnWalk;

    // Revision 4's weight arithmetic, all derived per tick from the capture
    // definition, the published hold, and observed objective weight.
    private int _ownWeight;
    private int _enemyWeight;
    private int _weightWanted;
    private int _bearingRank;
    private int _weightedAllies;
    private int _trajectories = 1;
    private bool _concentrate;

    // Stance hysteresis. Private memory survives a same-life form change, which
    // is exactly the continuity a stance needs: the tick this life last entered
    // its current form is what stops a body flapping in and out of a shield
    // faster than the contract's own punish window.
    private string? _lastFormId;
    private int _formSinceTick;

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _lens = new ContractLens(start);
        _programCache.Clear();
        _path = new Position[Math.Max(16, _lens.Width + _lens.Height)];
        _placementRotateTick = -PlacementRotateCooldown - 1;
        _dodgeOrigin = null;
        _avoidDodgeOriginThroughTick = -1;
        _holdTicks = 0;
        _lastKnownIndex = -1;
        _attributedAdvanceTick = int.MinValue;
        _attributedAdvanceWasMine = false;
    }

    /// <inheritdoc />
    public GenericActorDecision Tick(GenericActorContext context)
    {
        ContractLens lens = _lens
            ?? throw new InvalidOperationException("StartLife was not called.");

        if (context.Self.PendingSameLifeTransition is not null)
            return Fallback(context, lens, "committed to a form transition");

        Prepare(lens, context);

        // Order is the doctrine. Bodies first, then the ground, then the gun.
        // Companions are queued the instant they are legal; the elected holder
        // walks onto the contested tiles before it trades a long-range shot;
        // an unanswerable bolt is stepped out of rather than traded with,
        // because a body that survives keeps contesting the objective.
        //
        // Revision 3 inserts one step ahead of the ground: a capture that
        // would complete inside an enemy hold is not a win, it is fifteen
        // ticks of work deleted, so the body steps off the tiles it owns
        // rather than finish it.
        return TryFabricate(lens, context)
            ?? TryEvade(lens, context, lethalOnly: true)
            ?? TryStance(lens, context)
            ?? TryReachFabricationSource(lens, context)
            ?? TryEnterObjective(lens, context)
            ?? TryCloseOnObjective(lens, context)
            ?? TryEvade(lens, context, lethalOnly: false)
            ?? TryShoot(lens, context)
            ?? TryAim(lens, context)
            ?? TrySweepForContester(lens, context)
            ?? TryImproveStance(lens, context)
            ?? TryUnstickTheGun(lens, context)
            ?? TryAdvance(lens, context)
            ?? TryFaceApproach(lens, context)
            ?? Fallback(context, lens, "holding the front");
    }

    // ------------------------------------------------------------------
    // Per-tick shared state
    // ------------------------------------------------------------------

    private void Prepare(ContractLens lens, GenericActorContext context)
    {
        _objective = ActiveObjective(lens, context);
        _objectiveField = Tactics.DistanceField(
            lens,
            _objective.Length > 0 ? _objective : FallbackGoals(lens, context));

        _occupied.Clear();
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            _occupied.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            _occupied.Add(enemy.Position);

        // A visible tile can carry a lifecycle claim, and the observation says
        // so rather than leaving it to be inferred from a blocked move. A
        // permanent automatic-return anchor and a queued fabrication output are
        // different facts with the same consequence for a step, so both are
        // impassable; an enemy claim is additionally a body about to exist,
        // which is why it is worth not standing next to.
        _reserved.Clear();
        foreach (GenericActorContext.ObservedTile tile in context.VisibleTiles)
        {
            if (tile.SpawnReservation is not
                GenericActorContext.SpawnReservation claim)
            {
                continue;
            }
            _reserved.Add(tile.Position);
            // My own slot's return anchor is reserved against my TEAMMATES, not
            // against me, so leaving it walkable for this body is the contract
            // rather than an optimism. Every other claim — an ally's anchor, an
            // outstanding fabrication bundle, an enemy's queued output — is a
            // tile that will refuse the step or produce a body in it.
            if (claim.TeamId != lens.TeamId
                || claim.UnitId != context.Self.ActorId.UnitId)
            {
                _occupied.Add(tile.Position);
            }
        }

        // The kit's two threat shapes, resolved by form from the contract.
        // Both loops produce nothing at all on a cell whose classes own
        // neither skill, which is how the same artifact plays kit off and on.
        _guards.Clear();
        _fanners.Clear();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            string formId = PendingOrCurrentForm(
                enemy.FormId,
                enemy.PendingSameLifeTransition);
            if (lens.IsGuard(formId))
                _guards.Add(enemy);
            if (lens.FanOf(formId) > 1)
                _fanners.Add(enemy);
        }

        _hostile.Clear();
        if (context.VisibleProjectiles is { } projectiles)
        {
            foreach (GenericActorContext.ObservedProjectile projectile
                     in projectiles)
            {
                // Projectiles block movement and entering one is a contact, so
                // an occupied bolt tile is as impassable as a body.
                _occupied.Add(projectile.Position);
                if (projectile.OwnerTeamId != lens.TeamId)
                    _hostile.Add(projectile);
            }
        }

        _drift.Clear();
        foreach (GenericActorContext.ObservedEvent visible in context.VisibleEvents)
        {
            if (visible.Payload
                is not GenericActorContext.EventPayload.Movement movement)
            {
                continue;
            }
            if (movement.ActorId.TeamId == lens.TeamId)
                continue;
            _drift[movement.ActorId] = (
                movement.To.X - movement.From.X,
                movement.To.Y - movement.From.Y);
        }

        RememberBlocked(context);

        // Iteration order is the tie-break for otherwise equal candidates. An
        // absolute order (always try North, then East) is the same systematic
        // side bias on a mirror-symmetric map as an absolute preference, so
        // the shared helper orders by advance direction and randomizes the two
        // laterals from this life's deterministic stream.
        _order = ArenaBasics.OrderedDirections(lens.Contract, context);
        _coupling = lens.CouplingFor(context.Self.FormId);
        ResolveGunBreadth(lens, context);

        if (!string.Equals(_lastFormId, context.Self.FormId, StringComparison.Ordinal))
        {
            _lastFormId = context.Self.FormId;
            _formSinceTick = context.Tick;
        }

        _holdsObjective = Contains(_objective, context.Self.Position);
        ResolveCaptureClock(lens, context);
        ResolveReturnWalk(lens, context);
        _goals = ResolveGoals(lens, context);
        _atGoal = Contains(_goals, context.Self.Position);
        _contested = ResolveContested(lens, context);
    }

    /// <summary>
    /// How many distinct trajectories this body's gun can put on the board from
    /// one pose — and therefore whether the team's problem is COVERAGE or
    /// CAPTURE RATE. This single number is the hinge of revision 4 and it is
    /// read straight from the shot envelope the contract declares.
    ///
    /// <para>A gun with exactly one trajectory covers exactly one lane, so a
    /// body's whole contribution is the lane it stands on: the team's reach is
    /// the number of DISTINCT bearings it occupies, two bodies on one bearing
    /// are one gun, and piling a second body onto the objective buys capture
    /// ticks with a body that cannot defend the tiles it is standing on. A gun
    /// that bends covers several lanes from a fixed facing, so coverage stops
    /// being the binding constraint: an extra body on the ground is armed
    /// against the approaches by itself, and the constraint becomes the capture
    /// clock, which surplus weight multiplies directly.</para>
    ///
    /// <para>So the doctrine is one sentence with two branches —
    /// <b>concentrate what the gun can defend, spread what it cannot</b> — and
    /// the branch is chosen by the envelope rather than by an arm name, which is
    /// why the same artifact plays a straight-only cell and a universal-bend
    /// cell as if it had been written for each. Both branches were measured
    /// separately and each one loses badly on the other's cell; the measured
    /// asymmetry is in DX.md.</para>
    /// </summary>
    private void ResolveGunBreadth(ContractLens lens, GenericActorContext context)
    {
        _trajectories = 1;
        _concentrate = false;
        if (lens.AttackFor(context.Self.FormId) is not
            GenericActorRulesContract.AttackProfile profile)
        {
            return;
        }
        // An omnidirectional gun picks an absolute heading per shot, so its
        // breadth is the heading catalog rather than a program envelope.
        _trajectories = profile.OmnidirectionalAim ? 8 : ProgramsFor(profile).Length;
        _concentrate = _trajectories > 1;
    }

    /// <summary>
    /// Prices the ground from the capture definition instead of from habit.
    /// Three questions, all answered by contract fields that change nothing in
    /// the observation schema — which is why a bot that never reads them plays
    /// a structural arm as if it were the baseline:
    /// <list type="bullet">
    /// <item>Is a completed capture protected once it lands
    /// (<c>HoldTicks</c>), and is a hold running right now?</item>
    /// <item>Whose mark does that hold protect?</item>
    /// <item>Is a second weighted body a second unit of pressure
    /// (<c>SurplusWeightScalesGain</c>) or a redundant one?</item>
    /// </list>
    /// </summary>
    private void ResolveCaptureClock(ContractLens lens, GenericActorContext context)
    {
        _holdEndsAtTick = -1;
        _holdIsMine = false;
        _holdIsTheirs = false;
        _captureProgress = 0;
        _gainIfHolding = 0;
        _weightWanted = 0;

        (int own, int enemy, bool selfPresent) = ArenaBasics.ObjectivePresence(
            lens.Contract,
            context);
        _ownWeight = own;
        _enemyWeight = enemy;

        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline frontline)
        {
            _lastKnownIndex = -1;
            return;
        }
        if (lens.Capture is not ArenaBasics.CaptureRules capture)
        {
            _lastKnownIndex = frontline.ActivePositionIndex;
            return;
        }

        _captureProgress = frontline.CaptureProgress;

        // What one more tick of control is worth to me, counting myself even
        // when I am about to step on. Under a binary control policy that is
        // flatly the declared gain; under a weight-scaled one it is the net
        // surplus, which is the whole reason to stack rather than screen.
        int mine = ObjectiveWeightOf(lens, context.Self.FormId);
        int withMe = own + (selfPresent ? 0 : mine);
        _gainIfHolding = capture.SurplusWeightScalesGain
            ? Math.Max(0, withMe - enemy) * capture.GainPerSoleTeamTick
            : (withMe > 0 && enemy == 0 ? capture.GainPerSoleTeamTick : 0);

        // ASK, do not derive. The mode observation now publishes both halves
        // of the live hold — whose advance it protects and the tick the
        // protection lifts — and the two travel together or not at all. That
        // closes revision 3's worst structural gap: its owner attribution came
        // from watching the active index move, which a life created inside the
        // hold window cannot do, and a fabricator creates most of its bodies
        // inside somebody's window. The behaviour that was reachable by a
        // minority of bodies is now reachable by all of them.
        if (ArenaBasics.LiveHold(context) is ArenaBasics.Hold live)
        {
            _holdEndsAtTick = live.EndsAtTick;
            _holdIsMine = live.Mine;
            _holdIsTheirs = !live.Mine;
            _lastKnownIndex = frontline.ActivePositionIndex;
            ResolveWeightTarget(lens, capture, enemy);
            return;
        }

        // Fallback, and only for the case the field cannot cover: a contract
        // that DECLARES a hold duration while the observation publishes no
        // live hold. Then the redeploy clock still dates the advance exactly
        // (ControlResumesAtTick = captureTick + 1 + pause) and the sign of an
        // index change still names the team that made it. Kept because an
        // older observation schema is resolvable for archived replays, and
        // deliberately not consulted when the published fields are present.
        int advanceTick = int.MinValue;
        if (capture.HoldTicks is int hold
            && hold > 0
            && frontline.ControlResumesAtTick > 0)
        {
            advanceTick =
                frontline.ControlResumesAtTick - 1 - capture.RedeployPauseTicks;
            if (advanceTick >= 0 && context.Tick <= advanceTick + hold)
                _holdEndsAtTick = advanceTick + hold;
        }

        if (_lastKnownIndex >= 0
            && frontline.ActivePositionIndex != _lastKnownIndex
            && advanceTick != int.MinValue)
        {
            _attributedAdvanceTick = advanceTick;
            _attributedAdvanceWasMine =
                frontline.ActivePositionIndex - _lastKnownIndex == lens.AdvanceDelta;
        }
        _lastKnownIndex = frontline.ActivePositionIndex;

        if (_holdEndsAtTick >= 0)
        {
            // An unattributed hold is treated as MINE and every hold-dependent
            // behaviour stays gated on positive knowledge, exactly as in
            // revision 3: guessing "enemy" makes a fresh body hold its own
            // team's winning push back for up to a full hold.
            _holdIsMine = _attributedAdvanceTick != advanceTick
                || _attributedAdvanceWasMine;
            _holdIsTheirs = false;
        }

        ResolveWeightTarget(lens, capture, enemy);
    }

    /// <summary>
    /// How much objective weight this team should have standing on the active
    /// region right now — the revision's central number, and the reason a
    /// surplus is not simply piled onto the ground.
    ///
    /// <para>Three capture facts decide it, and all three are contract
    /// fields:</para>
    /// <list type="bullet">
    /// <item><b>Binary control</b> (<c>SurplusWeightScalesGain</c> false): one
    /// weighted body is the whole claim and the second adds nothing. Want one.
    /// This is the baseline and it is why revision 2's screen was correct
    /// there.</item>
    /// <item><b>Weight-scaled control</b>: gain is the net surplus, so the
    /// number that matters is the enemy's weight plus one. Piling on beyond
    /// that buys a marginal tick of clock at the price of a body that could be
    /// taking the next ground, and revision 3 measured the unbounded version of
    /// this as a loss on a three-slot arm. Capping it is the fix; the five-slot
    /// arm is what makes the cap reachable at all.</item>
    /// <item><b>Enemy-sole decay only</b>: empty and contested ticks preserve a
    /// claim, so MATCHING his weight is a full stop rather than a slow bleed.
    /// Inside an enemy hold, where my own completion would be deleted anyway,
    /// matching is all the ground is worth and the rest of the force is better
    /// placed on the objective the lapse will make live.</item>
    /// </list>
    /// </summary>
    private void ResolveWeightTarget(
        ContractLens lens,
        ArenaBasics.CaptureRules capture,
        int enemy)
    {
        if (!capture.SurplusWeightScalesGain || !_concentrate)
        {
            // Two reasons to want exactly one body, and they are different
            // facts. Binary control says a second body adds no pressure at all.
            // A single-trajectory gun says a second body adds pressure it
            // cannot defend: it covers the one lane the first body was already
            // covering, so the pair is one gun holding twice the exposure.
            // Either way presence is presence, and everything else screens.
            _weightWanted = 1;
            return;
        }

        // Inside an enemy hold a completed capture is spent, so the ground is
        // worth denial and nothing more. Where only his sole presence erodes my
        // claim, denial is exactly parity.
        if (_holdIsTheirs && lens.OnlyEnemySoleDecays)
        {
            _weightWanted = Math.Max(1, enemy);
            return;
        }

        _weightWanted = enemy + 1;
    }

    /// <summary>
    /// Whether the ground under this body is being shared. Two independent
    /// readings agree on it, and the second one sees through fog: a visible
    /// enemy with objective weight standing in the region, or the mode itself
    /// reporting that nobody is accumulating progress while I have been stood
    /// on the active region with positive weight and control has resumed.
    /// Sole presence would have made the claim mine, so an absent claim is
    /// someone else's body — whether or not a facing quadrant can see it.
    /// </summary>
    private bool ResolveContested(ContractLens lens, GenericActorContext context)
    {
        if (!_holdsObjective
            || ObjectiveWeightOf(lens, context.Self.FormId) <= 0)
        {
            _holdTicks = 0;
            return false;
        }
        _holdTicks++;

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (ObjectiveWeightOf(lens, enemy.FormId) > 0
                && Contains(_objective, enemy.Position))
            {
                return true;
            }
        }

        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline frontline)
        {
            return false;
        }
        if (frontline.ClaimingTeamId == lens.TeamId)
            return false;
        // A freshly advanced front pauses before it can progress; an absent
        // claim during the declared pause proves nothing.
        if (context.Tick < frontline.ControlResumesAtTick)
            return false;
        return _holdTicks >= SoleControlHoldTicks;
    }

    /// <summary>
    /// A joint step can block an individually legal move, and re-submitting it
    /// next tick usually blocks again. Remembering the refused destination for
    /// a few ticks converts a two-tick deadlock into a detour.
    /// </summary>
    private void RememberBlocked(GenericActorContext context)
    {
        Position destination = BlockedDestination(context);
        if (destination.X < 0)
            return;
        for (int index = 0; index < _blockedTile.Length; index++)
        {
            if (_blockedUntilTick[index] > context.Tick
                && _blockedTile[index] == destination)
            {
                _blockedUntilTick[index] = context.Tick + BlockedMemoryTicks;
                return;
            }
        }
        _blockedTile[_blockedSlot] = destination;
        _blockedUntilTick[_blockedSlot] = context.Tick + BlockedMemoryTicks;
        _blockedSlot = (_blockedSlot + 1) % _blockedTile.Length;
    }

    private bool RecentlyBlocked(Position tile, int tick)
    {
        for (int index = 0; index < _blockedTile.Length; index++)
        {
            if (_blockedUntilTick[index] > tick && _blockedTile[index] == tile)
                return true;
        }
        return false;
    }

    private static Position[] ActiveObjective(
        ContractLens lens,
        GenericActorContext context)
    {
        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline frontline)
        {
            return lens.ObjectiveAt(frontline.ActivePositionIndex);
        }
        return [];
    }

    private static Position[] FallbackGoals(
        ContractLens lens,
        GenericActorContext context)
    {
        if (!context.Enemies.IsEmpty)
        {
            var tiles = new Position[context.Enemies.Length];
            for (int index = 0; index < context.Enemies.Length; index++)
                tiles[index] = context.Enemies[index].Position;
            return tiles;
        }
        return [lens.ForwardPoint];
    }

    /// <summary>
    /// How far a body returning through an automatic arrival would have to
    /// walk to reach the region its fabrication is bound to. Zero when
    /// arrivals land at the slot's spawn anchor, because then the return
    /// already puts the body where it works.
    /// </summary>
    private void ResolveReturnWalk(ContractLens lens, GenericActorContext context)
    {
        _returnWalk = 0;
        if (!lens.ArrivalsRallyForward || lens.FabricationSourceTiles.Count == 0)
            return;

        int best = Tactics.Unreachable;
        foreach (Position tile in ArenaBasics.ExpectedArrivalTiles(
                     lens.Contract,
                     context))
        {
            best = Math.Min(best, lens.WalkToSource(tile));
        }
        _returnWalk = best >= Tactics.Unreachable ? 0 : best;
    }

    /// <summary>
    /// Splits the team between the objective's WANTED weight and everything
    /// else. Revision 3 elected exactly one occupier and sent the rest forward
    /// or to a screen; revision 4 elects as many as the capture arithmetic
    /// actually pays for — <see cref="_weightWanted"/> — and no more.
    ///
    /// <para>Both ends of that are contract reads. Under a binary control
    /// policy presence is presence: the second body on the same tile set adds
    /// no pressure, the number is one, and the surplus screens the approach —
    /// exactly revision 2's behaviour, unchanged. Under a weight-scaled policy
    /// the number is the enemy's observed weight plus one, because that is
    /// where the net surplus stops growing; under an enemy-sole decay clock
    /// inside an enemy hold it is bare parity, because my completion there
    /// would be deleted and parity already freezes his.</para>
    ///
    /// <para>The cap is the point. Revision 3 measured the UNCAPPED version of
    /// this — send every weighted body to the ground under contest-majority —
    /// and it lost twice, once broadly and once narrowed to a contested
    /// objective. The reason was never that weight is worthless; it was that
    /// the bodies came out of the screen that kept the enemy off the ground in
    /// the first place. Spending exactly enough keeps the screen and buys the
    /// clock, and the five-slot arm is what makes both affordable at once.</para>
    /// </summary>
    private Position[] ResolveGoals(ContractLens lens, GenericActorContext context)
    {
        _bearingRank = 0;
        _weightedAllies = 0;
        if (_objective.Length == 0)
            return FallbackGoals(lens, context);
        if (ObjectiveWeightOf(lens, context.Self.FormId) <= 0)
            return _objective;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (ObjectiveWeightOf(lens, ally.FormId) > 0)
                _weightedAllies++;
        }

        long myRank = OccupierRank(
            lens,
            context.Self.Position,
            context.Self.FormId,
            context.Self.ActorId,
            _weightedAllies > 0);
        // How many weighted allies out-rank me for the ground. That ordinal is
        // both the occupier election and this body's bearing index, and it is
        // computed from shared observation only — no team memory, no implicit
        // leader, and stable across the fresh instances a fabricator makes.
        int ahead = 0;
        int weightAhead = 0;
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            int weight = ObjectiveWeightOf(lens, ally.FormId);
            if (weight <= 0)
                continue;
            if (OccupierRank(
                    lens,
                    ally.Position,
                    ally.FormId,
                    ally.ActorId,
                    hasCompany: true)
                >= myRank)
            {
                continue;
            }
            ahead++;
            weightAhead += weight;
        }
        _bearingRank = ahead;

        int wanted = Math.Max(1, _weightWanted);
        if (weightAhead < wanted)
            return _objective;

        Position[] forward = NextGroundToTake(lens, context);
        if (forward.Length > 0)
            return forward;

        Position[] screen = ScreenTiles(lens);
        return screen.Length > 0 ? screen : _objective;
    }

    /// <summary>
    /// A soft cost for standing where one enemy decision can answer two of my
    /// bodies at once, plus the deterministic tile preference that keeps two
    /// occupiers off one tile without any shared state.
    ///
    /// <para>Three separate contract facts converge on the same instruction.
    /// A volley profile launches <c>projectileCount</c> bolts down contiguous
    /// adjacent headings from one tile, so bodies inside that spread are one
    /// cast rather than two. A guard form deflects one facing quadrant, so
    /// bodies approaching through it are one arc rather than two. And an
    /// ordinary bolt stops on the first enemy body, so a queue in a lane is a
    /// gun that only ever answers the body at the front. The required
    /// separation is read from the widest fan the contract declares — one on
    /// every arm without a volley, where the term collapses to the tile
    /// preference alone.</para>
    ///
    /// <para>It is DISABLED whenever the gun is broad enough to concentrate.
    /// Separation is what a one-lane gun buys reach with; a bending gun already
    /// has the reach and pays for separation in the capture rate that surplus
    /// weight was going to multiply. Applying both branches at once was measured
    /// and it is the worst of the four configurations — the two halves of this
    /// revision fight each other exactly, which is the finding.</para>
    /// </summary>
    private int SpreadCost(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        int cost = 0;
        if (_concentrate)
            return 0;
        if (_weightedAllies > 0)
        {
            int separation = Math.Max(1, lens.WidestFan - 1);
            foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            {
                if (ObjectiveWeightOf(lens, ally.FormId) <= 0)
                    continue;
                int gap = ally.Position.ChebyshevDistance(tile);
                if (gap == 0)
                    cost += 4;
                else if (gap <= separation)
                    cost += 2;
            }
        }
        // Distinct bearings need distinct destinations, and the only ordering
        // every one of my lives agrees on without talking is the objective's
        // own declared tile order against this body's election ordinal.
        if (_objective.Length > 1)
        {
            int index = Array.IndexOf(_objective, tile);
            if (index >= 0 && index % _objective.Length
                != _bearingRank % _objective.Length)
            {
                cost += 1;
            }
        }
        return cost;
    }

    /// <summary>
    /// The objective one step past this one, for the bodies that are not
    /// holding the current one — but only once the current capture is close
    /// enough to completion that the walk and the advance land together.
    ///
    /// This is what a hold is actually worth. Where a completed advance can be
    /// pushed straight back, ground taken ahead of the front is ground that
    /// may have to be given back, and screening the objective I am capturing
    /// is the safer investment. Where the contract locks an advance for a
    /// declared number of ticks, a capture that is about to land is as good as
    /// landed: the front is going to move and it is going to stay moved, so
    /// the surplus should already be walking to the ground it will be fighting
    /// over rather than guarding ground that is about to stop being contested.
    ///
    /// The lead time is derived, not guessed: leave when the walk is no
    /// shorter than what is left of the claim, so the body arrives as the
    /// redeploy pause ends instead of starting the trip then.
    ///
    /// It also takes BOTH structural reads to make the trade pay, which is the
    /// most useful thing this revision learned. The body being sent forward is
    /// the one that was screening the approach, and what a screen is worth
    /// depends on the control policy. Under binary control one enemy body
    /// nulls any number of mine, so the screen that keeps him off the ground
    /// is the whole claim, and spending it on a foothold loses more than the
    /// foothold gains. Under weight-scaled control he cannot null me — he can
    /// only subtract one — so the screen's job shrinks to something the
    /// occupier can absorb and the surplus is finally free to leave. Sticky
    /// ground says the trip is safe; contest weight says it is affordable.
    /// </summary>
    private Position[] NextGroundToTake(
        ContractLens lens,
        GenericActorContext context)
    {
        if (lens.Capture is not ArenaBasics.CaptureRules capture
            || capture.HoldTicks is null
            || !capture.SurplusWeightScalesGain
            || _gainIfHolding <= 0)
        {
            return [];
        }
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline frontline)
        {
            return [];
        }
        // Only my own claim is about to move the front, and only when it is
        // not going to be spent inside somebody else's hold.
        if (frontline.ClaimingTeamId != lens.TeamId || !_holdIsMine)
            return [];

        int remaining = capture.Threshold - _captureProgress;
        if (remaining <= 0)
            return [];
        int ticksToCapture = (remaining + _gainIfHolding - 1) / _gainIfHolding;

        Position[] ahead = lens.ObjectiveAt(
            frontline.ActivePositionIndex + lens.AdvanceDelta);
        if (ahead.Length == 0)
            return [];

        int[] field = Tactics.DistanceField(lens, ahead);
        int walk = Tactics.DistanceAt(lens, field, context.Self.Position);
        if (walk >= Tactics.Unreachable)
            return [];
        return walk >= ticksToCapture ? ahead : [];
    }

    private long OccupierRank(
        ContractLens lens,
        Position position,
        string formId,
        ActorIdentity actorId,
        bool hasCompany)
    {
        int distance = Tactics.DistanceAt(lens, _objectiveField, position);
        // A body that can still fabricate is worth more behind the line than
        // standing on it, but only while somebody else can hold the ground.
        //
        // How much more is a contract fact, and it is the same fact that
        // decides whether bodies are worth anything beyond the first. Where
        // control is binary, a fourth body adds no capture pressure at all, so
        // protecting the fabricator past this point buys nothing and costs
        // presence. Where surplus weight scales the gain, every companion it
        // ever builds is worth a share of the clock, and the price of losing
        // it is the walk from wherever a forward-rally contract puts it back
        // to the region its fabrication is bound to — which
        // <see cref="ContractLens.WalkToSource"/> derives exactly rather than
        // assuming the body wakes up at its own workbench.
        if (hasCompany && lens.CanFabricate(formId))
            distance += 2 + (lens.SurplusWeightScalesGain ? _returnWalk : 0);

        // No term here for a slot's rebuild clock, and that absence is measured
        // rather than an omission. The five-slot arm gives its late slots a
        // 30-tick clock against the early slots' 15, so those bodies really are
        // dearer to lose — but ranking them behind the cheap ones costs more
        // than it saves. Under weight-scaled capture a thinner objective costs
        // gain on the very next tick, while a rebuild clock is a contingent
        // future loss; and the body already standing on the ground is worth
        // more than a cheaper body two tiles away regardless of what either
        // costs to replace. Priced, it took the kit-on cell from +108 to +90
        // per seed and flipped one seed from +112 to 0. See DX.md.
        long rank = (long)distance * 4096;
        rank += (long)actorId.UnitId * 64;
        return rank + actorId.LifeId;
    }

    private Position[] ScreenTiles(ContractLens lens)
    {
        int objectiveReach = Tactics.NearestChebyshev(lens.ForwardPoint, _objective);
        var tiles = new List<Position>();
        for (int y = 0; y < lens.Height; y++)
        {
            for (int x = 0; x < lens.Width; x++)
            {
                var tile = new Position(x, y);
                if (lens.IsClosed(tile) || Contains(_objective, tile))
                    continue;
                int reach = Tactics.NearestChebyshev(tile, _objective);
                if (reach is < 1 or > 2)
                    continue;
                if (tile.ChebyshevDistance(lens.ForwardPoint) > objectiveReach)
                    continue;
                tiles.Add(tile);
            }
        }
        return tiles.ToArray();
    }

    private static int ObjectiveWeightOf(ContractLens lens, string formId) =>
        lens.Form(formId)?.ObjectiveWeight ?? 1;

    private static bool Contains(Position[] tiles, Position tile)
    {
        for (int index = 0; index < tiles.Length; index++)
        {
            if (tiles[index] == tile)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // 1. Fabrication — the doctrine's first claim on every tick
    // ------------------------------------------------------------------

    private GenericActorDecision? TryFabricate(
        ContractLens lens,
        GenericActorContext context)
    {
        GenericActorActionLegality? fabricate = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Fabrication);
        if (fabricate is null)
            return null;

        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = Constraint<GenericActorActionLegality.ArgumentConstraint
                .UnitTargetConstraint>(fabricate);
        if (targets is null || targets.AllowedValues.IsEmpty)
            return null;

        GenericActorActionArgument.UnitTarget target = targets.AllowedValues[0];

        if (TryImprovePlacement(lens, context) is Direction facing)
        {
            _placementRotateTick = context.Tick;
            GenericActorDecision? rotate = Rotate(
                lens,
                context,
                facing,
                $"turning so companion {target.TeamId}:{target.UnitId} lands forward");
            if (rotate is not null)
                return rotate;
        }

        return new GenericActorDecision(
            fabricate.ActionId,
            fabricate.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(target)],
            $"queueing companion {target.TeamId}:{target.UnitId}");
    }

    /// <summary>
    /// The host takes the first eligible declared offset relative to the source
    /// pose at queue time, so facing is the only lever a fabricator has over
    /// where its child appears. When the front is quiet, spend one tick turning
    /// so the child lands toward the objective instead of behind it.
    /// </summary>
    private Direction? TryImprovePlacement(
        ContractLens lens,
        GenericActorContext context)
    {
        if (lens.PlacementOffsets.IsDefaultOrEmpty || _objective.Length == 0)
            return null;
        if (context.Tick - _placementRotateTick <= PlacementRotateCooldown)
            return null;
        if (ImpactIn(lens, context.Self.Position) <= ThreatHorizonTicks)
            return null;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (enemy.Position.ChebyshevDistance(context.Self.Position)
                <= CloseContactRange + 1)
            {
                return null;
            }
        }

        GenericActorActionLegality? rotate = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        if (rotate is null)
            return null;
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = Constraint<GenericActorActionLegality.ArgumentConstraint
                .DirectionConstraint>(rotate);
        if (allowed is null)
            return null;

        int current = PlacementScore(lens, context, context.Self.Facing);
        int bestScore = current;
        Direction? best = null;
        foreach (Direction candidate in _order)
        {
            if (candidate == context.Self.Facing
                || !allowed.AllowedValues.Contains(candidate))
            {
                continue;
            }
            int score = PlacementScore(lens, context, candidate);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return current - bestScore >= PlacementRotateGain ? best : null;
    }

    private int PlacementScore(
        ContractLens lens,
        GenericActorContext context,
        Direction facing)
    {
        Position? tile = PredictChildTile(lens, context, facing);
        return tile is Position placed
            ? Tactics.NearestChebyshev(placed, _objective)
            : Tactics.Unreachable;
    }

    private Position? PredictChildTile(
        ContractLens lens,
        GenericActorContext context,
        Direction facing)
    {
        (int forwardX, int forwardY) = facing.Vector();
        (int rightX, int rightY) = facing.TurnedRight().Vector();
        foreach (GenericActorRulesContract.RelativePositionOffset offset
                 in lens.PlacementOffsets)
        {
            Position tile = context.Self.Position.Offset(
                (offset.Forward * forwardX) + (offset.Right * rightX),
                (offset.Forward * forwardY) + (offset.Right * rightY));
            if (lens.IsWall(tile))
                continue;
            if (lens.FabricationOutputTiles.Count > 0
                && !lens.FabricationOutputTiles.Contains(tile))
            {
                continue;
            }
            // Two reservation facts, one consequence. The static one is my
            // slots' authored return anchors, which the contract reserves for
            // the whole match; the live one is every outstanding lifecycle
            // claim, which the observation now publishes per tile — including
            // bundles my own earlier fabrications still hold open. A tile under
            // either claim is a placement the host will refuse.
            if (lens.ReservedSpawnTiles.Contains(tile)
                || _reserved.Contains(tile)
                || _occupied.Contains(tile))
            {
                continue;
            }
            return tile;
        }
        return null;
    }

    /// <summary>
    /// The form a body will be in once its declared windup completes, or its
    /// current one when nothing is pending. A stance entry is public — the
    /// windup carries the target form and its due tick — so an opponent's
    /// commitment is readable two ticks before its gun changes shape, and
    /// pricing the form it is BECOMING is what makes that public data worth
    /// anything.
    /// </summary>
    private static string PendingOrCurrentForm(
        string formId,
        GenericActorContext.PendingSameLifeTransition? pending) =>
        pending?.TargetFormId ?? formId;

    // ------------------------------------------------------------------
    // 2. Evasion that refuses to concede the objective for free
    // ------------------------------------------------------------------

    /// <summary>
    /// Evasion is a comparison, not a reflex. Each candidate tile is rated by
    /// how many projectile advances away its first contact is and by how many
    /// escape hatches it offers afterwards; the body only spends the tick when
    /// some tile is strictly safer than standing still, and never when leaving
    /// would hand over an objective it is currently the reason for holding.
    /// </summary>
    private GenericActorDecision? TryEvade(
        ContractLens lens,
        GenericActorContext context,
        bool lethalOnly)
    {
        int stayImpact = ImpactIn(lens, context.Self.Position);
        if (stayImpact > ThreatHorizonTicks)
            return null;

        bool lethal = IncomingDamageAt(lens, context.Self.Position)
            >= context.Self.Health;
        if (lethalOnly && !lethal)
            return null;

        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        long bestScore = SafetyScore(
            stayImpact,
            EscapeCount(lens, context.Self.Position),
            _holdsObjective,
            KeepsFiringSolution(lens, context, context.Self.Position, context.Self.Facing),
            orderIndex: 0);
        Direction? best = null;
        // A facing-locked profile can only step where it looks, so a dodge in
        // any other direction costs a turn first. That is only worth spending
        // when the bolt is still more than one advance away.
        bool mayTurnToDodge = _coupling
                == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
            && stayImpact > 1;
        foreach (Direction direction in _order)
        {
            bool offered = Offers(allowed, direction);
            if (!offered && !mayTurnToDodge)
                continue;
            Position destination = Step(context, direction);
            if (lens.IsClosed(destination) || _occupied.Contains(destination))
                continue;
            // Never trade an objective this body is holding for a survivable
            // hit; concession is only worth it when the alternative is death.
            if (!lethal && _holdsObjective && !Contains(_objective, destination))
                continue;

            long score = SafetyScore(
                ImpactIn(lens, destination),
                EscapeCount(lens, destination),
                Contains(_objective, destination),
                KeepsFiringSolution(
                    lens,
                    context,
                    destination,
                    FacingAfterStep(context.Self.Facing, direction)),
                Array.IndexOf(_order, direction) + 1);
            // A turn is a tick spent standing in the same tile, so only take
            // it when the step it unlocks is clearly better than staying.
            if (!offered)
                score += 2;
            if (score < bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        if (best is null)
            return null;

        _dodgeOrigin = context.Self.Position;
        _avoidDodgeOriginThroughTick = context.Tick + 1;
        return MoveOrTurn(
            lens,
            context,
            move,
            allowed,
            best.Value,
            lethal ? "breaking a lethal line" : "sidestepping without conceding");
    }

    private static long SafetyScore(
        int impact,
        int escapes,
        bool onObjective,
        bool keepsFiringSolution,
        int orderIndex)
    {
        long score = ThreatHorizonTicks + 1
            - Math.Min(impact, ThreatHorizonTicks + 1);
        score = (score * 2) + (onObjective ? 0 : 1);
        score = (score * 2) + (keepsFiringSolution ? 0 : 1);
        score = (score * 8) + (7 - Math.Min(7, escapes));
        return (score * 8) + Math.Clamp(orderIndex, 0, 7);
    }

    /// <summary>
    /// Whether the tile still puts a legal trajectory on a visible enemy, using
    /// the facing this body will really have once it gets there. Scoring every
    /// facing instead flatters the move: turning is a separate action, so a
    /// line that needs a rotation first is not a line this tick.
    /// </summary>
    private bool KeepsFiringSolution(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        Direction facing)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.AttackFor(context.Self.FormId);
        if (profile is null || context.Enemies.IsEmpty)
            return false;
        if (profile.OmnidirectionalAim)
            return AnyHeadingHits(lens, context, profile, tile);
        foreach (ShotProgram program in ProgramsFor(profile))
        {
            int count = Tactics.WalkProgram(
                lens,
                tile,
                facing,
                program,
                profile.Projectile.MaxTravelTiles,
                profile.Projectile.DiagonalCornersMustBeClear,
                _path);
            if (ScorePath(lens, context, profile, tile, count, bendPenalty: 0)
                < long.MaxValue)
            {
                return true;
            }
        }
        return false;
    }

    private bool AnyHeadingHits(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile profile,
        Position tile)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (!Tactics.TryRay(tile, enemy.Position, out _, out int distance))
                continue;
            if (distance > profile.Projectile.MaxTravelTiles)
                continue;
            if (Tactics.ClearRay(
                    lens,
                    tile,
                    enemy.Position,
                    profile.Projectile.DiagonalCornersMustBeClear))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Steps into the contested region the moment one is adjacent. Presence is
    /// the only thing that moves the objective clock, so a survivable bolt is
    /// an acceptable price for standing on the ground.
    /// </summary>
    private GenericActorDecision? TryEnterObjective(
        ContractLens lens,
        GenericActorContext context)
    {
        if (_objective.Length == 0 || _holdsObjective)
            return null;
        if (ObjectiveWeightOf(lens, context.Self.FormId) <= 0)
            return null;

        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        Direction? best = null;
        long bestScore = long.MaxValue;
        foreach (Direction direction in _order)
        {
            if (!CanReach(allowed, context, direction))
                continue;
            Position destination = Step(context, direction);
            if (!Contains(_objective, destination)
                || lens.IsClosed(destination)
                || _occupied.Contains(destination)
                || RecentlyBlocked(destination, context.Tick))
            {
                continue;
            }
            if (IncomingDamageAt(lens, destination) >= context.Self.Health)
                continue;
            long score = SafetyScore(
                ImpactIn(lens, destination),
                EscapeCount(lens, destination),
                onObjective: true,
                KeepsFiringSolution(
                    lens,
                    context,
                    destination,
                    FacingAfterStep(context.Self.Facing, direction)),
                Array.IndexOf(_order, direction));
            // Among otherwise equal entries, take the one that does not put two
            // of my bodies under one cast or one arc, then the one the walls
            // shelter. Both terms are low-order: safety still decides first.
            score = (score * 8) + Math.Min(7, SpreadCost(lens, context, destination));
            score = (score * 64) + Math.Min(63, lens.ExposureAt(destination));
            if (score < bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        return best is null
            ? null
            : MoveOrTurn(
                lens,
                context,
                move,
                allowed,
                best.Value,
                "entering the contested ground");
    }

    /// <summary>
    /// Re-stances inside the region already held: same objective presence,
    /// fewer firing lines pointed at the tile. This is the answer to being
    /// killed by a gun that outranges the sensor — the count of lines onto a
    /// tile is map geometry, so it is knowable without ever seeing the shooter.
    ///
    /// It is deliberately timid. It never runs with a bolt in flight (a step
    /// then reads as a dodge, and dodging a shot that cannot reach you is its
    /// own mistake), and with an enemy watching it runs only when this tile is
    /// already inside that enemy's declared reach — otherwise standing still
    /// is already the safe move and spending the tick would only walk into
    /// range.
    /// </summary>
    private GenericActorDecision? TryImproveStance(
        ContractLens lens,
        GenericActorContext context)
    {
        if (!_holdsObjective || _contested || _hostile.Count > 0)
            return null;
        if (!ReferenceEquals(_goals, _objective))
            return null;
        if (!context.Enemies.IsEmpty
            && !Exposed(lens, context, context.Self.Position))
        {
            return null;
        }

        int here = StanceCost(lens, context, context.Self.Position);
        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        Direction? best = null;
        int bestExposure = here;
        foreach (Direction direction in _order)
        {
            if (!CanReach(allowed, context, direction))
                continue;
            Position destination = Step(context, direction);
            if (!Contains(_objective, destination)
                || lens.IsClosed(destination)
                || _occupied.Contains(destination)
                || RecentlyBlocked(destination, context.Tick))
            {
                continue;
            }
            // The no-free-entry rule: while the clock is mine and nothing can
            // touch me, a tile that something can touch is not an improvement
            // however sheltered it looks on the static map.
            if (Exposed(lens, context, destination)
                && !Exposed(lens, context, context.Self.Position))
            {
                continue;
            }
            int cost = StanceCost(lens, context, destination);
            if (cost < bestExposure)
            {
                bestExposure = cost;
                best = direction;
            }
        }
        return best is null
            ? null
            : MoveOrTurn(
                lens,
                context,
                move,
                allowed,
                best.Value,
                "taking cover without leaving the ground");
    }

    /// <summary>
    /// What a tile inside the region costs to stand on: the static count of
    /// firing lines the walls allow onto it, plus the cost of sharing one cast
    /// or one arc with an ally. Exposure still dominates — the spread term
    /// separates tiles the map rates equally, which on a four-tile region is
    /// most pairs of them.
    /// </summary>
    private int StanceCost(
        ContractLens lens,
        GenericActorContext context,
        Position tile) =>
        (Math.Min(1 << 20, lens.ExposureAt(tile)) * 8)
        + SpreadCost(lens, context, tile);

    private static Position Step(GenericActorContext context, Direction direction)
    {
        (int dx, int dy) = direction.Vector();
        return context.Self.Position.Offset(dx, dy);
    }

    /// <summary>
    /// TICKS until the first hostile bolt would enter the tile, or one past the
    /// horizon when none does. Every bolt now publishes its own cadence between
    /// advances beside the ticks until its next one, so the arrival tick is the
    /// engine's arithmetic rather than a count of advances that silently
    /// assumed one tick each. It matters the moment a ruleset ships two
    /// cadences: a volley bolt need not agree with an ordinary one, and a
    /// deflected bolt carries the class of the bolt that was returned.
    /// </summary>
    private int ImpactIn(ContractLens lens, Position tile)
    {
        int soonest = ThreatHorizonTicks + 1;
        foreach (GenericActorContext.ObservedProjectile projectile in _hostile)
        {
            if (Tactics.Arrival(lens, projectile, tile, strictCorners: true)
                is (int ticks, _)
                && ticks < soonest)
            {
                soonest = Math.Max(0, ticks);
            }
        }
        return soonest;
    }

    private int EscapeCount(ContractLens lens, Position tile)
    {
        int escapes = 0;
        foreach (Direction direction in Tactics.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position neighbour = tile.Offset(dx, dy);
            if (lens.IsClosed(neighbour) || _occupied.Contains(neighbour))
                continue;
            if (ImpactIn(lens, neighbour) > ThreatHorizonTicks)
                escapes++;
        }
        return escapes;
    }

    /// <summary>
    /// What arriving on this tile would cost inside the threat horizon, priced
    /// per bolt. "Should I eat this?" is answered by the projectile itself now:
    /// <c>DamagePerHit</c> is published on every entry, so a doctrine no longer
    /// has to assume the worst gun in the catalog fired every bolt on the map.
    /// </summary>
    private int IncomingDamageAt(ContractLens lens, Position tile)
    {
        int damage = 0;
        foreach (GenericActorContext.ObservedProjectile projectile in _hostile)
        {
            if (Tactics.Arrival(lens, projectile, tile, strictCorners: true)
                is (int ticks, int hit)
                && ticks <= ThreatHorizonTicks)
            {
                damage += hit;
            }
        }
        return damage;
    }

    /// <summary>
    /// Whether a tile is inside anything that can actually deliver damage to
    /// it right now: a bolt already in flight whose own declared remaining
    /// travel still covers the tile, or a visible enemy whose declared attack
    /// profile has the range and the geometry for it. Facing is deliberately
    /// ignored for bodies — a rotation is one action, so an enemy's reach is
    /// its range, not its current aim. A bolt's remainder, by contrast, is
    /// fixed: it is exactly what separates a shot that looks live from one
    /// that expires a tile short.
    /// </summary>
    private bool Exposed(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        foreach (GenericActorContext.ObservedProjectile projectile in _hostile)
        {
            if (Tactics.WithinDeclaredRemaining(
                    lens,
                    projectile,
                    tile,
                    lens.StrictCorners))
            {
                return true;
            }
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            // Price the gun the body is BECOMING, not only the one it holds. A
            // stance entry is a public windup carrying its target form, so a
            // body two ticks from a wider gun is already inside that gun's
            // reach for planning purposes. Facing stays deliberately ignored
            // for bodies: a rotation is one action, and a fan covers several
            // headings at once, so reach is range rather than current aim.
            int reach = 0;
            bool corners = false;
            foreach (string candidate in new[]
                     {
                         enemy.FormId,
                         PendingOrCurrentForm(
                             enemy.FormId,
                             enemy.PendingSameLifeTransition),
                     })
            {
                if (lens.AttackFor(candidate) is not
                    GenericActorRulesContract.AttackProfile profile)
                {
                    continue;
                }
                reach = Math.Max(reach, profile.Projectile.MaxTravelTiles);
                corners |= profile.Projectile.DiagonalCornersMustBeClear;
            }
            if (reach <= 0)
                continue;
            if (!Tactics.TryRay(enemy.Position, tile, out _, out int distance))
                continue;
            if (distance > reach)
                continue;
            if (Tactics.ClearRay(lens, enemy.Position, tile, corners))
                return true;
        }
        return Reflected(lens, context, tile);
    }

    /// <summary>
    /// Whether standing here puts the tile on the wrong end of my OWN gun. A
    /// form declaring <c>projectileGuard</c> kills a bolt arriving inside its
    /// facing quadrant and launches a replacement from its own tile along the
    /// exactly reversed heading under ITS team's ownership — so a straight line
    /// from my tile into a guard's face is a line back onto me, and the tile is
    /// exposed to a gun I am holding.
    ///
    /// <para>The whole model is switched off by an absent field: canonical
    /// contracts omit an inert guard, so <see cref="ContractLens.AnyGuardForms"/>
    /// is false on every arm without the shell and this method returns
    /// immediately. The arc never tracks, which is the other half of the fact —
    /// so the answer to a shell is always a bearing, never a duel.</para>
    /// </summary>
    private bool Reflected(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        if (!lens.AnyGuardForms || _guards.Count == 0)
            return false;
        GenericActorRulesContract.AttackProfile? mine =
            lens.AttackFor(context.Self.FormId);
        if (mine is null)
            return false;

        foreach (GenericActorContext.ObservedEnemyState guard in _guards)
        {
            if (!Tactics.TryRay(
                    tile,
                    guard.Position,
                    out ProjectileHeading outbound,
                    out int distance))
            {
                continue;
            }
            if (distance > mine.Projectile.MaxTravelTiles)
                continue;
            if (!InFacingQuadrant(guard.Facing, outbound))
                continue;
            if (Tactics.ClearRay(
                    lens,
                    tile,
                    guard.Position,
                    mine.Projectile.DiagonalCornersMustBeClear))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether a bolt travelling on <paramref name="heading"/> arrives inside
    /// the quadrant a body facing <paramref name="facing"/> guards. A quadrant
    /// is the facing heading and its two neighbours, so an incoming bolt is
    /// deflected when its REVERSE — the bearing it came from — sits in that
    /// set.
    /// </summary>
    private static bool InFacingQuadrant(
        Direction facing,
        ProjectileHeading heading)
    {
        ProjectileHeading centre = facing.ToProjectileHeading();
        ProjectileHeading arriving = heading.Turned(4);
        return arriving == centre
            || arriving == centre.Turned(1)
            || arriving == centre.Turned(-1);
    }

    /// <summary>
    /// The facing this body will actually have after stepping one tile, which
    /// is what a straight gun will fire along next tick. Under the coupled arm
    /// a step is a turn; under the other two the body keeps its aim.
    /// </summary>
    private Direction FacingAfterStep(Direction facing, Direction step) =>
        _coupling
            == GenericActorRulesContract.MovementFacingCoupling
                .FaceMovementDirection
            ? step
            : facing;

    /// <summary>
    /// Whether a movement action can be submitted for a direction at all. A
    /// facing-locked profile offers only the current facing, so every other
    /// direction has to be reached by turning first.
    /// </summary>
    private static bool Offers(
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint allowed,
        Direction direction) =>
        allowed.AllowedValues.Contains(direction);

    private bool CanReach(
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint allowed,
        GenericActorContext context,
        Direction direction) =>
        Offers(allowed, direction)
        || (_coupling
                == GenericActorRulesContract.MovementFacingCoupling.FacingLocked
            && direction != context.Self.Facing);

    /// <summary>
    /// Commits to a destination direction. Where the movement mask offers the
    /// direction this is a step; where a facing-locked profile does not, the
    /// same intent costs one turn first, which is the whole difference that
    /// arm introduces. Returning null instead of stepping the wrong way keeps
    /// the caller's fallback chain honest.
    /// </summary>
    private GenericActorDecision? MoveOrTurn(
        ContractLens lens,
        GenericActorContext context,
        GenericActorActionLegality move,
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint allowed,
        Direction direction,
        string reason)
    {
        if (Offers(allowed, direction))
            return Move(move, direction, reason);
        if (_coupling
            != GenericActorRulesContract.MovementFacingCoupling.FacingLocked)
        {
            return null;
        }
        return Rotate(lens, context, direction, $"turning to walk: {reason}");
    }

    // ------------------------------------------------------------------
    // 3. Fire — straight, aimed, or curved, whatever the contract allows
    // ------------------------------------------------------------------

    private GenericActorDecision? TryShoot(
        ContractLens lens,
        GenericActorContext context)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.AttackFor(context.Self.FormId);
        if (profile is null || context.Enemies.IsEmpty)
            return null;

        GenericActorDecision? best = null;
        long bestScore = long.MaxValue;
        foreach (GenericActorActionLegality action
                 in AvailableByKind(
                     lens,
                     context,
                     GenericActorRulesContract.ActionKind.Attack))
        {
            if (lens.HasParameter(
                    action.ActionId,
                    GenericActorRulesContract.ActionParameterKind
                        .ProjectileHeading))
            {
                EvaluateHeadings(
                    lens,
                    context,
                    profile,
                    action,
                    ref best,
                    ref bestScore);
                continue;
            }
            EvaluatePrograms(
                lens,
                context,
                profile,
                action,
                ref best,
                ref bestScore);
        }
        return best;
    }

    private void EvaluateHeadings(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile profile,
        GenericActorActionLegality action,
        ref GenericActorDecision? best,
        ref long bestScore)
    {
        GenericActorActionLegality.ArgumentConstraint.ProjectileHeadingConstraint?
            headings = Constraint<GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint>(action);
        if (headings is null)
            return;

        foreach (ProjectileHeading heading in headings.AllowedValues)
        {
            int count = Tactics.WalkHeading(
                lens,
                context.Self.Position,
                heading,
                profile.Projectile.MaxTravelTiles,
                profile.Projectile.DiagonalCornersMustBeClear,
                _path);
            long score = ScorePath(
                lens,
                context,
                profile,
                context.Self.Position,
                count,
                bendPenalty: 0);
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = new GenericActorDecision(
                action.ActionId,
                action.ActionCode,
                [new GenericActorActionArgument.ProjectileHeadingArgument(heading)],
                $"omnidirectional fire {heading}");
        }
    }

    private void EvaluatePrograms(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile profile,
        GenericActorActionLegality action,
        ref GenericActorDecision? best,
        ref long bestScore)
    {
        bool hasProgramParameter = lens.HasParameter(
            action.ActionId,
            GenericActorRulesContract.ActionParameterKind.ShotProgram);
        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            programConstraint = Constraint<GenericActorActionLegality
                .ArgumentConstraint.ShotProgramConstraint>(action);
        bool payloadAllowed = hasProgramParameter
            && profile.ShotProgram.Enabled
            && programConstraint is { Allowed: true };

        ShotProgram straight = Default(profile);
        foreach (ShotProgram program in ProgramsFor(profile))
        {
            bool isDefault = program.Equals(straight);
            if (!isDefault && !payloadAllowed)
                continue;

            int count = Tactics.WalkProgram(
                lens,
                context.Self.Position,
                context.Self.Facing,
                program,
                profile.Projectile.MaxTravelTiles,
                profile.Projectile.DiagonalCornersMustBeClear,
                _path);
            long score = ScorePath(
                lens,
                context,
                profile,
                context.Self.Position,
                count,
                program.BendCount + Math.Abs(program.InitialAimOffset));
            if (score >= bestScore)
                continue;

            bool omitPayload = isDefault
                && (!hasProgramParameter || profile.ShotProgram.PayloadOptional);
            bestScore = score;
            best = omitPayload
                ? GenericActorDecision.WithoutArguments(
                    action.ActionId,
                    action.ActionCode,
                    "straight fire")
                : new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [new GenericActorActionArgument.ShotProgramArgument(program)],
                    program.BendCount > 0 ? "curved intercept" : "aimed fire");
        }
    }

    /// <summary>
    /// Rates one candidate trajectory: earliest contact wins, an enemy already
    /// standing on the contested objective outranks one that is not, and a
    /// straight bolt outranks an equivalent curved one.
    ///
    /// <para>Revision 4 adds the one contact that is worse than no contact.
    /// A form declaring a projectile guard deflects bolts arriving inside its
    /// facing quadrant and sends a live replacement back down my own line, so a
    /// trajectory whose contact lands in that quadrant is not a hit — it is a
    /// hit on me. The arriving heading comes from the path's own last step, so
    /// the test is exact rather than a guess about where I was standing, and a
    /// curved trajectory arriving from off-axis genuinely rates better than the
    /// straight one that fed the arc. That is the whole answer to a shell for a
    /// chassis with a one-bend gun: flank it, or bend around into its
    /// flank.</para>
    /// </summary>
    private long ScorePath(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.AttackProfile profile,
        Position origin,
        int pathLength,
        int bendPenalty)
    {
        long best = long.MaxValue;
        for (int index = 0; index < pathLength; index++)
        {
            Position tile = _path[index];
            int arrival = Tactics.ArrivalTick(profile.Projectile, index);
            Position previous = index == 0 ? origin : _path[index - 1];
            ProjectileHeading arriving = Tactics.HeadingOf(
                Math.Sign(tile.X - previous.X),
                Math.Sign(tile.Y - previous.Y));
            foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            {
                bool hit = enemy.Position == tile;
                if (!hit
                    && _drift.TryGetValue(
                        enemy.ActorId,
                        out (int Dx, int Dy) drift))
                {
                    Position predicted = enemy.Position.Offset(
                        drift.Dx * arrival,
                        drift.Dy * arrival);
                    hit = predicted == tile && !lens.IsClosed(predicted);
                }
                if (!hit)
                    continue;
                // Feeding an arc is a tempo tax with a bill. Never bill myself.
                if (lens.IsGuard(enemy.FormId)
                    && InFacingQuadrant(enemy.Facing, arriving))
                {
                    continue;
                }

                long score = Contains(_objective, enemy.Position) ? 0 : 1;
                score = (score * 64) + Math.Min(63, arrival);
                score = (score * 64) + Math.Min(63, enemy.Health);
                score = (score * 64) + Math.Min(63, bendPenalty);
                best = Math.Min(best, score);
            }
        }
        return best;
    }

    private ShotProgram[] ProgramsFor(
        GenericActorRulesContract.AttackProfile profile)
    {
        if (_programCache.TryGetValue(profile.Id, out ShotProgram[]? cached))
            return cached;

        var programs = new List<ShotProgram> { Default(profile) };
        GenericActorRulesContract.ShotProgramDefinition definition =
            profile.ShotProgram;
        if (definition.Enabled)
        {
            GenericActorRulesContract.AimOnlyShotProgramValue aim =
                definition.AimOnlyProgram;
            for (int offset = definition.MinInitialAimSteps;
                 offset <= definition.MaxInitialAimSteps;
                 offset++)
            {
                var candidate = new ShotProgram(
                    offset,
                    aim.BendDirection,
                    aim.BendAfterTiles,
                    aim.BendEveryTiles,
                    aim.BendCount);
                if (!programs.Contains(candidate))
                    programs.Add(candidate);
            }

            for (int count = Math.Max(1, definition.MinBendCount);
                 count <= definition.MaxBendCount && programs.Count < MaxPrograms;
                 count++)
            {
                for (int after = definition.MaxBendAfterTiles;
                     after >= definition.MinBendAfterTiles
                     && programs.Count < MaxPrograms;
                     after--)
                {
                    for (int every = Math.Max(1, definition.MinBendEveryTiles);
                         every <= definition.MaxBendEveryTiles
                         && programs.Count < MaxPrograms;
                         every++)
                    {
                        for (int offset = definition.MinInitialAimSteps;
                             offset <= definition.MaxInitialAimSteps
                             && programs.Count < MaxPrograms;
                             offset++)
                        {
                            foreach (int bend
                                     in definition.AllowedCurvedBendDirections)
                            {
                                if (programs.Count >= MaxPrograms)
                                    break;
                                programs.Add(new ShotProgram(
                                    offset,
                                    bend,
                                    after,
                                    every,
                                    count));
                            }
                        }
                    }
                }
            }
        }

        ShotProgram[] resolved = programs.ToArray();
        _programCache[profile.Id] = resolved;
        return resolved;
    }

    private static ShotProgram Default(
        GenericActorRulesContract.AttackProfile profile)
    {
        GenericActorRulesContract.ShotProgramValue value =
            profile.ShotProgram.DefaultProgram;
        return new ShotProgram(
            value.InitialAimOffset,
            value.BendDirection,
            value.BendAfterTiles,
            value.BendEveryTiles,
            value.BendCount);
    }

    // ------------------------------------------------------------------
    // 4. Aim — a facing-locked gun is worth a tick when contact is close
    // ------------------------------------------------------------------

    private GenericActorDecision? TryAim(
        ContractLens lens,
        GenericActorContext context)
    {
        GenericActorRulesContract.AttackProfile? profile =
            lens.AttackFor(context.Self.FormId);
        if (profile is null || profile.OmnidirectionalAim || context.Enemies.IsEmpty)
            return null;

        int nearest = Tactics.Unreachable;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            nearest = Math.Min(
                nearest,
                enemy.Position.ChebyshevDistance(context.Self.Position));
        }
        if (!_atGoal && !_holdsObjective && nearest > CloseContactRange)
            return null;

        GenericActorActionLegality? rotate = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = rotate is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(rotate);
        if (rotate is null || allowed is null)
            return null;

        Direction? best = null;
        long bestScore = long.MaxValue;
        ShotProgram straight = Default(profile);
        foreach (Direction direction in _order)
        {
            if (direction == context.Self.Facing
                || !allowed.AllowedValues.Contains(direction))
            {
                continue;
            }
            int count = Tactics.WalkProgram(
                lens,
                context.Self.Position,
                direction,
                straight,
                profile.Projectile.MaxTravelTiles,
                profile.Projectile.DiagonalCornersMustBeClear,
                _path);
            long score = ScorePath(
                lens,
                context,
                profile,
                context.Self.Position,
                count,
                bendPenalty: 0);
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = direction;
        }
        return best is null
            ? null
            : Rotate(lens, context, best.Value, "laying the gun on the front");
    }

    // ------------------------------------------------------------------
    // 4b. Sweep — a shared objective nobody can see is the real stalemate
    // ------------------------------------------------------------------

    /// <summary>
    /// The wave-1 mirrors that ran 400 ticks at zero progress were not
    /// standoffs; they were two blind bodies three tiles apart on the same
    /// three-tile region, each facing the enemy base and each therefore
    /// looking away from the only thing that mattered. Every downstream
    /// tactic needs a visible enemy, so a contest with none is a sensor
    /// problem, and the contest signal itself is the proof that a body is
    /// there: sole presence would have made the claim mine.
    ///
    /// The answer is to point the quadrant at the part of the region the
    /// observation says is unseen. It costs one tick, it cannot walk into
    /// anything, and it turns an unbreakable draw into a duel.
    /// </summary>
    private GenericActorDecision? TrySweepForContester(
        ContractLens lens,
        GenericActorContext context)
    {
        if (!_contested || !context.Enemies.IsEmpty || _objective.Length == 0)
            return null;

        Position? target = null;
        int best = int.MaxValue;
        foreach (Position tile in _objective)
        {
            if (tile == context.Self.Position || Seen(context, tile))
                continue;
            int distance = tile.ChebyshevDistance(context.Self.Position);
            if (distance < best)
            {
                best = distance;
                target = tile;
            }
        }
        if (target is not Position unseen)
            return null;

        int dx = unseen.X - context.Self.Position.X;
        int dy = unseen.Y - context.Self.Position.Y;
        Direction desired = Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
        if (desired == context.Self.Facing)
            return null;
        return Rotate(
            lens,
            context,
            desired,
            "sweeping for the body sharing this ground");
    }

    private static bool Seen(GenericActorContext context, Position tile)
    {
        foreach (GenericActorContext.ObservedTile observed in context.VisibleTiles)
        {
            if (observed.Position == tile)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // 5. Suppression — never trade a firing line for a staring contest
    // ------------------------------------------------------------------

    /// <summary>
    /// Two bodies that share a region but no straight line will otherwise stand
    /// still until the tick cap. When neither the gun nor a rotation can reach
    /// an enemy, step to an adjacent tile that opens a line — but only to a
    /// tile that keeps the objective presence this body already provides.
    ///
    /// What this step is worth depends entirely on who owns the clock, and
    /// revision 2 makes that explicit. While the capture is mine and nothing
    /// can reach me, the step is a gift: it buys a shot the opponent can
    /// answer, and standing still was already scoring. While the ground is
    /// shared, no one is scoring, tiles are worthless and only a firing line
    /// is worth anything — so the same step becomes the only move that ends
    /// the stalemate, and a survivable hit is a fair price for it. Wave 1 lost
    /// 400-tick mirrors to the version of this method that vetoed every tile
    /// next to an enemy body: exactly the alignment steps a cardinal gun needs
    /// to shoot a diagonally adjacent body at all.
    /// </summary>
    private GenericActorDecision? TryUnstickTheGun(
        ContractLens lens,
        GenericActorContext context)
    {
        if (context.Enemies.IsEmpty || (!_atGoal && !_holdsObjective))
            return null;
        GenericActorRulesContract.AttackProfile? profile =
            lens.AttackFor(context.Self.FormId);
        if (profile is null)
            return null;
        // While the clock is mine, repositioning is only worth a tick with the
        // gun actually loaded; during cooldown the same step is a free hit for
        // the opponent. On shared ground the tick was worth nothing anyway, so
        // walking into position during cooldown is strictly better than
        // waiting for a shot that has nothing to aim at.
        if (!_contested
            && FirstAvailable(
                lens,
                context,
                GenericActorRulesContract.ActionKind.Attack) is null)
        {
            return null;
        }

        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        int here = Tactics.DistanceAt(
            lens,
            _objectiveField,
            context.Self.Position);
        Position blocked = BlockedDestination(context);
        bool safeHere = !Exposed(lens, context, context.Self.Position);
        ShotProgram straight = Default(profile);
        Direction? best = null;
        long bestScore = long.MaxValue;
        foreach (Direction direction in _order)
        {
            if (!CanReach(allowed, context, direction))
                continue;
            Position destination = Step(context, direction);
            if (lens.IsClosed(destination)
                || _occupied.Contains(destination)
                || destination == blocked
                || RecentlyBlocked(destination, context.Tick))
            {
                continue;
            }
            if (_holdsObjective && !Contains(_objective, destination))
                continue;
            if (!_holdsObjective
                && Tactics.DistanceAt(lens, _objectiveField, destination) > here)
            {
                continue;
            }

            if (_contested)
            {
                // Nobody is scoring. Only lethality is still a veto.
                if (IncomingDamageAt(lens, destination) >= context.Self.Health)
                    continue;
            }
            else
            {
                if (ImpactIn(lens, destination) <= ThreatHorizonTicks)
                    continue;
                // A tile an enemy body can also reach this tick is a coin flip
                // that usually ends with both moves refused.
                if (Adjacent(context, destination))
                    continue;
                // No free entry: the clock is mine and nothing reaches me, so
                // a tile inside a declared reach costs more than the shot it
                // buys. The bolt's own remaining travel decides this, not the
                // direction it happens to be pointing.
                if (safeHere && Exposed(lens, context, destination))
                    continue;
            }

            Direction facing = FacingAfterStep(context.Self.Facing, direction);
            int count = Tactics.WalkProgram(
                lens,
                destination,
                facing,
                straight,
                profile.Projectile.MaxTravelTiles,
                profile.Projectile.DiagonalCornersMustBeClear,
                _path);
            long score = ScorePath(
                lens,
                context,
                profile,
                destination,
                count,
                bendPenalty: 0);
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = direction;
        }
        return best is null
            ? null
            : MoveOrTurn(
                lens,
                context,
                move,
                allowed,
                best.Value,
                _contested
                    ? "aligning the gun on the shared ground"
                    : "stepping into a firing line");
    }

    private static bool Adjacent(GenericActorContext context, Position tile)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (enemy.Position.ChebyshevDistance(tile) <= 1)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // 5b. Stances — a same-life route with a declared budget
    // ------------------------------------------------------------------

    /// <summary>
    /// Stance play, written against routes rather than against class names, so
    /// the same code casts a fan, raises a shield, or does nothing at all
    /// depending only on what the contract publishes for the form this body is
    /// currently in.
    ///
    /// <para>A stance is an ordinary same-life route whose RETURN route carries
    /// <c>automaticReturn</c>: a counter scoped to the stance form and a
    /// threshold the engine spends for me. That single field is enough to
    /// recognise a budgeted stance without knowing which one it is — the volley
    /// counts attacks to one, so an entry buys exactly one cast and there is no
    /// exit to author; the shell counts deflections to three, so the shield
    /// shatters on the third bolt and the punish window is its exit plus a fresh
    /// entry windup. Leaving early is still mine through the parameterless
    /// return; leaving late does not exist.</para>
    ///
    /// <para>This entrant's own chassis declares no same-life routes at all, so
    /// every branch below is unreachable on its own class arm and the method
    /// returns on its first line. It exists because the qualification profile
    /// and five of the six class pairs are not this chassis, and because a
    /// doctrine that hard-codes "I have no stance" is a doctrine that faults the
    /// first time the contract disagrees.</para>
    /// </summary>
    private GenericActorDecision? TryStance(
        ContractLens lens,
        GenericActorContext context)
    {
        if (lens.Form(context.Self.FormId) is not
            GenericActorRulesContract.Form form)
        {
            return null;
        }

        foreach (GenericActorRulesContract.FormTransition exit
                 in lens.RoutesFrom(form.Id))
        {
            // A budgeted return out of my own form means I am standing in a
            // stance right now.
            if (exit.AutomaticReturn is not null)
                return LeaveStance(lens, context, form, exit);
        }
        return EnterStance(lens, context, form);
    }

    /// <summary>
    /// Whether to spend the parameterless return before the engine spends it
    /// for me. Two contract facts decide it and neither needs the stance's
    /// name.
    ///
    /// <list type="bullet">
    /// <item>A guarded form only guards its FACING QUADRANT and cannot rotate,
    /// so an enemy that has walked around to a flank or rear bearing is an
    /// enemy the shield does nothing about — and the form has no gun to answer
    /// with. Drop it and become a body again.</item>
    /// <item>A stance whose budget is attacks and whose gun cannot reach
    /// anything is an immobile body paying for nothing. Drop it.</item>
    /// </list>
    /// </summary>
    private GenericActorDecision? LeaveStance(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form form,
        GenericActorRulesContract.FormTransition exit)
    {
        bool leave;
        if (form.ProjectileGuard
            != GenericActorRulesContract.FormProjectileGuard.None)
        {
            // A guard is worth its immobility exactly while something hostile
            // is pointed into the quadrant it froze. Two ways for that to stop
            // being true, and one test covers both: the threat walked around to
            // a bearing the arc does not cover, or the threat went away. Either
            // way the shield is now a body that cannot move, shoot or turn, so
            // spend the return rather than wait for a third deflection that is
            // never coming.
            leave = !GuardWorthHolding(lens, context);
        }
        else
        {
            leave = context.Enemies.IsEmpty;
        }
        if (!leave)
            return null;

        // Leaving costs this route's windup and coming back costs the entry's,
        // and that sum is the contract's own punish window. A stance held for
        // less than it is a stance that spends more tempo on the doorway than
        // on the doorway's purpose — measured, this was 1086 raises and 1080
        // immediate drops across three matches before the check existed.
        int punish = Math.Max(1, exit.Windup.DurationTicks)
            + lens.WindupInto(form.Id);
        if (context.Tick - _formSinceTick < punish)
            return null;

        foreach (GenericActorActionLegality action
                 in AvailableByKind(
                     lens,
                     context,
                     GenericActorRulesContract.ActionKind.SameLifeTransition))
        {
            if (!string.Equals(
                    action.ActionId,
                    exit.ActionId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (Constraint<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>(action) is
                GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint forms
                && !forms.AllowedFormIds.IsEmpty)
            {
                if (!forms.AllowedFormIds.Contains(
                        exit.TargetFormId,
                        StringComparer.Ordinal))
                {
                    continue;
                }
                return new GenericActorDecision(
                    action.ActionId,
                    action.ActionCode,
                    [
                        new GenericActorActionArgument.FormTargetArgument(
                            exit.TargetFormId),
                    ],
                    "dropping a stance whose reason has gone");
            }
            return GenericActorDecision.WithoutArguments(
                action.ActionId,
                action.ActionCode,
                "dropping a stance whose reason has gone");
        }
        return null;
    }

    /// <summary>
    /// When a stance beats staying a body. Both answers are derived from the
    /// target form's own published capability, so a chassis that gains a stance
    /// in some future arm gets the behaviour without an edit here.
    ///
    /// <list type="bullet">
    /// <item><b>A fan gun</b> (<c>volley.projectileCount &gt; 1</c>) is worth
    /// its windup when one cast answers MORE than one bolt would: two or more
    /// visible bodies inside the fan's coverage from this tile. One body is a
    /// job for the ordinary gun, which costs no windup and no immobility, and
    /// the fan is straight by construction so it buys no geometry either.</item>
    /// <item><b>A guard</b> is worth its windup when a bolt is already inbound
    /// on a bearing this body's CURRENT facing covers and the shield will be up
    /// before it lands — the quadrant is chosen before the shield rises and
    /// cannot be turned afterwards, so entry facing is the whole decision.</item>
    /// </list>
    ///
    /// <para>Both refuse a target form of lower objective weight than the body
    /// already contributes: fortifying off the scoreboard is the one mistake
    /// this experiment says traces back through both mirror stalemates and
    /// thrown games, and it is checked against the form catalog rather than
    /// against a turret's name.</para>
    /// </summary>
    private GenericActorDecision? EnterStance(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.Form form)
    {
        GenericActorActionLegality? transform = null;
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint? allowed =
            null;
        foreach (GenericActorActionLegality action
                 in AvailableByKind(
                     lens,
                     context,
                     GenericActorRulesContract.ActionKind.SameLifeTransition))
        {
            if (Constraint<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>(action) is not
                GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint forms
                || forms.AllowedFormIds.IsEmpty)
            {
                continue;
            }
            transform = action;
            allowed = forms;
            break;
        }
        if (transform is null || allowed is null)
            return null;

        foreach (GenericActorRulesContract.FormTransition route
                 in lens.RoutesFrom(form.Id))
        {
            if (!allowed.AllowedFormIds.Contains(
                    route.TargetFormId,
                    StringComparer.Ordinal))
            {
                continue;
            }
            if (lens.Form(route.TargetFormId) is not
                GenericActorRulesContract.Form target)
            {
                continue;
            }
            // Never trade scoring presence for durability.
            if (target.ObjectiveWeight < form.ObjectiveWeight)
                continue;

            bool budgeted = false;
            foreach (GenericActorRulesContract.FormTransition back
                     in lens.RoutesFrom(target.Id))
            {
                budgeted |= back.AutomaticReturn is not null;
            }
            if (!budgeted)
                continue;

            int windup = Math.Max(1, route.Windup.DurationTicks);
            if (IncomingDamageAt(lens, context.Self.Position)
                >= context.Self.Health)
            {
                // A windup is wait-only and lethal damage cancels it. Dodging is
                // the answer to a lethal line, in every arm.
                continue;
            }

            string reason;
            if (target.ProjectileGuard
                != GenericActorRulesContract.FormProjectileGuard.None)
            {
                if (!GuardWouldCatch(lens, context, windup))
                    continue;
                reason = "raising the arc against the bearing it came from";
            }
            else if (lens.FanOf(target.Id) > 1)
            {
                if (FanCoverage(lens, context, target.Id) < 2)
                    continue;
                reason = "casting into more bodies than one bolt answers";
            }
            else
            {
                continue;
            }

            return new GenericActorDecision(
                transform.ActionId,
                transform.ActionCode,
                [new GenericActorActionArgument.FormTargetArgument(target.Id)],
                reason);
        }
        return null;
    }

    /// <summary>
    /// True when a bolt already in flight will arrive on this tile AFTER the
    /// shield is up and from a bearing the current facing covers. The windup
    /// completes after combat resolves, so a bolt landing this tick is a bolt
    /// the shield misses — the comparison has to be strict.
    /// </summary>
    private bool GuardWouldCatch(
        ContractLens lens,
        GenericActorContext context,
        int windup)
    {
        foreach (GenericActorContext.ObservedProjectile bolt in _hostile)
        {
            if (Tactics.Arrival(
                    lens,
                    bolt,
                    context.Self.Position,
                    strictCorners: true)
                is not (int ticks, _))
            {
                continue;
            }
            if (ticks > windup && InFacingQuadrant(context.Self.Facing, bolt.Heading))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Whether an already-raised arc still has anything pointed into it: a bolt
    /// in flight on a guarded bearing, or a body with a line on one. The arc
    /// never tracks, so this is the only thing keeping the stance worth its
    /// immobility.
    ///
    /// <para>Note the deliberate asymmetry with entry, which requires a bolt
    /// already in flight. A body with a line is enough to keep a shield up and
    /// NOT enough to raise one: an enemy's cooldown is redacted, so a line is a
    /// possibility rather than a commitment, and raising against possibilities
    /// was measured at 1086 entries and 1080 immediate exits across three
    /// matches — a body spending its match in the doorway. The punish-window
    /// hysteresis below halved that to 546/540 and no further. Entry wants a
    /// fact; exit can settle for the absence of one.</para>
    /// </summary>
    private bool GuardWorthHolding(ContractLens lens, GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedProjectile bolt in _hostile)
        {
            if (Tactics.Arrival(
                    lens,
                    bolt,
                    context.Self.Position,
                    strictCorners: true)
                is not null
                && InFacingQuadrant(context.Self.Facing, bolt.Heading))
            {
                return true;
            }
        }
        return ThreatensQuadrant(lens, context);
    }

    /// <summary>
    /// Whether a visible enemy has a clear, in-range line onto this tile that
    /// arrives from inside the quadrant the current facing guards.
    /// </summary>
    private bool ThreatensQuadrant(ContractLens lens, GenericActorContext context)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (lens.AttackFor(
                    PendingOrCurrentForm(
                        enemy.FormId,
                        enemy.PendingSameLifeTransition))
                is not GenericActorRulesContract.AttackProfile profile)
            {
                continue;
            }
            if (!Tactics.TryRay(
                    enemy.Position,
                    context.Self.Position,
                    out ProjectileHeading inbound,
                    out int distance))
            {
                continue;
            }
            if (distance > profile.Projectile.MaxTravelTiles)
                continue;
            if (!InFacingQuadrant(context.Self.Facing, inbound))
                continue;
            if (Tactics.ClearRay(
                    lens,
                    enemy.Position,
                    context.Self.Position,
                    profile.Projectile.DiagonalCornersMustBeClear))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Visible enemy bodies a single cast from this tile would put a bolt on,
    /// counting the fan's contiguous headings around every facing the stance
    /// could rotate to. A fan is straight by construction — an attack profile
    /// carrying a volley refuses programmed shots — so coverage is pure ray
    /// geometry and the count is exact.
    /// </summary>
    private int FanCoverage(
        ContractLens lens,
        GenericActorContext context,
        string stanceFormId)
    {
        if (lens.AttackFor(stanceFormId) is not
            GenericActorRulesContract.AttackProfile profile)
        {
            return 0;
        }
        int fan = lens.FanOf(stanceFormId);
        int half = (fan - 1) / 2;
        int best = 0;
        foreach (Direction facing in Tactics.Cardinals)
        {
            ProjectileHeading centre = facing.ToProjectileHeading();
            int covered = 0;
            foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            {
                if (!Tactics.TryRay(
                        context.Self.Position,
                        enemy.Position,
                        out ProjectileHeading heading,
                        out int distance))
                {
                    continue;
                }
                if (distance > profile.Projectile.MaxTravelTiles)
                    continue;
                bool inFan = false;
                for (int offset = -half; offset <= half; offset++)
                    inFan |= heading == centre.Turned(offset);
                if (!inFan)
                    continue;
                if (Tactics.ClearRay(
                        lens,
                        context.Self.Position,
                        enemy.Position,
                        profile.Projectile.DiagonalCornersMustBeClear))
                {
                    covered++;
                }
            }
            best = Math.Max(best, covered);
        }
        return best;
    }

    // ------------------------------------------------------------------
    // 6. Rejoin the fabrication source when the contract binds it to a region
    // ------------------------------------------------------------------

    private GenericActorDecision? TryReachFabricationSource(
        ContractLens lens,
        GenericActorContext context)
    {
        if (!lens.CanFabricate(context.Self.FormId))
            return null;
        if (lens.FabricationSourceTiles.Count == 0)
            return null;
        if (lens.FabricationSourceTiles.Contains(context.Self.Position))
            return null;

        int[] field = Tactics.DistanceField(lens, lens.FabricationSourceTiles);
        int distance = Tactics.DistanceAt(lens, field, context.Self.Position);
        if (distance >= Tactics.Unreachable)
            return null;

        // A live hold makes the ground behind me unloseable for its duration,
        // so a trip home during it costs nothing that can be taken. Whether
        // the trip is worth making is again the headcount question: it buys a
        // companion, and a companion is only worth a walk where surplus weight
        // scales the gain. On a binary-control contract the same trip is the
        // prime standing in a corridor instead of on the ground.
        int window = distance + 2;
        if (lens.SurplusWeightScalesGain
            && _holdIsMine
            && _holdEndsAtTick >= context.Tick)
        {
            window += _holdEndsAtTick - context.Tick;
        }
        if (!SlotDueWithin(context, lens.TeamId, window))
            return null;

        return StepAlong(lens, context, field, "returning to the fabrication pad");
    }

    private static bool SlotDueWithin(
        GenericActorContext context,
        int teamId,
        int ticks)
    {
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.TeamId != teamId)
                continue;
            switch (slot.State)
            {
                case GenericActorContext.UnitSlotState.Ready:
                    return true;
                case GenericActorContext.UnitSlotState.AvailabilityPending pending
                    when pending.DueTick - context.Tick <= ticks:
                    return true;
                default:
                    continue;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // 7. Presence — enter the contested ground early and stay on it
    // ------------------------------------------------------------------

    /// <summary>
    /// The body the team has elected to hold the ground walks to it before it
    /// trades shots: a bolt fired from six tiles away is worth far less than
    /// the capture ticks lost standing in a corridor to fire it.
    /// </summary>
    private GenericActorDecision? TryCloseOnObjective(
        ContractLens lens,
        GenericActorContext context)
    {
        if (_holdsObjective
            || _objective.Length == 0
            || !ReferenceEquals(_goals, _objective))
        {
            return null;
        }
        return StepAlong(
            lens,
            context,
            _objectiveField,
            "closing on the contested ground");
    }

    private GenericActorDecision? TryAdvance(
        ContractLens lens,
        GenericActorContext context)
    {
        if (_atGoal)
            return null;

        int[] field = ReferenceEquals(_goals, _objective)
            ? _objectiveField
            : Tactics.DistanceField(lens, _goals);
        return StepAlong(lens, context, field, "taking the contested ground");
    }

    private GenericActorDecision? StepAlong(
        ContractLens lens,
        GenericActorContext context,
        int[] field,
        string reason)
    {
        GenericActorActionLegality? move = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = move is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(move);
        if (move is null || allowed is null)
            return null;

        int here = Tactics.DistanceAt(lens, field, context.Self.Position);
        Position blocked = BlockedDestination(context);
        int stayImpact = ImpactIn(lens, context.Self.Position);
        // Under a coupled profile the last step of an approach also sets the
        // facing a forward fabrication will place from, so a tie between two
        // equally short steps is settled by where the child would land.
        bool placementMatters = _coupling
                == GenericActorRulesContract.MovementFacingCoupling
                    .FaceMovementDirection
            && lens.CanFabricate(context.Self.FormId)
            && !lens.PlacementOffsets.IsDefaultOrEmpty
            && _objective.Length > 0;
        Direction? best = null;
        long bestScore = long.MaxValue;
        foreach (Direction direction in _order)
        {
            if (!CanReach(allowed, context, direction))
                continue;
            Position destination = Step(context, direction);
            if (lens.IsClosed(destination) || _occupied.Contains(destination))
                continue;
            if (destination == blocked || RecentlyBlocked(destination, context.Tick))
                continue;
            if (_dodgeOrigin is Position origin
                && context.Tick <= _avoidDodgeOriginThroughTick
                && destination == origin)
            {
                continue;
            }
            int distance = Tactics.DistanceAt(lens, field, destination);
            if (distance >= here)
                continue;
            // Walking into a bolt that lands sooner than the one already
            // aimed at this tile is never progress, however short the path.
            int impact = ImpactIn(lens, destination);
            if (impact < stayImpact)
                continue;
            long score = impact <= ThreatHorizonTicks ? 1 : 0;
            score = (score * 4096) + distance;
            score = (score * 64) + (placementMatters
                ? Math.Min(63, PlacementScore(lens, context, direction))
                : 0);
            // Converge on distinct bearings. Among equally short steps toward
            // the same goal, prefer the one that does not walk this body into
            // the lane an ally is already filling — a queue is what feeds a fan,
            // and it is also what leaves the rear body's gun with nothing to
            // shoot past.
            score = (score * 8) + Math.Min(7, SpreadCost(lens, context, destination));
            score = (score * 8) + Array.IndexOf(_order, direction);
            if (score < bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }
        return best is null
            ? null
            : MoveOrTurn(lens, context, move, allowed, best.Value, reason);
    }

    // ------------------------------------------------------------------
    // 8. Vigilance — a facing-quadrant sensor is worthless pointed backwards
    // ------------------------------------------------------------------

    /// <summary>
    /// A body that has finished moving still owes the team a direction: sight
    /// and, for facing-locked guns, the whole firing arc follow the facing, so
    /// an idle tick is spent pointing at wherever the next contact must come
    /// from rather than at nothing.
    /// </summary>
    private GenericActorDecision? TryFaceApproach(
        ContractLens lens,
        GenericActorContext context)
    {
        Position anchor = ApproachAnchor(lens, context);
        int dx = anchor.X - context.Self.Position.X;
        int dy = anchor.Y - context.Self.Position.Y;
        if (dx == 0 && dy == 0)
            return null;
        Direction desired = Math.Abs(dx) >= Math.Abs(dy)
            ? dx >= 0 ? Direction.East : Direction.West
            : dy >= 0 ? Direction.South : Direction.North;
        if (desired == context.Self.Facing)
            return null;
        return Rotate(lens, context, desired, "watching the enemy approach");
    }

    private Position ApproachAnchor(ContractLens lens, GenericActorContext context)
    {
        Position nearest = default;
        int best = Tactics.Unreachable;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            int distance = enemy.Position.ChebyshevDistance(context.Self.Position);
            if (distance < best)
            {
                best = distance;
                nearest = enemy.Position;
            }
        }
        if (best < Tactics.Unreachable)
            return nearest;

        if (context.Mode
            is GenericActorContext.ModeObservationState.Frontline frontline)
        {
            Position[] ahead = lens.ObjectiveAt(
                frontline.ActivePositionIndex + lens.AdvanceDelta);
            if (ahead.Length > 0)
                return ahead[0];
        }
        return lens.ForwardPoint;
    }

    private static Position BlockedDestination(GenericActorContext context)
    {
        if (context.Self.PreviousActionResolution
            is not
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous)
        {
            return new Position(-1, -1);
        }
        foreach (GenericActorActionArgument argument
                 in previous.AcceptedAction.Arguments)
        {
            if (argument is GenericActorActionArgument.DirectionArgument direction)
            {
                (int dx, int dy) = direction.Value.Vector();
                return context.Self.Position.Offset(dx, dy);
            }
        }
        return new Position(-1, -1);
    }

    // ------------------------------------------------------------------
    // Action plumbing
    // ------------------------------------------------------------------

    private static GenericActorDecision Move(
        GenericActorActionLegality move,
        Direction direction,
        string reason) =>
        new(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);

    private GenericActorDecision? Rotate(
        ContractLens lens,
        GenericActorContext context,
        Direction direction,
        string reason)
    {
        GenericActorActionLegality? rotate = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            allowed = rotate is null
                ? null
                : Constraint<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>(rotate);
        if (rotate is null
            || allowed is null
            || !allowed.AllowedValues.Contains(direction))
        {
            return null;
        }
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(direction)],
            reason);
    }

    private static List<GenericActorActionLegality> AvailableByKind(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        var matches = new List<GenericActorActionLegality>();
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (action.Available && lens.KindOf(action.ActionId) == kind)
                matches.Add(action);
        }
        return matches;
    }

    private static GenericActorActionLegality? FirstAvailable(
        ContractLens lens,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (action.Available && lens.KindOf(action.ActionId) == kind)
                return action;
        }
        return null;
    }

    private static TConstraint? Constraint<TConstraint>(
        GenericActorActionLegality action)
        where TConstraint : GenericActorActionLegality.ArgumentConstraint
    {
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            if (constraint is TConstraint typed)
                return typed;
        }
        return null;
    }

    /// <summary>
    /// Last resort that always produces one legal bounded action, whatever the
    /// current form's mask happens to allow.
    /// </summary>
    private static GenericActorDecision Fallback(
        GenericActorContext context,
        ContractLens lens,
        string reason)
    {
        GenericActorActionLegality? wait = FirstAvailable(
            lens,
            context,
            GenericActorRulesContract.ActionKind.Wait);
        if (wait is not null)
        {
            return GenericActorDecision.WithoutArguments(
                wait.ActionId,
                wait.ActionCode,
                reason);
        }

        foreach (GenericActorActionLegality action in context.ActionLegalities)
        {
            if (!action.Available)
                continue;
            if (TryBuildArguments(
                    action,
                    out ImmutableArray<GenericActorActionArgument> arguments))
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

    private static bool TryBuildArguments(
        GenericActorActionLegality action,
        out ImmutableArray<GenericActorActionArgument> arguments)
    {
        var built = new List<GenericActorActionArgument>();
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in action.Constraints)
        {
            switch (constraint)
            {
                case GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint directions:
                    if (directions.AllowedValues.IsEmpty)
                    {
                        arguments = [];
                        return false;
                    }
                    built.Add(new GenericActorActionArgument.DirectionArgument(
                        directions.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint units:
                    if (units.AllowedValues.IsEmpty)
                    {
                        arguments = [];
                        return false;
                    }
                    built.Add(new GenericActorActionArgument.UnitTargetArgument(
                        units.AllowedValues[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint forms:
                    if (forms.AllowedFormIds.IsEmpty)
                    {
                        arguments = [];
                        return false;
                    }
                    built.Add(new GenericActorActionArgument.FormTargetArgument(
                        forms.AllowedFormIds[0]));
                    break;
                case GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint headings:
                    if (headings.AllowedValues.IsEmpty)
                    {
                        arguments = [];
                        return false;
                    }
                    built.Add(
                        new GenericActorActionArgument.ProjectileHeadingArgument(
                            headings.AllowedValues[0]));
                    break;
                default:
                    break;
            }
        }
        arguments = [.. built];
        return true;
    }
}
