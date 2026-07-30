using BotArena.Sdk;

/// <summary>
/// When a fan beats the gun, and what to do once standing in one.
///
/// The stance is priced, not preferred. Read from the contract, a cast costs an
/// entry windup of wait-only ticks, buys exactly one launch — the return
/// counter is <c>attacks-issued-since-entering-source-form</c> at threshold one
/// — and leaves the stance gun's own cooldown behind. Over that whole window the
/// ordinary gun fires some number of times, and the cast has to beat it.
///
/// Revision 4 still granted the fan one thing the gun could not buy: BEARINGS.
/// A mobile gun whose shot program declared no initial aim offset left along one
/// cardinal and nothing else, so half the tiles at any distance were
/// unreachable and a diagonally adjacent body was unreachable at every
/// distance. Three rays diverging from tile one covered exactly what the gun
/// could not.
///
/// Revision 5 has to withdraw that, because the contract withdrew it. Where
/// <c>shotProgram</c> declares ±1 initial aim, the mobile gun launches along the
/// facing lane and both 45-degree neighbours — the fan's declared spread, lane
/// for lane. The fan's remaining product is those three lanes on ONE tick, and
/// per-body damage is capped, so simultaneity is worth nothing while the lanes
/// are aimed at one body: the gun can have any single lane this tick, at less
/// than half the cadence, without giving up the step. So the cast additionally
/// required more than one body under the rays, and refuses outright to feed a
/// raised arc, whose deflections all return to the tile a stance cannot leave.
///
/// Revision 7 does not withdraw that argument — it gives it the condition it
/// always had. Every sentence above turns on ONE premise: that a fan bolt and a
/// mobile bolt cost a body the same health. That premise is a contract field,
/// and on an arm that re-arms the fan it is false. Four declared numbers decide
/// the whole of this file, and all four are read rather than assumed:
///
/// <list type="bullet">
/// <item><c>projectile.damagePerHit</c> on the stance gun against the same
/// field on the mobile gun. Equal, and revision 5's refusal stands untouched.
/// Larger, and one landed fan reaches KILL THRESHOLDS the mobile gun does not,
/// which is the only place that difference is worth anything.</item>
/// <item>The entry route's <c>windup.durationTicks</c>. A one-tick entry is one
/// blind tick, so a cast is a reaction rather than a prediction, and the whole
/// pinned window is entry + launch + exit.</item>
/// <item>The stance gun's own <c>cooldownTicks</c> against the mobile gun's. At
/// or below it, this life walks out of the stance still holding its weapon —
/// which is what makes a fan an OPENER whose third point of damage arrives from
/// the ordinary gun a tick later.</item>
/// <item>The entry route's <c>cooldownTicks</c> — a route cooldown, held per
/// UNIT SLOT and surviving this body's death, whose live clock is published on
/// the observation. Frequency is priced there and nowhere else.</item>
/// </list>
///
/// Two further contract facts make the trade affordable rather than reckless.
/// The stance keeps objective weight one, so casting from the objective concedes
/// no ground at all — and under a decay clock that only erodes under enemy sole
/// presence, standing there contested holds the claim while the fan is aimed.
/// Nothing here reads a skill name, a class, or an arm; a contract with no
/// stance route produces no cast, and one whose fan is not heavier plays
/// revision 5's doctrine tick for tick.
/// </summary>
internal static class Cast
{
    // ---------------------------------------------------------------------
    // Revision 7 rule switches. THE SHIPPED BUILD HAS EVERY ONE TRUE. They
    // exist because coordination and doctrine do not decompose: the only
    // honest attribution for a rule is what removing it from the WORKING
    // WHOLE costs, so each number in DX.md comes from a build with exactly
    // one of these flipped and nothing else changed.
    // ---------------------------------------------------------------------

    /// <summary>R1 — the two-bodies requirement is contract-scoped, not absolute.</summary>
    private const bool BodiesGateIsContractScoped = true;

