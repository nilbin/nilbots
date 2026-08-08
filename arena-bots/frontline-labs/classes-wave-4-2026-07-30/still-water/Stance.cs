using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// One reversible same-life stance and everything the contract says about what
/// entering it costs and what it buys. Nothing here names a skill, a class, or a
/// form: a stance is any same-life route whose return route carries an
/// <c>automaticReturn</c> budget, or which is reversible by a parameterless
/// route back to the form we left. The interesting properties — a multi-bolt
/// gun, a projectile guard, whether the body can still move — are read off the
/// target form.
/// </summary>
internal sealed class StanceRoute
{
    public StanceRoute(
        GenericActorRulesContract.FormTransition entry,
        GenericActorRulesContract.FormTransition? exit,
        GenericActorRulesContract.Form target,
        GenericActorRulesContract.AttackProfile? gun,
        bool canMove,
        bool canRotate,
        bool canAttack)
    {
        Entry = entry;
        Exit = exit;
        Target = target;
        Gun = gun;
        CanMove = canMove;
        CanRotate = canRotate;
        CanAttack = canAttack;
    }

    public GenericActorRulesContract.FormTransition Entry { get; }

    /// <summary>The route back out, when the contract declares one.</summary>
    public GenericActorRulesContract.FormTransition? Exit { get; }

    public GenericActorRulesContract.Form Target { get; }

    /// <summary>The stance's own attack profile, which need not be the mobile gun.</summary>
    public GenericActorRulesContract.AttackProfile? Gun { get; }

    public bool CanMove { get; }

    public bool CanRotate { get; }

    public bool CanAttack { get; }

    public string TargetFormId => Target.Id;

    public string SourceFormId => Entry.SourceFormId;

    /// <summary>Ticks between requesting the stance and wearing it.</summary>
    public int EntryWindup => Math.Max(1, Entry.Windup.DurationTicks);

    /// <summary>Ticks the return route spends before the body is mobile again.</summary>
    public int ExitWindup => Math.Max(0, Exit?.Windup.DurationTicks ?? 0);

    /// <summary>
    /// True when the windup forbids everything but waiting, so the entry is a
    /// blind commitment: no dodge, no shot, and lethal damage cancels it.
    /// </summary>
    public bool BlindWindup => Entry.Windup.PendingAction.Contains(
        "wait-only",
        StringComparison.Ordinal);

    public bool LethalDamageCancels => Entry.Windup.LethalDamage.Contains(
        "cancel",
        StringComparison.Ordinal);

    /// <summary>Tile tags that refuse the stance at queue time and at completion.</summary>
    public ImmutableArray<GenericActorMapContract.TileTagKind> ForbiddenTags =>
        Entry.Placement.ForbiddenTileTags;

    /// <summary>
    /// How many of the stance's own attacks the engine allows before it takes
    /// the body out again, or <see langword="null"/> when the stance carries no
    /// attack budget. The counter is declared on the RETURN route, and an
    /// absent property means the engine never fires the route by itself.
    /// </summary>
    public int? AttackBudget =>
        Exit?.AutomaticReturn is { } trigger
        && trigger.Counter.Contains(
            "attacks-issued",
            StringComparison.Ordinal)
            ? Math.Max(1, trigger.Threshold)
            : null;

    /// <summary>
    /// Deflections the stance survives before the engine forces it out, or
    /// <see langword="null"/> when its budget is not counted in deflections.
    /// </summary>
    public int? DeflectionBudget =>
        Exit?.AutomaticReturn is { } trigger
        && trigger.Counter.Contains(
            "projectiles-deflected",
            StringComparison.Ordinal)
            ? Math.Max(1, trigger.Threshold)
            : null;

    /// <summary>Bolts one accepted attack from this stance launches.</summary>
    public int BoltsPerAttack => Gun?.ProjectilesPerAttack ?? 0;

    /// <summary>
    /// Heading offsets, in 45-degree sectors, of the bolts one attack launches.
    /// Derived from the declared count and spread policy, never from the number
    /// three: a symmetric adjacent fan of <c>n</c> bolts is the n offsets
    /// centred on the aim heading. A spread this code does not recognise
    /// degrades to the single aimed bolt, which is always correct about at
    /// least one of the bolts.
    /// </summary>
    public ImmutableArray<int> FanOffsets
    {
        get
        {
            GenericActorRulesContract.AttackVolley? volley = Gun?.Volley;
            if (volley is null || volley.ProjectileCount <= 1)
                return [0];
            if (!volley.Spread.Contains(
                    "adjacent-heading-fan",
                    StringComparison.Ordinal))
            {
                return [0];
            }
            int count = Math.Min(8, volley.ProjectileCount);
            var offsets = ImmutableArray.CreateBuilder<int>(count);
            int half = (count - 1) / 2;
            for (int index = 0; index < count; index++)
                offsets.Add(index - half);
            return offsets.ToImmutable();
        }
    }

