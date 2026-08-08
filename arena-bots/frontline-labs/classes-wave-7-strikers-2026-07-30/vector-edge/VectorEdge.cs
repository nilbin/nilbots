using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// VectorEdge — a pressure duelist.
///
/// The doctrine is one sentence: the objective is the only thing worth
/// standing on, so every tick either takes ground, holds ground, or removes
/// the body contesting it. Fire is chosen by what the target can still do — a
/// straight bolt to suppress a corridor where nothing can step aside, a
/// committed bend where an open chamber offers lateral escapes. Retreat is a
/// last resort: with a bolt inbound and the objective under this body,
/// VectorEdge would rather trade a hit than concede the tile.
///
/// Revision 2 changes one thing about that: the dodge model is measured
/// instead of assumed. A shot is still priced by where the target can be when
/// each path tile is occupied, but how willing that target is to leave its
/// tile is now counted rather than guessed — see <see cref="DodgeLedger"/>.
/// Against a body that steps off every bolt, the straight answer stops being
/// worth what it looks like, and the bend or the tile wins the tick instead.
///
/// The tick ledger underneath is unchanged in what it charges and repaired in
/// what it can price: aim can now be evaluated on a cooldown tick, which is
/// the tick on which turning is free, so the gun is laid there instead of
/// being paid for with a whole shot.
///
/// Revision 3 changes one more thing, and it is upstream of all of that: the
/// value of ground is read from the contract's capture policy rather than
/// inherited from the rule card. <see cref="Advance"/> answers, every tick,
/// whether this body's presence earns progress at all, whether a second
/// allied body beside it earns more, and whether the capture it is building
/// would actually move the front or be discarded inside an opposing advance
/// hold. Three habits fall out of that reading: a spare gun joins the
/// objective instead of covering it when surplus weight scales pressure; a
/// completion that the hold would spend is withheld one tile away rather than
/// thrown; and a body never walks home for a companion the contract is about
/// to deliver at the front. On a contract that declares none of those things,
/// the reading returns exactly what revision 2 assumed and nothing changes.
///
/// Revision 4 answers the question the class-skill kit asks — <em>what is a
/// special worth?</em> — and it answers it the same way this lineage answers
/// every other question, by pricing it against the tick it costs.
///
/// <list type="bullet">
/// <item><b>What the target can reach is geometry now, not a prior.</b> The
/// dodge model used to explore a ball of tiles and lean on a stickiness
/// multiplier to compensate for arms where that ball is a lie. Under
/// <c>facing-locked</c> it is a lie: the movement mask offers the current
/// facing and nothing else, so a body runs down the line it is on and pays a
/// rotation tick for anything else. <see cref="ShotSolver"/> now searches pose
/// rather than position — a tile and a facing, a tick that is either a turn or
/// a step along it — so the reachable set is the contract's own legality. This
/// is the revision's largest effect: it re-prices every straight bolt and
/// every bend on the phase-2 board.</item>
/// <item><b>A guard's arc is fixed, so go around it.</b> A form declaring
/// <c>projectileGuard</c> deflects contacts arriving inside its facing quadrant
/// and cannot rotate while raised. A bolt into that arc buys no damage and
/// launches one back under the other team's ownership, so the solver scores the
/// contact as dead and taxes the return — which makes a bend that curls to a
/// flank the best answer on the board, and makes standing inside the arc a cost
/// the router pays attention to.</item>
/// <item><b>A fan is bearings, and bearings are expensive.</b> The stance is
/// priced from the contract in <see cref="Cast"/>: an entry windup of wait-only
/// ticks, exactly one launch, and a stance gun whose cooldown is more than
/// double the mobile gun's. Measured against a rebuilt revision 3, casting it
/// on this board <em>loses</em> — so this revision declines almost every cast
/// and says so in its notes rather than shipping a special because it exists.
/// The route is still read, still priced, and still taken where the ticks were
/// free.</item>
/// <item><b>Envelopment over concentration.</b> Both specials in the kit punish
/// a shared bearing — a fan sweeps three rays from one gun, a shield answers one
/// bearing and nothing else — so destinations and tie-broken steps prefer a
/// bearing no ally already holds, and a body steps out of a fan that can launch
/// this tick, because movement resolves before combat.</item>
/// <item><b>The hold is asked, not derived.</b> <c>holdOwnerTeamId</c> and
/// <c>holdEndsAtTick</c> are published; revision 3's reconstruction survives
/// only for a contract that declares a hold and publishes no live one.</item>
/// </list>
///
/// Revision 5 answers the one new fact that changes a striker's arithmetic: the
/// mobile gun launches at -1/0/+1 sectors off facing. An aim-only diagonal is a
/// legal program with zero bends, so two shots that did not exist now do — the
/// diagonally adjacent kill, and the bolt that covers the tile a target steps
/// ONTO when it slips a straight one. The pose-space is re-derived around that.
///
/// <list type="bullet">
/// <item><b>The aperture, not the barrel.</b> <see cref="Arms"/> reads
/// <c>shotProgram.minInitialAimSteps</c>/<c>maxInitialAimSteps</c> and answers
/// which absolute headings a facing may launch along. One consequence is worth a
/// whole doctrine: a CARDINAL bearing is armed from exactly one facing, and a
/// DIAGONAL bearing from two, because it is the shared boundary of two
/// apertures. Under <c>facing-locked</c> a rotation is how a body travels, so a
/// contact on a diagonal is the only pose where turning onto a route does not
/// cost the shot — and it is the pose an omnidirectional chassis gains nothing
/// from, because it never paid an aperture. Steps and objective seats are
/// credited for it.</item>
/// <item><b>An aim offset is a direction, not a curve.</b> An aim-only diagonal
/// is one decision, one cooldown, one committed heading; it is enumerated beside
/// the straight bolt and needs no bend margin, while offset-plus-bend joins the
/// curved family. So a diagonally adjacent body is hit on the launch tick
/// instead of being unreachable at every distance.</item>
/// <item><b>Dodging is now a matter of degree.</b> One enemy facing lays three
/// rays, so stepping "out of the lane" mostly stops existing near a contact.
/// <see cref="Field.LanePressure"/> counts the rays over a tile and the router
/// takes the tile under fewest, which is the same question revision 4 asked as a
/// yes/no and gets the same answer wherever the offsets do not exist.</item>
/// <item><b>The fan has lost its argument, and the decline is re-measured.</b>
/// Revision 4 declined the cast but granted it BEARINGS — three rays the
/// cardinal-only gun could not open. The gun now opens exactly those three
/// headings, one at a time, at less than half the cadence and without giving up
/// the step, so the fan's whole remaining product is simultaneity across lanes.
/// Per-body damage is capped, so that is worth nothing while the lanes point at
/// one body: <see cref="Cast"/> now requires more than one body under the rays,
/// and refuses to feed a raised arc at all, because every deflected ray returns
/// to the tile a stance cannot leave.</item>
/// <item><b>A fortification is a posture, not a sentence.</b> Placement legality
/// is read from the ROUTE rather than from the union of the map's
/// transition-forbidden tags — a ground arm may empty the route's tags while the
/// map keeps them — and a zero-weight body that declares a route back into a
/// form with objective weight is priced as temporarily absent rather than spent.
/// Both are contract reads; neither assumes a once-per-life rule that this arm
/// does not have.</item>
/// </list>
///
/// Revision 6 is an IQ pass on MULTI-BODY COORDINATION and nothing else. The
/// doctrine above is unchanged, including the sight-band standoff and both
/// aperture tie-breaks; what is new is <see cref="Traffic"/>, which answers one
/// question — what may this body do given what its own siblings are doing — from
/// the frozen observation alone, since a life has no shared state, never sees an
/// ally's current action, and starts every new body with empty memory.
///
/// <list type="bullet">
/// <item><b>They never blocked; they oscillated, and that is the whole finding.</b>
/// Revision 5's route search returned the CHEAPEST legal first step. With the one
/// tile that shortens the route occupied by a sibling, the cheapest legal step is
/// a step AWAY — and next tick the step back is cheapest again. Under
/// <c>facing-locked</c> each leg also buys a rotation, so the body spends four
/// ticks arriving where it started, forever. Measured on the mirror, revision 5
/// blocked a sibling exactly zero times and still lost 152 route steps per
/// thousand two-body ticks to a tile a sibling was standing on. So a route step
/// must REDUCE the route, and yielding is a hold: no forward step means keep the
/// tile and spend the tick on the gun.</item>
/// <item><b>Precedence is written and it is about the game.</b> Nearer the
/// contested ground first, then MORE health — the body that can survive a
/// corridor is the one that should be in it — then actor identity, which only
/// makes the order total. Total is what makes it deadlock-free: the most senior
/// body claims nothing, so somebody always moves.</item>
/// <item><b>A sibling's claim is a preference, and the tier is measured.</b> A
/// senior body claims the tiles its shortest routes need this tick and next, plus
/// any one-tile corridor run it owns. Honouring that absolutely lost the mirror
/// 0-4-0 at the breach floor while removing most of the visible silliness;
/// binding only the corridors lost too and jammed them MORE. Ground outranks
/// courtesy.</item>
/// <item><b>Two bodies on one ray are one firing seat used twice</b>, so an
/// equal-value seat on a ray no sibling holds is preferred — measured inert on
/// this map, and reported as inert. The sibling of that rule, refusing a pose one
/// enemy facing sweeps together with a sibling, was built and DELETED: it removed
/// about a tenth of the shared-fan poses and cost 1.95 points of progress over
/// twenty seeds, which is the wave's test failed rather than passed.</item>
/// </list>
///
/// Nothing here assumes participant IDs, team IDs, slot counts, unlock ticks,
/// form names, action codes, map coordinates, projectile constants, shot-program
/// bounds, a movement profile's facing coupling, transition reversibility, or
/// the presence of any skill. All of it is resolved from the delivered contract
/// and the per-tick legality mask; on a contract with no stance route and no
/// guard the skill code is inert, and on one whose aim bounds are zero every
/// aperture query returns revision 4's cardinal-only answer.
/// </summary>
public sealed class VectorEdge : IGenericActorBot
{
    /// <summary>
    /// Expected value below which even a free tick is better spent elsewhere.
    /// It keeps a bolt that threatens nothing from looking like initiative.
    /// </summary>
    private const double FreeFire = 0.08;