    /// <summary>R2 — read the entry clock and never request a held route.</summary>
    private const bool ReadEntryClock = true;

    /// <summary>R3 — credit the kill thresholds a heavier bolt moves.</summary>
    private const bool CreditKillThresholds = true;

    /// <summary>R4 — a marching tick is a price on the cast, not a refusal.</summary>
    private const bool MarchIsPriced = true;

    /// <summary>R5 — derive the exposure from the actual pin and the actual threats.</summary>
    private const bool PinDerivedSafety = true;

    // Two further rules were built, measured against this same whole, and are
    // NOT here. Both are in DX.md with their numbers.
    //
    // Scaling the fan's whole score by the damage ratio — "a landed volley
    // hits twice as hard", applied as a multiplier — cost 8 wins and 7.4
    // territorial points. The solver scores a launch as coverage times a
    // PRIORITY, and priority is a statement about ground: which body is on the
    // objective, which one is already hurt. Multiplying a positional weight by
    // a weapon's damage is a category error, and it double-counts the one
    // place the damage genuinely belongs — the kill thresholds in R3.
    //
    // Spending a stance the entry clock already paid for, rather than dropping
    // it when nothing is worth a fan, cost 9 wins and 2.99 points. The reasoning
    // was sound and the measurement disagreed: a body that will not walk out of
    // a form it cannot move in loses more ground than the wasted entry is worth.

    /// <summary>
    /// Expected value below which a fan is never worth the immobility. A cast
    /// that might miss everything is worse than a bolt that might.
    /// </summary>
    private const double CastFloor = 0.52;

    /// <summary>Margin the fan must beat the same window of ordinary fire by.</summary>
    private const double CastMargin = 1.10;

    /// <summary>
    /// Margin a cast must clear when the ticks are NOT free — when this body
    /// had a step worth taking and is standing still to fan instead. Revision 6
    /// refused that case outright, on a contract where the cast cost four
    /// immobile ticks and bought one ordinary contact. It costs three now and
    /// buys a heavier one, so the case stops being a refusal and becomes a
    /// price — which is how this doctrine prices every other tick.
    /// </summary>
    private const double MarchMargin = 1.55;

    /// <summary>
    /// Margin a cast clears when the fan would REMOVE a body outright. A
    /// removed body stops contesting the ground and stops answering, which is
    /// worth more than the window of fire it costs almost whenever it is real.
    /// </summary>
    private const double FinishMargin = 0.85;

    /// <summary>
    /// Bolt-equivalents credited for coverage of a body this fan removes and
    /// the mobile gun could not have. This is the whole product of a heavier
    /// bolt, so it is priced as more than the contact it rides on.
    /// </summary>
    private const double RemovalCredit = 1.25;

    /// <summary>
    /// Bolt-equivalents credited for coverage of a body the fan leaves inside
    /// the mobile gun's one-contact band. Only claimed where the contract
    /// leaves that gun in hand on the tick after the stance ends.
    /// </summary>
    private const double OpenerCredit = 0.55;

    /// <summary>
    /// Once committed, fire at anything this side of noise: squatting in a
    /// stance spends mobility for nothing, and the rule will not let a life
    /// stay past its budget anyway.
    /// </summary>
    private const double FireFloor = 0.20;

    /// <summary>Re-aiming inside the stance has to clearly beat firing now.</summary>
    private const double ReaimGain = 1.30;

    /// <summary>
    /// What a shot one cadence further out is worth relative to the shot in
    /// hand. The window of ordinary fire a cast gives up is not several copies
    /// of the best shot available now — that shot was picked from the current
    /// board, and every later one is fired at a board nobody has seen. The
    /// discount says so instead of pretending the cadence is the value.
    /// </summary>
    private const double LaterShot = 0.6;