    /// <summary>
    /// Total ticks one cast takes out of this body's life: the windup, the tick
    /// that fires, the exit windup, and the cooldown the stance gun leaves
    /// behind (cooldown survives the return, because the declared continuity is
    /// to preserve remaining ticks).
    /// </summary>
    public int CastTempoTicks =>
        EntryWindup + 1 + ExitWindup + (Gun?.CooldownTicks ?? 0);
}

/// <summary>
/// The stance routes reachable from each form, indexed by source form, plus the
/// parameterless way back. Built once per life from the contract.
/// </summary>
internal sealed class StanceBook
{
    private readonly Dictionary<string, List<StanceRoute>> _bySource;
    private readonly Dictionary<string, StanceRoute> _byTarget;

    public StanceBook(
        GenericActorResolvedMatchContract contract,
        Func<string, GenericActorRulesContract.Form?> form,
        Func<string, GenericActorRulesContract.AttackProfile?> attack,
        Func<string, GenericActorRulesContract.ActionKind?> actionKind,
        Func<string, bool> actionTakesFormTarget,
        Func<string, bool> actionIsParameterless,
        Func<string, bool> isCreationForm)
    {
        _bySource = new Dictionary<string, List<StanceRoute>>(StringComparer.Ordinal);
        _byTarget = new Dictionary<string, StanceRoute>(StringComparer.Ordinal);

        var transitions = contract.Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .ToList();

        foreach (var entry in transitions)
        {
            if (form(entry.TargetFormId) is not { } target)
                continue;
            // A stance is a route we can come back from. Anything one-way is an
            // Anchor-shaped commitment and is not this file's business.
            var exit = transitions.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.SourceFormId,
                    entry.TargetFormId,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.TargetFormId,
                    entry.SourceFormId,
                    StringComparison.Ordinal));
            if (entry.IrreversibleForLife || exit is null)
                continue;

            // A reversible pair is two routes, and only one of them ENTERS the
            // stance. Three contract facts say which: the entry is requested by
            // naming a target form, the return is the parameterless route, and no
            // life is ever created already wearing a stance. Reading only
            // "reversible" makes every ordinary mobile form look like a stance —
            // which cost this revision a match where the prime stood still for
            // seventy-two ticks believing it was already casting.
            if (!actionTakesFormTarget(entry.ActionId)
                || !actionIsParameterless(exit.ActionId)
                || isCreationForm(entry.TargetFormId))
            {
                continue;
            }

            bool canMove = false;
            bool canRotate = false;
            bool canAttack = false;
            foreach (string actionId in target.AllowedActionIds)
            {
                switch (actionKind(actionId))
                {
                    case GenericActorRulesContract.ActionKind.Movement:
                        canMove = true;
                        break;
                    case GenericActorRulesContract.ActionKind.Rotation:
                        canRotate = true;
                        break;
                    case GenericActorRulesContract.ActionKind.Attack:
                        canAttack = true;
                        break;
                }
            }

            var route = new StanceRoute(
                entry,
                exit,
                target,
                target.AttackProfileId is string profileId
                    ? attack(profileId)
                    : null,
                canMove,
                canRotate,
                canAttack);

            if (!_bySource.TryGetValue(entry.SourceFormId, out var list))
            {
                list = [];
                _bySource[entry.SourceFormId] = list;
            }
            list.Add(route);
            _byTarget[entry.TargetFormId] = route;
        }

        foreach (var list in _bySource.Values)
        {
            list.Sort((left, right) => string.CompareOrdinal(
                left.TargetFormId,
                right.TargetFormId));
        }
    }

    /// <summary>Whether this contract offers any reversible stance at all.</summary>
    public bool Any => _bySource.Count > 0;

    /// <summary>Stances reachable from a form, best gun first.</summary>
    public IReadOnlyList<StanceRoute> From(string formId) =>
        _bySource.TryGetValue(formId, out var list) ? list : [];

    /// <summary>The route this form is the stance of, if it is one.</summary>
    public StanceRoute? Wearing(string formId) =>
        _byTarget.TryGetValue(formId, out var route) ? route : null;

    /// <summary>
    /// The best multi-bolt stance reachable from a form: the one whose single
    /// attack launches the most bolts. Null when no stance from here shoots
    /// more than the mobile gun already does.
    /// </summary>
    public StanceRoute? BestFan(string formId, int mobileBolts)
    {
        StanceRoute? best = null;
        foreach (StanceRoute route in From(formId))
        {
            if (!route.CanAttack || route.BoltsPerAttack <= mobileBolts)
                continue;
            if (best is null || route.BoltsPerAttack > best.BoltsPerAttack)
                best = route;
        }
        return best;
    }
}