    /// <summary>
    /// A rotation taken while the gun is ready throws the shot away, so the
    /// aim it buys has to be worth more than one whole bolt.
    /// </summary>
    private const double HotAimMargin = 2.0;

    /// <summary>Absolute floor under which a ready tick is never spent turning.</summary>
    private const double HotAimFloor = 0.30;

    /// <summary>Aim bought on a cooldown tick still has to beat the current facing.</summary>
    private const double ColdAimGain = 1.25;

    /// <summary>Expected value below which laying the gun threatens nothing.</summary>
    private const double ColdAimFloor = 0.10;

    /// <summary>Cost of standing where a three-ray fan already sweeps.</summary>
    private const double FanLanePenalty = 0.60;

    /// <summary>Cost of standing in a predicted enemy launch lane at all.</summary>
    private const double EnemyLanePenalty = 0.20;

    /// <summary>
    /// Cost of each launch ray beyond the first that crosses a tile. Under ±1
    /// initial aim one facing lays three rays, so "out of the lane" mostly stops
    /// existing near a contact and the honest choice is the tile under the
    /// fewest rays. Zero extra rays on every arm without the offsets, which is
    /// why this leaves the measured doctrine alone there.
    /// </summary>
    private const double ExtraLanePenalty = 0.10;

    /// <summary>
    /// Value of a pose that more than one facing arms.
    ///
    /// A cardinal bearing is launchable from exactly one facing; a diagonal
    /// bearing, once initial-aim offsets exist, is launchable from the two
    /// facings that bracket it. Under <c>facing-locked</c> a rotation is not a
    /// flourish but the way a body travels, so a target on a diagonal is the one
    /// pose where turning onto a route does not cost the shot. That is the
    /// striker's own use of the offsets, and it is unavailable to a chassis that
    /// never paid an aperture in the first place. Without the offsets no tile is
    /// ever armed from two facings and this term is identically zero.
    /// </summary>
    private const double DiagonalPoseValue = 0.30;

    /// <summary>
    /// Expected value a seat has to be worth before it is held against the
    /// ground. A blind band with nothing worth shooting in it is just a corner
    /// to hide in, which is not what this doctrine is for.
    /// </summary>
    private const double StandoffFloor = 0.30;

    /// <summary>Cost of sharing an enemy's bearing with an ally.</summary>
    private const double ClumpPenalty = 0.35;

    /// <summary>Cost of standing inside a raised shield's fixed arc.</summary>
    private const double GuardArcPenalty = 0.25;

    /// <summary>
    /// Cost of taking the launch ray onto a contact that a senior sibling
    /// already holds. Two guns on one ray are one firing seat used twice: the
    /// target slips both bolts with the same step, and the front body absorbs
    /// every answer meant for either. Priced below a whole bolt, because a
    /// duplicated line still beats no line.
    /// </summary>
    private const double SharedRayPenalty = 0.45;

    private Doctrine? _doctrine;
    private DodgeLedger _dodges = new();
    private FrontLedger _front = new();
    private Position? _lastDodgeOrigin;
    private int _avoidDodgeOriginThroughTick = -1;
    private Position? _lostRaceTile;
    private int _yieldRaceThroughTick = -1;

    /// <inheritdoc />
    public void StartLife(GenericActorMatchStart start)
    {
        _doctrine = Doctrine.Resolve(start);
        _dodges = new DodgeLedger();
        _front = new FrontLedger();
        _lastDodgeOrigin = null;
        _avoidDodgeOriginThroughTick = -1;
        _lostRaceTile = null;
        _yieldRaceThroughTick = -1;
    }

    /// <inheritdoc />
    public GenericActorDecision Tick(GenericActorContext context)
    {
        Doctrine? doctrine = _doctrine;
        if (doctrine is null)
            return Safe(context, "contract unavailable");
        try
        {
            return Decide(doctrine, context);
        }
        catch (Exception error)
        {
            // A bounded legal action always beats a runtime fault.
            return Safe(context, $"recovered: {error.GetType().Name}");
        }
    }

