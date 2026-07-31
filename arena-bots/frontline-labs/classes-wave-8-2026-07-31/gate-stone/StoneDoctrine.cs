/// <summary>
/// The wave-8 doctrine pass, named one rule per switch so that every claim in
/// the freeze record is a leave-one-out measurement rather than an opinion.
///
/// <para>They are <c>static readonly</c> rather than <c>const</c> so that a
/// disabled rule still compiles its call site — an ablation must remove the
/// rule's EFFECT, not its code, or the measurement is of a different
/// program.</para>
///
/// <para>Every rule is additionally gated on a CONTRACT fact, so on a ruleset
/// without the channel or without the economy the switch is already inert and
/// the same artifact plays the older arms unchanged.</para>
///
/// <para><b>A <c>false</c> here does not mean unfinished.</b> It means the rule
/// was built, measured over eighteen distinct games, and refused — its code and
/// its number are kept because the next wave will otherwise derive it again.
/// The base every number is read against is this same artifact with every
/// switch off, which reproduces the wave-6 predecessor DECISION FOR DECISION
/// and scores <b>+79 aggregate / +78 same-side</b>.</para>
/// </summary>
internal static class StoneDoctrine
{
    /// <summary>
    /// D1 — CHANNEL ARITHMETIC. <b>Built, measured, REFUSED (−62 aggregate,
    /// −61 same-side).</b>
    ///
    /// <para>Price a body by what its stillness (or its denial) moves the claim
    /// by this tick, read from <c>gameMode.capture</c>: the stationary claim,
    /// the total denial, the surplus cap, the erosion multiple and the damage
    /// interrupt. It is the faithful implementation of the arm and it is the
    /// wave's headline idea, and with it on the doctrine scores +17 against
    /// +79 with it off.</para>
    ///
    /// <para>Why it loses is worth more than the rule. The channel marginal is
    /// an exact answer to "what is this body worth THIS TICK", and almost every
    /// decision it was asked to price spans twenty — whether to fortify,
    /// whether to walk for scrap, whether to hold a shoulder. A body that is
    /// the third on a capped point is worth zero right now and worth one the
    /// moment a sibling dies; the flat weight is a worse estimate of the tick
    /// and a better estimate of the window. The two things the channel
    /// legitimately zeroes are the ones a published CLOCK already zeroed — the
    /// redeploy pause and a completion that would be spent inside an enemy hold
    /// — and this lineage was already reading both, in wave 5.</para>
    ///
    /// <para>The three rules below consume the field this switch builds, so
    /// they are inert while it is off and are recorded with it.</para>
    /// </summary>
    public static readonly bool ChannelArithmetic = false;

    /// <summary>
    /// D2 — CHANNEL DISCIPLINE. <b>Built, measured EXACTLY INERT, refused with
    /// D1.</b> A body whose stillness is buying progress does not spend that
    /// tick on a step: a dodge and a yield both cost exactly the gain they
    /// interrupt, and are taken only when the bolt costs strictly more.
    ///
    /// <para>It never fires because the contract prices it at parity:
    /// <c>revertPerDamagePoint</c> is 1 against a <c>gainPerSoleTeamTick</c> of
    /// 1, and the surplus cap holds the marginal body's build contribution at 1
    /// whatever the stack depth — so stepping aside and eating the bolt cost
    /// the same progress at every depth, and stepping aside also keeps the
    /// health. The one state where standing wins is erosion, where the build is
    /// multiplied by four and the revert is not, and that state did not decide
    /// a game in eighteen.</para>
    /// </summary>
    public static readonly bool ChannelDiscipline = false;

    /// <summary>
    /// D3 — THE CHANNEL STATION. <b>Built, measured, refused with D1.</b> Where
    /// a body is sent is decided by the same marginal question the ledger asks
    /// — would standing on the point move the claim? — rather than by wave 6's
    /// net-weight test. Measured on its own against a full build it was worth
    /// +9, and against a build that had already dropped the channel-priced
    /// lease it was worth −5: it is a consumer of D1 and shares its verdict.
    /// </summary>
    public static readonly bool ChannelStation = false;

    /// <summary>
    /// D4 — THE REVERT GUN. <b>Shipped.</b> A bolt into a body standing on the
    /// contested point is worth the progress it takes back, not merely the
    /// health it removes: at a threshold of 8 one point of damage to a body of
    /// the CONTROLLING team standing there is an eighth of a capture. The
    /// premium is computed from <c>claimInterrupt.revertPerDamagePoint</c>
    /// times the gun's own damage, so it collapses to this lineage's old flat
    /// presence bonus on a ruleset that declares no interrupt.
    /// </summary>
    public static readonly bool RevertGun = true;

    /// <summary>
    /// D5 — THE DENIAL ANCHOR. <b>Built, measured EXACTLY INERT, refused with
    /// D1.</b> A turret with a clear lane onto a point an enemy is channelling
    /// is reverting their run a point at a tick, so the lease is renewed while
    /// the gun is denying. Correct, contract-driven, and it never changed a
    /// game: presence beats the gun so consistently that the state it needs —
    /// an enemy claim standing, a lane onto it, and nothing better for this
    /// body to do — did not arise.
    /// </summary>
    public static readonly bool DenialAnchor = false;

    /// <summary>
    /// D8 — TURRET RELIEF. <b>Shipped.</b> A turret asks whether the point
    /// needs a body by pricing the MOBILE body it would become, not by pricing
    /// its own objective weight — which is zero by declaration, so wave 6 asked
    /// the question and always got "no". A repair, and therefore measured like
    /// every other rule rather than assumed.
    /// </summary>
    public static readonly bool TurretRelief = true;

    /// <summary>
    /// D6 — SALVAGE. <b>Shipped.</b> A pile is taken when the detour is cheaper
    /// than the ticks this body's presence is worth and the free window is long
    /// enough to walk it — which on a live front is never and in a redeploy
    /// pause is sometimes. A loaded body does not anchor: a zero-weight form
    /// drops the whole load on the floor.
    /// </summary>
    public static readonly bool Salvage = true;

    /// <summary>
    /// D7 — INVEST ON A ZERO TICK. <b>Shipped.</b> The store's verb costs a
    /// body its action, so it is cast from a tick that was already worth
    /// nothing — from under the shield, from a turret with an empty lane, from
    /// a body whose gun is cold — and by exactly one body per tick, elected in
    /// muster order from shared state so the second is never Blocked.
    /// </summary>
    public static readonly bool Invest = true;
}