    /// <summary>
    /// Enters a fan stance when the arithmetic says the bearings are worth the
    /// ticks, or <see langword="null"/> — which is every tick on a contract
    /// that declares no fan route at all.
    /// </summary>
    public static GenericActorDecision? TryEnter(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        ShotPlan? gun,
        March march,
        Advance advance)
    {
        if (field.InStance
            || doctrine.Skills.VolleyFrom(field.FormId)
                is not StanceRoute route
            || !solver.HasTargets)
        {
            return null;
        }

        // FREQUENCY IS PRICED ON THE ENTRY. The entry route may declare a
        // route cooldown, and this one does; it is held per UNIT SLOT, so it
        // survives this body's death and a life born inside the window has no
        // history to infer it from. The live clock is published on the
        // observation under the route's own transition ID, so it is read, and
        // requesting a held route only buys a Blocked tick.
        if (ReadEntryClock
            && ReadyAt(context, route.TransitionId) is int opens
            && context.Tick < opens)
        {
            return null;
        }

        // The gun has to be ready on the stance's first tick, or the windup
        // buys a stance that cannot shoot. Cooldown carries across the route
        // unchanged, so the test is arithmetic on declared values.
        if (field.Cooldown > route.WindupTicks)
            return null;

        // Wait-only for the entry windup, the launch tick, and the forced exit.
        // Nothing can be stepped out of in that window, so a bolt already
        // tracked toward this tile is a hit that has to be affordable.
        int pinned = route.WindupTicks + 1
            + (doctrine.Skills.ReturnFrom(route.TargetFormId)?.WindupTicks
                ?? 1);

        // Revision 6 refused outright whenever one worst-case contact could
        // kill this body. That reading was written against a contract where the
        // largest declared damage was one, so it fired essentially never — and
        // the salvo arm silently turned it into "no wounded body may ever
        // cast", because the fan itself is now the largest declared damage.
        // The rule was always a PROXY for a fan aimed at this tile, and that is
        // a thing the observation reports: an enemy inside a stance, or inside
        // the windup into one, publishes the form its gun will fire from and
        // cannot turn while it does. So ask the real question instead, and let
        // the tracked-threat test below carry the ordinary bolt.
        if (!PinDerivedSafety && field.Health <= doctrine.HardestHit)
            return null;
        if (field.ThreatAt(field.Self) is int arriving
            && arriving <= pinned
            && field.Health <= field.ThreatDamageAt(field.Self))
        {
            return null;
        }
        // A fan aimed at this tile is three rays, not one; standing inside one
        // while immobile is how a body dies mid-windup.
        if (field.InPredictedFan(field.Self))
            return null;

        // Ground first, always. The tick ledger this doctrine has always run
        // says a gun may only take a tick the ground did not want, and a cast
        // has to clear that bar once per pinned tick.
        //
        // Two refusals stay absolute, because both are ground THIS tick: a
        // capture this body is withholding on purpose, and a step that takes a
        // tile. Everything else is a matter of degree now. A stance that keeps
        // its objective weight holds the tile it stands on, so a cast from the
        // seat concedes nothing at all; a tick with no step worth taking was
        // already free; a target that cannot move at all is a certainty. What
        // revision 6 refused — an ordinary marching tick — is the case the
        // 1-tick entry changed, so it is charged a margin rather than declined.
        int weight = doctrine.FormFor(field.FormId)?.ObjectiveWeight ?? 1;
        bool holdsGround = route.ObjectiveWeight >= weight;
        bool seated = field.OnObjective && holdsGround;
        bool free = march.Decision is null;
        bool cheap = seated || free || Pinned(doctrine, field, context);
        if (advance.WithholdCompletion || march.TakesGround)
            return null;
        if (!MarchIsPriced && !cheap)
            return null;

        GenericActorActionLegality? transform = Action(
            context,
            route.ActionId);
        GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint?
            forms = transform?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .FormTargetConstraint>()
                .FirstOrDefault();
        if (transform is null
            || forms is null
            || !forms.AllowedFormIds.Contains(route.TargetFormId))
        {
            return null;
        }

        // The windup is ticks the board does not stop for, so a cast needs a
        // read that survives them. Two survive: a body that cannot move at all
        // — a published windup, or a stance form whose own action mask offers no
        // step — and a body close enough that three diverging rays still cover
        // where it can get to. Beyond that band a fan is aimed at a memory,
        // which is exactly how the first version of this code spent a stance on
        // an empty corridor.
        int band = Math.Max(2, route.MaxTravelTiles / 2);
        if (!InBand(doctrine, field, context, band))
            return null;

        // What the fan is worth, and what the same ticks of ordinary fire are
        // worth beside it. The stance may re-aim once it exists, so the best
        // reachable bearing counts — charged the extra tick that rotation costs.
        (FanReport report, _) = BestFan(
            doctrine,
            field,
            context,
            solver,
            route.TargetFormId,
            route.WindupTicks,
            field.Self);

        // A ray a raised arc eats is not a wasted ray, it is a returned one: it
        // relaunches from the shield's tile along the exactly reversed heading,
        // which is the line back to this tile. A stance cannot step, so fanning
        // a shield feeds it the whole break and takes the whole bill standing
        // still. The addendum is right that a fan breaks a shell — it is simply
        // not the caster who profits.
        if (report.Deflected > 0)
            return null;

        // What the two guns cost a body, declared. Everything below turns on the
        // COMPARISON between these two numbers rather than on either of them:
        // where they are equal the fan is a taxed bolt and revision 5's whole
        // refusal stands unchanged, and where the fan's is larger it is a
        // different weapon with different thresholds.
        int gunDamage = Math.Max(
            1,
            doctrine.AttackFor(field.FormId)?.Projectile.DamagePerHit ?? 1);
        int fanDamage = Math.Max(0, route.DamagePerHit);
        double fan = report.Value;

        // Revision 5's re-derivation, kept and given its condition. The fan's
        // declared spread is the facing lane and both 45-degree neighbours;
        // with ±1 initial aim the MOBILE gun launches along exactly those three
        // headings, one at a time, at less than half the cadence and without
        // giving up the step. WHERE THE TWO BOLTS COST A BODY THE SAME HEALTH
        // that leaves the fan selling simultaneity alone, and simultaneity is
        // worth nothing while every lane is aimed at one body, because coverage
        // is capped per body however many rays sweep it. Where the fan bolt is
        // heavier the premise is gone: one body under the rays is one body
        // taking double, which is an ordinary reason to cast. So the rule keeps
        // its shape and reads its condition off the contract instead of
        // outliving it.
        if (report.Bodies < 2
            && (!BodiesGateIsContractScoped || fanDamage <= gunDamage))
        {
            return null;
        }

        // THE KILL THRESHOLDS MOVED, and they are the reason to pick this
        // target over that one. A contact that removes a body the mobile gun
        // could not have removed is the heavier bolt's entire product. And a
        // contact that leaves a body inside the mobile gun's one-shot band is
        // worth nearly as much — but only because THE FAN NO LONGER TAXES THE
        // GUN: the stance gun's declared cadence is at most the mobile gun's,
        // so this life walks out of the stance with its own weapon in hand
        // rather than owing it several ticks. That is a contract read, and
        // where it is false the credit is simply not claimed.
        bool gunSurvivesTheCast = Math.Max(0, route.CooldownTicks)
            <= Math.Max(1, doctrine.AttackFor(field.FormId)?.CooldownTicks ?? 1);
        double finish = 0.0;
        if (CreditKillThresholds)
        {
            finish = RemovalCredit * report.Removes
                + (gunSurvivesTheCast ? OpenerCredit * report.Opens : 0.0);
        }
        double priced = fan + finish;
        if (priced < CastFloor)
            return null;

        // The window of ordinary fire the cast gives up: the ticks this body
        // is pinned, and the shots its own cadence would actually have fired
        // inside them from the cooldown it is standing on right now. Revision 6
        // approximated that with the STANCE gun's cadence, which was the right
        // number by accident on the arm it was measured against and is wrong
        // the moment the stance gun's cadence moves.
        int cadence = Math.Max(
            1,
            doctrine.AttackFor(field.FormId)?.CooldownTicks ?? 1);
        int shots = 0;
        for (int tick = Math.Max(0, field.Cooldown); tick < pinned; tick += cadence)
            shots++;
        double window = BestGun(field, solver, gun) * Discounted(Math.Max(1, shots));

        // The bar. A cast that finishes a body clears a low one, because a body
        // that is gone answers nothing; a cast taken instead of a step that was
        // worth taking clears a high one, because that is ground; everything
        // else clears revision 5's.
        double margin = CastMargin;
        if (CreditKillThresholds && report.Removes > 0.0)
            margin = FinishMargin;
        else if (MarchIsPriced && !cheap)
            margin = MarchMargin;
        if (priced <= window * margin)
            return null;

        return new GenericActorDecision(
            transform.ActionId,
            transform.ActionCode,
            [new GenericActorActionArgument.FormTargetArgument(
                route.TargetFormId)],
            $"casting {route.ProjectileCount} lanes at {fanDamage}"
            + $" ev={priced:0.00} over gun {window:0.00}x{margin:0.00}");
    }