    private GenericActorDecision Decide(
        Doctrine doctrine,
        GenericActorContext context)
    {
        _dodges.Observe(doctrine, context);
        var field = new Field(doctrine, context);
        _front.Observe(doctrine, field);
        // What this body's own siblings are doing, derived from the same frozen
        // observation every one of them sees. Revision 6 is an IQ pass on this
        // layer and nothing else; see Traffic for the seven rules and the
        // measured attribution of each.
        var traffic = new Traffic(doctrine, field, context);
        NoteLostRace(field, context, traffic);
        // What this contract's capture policy makes the ground worth, this
        // tick. Every threshold and every destination below is priced against
        // it instead of against the rule card's default assumptions.
        Advance advance = Advance.Read(doctrine, field, context, _front);
        var solver = new ShotSolver(doctrine, field, _dodges);

        // 0. A stance is a different game with a different verb set: aim it,
        //    fire it, or leave it. Nothing below applies, because the form's own
        //    action mask offers no step and no gun but the one.
        if (field.InStance)
        {
            return Cast.Conduct(doctrine, field, context, solver)
                ?? Safe(context, "holding the fan");
        }

        // 1. More bodies is more pressure. Fabrication is only ever offered
        //    where the contract says it is legal, so this needs no geography.
        if (TryFabricate(doctrine, context) is { } fabrication)
            return fabrication;

        // The bolt in hand — null for the whole cooldown window, which is
        // exactly what makes those ticks cheap. The straight-only answer is
        // tracked separately: a committed trajectory has to be earned, and
        // cheap tempo is not the same thing as a good reason to bend.
        ShotPlan? shot = solver.Best(field.Facing, extraEnemyMoves: 0);
        ShotPlan? straight = solver.Best(
            field.Facing,
            extraEnemyMoves: 0,
            allowCurved: false);
        Position? rally = RallyPoint(doctrine, field, context);

        // 2. An inbound bolt is a question about ground, not about health.
        if (TryAnswerIncoming(doctrine, field, context, solver, shot, rally)
            is { } answer)
        {
            return answer;
        }

        // 2b. A fan already standing on the board is three rays leaving one
        //     gun this tick, and movement resolves before combat — so the tick
        //     the stance first appears is still a tick in which one step buys
        //     the whole launch. It is only ever a step that keeps the ground:
        //     conceding a tile to a shot is the habit this doctrine exists
        //     without.
        if (TryLeaveFan(doctrine, field, context) is { } sidestep)
            return sidestep;

        // 3. A lone body with a slot waiting is worth a round trip: one
        //    conceded push buys a second gun for the rest of the match. Once
        //    that trip is on, it outranks trading shots at the front.
        if (rally is not null
            && TryReinforce(doctrine, field, context, traffic) is { } reinforce)
        {
            return reinforce;
        }

        // 4. Plan the step first: what a tick is worth is what the step it
        //    displaces would buy, and a tick with no step left in it is free.
        March march = PlanMarch(
            doctrine,
            field,
            context,
            solver,
            straight,
            advance,
            traffic);

        // 4a. A seat this body can shoot from and the other body cannot answer
        //     from is the one tick in this mode worth more than a tile, and it is
        //     the striker's own asymmetry rather than a general trick. Holding it
        //     is a STOP, never a chase: it is read off the tile already occupied,
        //     so it cannot thrash the way a bearing-derived destination does.
        if (Standoff(doctrine, field, context, solver, advance))
            march = March.None;

        // 4b. One step outranks the gun: declining to complete a capture the
        //     opposing hold would discard is only possible by leaving the
        //     tile, so a shot taken instead of that step spends the claim it
        //     was protecting.
        if (march.Mandatory && march.Decision is not null)
            return march.Decision;

        // 4c. A fan buys bearings the gun does not have — three diverging rays
        //     from a chassis whose bolt leaves along one cardinal and bends no
        //     earlier than a tile out. It is priced against the whole window of
        //     ordinary fire the same ticks would buy, so it only wins where the
        //     gun cannot reach, and it is refused wherever standing still would
        //     cost ground.
        if (Cast.TryEnter(doctrine, field, context, solver, shot, march, advance)
            is { } cast)
        {
            return cast;
        }

        // 5. Fire. A committed trajectory always has to clear the price of the
        //    ground it costs; the straight answer may also be taken on a tick
        //    that had nothing else to spend.
        double positional = PositionalThreshold(field, advance);
        if (shot is not null && shot.Score >= positional)
            return shot.Decision;
        double commit = CommitThreshold(field, march, advance);
        if (straight is not null && straight.Score >= commit)
            return straight.Decision;

        // 6. A spare body may fortify the approach the objective depends on.
        if (TryFortify(doctrine, field, context) is { } fortify)
            return fortify;

        // 7. Facing is the striker's aim and its sight quadrant at once. Buy
        //    it on the last tick of the cooldown window, where it is free.
        if (TryLayGun(doctrine, field, context, solver, straight, march)
            is { } aim)
        {
            return aim;
        }

        // 8. Objective first: take the tile, or hold the tile.
        if (march.Decision is not null)
            return march.Decision;

        // 9. Nothing left to spend the tick on: take the cheap shot, then the
        //    cheap degree of facing.
        if (straight is not null && straight.Score >= FreeFire)
            return straight.Decision;
        if (TryLayGun(doctrine, field, context, solver, straight, March.None)
            is { } free)
        {
            return free;
        }

        // 10. Keep the gun pointed where the next body will come from.
        return TryOrient(doctrine, field, context)
            ?? Safe(context, "holding the line");
    }

    /// <summary>
    /// RULE 8. Records a step this body lost to its own traffic, so it stops
    /// trying the same tile every third tick.
    ///
    /// <para>The block itself is a published fact — <c>PreviousActionResolution</c>
    /// carries the outcome and the direction — and revision 5 already fed it into
    /// this tick's occupancy. What it could not do is carry it into the NEXT tick,
    /// and one tick of memory is exactly one tick too few: hold, forget, collide
    /// again, forever. The memory here is life-scoped private state, which is the
    /// only kind that exists, and the written precedence is what makes it
    /// asymmetric — only the body that is NOT senior for the contested tile backs
    /// off, so the tile clears rather than staying contested by two bodies being
    /// polite on the same schedule.</para>
    /// </summary>
    private void NoteLostRace(
        Field field,
        GenericActorContext context,
        Traffic traffic)
    {
        if (!Traffic.RaceMemory)
            return;
        if (context.Self.PreviousActionResolution is not
            {
                Outcome: GenericActorActionResolution.ActionOutcome.Blocked,
            } previous)
        {
            return;
        }
        if (previous.AcceptedAction.Arguments
                .OfType<GenericActorActionArgument.DirectionArgument>()
                .FirstOrDefault() is not { } step)
        {
            return;
        }
        (int dx, int dy) = step.Value.Vector();
        Position tile = field.Self.Offset(dx, dy);
        // Only a tile some SENIOR sibling wants. A tile an enemy took is not a
        // coordination problem, and a tile this body is senior for is one this
        // body should keep trying for.
        if (!traffic.Avoid.Contains(tile))
            return;
        _lostRaceTile = tile;
        _yieldRaceThroughTick = context.Tick + Traffic.RaceMemoryTicks;
    }

    /// <summary>
    /// The tile this body lost to its own traffic and is still leaving alone, or
    /// <see langword="null"/> once the memory has expired.
    /// </summary>
    private Position? LostRace(GenericActorContext context) =>
        _lostRaceTile is Position tile && context.Tick <= _yieldRaceThroughTick
            ? tile
            : null;

    private static GenericActorDecision? TryFabricate(
        Doctrine doctrine,
        GenericActorContext context)
    {
        GenericActorActionLegality? action = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Fabrication);
        GenericActorActionLegality.ArgumentConstraint.UnitTargetConstraint?
            targets = action?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .UnitTargetConstraint>()
                .FirstOrDefault();
        if (action is null || targets is null || targets.AllowedValues.IsEmpty)
            return null;

