using BotArena.Sdk;

/// <summary>
/// Fire control. Every candidate path comes from the engine's own bend rule
/// (<see cref="ShotPaths.Preview"/>) or from an eight-way ray, and every bolt is
/// priced against where a body will BE — movement resolves before combat — with
/// one extra question this ruleset makes unavoidable: does my bolt arrive inside
/// a guard's protected quadrant? If it does, the bolt does not hit; it comes
/// back with my name on it. A curve that arrives from outside the arc is worth
/// more than a straight line into the face of a shield.
/// </summary>
internal static class StoneAim
{
    private const int MaxProgramCandidates = 320;

    /// <summary>
    /// A curve has to be a shot, not a hope. A straight bolt down a lane is
    /// cheap suppression — it costs a cooldown and denies a tile — but a
    /// programmed bend commits to an exact arrival tile, and firing one at a
    /// body that has five tiles to choose from is how a bot spends its
    /// trajectory language on nothing.
    /// </summary>
    private const int CurvedFloor = 55;

    /// <summary>One priced shot with its selected action.</summary>
    /// <param name="Decision">The action to submit.</param>
    /// <param name="Score">Expected value; higher is better.</param>
    /// <param name="BreaksGuard">
    /// True when this contact is the one that shatters an enemy stance.
    /// </param>
    public sealed record Shot(
        GenericActorDecision Decision,
        int Score,
        bool BreaksGuard);

    /// <summary>
    /// The best shot this form can fire this tick, or null when nothing is worth
    /// the cooldown.
    /// </summary>
    public static Shot? Best(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        int minimumScore)
    {
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(context.Self.FormId);
        if (attack is null || context.Enemies.IsEmpty)
            return null;

        Shot? best = null;
        foreach (GenericActorActionLegality legality in context.ActionLegalities)
        {
            if (!legality.Available || !lens.IsAttack(legality.ActionId))
                continue;

            GenericActorActionLegality.ArgumentConstraint
                .ProjectileHeadingConstraint? headings = Heading(legality);
            if (headings is not null)
            {
                foreach (ProjectileHeading heading in headings.AllowedValues)
                {
                    Position[] path = Ray(
                        lens,
                        context.Self.Position,
                        heading,
                        attack.Projectile.MaxTravelTiles,
                        attack.Projectile.DiagonalCornersMustBeClear);
                    (int score, bool breaks) = Price(
                        lens,
                        context,
                        memory,
                        attack,
                        context.Self.Position,
                        path);
                    if (score > (best?.Score ?? minimumScore - 1))
                    {
                        best = new Shot(
                            new GenericActorDecision(
                                legality.ActionId,
                                legality.ActionCode,
                                [
                                    new GenericActorActionArgument
                                        .ProjectileHeadingArgument(heading),
                                ],
                                $"turret fire {heading} v{score}"),
                            score,
                            breaks);
                    }
                }
                continue;
            }

            bool payloadAllowed =
                Program(legality) is { Allowed: true }
                && attack.ShotProgram.Enabled;
            bool straightAllowed =
                attack.ShotProgram.PayloadOptional
                || !attack.ShotProgram.Enabled
                || !payloadAllowed;

            if (straightAllowed)
            {
                Position[] path = ShotPaths.Preview(
                    context.Self.Position,
                    context.Self.Facing,
                    ShotProgram.Straight,
                    attack.Projectile.MaxTravelTiles,
                    lens.IsWall).ToArray();
                (int score, bool breaks) = Price(
                    lens,
                    context,
                    memory,
                    attack,
                    context.Self.Position,
                    path);
                if (score > (best?.Score ?? minimumScore - 1))
                {
                    best = new Shot(
                        GenericActorDecision.WithoutArguments(
                            legality.ActionId,
                            legality.ActionCode,
                            $"straight fire v{score}"),
                        score,
                        breaks);
                }
            }

            if (!payloadAllowed)
                continue;

            foreach (ShotProgram program in Programs(attack.ShotProgram))
            {
                Position[] path = ShotPaths.Preview(
                    context.Self.Position,
                    context.Self.Facing,
                    program,
                    attack.Projectile.MaxTravelTiles,
                    lens.IsWall).ToArray();
                (int score, bool breaks) = Price(
                    lens,
                    context,
                    memory,
                    attack,
                    context.Self.Position,
                    path);
                if (program.BendCount > 0 && score < CurvedFloor && !breaks)
                    continue;
                // A curve is only worth taking over the straight line when it
                // is strictly better, so tie-breaks keep the simplest shot.
                if (score > (best?.Score ?? minimumScore - 1))
                {
                    best = new Shot(
                        new GenericActorDecision(
                            legality.ActionId,
                            legality.ActionCode,
                            [
                                new GenericActorActionArgument
                                    .ShotProgramArgument(program),
                            ],
                            $"bend {program.BendDirection}@"
                            + $"{program.BendAfterTiles} v{score}"),
                        score,
                        breaks);
                }
            }
        }
        return best is not null && best.Score >= minimumScore ? best : null;
    }