    /// <summary>
    /// First tick the named route accepts a request again, or null when no
    /// clock is live for it. The field is published per unit slot and is absent
    /// while nothing is held, so an empty list means "open", never "unknown".
    /// </summary>
    private static int? ReadyAt(GenericActorContext context, string transitionId)
    {
        foreach (GenericActorContext.ObservedRouteCooldown clock
                 in context.Self.RouteCooldowns)
        {
            if (string.Equals(
                    clock.TransitionId,
                    transitionId,
                    StringComparison.Ordinal))
            {
                return clock.ReadyAtTick;
            }
        }
        return null;
    }

    /// <summary>
    /// Conduct inside a stance: aim it, fire it, or leave it. There is no exit
    /// to author for a spent budget — the engine starts that return itself —
    /// but leaving early is an ordinary decision, and a stance holding nothing
    /// worth firing at is exactly when to take it.
    /// </summary>
    public static GenericActorDecision? Conduct(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver)
    {
        if (field.SelfReturn is not ReturnRoute route)
            return null;

        GenericActorActionLegality? attack = context.ActionLegalities
            .Where(action =>
                action.Available
                && doctrine.Contract.Rules.Actions.Any(entry =>
                    entry.Kind
                        == GenericActorRulesContract.ActionKind.Attack
                    && string.Equals(
                        entry.Id,
                        action.ActionId,
                        StringComparison.Ordinal)))
            .OrderBy(action => action.ActionCode)
            .FirstOrDefault();

        double now = solver.Fan(field.FormId, field.Facing, 0, field.Self);
        (FanReport reaimed, Direction? bearing) = BestFan(
            doctrine,
            field,
            context,
            solver,
            field.FormId,
            delay: 1,
            field.Self,
            excludeCurrentFacing: true);
        double turned = reaimed.Value;

        if (attack is not null)
        {
            // A fan in hand beats a better fan next tick unless the difference
            // is large: the tick spent turning is a tick of standing still with
            // no weapon out.
            if (now >= FireFloor && now * ReaimGain >= turned)
            {
                return Fire(
                    attack,
                    $"fan away ev={now:0.00}");
            }
            if (bearing is Direction aim
                && turned >= FireFloor
                && Rotate(doctrine, context, aim, $"laying the fan {aim}")
                    is { } turn)
            {
                return turn;
            }
            if (now > 0.0)
                return Fire(attack, $"spending the stance ev={now:0.00}");
        }
        else if (bearing is Direction wanted
            && turned > now
            && Rotate(doctrine, context, wanted, $"laying the fan {wanted}")
                is { } aiming)
        {
            // Cooldown tick: turning is the only thing the stance can do that
            // costs nothing, so buy the bearing here.
            return aiming;
        }

        // Nothing worth a fan from any bearing: walk away rather than squat.
        // The budget is unspent, so this is an ordinary request, and the form is
        // immobile until it completes — which is the whole reason not to wait.
        //
        // The entry clock is an argument AGAINST this and it loses. Where the
        // route in declares a cooldown, that clock started the tick the entry
        // completed: this body is already inside the window, so leaving refunds
        // nothing and the wasted cast is already sunk. Spending the fan anyway
        // was built on exactly that reasoning and measured 9 wins worse — a body
        // that will not walk out of a form it cannot move in gives up more
        // ground than the entry was worth. Sunk is sunk; the tile is not.
        if (Math.Max(now, turned) < FireFloor)
        {
            GenericActorActionLegality? exit = Action(context, route.ActionId);
            if (exit is not null)
            {
                return GenericActorDecision.WithoutArguments(
                    exit.ActionId,
                    exit.ActionCode,
                    "nothing worth a fan — dropping the stance");
            }
        }
        return null;
    }

