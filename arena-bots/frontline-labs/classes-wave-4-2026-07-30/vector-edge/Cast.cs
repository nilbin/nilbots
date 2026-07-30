using BotArena.Sdk;

/// <summary>
/// When a fan beats the gun, and what to do once standing in one.
///
/// The stance is priced, not preferred. Read from the contract, a cast costs an
/// entry windup of wait-only ticks, buys exactly one launch — the return
/// counter is <c>attacks-issued-since-entering-source-form</c> at threshold one
/// — and leaves the stance gun's own cooldown behind, which on this chassis is
/// more than double the mobile gun's. Over that whole window the ordinary gun
/// fires several times. So a fan has to be worth several bolts, and against a
/// target the ordinary gun can already reach it never is.
///
/// What it is for is the case revision 2 measured and could not answer: a
/// target that steps off every straight bolt. The mobile gun's shot program
/// declares no initial aim offset and no bend before the first tile, so a
/// striker's bolt leaves along one cardinal and nothing else — half the tiles
/// at any distance are permanently unreachable, and a diagonally adjacent body
/// is unreachable at every distance. The fan's rays diverge from tile one, so
/// they cover exactly the bearings the gun cannot, and they cover three of them
/// at once. That is the trade: mobility and cadence for bearings.
///
/// Two contract facts make the trade affordable rather than reckless. The
/// stance keeps objective weight one, so casting from the objective concedes no
/// ground at all — and under a decay clock that only erodes under enemy sole
/// presence, standing there contested holds the claim while the fan is aimed.
/// Nothing here reads a skill name, a class, or an arm; a contract with no
/// stance route produces no cast.
/// </summary>
internal static class Cast
{
    /// <summary>
    /// Expected value below which a fan is never worth the immobility. A cast
    /// that might miss everything is worse than a bolt that might.
    /// </summary>
    private const double CastFloor = 0.52;

    /// <summary>Margin the fan must beat the same window of ordinary fire by.</summary>
    private const double CastMargin = 1.10;

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

        // The gun has to be ready on the stance's first tick, or the windup
        // buys a stance that cannot shoot. Cooldown carries across the route
        // unchanged, so the test is arithmetic on declared values.
        if (field.Cooldown > route.WindupTicks)
            return null;

        // Wait-only for the entry windup, the launch tick, and the forced exit.
        // Nothing can be stepped out of in that window, so a bolt already
        // tracked toward this tile is a hit that has to be affordable — and so
        // is a bolt nobody has fired yet. A body that one contact kills has no
        // business entering a form it cannot dodge in, whatever the board looks
        // like on the tick it enters; the price of a contact is the contract's
        // own largest declared damage, not a guess.
        int pinned = route.WindupTicks + 1
            + (doctrine.Skills.ReturnFrom(route.TargetFormId)?.WindupTicks
                ?? 1);
        if (field.Health <= doctrine.HardestHit)
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

        // Ground first, always — and for a stance that is a harder test than a
        // shot, because a shot spends one tick and this spends four. The tick
        // ledger this doctrine has always run says a gun may only take a tick
        // the ground did not want; a cast has to clear the same bar four times
        // over, so it is allowed exactly where those ticks were not going to buy
        // ground anyway.
        //
        // Three such places, all read rather than assumed. A stance that keeps
        // its objective weight holds the tile it stands on, so a cast from the
        // seat concedes nothing at all — under a decay clock that erodes only
        // under enemy sole presence, standing there contested is the claim
        // preserved. A tick with no step worth taking was already free. And a
        // target that cannot move at all is a certainty, which is worth walking
        // for. Everything else is ground not taken, which is what the first
        // measured version of this code spent and lost.
        int weight = doctrine.FormFor(field.FormId)?.ObjectiveWeight ?? 1;
        bool holdsGround = route.ObjectiveWeight >= weight;
        bool seated = field.OnObjective && holdsGround;
        bool free = march.Decision is null;
        if (advance.WithholdCompletion
            || march.TakesGround
            || !seated && !free && !Pinned(doctrine, field, context))
        {
            return null;
        }

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
        (double fan, _) = BestFan(
            doctrine,
            field,
            context,
            solver,
            route.TargetFormId,
            route.WindupTicks,
            field.Self);
        if (fan < CastFloor)
            return null;

        int cadence = Math.Max(
            1,
            doctrine.AttackFor(field.FormId)?.CooldownTicks ?? 1);
        int horizon = route.WindupTicks + 1 + Math.Max(0, route.CooldownTicks);
        int shots = Math.Max(1, horizon / cadence);
        double window = BestGun(field, solver, gun) * Discounted(shots);
        if (fan <= window * CastMargin)
            return null;

        return new GenericActorDecision(
            transform.ActionId,
            transform.ActionCode,
            [new GenericActorActionArgument.FormTargetArgument(
                route.TargetFormId)],
            $"casting {route.ProjectileCount} lanes ev={fan:0.00}"
            + $" over gun {window:0.00}");
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
        (double turned, Direction? bearing) = BestFan(
            doctrine,
            field,
            context,
            solver,
            field.FormId,
            delay: 1,
            field.Self,
            excludeCurrentFacing: true);

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
    private static (double Value, Direction? Bearing) BestFan(
        Doctrine doctrine,
        Field field,
        GenericActorContext context,
        ShotSolver solver,
        string stanceFormId,
        int delay,
        Position origin,
        bool excludeCurrentFacing = false)
    {
        double best = excludeCurrentFacing
            ? 0.0
            : solver.Fan(stanceFormId, field.Facing, delay, origin);
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
            double value = solver.Fan(
                stanceFormId,
                direction,
                delay + 1,
                origin);
            if (value > best)
            {
                best = value;
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
