using BotArena.Sdk;

/// <summary>
/// GARRISON — the seven doctrine switches of revision 7, each measured alone.
///
/// <para>Every one of them is gated on a contract fact that a ruleset without
/// the arm simply does not publish, so on such a ruleset the switch is
/// PROVABLY inert rather than merely quiet: <see cref="ChannelRules.Channels"/>
/// is false unless the declared control policy channels, and
/// <see cref="Salvage.Declared"/> is false unless the mode declares an
/// economy.</para>
/// </summary>
internal static class Garrison
{
    /// <summary>G1 — stillness is the capture. A body whose tile did not change
    /// is the only body that pays into a claim, so a body already standing on
    /// the surface refuses the step that would zero its own contribution.</summary>
    public static readonly bool Stillness = true;

    /// <summary>
    /// G2a — ROOT FOR DENIAL: treat the channel's revert rate as a reason to
    /// give up objective weight and become a gun.
    ///
    /// <para><b>OFF, and this is the wave's headline negative result rather than
    /// an unfinished rule.</b> It is the arm's advertised leverage for this
    /// chassis and it is measurably backwards. A body of positive weight
    /// standing on the surface subtracts from the enemy's multiplier
    /// UNCONDITIONALLY — no line, no vision, no cooldown, nothing to dodge and
    /// no arc to turn it. A rooted gun's revert is the same size and is
    /// conditional on every one of those. Sixteen mirror cells, permissive gate:
    /// 487 anchored ticks bought 5 points of denial and cost 24.5 points of
    /// territory a cell. Strict gate demanding a live, visible, clear-ray body
    /// of the claiming team on the surface: it fires almost never (2 anchored
    /// ticks) and still trails the predecessor's own tenure arithmetic by 11.3
    /// a cell, because vetoing is not free either. The class's existing bargain
    /// — root when relief is standing and the front is not about to move —
    /// already prices the channel better than a rule written for it.</para>
    /// </summary>
    public static readonly bool TurretGate = false;

    /// <summary>
    /// G2b — the interrupt tells FIRE CONTROL which body is the score: a bolt
    /// into a body of the claiming team standing on the region reverts that
    /// team's run, and the identical bolt one tile off it reverts nothing.
    /// </summary>
    public static readonly bool DenialFire = true;

    /// <summary>
    /// G2c — the interrupt tells a ROOTED GUN when the ground wants its weight
    /// back: leave the turret the moment the surface it would join could build
    /// or erode.
    /// </summary>
    public static readonly bool DenialExit = false;

    /// <summary>G3 — screen the channeler. A body on the firing line and OFF
    /// the surface eats a bolt that would have reverted the run.</summary>
    public static readonly bool Screen = true;

    /// <summary>G4 — cap discipline. Surplus stops paying at the declared cap,
    /// so a body beyond it buys no speed and adds interrupt surface.</summary>
    public static readonly bool CapDiscipline = true;

    /// <summary>G5 — erosion urgency. An enemy claim erodes at the declared
    /// multiple, so two ticks of control can undo eight ticks of theirs.</summary>
    public static readonly bool Erosion = true;

    /// <summary>G6 — the interrupt prices the shield. A deflected bolt on the
    /// point now saves progress as well as health.</summary>
    public static readonly bool Interrupt = true;

    /// <summary>G7 — spend the bank. The mask says what is affordable; the
    /// declared effects say which track this chassis is short of.</summary>
    public static readonly bool Invest = true;

    /// <summary>G8 — bank without leaving the front: take the pile under the
    /// boot, never the walk across the map.</summary>
    public static readonly bool Salvage = true;

    /// <summary>
    /// Whether the store's gun-travel track may be bought at all. OFF, and not
    /// for a doctrine reason: buying it aborts the match with an engine
    /// invariant failure and writes no replay. See
    /// <c>Salvage.MutatesProjectileEnvelope</c> and the DX report — this is the
    /// one switch in this file that is a defect workaround rather than a rule,
    /// and it is separated from <see cref="Invest"/> so the rule can still be
    /// measured with the defect avoided.
    /// </summary>
    public static readonly bool TravelTier = false;
}