    /// <summary>
    /// True when some visible body is either inside the fan's useful band or
    /// unable to leave its tile at all. Both are contract reads: the band comes
    /// from the stance gun's own travel budget, and immobility from a published
    /// windup or a form whose action mask declares no movement.
    /// </summary>
    private static bool InBand(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        int band)
    {
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            if (field.Self.ChebyshevDistance(enemy.Position) <= band)
                return true;
            if (Pinned(doctrine, field, enemy))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when some visible body cannot leave its tile: a published windup due
    /// tick, or a form whose own action mask declares no movement. Every stance
    /// in this kit is the second kind, so an opponent that just cast something
    /// is a target that cannot answer a cast of its own.
    /// </summary>
    private static bool Pinned(
        Doctrine doctrine,
        Field field,
        GenericActorContext context) =>
        context.Enemies.Any(enemy => Pinned(doctrine, field, enemy));

    private static bool Pinned(
        Doctrine doctrine,
        Field field,
        GenericActorContext.ObservedEnemyState enemy)
    {
        if (field.FrozenUntil(enemy.ActorId) is not null)
            return true;
        GenericActorRulesContract.Form? form = doctrine.FormFor(enemy.FormId);
        if (form is null)
            return false;
        HashSet<string> movement = doctrine.Contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Movement)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        return !form.AllowedActionIds.Any(movement.Contains);
    }