/// <summary>One candidate cast: the facing to wear and what the fan is worth.</summary>
/// <param name="Facing">Facing the fan is aimed along.</param>
/// <param name="Value">Summed per-ray peak prediction weight.</param>
/// <param name="CoveredObjectiveTiles">Active-objective tiles the rays cross.</param>
/// <param name="AnchoredRays">Rays arriving on a named prediction, not a guess.</param>
/// <param name="DistinctBodies">
/// Separate enemy bodies the fan can reach at once. This is the number that
/// justifies a fan at all: the bolts are simultaneous, so two bodies inside the
/// cone are two bodies one decision answers, while one body is a coverage bet a
/// cheaper single bolt could have made.
/// </param>
internal readonly record struct CastPlan(
    Direction Facing,
    double Value,
    int CoveredObjectiveTiles,
    int AnchoredRays,
    int DistinctBodies);

/// <summary>
/// Fan geometry and cast valuation. A fan is <c>n</c> simultaneous straight
/// bolts on adjacent headings, so its worth is the SUM of the per-ray peaks
/// rather than the best ray: every bolt can hit something of its own. That is
/// also exactly why it is the answer to the one hole in a one-bend envelope —
/// a single bend can never reach an exact diagonal (there is no dominant axis
/// to bend from), and the fan's outer rays are exactly those diagonals.
/// </summary>
internal static class Volley
{
    /// <summary>Tiles each bolt of the fan sweeps, with travel and tick offset.</summary>
    public static List<List<(Position Tile, int Travel, int Offset)>> Rays(
        Field field,
        Position origin,
        Direction facing,
        StanceRoute route)
    {
        var rays = new List<List<(Position, int, int)>>();
        if (route.Gun is not { } gun)
            return rays;
        ProjectileHeading aim = facing.ToProjectileHeading();
        int maxTiles = gun.Projectile.MaxTravelTiles;
        bool strict = gun.Projectile.DiagonalCornersMustBeClear;

        foreach (int offset in route.FanOffsets)
        {
            ProjectileHeading heading = aim.Turned(offset);
            (int dx, int dy) = heading.Vector();
            var swept = new List<(Position, int, int)>();
            Position cursor = origin;
            for (int step = 1; step <= maxTiles; step++)
            {
                Position next = cursor.Offset(dx, dy);
                if (field.IsWall(next))
                    break;
                if (strict
                    && dx != 0
                    && dy != 0
                    && (field.IsWall(cursor.Offset(dx, 0))
                        || field.IsWall(cursor.Offset(0, dy))))
                {
                    break;
                }
                swept.Add((next, step, ForkPlanner.ImpactOffset(gun, step)));
                cursor = next;
            }
            if (swept.Count > 0)
                rays.Add(swept);
        }
        return rays;
    }

    /// <summary>
    /// What a fan fired from this pose after <paramref name="delay"/> ticks is
    /// worth against the current forecasts, plus how much of the contested
    /// objective it seals. The objective term is the whole point on this
    /// contract: an opponent has to STAND on those tiles to take them, and
    /// three bolts across the front rank deny a tile each rather than betting
    /// one bolt on one guess.
    /// </summary>
    public static CastPlan Score(
        Field field,
        Position origin,
        Direction facing,
        StanceRoute route,
        List<EnemyForecast> forecasts,
        HashSet<Position> objective,
        int delay)
    {
        double value = 0;
        int anchored = 0;
        var sealedTiles = new HashSet<Position>();
        var bodies = new HashSet<ActorIdentity>();
        int damage = route.Gun?.Projectile.DamagePerHit ?? 1;

        foreach (var ray in Rays(field, origin, facing, route))
        {
            double peak = 0;
            bool rayAnchored = false;
            EnemyForecast? rayBody = null;
            foreach ((Position tile, int _, int offset) in ray)
            {
                if (objective.Contains(tile))
                    sealedTiles.Add(tile);
                int steps = delay + offset + 1;
                foreach (EnemyForecast forecast in forecasts)
                {
                    int weight = forecast.Weight(tile, steps);
                    if (weight <= 0)
                        continue;
                    double scaled = weight * forecast.Pressure(damage);
                    if (scaled > peak)
                    {
                        peak = scaled;
                        rayAnchored = forecast.IsAnchored(tile, steps);
                        rayBody = rayAnchored ? forecast : null;
                    }
                }
            }
            value += peak;
            if (rayAnchored)
                anchored++;
            if (rayBody is not null)
                bodies.Add(rayBody.State.ActorId);
        }
        return new CastPlan(
            facing,
            value,
            sealedTiles.Count,
            anchored,
            bodies.Count);
    }
}