/// <summary>
/// The declared capture arithmetic, read once from the resolved contract.
///
/// <para><b>Why this type exists.</b> Revision 6 asked one question of the
/// control policy — does surplus weight scale gain — and answered it by looking
/// for a substring that names a DIFFERENT policy. Under a channelling policy
/// that reader says "binary control: one body of positive weight nulls any
/// number", which is the exact opposite of the truth, and every tenure and
/// contest decision downstream inherited the error. The fields below are the
/// contract's own, and the presence of the last three is what says the mechanic
/// exists at all: a ruleset without a channel omits them, exactly like a
/// ruleset without a ratchet omits its hold.</para>
/// </summary>
internal sealed class ChannelRules
{
    public ChannelRules(GenericActorResolvedMatchContract contract)
    {
        if (contract.Rules.GameMode
            is not GenericActorRulesContract.FrontlineGameMode frontline)
        {
            return;
        }

        GenericActorRulesContract.FrontlineCapture capture = frontline.Capture;
        Threshold = capture.Threshold;
        Gain = Math.Max(1, capture.GainPerSoleTeamTick);

        // The whole arm in one read. "stationary-claim-weight" is the policy's
        // own name for the rule that only a body which did not change tile pays
        // into a claim; every other policy in this family leaves the three
        // fields below at their inert zero/null.
        Channels = capture.ControlPolicy.Contains(
            "stationary-claim-weight",
            StringComparison.Ordinal);
        GainCap = capture.StationaryGainMultiplierCap;
        ErosionMultiplier = capture.OpposingErosionMultiplier;

        GenericActorRulesContract.FrontlineClaimInterrupt? interrupt =
            capture.ClaimInterrupt;
        if (interrupt is not null)
        {
            RevertPerDamagePoint = Math.Max(0, interrupt.RevertPerDamagePoint);
            // Both are exact policy IDs. The scope decides whether a screen is
            // free (it is, when only bodies ON the region revert anything) and
            // the granularity decides how expensive one leaked bolt is.
            InterruptOnObjectiveOnly = interrupt.Scope.Contains(
                "on-active-objective-region",
                StringComparison.Ordinal);
            InterruptControllerOnly = interrupt.Scope.Contains(
                "controlling-team",
                StringComparison.Ordinal);
            InterruptTakesWholeRun = interrupt.Granularity.Contains(
                "whole-run",
                StringComparison.Ordinal);
        }
    }

    /// <summary>Progress one capture costs.</summary>
    public int Threshold { get; }

    /// <summary>Base gain for a sole controlling team, per tick.</summary>
    public int Gain { get; } = 1;

    /// <summary>
    /// True when only bodies that did not change tile this tick pay into a
    /// claim while every body denies. False on every ruleset that does not
    /// declare the channel, which makes every rule below inert there.
    /// </summary>
    public bool Channels { get; }

    /// <summary>
    /// Ceiling on the stationary-surplus gain multiplier. Zero means the field
    /// is absent — no channel — so treat surplus as uncapped presence.
    /// </summary>
    public int GainCap { get; }

    /// <summary>How many times faster an enemy claim erodes than one builds.</summary>
    public int ErosionMultiplier { get; }

    /// <summary>Progress reverted per point of health actually removed.</summary>
    public int RevertPerDamagePoint { get; }

    /// <summary>Only damage taken ON the active objective region reverts.</summary>
    public bool InterruptOnObjectiveOnly { get; }

    /// <summary>Only the controlling team's own bodies revert anything.</summary>
    public bool InterruptControllerOnly { get; }

    /// <summary>One leaked bolt costs the run, not one body's share of it.</summary>
    public bool InterruptTakesWholeRun { get; }

    /// <summary>
    /// True when a body standing OFF the objective region can eat a bolt for
    /// free — which is the entire arithmetic of the escort pattern, and is a
    /// contract read rather than a claim about physics.
    /// </summary>
    public bool ScreeningIsFree =>
        Channels && RevertPerDamagePoint > 0 && InterruptOnObjectiveOnly;

    /// <summary>
    /// Ticks a claim of <paramref name="surplus"/> stationary surplus needs to
    /// cross the threshold from <paramref name="progress"/>, or
    /// <see cref="int.MaxValue"/> when it never does.
    /// </summary>
    public int TicksToComplete(int progress, int surplus)
    {
        int rate = RateFor(surplus);
        if (rate <= 0)
            return int.MaxValue;
        int remaining = Math.Max(0, Threshold - progress);
        return (remaining + rate - 1) / rate;
    }

    /// <summary>Ticks to wipe an enemy claim of <paramref name="progress"/>.</summary>
    public int TicksToErode(int progress, int surplus)
    {
        int rate = RateFor(surplus) * Math.Max(1, ErosionMultiplier);
        if (rate <= 0)
            return int.MaxValue;
        return (Math.Max(0, progress) + rate - 1) / rate;
    }

    /// <summary>Progress one tick of <paramref name="surplus"/> is worth.</summary>
    public int RateFor(int surplus)
    {
        if (surplus <= 0)
            return 0;
        int capped = GainCap > 0 ? Math.Min(GainCap, surplus) : surplus;
        return Gain * capped;
    }
}