    /// <summary>
    /// The ordinary gun's honest alternative: the best bolt this life could put
    /// out from any bearing it may legally take, not just the one it happens to
    /// face. Comparing a fan against the current facing alone is how a cast
    /// steals a tick that a rotation would have spent better.
    /// </summary>
    private static double BestGun(
        Field field,
        ShotSolver solver,
        ShotPlan? gun)
    {
        int wait = Math.Max(0, field.Cooldown);
        double best = Math.Max(
            gun?.Score ?? 0.0,
            solver.Forecast(field.Facing, wait));
        foreach (Direction direction in field.Order)
        {
            if (direction == field.Facing)
                continue;
            best = Math.Max(best, solver.Forecast(direction, wait + 1));
        }
        return best;
    }

    /// <summary>
    /// Value of <paramref name="shots"/> successive bolts relative to the first,
    /// each later one discounted because it is fired at a board nobody has seen.
    /// </summary>
    private static double Discounted(int shots)
    {
        double total = 0.0;
        double weight = 1.0;
        for (int index = 0; index < shots; index++)
        {
            total += weight;
            weight *= LaterShot;
        }
        return total;
    }

    /// <summary>
    /// Best fan value reachable from this tile and the bearing that produces
    /// it. Rotations are charged one extra tick of enemy movement, because that
    /// is what turning costs.
    /// </summary>
    private static (FanReport Report, Direction? Bearing) BestFan(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        string stanceFormId,
        int delay,
        Position origin,
        bool excludeCurrentFacing = false)
    {
        FanReport best = excludeCurrentFacing
            ? new FanReport(0.0, 0, 0, 0.0, 0.0)
            : solver.FanDetail(stanceFormId, field.Facing, delay, origin);
        Direction? bearing = null;

        // A stance can only re-aim where its own form declares rotation; read
        // the target form's action mask rather than assuming the stance turns.
        GenericActorRulesContract.Form? form =
            doctrine.FormFor(stanceFormId);
        HashSet<string> rotations = doctrine.Contract.Rules.Actions
            .Where(action =>
                action.Kind
                    == GenericActorRulesContract.ActionKind.Rotation)
            .Select(action => action.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (form is null
            || !form.AllowedActionIds.Any(rotations.Contains))
        {
            return (best, null);
        }

        foreach (Direction direction in field.Order)
        {
            if (direction == field.Facing)
                continue;
            FanReport candidate = solver.FanDetail(
                stanceFormId,
                direction,
                delay + 1,
                origin);
            if (candidate.Value > best.Value)
            {
                best = candidate;
                bearing = direction;
            }
        }
        return (best, bearing);
    }

    private static GenericActorDecision Fire(
        GenericActorActionLegality attack,
        string reason)
    {
        // The fan refuses programmed shots by construction — an attack profile
        // carrying a volley rejects a payload — and this action declares none,
        // so the launch is parameterless.
        GenericActorActionLegality.ArgumentConstraint.ShotProgramConstraint?
            program = attack.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint>()
                .FirstOrDefault();
        return program is { Allowed: true }
            ? new GenericActorDecision(
                attack.ActionId,
                attack.ActionCode,
                [new GenericActorActionArgument.ShotProgramArgument(
                    new ShotProgram(0, 0, 0, 1, 0))],
                reason)
            : GenericActorDecision.WithoutArguments(
                attack.ActionId,
                attack.ActionCode,
                reason);
    }

    private static GenericActorDecision? Rotate(
        Doctrine doctrine,
        GenericActorContext context,
        Direction wanted,
        string reason)
    {
        GenericActorActionLegality? rotate = context.ActionLegalities
            .Where(action =>
                action.Available
                && doctrine.Contract.Rules.Actions.Any(entry =>
                    entry.Kind
                        == GenericActorRulesContract.ActionKind.Rotation
                    && string.Equals(
                        entry.Id,
                        action.ActionId,
                        StringComparison.Ordinal)))
            .OrderBy(action => action.ActionCode)
            .FirstOrDefault();
        GenericActorActionLegality.ArgumentConstraint.DirectionConstraint?
            directions = rotate?.Constraints
                .OfType<GenericActorActionLegality.ArgumentConstraint
                    .DirectionConstraint>()
                .FirstOrDefault();
        if (rotate is null
            || directions is null
            || !directions.AllowedValues.Contains(wanted))
        {
            return null;
        }
        return new GenericActorDecision(
            rotate.ActionId,
            rotate.ActionCode,
            [new GenericActorActionArgument.DirectionArgument(wanted)],
            reason);
    }

    /// <summary>
    /// One available action resolved by its stable catalog ID. A stance route
    /// names the action it is requested with, so the route is the lookup key —
    /// never the lowest action code that happens to share a kind, which stops
    /// being unambiguous the moment a contract declares two same-life routes.
    /// </summary>
    private static GenericActorActionLegality? Action(
        GenericActorContext context,
        string actionId) =>
        context.ActionLegalities.FirstOrDefault(action =>
            action.Available
            && string.Equals(
                action.ActionId,
                actionId,
                StringComparison.Ordinal));
}
