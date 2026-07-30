using System.Collections.Immutable;
using BotArena.Sdk;

/// <summary>
/// The class-skill kit as this contract declares it, resolved once.
///
/// Nothing here names a skill, a class, or an arm. A stance is recognized by
/// its shape: a same-life route into a form whose attack profile declares a
/// multi-projectile <c>volley</c>, or into a form whose <c>projectileGuard</c>
/// deflects contacts. The budget that ends a stance is the route's own
/// <c>automaticReturn</c> trigger, which is absent on every route the engine
/// never fires by itself — so an absent budget means "this stance has none",
/// not "this stance is unknown".
///
/// Three facts fall out of that reading and drive the doctrine in
/// <see cref="Cast"/> and <see cref="ShotSolver"/>:
///
/// <list type="bullet">
/// <item>A volley is one cast, not a posture. The return counter is
/// <c>attacks-issued-since-entering-source-form</c> at threshold one, so
/// entering buys exactly one fan and the engine spends the exit for you. The
/// price is the entry windup plus the fan gun's own cooldown, and both are
/// contract values rather than guesses.</item>
/// <item>A guard deflects only what arrives inside its facing quadrant, and it
/// cannot rotate while raised. So the arc is a fixed piece of geometry, a bolt
/// that arrives from anywhere else lands normally, and a bolt that arrives
/// head-on comes back owned by the other team.</item>
/// <item>Every windup is <c>wait-only</c> while retaining the source form. A
/// body inside one cannot step aside, cannot turn, and is still targetable —
/// which is the only moment in this ruleset when a target's position is a
/// certainty rather than a distribution.</item>
/// </list>
/// </summary>
internal sealed class Skills
{
    /// <summary>
    /// Counter ID naming a stance whose budget is spent by attacking. The
    /// string is the contract's, matched rather than assumed: a route without
    /// it has no attack budget.
    /// </summary>
    private const string AttackCounter = "attacks-issued";

    /// <summary>Counter ID naming a stance whose budget is spent by deflecting.</summary>
    private const string DeflectCounter = "projectiles-deflected";

    /// <summary>Spread policy that lays a fan symmetrically around the aim.</summary>
    private const string SymmetricFan = "symmetric-adjacent-heading-fan";

    private readonly Dictionary<string, StanceRoute> _volleyEntry =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, StanceRoute> _guardEntry =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReturnRoute> _returns =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _fans =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _guardForms = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _slots = [];

    private Skills(Doctrine doctrine)
    {
        GenericActorResolvedMatchContract contract = doctrine.Contract;

        foreach (GenericActorRulesContract.Form form in contract.Rules.Forms)
        {
            if (form.ProjectileGuard
                != GenericActorRulesContract.FormProjectileGuard.None)
            {
                _guardForms.Add(form.Id);
            }
            GenericActorRulesContract.AttackProfile? attack =
                doctrine.AttackFor(form.Id);
            if (attack is not null)
                _fans[form.Id] = Offsets(attack);
        }

        foreach (GenericActorRulesContract.FormTransition transition
                 in contract.Rules.SameLifeTransitions
                     .OfType<GenericActorRulesContract.FormTransition>())
        {
            // The return leg is keyed by the form it leaves, because that is
            // the form a life reads on itself while standing in the stance.
            if (transition.AutomaticReturn is
                GenericActorRulesContract.AutomaticReturnTrigger trigger)
            {
                _returns[transition.SourceFormId] = new ReturnRoute(
                    transition.ActionId,
                    transition.TargetFormId,
                    Math.Max(1, transition.Windup.DurationTicks),
                    trigger.Counter.Contains(
                        AttackCounter,
                        StringComparison.Ordinal),
                    trigger.Counter.Contains(
                        DeflectCounter,
                        StringComparison.Ordinal),
                    Math.Max(1, trigger.Threshold));
            }
            else if (_guardForms.Contains(transition.SourceFormId)
                || doctrine.AttackFor(transition.SourceFormId)
                    is { Volley: not null })
            {
                // A stance the engine never returns for still has an ordinary
                // way out; record it so a life is never stuck in one.
                _returns.TryAdd(
                    transition.SourceFormId,
                    new ReturnRoute(
                        transition.ActionId,
                        transition.TargetFormId,
                        Math.Max(1, transition.Windup.DurationTicks),
                        false,
                        false,
                        int.MaxValue));
            }

            GenericActorRulesContract.AttackProfile? target =
                doctrine.AttackFor(transition.TargetFormId);
            GenericActorRulesContract.Form? targetForm =
                doctrine.FormFor(transition.TargetFormId);
            if (targetForm is null)
                continue;

            if (target?.Volley is not null)
            {
                var route = new StanceRoute(
                    transition.ActionId,
                    transition.TargetFormId,
                    Math.Max(1, transition.Windup.DurationTicks),
                    target.CooldownTicks,
                    target.Projectile.MaxTravelTiles,
                    target.Volley.ProjectileCount,
                    targetForm.ObjectiveWeight);
                if (!_volleyEntry.TryGetValue(
                        transition.SourceFormId,
                        out StanceRoute? known)
                    || route.ProjectileCount > known.ProjectileCount)
                {
                    _volleyEntry[transition.SourceFormId] = route;
                }
            }
            else if (targetForm.ProjectileGuard
                != GenericActorRulesContract.FormProjectileGuard.None)
            {
                _guardEntry[transition.SourceFormId] = new StanceRoute(
                    transition.ActionId,
                    transition.TargetFormId,
                    Math.Max(1, transition.Windup.DurationTicks),
                    0,
                    0,
                    0,
                    targetForm.ObjectiveWeight);
            }
        }

        foreach (PublicUnitSlot slot in contract.Topology.UnitSlots)
        {
            _slots.TryGetValue(slot.TeamId, out int count);
            _slots[slot.TeamId] = count + 1;
        }
        OwnSlots = SlotsFor(doctrine.TeamId);
        MaxOpposingSlots = _slots
            .Where(entry => entry.Key != doctrine.TeamId)
            .Select(entry => entry.Value)
            .DefaultIfEmpty(OwnSlots)
            .Max();
    }

