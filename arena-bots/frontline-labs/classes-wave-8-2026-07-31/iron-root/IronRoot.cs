using BotArena.Sdk;

/// <summary>
/// IRON ROOT — the FORTRESS ROTATOR doctrine, revision 5 (OPEN ROOT).
///
/// <para><b>What revision 5 is.</b> One idea: <i>the root is rented, not sold,
/// so the answer to a gun is an arc and the answer to numbers is a turret.</i>
/// </para>
///
/// <para>Three declarations moved, and each of them turns a revision-4 rule from
/// correct into backwards.</para>
///
/// <para><b>One: the placement tag stopped binding.</b> Every same-life route now
/// declares an EMPTY <c>forbiddenTileTags</c> while the map still publishes 112
/// tiles tagged <c>transition-placement-forbidden</c>. Revision 4 asked the map,
/// so it refused to armour or root on the entire scoring surface and the central
/// corridor — a third of the walkable board, and precisely the third worth
/// standing on. Asking the ROUTE is right on both arms: where the tag reappears
/// in a route's own list this behaves exactly as revision 4 behaved.</para>
///
/// <para><b>Two: the turret became a cycle.</b> <c>irreversibleForLife</c> is
/// false on the anchor route and a route back exists, so anchor and mobilize are
/// unlimited for the life. Revision 4's whole tenure gate exists because the root
/// was a SALE — a body that gave away its objective weight could never take it
/// back, so the gate demanded relief already standing on the ground and a front
/// that would hold for a full capture window. Under a rental the price is two
/// declared windups and the gate is asking for collateral on a loan it is not
/// taking out. What replaces it is arithmetic the contract also declares: health
/// maps by ratio with a floor of one in BOTH directions and no entry heal, so a
/// cycle at full health is exactly free (4/4 ⇄ 7/7, 5/5 ⇄ 7/7) and a cycle at
/// partial health pays the floor EVERY round trip. So the doctrine cycles freely
/// where the round trip costs nothing and treats it as a purchase where it does,
/// and it never flickers, because a windup is the price and two of these facing
/// each other would otherwise pay it forever.</para>
///
/// <para><b>Three: the mobile gun got 45 degrees.</b> The attack profile declares
/// a ±1 initial aim offset, which is an ordinary aim-only program with no bends.
/// Rotation is cardinal and headings are eight-way, so on a zero-offset arm half
/// of every ring was unreachable no matter how the body turned. This chassis is
/// the one that gains most and the reason is not the gun: the bulwark's vision is
/// OMNIDIRECTIONAL, so it has always seen the diagonal bodies it had to walk onto
/// a lane to answer. Now it shoots them where it stands — and every "does that
/// muzzle bear on me" question in the doctrine widened by the same declared
/// envelope, because a gun that can do this to me is any gun whose profile says
/// so.</para>
///
/// <para><b>Shell discipline is the assignment, and the measurement was
/// opponent-shaped.</b> The arc is strong against a muzzle and a trap against
/// numbers: it cannot move, cannot rotate, and covers ONE quadrant, so a second
/// body walking around it is free damage and the tile is what pays. The rule is
/// therefore a count rather than a class name — how many hostile bearings bear on
/// this tile inside the stance's own window, and how many of them the arc
/// covers — and when the arc would be the minority answer the shield is declined.
/// It is declined IN FAVOUR of something, which is the part that needed the
/// cycle: a turret is omnidirectional, tougher, and faster, so <i>against poke
/// raise the arc, against numbers root the gun.</i></para>
///
/// <para>Previous revision: FORTRESS ROTATOR revision 4 (AEGIS COUNT). Its idea
/// stands unchanged wherever the declarations do: <i>this class buys ground with
/// weight that cannot be removed, and every bolt is priced by the heading it
/// arrives on.</i></para>
///
/// <para><b>The shape underneath, unchanged since revision 2.</b> One body
/// fortifies forward and every other body screens the scoring surface and
/// contests rather than concedes, because territory is the only currency. A root
/// is priced in what it buys: covering fire over ground somebody else is standing
/// on, for long enough that the presence becomes territory. Presence itself is
/// two alternating currencies wherever a capture definition declares a hold —
/// inside ours a body on the surface buys ground and a zero-weight gun buys
/// nothing, inside theirs it is exactly reversed — and the hold's owner and clock
/// are published fields, so they are asked rather than reconstructed. Revision 4
/// added the guard stance and the fire control that prices every bolt by the
/// heading it arrives on. The frozen wave-1..4 trees carry that history in full;
/// this file documents what is live.</para>
///
/// <para>Nothing below names a rule or an arm. Forms, routes, windups, health
/// transfer policies, reversibility, placement legality, aim envelopes, reach,
/// cadence, objectives, slot counts, rebuild economies, tile legality,
/// movement/facing coupling, hold ticks, control arithmetic, decay clock, return
/// placement, and action codes are all read from the resolved contract and the
/// current legality mask. Canonical contracts omit inert fields, so a rule about
/// something the contract does not declare is PROVABLY inert rather than merely
/// usually quiet — and one artifact plays every arm without re-authoring.</para>
/// </summary>
public sealed class IronRoot : IGenericActorBot
{
    /// <summary>Ticks of clear air that count as simply "safe".</summary>
    private const int SafeHorizon = 4;

    private ContractLens? _lens;

    /// <summary>
    /// The declared capture arithmetic. Under a channelling policy taking
    /// ground stops being "stand here" and becomes "stand STILL here, and do
    /// not get shot while you do it" — so every presence decision in this file
    /// consults it rather than the binary-control reading revision 6 shipped.
    /// </summary>
    private ChannelRules? _channel;

    /// <summary>This tick's channel arithmetic, derived from the observation.</summary>
    private readonly ChannelState _state = new();

    /// <summary>The declared battlefield economy, and the store's verb.</summary>
    private Salvage? _salvage;

    /// <summary>Forensics: channel decisions this life has taken.</summary>
    private int _locks;
    private int _screens;
    private int _denials;

    /// <summary>
    /// The map's own traffic geometry: which tiles are 1-tile lanes, which of
    /// them are one corridor run, and what walling any tile costs a body that
    /// wants to be somewhere. Built once per life from the resolved map.
    /// </summary>
    private Traffic? _traffic;

    /// <summary>
    /// Tiles a senior sibling holds or could step onto this tick, derived this
    /// tick from the frozen observation. Recomputed every tick because it is
    /// entirely a fact about now; never remembered, because a life that
    /// remembers a sibling's route is remembering something that has changed.
    /// </summary>
    private readonly HashSet<Position> _claims = [];

    /// <summary>The tile my own next arrival will take, when one is due.</summary>
    private Position? _rally;

    /// <summary>Forensics counters, printed in decision text so a replay can be
    /// read without a debugger: lane refusals, gates taken, yields.</summary>
    private int _laneRefusals;
    private int _gates;
    private int _yields;

    private readonly Dictionary<string, (Position Tile, int Tick)> _seen =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Deflections each visible guard has already spent, keyed by life, counted
    /// from the published deflection events. The stance's budget is a declared
    /// counter that restarts on entry and never survives the form, so the tally
    /// is dropped the moment that body is seen outside a guarding form. This is
    /// what turns "poke it and hope" into "one more bolt breaks it".
    /// </summary>
    private readonly Dictionary<string, int> _deflected =
        new(StringComparer.Ordinal);

    /// <summary>Lives seen in a guarding form last tick, to expire tallies.</summary>
    private readonly HashSet<string> _guardsLastSeen = new(StringComparer.Ordinal);

    /// <summary>Ticks this life has spent inside a guard stance, for forensics.</summary>
    private int _guardSinceTick = int.MaxValue;

    /// <summary>
    /// The tick each visible enemy was last seen or heard to attack. An enemy's
    /// cooldown is redacted, but its CADENCE is not: the attack is a published
    /// event and the cooldown is a declared number on its form's profile, so
    /// "when can that muzzle fire again" is a subtraction rather than a guess.
    /// This is what lets a windup-one shield be raised BEFORE the shot instead
    /// of after the bolt, which is the difference between a shield and a
    /// souvenir.
    /// </summary>
    private readonly Dictionary<string, int> _lastAttack =
        new(StringComparer.Ordinal);

    /// <summary>Last tick this body's own arc actually turned a bolt.</summary>
    private int _lastTurnTick = int.MinValue / 4;

    /// <summary>
    /// Tick the previous stance ended, and whether it turned anything. Together
    /// they are the hysteresis that keeps a windup-one stance from becoming a
    /// flicker — see <see cref="TryRaiseShell"/>.
    /// </summary>
    private int _stanceEndedTick = int.MinValue / 4;
    private bool _stanceEarnedIt = true;

    /// <summary>
    /// Tick this life last completed a leg of the turret cycle, in either
    /// direction. A reversible route with a one-tick windup is cheap enough to
    /// thrash — which is exactly how revision 4's windup-one shell livelocked
    /// before its hysteresis clause — so a leg buys silence for the cost of the
    /// full cycle. The number is the two routes' own declared windups, never a
    /// chosen constant.
    /// </summary>
    private int _cycledTick = int.MinValue / 4;

    /// <summary>
    /// Why the shield was last declined, when the reason was ENVELOPMENT rather
    /// than anything else. A quadrant arc is the wrong tool against bodies on
    /// several bearings, and the right tool is the omnidirectional one — so the
    /// refusal is handed to the anchor instead of thrown away.
    /// </summary>
    private bool _arcOutnumbered;

    /// <summary>This tick's hold phase, from the published pair.</summary>
    private HoldPhase _phase = HoldPhase.None;
    private int _phaseRemaining;

