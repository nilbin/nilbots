using BotArena.Sdk;

/// <summary>
/// What a same-life round trip COSTS. Revisions 1–4 treated fortification as a
/// one-way door, because it was one: the anchor route declared
/// <c>irreversibleForLife</c> and paid a flat entry heal, so the only questions
/// were whether to spend the body and where. Neither is true any more, and both
/// facts are declared rather than announced:
///
///  - <c>irreversibleForLife</c> is FALSE on the routes in both directions, so
///    anchor ⇄ mobilize is a cycle a life may ride as often as it likes;
///  - the health policy is a RATIO with a floor and no flat gain, so the two
///    maxima decide what the trip costs. Full health maps onto full health in
///    both directions and costs nothing; a wounded body loses the remainder to
///    the floor every time it comes back.
///
/// So the doctrine needs one number the contract does not publish directly:
/// the health a body would hold after going out and coming back. That is
/// <see cref="RoundTripCost"/>, and it is computed from the declared policy IDs
/// rather than from the 4/4 ⇄ 7/7 table, because the table is this arm's and
/// the policy is the rule.
///
/// <para>Nothing here names a form, a class or an arm. A contract whose routes
/// are irreversible reports exactly that, and every caller falls back to the
/// one-way doctrine it had before.</para>
/// </summary>
internal static class Cycle
{
    /// <summary>
    /// True when the declared route leaves the return leg open for this life.
    /// The old rule — one anchor per life, forever — is a value of this field,
    /// not a property of turrets.
    /// </summary>
    public static bool Reversible(
        GenericActorRulesContract.FormTransition? route) =>
        route is { IrreversibleForLife: false };

    /// <summary>
    /// Health this body would hold on the far side of one declared transition,
    /// resolved from the route's own health policy. Three policies exist in the
    /// catalog and each is recognized by what it says rather than by which arm
    /// declared it:
    ///
    ///  - a RATIO policy re-expresses the same fraction against the target's
    ///    maximum and floors the result at one, so a transform never kills;
    ///  - a FLAT gain adds its declared delta and clamps (the historical entry
    ///    heal, still the right read on a contract that declares it);
    ///  - anything else preserves the current value under the target's cap.
    /// </summary>
    public static int HealthAfter(
        ContractView view,
        GenericActorRulesContract.FormTransition route,
        int current)
    {
        int sourceMax = Math.Max(1, view.MaxHealth(route.SourceFormId));
        int targetMax = Math.Max(1, view.MaxHealth(route.TargetFormId));
        int health = Math.Max(0, current);

        GenericActorRulesContract.SameLifeHealth policy = route.Health;
        if (policy.Policy.Contains("preserve-ratio", StringComparison.Ordinal))
        {
            // The declared formula is floor(current * targetMax / sourceMax)
            // with a minimum of one. Integer division truncates toward zero,
            // which for non-negative health is exactly that floor.
            int mapped = health * targetMax / sourceMax;
            return Math.Clamp(Math.Max(1, mapped), 1, targetMax);
        }
        if (policy.FlatHealthGain != 0)
            return Math.Clamp(health + policy.FlatHealthGain, 1, targetMax);
        return Math.Clamp(health, 0, targetMax);
    }

    /// <summary>
    /// Health left after going out and coming back once, or null when either leg
    /// is missing — an absent route is not a cheap trip, it is no trip.
    /// </summary>
    public static int? RoundTripHealth(
        ContractView view,
        GenericActorRulesContract.FormTransition? outbound,
        GenericActorRulesContract.FormTransition? inbound,
        int current)
    {
        if (outbound is null || inbound is null)
            return null;
        return HealthAfter(view, inbound, HealthAfter(view, outbound, current));
    }

    /// <summary>
    /// Health one full cycle costs from this exact health, or null when the trip
    /// is not available. Zero is the interesting value: it means the wall can
    /// fortify and un-fortify for nothing but the two windups, which is the
    /// whole economic case for treating the turret as a posture instead of a
    /// destination.
    /// </summary>
    public static int? RoundTripCost(
        ContractView view,
        GenericActorRulesContract.FormTransition? outbound,
        GenericActorRulesContract.FormTransition? inbound,
        int current) =>
        RoundTripHealth(view, outbound, inbound, current) is int after
            ? Math.Max(0, current - after)
            : null;

    /// <summary>
    /// True when the wall may treat fortifying as a posture: the return leg is
    /// declared open and the round trip is free at this exact health. Cycling
    /// while wounded is what the floor punishes, so the predicate is asked of
    /// the health we hold and not of the health we started the match with.
    /// </summary>
    public static bool FreeAt(
        ContractView view,
        GenericActorRulesContract.FormTransition? outbound,
        GenericActorRulesContract.FormTransition? inbound,
        int current) =>
        Reversible(outbound)
        && Reversible(inbound)
        && RoundTripCost(view, outbound, inbound, current) == 0;

    /// <summary>
    /// Ticks a full cycle spends in windup. Both legs are commitments the
    /// opponent can read from the contract before tick zero, and they are the
    /// real price of the cycle now that health is not.
    /// </summary>
    public static int WindupCost(
        GenericActorRulesContract.FormTransition? outbound,
        GenericActorRulesContract.FormTransition? inbound) =>
        Math.Max(1, outbound?.Windup.DurationTicks ?? 1)
        + Math.Max(1, inbound?.Windup.DurationTicks ?? 1);

    /// <summary>
    /// True when the target form's gun strictly beats the source form's on the
    /// two numbers a stationary duel is decided by — how often it fires and how
    /// far it reaches — or gains absolute aim freedom. This is why the cycle is
    /// worth riding at all, and it is read from the two attack profiles rather
    /// than from the knowledge that turrets are good.
    /// </summary>
    public static bool GunImproves(
        ContractView view,
        string sourceFormId,
        string targetFormId)
    {
        GenericActorRulesContract.AttackProfile? mine = view.Attack(sourceFormId);
        GenericActorRulesContract.AttackProfile? theirs =
            view.Attack(targetFormId);
        if (theirs is null)
            return false;
        if (mine is null)
            return true;
        if (theirs.CooldownTicks < mine.CooldownTicks)
            return true;
        if (theirs.Projectile.MaxTravelTiles > mine.Projectile.MaxTravelTiles)
            return true;
        return theirs.OmnidirectionalAim && !mine.OmnidirectionalAim;
    }
}