        GenericActorActionArgument.UnitTarget target = targets.AllowedValues
            .OrderBy(value => value.TeamId)
            .ThenBy(value => value.UnitId)
            .First();
        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.UnitTargetArgument(target)],
            $"building pressure at {target.TeamId}:{target.UnitId}");
    }

    /// <summary>
    /// The fabrication source this life should be walking to, or
    /// <see langword="null"/> when its place is at the front. Recomputed every
    /// tick from the contract and the frozen observation, so the trip ends the
    /// moment it stops being worth taking.
    /// </summary>
    private static Position? RallyPoint(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        // A contract that lands its automatic arrivals on the own-side
        // objective has already made this trip: the second gun appears beside
        // the fight whether or not this body walks home for it, so walking
        // home only removes the first gun from the front. Read the placement
        // policy rather than the arm's name.
        if (ArenaBasics.ArrivalsRallyForward(doctrine.Contract)
            || doctrine.FabricationSourceTiles.IsEmpty
            || field.AlliedObjectiveBodies >= 2
            || field.EnemyAdvancesRemaining < 2
            || doctrine.FabricationSourceTiles.Contains(field.Self))
        {
            return null;
        }

        HashSet<string> fabricationIds = doctrine.Contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Fabrication)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        bool canFabricate = context.ActionLegalities.Any(action =>
            action.AllowedByForm && fabricationIds.Contains(action.ActionId));
        bool slotWaiting = context.TeamUnits.Any(slot =>
            slot.State.Kind == GenericActorContext.UnitSlotStateKind.Ready);
        if (!canFabricate || !slotWaiting)
            return null;

        return doctrine.FabricationSourceTiles
            .OrderBy(tile => field.Self.ChebyshevDistance(tile))
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .First();
    }

    private GenericActorDecision? TryAnswerIncoming(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? shot,
        Position? rally)
    {
        if (field.ThreatAt(field.Self) is not 0)
            return null;

        GenericActorActionLegality? move = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();

        // Under a facing-locked profile the movement mask offers the current
        // facing and nothing else, so a sideways dodge is a rotation this tick
        // and a step the next — two ticks against a bolt that lands on the
        // first one. The escape set collapses on its own, which is the reason
        // to read the mask instead of assuming four cardinals.
        (Direction Direction, Position Tile)[] escapes =
            move is null || directions is null
                ? []
                : directions.AllowedValues
                    .Select(direction =>
                    {
                        (int dx, int dy) = direction.Vector();
                        return (
                            Direction: direction,
                            Tile: field.Self.Offset(dx, dy));
                    })
                    .Where(candidate =>
                        field.CanEnter(candidate.Tile)
                        && field.ThreatAt(candidate.Tile) is not 0)
                    .ToArray();

        (Direction Direction, Position Tile)[] preferred = field.OnObjective
            ? escapes
                .Where(candidate => field.IsObjective(candidate.Tile))
                .ToArray()
            : escapes;

        // Suppression over concession: while this body owns an objective tile
        // and can survive the hit, answering the shot keeps ground that
        // stepping aside would surrender.
        if (field.OnObjective && preferred.Length == 0 && field.Health > 1)
        {
            if (shot is not null)
                return shot.Decision;
            if (TryAimAtNearest(doctrine, field, context) is { } turn)
                return turn;
            return Safe(context, "absorbing a hit to keep the objective");
        }

        (Direction Direction, Position Tile)[] pool =
            preferred.Length > 0 ? preferred : escapes;
        if (pool.Length == 0 || move is null)
            return shot?.Decision;

        // Where a step is also a turn, a dodge spends the aim and the sight
        // quadrant along with the tile. Price that before taking it: a bolt in
        // hand worth more than every post-dodge answer is worth the hit,
        // because the dodge would cost the shot and the ground together.
        double bestAfter = pool.Max(candidate =>
            ReaimValue(field, solver, candidate.Direction));
        if (field.MoveTurns
            && field.OnObjective
            && field.Health > 1
            && shot is not null
            && shot.Score >= bestAfter)
        {
            return shot.Decision;
        }

        Direction chosen = pool
            .OrderByDescending(candidate =>
                rally is null && field.IsObjective(candidate.Tile))
            .ThenBy(candidate => field.ThreatAt(candidate.Tile) ?? int.MaxValue)
            .ThenBy(candidate => field.LanePressure(candidate.Tile))
            .ThenBy(candidate => rally is Position point
                ? candidate.Tile.ChebyshevDistance(point)
                : field.DistanceToObjective(candidate.Tile))
            .ThenByDescending(candidate =>
                ReaimValue(field, solver, candidate.Direction))
            .ThenBy(candidate => Array.IndexOf(field.Order, candidate.Direction))
            .First()
            .Direction;
        _lastDodgeOrigin = field.Self;
        _avoidDodgeOriginThroughTick = context.Tick + 1;
        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(chosen)],
            $"slipping the bolt toward {chosen}");
    }

    /// <summary>
    /// Steps out of a fan that can launch this tick, without ever giving up
    /// ground for it. On the objective only another objective tile will do; off
    /// it, any tile out of the rays. When no such tile exists the tick falls
    /// through to the ordinary answer — the shot, or the ground — because a fan
    /// is still only one damage per body it catches.
    /// </summary>
    private static GenericActorDecision? TryLeaveFan(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        if (!field.InPredictedFan(field.Self) || !field.FanImminent)
            return null;

        GenericActorActionLegality? move = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (move is null || directions is null)
            return null;

        (Direction Direction, Position Tile)[] out_ = directions.AllowedValues
            .Select(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return (Direction: direction, Tile: field.Self.Offset(dx, dy));
            })
            .Where(candidate =>
                field.CanEnter(candidate.Tile)
                && !field.InPredictedFan(candidate.Tile)
                && field.ThreatAt(candidate.Tile) is not 0
                && (!field.OnObjective || field.IsObjective(candidate.Tile)))
            .OrderBy(candidate => field.LanePressure(candidate.Tile))
            .ThenBy(candidate => field.DistanceToObjective(candidate.Tile))
            .ThenBy(candidate =>
                Array.IndexOf(field.Order, candidate.Direction))
            .ToArray();
        if (out_.Length == 0)
            return null;

        Direction chosen = out_[0].Direction;
        return new GenericActorDecision(
            move.ActionId,
            move.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(chosen)],
            $"stepping out of the fan toward {chosen}");
    }

    /// <summary>
    /// What the gun would be worth after stepping <paramref name="step"/> —
    /// from the tile it lands on, through the facing that step leaves behind.
    /// Under an uncoupled profile the facing term drops out and only the tile
    /// moves, so the same expression serves every arm.
    /// </summary>
    private static double ReaimValue(
        Field field,
        ShotSolver solver,
        Direction step)
    {
        (int dx, int dy) = step.Vector();
        return solver.Forecast(
            field.FacingAfter(step),
            extraEnemyMoves: 1,
            from: field.Self.Offset(dx, dy));
    }

    /// <summary>
    /// Expected value a straight shot must reach before it is worth the tick.
    /// The ground is priced exactly as revision 1 priced it; the only change
    /// is that a tick with no step left in it is recognized as free rather
    /// than charged for a march that was never going to happen.
    /// </summary>
    private static double CommitThreshold(
        Field field,
        March march,
        Advance advance)
    {
        // No step to displace: any bolt that threatens something wins.
        if (march.Decision is null)
            return FreeFire;

        return PositionalThreshold(field, advance);
    }

    /// <summary>
    /// What the contested ground is worth this tick. The table is revision 1's
    /// exactly — the numbers were probe-tuned and are not this revision's
    /// budget. What changed is the two predicates that select a row, which were
    /// assumptions about the rules and are now readings of them:
    ///
    /// <list type="bullet">
    /// <item>"an enemy body is standing here" meant "my presence earns
    /// nothing, so removing that body is the capture". Under a policy where
    /// surplus weight scales pressure it means no such thing — a contested
    /// objective this team outnumbers still pays every tick, so the row for a
    /// body that is capturing applies. <see cref="Advance.Nulled"/> is the
    /// contract's answer to the question the field was standing in for.</item>
    /// <item>"control is paused" meant "there is no advance to win from this
    /// ground yet". A capture completed inside the opposing hold is discarded,
    /// which is the same statement about the same ground.</item>
    /// </list>
    ///
    /// On a contract that declares binary control and no hold, both readings
    /// collapse onto the fields they replaced and the table selects exactly the
    /// rows revision 2 selected.
    /// </summary>
    private static double PositionalThreshold(Field field, Advance advance) =>
        field.OnObjective && advance.Nulled ? 0.28
        : field.OnObjective ? 0.38
        : advance.NoGroundToWin ? 0.42
        : field.IsCapturer ? 0.58 : 0.46;

    private static GenericActorDecision? TryFortify(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        // A contract may declare several same-life routes with several actions —
        // this kit declares a parameterless return beside the form-target
        // request — so the route is found by the argument it needs rather than
        // by whichever action code sorts first within the kind.
        GenericActorActionLegality? action = null;
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = null;
        foreach (GenericActorActionLegality candidate in AvailableAll(
                     doctrine,
                     context,
                     GenericActorRulesContract.ActionKind.SameLifeTransition))
        {
            forms = candidate.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .FirstOrDefault();
            if (forms is not null && !forms.AllowedFormIds.IsEmpty)
            {
                action = candidate;
                break;
            }
        }
        if (action is null || forms is null)
            return null;

        GenericActorRulesContract.Form? current =
            doctrine.FormFor(field.FormId);
        int distance = field.DistanceToObjective(field.Self);
        if (field.IsCapturer
            || field.AlliedObjectiveBodies < 2
            || distance < 1
            || distance > 3
            || field.ControlPaused
            || field.ThreatAt(field.Self) is not null
            || current is null
            || field.Health < current.MaxHealth)
        {
            return null;
        }

        // Only a genuinely tougher, objective-neutral emplacement is worth the
        // mobility this life gives up — and never a stance the engine will pull
        // this life back out of on a budget, which is a cast rather than a
        // commitment.
        //
        // Two contract reads decide where and whether, and revision 5 corrects
        // both. WHERE comes from the ROUTE's own placement legality rather than
        // from the union of transition-forbidden tags on the map: a ground arm
        // may empty a route's forbidden tags while the map keeps them, and the
        // old test then refuses a legal emplacement on exactly the tiles that
        // matter. WHETHER is priced off `irreversibleForLife`, which is no longer
        // always true — where the route back out exists this is a posture rather
        // than a life sentence, so a body may take it closer to the fight.
        GenericActorRulesContract.Form? target = forms.AllowedFormIds
            .Select(doctrine.FormFor)
            .Where(form =>
                form is not null
                && form.ObjectiveWeight <= 0
                && form.MaxHealth > current.MaxHealth
                && form.AttackProfileId is not null
                && doctrine.Skills.ReturnFrom(form.Id)?.Threshold
                    is null or int.MaxValue
                && doctrine.RouteFor(field.FormId, form.Id)
                    is GenericActorRulesContract.FormTransition route
                && doctrine.PlacementAllows(route, field.Self))
            .OrderByDescending(form => form!.MaxHealth)
            .ThenBy(form => form!.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target is null)
            return null;

        return new GenericActorDecision(
            action.ActionId,
            action.ActionCode,
            [new GenericActorActionArgument.FormTargetArgument(target.Id)],
            $"emplacing as {target.Id} to lock the approach");
    }

    /// <summary>
    /// Lays the gun, priced against the cadence.
    ///
    /// The measured failure of revision 1 lives here: the solver could only
    /// answer "what would I hit facing there?" on a tick when it could also
    /// fire, so every turn was paid for with a whole shot — and two of every
    /// five such turns never produced one, because the life ended first. The
    /// forecast now works on a cooldown tick, so the gun is laid on the last
    /// tick of the window, where the shot it buys is the very next one.
    /// </summary>
    private static GenericActorDecision? TryLayGun(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? shot,
        March march)
    {
        GenericActorRulesContract.AttackProfile? attack =
            doctrine.AttackFor(field.FormId);
        if (!solver.HasTargets
            || attack is null
            || !context.Enemies.Any(enemy =>
                field.Self.ChebyshevDistance(enemy.Position)
                    <= attack.Projectile.MaxTravelTiles))
        {
            return null;
        }
        GenericActorActionLegality? rotate = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (rotate is null || directions is null)
            return null;

        bool ready = solver.Ready;
        int wait = ready ? 1 : Math.Max(1, field.Cooldown);
        double floor;
        if (ready)
        {
            // A ready gun is a whole bolt. Turning throws it away.
            floor = Math.Max(HotAimFloor, (shot?.Score ?? 0.0) * HotAimMargin);
        }
        else if (march.Decision is null)
        {
            floor = FreeFire;
        }
        else if (march.TakesGround || field.Cooldown > 1)
        {
            // Either the step is itself the score, or the window still has
            // room: the gun can be laid on its last tick without costing this
            // one a tile.
            return null;
        }
        else
        {
            floor = ColdAimFloor;
        }

        double held = solver.Forecast(field.Facing, wait);
        Direction? best = null;
        double bestScore = 0.0;
        foreach (Direction direction in field.Order)
        {
            if (direction == field.Facing
                || !directions.AllowedValues.Contains(direction))
            {
                continue;
            }
            double plan = solver.Forecast(direction, wait);
            if (plan > bestScore)
            {
                bestScore = plan;
                best = direction;
            }
        }

        if (best is not Direction facing
            || bestScore < floor
            || bestScore <= held * ColdAimGain)
        {
            return null;
        }
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(facing)],
            $"laying the gun {facing} ev={bestScore:0.00}");
    }

    /// <summary>
    /// Walks back to a declared fabrication source, planning around whatever
    /// tiles other bodies are about to claim.
    /// </summary>
    private static GenericActorDecision? TryReinforce(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        Traffic traffic)
    {
        GenericActorActionLegality? move = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (move is null || directions is null)
            return null;

        var sources = doctrine.FabricationSourceTiles.ToHashSet();
        HashSet<Direction> plannable = Travelable(
            doctrine,
            field,
            context,
            directions);
        Direction? step = field.StepToward(
            sources,
            plannable,
            Yielded(field, context, traffic),
            requireProgress: Traffic.HoldNotDetour)
            ?? field.StepToward(
                sources,
                plannable,
                avoid: null,
                requireProgress: Traffic.HoldNotDetour);
        if (step is not Direction chosen)
            return null;
        return Travel(
            field,
            move,
            doctrine,
            context,
            chosen,
            $"falling back to build a second gun via {chosen}")
            ?.Decision;
    }

    /// <summary>
    /// The step this tick would otherwise take, and whether that step is worth
    /// a shot. Planning it before the fire decision is what makes the fire
    /// decision honest: a tick is worth exactly what it buys.
    /// </summary>
    private March PlanMarch(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? straight,
        Advance advance,
        Traffic traffic)
    {
        GenericActorActionLegality? move = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Movement);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (move is null || directions is null || field.Objective.IsEmpty)
            return March.None;

        HashSet<Direction> plannable = Travelable(
            doctrine,
            field,
            context,
            directions);

        // RULE 4, the half a corridor cannot resolve by waiting. Two of this
        // team's bodies inside one one-tile run cannot pass, and the senior one
        // may be walking toward this one — so the junior body backs out along
        // the run. Turning in place is what revision 5 did here, and a rotation
        // inside a full corridor buys nothing at all.
        if (traffic.BackOut(plannable) is { } retreat
            && Travel(
                field,
                move,
                doctrine,
                context,
                retreat,
                $"yielding the corridor — backing {retreat}")
                is { } backing)
        {
            return backing with { Mandatory = true };
        }

        if (field.OnObjective)
        {
            // A capture the opposing hold will discard is worth less than the
            // claim it resets. One tile off the objective stops the count
            // exactly one tick short and keeps the bank for the far side of
            // the hold; the ledger only asks for it when the bank can survive
            // the wait and no enemy body is close enough to take the ground.
            if (advance.WithholdCompletion
                && StepAside(
                    doctrine,
                    field,
                    context,
                    move,
                    plannable,
                    Yielded(field, context, traffic))
                    is { } aside)
            {
                return aside;
            }
            return Reseat(
                doctrine,
                field,
                context,
                solver,
                straight,
                move,
                plannable,
                traffic);
        }

        // Where control is binary, stacking allies on one objective buys
        // nothing: a body that is not the nearest to an objective an ally
        // already holds takes the supporting ring instead, where it covers the
        // approaches. Where surplus objective weight scales capture pressure
        // the same step is the fastest capture on the board — two bodies of
        // net weight gain twice as fast, and outnumbering a contested
        // objective is the only thing that moves it at all. The contract says
        // which world this is; the doctrine used to assume.
        var free = new HashSet<Position>();
        foreach (Position tile in field.Objective)
        {
            if (!field.IsOccupied(tile) || tile == field.Self)
                free.Add(tile);
        }
        // Withholding is a standing intent, not one step: a body that has just
        // left the objective to protect a claim must not walk straight back on
        // and complete it. It waits one tile out, where it still covers the
        // ground, until the arithmetic turns over or an enemy gets close
        // enough to take it.
        HashSet<Position> goals =
            advance.WithholdCompletion
                ? field.Ring(1, 2)
                : !advance.StackHelps
                && !field.IsCapturer
                && field.AllyOnObjective
                && !field.EnemyOnObjective
                    ? field.Ring(1, 2)
                    : free.Count > 0
                        ? free
                        : field.Objective.ToHashSet();
        if (goals.Count == 0 && advance.WithholdCompletion)
            goals = field.Ring(1, 3);
        if (goals.Count == 0)
            goals = field.Objective.ToHashSet();

        // Envelopment over concentration. A side that may field more bodies
        // than this one wins any straight lane trade, and both specials in this
        // kit are built to punish a shared bearing: a fan sweeps three rays from
        // one gun, and a shield answers exactly one bearing and nothing else. So
        // where an ally is already committed and a contact is visible, this body
        // takes a destination on a different bearing — but never no destination
        // at all, because ground still outranks geometry.
        if (!context.Allies.IsEmpty && field.NearestEnemy is Position contact)
        {
            HashSet<Position> spread = goals
                .Where(tile => !field.BearingClash(contact, tile))
                .ToHashSet();
            if (spread.Count > 0)
                goals = spread;
        }


        var avoid = new HashSet<Position>();
        foreach (Direction direction in Field.Cardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position tile = field.Self.Offset(dx, dy);
            if (field.ThreatAt(tile) is 0)
                avoid.Add(tile);
        }
        if (_lastDodgeOrigin is Position origin
            && context.Tick <= _avoidDodgeOriginThroughTick)
        {
            avoid.Add(origin);
        }

        // Two bodies walking into the same tile simply block each other, which
        // spends a tick for nothing. Every life sees the same frozen picture,
        // so the one whose identity sorts later yields the contested step —
        // no shared state, no negotiation, no deadlock.
        foreach (Position tile in EnemyClaims(field, context))
            avoid.Add(tile);
        if (LostRace(context) is Position lostRace)
            avoid.Add(lostRace);
        // RULES 1, 3, 4 and 5 arrive as one set of tiles: a senior sibling's
        // committed two-tick route, the corridor runs it owns, and the landing
        // tile an imminent arrival of this team's own needs. Which sibling is
        // senior is rule 1's written order, and that order is a strict TOTAL one,
        // so the most senior body claims nothing from anyone and always has its
        // route — there is no cycle to deadlock on. The set is dropped on the
        // second pass, deliberately and measurably: see Traffic.Avoid for the two
        // binding variants that were built and lost.
        IReadOnlySet<Position> siblings = traffic.Avoid;

        // Two routing changes were built, measured, and thrown away here; both
        // notes are in DX.md because both reasons generalize past this bot.
        //
        // A destination chosen for its bearing to a contact thrashes: under
        // facing-locked a change of destination costs a ROTATION, and a
        // bearing-derived destination changes every time the contact steps, so
        // the body pays a turn per enemy step and arrives nowhere. A pose may be
        // preferred among equally good steps — which is free — but never chased.
        //
        // Three routing changes were built, measured, and thrown away here. All
        // three numbers are in DX.md, because two of the reasons generalize past
        // this bot and the third is a warning about this mode.
        //
        // A DESTINATION chosen for its bearing to a contact thrashes: under
        // facing-locked a change of destination costs a ROTATION, and a
        // bearing-derived destination changes every time the contact steps, so
        // the body pays a turn per enemy step and arrives nowhere (2-38-0).
        //
        // Routing in ACTIONS rather than tiles is the correct cost model for a
        // profile where a turn is a tick, and it worked as designed — first claim
        // moved from tick 80 to 51 against a slower chassis and to 12 in the
        // mirror. It lost every mirror match anyway (0-40-0). Arriving first is
        // what loses: companions unlock at 120 and 260 and arrivals rally to the
        // owning side of the ACTIVE objective, so a body that moves the front
        // before it has a companion is a lone body deep in ground the opponent
        // respawns beside. Correct arithmetic, wrong objective.
        // RULE 2 rides on `requireProgress`: a route step reduces the route, and
        // where none does this body HOLDS its tile and spends the tick on its
        // gun rather than touring the tile behind it. Both passes carry it, since
        // the second pass — the one that drops the claims — is exactly where
        // revision 5 found its detour.
        var all = new HashSet<Position>(avoid);
        all.UnionWith(siblings);
        List<Direction> steps = field.StepsToward(
            goals,
            plannable,
            all,
            requireProgress: Traffic.HoldNotDetour);
        if (steps.Count == 0)
        {
            // The fallback drops every preference and keeps one FACT: the tile
            // this body already collided with a sibling on, left alone for two
            // ticks while the senior body walks through it. Refusing a sibling's
            // merely INTENDED tile here was built and lost the mirror; refusing
            // one it already lost costs nothing, because the collision has
            // already happened.
            steps = field.StepsToward(
                goals,
                plannable,
                LostRace(context) is Position lost
                    ? new HashSet<Position> { lost }
                    : null,
                requireProgress: Traffic.HoldNotDetour);
        }
        if (steps.Count == 0)
            return March.None;

        Direction chosen = Prefer(
            field,
            solver,
            steps,
            directions.AllowedValues.ToHashSet(),
            traffic);
        return Travel(
            field,
            move,
            doctrine,
            context,
            chosen,
            $"pressing the objective via {chosen}")
            ?? March.None;
    }

    /// <summary>
    /// True when this tile is a seat the other body cannot answer from — so the
    /// tick is worth more standing still than walking.
    ///
    /// <para>This is the striker's asymmetry, and every number in it is the
    /// contract's rather than the class table's. "Range" is three different
    /// numbers per form, and on a chassis built around a facing quadrant two of
    /// them disagree with an opponent's in a way worth exploiting: a body whose
    /// declared sight envelope does not contain this tile cannot aim at it at
    /// all, and a body whose projectile travel does not reach this tile cannot
    /// touch it if it does. Either one makes the exchange free, and free damage
    /// against a chassis with more health than this one is the only way that duel
    /// is ever won — closing to trade with it is losing on purpose.</para>
    ///
    /// <para>It is also where the initial-aim offsets pay for themselves against
    /// an omnidirectional chassis, which is the comparison this wave is about. A
    /// standoff seat has to have a firing line, and with the offsets three rays
    /// leave every facing instead of one, so roughly twice as many tiles in the
    /// blind band are seats at all. A shorter-ranged chassis gains nothing from
    /// the same offsets out here, because it cannot reach this far to begin
    /// with.</para>
    ///
    /// <para>Ground still outranks it, everywhere it matters: this never applies
    /// while standing on the objective, never while a body that can actually
    /// score stands on it, and never while a claim is being withheld. A
    /// zero-weight body is skipped outright — shooting a fortification that
    /// cannot capture is not a reason to stop taking the point it is sitting
    /// on.</para>
    /// </summary>
    private static bool Standoff(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        Advance advance)
    {
        if (field.OnObjective
            || field.EnemyOnObjective
            || advance.WithholdCompletion)
        {
            return false;
        }
        // A shot from this pose, ready now or at the end of the cooldown window:
        // the standoff has to survive the ticks the gun is reloading, or the body
        // simply alternates between holding and walking.
        if (solver.Forecast(field.Facing, Math.Max(0, field.Cooldown))
            < StandoffFloor)
        {
            return false;
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            GenericActorRulesContract.Form? form =
                doctrine.FormFor(enemy.FormId);
            if ((form?.ObjectiveWeight ?? 1) <= 0)
                continue;
            // The asymmetry has to be DURABLE, and this is where the first
            // measured version of this rule was wrong. Standing outside a
            // chassis's facing quadrant is not safety: a quadrant is one
            // rotation wide, so the blind spot closes for a tick's price and the
            // body that trusted it is holding still inside a gun's envelope.
            // What cannot be rotated away is a declared RANGE. A sight envelope
            // that does not extend this far cannot be turned to reach it, and a
            // projectile that cannot travel this far cannot be aimed into it.
            // Against an omnidirectional chassis those two numbers are the whole
            // of the striker's edge — it sees four and shoots six where this
            // chassis sees six and shoots eight — and against a chassis whose
            // envelope matches this one they are never true, so the rule
            // correctly does nothing at all in a mirror.
            int distance = enemy.Position.ChebyshevDistance(field.Self);
            int reach = doctrine.AttackFor(enemy.FormId)
                ?.Projectile.MaxTravelTiles ?? int.MaxValue;
            if (distance > reach)
                return true;

            // A sensor edge is only an edge where it is THIS chassis's edge.
            // Allied perception is an immediate union, so "it cannot see me" is
            // never quite a fact — an ally of its own standing closer can hand it
            // this tile. What is a fact is that a chassis whose declared sight is
            // shorter than this one's is blind in a band this one is not, at every
            // bearing and from every facing, and that band is where a duel
            // against more health than this body carries is winnable at all.
            // Where the envelopes match, the clause is false and the rule is
            // inert — which is exactly right in a mirror, where there is no edge
            // to hold and the ground is the whole game.
            int sight = doctrine.VisionFor(enemy.FormId)
                is GenericActorRulesContract.VisionProfile vision
                ? Math.Max(vision.Range, vision.OmnidirectionalProximityRange)
                : int.MaxValue;
            int own = doctrine.VisionFor(field.FormId)
                is GenericActorRulesContract.VisionProfile mine
                ? Math.Max(mine.Range, mine.OmnidirectionalProximityRange)
                : 0;
            if (sight < own && distance > sight)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Directions this life may plan a route through. Under a facing-locked
    /// profile only the current facing is offered to a move, so a route step
    /// in any other direction is a rotation this tick and a step the next; the
    /// rotation domain is therefore the honest travel domain.
    /// </summary>
    private static HashSet<Direction> Travelable(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint
            movement)
    {
        if (!field.MoveLocked)
            return movement.AllowedValues.ToHashSet();
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            turns = Available(
                doctrine,
                context,
                GenericActorRulesContract.ActionKind.Rotation)
                ?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        return turns is null
            ? movement.AllowedValues.ToHashSet()
            : turns.AllowedValues.ToHashSet();
    }

    /// <summary>
    /// Turns a chosen travel direction into the action that actually starts
    /// it. Where the profile allows that step it is the step; where only the
    /// facing may move, it is the turn that makes the step legal next tick.
    /// </summary>
    private static March? Travel(
        Field field,
        GenericActorActionLegality move,
        Doctrine doctrine,
        GenericActorContext context,
        Direction chosen,
        string reason)
    {
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = move.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        (int dx, int dy) = chosen.Vector();
        bool gains = field.IsObjective(field.Self.Offset(dx, dy));
        if (directions is not null && directions.AllowedValues.Contains(chosen))
        {
            return new March(
                new GenericActorDecision(
                    move.ActionId,
                    move.ActionCode,
                    [new GenericActorActionArgument.DirectionArgument(chosen)],
                    reason),
                gains);
        }

        if (!field.MoveLocked)
            return null;
        GenericActorActionLegality? rotate = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            turns = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (rotate is null
            || turns is null
            || !turns.AllowedValues.Contains(chosen))
        {
            return null;
        }
        return new March(
            new GenericActorDecision(
                rotate.ActionId,
                rotate.ActionCode,
                [new GenericActorActionArgument.DirectionArgument(chosen)],
                $"turning onto the route — {chosen}"),
            gains);
    }

    /// <summary>
    /// Picks between equally cheap first travel intents. The cost is already
    /// equal, so what is left is bearing: a fan sweeps three rays from one gun, a
    /// shield's arc answers one bearing and nothing else, and two allied bodies
    /// on the same bearing from an enemy are two hits from one attack.
    ///
    /// <para>Revision 5 scores the pose the action actually produces. Under
    /// facing-locked an "equally short step" is often a ROTATION — the turn that
    /// unlocks the step next tick — and revision 4 scored it as though the body
    /// had already moved, charging lanes and arcs to a tile it is not standing on
    /// and crediting nothing for the aim the turn buys. A rotation keeps the tile
    /// and changes the facing; a step keeps the facing and changes the tile. Both
    /// are priced from the pose they leave behind, so the route's own turns are
    /// chosen for what they will be able to shoot — which is the whole of the
    /// striker's tick economy in an arm where turning is how a body
    /// travels.</para>
    /// </summary>
    private static Direction Prefer(
        Field field,
        ShotSolver solver,
        List<Direction> steps,
        IReadOnlySet<Direction> steppable,
        Traffic traffic)
    {
        if (steps.Count == 1)
            return steps[0];
        Direction best = steps[0];
        double bestScore = double.NegativeInfinity;
        Position? enemy = field.NearestEnemy;
        foreach (Direction step in steps)
        {
            bool stepping = steppable.Contains(step);
            (int dx, int dy) = step.Vector();
            Position tile = stepping
                ? field.Self.Offset(dx, dy)
                : field.Self;
            Direction facing = stepping ? field.FacingAfter(step) : step;
            double score = 0.0;
            int rays = field.LanePressure(tile);
            if (field.InPredictedFan(tile))
                score -= FanLanePenalty;
            else if (rays > 0)
                score -= EnemyLanePenalty;
            score -= ExtraLanePenalty * Math.Max(0, rays - 1);
            // The pose the offsets pay for: a bearing more than one facing arms.
            score += DiagonalPoseValue
                * Math.Max(0, field.ArmingFacings(tile) - 1);
            if (enemy is Position contact)
            {
                // Envelopment. Clumping onto one bearing is what both specials
                // in this kit are paid to punish; arriving from two is what
                // neither can answer.
                if (field.BearingClash(contact, tile))
                    score -= ClumpPenalty;
                if (field.InGuardArc(tile))
                    score -= GuardArcPenalty;
            }
            // RULE 7. A ray a senior sibling already holds is a firing seat
            // used twice: the target dodges both bolts with the same step, and
            // the front body eats every answer for both.
            if (traffic.SharesRay(tile))
                score -= SharedRayPenalty;
            if (field.MoveTurns)
                score += ReaimValue(field, solver, step);
            if (score > bestScore)
            {
                bestScore = score;
                best = step;
            }
        }
        return best;
    }

    /// <summary>
    /// Tiles another body is about to claim: every step that takes a
    /// higher-priority ally, or any visible enemy, strictly closer to the
    /// objective it is walking toward.
    /// </summary>
    /// <summary>
    /// Every tile this body has yielded to something else this tick: an enemy's
    /// likely step, and the whole of a sibling's claim.
    /// </summary>
    private static HashSet<Position> Yielded(
        Field field,
        GenericActorContext context,
        Traffic traffic)
    {
        var claimed = EnemyClaims(field, context);
        claimed.UnionWith(traffic.Avoid);
        return claimed;
    }

    private static HashSet<Position> EnemyClaims(
        Field field,
        GenericActorContext context)
    {
        // The enemy half of revision 5's contested set, unchanged and still
        // soft: an opposing body walking toward the same objective will probably
        // take this tile, which is a good reason to prefer another and never a
        // reason to stand still. The allied half moved into
        // <see cref="Traffic"/>, where it became a rule with a stated
        // precedence instead of a comparison of identities.
        var claimed = new HashSet<Position>();
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            int distance = field.DistanceToObjective(enemy.Position);
            foreach (Direction direction in Field.Cardinals)
            {
                (int dx, int dy) = direction.Vector();
                Position tile = enemy.Position.Offset(dx, dy);
                if (field.DistanceToObjective(tile) < distance)
                    claimed.Add(tile);
            }
        }
        return claimed;
    }

    /// <summary>
    /// One step off the objective that keeps the body beside it: the only way
    /// a life can decline to complete a capture, because presence is passive.
    /// The seat it takes is chosen exactly as a firing seat is — no inbound
    /// bolt, out of a lane a visible enemy is already pointing down, and back
    /// on the objective next tick — so the tick spent withholding is still a
    /// tick spent covering the ground it is protecting.
    /// </summary>
    private static March? StepAside(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        GenericActorActionLegality move,
        HashSet<Direction> allowed,
        HashSet<Position> claimed)
    {
        // An objective tile usually has one exit, and an ally standing beside
        // it is often walking through that exit. Yielding to the ally is the
        // right first answer and a deadlock as the only answer, so the second
        // pass takes the tile anyway: staying on the objective spends the
        // claim, which is worse than two bodies blocking for one tick.
        foreach (bool yieldToAllies in new[] { true, false })
        {
            foreach (Direction direction in allowed
                .Select(direction =>
                {
                    (int dx, int dy) = direction.Vector();
                    return (
                        Direction: direction,
                        Tile: field.Self.Offset(dx, dy));
                })
                .Where(candidate =>
                    !field.IsObjective(candidate.Tile)
                    && field.CanEnter(candidate.Tile)
                    && (!yieldToAllies || !claimed.Contains(candidate.Tile))
                    && field.ThreatAt(candidate.Tile) is not 0)
                .OrderBy(candidate =>
                    field.LanePressure(candidate.Tile))
                .ThenBy(candidate => field.DistanceToObjective(candidate.Tile))
                .ThenBy(candidate =>
                    Array.IndexOf(field.Order, candidate.Direction))
                .Select(candidate => candidate.Direction))
            {
                if (Travel(
                        field,
                        move,
                        doctrine,
                        context,
                        direction,
                        $"withholding a spent capture — stepping {direction}")
                    is { } aside)
                {
                    return aside with { Mandatory = true };
                }
            }
        }
        return null;
    }

    private static March Reseat(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? straight,
        GenericActorActionLegality move,
        HashSet<Direction> allowed,
        Traffic traffic)
    {
        // Already on the objective, so the only steps worth taking are the
        // ones that keep it. Where a step is also a turn, the seat and the
        // facing are one choice, so seats are ranked by what they could fire —
        // plus, now, by how many facings the seat is armed from and how many
        // launch rays cross it, both of which only move once the contract
        // declares initial-aim offsets.
        (Direction Direction, Position Tile)[] seats = allowed
            .Select(direction =>
            {
                (int dx, int dy) = direction.Vector();
                return (
                    Direction: direction,
                    Tile: field.Self.Offset(dx, dy));
            })
            .Where(candidate =>
                field.IsObjective(candidate.Tile)
                && field.CanEnter(candidate.Tile)
                && field.ThreatAt(candidate.Tile) is not 0)
            .OrderByDescending(candidate =>
                SeatValue(field, solver, candidate, traffic))
            .ThenBy(candidate => Array.IndexOf(field.Order, candidate.Direction))
            .ToArray();
        if (seats.Length == 0)
            return March.None;

        // Holding ground with a body in sight and no line to it is the one
        // thing a duelist must never settle for: take the seat that has one.
        // "A line" is the contract's aperture, not an assumption about
        // cardinals — where the gun may launch 45 degrees off its facing, a seat
        // diagonal to a body is armed, and revision 4 would have walked off it
        // looking for an alignment it no longer needs.
        // A bolt this life can fire on this very tick — not one it could fire
        // if the gun were ready. Holding a seat because of a shot that is two
        // ticks away is how a body stays in a lane it should have left.
        bool hasShot = straight is not null;
        if (!hasShot
            && !context.Enemies.IsEmpty
            && !field.Armed(field.Self))
        {
            // RULE 7 on the seat itself: among armed seats, take one whose ray
            // onto a contact is not the ray a senior sibling is already holding.
            // Only where such a seat exists — a seat with a line always beats a
            // seat without one.
            foreach ((Direction direction, Position tile) in seats
                .Where(candidate => field.Armed(candidate.Tile)
                    && !traffic.SharesRay(candidate.Tile))
                .Concat(seats))
            {
                if (!field.Armed(tile))
                    continue;
                return Travel(
                    field,
                    move,
                    doctrine,
                    context,
                    direction,
                    $"taking the firing seat {direction}")
                    ?? March.None;
            }
        }

        if (hasShot)
            return March.None;

        // Only shuffle to stand under fewer launch rays than this
        // tile does, and never at the cost of a shot of our own. Revision 4
        // asked this as "leave the lane", which was the same question while a
        // gun had one ray; with three it is a question of degree.
        int here = field.LanePressure(field.Self);
        if (here == 0)
            return March.None;
        foreach ((Direction direction, Position tile) in seats)
        {
            if (field.LanePressure(tile) >= here)
                continue;
            return Travel(
                field,
                move,
                doctrine,
                context,
                direction,
                $"shifting out from under {here} rays to {direction}")
                ?? March.None;
        }
        return March.None;
    }

    /// <summary>
    /// What an objective seat is worth to the gun: the bolt it could lay from
    /// there, credited for a bearing more than one facing arms and charged for
    /// every launch ray beyond the first standing over it. Both corrections are
    /// identically zero where the contract declares no initial-aim offsets, so
    /// the seat this picks on the measured arms is revision 4's seat.
    /// </summary>
    private static double SeatValue(
        Field field,
        ShotSolver solver,
        (Direction Direction, Position Tile) seat,
        Traffic traffic) =>
        ReaimValue(field, solver, seat.Direction)
        + DiagonalPoseValue * Math.Max(0, field.ArmingFacings(seat.Tile) - 1)
        - ExtraLanePenalty * Math.Max(0, field.LanePressure(seat.Tile) - 1)
        - (traffic.SharesRay(seat.Tile) ? SharedRayPenalty : 0.0);

    private static GenericActorDecision? TryOrient(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        // Track the nearest body when nothing better has claimed the tick.
        // Facing is perception as well as aim: a contact that leaves the sight
        // quadrant stops arriving in the observation at all, and every shot
        // after that is fired at a memory.
        if (TryAimAtNearest(doctrine, field, context) is { } atEnemy)
            return atEnemy;

        // No contact: face the side the next body must arrive from, so the
        // opening shot of the next duel is already lined up.
        ImmutableArray<Position> ahead =
            doctrine.TilesAt(field.ActiveIndex + doctrine.AdvanceDelta);
        ImmutableArray<Position> anchor =
            ahead.IsEmpty ? field.Objective : ahead;
        return anchor.IsEmpty
            ? null
            : Turn(
                doctrine,
                field,
                context,
                Centroid(anchor),
                "watching the approach");
    }

    private static GenericActorDecision? TryAimAtNearest(
        Doctrine doctrine,
        Field field,
        GenericActorContext context)
    {
        GenericActorContext.ObservedEnemyState? nearest = context.Enemies
            .OrderBy(enemy => field.Self.ChebyshevDistance(enemy.Position))
            .ThenBy(enemy => enemy.ActorId)
            .FirstOrDefault();
        return nearest is null
            ? null
            : Turn(
                doctrine,
                field,
                context,
                nearest.Position,
                "tracking contact");
    }

    private static GenericActorDecision? Turn(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        Position target,
        string reason)
    {
        GenericActorActionLegality? rotate = Available(
            doctrine,
            context,
            GenericActorRulesContract.ActionKind.Rotation);
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (rotate is null || directions is null)
            return null;

        int dx = target.X - field.Self.X;
        int dy = target.Y - field.Self.Y;
        Direction horizontal = dx >= 0 ? Direction.East : Direction.West;
        Direction vertical = dy >= 0 ? Direction.South : Direction.North;
        Direction wanted;
        if (Math.Abs(dx) > Math.Abs(dy))
            wanted = horizontal;
        else if (Math.Abs(dy) > Math.Abs(dx))
            wanted = vertical;
        else
        {
            // Exactly diagonal, which is exactly the bearing both bracketing
            // facings arm once initial-aim offsets exist — and both keep the
            // contact on the quadrant's boundary, so both see it too. Choosing
            // the horizontal axis here, as revision 4 did, is a systematic side
            // preference on a mirror-symmetric map that both teams share and
            // therefore does not cancel. Break it on this life's own stream,
            // advance-first, exactly as every other tie in this doctrine.
            wanted = Array.IndexOf(field.Order, horizontal)
                <= Array.IndexOf(field.Order, vertical)
                ? horizontal
                : vertical;
        }
        if (wanted == field.Facing || !directions.AllowedValues.Contains(wanted))
            return null;
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(wanted)],
            $"{reason} — facing {wanted}");
    }

    private static Position Centroid(ImmutableArray<Position> tiles)
    {
        int x = 0;
        int y = 0;
        foreach (Position tile in tiles)
        {
            x += tile.X;
            y += tile.Y;
        }
        return new Position(x / tiles.Length, y / tiles.Length);
    }

    private static GenericActorActionLegality? Available(
        Doctrine doctrine,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind) =>
        AvailableAll(doctrine, context, kind).FirstOrDefault();

    private static List<GenericActorActionLegality> AvailableAll(
        Doctrine doctrine,
        GenericActorContext context,
        GenericActorRulesContract.ActionKind kind)
    {
        HashSet<string> ids = doctrine.Contract.Rules.Actions
            .Where(action => action.Kind == kind)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return context.ActionLegalities
            .Where(action => action.Available && ids.Contains(action.ActionId))
            .OrderBy(action => action.ActionCode)
            .ToList();
    }

    private static GenericActorDecision Safe(
        GenericActorContext context,
        string reason)
    {
        GenericActorActionLegality? fallback = context.ActionLegalities
            .Where(action => action.Available && action.Constraints.IsEmpty)
            .OrderBy(action => action.ActionCode)
            .FirstOrDefault()
            ?? context.ActionLegalities
                .Where(action => action.Available)
                .OrderBy(action => action.ActionCode)
                .FirstOrDefault()
            ?? context.ActionLegalities
                .OrderBy(action => action.ActionCode)
                .FirstOrDefault();
        return fallback is null
            ? GenericActorDecision.WithoutArguments("wait", 0, reason)
            : GenericActorDecision.WithoutArguments(
                fallback.ActionId,
                fallback.ActionCode,
                reason);
    }
}

/// <summary>
/// The step a tick would otherwise take, and whether that step is itself the
/// score. Fire is priced against this: only ground worth taking outbids a gun
/// that is ready now.
/// </summary>
internal readonly record struct March(
    GenericActorDecision? Decision,
    bool TakesGround,
    bool Mandatory = false)
{
    /// <summary>No step worth taking this tick.</summary>
    public static readonly March None = new(null, false);
}