    /// <summary>
    /// What this gun could reach if the body faced <paramref name="facing"/>.
    /// Under a facing-locked profile the initial aim offset is usually pinned to
    /// zero, so a turn IS the aiming mechanism: this is how a rotation stops
    /// being a fidget and becomes a shot two ticks from now.
    /// </summary>
    public static int Reach(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        Direction facing)
    {
        GenericActorRulesContract.AttackProfile? attack =
            lens.Attack(context.Self.FormId);
        if (attack is null || attack.OmnidirectionalAim || context.Enemies.IsEmpty)
            return 0;

        int best = Price(
            lens,
            context,
            memory,
            attack,
            context.Self.Position,
            ShotPaths.Preview(
                context.Self.Position,
                facing,
                ShotProgram.Straight,
                attack.Projectile.MaxTravelTiles,
                lens.IsWall).ToArray()).Score;
        if (!attack.ShotProgram.Enabled)
            return best;
        foreach (ShotProgram program in Programs(attack.ShotProgram))
        {
            int score = Price(
                lens,
                context,
                memory,
                attack,
                context.Self.Position,
                ShotPaths.Preview(
                    context.Self.Position,
                    facing,
                    program,
                    attack.Projectile.MaxTravelTiles,
                    lens.IsWall).ToArray()).Score;
            if (score > best)
                best = score;
        }
        return best;
    }

    /// <summary>
    /// Whether firing along <paramref name="path"/> would be turned back by a
    /// guard standing on it — the question that decides whether poking is a
    /// tempo tax or a free hit.
    /// </summary>
    private static (int Score, bool BreaksGuard) Price(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        GenericActorRulesContract.AttackProfile attack,
        Position origin,
        Position[] path)
    {
        int best = 0;
        bool breaks = false;
        Position previous = origin;
        for (int index = 0; index < path.Length; index++)
        {
            Position tile = path[index];
            ProjectileHeading arrival = Toward(previous, tile);
            previous = tile;
            int offset = ArrivalOffset(attack, index + 1);

            bool occupiedByEnemy = false;
            foreach (GenericActorContext.ObservedEnemyState enemy
                     in context.Enemies)
            {
                if (enemy.Position == tile)
                    occupiedByEnemy = true;
                (int score, bool shatters) = Value(
                    lens,
                    context,
                    memory,
                    attack,
                    enemy,
                    tile,
                    offset,
                    arrival);
                if (score > best)
                {
                    best = score;
                    breaks = shatters;
                }
            }
            if (occupiedByEnemy)
                break;
            bool alliedBlock = false;
            if (!lens.AlliedBoltsPass)
            {
                foreach (GenericActorContext.ObservedAllyState ally
                         in context.Allies)
                {
                    if (ally.Position == tile)
                        alliedBlock = true;
                }
            }
            if (alliedBlock)
                break;
        }
        return (best, breaks);
    }

    private static (int Score, bool BreaksGuard) Value(
        StoneContract lens,
        GenericActorContext context,
        StoneMemory memory,
        GenericActorRulesContract.AttackProfile attack,
        GenericActorContext.ObservedEnemyState enemy,
        Position tile,
        int offset,
        ProjectileHeading arrival)
    {
        Position[] predicted = memory.Predicted(lens, enemy);
        bool onCurrent = enemy.Position == tile;
        bool onPredicted = false;
        foreach (Position candidate in predicted)
        {
            if (candidate == tile)
                onPredicted = true;
        }
        if (!onCurrent && !onPredicted)
            return (0, false);

        int share = predicted.Length <= 1
            ? 100
            : 100 / predicted.Length + (onCurrent ? 15 : 0);
        // Flight time is uncertainty, not futility: a bolt three ticks out at a
        // body that has not moved in three ticks is a good bolt.
        int score = share - (offset > 1 ? (offset - 1) * 6 : 0);
        if (enemy.Health <= attack.Projectile.DamagePerHit)
            score += 70;
        foreach (Position objective in ActiveObjective(lens, context))
        {
            if (objective == enemy.Position)
            {
                score += 20;
                break;
            }
        }

        if (!lens.Guards(enemy.FormId)
            || !StoneContract.ArcCovers(enemy.Facing, arrival))
        {
            return (Math.Max(score, 0), false);
        }

        // The arc eats this bolt and sends a live one back down our own ray.
        // Only worth it when the contact is the one that shatters the shield
        // and we can afford the return.
        int? budget = lens.GuardBudget(enemy.FormId);
        bool shatters = budget is int threshold
            && memory.DeflectionsSpent(enemy.ActorId) + 1 >= threshold;
        return shatters && context.Self.Health > attack.Projectile.DamagePerHit
            ? (40, true)
            : (0, false);
    }

