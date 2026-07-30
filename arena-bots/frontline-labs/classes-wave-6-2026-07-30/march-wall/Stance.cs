using BotArena.Sdk;

/// <summary>
/// The stance layer: same-life routes into a form that trades one capability
/// for another and leaves on a declared budget. Two exist in the skill kit and
/// this reader knows neither by name — a stance is recognized by what its
/// TARGET FORM declares:
///
///  - a <c>projectileGuard</c> is a shield: contacts arriving inside the
///    guarded arc die and are relaunched from the guard's tile along the
///    exactly reversed heading under the guard's own team. The form carries no
///    attack profile, cannot move and cannot rotate, so the arc is chosen
///    before it rises.
///  - a <c>volley</c> on the target form's attack profile is a fan: one attack
///    launches several bolts on adjacent headings.
///
/// Both leave by themselves. The route BACK carries
/// <c>automaticReturn { counter, threshold }</c>, which is the budget as a
/// rule rather than a convention: the engine starts the return the tick the
/// counter reaches the threshold, and the counter is scoped to one entry. The
/// property is absent on every route the engine never fires by itself, so a
/// missing budget means "this stance has no budget" rather than "unknown".
///
/// Nothing here names a class, a form, or a transition. On an arm that
/// declares no stance at all every reader returns null and the doctrine falls
/// through to the mobile play it had before.
/// </summary>
internal static class Stance
{
    /// <summary>
    /// The declared route from this form into a projectile-guarding form, or
    /// null when the contract offers none. Recognized by the target form's
    /// declared guard rather than by any transition ID.
    /// </summary>
    public static GenericActorRulesContract.FormTransition? GuardRoute(
        ContractView view,
        string formId) =>
        view.Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Where(route =>
                string.Equals(
                    route.SourceFormId,
                    formId,
                    StringComparison.Ordinal)
                && view.HasGuard(route.TargetFormId))
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The declared route from this form into a form whose gun fires more than
    /// one projectile per attack. Absent for every chassis that does not own
    /// the fan; the doctrine below is written once and simply never fires
    /// there.
    /// </summary>
    public static GenericActorRulesContract.FormTransition? VolleyRoute(
        ContractView view,
        string formId) =>
        view.Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Where(route =>
                string.Equals(
                    route.SourceFormId,
                    formId,
                    StringComparison.Ordinal)
                && view.ProjectilesPerAttack(route.TargetFormId) > 1)
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// The route out of the form this body currently holds. It is the same
    /// parameterless return the engine fires when the budget runs out, so
    /// leaving early is an ordinary decision on the same route.
    /// </summary>
    public static GenericActorRulesContract.FormTransition? ReturnRoute(
        ContractView view,
        string formId) =>
        view.Rules.SameLifeTransitions
            .OfType<GenericActorRulesContract.FormTransition>()
            .Where(route =>
                string.Equals(
                    route.SourceFormId,
                    formId,
                    StringComparison.Ordinal)
                && view.IsMobile(route.TargetFormId)
                && !view.HasGuard(route.TargetFormId)
                && view.ProjectilesPerAttack(route.TargetFormId) <= 1)
            .OrderBy(route => route.TransitionId, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>
    /// How many counted events this stance is worth before the engine returns
    /// the body itself, or null when the route declares no budget at all.
    /// </summary>
    public static int? Budget(
        GenericActorRulesContract.FormTransition? route) =>
        route?.AutomaticReturn?.Threshold;

    /// <summary>
    /// True when the declared budget counts DEFLECTIONS rather than attacks.
    /// Read from the counter's semantic ID: a shield spends its life stopping
    /// bolts and a fan spends its life firing them, and a doctrine that
    /// confuses the two mis-times both exits.
    /// </summary>
    public static bool BudgetCountsDeflections(
        GenericActorRulesContract.FormTransition? route) =>
        route?.AutomaticReturn?.Counter.Contains(
            "deflected",
            StringComparison.Ordinal)
        ?? false;

    /// <summary>
    /// Whether a bolt travelling <paramref name="travel"/> arrives inside the
    /// arc a body facing <paramref name="facing"/> guards. A quadrant is 90
    /// degrees: the bearing the bolt CAME FROM — the reverse of its travel
    /// heading — must be within one 45-degree sector of the facing. So a shell
    /// facing east turns everything arriving from the east, north-east and
    /// south-east, and is hurt normally by the four remaining bearings.
    ///
    /// <para>This predicate is the one piece of the guard the contract states
    /// only as a policy ID (<c>facing-quadrant-contacts-deflected</c>), so it
    /// was verified against the published <c>projectile-deflected</c> events
    /// and against the damage events a shell takes: every observed deflection
    /// satisfies it and every bolt that damaged a shell violates it.</para>
    /// </summary>
    public static bool GuardsAgainst(Direction facing, ProjectileHeading travel)
    {
        int arc = (int)facing.ToProjectileHeading();
        int from = ((int)travel + 4) % 8;
        int delta = Math.Abs(arc - from);
        return Math.Min(delta, 8 - delta) <= 1;
    }

    /// <summary>
    /// True when a body of this form, facing this way, would turn a bolt
    /// travelling <paramref name="travel"/>. False for every unguarded form,
    /// which is every form on an arm without the kit.
    /// </summary>
    public static bool Deflects(
        ContractView view,
        string formId,
        Direction facing,
        ProjectileHeading travel) =>
        view.HasGuard(formId) && GuardsAgainst(facing, travel);

    /// <summary>
    /// The heading a returned bolt would fly: the exact reverse of what
    /// arrived. Used to price poking an arc — the bolt comes back down the
    /// lane it went out on, from the guard's tile rather than ours.
    /// </summary>
    public static ProjectileHeading Reversed(ProjectileHeading heading) =>
        (ProjectileHeading)(((int)heading + 4) % 8);
}
