using BotArena.Sdk;

/// <summary>
/// The gun's aperture: which absolute headings a form may actually launch a
/// bolt along, and how many facings buy each of them.
///
/// <para>This is the whole of revision 5's new geometry, and it is one contract
/// read: <c>shotProgram.minInitialAimSteps</c> and
/// <c>maxInitialAimSteps</c>. Where they are zero — every arm this lineage was
/// measured on before — a gun launches along its facing and nothing else, so a
/// body is armed only while it stands on a cardinal line and the four facings
/// partition the four rays one apiece. Where they are ±1 a gun launches along
/// its facing <em>or</em> either 45-degree neighbour, and the partition breaks in
/// a way that is worth a doctrine:</para>
///
/// <list type="bullet">
/// <item>A cardinal ray is launchable from exactly ONE facing. Face east and
/// nothing but east fires due east.</item>
/// <item>A diagonal ray is launchable from TWO — north-east belongs to the
/// north facing's aperture and the east facing's alike.</item>
/// </list>
///
/// <para>Under <c>facing-locked</c> that asymmetry is the striker's whole edge,
/// because a rotation is not a flourish there: it is how a body moves. A target
/// on a cardinal bearing is covered by one facing, so the tick this life turns
/// onto its route it stops being armed. A target on a DIAGONAL bearing is
/// covered by both facings that bracket it, so the same life turns, steps, and
/// keeps the shot. Standing diagonal to a contact is therefore not a nicety; it
/// is the pose in which movement and fire stop competing for the tick — and an
/// omnidirectional chassis gains nothing from it, because it was never paying
/// the aperture in the first place.</para>
///
/// <para>Nothing here names an arm, a class, or a skill. On a contract whose
/// aim bounds are zero every query below returns exactly what revision 4's
/// cardinal-only test returned, which is what keeps the qualification profile —
/// where the offsets do not exist — playing the measured doctrine.</para>
/// </summary>
internal sealed class Arms
{
    /// <summary>Absolute heading sectors in the contract's heading model.</summary>
    private const int Sectors = 8;

    private readonly Doctrine _doctrine;
    private readonly Dictionary<string, int[]> _offsets =
        new(StringComparer.Ordinal);

    public Arms(Doctrine doctrine) => _doctrine = doctrine;

    /// <summary>
    /// Signed initial aim offsets this form's gun may launch with. A gun with no
    /// programmable aim answers <c>[0]</c>, so callers need no special case for
    /// a straight-only chassis or a contract without the offset arm.
    /// </summary>
    public int[] OffsetsFor(string formId)
    {
        if (_offsets.TryGetValue(formId, out int[]? known))
            return known;
        int[] resolved = Resolve(_doctrine.AttackFor(formId));
        _offsets[formId] = resolved;
        return resolved;
    }

    /// <summary>
    /// True when this form's gun may leave its facing at launch — the one fact
    /// that turns a diagonal bearing into a pose worth taking.
    /// </summary>
    public bool HasOffsets(string formId) => OffsetsFor(formId).Length > 1;

    /// <summary>
    /// Absolute headings a bolt may leave this form's tile along while it faces
    /// <paramref name="facing"/>.
    /// </summary>
    public List<ProjectileHeading> Aperture(string formId, Direction facing)
    {
        var headings = new List<ProjectileHeading>();
        GenericActorRulesContract.AttackProfile? attack =
            _doctrine.AttackFor(formId);
        if (attack is null)
            return headings;
        if (attack.OmnidirectionalAim)
        {
            // An absolutely-aimed gun is every heading from every facing; the
            // turret's eight-way fire is the contract's example.
            ProjectileHeading forward = facing.ToProjectileHeading();
            for (int sector = 0; sector < Sectors; sector++)
                headings.Add(forward.Turned(sector));
            return headings;
        }
        ProjectileHeading aim = facing.ToProjectileHeading();
        foreach (int offset in OffsetsFor(formId))
            headings.Add(aim.Turned(offset));
        return headings;
    }

    /// <summary>
    /// How many distinct facings would leave this life armed against some tile
    /// in <paramref name="targets"/> from <paramref name="from"/>: a count of
    /// the cardinals whose aperture puts an unobstructed ray, inside the
    /// declared travel budget, onto one of those tiles.
    ///
    /// <para>Zero means the tile is not a firing seat at all. One is the
    /// ordinary case and the only case a gun without offsets can produce. Two
    /// is the diagonal pose — armed either way this body turns — and under
    /// <c>facing-locked</c> that is the difference between a route and a
    /// duel.</para>
    /// </summary>
    public int ArmingFacings(
        string formId,
        Position from,
        IReadOnlyCollection<Position> targets)
    {
        if (targets.Count == 0)
            return 0;
        GenericActorRulesContract.AttackProfile? attack =
            _doctrine.AttackFor(formId);
        if (attack is null)
            return 0;
        int armed = 0;
        foreach (Direction facing in Field.Cardinals)
        {
            foreach (ProjectileHeading heading in Aperture(formId, facing))
            {
                if (!Reaches(from, heading, attack, targets))
                    continue;
                armed++;
                break;
            }
        }
        return armed;
    }

    /// <summary>
    /// True when some target tile lies on a launchable ray from
    /// <paramref name="from"/> — the diagonal-aware replacement for "is there a
    /// cardinal line to a body from here".
    /// </summary>
    public bool Armed(
        string formId,
        Position from,
        IReadOnlyCollection<Position> targets) =>
        ArmingFacings(formId, from, targets) > 0;

    /// <summary>
    /// Traces one launchable ray through the contract's own projectile rules —
    /// travel budget, wall termination, strict diagonal corners — and reports
    /// whether it reaches a target tile. Reusing <see cref="Ballistics"/> here
    /// is what makes the diagonal case honest: a diagonal ray is refused
    /// through a corner exactly where a diagonal bolt would be.
    /// </summary>
    private bool Reaches(
        Position from,
        ProjectileHeading heading,
        GenericActorRulesContract.AttackProfile attack,
        IReadOnlyCollection<Position> targets)
    {
        foreach (Position tile in Ballistics.Trace(
                     _doctrine,
                     from,
                     heading,
                     bendDirection: 0,
                     bendAfterTiles: 0,
                     bendEveryTiles: 1,
                     bendCount: 0,
                     attack.Projectile.MaxTravelTiles,
                     attack.Projectile.DiagonalCornersMustBeClear))
        {
            if (targets.Contains(tile))
                return true;
        }
        return false;
    }

    private static int[] Resolve(
        GenericActorRulesContract.AttackProfile? attack)
    {
        if (attack is null)
            return [0];
        GenericActorRulesContract.ShotProgramDefinition shots =
            attack.ShotProgram;
        if (!shots.Enabled)
            return [0];
        int low = Math.Min(shots.MinInitialAimSteps, shots.MaxInitialAimSteps);
        int high = Math.Max(shots.MinInitialAimSteps, shots.MaxInitialAimSteps);
        var offsets = new List<int>(high - low + 1);
        for (int offset = low; offset <= high; offset++)
            offsets.Add(offset);
        return offsets.Count == 0 ? [0] : [.. offsets];
    }
}
