using BotArena.Sdk;

/// <summary>
/// The little a single life is allowed to remember. Private memory is
/// life-scoped — a respawn starts empty — so nothing here may be load-bearing:
/// every entry is an accelerator for a decision the observation can already
/// justify on its own.
/// </summary>
internal sealed class StoneMemory
{
    private readonly Dictionary<string, Position> _lastSeen =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, (int Dx, int Dy)> _lastStep =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _lastForm =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _deflections =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _stillFor =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Position, int> _refused = new();
    private readonly Dictionary<string, Position> _wasAt =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);
    private int _eventCursorTick = -1;
    private int _eventCursorOrdinal = -1;
    private string? _form;
    private int _formSince;
    private int _cycles;

    /// <summary>Tile this life vacated to dodge, avoided for one more tick.</summary>
    public Position? DodgeOrigin { get; private set; }
    /// <summary>Last tick on which <see cref="DodgeOrigin"/> still applies.</summary>
    public int AvoidThroughTick { get; private set; } = -1;

    /// <summary>Records a dodge so objective pathing cannot walk back into it.</summary>
    public void NoteDodge(Position from, int tick)
    {
        DodgeOrigin = from;
        AvoidThroughTick = tick + 1;
    }

    /// <summary>
    /// A tile that refused this body twice is treated as a wall for the rest of
    /// the life. Some obstructions are invisible in the observation — a slot's
    /// authored return anchor is reserved against its own team's children, and
    /// an opposing protected pad refuses ground entry — and the legality mask
    /// cannot promise the outcome of joint resolution either. One refusal is
    /// traffic; two is architecture.
    /// </summary>
    public void NoteRefused(Position tile)
    {
        _refused.TryGetValue(tile, out int count);
        _refused[tile] = count + 1;
    }

    /// <summary>Whether a tile has refused this body often enough to be a wall.</summary>
    public bool Refused(Position tile) =>
        _refused.TryGetValue(tile, out int count) && count >= 2;

    /// <summary>Tiles this life should not step back onto right now.</summary>
    public Position[] Avoided(int tick) =>
        DodgeOrigin is Position tile && tick <= AvoidThroughTick
            ? [tile]
            : [];

    /// <summary>
    /// Folds this tick's observation into memory: enemy displacement for
    /// prediction, and deflections charged against each guarding enemy so we
    /// know which shell is one bolt from breaking.
    /// </summary>
    public void Observe(StoneContract lens, GenericActorContext context)
    {
        // STILLNESS, one tick late. The channel counts a body toward a claim
        // when its tile did not change this tick; this tick's moves have not
        // happened when the observation freezes, so the honest observable is
        // "its published tile equals the tile it published last tick". A body
        // with no previous tile counts as stationary, which is also exactly
        // what the rule says about the tick a life spawns.
        _held.Clear();
        Hold(context.Self.ActorId, context.Self.Position);
        foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
            Hold(ally.ActorId, ally.Position);
        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
            Hold(enemy.ActorId, enemy.Position);

        // A same-life form change preserves private memory, which is exactly
        // what makes an UNLIMITED anchor/mobilize cycle authorable: the body
        // that comes back out of a turret remembers how long it was in there
        // and how many times it has been round. Without that, a reversible
        // stance is an invitation to spend every tick transforming.
        if (!string.Equals(_form, context.Self.FormId, StringComparison.Ordinal))
        {
            if (_form is not null)
                _cycles++;
            _form = context.Self.FormId;
            _formSince = context.Tick;
        }

        foreach (GenericActorContext.ObservedEnemyState enemy in context.Enemies)
        {
            string key = enemy.ActorId.ToString();
            if (_lastForm.TryGetValue(key, out string? previousForm)
                && !string.Equals(
                    previousForm,
                    enemy.FormId,
                    StringComparison.Ordinal))
            {
                // A stance counter never survives its form: entering again
                // starts a fresh budget, so the tally has to reset with it.
                _deflections.Remove(key);
            }
            _lastForm[key] = enemy.FormId;
            if (_lastSeen.TryGetValue(key, out Position previous))
            {
                int dx = enemy.Position.X - previous.X;
                int dy = enemy.Position.Y - previous.Y;
                _lastStep[key] = (dx, dy);
                // A body that has not moved for two observations is evidence, not
                // a coin flip. Spreading a shot's value over five tiles it might
                // step to is how a bot declines a shot it would have landed.
                _stillFor[key] = dx == 0 && dy == 0
                    ? _stillFor.TryGetValue(key, out int still) ? still + 1 : 1
                    : 0;
            }
            _lastSeen[key] = enemy.Position;
        }

        foreach (GenericActorContext.ObservedEvent observed
                 in context.VisibleEvents)
        {
            if (observed.SourceTick < _eventCursorTick
                || (observed.SourceTick == _eventCursorTick
                    && observed.SourceOrdinal <= _eventCursorOrdinal))
            {
                continue;
            }
            _eventCursorTick = observed.SourceTick;
            _eventCursorOrdinal = observed.SourceOrdinal;

            if (observed.Payload
                is GenericActorContext.EventPayload.ProjectileDeflected
                    deflection
                && deflection.TargetActorId.TeamId != lens.TeamId)
            {
                string key = deflection.TargetActorId.ToString();
                _deflections.TryGetValue(key, out int count);
                _deflections[key] = count + 1;
            }
            if (observed.Payload
                is GenericActorContext.EventPayload.FormTransition transition
                && transition.ActorId.TeamId != lens.TeamId)
            {
                _deflections.Remove(transition.ActorId.ToString());
            }
        }
    }

    private void Hold(ActorIdentity actorId, Position position)
    {
        string key = actorId.ToString();
        if (!_wasAt.TryGetValue(key, out Position previous)
            || previous == position)
        {
            _held.Add(key);
        }
        _wasAt[key] = position;
    }

    /// <summary>
    /// Whether a body did not change tile on the transition into this tick —
    /// the observable that decides whether it counts toward a claim. True for
    /// a body we are seeing for the first time, which covers a fresh life and
    /// treats an enemy that just walked into view as the threat it is.
    /// </summary>
    public bool HeldTile(ActorIdentity actorId, Position position) =>
        _held.Contains(actorId.ToString())
        && (!_wasAt.TryGetValue(actorId.ToString(), out Position at)
            || at == position);

    /// <summary>
    /// Ticks this life has held its current form. The windups are the price of
    /// a cycle, so a stance that has not yet been in place longer than the
    /// round trip costs has not yet earned its exit.
    /// </summary>
    public int TicksInForm(int tick) => Math.Max(tick - _formSince, 0);

    /// <summary>Form changes this life has made — the cycle counter.</summary>
    public int Cycles => _cycles;

    /// <summary>
    /// Deflections this enemy's current stance has already spent, as far as we
    /// have seen. A lower bound: an unobserved deflection is not counted, so
    /// the doctrine treats it as "at least".
    /// </summary>
    public int DeflectionsSpent(ActorIdentity enemy) =>
        _deflections.TryGetValue(enemy.ToString(), out int count) ? count : 0;

    /// <summary>
    /// Where an enemy is likely to be after this tick's movement phase. Movement
    /// resolves before combat, so a shot aimed at the tile it stands on now is a
    /// shot at yesterday.
    /// </summary>
    public Position[] Predicted(
        StoneContract lens,
        GenericActorContext.ObservedEnemyState enemy)
    {
        if (lens.Immobile(enemy.FormId)
            || enemy.PendingSameLifeTransition is not null)
        {
            // A stance or a windup cannot move: this is not a guess.
            return [enemy.Position];
        }
        if (_stillFor.TryGetValue(enemy.ActorId.ToString(), out int still)
            && still >= 2)
        {
            // Observed twice in the same tile: treat it as parked until it moves.
            return [enemy.Position];
        }

        var candidates = new List<Position> { enemy.Position };
        GenericActorRulesContract.MovementFacingCoupling coupling =
            lens.Coupling(enemy.FormId);
        if (coupling
            == GenericActorRulesContract.MovementFacingCoupling.FacingLocked)
        {
            (int dx, int dy) = enemy.Facing.Vector();
            Position ahead = enemy.Position.Offset(dx, dy);
            if (!lens.IsWall(ahead))
                candidates.Add(ahead);
            return candidates.ToArray();
        }

        if (_lastStep.TryGetValue(enemy.ActorId.ToString(), out var step)
            && (step.Dx != 0 || step.Dy != 0))
        {
            Position drift = enemy.Position.Offset(step.Dx, step.Dy);
            if (!lens.IsWall(drift))
                candidates.Add(drift);
        }
        foreach (Direction direction in StoneContract.AllCardinals)
        {
            (int dx, int dy) = direction.Vector();
            Position next = enemy.Position.Offset(dx, dy);
            if (!lens.IsWall(next) && !candidates.Contains(next))
                candidates.Add(next);
        }
        return candidates.ToArray();
    }
}