    /// <summary>Reads the declared skill kit out of the resolved contract.</summary>
    public static Skills Resolve(Doctrine doctrine) => new(doctrine);

    /// <summary>Stable unit slots this team may ever field.</summary>
    public int OwnSlots { get; }

    /// <summary>
    /// Largest stable-slot count any opposing team may field. A side that may
    /// field more bodies than this one wins any straight lane trade, so the
    /// answer is bearings rather than a bigger clump — never a hard-coded
    /// three.
    /// </summary>
    public int MaxOpposingSlots { get; }

    /// <summary>True when some form in this contract deflects contacts.</summary>
    public bool AnyGuard => _guardForms.Count > 0;

    /// <summary>True when some form in this contract fires a fan.</summary>
    public bool AnyVolley => _volleyEntry.Count > 0 || _fans.Values.Any(
        offsets => offsets.Length > 1);

    /// <summary>Stable unit slots declared for one team.</summary>
    public int SlotsFor(int teamId) =>
        _slots.TryGetValue(teamId, out int count) ? count : 0;

    /// <summary>
    /// The fan route out of <paramref name="formId"/>, or null when this form
    /// has none — which is every form on a contract without the kit.
    /// </summary>
    public StanceRoute? VolleyFrom(string formId) =>
        _volleyEntry.TryGetValue(formId, out StanceRoute? route) ? route : null;

    /// <summary>The guard route out of a form, or null when it has none.</summary>
    public StanceRoute? GuardFrom(string formId) =>
        _guardEntry.TryGetValue(formId, out StanceRoute? route) ? route : null;

    /// <summary>The way out of a stance form, or null when this is not one.</summary>
    public ReturnRoute? ReturnFrom(string formId) =>
        _returns.TryGetValue(formId, out ReturnRoute? route) ? route : null;

    /// <summary>True when a body in this form deflects facing-quadrant contacts.</summary>
    public bool Guards(string formId) => _guardForms.Contains(formId);

    /// <summary>
    /// Heading sector offsets one attack of this form launches, relative to its
    /// aim. A single-bolt gun answers <c>[0]</c>, so callers need no special
    /// case for a contract without volleys.
    /// </summary>
    public int[] FanFor(string formId) =>
        _fans.TryGetValue(formId, out int[]? offsets) ? offsets : [0];

    private static int[] Offsets(
        GenericActorRulesContract.AttackProfile attack)
    {
        int count = attack.ProjectilesPerAttack;
        if (count <= 1)
            return [0];
        if (attack.Volley is null
            || !attack.Volley.Spread.Contains(
                SymmetricFan,
                StringComparison.Ordinal))
        {
            return [0];
        }
        // Symmetric about the aim heading: an odd count centres a lane on the
        // facing and pairs the rest off around it.
        var offsets = ImmutableArray.CreateBuilder<int>(count);
        int low = -((count - 1) / 2);
        for (int index = 0; index < count; index++)
            offsets.Add(low + index);
        return [.. offsets];
    }
}

/// <summary>
/// One declared same-life route into a stance, priced from the contract.
/// </summary>
/// <param name="ActionId">Action catalog entry that requests the route.</param>
/// <param name="TargetFormId">Form the route completes into.</param>
/// <param name="WindupTicks">
/// Ticks of wait-only commitment before the stance exists. A body inside them
/// cannot step, cannot turn, and remains targetable.
/// </param>
/// <param name="CooldownTicks">Cooldown the stance gun applies per attack.</param>
/// <param name="MaxTravelTiles">Travel budget of the stance gun.</param>
/// <param name="ProjectileCount">Projectiles one stance attack launches.</param>
/// <param name="ObjectiveWeight">
/// Weight the stance form contributes to control. A stance that keeps weight
/// one holds its ground while it aims, which is what makes casting from the
/// objective cost nothing positional.
/// </param>
internal sealed record StanceRoute(
    string ActionId,
    string TargetFormId,
    int WindupTicks,
    int CooldownTicks,
    int MaxTravelTiles,
    int ProjectileCount,
    int ObjectiveWeight);

/// <summary>
/// The way back out of a stance, and the budget the engine spends for you.
/// </summary>
/// <param name="ActionId">Parameterless action that requests the return.</param>
/// <param name="TargetFormId">Form the return completes into.</param>
/// <param name="WindupTicks">Ticks of wait-only commitment the exit costs.</param>
/// <param name="SpentByAttacking">
/// True when the budget counts attacks issued since entering — the fan's
/// "one cast and you are out".
/// </param>
/// <param name="SpentByDeflecting">
/// True when the budget counts deflections since entering — the shield's
/// "third bolt shatters it".
/// </param>
/// <param name="Threshold">
/// Count at which the engine starts the return, or
/// <see cref="int.MaxValue"/> when this route carries no budget at all.
/// </param>
internal sealed record ReturnRoute(
    string ActionId,
    string TargetFormId,
    int WindupTicks,
    bool SpentByAttacking,
    bool SpentByDeflecting,
    int Threshold);