    /// <summary>
    /// Whether the phase was READ rather than guessed. Two gates, not one,
    /// because the two kinds of decision have wildly different costs: a
    /// reversible preference (which tile to prefer, whether to refuse a root)
    /// may act on a prior, since being wrong there costs at most one decision.
    /// A one-use irreversible route may not.
    ///
    /// <para>Revision 4 usually sets this from the observation, where it is a
    /// fact and not a confidence: the mode publishes the hold's owner and the
    /// tick its protection lifts. Revision 3's three-step inference survives
    /// only for the case the observation cannot answer.</para>
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
        _traffic = new Traffic(_lens.Map);
        _channel = new ChannelRules(start.Contract);
        _salvage = new Salvage(start.Contract);
        _state.Reset();
        _locks = 0;
        _screens = 0;
        _denials = 0;
        _claims.Clear();
        _rally = null;
        _laneRefusals = 0;
        _gates = 0;
        _yields = 0;
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
        _phase = HoldPhase.None;
        _phaseRemaining = 0;
        _phaseCertain = true;
        _phaseTrusted = false;
        _forwardReturn = false;
        _deflected.Clear();
        _guardsLastSeen.Clear();
        _lastAttack.Clear();
        _guardSinceTick = int.MaxValue;
        _lastTurnTick = int.MinValue / 4;
        _stanceEndedTick = int.MinValue / 4;
        _stanceEarnedIt = true;
        _cycledTick = int.MinValue / 4;
        _arcOutnumbered = false;
    }

    public GenericActorDecision Tick(GenericActorContext context)
    {
        try
        {
            return Decide(context);
        }
        catch (Exception)
        {
            // A bounded legal action always beats a fault. Nothing clever here:
            // the store's verb is not a recovery, and a tick that already went
            // wrong is the wrong tick to spend a team resource on.
            return SafeAction(
                context,
                "falling back to a legal action",
                idle: false);
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

        GenericActorRulesContract.Form? form = lens.Form(context.Self.FormId);
        var mode = context.Mode
            as GenericActorContext.ModeObservationState.Frontline;
        int activeIndex = mode?.ActivePositionIndex ?? -1;

        // THE CHANNEL IS READ BEFORE ANYTHING DECIDES ANYTHING, and it is read
        // even inside a windup, because the position memory it keeps must not
        // develop holes: a tick this life skipped is a tick it can no longer
        // say whether a body moved on. Cheap and unconditional beats clever.
        _state.Observe(lens, context, lens.ObjectiveTiles(activeIndex));

        // A committed windup is wait-only by declaration; do not fight it.
        if (context.Self.PendingSameLifeTransition is not null)
            return SafeAction(context, "riding out the transform windup", idle: false);

        // Whose progress is real this tick, and what a death costs. The first
        // is now ASKED rather than derived; the second is still geometry.
        ReadHold(context);
        _forwardReturn =
            lens.ForwardReturn(context.Self.ActorId.UnitId, activeIndex);

        Position[] active = lens.ObjectiveTiles(activeIndex);
        List<Gunnery.Target> targets = BuildTargets(lens, context, active);

        // Three lanes, chosen by what this form DECLARES rather than by what it
        // is called. A guarding stance keeps its objective weight and gives up
        // its gun; a fortified form gives up its weight and keeps a better gun.
        // Revision 3 had one lane for "cannot move" and would have driven the
        // guard stance with the turret's rules.
        if (lens.IsGuarded(context.Self.FormId))
            return ShellTick(lens, context, mode, active, targets);
        if (lens.IsFortified(context.Self.FormId))
            return FortressTick(lens, context, form, mode, active, targets);
        if (lens.IsStatic(context.Self.FormId))
            return SafeAction(context, "immobile and unarmed: waiting it out");
        return FieldTick(
            lens, context, form, mode, active, targets, activeIndex);
    }

    // ------------------------------------------------------- the channel game

    /// <summary>
    /// Is this body's own stillness load-bearing THIS tick?
    ///
    /// <para>G1, and it is one subtraction rather than a heuristic. Claim weight
    /// counts only bodies whose tile did not change; denial weight counts all of
    /// them. So stepping off a claim I am paying into costs my team exactly the
    /// difference between the gain rate with me still and the gain rate with me
    /// moving — and the declared CAP is what makes that difference frequently
    /// ZERO. A third stationary body on a surplus already at the ceiling buys no
    /// speed at all, so it is free to leave, and this same subtraction is what
    /// tells it so. That is why the rule is written as a rate comparison and not
    /// as "am I standing on the objective".</para>
    ///
    /// <para>It answers for EROSION too without a second branch: eroding an enemy
    /// claim runs the same stillness gate and the same cap at a declared
    /// multiple, so a rate that is higher when I hold still is higher when I hold
    /// still, whichever direction the number is moving.</para>
    /// </summary>
    private bool StillnessPays(ContractLens lens, GenericActorContext context)
    {
        ChannelRules? rules = _channel;
        if (rules is null || !rules.Channels || !Garrison.Stillness)
            return false;
        if (_state.SelfWeight <= 0 || !_state.SelfStill)
            return false;
        _ = lens;
        _ = context;
        return rules.RateFor(_state.SurplusIfIMove)
            < rules.RateFor(_state.Surplus);
    }

    /// <summary>
    /// True while my team owns the running claim on the active surface — the
    /// only state in which damage my bodies take on that surface costs progress.
    /// </summary>
    private bool WeControl(
        GenericActorContext.ModeObservationState.Frontline? mode,
        ContractLens lens) =>
        mode?.ClaimingTeamId is int claimant && claimant == lens.TeamId;

    /// <summary>
    /// True while the opposition owns a running claim worth eroding. G5: a
    /// standing enemy claim erodes at the declared multiple, so a two-tick
    /// window of control can undo eight ticks of theirs — which makes walking
    /// onto a contested surface the highest-rate action on the board, and it is
    /// the contract that says so rather than the doctrine.
    /// </summary>
    private bool ErosionWaiting(
        GenericActorContext.ModeObservationState.Frontline? mode,
        ContractLens lens)
    {
        ChannelRules? rules = _channel;
        if (rules is null || !rules.Channels || !Garrison.Erosion)
            return false;
        if (rules.ErosionMultiplier <= 1)
            return false;
        return mode?.ClaimingTeamId is int claimant
            && claimant != lens.TeamId
            && mode.CaptureProgress > 0;
    }

    /// <summary>
    /// How many of my bodies the surface is worth holding with, under the
    /// declared cap.
    ///
    /// <para>G4, and its sign flips with who is building. While my team builds,
    /// a body beyond <c>enemy denial + cap</c> buys no speed and adds one more
    /// place a bolt can land that reverts the whole run — so it belongs on the
    /// firing line instead. While the opposition builds, or while nobody does,
    /// every body on the surface is denial weight that subtracts directly from
    /// their multiplier, and the more of them the better. Revision 6's ranking
    /// filled the surface unconditionally, which is right in one of those two
    /// states and expensive in the other.</para>
    /// </summary>
    private int SurfaceWanted(
        ContractLens lens,
        GenericActorContext.ModeObservationState.Frontline? mode,
        int surfaceTiles)
    {
        ChannelRules? rules = _channel;
        if (rules is null
            || !rules.Channels
            || !Garrison.CapDiscipline
            || rules.GainCap <= 0)
        {
            return surfaceTiles;
        }
        if (!WeControl(mode, lens) || ErosionWaiting(mode, lens))
            return surfaceTiles;
        return Math.Clamp(_state.Theirs + rules.GainCap, 1, surfaceTiles);
    }

    /// <summary>
    /// Does <paramref name="tile"/> stand between a live hostile muzzle and one
    /// of my bodies that is paying into a claim, WITHOUT standing on the surface
    /// itself?
    ///
    /// <para>G3, the escort pattern, and nothing was added to the game for it:
    /// projectiles stop on the first enemy actor, allied projectiles pass
    /// through, and only damage taken ON the region reverts anything. Those
    /// three declared facts together mean a body one tile off the surface eats a
    /// bolt aimed at the channeler for FREE — it loses health, the run loses
    /// nothing, and my own return fire is not blocked. The gate is the interrupt
    /// scope: where damage reverts wherever it lands, a screen buys nothing and
    /// this returns false everywhere.</para>
    /// </summary>
    private bool ScreensAChanneler(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        HashSet<Position> surface)
    {
        ChannelRules? rules = _channel;
        if (rules is null || !Garrison.Screen || !rules.ScreeningIsFree)
            return false;
        if (surface.Contains(tile) || context.Enemies.Length == 0)
            return false;

        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!surface.Contains(ally.Position))
                continue;
            if ((lens.Form(ally.FormId)?.ObjectiveWeight ?? 0) <= 0)
                continue;
            foreach (GenericActorContext.ObservedEnemyState enemy
                in context.Enemies)
            {
                if (lens.Attack(lens.Form(enemy.FormId)) is null)
                    continue;
                if (Between(lens, enemy.Position, ally.Position, tile))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// True when <paramref name="tile"/> lies strictly on the clear ray from
    /// <paramref name="from"/> to <paramref name="to"/>. Eight-way rays are the
    /// only paths a bolt travels, so a tile off the ray screens nothing.
    /// </summary>
    private static bool Between(
        ContractLens lens,
        Position from,
        Position to,
        Position tile)
    {
        if (tile == from || tile == to)
            return false;
        if (!ArenaGeometry.TryRay(from, to, out ProjectileHeading heading, out int span))
            return false;
        if (!ArenaGeometry.TryRay(from, tile, out ProjectileHeading own, out int step))
            return false;
        if (own != heading || step >= span)
            return false;
        return ArenaGeometry.ClearRay(lens.Map, from, tile, strictCorners: true);
    }

    /// <summary>
    /// Progress a rooted gun with a live line reverts per tick, scaled by the
    /// gun's own declared cadence so the comparison against a weight loss is
    /// exact rather than assumed. Returned as a numerator over
    /// <paramref name="perTicks"/> so no division rounds a rate to zero.
    /// </summary>
    private int TurretRevertRate(
        ContractLens lens,
        string? formId,
        out int perTicks)
    {
        perTicks = 1;
        ChannelRules? rules = _channel;
        if (rules is null || rules.RevertPerDamagePoint <= 0)
            return 0;
        GenericActorRulesContract.FormTransition? anchor =
            lens.AnchorRoute(formId);
        GenericActorRulesContract.AttackProfile? gun =
            lens.Attack(lens.Form(anchor?.TargetFormId));
        if (gun is null)
            return 0;
        perTicks = Math.Max(1, gun.CooldownTicks);
        return rules.RevertPerDamagePoint
            * Math.Max(1, gun.Projectile.DamagePerHit);
    }

    /// <summary>
    /// Whose completed advance is protected right now, and for how long.
    ///
    /// <para>THE READER THAT REPLACED A FILE. The mode observation publishes
    /// <c>holdOwnerTeamId</c> and <c>holdEndsAtTick</c>, which is the whole of
    /// what revision 3 reconstructed through three chained derivations: a clock
    /// recovered from <c>ControlResumesAtTick</c> minus the declared pause, an
    /// owner watched from an index change, an owner proved from a capture that
    /// collapsed without moving the front, and failing all of those an owner
    /// guessed from the signed displacement of the front. The guess was wrong
    /// after an opponent's first regression from a lead and unavailable to a
    /// life born inside the hold, because private memory is life-scoped. Asking
    /// is exact, needs no memory, and is right on a body's first tick.</para>
    ///
    /// <para>Null is an ANSWER, not a gap: no hold binds this tick, including
    /// on every ruleset whose capture definition declares none.</para>
    ///
    /// <para><b>WAVE 5 DELETED THE FALLBACK, and the reason is worth recording
    /// because it is not a doctrine reason.</b> Revision 4 kept the whole
    /// inference — a separate file — as a contradiction check for a contract that
    /// declares a hold duration while the observation names no owner, and its own
    /// DX report stated that the branch is unreachable on every contract this
    /// lineage can run. The controlled toolchain caps submitted sources at 256 KB
    /// and revision 4 froze at 250.6 KB, so this revision's contract reads did not
    /// fit beside a file that provably never executes. Unreachable code that costs
    /// budget is the cheapest thing on the board to cut, and saying so is more
    /// honest than pretending it was a design decision.</para>
    /// </summary>
    private void ReadHold(GenericActorContext context)
    {
        if (ArenaBasics.LiveHold(context) is ArenaBasics.Hold hold)
        {
            _phase = hold.Mine ? HoldPhase.Ours : HoldPhase.Theirs;
            _phaseRemaining = Math.Max(0, hold.RemainingTicks);
            _phaseCertain = true;
            _phaseTrusted = true;
            return;
        }

        _phase = HoldPhase.None;
        _phaseRemaining = 0;
        _phaseCertain = true;
        _phaseTrusted = false;
    }

    // --------------------------------------------------------------- shielded

    /// <summary>
    /// Inside the guard stance. The stance cannot move, shoot, or rotate, so
    /// there is exactly one decision here and it is <em>when to drop</em>.
    ///
    /// <para>What the stance is buying is stated by the contract, not by me: the
    /// form keeps its objective weight, so this body is still holding ground,
    /// and its declared guard turns a bolt arriving inside the arc into a bolt
    /// of ours launched from this tile back along the ray the shooter is
    /// standing on. Against fire, that is the best rate this chassis has: zero
    /// damage in, one damage out, ground kept. Against no fire it is a body with
    /// no gun, which is why leaving early is a real decision the route
    /// explicitly permits.</para>
    ///
    /// <para>Three reasons to drop, in order of how much they cost to get
    /// wrong:</para>
    /// <list type="number">
    /// <item><b>The arc is not the answer.</b> A bolt is inbound that the arc
    /// will NOT catch — a flank or rear contact damages normally and this body
    /// cannot step out of the way — and eating it would leave it too thin to
    /// hold anything. Drop and become a body that can move again.</item>
    /// <item><b>Nothing is inbound and there is work.</b> The exit windup plus
    /// the gun's own readiness is the price of returning to fire; pay it while
    /// no bolt is in flight rather than while one is.</item>
    /// <item><b>The ground stopped paying.</b> The front rotated away, so
    /// standing here armoured is standing nowhere.</item>
    /// </list>
    /// </summary>
    private GenericActorDecision ShellTick(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode,
        Position[] active,
        List<Gunnery.Target> targets)
    {
        if (_guardSinceTick == int.MaxValue)
            _guardSinceTick = context.Tick;

        GenericActorRulesContract.FormTransition? exit =
            lens.ReverseRoute(context.Self.FormId);
        if (exit is null)
            return SafeAction(context, "shielded with no declared way back");

        int exitWindup = Math.Max(1, exit.Windup.DurationTicks);
        bool holding = Contains(active, context.Self.Position);

        // The arc is live now, so it answers everything from this tick onward.
        (int caught, int uncaught, int soonestUncaught) = ArcPressure(
            lens,
            context,
            context.Self.Position,
            context.Self.Facing,
            liveInTicks: 0);

        // (1) The arc is not the answer to this bolt and the body cannot dodge.
        bool bleeding = uncaught > 0
            && context.Self.Health - uncaught < 1
            && soonestUncaught >= exitWindup;

        // (3) Armour over ground that is no longer the ground.
        bool stranded = active.Length > 0
            && !holding
            && ArenaGeometry.NearestDistance(context.Self.Position, active)
                > Math.Max(1, lens.CaptureThreshold / 4);

        // (2) Nothing bears on the arc; the gun is the better use of the next
        // ticks. "Work" is a visible enemy at all: a body with no gun and
        // nothing to turn is spending the stance on nothing.
        bool idle = caught == 0
            && uncaught == 0
            && (targets.Count > 0 || !holding);

        // (4) THE ANTI-STALEMATE CLAUSE, and it exists because the first draft
        // needed it. Two guards facing each other turn nothing, spend no budget,
        // and hold their tiles forever: a shield is only a trade while somebody
        // is shooting. So patience is the opposition's own declared cadence — if
        // an arc that something bears on has turned nothing for longer than the
        // slowest visible muzzle takes to come round again, that muzzle has
        // decided not to fire, and standing armoured in front of it is just a
        // body with no gun. Drop and make it a duel again.
        int patience = SlowestVisibleCadence(lens, context)
            + exitWindup
            + Math.Max(1, lens.CycleCost(context.Self.FormId, exit.TargetFormId));
        int quiet = context.Tick
            - Math.Max(_guardSinceTick, _lastTurnTick);
        bool refused = caught > 0 && quiet > patience;

        // (5) C1 AS AN EXIT — THE CLAUSE THAT ACTUALLY REMOVES THE OWNER'S BUG.
        //
        // Entry gating is not enough on its own, and the replays say why: the
        // shell is raised on a tile nobody needs, and then a companion unlocks at
        // its declared tick, or the front rotates, and the tile that cost nobody
        // anything is suddenly the only lane a sibling has. Nothing about the
        // shell's own state changed, so every one of the four reasons above stays
        // false and the plug stays in. Asking the traffic question from INSIDE the
        // stance every tick is what turns a rule about placement into a rule about
        // occupancy. The stance's own budget cannot do this: it is spent by the
        // ENEMY's decision to fire, so a shell nobody shoots at never leaves.
        bool walling = !LaneStaysOpen(
            lens, context, context.Self.Position, active, out int laneMine, out _);

        if (bleeding || stranded || idle || refused || walling)
        {
            _stanceEndedTick = context.Tick;
            _stanceEarnedIt = _lastTurnTick >= _guardSinceTick;
            GenericActorDecision? drop = BuildTransition(
                context,
                exit,
                bleeding
                    ? "dropping the shield: the arc does not cover this one"
                    : stranded
                        ? "dropping the shield: the front moved on"
                        : refused
                            ? $"dropping the shield: nothing fired in {quiet}"
                            : walling
                                ? $"dropping the shield: clearing a lane my own "
                                    + $"traffic needs by {laneMine}"
                                : "dropping the shield: nothing to turn, work to do");
            if (drop is not null)
                return drop;
        }
        _ = mode;
        return SafeAction(
            context,
            caught > 0
                ? $"shield up: returning {caught}"
                : "shield up: holding the tile");
    }

    /// <summary>
    /// Raise the shield. The gate is this chassis's own fire cadence, and that
    /// is the whole point.
    ///
    /// <para>A gun that declares a cooldown of <c>n</c> ticks is idle for
    /// <c>n</c> ticks between shots. The guard route declares an entry windup
    /// and the return route an exit windup. When the two windups fit inside the
    /// idle window, a cycle in and out of the stance is spent entirely on ticks
    /// the gun could not have fired on anyway — the shield is FREE, and refusing
    /// to raise it is refusing a strictly better tick. Both numbers come from
    /// the contract, so a chassis with a fast gun correctly declines and one
    /// with a slow gun correctly cycles.</para>
    ///
    /// <para>It is only worth raising against a bolt the arc will actually
    /// catch, arriving late enough for the windup to complete: a transition
    /// retains the source form through combat, so a bolt landing on the tick the
    /// route is requested lands on an unarmoured body. And it is only raised on
    /// ground worth keeping, because a stance is not evasion — this body would
    /// rather step out of a lane than armour a tile that pays nobody.</para>
    /// </summary>
    private GenericActorDecision? TryRaiseShell(
        ContractLens lens,
        GenericActorContext context,
        Position[] active,
        bool holding,
        bool stationed,
        GenericActorDecision? shot,
        List<Gunnery.Target> targets)
    {
        GenericActorRulesContract.FormTransition? entry =
            lens.GuardRoute(context.Self.FormId);
        if (entry is null)
            return null;

        // ASK THE ROUTE, NOT THE MAP. Revision 4 tested the map's own
        // `transition-placement-forbidden` tag set, which on this arm covers the
        // whole scoring surface and the central corridor while every route's
        // forbidden list is empty — so it declined to armour precisely the tiles
        // worth armouring. Where the tag does bind, this refuses exactly as before.
        if (!lens.PlacementAllows(entry, context.Self.Position))
            return null;

        // C1 — A SHELL IS A WALL TOO, AND THIS IS THE ONE THE OWNER WATCHED.
        //
        // The shell keeps its objective weight, so the doctrine thinks of it as a
        // body that is still holding ground. It is — but it is also a body that
        // cannot move, cannot rotate, and sits there until its declared budget is
        // spent, and the enemy chooses when to spend it by choosing whether to
        // fire into the arc. Two of these facing each other across a 1-tile lane
        // is a plug in the lane for as long as neither shoots, and the replays
        // measure that plug at up to ninety-seven ticks with my own bodies
        // walking round it. The gate reading applies here exactly as it does to
        // the root: a lane only the opposition needs is a lane worth corking.
        if (!LaneStaysOpen(
                lens, context, context.Self.Position, active,
                out int laneMine, out _))
        {
            _ = laneMine;
            return null;
        }

        int entryWindup = Math.Max(1, entry.Windup.DurationTicks);
        int exitWindup = Math.Max(
            1,
            lens.ReverseRoute(entry.TargetFormId)?.Windup.DurationTicks ?? 1);

        // What the arc would turn once it is actually live, which is after the
        // entry windup and not before it.
        (int returning, int uncaught, _) = ArcPressure(
            lens,
            context,
            context.Self.Position,
            context.Self.Facing,
            liveInTicks: entryWindup);
        if (returning <= 0)
            return null;

        // Armour that leaves more damage on the tile than it turns is not
        // armour, it is an immobile body in a crossfire. Flank and rear contacts
        // hurt normally and the stance cannot rotate to answer them.
        if (uncaught >= returning && context.Self.Health - uncaught < 1)
            return null;

        // G6 — THE INTERRUPT PRICES THE SHIELD, AND IT DOUBLES WHAT IT IS WORTH.
        //
        // Revision 6 valued a deflection in health, which is what a deflection
        // was worth. Under a claim interrupt a bolt that lands on a controlling
        // body standing on the surface also reverts that team's whole run by the
        // declared amount per point — so for the body actually channelling, the
        // arc is not armour, it is the claim. A shell blanks its frontal
        // quadrant completely, which is the only way this class has of standing
        // on ground it is taking and being shot at for nothing.
        //
        // What it buys is bounded and the bound is declared: the return route's
        // budget is three deflections, so an arc lasts three bolts and then
        // forces a windup out and a windup back. That is roughly two ticks short
        // of a full channel at the declared threshold — a fact worth knowing
        // before the loss rather than after it — which is why this RAISES the
        // priority of the shield without touching the envelopment refusal below.
        // Against numbers the answer is still the gun.
        bool channelling = _channel is ChannelRules live
            && live.Channels
            && live.RevertPerDamagePoint > 0
            && _state.SelfWeight > 0
            && _state.SelfStill
            && live.RateFor(_state.Surplus) > 0;
        if (Garrison.Interrupt && channelling && !_arcOutnumbered)
        {
            return BuildTransition(
                context,
                entry,
                $"arc over the claim: turning {returning} at {_state.Claim}"
                + $" against {_state.Theirs}");
        }

        // SHELL DISCIPLINE — THE ASSIGNMENT, AND THE DECLINE IS THE WHOLE OF IT.
        // The count is <see cref="Enveloped"/>; this is the refusal it drives.
        if (_arcOutnumbered)
            return null;

        // NO GROUND CLAUSE, and that was a measured correction. The first draft
        // refused to armour a tile that was neither the scoring surface nor this
        // body's own post, on the reasoning that a stance which freezes a
        // travelling body costs it its errand. Measured, the clause refused
        // EVERY shield in the cell it was written for: on this map the duel
        // happens a tile or two off the surface, so the bodies that are under
        // fire are precisely the ones still walking. The errand costs two ticks;
        // dying costs the whole walk plus the return delay, and this lineage's
        // own replays put twenty-one deaths a side into a five-hundred-tick
        // match. The pressure filter below is already imminent by construction —
        // a muzzle more than one tick from firing is not counted — so "under a
        // gun that bears on the arc" is the only ground test worth having.
        _ = holding;
        _ = stationed;
        _ = active;

        // HYSTERESIS, and it exists because the first draft livelocked. Two of
        // these doctrines facing each other each raised a shield the tick the
        // other's muzzle came ready, saw nothing fired because the other had
        // also raised one, dropped, and raised again: two hundred and
        // twenty-three stance entries a match, zero deflections, zero advances,
        // and thirty-five ticks a side spent on the scoring surface. Neither
        // body was ever in danger and neither ever played.
        //
        // A windup-one stance is cheap enough to flicker, so the doctrine has to
        // supply what the contract does not: a stance that turned NOTHING was
        // facing a muzzle that declined to fire, and raising against that same
        // muzzle again before its declared cadence has come round is asking the
        // identical question and paying two ticks for the identical answer. So
        // one unearned stance buys silence for the opposition's own cadence, and
        // an arc that DID turn something may re-raise immediately — the muzzle
        // that fired into it has proved it will.
        int cadence = SlowestVisibleCadence(lens, context) + entryWindup + exitWindup;
        if (!_stanceEarnedIt && context.Tick - _stanceEndedTick < cadence)
            return null;

        // A KILL OUTRANKS ARMOUR. Everything else about this trade is a wash on
        // damage — a turned bolt costs the shooter exactly what this gun's own
        // bolt would have — so the one shot never given up is the one that
        // removes a body from the board, because removing the last body on a
        // contested surface is the only thing that converts to territory.
        if (shot is not null && KillShotAvailable(lens, context, targets))
            return null;

        // THE COOLDOWN SHADOW, which is why this is nearly free. A gun that
        // declares a cooldown of n ticks cannot fire for n ticks after it does;
        // the entry and exit windups are declared on the two routes. When both
        // fit inside that idle window, the entire cycle is spent on ticks the
        // gun could not have used, so the shield costs no fire at all. When they
        // do not fit, the shield is a real trade and has to justify itself: it
        // still turns at least as much damage as this gun deals, and it does it
        // without spending health, which is this chassis's actual advantage.
        int idleTicks = shot is not null
            ? 0
            : Math.Max(
                context.Self.Cooldown,
                lens.FireIdleTicks(context.Self.FormId));
        bool free = idleTicks >= entryWindup + exitWindup;
        int ownBolt = Math.Max(
            1,
            lens.Attack(lens.Form(context.Self.FormId))?.Projectile.DamagePerHit
                ?? 1);
        if (!free && returning < ownBolt)
            return null;

        return BuildTransition(
            context,
            entry,
            free
                ? $"shield up in the cooldown shadow: turning {returning}"
                : $"shield up over a shot: turning {returning}");
    }

    /// <summary>
    /// Is a quadrant arc the MINORITY answer to what is bearing on this body?
    ///
    /// <para>THE ASSIGNED RULE. The lab measured the shell as opponent-shaped:
    /// strong against poke, a trap against numbers, and raising it in front of
    /// envelopment lost games — the freed-placement experiment made that worse
    /// rather than better, because a better tile does not fix a wrong shape. The
    /// mechanism is entirely in the form's own declarations and needs no opponent
    /// model at all. The arc covers ONE quadrant. The stance cannot move, cannot
    /// shoot, and — the clause that does the damage — cannot ROTATE, so the
    /// protected quadrant is chosen before the shield rises and every body that
    /// gets outside it for the rest of the stance is hitting an immobile target
    /// for free.</para>
    ///
    /// <para>So the rule is a count, not a class name: one bearing is poke and the
    /// arc answers it completely; two bearings is a quadrant answering half a
    /// problem while frozen. And "numbers" has a DECLARED form too, which is what
    /// makes this generalise past the three classes that exist — a side with more
    /// unit slots than mine whose bodies rebuild faster than mine can afford to
    /// walk into a gun, and an immobile body is what it is affording. That raises
    /// the bar rather than setting it, because a body the opposition has not
    /// fielded yet cannot flank anything.</para>
    ///
    /// <para>The window is the stance's own declared cycle — the ticks this body
    /// would actually be frozen — taken from the shell's routes where a shell
    /// exists and from the anchor's where it does not, so the same count also
    /// selects the turret on a contract that declares no shell at all.</para>
    /// </summary>
    private bool Enveloped(ContractLens lens, GenericActorContext context)
    {
        GenericActorRulesContract.FormTransition? stance =
            lens.GuardRoute(context.Self.FormId)
            ?? lens.AnchorRoute(context.Self.FormId);
        if (stance is null)
            return false;
        int window = Math.Max(1, stance.Windup.DurationTicks)
            + Math.Max(
                1,
                lens.ReverseRoute(stance.TargetFormId)?.Windup.DurationTicks ?? 1);

        (int covered, int exposed) = Bearings(
            lens,
            context,
            context.Self.Position,
            context.Self.Facing,
            window);
        if (exposed <= 0)
            return false;
        bool outnumberedByContract = lens.EnemySlotCount > lens.OwnSlotCount
            || lens.EnemyRebuildTicks < lens.OwnRebuildTicks;
        return exposed >= covered || outnumberedByContract;
    }

    /// <summary>
    /// How many hostile SOURCES a quadrant arc on this tile would answer, and how
    /// many would shoot it from outside the arc, inside <paramref name="window"/>
    /// ticks.
    ///
    /// <para>Counted per source rather than per bearing, and that choice is the
    /// difference between a rule and a refusal. One body can reach several
    /// bearings if you give it long enough, so counting bearings makes a lone
    /// duelist look like an envelopment and declines every shield ever. Counting
    /// bodies asks the question the stance actually poses: while I am frozen for
    /// this many ticks, how many separate things can hurt me, and can this one
    /// quadrant be pointed at all of them?</para>
    ///
    /// <para>A body is EXPOSED when it can bring a muzzle to bear from outside the
    /// arc within the window, and covered only when every lane it can reach in
    /// time lies inside the arc. The travel budget is the declared movement
    /// coupling's own arithmetic, so the same rule is stricter on a free-strafing
    /// arm (where walking round the arc is one tick a tile) than on a
    /// facing-locked one (where it is a rotation, the walk, and a rotation) —
    /// which is correct, and is the contract deciding rather than me.</para>
    ///
    /// <para>Bolts already in flight are their own sources: a bolt has a heading
    /// and cannot be talked out of it.</para>
    /// </summary>
    private (int Covered, int Exposed) Bearings(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        Direction facing,
        int window)
    {
        int covered = 0;
        int exposed = 0;

        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == lens.TeamId)
                continue;
            if (Arriving(lens, context, projectile, tile) is null)
                continue;
            if (ArenaGeometry.GuardCatches(facing, projectile.Heading))
                covered++;
            else
                exposed++;
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? attack =
                lens.Attack(lens.Form(enemy.FormId));
            if (attack is null)
                continue;
            bool immobile = lens.IsStatic(enemy.FormId);
            HashSet<Position> lanes = FortressPlan.FiringTilesOn(
                lens.Map,
                tile,
                attack.Projectile.MaxTravelTiles,
                attack.Projectile.DiagonalCornersMustBeClear);

            bool insideInTime = false;
            bool outsideInTime = false;
            foreach (Position lane in lanes)
            {
                if (!ArenaGeometry.TryRay(
                        lane,
                        tile,
                        out ProjectileHeading arrival,
                        out _))
                {
                    continue;
                }
                int steps = enemy.Position.ChebyshevDistance(lane);
                if (immobile && steps > 0)
                    continue;
                // Only consulted for a muzzle already standing on the lane; a
                // body that has to walk pays the coupling's price instead.
                bool aimed = Gunnery.BearsOn(attack, enemy.Facing, arrival);
                int ready = Kinematics.TicksToFirstShot(
                    lens.Coupling(enemy.FormId),
                    steps,
                    attack.OmnidirectionalAim,
                    aimed);
                if (ready > window)
                    continue;
                if (ArenaGeometry.GuardCatches(facing, arrival))
                    insideInTime = true;
                else
                    outsideInTime = true;
            }
            if (outsideInTime)
                exposed++;
            else if (insideInTime)
                covered++;
        }
        return (covered, exposed);
    }

    /// <summary>
    /// True when some reachable target would die to one bolt from this gun. Read
    /// from the target's observed health and this gun's declared damage, so a
    /// contract with tougher bodies or heavier bolts moves the threshold
    /// without a code change.
    /// </summary>
    private static bool KillShotAvailable(
        ContractLens lens,
        GenericActorContext context,
        List<Gunnery.Target> targets)
    {
        int damage = Math.Max(
            1,
            lens.Attack(lens.Form(context.Self.FormId))?.Projectile.DamagePerHit
                ?? 1);
        foreach (Gunnery.Target target in targets)
        {
            if (target.Health <= damage)
                return true;
        }
        return false;
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
        int exitWindup = Math.Max(1, route.Windup.DurationTicks);

        // Can this body root again after leaving? The answer changes what leaving
        // IS. A one-use return is a rotation spent once a match and every trigger
        // below is written to hoard it. A reversible one is a gear change, and
        // hoarding a gear change is just refusing to steer.
        GenericActorRulesContract.FormTransition? back =
            lens.AnchorRoute(route.TargetFormId);
        bool reversible = !route.IrreversibleForLife && back is not null;
        int cycleTicks = exitWindup
            + Math.Max(1, back?.Windup.DurationTicks ?? 1);

        if (reversible && context.Tick - _cycledTick < cycleTicks)
            return null;

        // WEIGHT ON DEMAND — the trigger a one-use return could never have.
        //
        // A turret has objective weight zero, which is the whole bargain, and
        // under a sale that bargain was permanent: the tenure gate had to be
        // certain before the body committed, because being wrong meant a match
        // spent as a gun over ground nobody was holding. Under a rental the
        // bargain is revisited every tick for one windup, so the correct policy
        // is the cheap one: stand as a gun while the ground does not need weight,
        // and BE the weight the moment it does.
        //
        // "Needs weight" is the mode's own arithmetic, not a feeling. Somebody
        // else is claiming this objective and nobody of ours is standing on it,
        // or the declared control policy scales gain by surplus weight and ours
        // does not lead. And it has to be answerable: the exit windup plus the
        // walk has to fit inside what is left of their capture, or the weight
        // arrives after the ground is gone.
        bool weightWanted = false;
        if (reversible && mode is not null)
        {
            (int own, int enemy, _) =
                ArenaBasics.ObjectivePresence(lens.Contract, context);
            bool theirClaim = mode.ClaimingTeamId is int claimant
                && claimant != lens.TeamId
                && mode.CaptureProgress > 0;
            bool outweighed = lens.SurplusWeightScalesGain && own <= enemy;
            int reachable = ArenaGeometry.NearestDistance(
                context.Self.Position,
                lens.ObjectiveTiles(mode.ActivePositionIndex));
            int slack = theirClaim
                ? lens.CaptureThreshold - mode.CaptureProgress
                : lens.CaptureThreshold;
            weightWanted = (theirClaim || outweighed)
                && own <= enemy
                && exitWindup + reachable <= Math.Max(1, slack);

            // G2, THE EXIT HALF. A gun that has stopped paying is a body that
            // has stopped scoring, and under the channel both halves of that are
            // arithmetic. The gun pays while its declared revert rate covers the
            // weight it withheld; the moment my team could BUILD instead —
            // because the surface it would join already out-claims theirs, or
            // because a claim of theirs is standing there to erode at the
            // declared multiple — the weight is worth more than the bolts and
            // this body walks back onto the ground.
            if (Garrison.DenialExit
                && _channel is ChannelRules rules
                && rules.Channels)
            {
                int weight = lens.Form(route.TargetFormId)?.ObjectiveWeight ?? 1;
                bool couldBuild =
                    rules.RateFor(_state.Claim + weight - _state.Theirs) > 0;
                bool couldErode = ErosionWaiting(mode, lens);
                weightWanted = (couldBuild || couldErode)
                    && exitWindup + reachable <= Math.Max(1, slack);
            }
        }

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
        // supply presence inside it is a one-use route spent on a reset. Under a
        // reversible cycle the same fact still holds — presence inside their hold
        // buys nothing — so it suppresses the weight trigger too.
        if (_phase == HoldPhase.Theirs
            && _phaseTrusted
            && _phaseRemaining > lens.CaptureThreshold)
        {
            lastCall = false;
            weightWanted = false;
        }

        // C1 AS AN EXIT, the turret half. A root is the longer-lived of the two
        // walls and the one whose tile was chosen for coverage rather than for
        // traffic, so it is the more likely of the two to be in the way later.
        // Same question, same subtraction, asked from inside the form: a gun in a
        // lane my own bodies need is worth less than the lane.
        Position[] surface = lens.ObjectiveTiles(
            mode?.ActivePositionIndex ?? lens.NextObjectiveIndex(0));
        int laneMine = 0;
        bool walling = reversible
            && !LaneStaysOpen(
                lens, context, context.Self.Position, surface,
                out laneMine, out _);

        if (!rotate && !lastCall && !weightWanted && !walling)
            return null;

        GenericActorDecision? leaving = BuildTransition(
            context,
            route,
            walling
                ? $"unrooting: this gun walls a lane my own traffic needs "
                    + $"by {laneMine}"
                : rotate
                    ? $"front rotated: unrooting after {rooted} rooted ticks"
                    : weightWanted && !lastCall
                        ? $"weight wanted: unrenting the gun after {rooted} ticks"
                        : "last call: unrooting to put a body on the surface");
        if (leaving is not null)
            _cycledTick = context.Tick;
        return leaving;
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
        _guardSinceTick = int.MaxValue;

        // Union requirement: an explicitly fabricated companion is taken the
        // moment the mask offers one, exactly like a declared automatic one.
        // Where fabrication is explicit it is also the class's whole economy —
        // a chassis that never queues gets no companions at all — and where a
        // slot arm gives one side more slots than the other, the extra ones are
        // simply more entries in this same mask.
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

        // COORDINATION, COMPUTED ONCE AND SHARED BY EVERY DECISION BELOW.
        //
        // The order matters and it is not arbitrary. The rally tile is a fact
        // about the contract's clock, so it is read first and then kept OUT of
        // the post list — a post I am told not to take is better than a post I
        // have to be evicted from. The station list is next, because it is what
        // every body's goal is derived from, mine and my siblings'. The claims
        // come last because they are a function of the other two: who is going
        // where, and therefore whose tiles are not mine this tick.
        //
        // All three are recomputed every tick from the frozen observation and
        // none of them is remembered. A life that caches a sibling's route is
        // caching the one thing that changed.
        _rally = DueArrivalTile(
            lens, context, activeIndex, within: lens.RedeployPauseTicks + 2);
        List<Position> stations = Stations(
            lens, context, active, activeIndex, _rally, mode);
        BuildClaims(lens, context, stations, worth ? fortressActor : null);

        HashSet<Position> goals = SelectGoals(
            lens,
            context,
            fortress,
            objective,
            active,
            stations,
            worth ? fortressActor : null);
        bool stationed = goals.Count == 0 || goals.Contains(context.Self.Position);

        // G1 — STILLNESS IS THE CAPTURE, AND IT OUTRANKS THE POST.
        //
        // The whole grammar of taking ground changed under this arm: a body
        // that changes tile pays nothing into the claim it is standing in. So a
        // body already paying is STATIONED by definition, whatever the ranking
        // thinks of its tile — walking to a better post costs a tick of gain and
        // buys a tile that was only ever a proxy for gain. Revision 6 had no way
        // to think this: it ranked posts, walked to them, and treated arriving
        // as the goal.
        //
        // The lock is a rate comparison, so it releases itself the moment the
        // step is free: at the declared cap a third stationary body's departure
        // costs nothing, and this body is told so by the same subtraction that
        // pins the first two.
        bool locked = StillnessPays(lens, context);
        if (locked)
        {
            stationed = true;
            _locks++;
        }
        else if (Garrison.Screen
            && !objective.Contains(context.Self.Position)
            && goals.Contains(context.Self.Position)
            && ScreensAChanneler(lens, context, context.Self.Position, objective))
        {
            _screens++;
        }

        GenericActorDecision? shot = Gunnery.TryFire(lens, context, form, targets);

        // THE ENVELOPMENT COUNT, computed once because it decides two things.
        //
        // Both immobilising forms this class owns are answers to being shot at,
        // and they are answers to DIFFERENT NUMBERS of things shooting. The
        // quadrant arc answers one bearing completely and cannot be turned toward
        // a second. The turret answers every bearing and pays objective weight for
        // it. So the same count — how many hostile sources bear on this tile
        // inside the frozen window, and how many of them one quadrant covers —
        // selects between them, and a doctrine that computes it twice with two
        // windows would be able to disagree with itself.
        _arcOutnumbered = Enveloped(lens, context);

        // THE REVISION AT THE MOMENT IT DECIDES SOMETHING, and it decides
        // BEFORE the shot rather than as a reaction to a bolt.
        //
        // Revision 4's first draft put this inside the bolt-response block
        // below, which is where every other threat answer lives, and the
        // measured result was thirty-four ticks a match inside the stance and
        // zero deflections: a bolt is only visible once it is already too close
        // for a windup, and at this chassis's duelling distance it lands the
        // tick after it is fired. The shield answers the MUZZLE. That moves the
        // decision one tick earlier and out of the reactive block entirely.
        GenericActorDecision? shield = TryRaiseShell(
            lens,
            context,
            active,
            holding,
            stationed,
            shot,
            targets);
        if (shield is not null)
            return shield;

        // AGAINST POKE RAISE THE ARC, AGAINST NUMBERS ROOT THE GUN.
        //
        // The shield was just declined for one specific reason — the arc would
        // have been the minority answer to the bodies bearing on this tile — and
        // that refusal is worth something rather than nothing. A quadrant is the
        // wrong shape for envelopment; the right shape is already in the catalog
        // and it is the one this class has always owned. A turret sees and fires
        // OMNIDIRECTIONALLY, carries the highest declared health in the class, and
        // fires on the shortest declared cooldown, so it answers three bearings as
        // easily as one. What it gives up is the objective weight — which is the
        // whole bargain, is why the tenure arithmetic above still runs, and is
        // recoverable in one declared windup now that the route reverses.
        //
        // It roots WHERE IT STANDS rather than at the best site on the map, which
        // is the one place this deliberately relaxes the site floor: the reason to
        // commit is the crossfire on this tile, not the ranking of this tile.
        //
        // IT DOES NOT RELAX THE BARGAIN, and the first draft did. Written without
        // the `worth` term this branch anchored whenever two things were shooting,
        // margin or no margin, and it converted the body holding the ground into a
        // gun over ground nobody held. Ablated, it lost ten cells out of ten by
        // sixty points of territory each — a whole match's worth of front, every
        // time. The turret bargain is not suspended by being shot at; being shot at
        // is when a doctrine most wants to believe it is.
        if (_arcOutnumbered && worth && (holding || stationed))
        {
            GenericActorDecision? dugIn = TryAnchor(
                lens,
                context,
                active,
                anyCoveringTile: true);
            if (dugIn is not null)
                return dugIn;
        }

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

        // A cast is one entry, one fan, one automatic return: the budget on the
        // return route says the stance is worth exactly one attack, so there is
        // no squatting to author and no exit to schedule. What makes it a
        // decision is that the entry windup is paid before the fan exists —
        // so it is taken only when several bodies are already inside a fan this
        // muzzle can lay without walking, and never as a way to reach one
        // target a straight bolt already reaches.
        GenericActorDecision? cast = TryCastVolley(lens, context, targets);
        if (cast is not null)
            return cast;

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

        // G8 — TAKE THE PILE UNDER THE BOOT, NEVER THE WALK ACROSS THE MAP.
        //
        // The assay pays in full at the tile with no transport, so a pile one
        // step away is a whole banked unit for a tick this body was going to
        // spend walking anyway. What this doctrine refuses is the other half of
        // the economy: the deposits sit sixteen ticks from home in a lane the
        // front cannot see, and under a channelling capture a body-light front
        // is a lost front. So the detour budget is one step while my stillness
        // is worth anything, and only opens up for a body the surface does not
        // want — which the cap discipline above has already identified.
        GenericActorDecision? scrap = TryHarvest(lens, context, goals, locked);
        if (scrap is not null)
            return scrap;

        if (!stationed)
        {
            GenericActorDecision? step = TryStep(lens, context, goals);
            if (step is not null)
                return step;
        }
        _ = mode;
        return SafeAction(
            context,
            locked
                ? $"channelling: {_state.Claim} still against {_state.Theirs}"
                : holding
                    ? "holding the scoring surface"
                    : fortress
                        ? $"on overwatch: {_veto ?? "waiting"}"
                        : "screening");
    }

    /// <summary>
    /// Enter a stance whose gun launches more than one bolt per action, when the
    /// fan is worth the windup.
    ///
    /// <para>Owned by no chassis this doctrine plays, and written from the
    /// contract rather than from the table: the route is found by asking which
    /// same-life target form's attack profile declares more projectiles per
    /// attack than one, the fan's width comes from that declared count, and the
    /// cast's whole cost is the entry windup because the return spends itself.
    /// The gate is the only thing that makes a fan better than a bolt — MORE
    /// THAN ONE BODY inside the fan the muzzle already faces. A fan aimed at a
    /// single target is a slower version of a shot this body could already take,
    /// and the stance cannot curve, cannot move, and cannot be held.</para>
    ///
    /// <para><b>Unmeasured, and declared as such.</b> This lineage's chassis
    /// declares no such route on any contract it can run, so this method returns
    /// null on every tick of every match reported in DX.md. It is here because
    /// the contract, not the class table, decides what a body can do — but a
    /// reader nobody has exercised is a liability, so it is deliberately the
    /// smallest correct thing rather than a tuned one.</para>
    /// </summary>
    private static GenericActorDecision? TryCastVolley(
        ContractLens lens,
        GenericActorContext context,
        List<Gunnery.Target> targets)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.VolleyRoute(context.Self.FormId);
        if (route is null || targets.Count == 0)
            return null;
        if (!lens.PlacementAllows(route, context.Self.Position))
            return null;

        GenericActorRulesContract.AttackProfile? fan =
            lens.Attack(lens.Form(route.TargetFormId));
        if (fan is null)
            return null;
        int lanes = fan.ProjectilesPerAttack;
        if (lanes <= 1)
            return null;

        int half = (lanes - 1) / 2;
        int reach = fan.Projectile.MaxTravelTiles;
        bool strict = fan.Projectile.DiagonalCornersMustBeClear;
        int inFan = 0;
        foreach (Gunnery.Target target in targets)
        {
            if (!ArenaGeometry.TryRay(
                    context.Self.Position,
                    target.Tile,
                    out ProjectileHeading heading,
                    out int distance)
                || distance > reach
                || !ArenaGeometry.ClearRay(
                    lens.Map,
                    context.Self.Position,
                    target.Tile,
                    strict))
            {
                continue;
            }
            if (Math.Abs(
                    ArenaGeometry.SignedOctants(
                        context.Self.Facing.ToProjectileHeading(),
                        heading))
                > half)
            {
                continue;
            }

            // Straight by construction, so a guarded arc turns a fan bolt
            // exactly as it turns an ordinary one.
            if (target.Guarded
                && ArenaGeometry.GuardCatches(target.Facing, heading)
                && !target.FeedGuard)
            {
                continue;
            }
            inFan++;
        }
        return inFan > 1
            ? BuildTransition(
                context,
                route,
                $"casting: {inFan} bodies inside the fan")
            : null;
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
        Position[] active,
        bool anyCoveringTile = false)
    {
        GenericActorRulesContract.FormTransition? route =
            lens.AnchorRoute(context.Self.FormId);
        if (route is null || active.Length == 0)
            return null;

        // The route's own placement legality, not the map's tag vocabulary. This
        // is what puts the point itself on the list of posts.
        if (!lens.PlacementAllows(route, context.Self.Position))
        {
            _veto = "this tile is not a legal anchor";
            return null;
        }

        // C1 — A ROOT IS A WALL, AND IT IS MY WALL TOO.
        //
        // The route says this placement is legal and the coverage ranking is
        // about to say the tile is good. Neither of them knows that on this map
        // the tiles that cover the surface best include 1-tile lanes, and that a
        // turret cannot step aside for the rest of its form. So the third
        // question is who else needs this tile — and the honest answer is
        // sometimes "nobody, and the opposition needs it badly", which is not a
        // veto but the best reason to root there that this doctrine has.
        if (!LaneStaysOpen(
                lens, context, context.Self.Position, active,
                out int laneMine, out int laneTheirs))
        {
            _veto = $"rooting here walls my own lane by {laneMine}";
            return null;
        }
        bool gate = IsGate(laneMine, laneTheirs);

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
        // The site floor is the ranking's top tier, because a fortress that sees
        // half the surface is scenery. It is relaxed to "covers anything" for the
        // one caller whose reason to commit is the crossfire on THIS tile rather
        // than the quality of it.
        // A GATE RELAXES THE SITE FLOOR, and this is the one relaxation in the
        // doctrine that is about geometry rather than urgency. The floor exists
        // because a fortress that sees half the surface is scenery — but a
        // fortress standing in the only lane the opposition can reach the
        // surface through is not scenery even if it sees nothing: the wall IS the
        // contribution, and denied entry is the same currency as covering fire.
        // It still has to cover something, so the floor drops to one rather than
        // to zero.
        int siteFloor = anyCoveringTile || gate ? 1 : _siteFloor;
        if (coverage <= 0 || coverage < siteFloor)
        {
            _veto = "not a covering tile";
            return null;
        }

        bool reversible = lens.Reversible(route);
        int cycleTicks = Math.Max(1, route.Windup.DurationTicks)
            + Math.Max(
                1,
                lens.ReverseRoute(route.TargetFormId)?.Windup.DurationTicks ?? 1);

        // THE PRICE OF A RENTAL, AND IT IS NOT THE WINDUP.
        //
        // Health maps by the route's declared ratio policy with a floor of one,
        // in both directions, with no entry heal. At full health that is exactly
        // lossless — a 4/4 child becomes a 7/7 turret and comes back 4/4, a 5/5
        // prime the same — and a cycle costs only the two windups. Below full it
        // pays the floor on EVERY round trip: a 3/4 child becomes a 5/7 turret
        // (absolutely tougher, which is why the anchor is still a good answer to
        // being shot) and comes back at 2/4. The bill is one health per lap,
        // charged forever, and a doctrine that cycles on a whim grinds itself
        // down to the floor. So a lossy lap has to be bought rather than taken:
        // it is worth it when the arithmetic says this body is about to lose that
        // health anyway, and not otherwise.
        int? lap = lens.RoundTripCost(route, context.Self.Health);
        if (reversible
            && lap is int cost
            && cost > 0
            && !Threatened(lens, context, context.Self.Position)
            && context.Tick - _lastDamageTick > cycleTicks)
        {
            _veto = $"a lap costs {cost} health and nothing is shooting";
            return null;
        }

        // ANTI-FLICKER. A reversible one-tick route is cheap enough to thrash,
        // and revision 4 already paid for learning that with a windup-one shell
        // that entered its stance 223 times in a match. A completed leg buys
        // silence for one full declared cycle.
        if (reversible && context.Tick - _cycledTick < cycleTicks)
        {
            _veto = "just cycled";
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
        //
        // WAVE 5: and this refusal is a SALE'S refusal. Rooting into a line that
        // is about to move wastes a one-use return; it wastes one windup out of
        // an unlimited supply. Where the cycle is reversible the body follows the
        // front instead of refusing to commit near it, so the veto narrows to the
        // completion that lands inside the windup itself — the case where the
        // transition finishes onto ground that has already stopped existing.
        int rotateGuard = reversible
            ? Math.Max(1, route.Windup.DurationTicks)
            : Math.Max(3, route.Windup.DurationTicks);
        if (mode is not null
            && completionWouldMove
            && lens.CaptureThreshold > 0
            && mode.CaptureProgress >= lens.CaptureThreshold - rotateGuard)
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
        bool onPoint = Contains(active, context.Self.Position);
        GenericActorDecision? rooting = BuildTransition(
            context,
            route,
            gate
                ? $"gating the lane: costs them {laneTheirs}, costs us 0, "
                    + $"{coverage} covered"
                : reversible
                    ? onPoint
                        ? $"renting the gun on the point: {coverage} tiles covered"
                        : $"renting the gun: {coverage} covered objective tiles"
                    : $"rooting: {coverage} covered objective tiles");
        if (rooting is not null)
            _cycledTick = context.Tick;
        return rooting;
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

            // The envelope, defensively. A muzzle standing on a lane is "already
            // aimed" whenever the arrival heading is anywhere inside its own
            // declared launch envelope — which on an offset arm is its facing and
            // both diagonals, not just its facing. Revision 4 tested equality and
            // therefore priced a windup as one tick safer than it was against
            // every diagonal muzzle on the board.
            bool aimed =
                ArenaGeometry.TryRay(
                    enemy.Position,
                    context.Self.Position,
                    out ProjectileHeading heading,
                    out _)
                && Gunnery.BearsOn(attack, enemy.Facing, heading);
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
    /// <para>Three conditions where the root is a SALE, all read from the
    /// contract: relief already in place on the scoring surface rather than merely
    /// alive or merely due; a front that will still be this front for the whole
    /// tenure; and a clock with room to serve it. A contract with no companion
    /// slots never passes the first, which is the correct answer — a lone body
    /// that fortifies has conceded the match.</para>
    ///
    /// <para><b>WAVE 5 TRIED TO RELAX ALL OF THAT AND THE BOARD SAID NO.</b> The
    /// reasoning was clean: every clause above exists because the route was
    /// <c>irreversibleForLife</c>, the body was spending its objective weight for
    /// the rest of its life, and under a rental the same commitment costs two
    /// declared windups instead — so demanding a guaranteed capture window of
    /// relief is collateral on a loan nobody is taking out. Replacing the whole
    /// gate with the one question a rental can actually lose on (<i>am I the
    /// margin?</i>) was measured against this lineage's own rebuilt predecessor
    /// over ten cells and cost <b>47 points of territory per cell</b>: the
    /// conservative gate scored +60.0 and the relaxed one +12.6.</para>
    ///
    /// <para>So the gate is unchanged, and the correction is worth more than the
    /// rule was. <b>Reversibility does not make a root cheaper to TAKE. It makes
    /// one cheaper to LEAVE</b> — and leaving is where a one-use return was
    /// hoarded rather than spent. The whole value of the cycle is in
    /// <see cref="TryMobilize"/>: weight on demand, an immediate follow when the
    /// front rotates, and a root that can be given up the tick it stops paying
    /// because another one is always available. Entry keeps the bar that measured
    /// well; exit gets the freedom the declaration actually granted. The margin
    /// test survives as extra insurance on top, because a turret must never be the
    /// weight that is holding the ground.</para>
    /// </summary>
    private bool TenureAvailable(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ModeObservationState.Frontline? mode,
        HashSet<Position> objective)
    {
        int windup = AnchorWindup(lens, context);
        GenericActorRulesContract.FormTransition? anchor =
            lens.AnchorRoute(context.Self.FormId);
        bool reversible = lens.Reversible(anchor);
        ChannelRules? rules = _channel;

        if (lens.MaxTicks - context.Tick < windup + lens.CaptureWindowTicks)
        {
            _veto = "no time left to serve a tenure";
            return false;
        }

        // G2 — THE TURRET IS THE DENIAL ENGINE, AND THE CHANNEL IS WHAT PAYS IT.
        //
        // This is the branch the wave-8 arm creates and it is the doctrine's
        // headline, so it is stated as arithmetic rather than as a preference.
        //
        // Under a channelling policy my body standing on the surface is worth
        // exactly two things: it denies (subtracting its weight from THEIR
        // multiplier, whether it moves or not) and, if it holds still while my
        // team can build, it claims. A turret gives up both — objective weight
        // zero — and buys instead a gun that fires on the shortest declared
        // cadence on the board at a target on the surface, where every point of
        // health it removes from the CONTROLLING team reverts that team's whole
        // run by the declared amount.
        //
        // So the trade is priceable, and both operands are declared:
        //   cost = how much faster they build once my weight leaves the surface
        //   pay  = revertPerDamagePoint x damagePerHit per cooldownTicks
        // Root when the gun pays for the weight. It usually only does when I
        // could not have out-claimed them anyway, which is exactly the position
        // this class was losing from: outnumbered on the point, contributing one
        // denial, watching a claim I cannot stop climb at capped speed.
        //
        // It is NOT a suspension of the turret bargain. The bargain is the reason
        // the comparison exists: a turret scores nothing, so it has to buy back
        // more than the score it gave up, in the only currency the channel
        // trades in — progress.
        if (rules is not null && rules.Channels && Garrison.TurretGate)
        {
            int mineNow = _state.Mine;
            int mineAfter = mineNow - _state.SelfWeight;
            int theirRateNow = rules.RateFor(_state.TheirClaim - mineNow);
            int theirRateAfter = rules.RateFor(_state.TheirClaim - mineAfter);
            int cost = theirRateAfter - theirRateNow;

            int myRateNow = rules.RateFor(_state.Claim - _state.Theirs);
            int pay = TurretRevertRate(
                lens,
                context.Self.FormId,
                out int perTicks);

            // Never trade a claim I am actually building for a gun: the ground
            // is the score and the gun is only ever a way to protect it.
            if (myRateNow > 0 && _state.SelfStill && _state.SelfWeight > 0)
            {
                _veto = $"I am {myRateNow} of the claim: weight beats the gun";
                return false;
            }
            if (ErosionWaiting(mode, lens) && _state.SelfWeight > 0)
            {
                _veto = "their claim is eroding under me: weight beats the gun";
                return false;
            }

            // A gun with no line onto the ground denies nothing at all, and the
            // tile is asked rather than assumed — the same coverage question the
            // fortress site ranking already answers.
            int here = FortressPlan.Coverage(
                lens.Map,
                context.Self.Position,
                [.. objective],
                lens.Reach(lens.AnchorRoute(context.Self.FormId)?.TargetFormId),
                strictCorners: true);
            if (here <= 0)
            {
                _veto = "no line onto the surface from this tile";
                return false;
            }

            // THE REPAIR, AND IT IS THE DIFFERENCE BETWEEN A RATE AND A HOPE.
            //
            // The first version of this gate compared a weight I would certainly
            // lose against a revert rate the gun would only earn if it had a
            // target, a line, a ready cooldown and a bolt that was not turned by
            // an arc. `pay` was an upper bound treated as a certainty, and the
            // replays priced the difference exactly: over sixteen mirror cells
            // the rooted guns spent four hundred and eighty-seven ticks anchored
            // and landed FIVE points of denial between them. So the gate now
            // demands the target rather than the geometry — a body of the
            // claiming team standing on the surface, visible, and on a clear ray
            // from this tile inside the rooted gun's own declared reach — and
            // relief that keeps some weight on the ground while I am a gun.
            bool live = false;
            int turretReach =
                lens.Reach(lens.AnchorRoute(context.Self.FormId)?.TargetFormId);
            foreach (GenericActorContext.ObservedEnemyState enemy
                in context.Enemies)
            {
                if (!objective.Contains(enemy.Position))
                    continue;
                if ((lens.Form(enemy.FormId)?.ObjectiveWeight ?? 0) <= 0)
                    continue;
                if (context.Self.Position.ChebyshevDistance(enemy.Position)
                    > turretReach)
                {
                    continue;
                }
                if (!ArenaGeometry.ClearRay(
                        lens.Map,
                        context.Self.Position,
                        enemy.Position,
                        strictCorners: true))
                {
                    continue;
                }
                live = true;
                break;
            }
            if (!live)
            {
                _veto = "no body on the surface for the gun to revert";
                return false;
            }
            if (_state.Mine - _state.SelfWeight <= 0)
            {
                _veto = "rooting here empties the surface";
                return false;
            }

            if (pay > 0 && cost * perTicks <= pay)
            {
                _veto = null;
                _denials++;
                return true;
            }
            _veto =
                $"the gun does not pay the weight: costs {cost}, pays {pay}/{perTicks}";
            return false;
        }

        // AM I THE MARGIN? Insurance a reversible root can afford to carry: a
        // turret has objective weight zero, so if the count on the tile without
        // this body does not still hold the ground, whatever the gun buys, the
        // ground is what pays. It is stated for both declared control policies at
        // once — under binary control an enemy left alone starts gaining, under a
        // net-weight policy the difference goes the wrong way — and it is checked
        // only where the route reverses, because a one-way root is already gated
        // on relief standing on the surface below.
        if (reversible)
        {
            (int own, int enemy, bool self) =
                ArenaBasics.ObjectivePresence(lens.Contract, context);
            int without = own - (self ? SelfWeight(lens, context) : 0);
            if (enemy > 0 && without <= enemy)
            {
                _veto = $"I am the margin: {without} vs {enemy} without me";
                return false;
            }
        }

        // THE VETO IS THE CONTROL POLICY'S, NOT THE HOLD'S — and revision 3
        // measured that the hard way. Inside our own hold their captures are spent
        // and ours advance, which reads like "presence is doubled, never convert to
        // zero weight here"; ablated on the plain-ratchet cells that reading cost
        // 21 points of territory, and the cells it was gaining on were the contest
        // ones. Under binary control one body of positive weight nulls any number,
        // so the second body adds no capture rate and the fortress is free
        // suppression over ground that cannot be lost. Under a net-weight policy
        // every body is pressure and removing one subtracts from a number briefly
        // worth twice as much.
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
        List<Position> stations,
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

        if (stations.Count == 0)
            return objective;

        // The post my own rank names — derived by the identical function every
        // sibling uses to derive mine, which is the whole of how three bodies
        // with no shared memory end up on three different tiles.
        int rank = RankOf(lens, context, context.Self.ActorId, fortressActor);
        _ = active;
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
    /// <summary>
    /// <para><b>Wave 6 adds two coordination terms to a ranking that was
    /// otherwise right.</b> The rally tile is REMOVED from the list rather than
    /// ranked down, because a post that displaces my own reinforcement is not a
    /// worse post, it is a post nobody should hold. And a tile that stands under
    /// a muzzle already bearing on an allied body ranks below an otherwise equal
    /// tile that does not — a tie-break and nothing stronger, because presence
    /// outranks spacing and this lineage has the measurement to prove it.</para>
    /// </summary>
    private List<Position> Stations(
        ContractLens lens,
        GenericActorContext context,
        Position[] active,
        int activeIndex,
        Position? reserved,
        GenericActorContext.ModeObservationState.Frontline? mode)
    {
        HashSet<Position> hot = FortressPlan.HotTiles(lens, context);
        HashSet<Position> covered = CoveredSurface(lens, context, active);
        var surface = new HashSet<Position>(active);

        // G4 — HOW MUCH SURFACE IS WORTH WANTING, this tick, under the cap.
        int wanted = SurfaceWanted(lens, mode, active.Length);

        // TRIED AND REVERTED, WITH THE MEASUREMENT. Ranking posts by where
        // reinforcements actually arrive instead of by the home anchor cost 52
        // points of territory across ten ratchet cells. The home anchor is not
        // really "home" — it is the rearmost point of our own approach — so
        // ranking by it puts bodies on the side of the ground the opponent has to
        // walk past. Where arrivals land is a fact about the future; which side of
        // the ground you stand on is a fact about this tick.
        Position? rally = lens.HomeAnchor;
        _ = activeIndex;

        var ranked = new List<(
            Position Tile,
            int Onto,
            int Shield,
            int Cover,
            int Cool,
            int Fan,
            int Home)>();
        void Offer(Position tile, int onto)
        {
            if (!ArenaGeometry.IsOpen(lens.Map, tile)
                || lens.SpawnProtected.Contains(tile))
            {
                return;
            }
            // C4: never offer the tile my own next arrival is about to take.
            if (reserved is Position keep && keep == tile)
                return;
            ranked.Add((
                tile,
                onto,
                // G3: among tiles OFF the surface, one that stands between a
                // live muzzle and a body that is paying into our claim outranks
                // one that does not. It is a rank term rather than a separate
                // post list because a screen is still a post: the same body has
                // to be somewhere, and this only says where.
                onto == 0 || !ScreensAChanneler(lens, context, tile, surface)
                    ? 1
                    : 0,
                covered.Contains(tile) ? 0 : 1,
                hot.Contains(tile) ? 1 : 0,
                SharedEnvelope(lens, context, tile) ? 1 : 0,
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
            int shield = left.Shield.CompareTo(right.Shield);
            if (shield != 0)
                return shield;
            int cover = left.Cover.CompareTo(right.Cover);
            if (cover != 0)
                return cover;
            int cool = left.Cool.CompareTo(right.Cool);
            if (cool != 0)
                return cool;
            // C5: between two otherwise equal posts, take the one that is not
            // already under a muzzle covering one of my other bodies.
            int fan = left.Fan.CompareTo(right.Fan);
            if (fan != 0)
                return fan;
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
        //
        // G4 CUTS THE LIST HERE, and only in the state where the cap binds.
        // While my team is building, surface tiles past `wanted` buy no speed and
        // add one more body a leaked bolt can revert the whole run through, so
        // they are removed from the ranking entirely and the bodies that would
        // have taken them fall through to ring posts — which is where the screen
        // rank puts them on the firing line. While the opposition is building, or
        // while nobody is, every body on the surface subtracts from THEIR
        // multiplier and the list is not cut at all.
        var posts = new List<Position>();
        int onSurface = 0;
        foreach ((Position tile, int onto, int _, int _, int _, int _, int _)
            in ranked)
        {
            if (onto == 0)
            {
                if (onSurface >= wanted)
                    continue;
                onSurface++;
            }
            posts.Add(tile);
        }
        _ = activeIndex;
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

    // WAVE 6 DELETED `ScreenRank`, and the deletion is the coordination rule.
    //
    // It answered "what is MY index among the screening bodies", which is exactly
    // enough to hand out distinct posts and not nearly enough to yield to
    // anybody: a body that knows its own rank and nobody else's cannot tell who
    // has right of way. <see cref="RankOf"/> is the same arithmetic asked about an
    // arbitrary body, so every life can order the whole team the same way and the
    // precedence rule has something to be a rule ABOUT. Same numbers for self,
    // one extra argument, and a coordination layer becomes expressible.

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
    /// <summary>
    /// Steps onto a live pile when the detour is cheap enough that the front
    /// does not notice.
    ///
    /// <para>The budget is the rule. One step is free for a body that is walking
    /// somewhere anyway and worth a whole banked unit; anything longer is a body
    /// leaving the front, and the arm's own arithmetic says a body-light front
    /// loses ground faster than a bank buys tiers. So the wider budget is spent
    /// only by a body the surface does not want — a body whose stillness is
    /// already worth nothing under the declared cap.</para>
    /// </summary>
    private GenericActorDecision? TryHarvest(
        ContractLens lens,
        GenericActorContext context,
        HashSet<Position> goals,
        bool locked)
    {
        Salvage? salvage = _salvage;
        if (salvage is null || !salvage.Declared || !Garrison.Salvage || locked)
            return null;
        if (context.Mode
            is not GenericActorContext.ModeObservationState.Frontline mode
            || mode.ScrapPiles.Length == 0)
        {
            return null;
        }

        HashSet<Position> blocked = OccupiedTiles(lens, context);
        Dictionary<Position, int> reach = ArenaGeometry.Distances(
            lens.Map,
            context.Self.Position,
            blocked);

        // A body standing on its own post with nothing to walk toward may spend
        // a few ticks; a body still travelling may only take what is on its way.
        bool spare = goals.Count == 0 || goals.Contains(context.Self.Position);
        int budget = spare ? Math.Max(1, salvage.CarryCapacity / 2) : 1;
        Position? pile = salvage.NearestPile(context, reach, budget);
        if (pile is null)
            return null;

        GenericActorDecision? step = TryStep(lens, context, [pile.Value]);
        if (step is null)
            return null;
        return new GenericActorDecision(
            step.ActionId,
            step.ActionCode,
            step.Arguments,
            $"banking the assay at ({pile.Value.X},{pile.Value.Y})");
    }

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

        // C2/C3 — YIELD BEFORE THE COLLISION, NOT AFTER IT.
        //
        // Revision 5 discovered a sibling was in the way by walking into it: the
        // engine refused the move, the reactive blacklist noted the tile, and the
        // tick was gone. Worse, the declared collisions do not pick a winner —
        // two of my bodies choosing one tile BOTH block — so the classic failure
        // was two bodies losing the same tick to each other, repeatedly, which is
        // exactly what the replays show at (4,9).
        //
        // Adding the senior's claims to the obstacle set fixes it in the good
        // way rather than the obedient way: the route search runs again over a
        // board where those tiles are walls, so if an equal-length way round
        // exists this body TAKES it and loses nothing at all.
        //
        // WHAT TO DO WHEN THERE IS NO WAY ROUND IS THE WHOLE DESIGN, AND THE
        // FIRST VERSION GOT IT WRONG BY TEN POINTS OF TERRITORY A CELL.
        //
        // That version waited. It is the obedient answer and it reads as the
        // safe one — never contest a senior's tile — and measured against the
        // predecessor it removed twenty-eight sibling collisions, removed a
        // hundred and ninety corridor-wall ticks, and LOST, because it also
        // bought four hundred and forty extra waits. A wait is a whole tick of a
        // body doing nothing; a collision is a whole tick of two bodies doing
        // nothing. Yielding is only the better trade when it actually prevents a
        // collision, and out in the open it usually does not: a senior with two
        // equal-length first steps was never certain to take mine, so I paid a
        // certain tick to avoid a coin flip.
        //
        // So the rule is now asymmetric, and the asymmetry is the map's own:
        // <b>route around a claim wherever routing around exists; wait only
        // where the geometry forbids it, which is exactly inside a 1-tile
        // corridor.</b> In a corridor there is no way round by construction, the
        // senior cannot step aside either, and two bodies meeting inside one is
        // the only collision on this board that cannot resolve itself. That is
        // also why the corridor rule is the one that gets to spend a tick.
        var yielded = new HashSet<Position>(blocked);
        foreach (Position claim in _claims)
            yielded.Add(claim);
        Direction? step = ArenaGeometry.FirstStep(
            lens.Map,
            context.Self.Position,
            goals,
            yielded,
            _order);
        if (step is null)
        {
            step = ArenaGeometry.FirstStep(
                lens.Map,
                context.Self.Position,
                goals,
                blocked,
                _order);
            if (step is Direction forced
                && Coordination.ChokePrecedence
                && _traffic is Traffic traffic)
            {
                Position into = ArenaGeometry.Step(context.Self.Position, forced);
                if (traffic.IsChoke(into) && _claims.Contains(into))
                {
                    // C3 — THE EXPLICIT CHOKE PRECEDENCE RULE, and the one place
                    // in the doctrine that spends a tick on politeness. A senior
                    // is committed to this run and there is no second lane. I
                    // wait at the mouth; next tick it is through and the claim is
                    // somewhere else.
                    _yields++;
                    return null;
                }
            }
        }
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

        // C3 ONLY, AND DELIBERATELY NOT C2. Evasion is a step like any other and
        // a step into a sibling's lane is the same lost tick — but a dodge is
        // paid for in HEALTH rather than tempo, and a collision is much cheaper
        // than a bolt. So the dodge respects the corridor reservation, where two
        // bodies genuinely cannot pass, and ignores ordinary claims, where the
        // worst case is one shared tick and the alternative is a hit.
        if (_traffic is Traffic traffic)
        {
            foreach (Position claim in _claims)
            {
                if (traffic.IsChoke(claim))
                    blocked.Add(claim);
            }
        }
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

        TallyDeflections(context);
    }

    /// <summary>
    /// Count each visible guard's spent deflections from the published exchange
    /// events, and drop the tally the moment its body is seen outside a guarding
    /// form. The counter is declared to restart on entry and never survive the
    /// form, so a tally that outlived the stance would be a number about a
    /// shield that no longer exists — and the whole value of the tally is
    /// knowing which bolt is the last one.
    /// </summary>
    private void TallyDeflections(GenericActorContext context)
    {
        ContractLens? lens = _lens;
        if (lens is null)
            return;

        var guardsNow = new HashSet<string>(StringComparer.Ordinal);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            string key = enemy.ActorId.ToString();
            if (lens.IsGuarded(enemy.FormId))
                guardsNow.Add(key);
            else
                _deflected.Remove(key);
        }
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (!lens.IsGuarded(ally.FormId))
                _deflected.Remove(ally.ActorId.ToString());
        }

        // A guard that has gone out of sight keeps its tally: the stance is
        // still running as far as anything observable says, and forgetting it
        // would restart the count at zero next time it is seen.
        _guardsLastSeen.Clear();
        foreach (string key in guardsNow)
            _guardsLastSeen.Add(key);

        foreach (GenericActorContext.ObservedEvent visible in context.VisibleEvents)
        {
            switch (visible.Kind, visible.Payload)
            {
                case (GenericActorContext.EventKind.ProjectileDeflected,
                    GenericActorContext.EventPayload.ProjectileDeflected
                        deflection):
                    string guard = deflection.TargetActorId.ToString();
                    _deflected.TryGetValue(guard, out int count);
                    _deflected[guard] = count + 1;
                    if (deflection.TargetActorId == context.Self.ActorId)
                        _lastTurnTick = visible.SourceTick;
                    break;
                case (GenericActorContext.EventKind.Attack,
                    GenericActorContext.EventPayload.Attack attack):
                    if (attack.ActorId.TeamId != lens.TeamId)
                        _lastAttack[attack.ActorId.ToString()] = visible.SourceTick;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// The slowest declared fire cadence among visible enemies, falling back to
    /// this body's own. It is the honest unit of "how long before that gun comes
    /// round again", and it is a declared number rather than a chosen one.
    /// </summary>
    private static int SlowestVisibleCadence(
        ContractLens lens,
        GenericActorContext context)
    {
        int slowest = 0;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? attack =
                lens.Attack(lens.Form(enemy.FormId));
            if (attack is not null)
                slowest = Math.Max(slowest, Math.Max(1, attack.CooldownTicks));
        }
        return slowest > 0
            ? slowest
            : Math.Max(1, lens.FireIdleTicks(context.Self.FormId));
    }

    /// <summary>
    /// Ticks before this enemy's muzzle could put a bolt on <paramref name="tile"/>,
    /// and the heading that bolt would arrive on.
    ///
    /// <para>Three declared quantities and one observed one. The reach, the
    /// launch geometry, the travel cadence and the cooldown come from the
    /// enemy's own form and attack profile; the tick it last fired comes from
    /// the published attack event. A muzzle that has never been seen firing is
    /// assumed ready, which is the safe direction to be wrong in — and a muzzle
    /// that owes a rotation before it can fire pays for that in the same
    /// currency, priced by its declared movement coupling.</para>
    ///
    /// <para>Returns null when this enemy cannot reach the tile at all.</para>
    /// </summary>
    private (int LandsIn, ProjectileHeading Arrival, int Damage)? MuzzleClock(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState enemy,
        Position tile)
    {
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(lens.Form(enemy.FormId));
        if (attack is null)
            return null;
        if (!ArenaGeometry.TryRay(
                enemy.Position,
                tile,
                out ProjectileHeading arrival,
                out int distance)
            || distance > attack.Projectile.MaxTravelTiles
            || !ArenaGeometry.ClearRay(
                lens.Map,
                enemy.Position,
                tile,
                attack.Projectile.DiagonalCornersMustBeClear))
        {
            return null;
        }

        // THE ONE-LINE FIX FOR REVISION 4'S OWN SUSPECTED VARIANCE. Its DX report
        // named this exact expression: "the muzzle clock models only the straight
        // arrival heading, so the arc is sometimes raised against an angle that no
        // longer comes." An equality against facing says a diagonal muzzle is a
        // tick away from being able to fire; the declared envelope says it can
        // fire NOW. Every shield decision, every windup price and every dodge
        // clock reads through here, so widening it by the contract's own number
        // corrects all of them at once.
        bool aimed = Gunnery.BearsOn(attack, enemy.Facing, arrival);
        int aimTicks = Kinematics.TicksToFirstShot(
            lens.Coupling(enemy.FormId),
            0,
            attack.OmnidirectionalAim,
            aimed);

        int cooldown = 0;
        if (_lastAttack.TryGetValue(enemy.ActorId.ToString(), out int fired))
        {
            cooldown = Math.Max(
                0,
                fired + Math.Max(1, attack.CooldownTicks) + 1 - context.Tick);
        }

        int perAdvance = Math.Max(1, attack.Projectile.TilesPerAdvance);
        int beyondLaunch = Math.Max(
            0,
            distance - Math.Max(0, attack.Projectile.LaunchTiles));
        int flight = (beyondLaunch + perAdvance - 1) / perAdvance
            * Math.Max(1, attack.Projectile.TicksPerAdvance);
        return (
            Math.Max(aimTicks, cooldown) + flight,
            arrival,
            Math.Max(1, attack.Projectile.DamagePerHit));
    }

    /// <summary>
    /// What a guard arc on <paramref name="tile"/> facing <paramref name="facing"/>
    /// would be worth, if it were live from <paramref name="liveInTicks"/> ticks
    /// from now.
    ///
    /// <para>The offset is the whole reason this is a method and not a loop over
    /// projectiles, and it is where revision 4's first draft was wrong. A
    /// transition retains the SOURCE form through combat and completes after it,
    /// so a shield requested this tick does not stop a bolt that lands this
    /// tick — and at this chassis's own duelling distance every bolt lands the
    /// tick after it is fired, which means a shield raised in reaction to a bolt
    /// in flight is always exactly one tick late. It has to be raised against
    /// the MUZZLE, not the bolt: an enemy whose cadence says it may fire now.
    /// The first draft raised it against bolts, spent thirty-four ticks a match
    /// inside the stance and deflected NOTHING.</para>
    /// </summary>
    private (int Turned, int Uncaught, int SoonestUncaught) ArcPressure(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        Direction facing,
        int liveInTicks)
    {
        int turned = 0;
        int uncaught = 0;
        int soonest = int.MaxValue;

        foreach (GenericActorContext.ObservedProjectile projectile
                 in context.VisibleProjectiles ?? [])
        {
            if (projectile.OwnerTeamId == lens.TeamId)
                continue;
            ArenaBasics.Incoming? threat =
                Arriving(lens, context, projectile, tile);
            if (threat is not ArenaBasics.Incoming incoming)
                continue;

            // A bolt whose reported arrival is one tick away lands during THIS
            // tick's resolution, so the offset from now is one less.
            int landsIn = incoming.TicksUntilArrival - 1;
            if (landsIn >= liveInTicks
                && ArenaGeometry.GuardCatches(facing, projectile.Heading))
            {
                turned += incoming.Damage;
                continue;
            }
            uncaught += incoming.Damage;
            soonest = Math.Min(soonest, landsIn);
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            var clock = MuzzleClock(lens, context, enemy, tile);
            if (clock is not (int landsIn, ProjectileHeading arrival, int damage))
                continue;

            // One tick of slack: a muzzle that becomes ready the tick after the
            // shield is live is still a muzzle the shield answers, and shaving
            // the window to zero is how a windup-one stance never gets used.
            if (landsIn > liveInTicks + 1)
                continue;
            if (landsIn >= liveInTicks && ArenaGeometry.GuardCatches(facing, arrival))
            {
                turned += damage;
                continue;
            }
            uncaught += damage;
            soonest = Math.Min(soonest, landsIn);
        }
        return (turned, uncaught, soonest);
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

            // G2b — THE CHANNEL TELLS FIRE CONTROL WHICH BODY IS THE SCORE.
            //
            // Under a claim interrupt scoped to the CONTROLLING team standing ON
            // the region, a bolt into that exact body is not merely damage: it
            // is the declared revert per point against a run somebody has been
            // building for eight ticks. A bolt into the identical body one tile
            // off the region reverts nothing. So the priority is the interrupt's
            // own scope read back as a target weight, and it is inert on a
            // ruleset that declares no interrupt.
            if (Garrison.DenialFire
                && _channel is ChannelRules rules
                && rules.Channels
                && rules.RevertPerDamagePoint > 0
                && onSurface
                && context.Mode
                    is GenericActorContext.ModeObservationState.Frontline live
                && live.ClaimingTeamId is int claimant
                && claimant != lens.TeamId
                && (!rules.InterruptOnObjectiveOnly || onSurface))
            {
                weight += 6;
            }

            // A zero-weight body has left every capture count, so it is not
            // the thing standing between this team and the ground. Read the
            // declared weight rather than immobility: a guard stance is
            // immobile and is still holding the tile, which makes it a HIGHER
            // priority than an ordinary body, not a lower one.
            if (lens.IsFortified(enemy.FormId))
                weight -= 3;
            if (enemy.PendingSameLifeTransition is not null)
                weight += 4;   // a visible windup is the cheapest kill available

            bool guarded = lens.IsGuarded(enemy.FormId);

            // C5, SECOND HALF — DO NOT FEED AN ARC WITH A SIBLING BEHIND ME.
            //
            // A deflection launches a new bolt from the shell's tile along the
            // EXACTLY REVERSED heading, owned by the shell's team. So a bolt I
            // feed into an arc comes back down the lane I fired it on: through my
            // tile, and then on through whoever of mine is standing behind me on
            // it. The doctrine feeds arcs on purpose — a turned bolt still spends
            // a third of the declared budget — so this is the standard play, and
            // the only thing that makes it safe is a clear lane behind the
            // muzzle. Refusing the FEED rather than the shot keeps the ordinary
            // flanking answer available: the arc never tracks, so going round it
            // always works and costs nobody a hit.
            bool feed = guarded
                && WorthBreaking(lens, context, enemy)
                && !SiblingOnReturnLane(context, enemy.Position);
            targets.Add(new Gunnery.Target(
                enemy.Position,
                Drift(enemy, context.Tick),
                enemy.Health,
                weight,
                guarded,
                enemy.Facing,
                feed));
        }
        return targets;
    }

    /// <summary>
    /// Is feeding this guard the plan? Yes, unless the bill kills the feeder.
    ///
    /// <para>Revision 4 shipped this backwards and a measurement turned it round:
    /// it fed only an arc already one bolt from breaking, which is a gate that can
    /// never open, because a deflection count only reaches its threshold because
    /// somebody fed it. Sparred against a variant of this artifact with the
    /// refusal removed it lost every cell of the candidate game by 31 points of
    /// territory. What the board actually pays, in order: a bolt that LANDS is
    /// best; a bolt that is TURNED still spends a third of the arc's declared
    /// budget and the third shatters it into a forced return; and only a bolt
    /// whose own return would kill the shooter is worth holding. The return is
    /// dodgeable and hostile, so the evasion already here covers the bill.</para>
    /// </summary>
    private static bool WorthBreaking(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ObservedEnemyState enemy)
    {
        GenericActorRulesContract.AutomaticReturnTrigger? budget =
            lens.ReturnBudget(enemy.FormId);
        if (budget is null || budget.Threshold < 1)
        {
            // A guard with no declared budget never breaks, so a turned bolt
            // buys nothing at all and the arc is simply a tile not to shoot.
            return false;
        }

        // The return carries the damage class of the bolt that was returned, so
        // the bill for a fed bolt is this gun's own declared damage.
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(lens.Form(context.Self.FormId));
        int bill = Math.Max(1, attack?.Projectile.DamagePerHit ?? 1);
        return context.Self.Health - bill >= 1;
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
    /// What one hostile bolt costs this tile and when — asked of the BOLT.
    ///
    /// <para>THE REPAIR THAT MATTERS MOST ONCE SPECIALS EXIST. Revision 3 knew
    /// neither a bolt's cadence nor its damage, so it substituted the fastest
    /// and heaviest values declared anywhere in the contract and was
    /// conservative on both. That is exactly wrong in a kit arm: a fan bolt, an
    /// ordinary bolt and a bolt that has been TURNED BACK by a guard need not
    /// agree on either number, and a returned bolt carries the damage class of
    /// the bolt that was returned rather than of the form that launched it. Both
    /// facts are per-projectile fields now, so the arithmetic is exact instead
    /// of pessimistic, and the scaffold's own helper does it.</para>
    ///
    /// <para>Occlusion is still ours: walls consume bolts, and a lane that is
    /// blocked between here and there is not a threat at all.</para>
    /// </summary>
    private static ArenaBasics.Incoming? Arriving(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ObservedProjectile projectile,
        Position tile)
    {
        _ = context;
        if (projectile.Position == tile)
            return new ArenaBasics.Incoming(0, projectile.DamagePerHit, 0);
        ArenaBasics.Incoming? threat = ArenaBasics.Threat(projectile, tile);
        if (threat is null)
            return null;
        return ArenaGeometry.ClearRay(lens.Map, projectile.Position, tile, true)
            ? threat
            : null;
    }

    /// <summary>
    /// Ticks until the soonest hostile projectile occupies this tile, or
    /// <see cref="int.MaxValue"/> when none ever does. Counting the clock
    /// instead of testing a fixed radius is what stops a body walking two tiles
    /// deeper into a walled lane because the bolt was not "close enough" yet.
    ///
    /// <para>"Hostile" is ownership, and a deflected bolt is hostile by exactly
    /// that test: a guard's return belongs to the guard's team, so a bolt this
    /// body fired into an enemy arc comes back as an ordinary enemy projectile
    /// and is dodged like one. Nothing here needed changing for that, which is
    /// the point of reading ownership rather than tracking who shot what.</para>
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
            ArenaBasics.Incoming? threat =
                Arriving(lens, context, projectile, tile);
            if (threat is ArenaBasics.Incoming incoming)
                soonest = Math.Min(soonest, incoming.TicksUntilArrival);
        }
        return soonest;
    }

    private static bool Threatened(
        ContractLens lens,
        GenericActorContext context,
        Position tile) =>
        TicksToImpact(lens, context, tile) <= 2;

    /// <summary>
    /// Damage that lands on a tile within the next few ticks, at each bolt's own
    /// declared cost. This is what a body decides to absorb rather than concede
    /// ground for, so it is counted from the bolts rather than guessed at from
    /// the worst profile in the catalog.
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
            ArenaBasics.Incoming? threat =
                Arriving(lens, context, projectile, tile);
            if (threat is ArenaBasics.Incoming incoming
                && incoming.TicksUntilArrival <= withinTicks)
            {
                hits += incoming.Damage;
            }
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
            lens.HomeAnchor,
            route);
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

        // ASK, DO NOT LEARN BY WALKING INTO IT. A tile can be individually legal
        // for a movement action and still never enterable — an own slot's
        // authored return anchor is permanently claimed against our own bodies,
        // which is the classic case and the one revision 3 discovered by
        // counting three refusals per tile and blacklisting it for fifty ticks.
        // The claim is a published field on the visible tile now, carrying the
        // team, the unit and (for a pending claim) the tick it is due, so the
        // permanent kind is a wall and a due one is a wall from now until it
        // resolves. The refusal counter is retained underneath because it also
        // catches things no reservation explains — a body that never moves, a
        // contested step — but it no longer has to discover the map's own rules.
        foreach (GenericActorContext.ObservedTile visible in context.VisibleTiles)
        {
            if (visible.SpawnReservation is not
                GenericActorContext.SpawnReservation claim)
            {
                continue;
            }
            if (claim.TeamId != lens.TeamId)
                continue;   // an enemy claim blocks enemy bodies, not ours

            // THE CLAIM NAMES A UNIT, AND THAT MATTERS MORE THAN IT LOOKS. A
            // reservation is held FOR a slot, so it blocks this team's OTHER
            // bodies and never the claimant. Reading it as "my team's claim is a
            // wall for me" makes a Prime treat its own authored return anchor as
            // a wall — and on the qualification suite's pressure-entry map that
            // anchor sits on the only approach lane, one tile ahead of the
            // spawn. The bot detoured, found two equal-length detours, and
            // oscillated north-south for five hundred ticks without ever
            // reaching the objective. It cost this revision its T4 on the first
            // attempt, and it is the exact hazard of replacing an inference with
            // a field: the inference could not make this mistake because it only
            // ever blacklisted tiles a body had actually been refused.
            if (claim.UnitId == context.Self.ActorId.UnitId)
                continue;
            bool permanent = claim.DueTick is null;
            if (permanent || claim.DueTick >= context.Tick)
                blocked.Add(visible.Position);
        }
        foreach (Position denied in _denied)
            blocked.Add(denied);
        return blocked;
    }

    // ---------------------------------------------------------- coordination

    /// <summary>
    /// C1 — <b>THE ASSIGNMENT.</b> Would freezing a body on this tile wall a
    /// 1-tile lane my OWN traffic needs, and if it walls one only the enemy
    /// needs, is that a gate worth taking?
    ///
    /// <para><b>What the owner watched.</b> A rooted turret and a raised shell
    /// are both permanent walls on their tile — actors block actors, and neither
    /// form can step aside — so they are walls for BOTH teams. On this map the
    /// mouths of the central objective are 1-tile corridors, they are the best
    /// tiles this chassis can shoot the surface from, and revision 5 parked a
    /// shell in one of them for up to ninety-seven ticks of a five-hundred-tick
    /// match while its own reinforcements walked a three-tile detour around it.
    /// Revision 5 did not know it had done anything: nothing was illegal, no
    /// action was refused, and the coverage ranking that chose the tile is
    /// correct about everything except who else was going to use it.</para>
    ///
    /// <para><b>The arithmetic is a subtraction, not a heuristic.</b> Ask the
    /// map what the wall costs each body that wants the objective: route length
    /// with the tile open, route length with it walled. Do it for my own mobile
    /// siblings and for the enemy's. Then the rule states itself:</para>
    /// <list type="bullet">
    /// <item>costs one of my own bodies two steps or more — <b>refuse</b>. Keep
    /// one clear lane for my own traffic; there is always another tile.</item>
    /// <item>costs mine nothing and the enemy two or more — <b>gate</b>. This is
    /// the best tile on the board and the brief's own second option: root
    /// deliberately, with my bodies on the right side of it.</item>
    /// <item>anything between — ordinary tile, ordinary rules.</item>
    /// </list>
    ///
    /// <para><b>Why two and not one.</b> A one-step detour is a tick, and a tick
    /// is cheaper than giving up the only tile that covers the surface. Two is
    /// where the cost stops being a rounding error: on this map's corridors the
    /// real detour is three, and a severed route is priced at
    /// <see cref="Traffic.Severed"/> so it can never be confused with one.</para>
    ///
    /// <para><b>It is also the EXIT test, which matters more than it looks.</b>
    /// Entry is not the only moment a lane can be needed — a sibling can be born
    /// behind me, or the front can rotate and put my lane on somebody's route.
    /// The turret cycle is reversible and the shell has its own return, so this
    /// same question is asked every tick from inside both forms, and a body that
    /// is now in the way leaves. A rule that only gated entry would have fixed
    /// the first thirty ticks of the ninety-seven.</para>
    /// </summary>
    private bool LaneStaysOpen(
        ContractLens lens,
        GenericActorContext context,
        Position tile,
        Position[] active,
        out int mine,
        out int theirs)
    {
        mine = 0;
        theirs = 0;
        Traffic? traffic = _traffic;
        if (!(Coordination.ChokeRefusal || Coordination.ChokeGate)
            || traffic is null
            || active.Length == 0
            || !traffic.IsChoke(tile))
        {
            return true;
        }

        // Every other body is an obstacle to everybody's route, which is what
        // makes this a question about the tile rather than about the map.
        HashSet<Position> bodies = OccupiedBodies(context);

        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsStatic(ally.FormId))
                continue;   // it is not going anywhere; it needs no lane
            var others = new HashSet<Position>(bodies);
            others.Remove(ally.Position);
            others.Remove(tile);
            mine = Math.Max(
                mine,
                traffic.WallCost(ally.Position, active, others, tile));
        }

        // A body that is DUE BACK also needs the lane, and the contract says
        // when and where. Ignoring it is how a gate closes behind a body that
        // has not been born yet.
        if (_rally is Position arrival && arrival != tile)
        {
            var others = new HashSet<Position>(bodies);
            others.Remove(arrival);
            others.Remove(tile);
            mine = Math.Max(
                mine,
                traffic.WallCost(arrival, active, others, tile));
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (lens.IsStatic(enemy.FormId))
                continue;
            var others = new HashSet<Position>(bodies);
            others.Remove(enemy.Position);
            others.Remove(tile);
            theirs = Math.Max(
                theirs,
                traffic.WallCost(enemy.Position, active, others, tile));
        }

        if (Coordination.ChokeRefusal && mine >= 2)
        {
            _laneRefusals++;
            return false;
        }
        if (IsGate(mine, theirs))
            _gates++;
        return true;
    }

    /// <summary>Is this tile a gate — a wall that costs only the opposition?</summary>
    private bool IsGate(int mine, int theirs) =>
        Coordination.ChokeGate && mine == 0 && theirs >= 2;

    /// <summary>
    /// C2 and C3 — the tiles a body with PRECEDENCE holds or could step onto
    /// this tick, and therefore the tiles this body must not take.
    ///
    /// <para><b>The written precedence rule, because the brief asks for one and
    /// because an unwritten one is not a rule.</b></para>
    /// <list type="number">
    /// <item><b>Screen rank decides.</b> Rank is the body's index among the
    /// team's screening bodies in canonical actor order — the same number
    /// <see cref="ScreenRank"/> already used to hand out distinct posts. Rank 0
    /// (the body assigned to the scoring surface) has right of way over rank 1,
    /// rank 1 over rank 2. Every life computes every body's rank from the same
    /// frozen observation, so the ordering is total, agreed, and needs no
    /// message.</item>
    /// <item><b>Inside a corridor outranks outside it.</b> A body already in a
    /// 1-tile run cannot step aside — there is no aside — so it keeps the run
    /// even against a senior. This is what stops two of my bodies meeting in the
    /// middle of a corridor and both waiting for the other forever.</item>
    /// <item><b>The whole run is reserved, not the entry cell.</b> Reserving one
    /// end lets the other end be entered, and then rule 2 has to arbitrate a
    /// collision that rule 3 could have prevented.</item>
    /// <item><b>Ties break by canonical actor identity</b>, which is the only
    /// total order every life shares.</item>
    /// </list>
    ///
    /// <para><b>Why the claim is a UNION and not a step.</b> The declared
    /// collisions are unforgiving in exactly the way that matters: same-destination
    /// moves all block, swaps block, and following a vacated actor blocks. Two of
    /// my bodies choosing the same tile do not resolve in someone's favour —
    /// they BOTH lose the tick. So the junior has to avoid every tile the senior
    /// might take, and it cannot know which one that is: the choice between
    /// equal-length routes is made with per-life state (revision 5 drew a random
    /// bool from the per-life stream for it). Claiming the union of every
    /// shortest first step is the only derivation that is identical in the
    /// senior's life and in mine.</para>
    ///
    /// <para><b>It terminates by construction, which is why there is no timer.</b>
    /// A claim beyond the senior's own tile exists only while the senior has a
    /// route it can actually walk. A stationed or stuck senior claims nothing but
    /// the tile it stands on — which was already blocked — so yielding always
    /// ends. A yield costs one tick and buys the senior a step; a collision costs
    /// two bodies a tick and buys nothing.</para>
    /// </summary>
    private void BuildClaims(
        ContractLens lens,
        GenericActorContext context,
        List<Position> stations,
        ActorIdentity? fortressActor)
    {
        _claims.Clear();
        Traffic? traffic = _traffic;
        if (!Coordination.RightOfWay || traffic is null)
            return;

        int myRank = RankOf(lens, context, context.Self.ActorId, fortressActor);
        IReadOnlyList<Position> myRun = Coordination.ChokePrecedence
            ? traffic.RunOf(context.Self.Position)
            : [];
        HashSet<Position> bodies = OccupiedBodies(context);

        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            if (lens.IsStatic(ally.FormId))
                continue;   // already a wall in OccupiedTiles; not traffic

            int rank = RankOf(lens, context, ally.ActorId, fortressActor);
            bool senior = rank < myRank
                || (rank == myRank
                    && ally.ActorId.CompareTo(context.Self.ActorId) < 0);
            if (!senior)
                continue;

            // Precedence rule 2: I am inside a run and this senior is not. It
            // has somewhere else to be; I have one way out.
            if (Coordination.ChokePrecedence
                && myRun.Count > 0
                && !Contains(myRun, ally.Position))
            {
                continue;
            }

            // The senior's own goal, derived exactly as the senior derives it.
            HashSet<Position> goals = GoalsOf(
                lens, context, ally, stations, fortressActor, rank);
            if (goals.Count == 0)
                continue;

            var others = new HashSet<Position>(bodies);
            others.Remove(ally.Position);
            others.Remove(context.Self.Position);

            // Its tile: following a vacated actor blocks, so a sibling's current
            // tile is not free even when the sibling is leaving it.
            _claims.Add(ally.Position);
            var steps = new HashSet<Position>();
            traffic.FirstSteps(ally.Position, goals, others, steps);

            // CLAIM ONLY A FORCED MOVE, AND THAT CORRECTION IS WORTH MORE THAN
            // THE RULE WAS. The first version claimed every tile a shortest first
            // step could use, which is airtight about collisions and measured a
            // TEN-POINT loss per cell: three bodies converging on a six-tile
            // objective claim six of the tiles worth standing on, so the route
            // search that was supposed to find a way round instead found a longer
            // way in, over and over, on every tick of the approach.
            //
            // The asymmetry the first version missed: a senior with TWO equal
            // first steps can avoid me by itself, and it does not need my tick to
            // do it. Only a senior with exactly ONE is certain to take that tile,
            // and only a certain collision is worth a certain tick. So the claim
            // is a forced move — which is also the case the replays actually
            // caught: two of my bodies at (4,8) and (4,10) both had (4,9) as
            // their unique first step and both lost the tick to each other,
            // twice, four ticks apart.
            if (steps.Count != 1)
                continue;

            foreach (Position step in steps)
            {
                _claims.Add(step);
                // Precedence rule 3: a forced step into a corridor reserves the
                // whole run, so I wait at the mouth instead of meeting it inside.
                if (Coordination.ChokePrecedence)
                {
                    foreach (Position cell in traffic.RunOf(step))
                    {
                        if (!Contains(myRun, cell))
                            _claims.Add(cell);
                    }
                }
            }
        }
        _claims.Remove(context.Self.Position);
    }

    /// <summary>
    /// Any body's index among the team's screening bodies, in canonical actor
    /// order. Generalises <see cref="ScreenRank"/> from "mine" to "anyone's",
    /// which is what makes a precedence order derivable rather than declared:
    /// every life asks this question about every body and gets the same answer,
    /// because the inputs are the frozen observation and the identity order.
    /// </summary>
    private static int RankOf(
        ContractLens lens,
        GenericActorContext context,
        ActorIdentity actor,
        ActorIdentity? fortressActor)
    {
        int rank = 0;
        void Consider(ActorIdentity candidate, string formId, bool pending)
        {
            if (candidate == actor
                || lens.IsStatic(formId)
                || pending
                || candidate == fortressActor)
            {
                return;
            }
            if (candidate.CompareTo(actor) < 0)
                rank++;
        }

        Consider(
            context.Self.ActorId,
            context.Self.FormId,
            context.Self.PendingSameLifeTransition is not null);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            Consider(
                ally.ActorId,
                ally.FormId,
                ally.PendingSameLifeTransition is not null);
        }
        return rank;
    }

    /// <summary>
    /// Where an allied body is heading, derived the way that body derives it:
    /// the fortress role walks to a fortress site, everybody else takes the post
    /// its own rank names. Any disagreement here is a coordination bug, so the
    /// station list and the ranking are computed once per tick and shared.
    /// </summary>
    private HashSet<Position> GoalsOf(
        ContractLens lens,
        GenericActorContext context,
        GenericActorContext.ObservedAllyState ally,
        List<Position> stations,
        ActorIdentity? fortressActor,
        int rank)
    {
        if (ally.ActorId == fortressActor && _sites.Count > 0)
            return [.. _sites];
        if (stations.Count == 0)
            return [];
        _ = lens;
        _ = context;
        return [stations[Math.Min(rank, stations.Count - 1)]];
    }

    /// <summary>
    /// C4 — the exact tile my team's next automatic arrival will take, when one
    /// is due soon enough to matter.
    ///
    /// <para><b>The one place a bulwark has placement influence over its own
    /// reinforcements, and it is not the obvious one.</b> This chassis does not
    /// fabricate at all — its companions are automatic, and on this arm the
    /// contract's fabrication transition list is EMPTY, so the brief's "do not
    /// fabricate into your own traffic" bar is structurally inert for me and I
    /// will not pretend otherwise. What is live is the other half. Under a
    /// forward rally the arrival takes <i>the rear-most FREE tile</i> of its
    /// region measured along my own advance direction — so which tile it takes
    /// is decided by which tiles are occupied, and the body occupying them is
    /// usually me. Stand on the rear-most tile and my own reinforcement appears
    /// one tile further forward: it is born deeper into the fight it was
    /// returning from, on a tile I had not chosen for it, possibly in a lane.
    /// </para>
    ///
    /// <para>Both halves are read, never assumed: the due tick comes from the
    /// slot's own pending state (<c>AvailabilityPending</c> for a first unlock,
    /// <c>AutomaticReturnPending</c> for a rebuild), and the region from the
    /// declared return placement. A contract that rallies home instead resolves
    /// this to the spawn anchor and the same rule keeps working.</para>
    /// </summary>
    private Position? DueArrivalTile(
        ContractLens lens,
        GenericActorContext context,
        int activeIndex,
        int within)
    {
        if (!Coordination.RallyClearance || activeIndex < 0)
            return null;

        int soonest = int.MaxValue;
        int unit = -1;
        foreach (GenericActorContext.ObservedUnitSlot slot in context.TeamUnits)
        {
            if (slot.TeamId != lens.TeamId)
                continue;
            int due = slot.State switch
            {
                GenericActorContext.UnitSlotState.AvailabilityPending a
                    => a.DueTick,
                GenericActorContext.UnitSlotState.AutomaticReturnPending r
                    => r.DueTick,
                _ => int.MaxValue,
            };
            if (due < soonest)
            {
                soonest = due;
                unit = slot.UnitId;
            }
        }
        if (unit < 0 || soonest - context.Tick > within)
            return null;

        Position[] region = lens.ArrivalTiles(unit, activeIndex);
        if (region.Length == 0)
            return null;

        // Everything except me: the question is which tile is free FOR the
        // arrival, and my own tile is the one I can still do something about.
        HashSet<Position> occupied = OccupiedBodies(context);
        occupied.Remove(context.Self.Position);
        return Traffic.RearMostFree(region, lens.AdvanceDelta, occupied);
    }

    /// <summary>
    /// C5 — does this tile stand under a muzzle that already bears on one of my
    /// other bodies, so that one launch envelope covers both?
    ///
    /// <para>The brief's fourth bar names a volley fan, and the fan is the loud
    /// case: three simultaneous bolts down the facing lane and both 45-degree
    /// neighbours, so two bodies on adjacent bearings are two hits from one
    /// action. But the general fact is wider and it is the one this arm actually
    /// has. Every mobile gun here declares a ±1 initial aim offset, so ANY
    /// muzzle covers three headings this tick without rotating —
    /// <see cref="Gunnery.LaunchWidth"/> is exactly that number, read from the
    /// enemy's own profile, and a volley's fan and a turret's omnidirectional
    /// aim fall out of the same reader. So the rule is not "avoid volleys", it is
    /// "do not hand one muzzle two targets it can choose between without
    /// turning", and it binds on every arm including the ones with no volley in
    /// them at all.</para>
    ///
    /// <para>It is a TIE-BREAK and deliberately nothing stronger. Spreading out
    /// is worth less than standing on the scoring surface — this lineage measured
    /// that two revisions ago, when interleaving holders with ring posts cost
    /// nine wins — so this only ever chooses between posts that are already
    /// equal on presence, cover and heat.</para>
    /// </summary>
    private bool SharedEnvelope(
        ContractLens lens,
        GenericActorContext context,
        Position tile)
    {
        if (!Coordination.Spacing)
            return false;
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.AttackProfile? attack =
                lens.Attack(lens.Form(enemy.FormId));
            if (attack is null)
                continue;
            if (!Covers(lens, attack, enemy, tile))
                continue;
            foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            {
                if (ally.Position != tile
                    && Covers(lens, attack, enemy, ally.Position))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Could that muzzle put a bolt on that tile this tick, with no rotation?
    /// Reach, corner strictness and the launch envelope all come from the
    /// enemy's own declared profile.
    /// </summary>
    private static bool Covers(
        ContractLens lens,
        GenericActorRulesContract.AttackProfile attack,
        GenericActorContext.ObservedEnemyState enemy,
        Position tile) =>
        ArenaGeometry.TryRay(
            enemy.Position,
            tile,
            out ProjectileHeading heading,
            out int distance)
        && distance <= attack.Projectile.MaxTravelTiles
        && Gunnery.BearsOn(attack, enemy.Facing, heading)
        && ArenaGeometry.ClearRay(
            lens.Map,
            enemy.Position,
            tile,
            attack.Projectile.DiagonalCornersMustBeClear);

    /// <summary>
    /// C5, second half — is one of my own bodies standing behind me on the ray
    /// to that tile?
    ///
    /// <para>This is the deflection-return case, and it is the one place in the
    /// whole doctrine where MY OWN shot is what hurts my sibling. A shell
    /// deflects a bolt arriving inside its arc by launching a new bolt from its
    /// own tile <i>along the exactly reversed heading</i>, owned by its team. So
    /// a bolt I feed into an arc comes straight back down the lane I fired it
    /// on — through me, and then through whoever is lined up behind me. The
    /// doctrine deliberately feeds arcs (a turned bolt still spends a third of
    /// the declared budget, and the third shatters the shield), so this is not a
    /// rare accident: it is the standard play, aimed down a lane, and the only
    /// thing that makes it safe is that nobody of mine is stacked on it.</para>
    /// </summary>
    private bool SiblingOnReturnLane(
        GenericActorContext context,
        Position target)
    {
        if (!Coordination.Spacing)
            return false;
        if (!ArenaGeometry.TryRay(
                context.Self.Position,
                target,
                out ProjectileHeading heading,
                out _))
        {
            return false;
        }
        ProjectileHeading back = ArenaGeometry.Reverse(heading);
        (int dx, int dy) = back.Vector();
        Position tile = context.Self.Position;
        for (int step = 0; step < 8; step++)
        {
            tile = tile.Offset(dx, dy);
            if (!ArenaGeometry.IsOpen(_lens!.Map, tile))
                return false;
            foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            {
                if (ally.Position == tile)
                    return true;
            }
        }
        return false;
    }

    /// <summary>Every body's tile, mine and theirs — the obstacle set every
    /// route question shares.</summary>
    private static HashSet<Position> OccupiedBodies(GenericActorContext context)
    {
        var tiles = new HashSet<Position> { context.Self.Position };
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            tiles.Add(ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            tiles.Add(enemy.Position);
        return tiles;
    }

    private static bool Contains(IReadOnlyList<Position> tiles, Position tile)
    {
        foreach (Position candidate in tiles)
        {
            if (candidate == tile)
                return true;
        }
        return false;
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
    /// <summary>
    /// The bounded legal action this body falls back to — and, when the tick was
    /// going to be spent on nothing anyway, the tick it spends on the store.
    ///
    /// <para>G7 LIVES HERE, AND THE PLACEMENT IS THE RULE. The store's verb
    /// costs the casting body its action for the tick, exactly like fabrication,
    /// so the cheapest possible caster is a body whose action was already going
    /// to be a wait — and this doctrine produces those in quantity by
    /// construction. A channeler holding still on the surface is waiting. A shell
    /// with nothing inbound is waiting. A rooted gun with no line is waiting.
    /// Every one of them is a full-price purchase at a price of zero, and none
    /// of them had to be scheduled: the doctrine's own idleness is the budget.
    /// </para>
    ///
    /// <para>Everything else about the purchase is the mask's. A track is
    /// offered only when the bank covers its next tier and no cap forbids it, so
    /// there is no arithmetic to get wrong and no <c>Blocked</c> to eat; two
    /// teammates casting against a bank that covers one resolve in canonical
    /// order and the loser simply waited, which is what it was doing anyway.
    /// <paramref name="idle"/> is false on the two paths where the tick was NOT
    /// free — a committed windup and a fault recovery.</para>
    /// </summary>
    private GenericActorDecision SafeAction(
        GenericActorContext context,
        string reason,
        bool idle = true)
    {
        if (idle && _lens is ContractLens lens && _salvage is Salvage salvage)
        {
            GenericActorDecision? buy = salvage.TryInvest(lens, context);
            if (buy is not null)
                return buy;
        }

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

/// <summary>
/// Whose completed advance, if any, is currently protected. Read from the mode
/// observation's published <c>holdOwnerTeamId</c>/<c>holdEndsAtTick</c> pair;
/// null there means no hold binds this tick, which is an answer.
/// </summary>
internal enum HoldPhase
{
    /// <summary>No hold is declared, or none is live.</summary>
    None,

    /// <summary>Our advance is protected: our progress counts, theirs is spent.</summary>
    Ours,

    /// <summary>Their advance is protected: their progress counts, ours is spent.</summary>
    Theirs,
}