/// <summary>
/// The channel as it stands THIS tick: who is claiming, which bodies are
/// paying into it, and what one more body or one more bolt is worth.
///
/// <para><b>Stillness is derived, not published.</b> There are no new
/// observation facts on this arm — the rule moves <c>captureProgress</c> and
/// nothing else — so "did that body change tile" is a comparison against last
/// tick's frozen observation, kept in this life's own private memory. Three
/// consequences the doctrine has to respect: a life born this tick has no
/// previous position and the rules say it counts as stationary, so the missing
/// entry is an ANSWER; an enemy that walked out of vision and back cannot be
/// judged and is assumed to be paying, which is the conservative direction; and
/// nothing here is remembered across a life, because a fresh life starts with
/// empty memory by declaration.</para>
/// </summary>
internal sealed class ChannelState
{
    private readonly Dictionary<string, Position> _wasAt =
        new(StringComparer.Ordinal);
    private int _stampedTick = int.MinValue;
    private readonly Dictionary<string, Position> _pending =
        new(StringComparer.Ordinal);

    /// <summary>My team's stationary claim weight on the active surface.</summary>
    public int Claim { get; private set; }

    /// <summary>My team's total denial weight on the active surface.</summary>
    public int Mine { get; private set; }

    /// <summary>Every enemy body's weight on the active surface.</summary>
    public int Theirs { get; private set; }

    /// <summary>Enemy stationary claim weight, as far as vision allows.</summary>
    public int TheirClaim { get; private set; }

    /// <summary>My own weight, zero when I am not standing on the surface.</summary>
    public int SelfWeight { get; private set; }

    /// <summary>True when my own tile did not change on the previous step.</summary>
    public bool SelfStill { get; private set; } = true;

    /// <summary>Allied bodies (excluding me) standing still on the surface.</summary>
    public int Channelers { get; private set; }

    /// <summary>My surplus if I hold my tile: claim minus their denial.</summary>
    public int Surplus => Claim - Theirs;

    /// <summary>My surplus if I step this tick and stop paying into the claim.</summary>
    public int SurplusIfIMove => Claim - (SelfStill ? SelfWeight : 0) - Theirs;

    /// <summary>Their surplus against my total denial weight.</summary>
    public int TheirSurplus => TheirClaim - Mine;

    /// <summary>
    /// Refreshes this tick's arithmetic and rolls the position memory forward.
    /// Idempotent within a tick.
    /// </summary>
    public void Observe(
        ContractLens lens,
        GenericActorContext context,
        Position[] active)
    {
        if (_stampedTick == context.Tick)
            return;
        _stampedTick = context.Tick;

        var surface = new HashSet<Position>(active);
        Claim = 0;
        Mine = 0;
        Theirs = 0;
        TheirClaim = 0;
        Channelers = 0;
        SelfWeight = 0;

        _pending.Clear();

        string me = context.Self.ActorId.ToString();
        SelfStill = Still(me, context.Self.Position);
        _pending[me] = context.Self.Position;
        if (surface.Contains(context.Self.Position))
        {
            SelfWeight = Weight(lens, context.Self.FormId);
            Mine += SelfWeight;
            if (SelfStill)
                Claim += SelfWeight;
        }

        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        {
            string key = ally.ActorId.ToString();
            bool still = Still(key, ally.Position);
            _pending[key] = ally.Position;
            if (!surface.Contains(ally.Position))
                continue;
            int weight = Weight(lens, ally.FormId);
            Mine += weight;
            if (!still || weight <= 0)
                continue;
            Claim += weight;
            Channelers++;
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            string key = enemy.ActorId.ToString();
            // An enemy I did not see last tick is ASSUMED to have held its tile.
            // That over-counts their claim, which is the safe direction: it
            // makes me less willing to give up weight, never more.
            bool still = !_wasAt.TryGetValue(key, out Position before)
                || before == enemy.Position;
            _pending[key] = enemy.Position;
            if (!surface.Contains(enemy.Position))
                continue;
            int weight = Weight(lens, enemy.FormId);
            Theirs += weight;
            if (still)
                TheirClaim += weight;
        }

        _wasAt.Clear();
        foreach (KeyValuePair<string, Position> entry in _pending)
            _wasAt[entry.Key] = entry.Value;
    }

    /// <summary>Forgets every remembered pose. Called at StartLife.</summary>
    public void Reset()
    {
        _wasAt.Clear();
        _pending.Clear();
        _stampedTick = int.MinValue;
        Claim = Mine = Theirs = TheirClaim = SelfWeight = Channelers = 0;
        SelfStill = true;
    }

    private bool Still(string key, Position now) =>
        !_wasAt.TryGetValue(key, out Position before) || before == now;

    private static int Weight(ContractLens lens, string formId) =>
        lens.Form(formId)?.ObjectiveWeight ?? 0;
}
