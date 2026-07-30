namespace BotArena.Engine;

/// <summary>
/// Registered aim-grammar levels (DECISIONS #173). The one-bend grammar's
/// flag conflated "one bend per shot" with "no initial aim offset", so
/// since the class arms began no mobile gun could launch a bolt at ±45°
/// off its facing — and a diagonally-adjacent enemy was unhittable. That
/// was never a design ruling; the owner spotted it in watched games. The
/// Offset arm restores the ±1-sector initial aim on every class's mobile
/// gun. Specials are untouched: the volley aims by facing and the turret
/// already aims absolutely.
/// </summary>
public enum FrontlineLabsAimArm
{
    /// <summary>
    /// Today's class-arm grammar: bolts launch along facing only. Not an
    /// arm — selecting it changes nothing.
    /// </summary>
    Straight = 0,

    /// <summary>
    /// Mobile guns may launch at −1/0/+1 sectors off facing (the 45°
    /// diagonals), keeping the one-bend rule. Flank grammar: registered
    /// with the prediction that it pulls the over-band
    /// bulwark-vs-striker edge down.
    /// </summary>
    Offset,
}