    /// <summary>
    /// Tick offset at which a bolt reaches the nth tile of its path, from the
    /// declared launch length and advance cadence rather than from habit.
    /// </summary>
    private static int ArrivalOffset(
        GenericActorRulesContract.AttackProfile attack,
        int tileNumber)
    {
        int launch = Math.Max(attack.Projectile.LaunchTiles, 1);
        if (tileNumber <= launch)
            return 0;
        int perAdvance = Math.Max(attack.Projectile.TilesPerAdvance, 1);
        int advances = (tileNumber - launch + perAdvance - 1) / perAdvance;
        return advances * Math.Max(attack.Projectile.TicksPerAdvance, 1);
    }

    /// <summary>Every legal curve this envelope declares, bounded.</summary>
    private static List<ShotProgram> Programs(
        GenericActorRulesContract.ShotProgramDefinition envelope)
    {
        var programs = new List<ShotProgram>();
        GenericActorRulesContract.AimOnlyShotProgramValue flat =
            envelope.AimOnlyProgram;
        for (int aim = envelope.MinInitialAimSteps;
             aim <= envelope.MaxInitialAimSteps;
             aim++)
        {
            if (aim != 0)
            {
                programs.Add(new ShotProgram(
                    aim,
                    flat.BendDirection,
                    flat.BendAfterTiles,
                    flat.BendEveryTiles,
                    flat.BendCount));
            }
            foreach (int bend in envelope.AllowedCurvedBendDirections)
            {
                for (int after = envelope.MinBendAfterTiles;
                     after <= envelope.MaxBendAfterTiles;
                     after++)
                {
                    for (int every = envelope.MinBendEveryTiles;
                         every <= envelope.MaxBendEveryTiles;
                         every++)
                    {
                        for (int count = envelope.MinBendCount;
                             count <= envelope.MaxBendCount;
                             count++)
                        {
                            if (programs.Count >= MaxProgramCandidates)
                                return programs;
                            programs.Add(new ShotProgram(
                                aim,
                                bend,
                                after,
                                every,
                                count));
                        }
                    }
                }
            }
        }
        return programs;
    }

    /// <summary>Tiles an eight-way absolute shot would enter, walls stopping it.</summary>
    public static Position[] Ray(
        StoneContract lens,
        Position origin,
        ProjectileHeading heading,
        int maxTiles,
        bool strictCorners)
    {
        var tiles = new List<Position>();
        (int dx, int dy) = heading.Vector();
        Position cursor = origin;
        for (int step = 0; step < maxTiles; step++)
        {
            Position next = cursor.Offset(dx, dy);
            if (lens.IsWall(next))
                break;
            if (strictCorners
                && dx != 0
                && dy != 0
                && (lens.IsWall(cursor.Offset(dx, 0))
                    || lens.IsWall(cursor.Offset(0, dy))))
            {
                break;
            }
            cursor = next;
            tiles.Add(cursor);
        }
        return tiles.ToArray();
    }

    /// <summary>Eight-way heading from one tile toward an adjacent tile.</summary>
    public static ProjectileHeading Toward(Position from, Position to)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        return (dx, dy) switch
        {
            (0, -1) => ProjectileHeading.North,
            (1, -1) => ProjectileHeading.NorthEast,
            (1, 0) => ProjectileHeading.East,
            (1, 1) => ProjectileHeading.SouthEast,
            (0, 1) => ProjectileHeading.South,
            (-1, 1) => ProjectileHeading.SouthWest,
            (-1, 0) => ProjectileHeading.West,
            _ => ProjectileHeading.NorthWest,
        };
    }

    /// <summary>Tiles of the objective currently under contest.</summary>
    public static Position[] ActiveObjective(
        StoneContract lens,
        GenericActorContext context) =>
        context.Mode
            is GenericActorContext.ModeObservationState.Frontline mode
            ? lens.Objective(mode.ActivePositionIndex)
            : [];

    private static GenericActorActionLegality.ArgumentConstraint
        .ProjectileHeadingConstraint? Heading(
            GenericActorActionLegality legality)
    {
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in legality.Constraints)
        {
            if (constraint
                is GenericActorActionLegality.ArgumentConstraint
                    .ProjectileHeadingConstraint headings)
            {
                return headings;
            }
        }
        return null;
    }

    private static GenericActorActionLegality.ArgumentConstraint
        .ShotProgramConstraint? Program(GenericActorActionLegality legality)
    {
        foreach (GenericActorActionLegality.ArgumentConstraint constraint
                 in legality.Constraints)
        {
            if (constraint
                is GenericActorActionLegality.ArgumentConstraint
                    .ShotProgramConstraint program)
            {
                return program;
            }
        }
        return null;
    }
}
