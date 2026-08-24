/// <summary>
/// The squad-wide operation state machine for program (v3) sheets. Exactly
/// one tactic and one of its phases own the team at a time; instructions
/// light up through their phase tags. Latching is structural: an entered
/// phase is never re-checked against its entry condition, transitions are
/// forward-only, and at most one transition applies per tick. Selection runs
/// only when no tactic is active — an ordinary higher-priority tactic can
/// never shadow, pause, or terminate a running operation.
/// </summary>
internal sealed class TacticsProgramCoordinator
{
    private static readonly HashSet<string> EmptyTags =
        new(StringComparer.Ordinal);

    private readonly OrdersPackage.SquadProgram _program;

    // A tactic may not be reselected on the very tick it ended (tick+1+rearm,
    // the order runtime's own cooldown convention): the ended run's per-body
    // node states clear lazily on the next dark evaluation pass, and that
    // pass needs its tick.
    private readonly Dictionary<string, int> _rearmReadyTick =
        new(StringComparer.Ordinal);

    private OrdersPackage.ProgramTactic? _active;
    private int _tacticEntryTick;
    private int _phaseIndex;
    private int _phaseEntryTick;

    public TacticsProgramCoordinator(OrdersPackage.SquadProgram program) =>
        _program = program;

    public string? ActiveTacticId => _active?.Id;

    public string? ActivePhaseId => _active?.Phases[_phaseIndex].Id;

    public View Step(
        int tick,
        Func<OrdersPackage.ConditionExpression, int,
            OrderTreeRuntime.ConditionEvaluation> evaluate)
    {
        var events = new List<string>();
        OrdersPackage.ProgramTactic? pickupTactic = _active;
        int pickupPhaseIndex = _phaseIndex;

        bool CaseTrue(OrdersPackage.ProgramCase value, int anchorTicks) =>
            (value.All is null || evaluate(value.All, anchorTicks).Value)
            && (value.Any is null || evaluate(value.Any, anchorTicks).Value);

        bool AnyCase(
            OrdersPackage.ProgramCase[]? cases,
            int anchorTicks,
            out int index)
        {
            index = -1;
            if (cases is null)
                return false;
            for (int candidate = 0; candidate < cases.Length; candidate++)
            {
                if (CaseTrue(cases[candidate], anchorTicks))
                {
                    index = candidate;
                    return true;
                }
            }
            return false;
        }

        void End(string reason)
        {
            _rearmReadyTick[_active!.Id] = checked(tick + 1 + _active.Rearm);
            events.Add(reason);
            _active = null;
        }

        void Select()
        {
            foreach (OrdersPackage.ProgramTactic tactic in _program.Tactics)
            {
                if (_rearmReadyTick.GetValueOrDefault(tactic.Id) > tick)
                    continue;
                // Null start = always eligible. Anchor 0 makes a phase-entry
                // condition in a start case degenerate to "since this tick"
                // (empty); rolling windowTicks conditions read normally.
                if (tactic.Start is not null
                    && !AnyCase(tactic.Start, 0, out _))
                    continue;
                _active = tactic;
                _tacticEntryTick = tick;
                _phaseIndex = 0;
                _phaseEntryTick = tick;
                events.Add($"begin-{tactic.Id}");
                return;
            }
        }

        if (_active is null)
        {
            Select();
            // A pickup that selects a previously idle Tactic belongs to its
            // newly entered first phase. For a running Tactic, the snapshot
            // above deliberately remains the phase active before this tick's
            // transition/abort evaluation: the pickup event caused by the
            // preceding world step happened under that phase.
            pickupTactic = _active;
            pickupPhaseIndex = _phaseIndex;
        }
        // A tactic selected this very tick starts executing now, but its
        // clocks and transitions wait for the next tick — one transition
        // per tick, and never on the entry tick.
        if (_active is OrdersPackage.ProgramTactic active
            && _tacticEntryTick != tick)
        {
            int phaseTicks = tick - _phaseEntryTick;
            if (active.MaxTicks is int maxTicks
                && tick - _tacticEntryTick >= maxTicks)
            {
                End($"max-duration-{active.Id}");
                Select();
            }
            else if ((active.AbortThroughPhase is null
                    || _phaseIndex <= Array.FindIndex(
                        active.Phases,
                        phase => phase.Id == active.AbortThroughPhase))
                && AnyCase(active.Abort, phaseTicks, out int abortCase))
            {
                End($"abort-{active.Id}[c{abortCase + 1}]");
                Select();
            }
            else
            {
                OrdersPackage.TransitionRule[] rules =
                    active.Phases[_phaseIndex].Continue;
                for (int rule = 0; rule < rules.Length; rule++)
                {
                    if (!CaseTrue(rules[rule].When, phaseTicks))
                        continue;
                    string to = rules[rule].To;
                    if (to == "finish")
                    {
                        End($"finish-{active.Id}[c{rule + 1}]");
                        Select();
                    }
                    else
                    {
                        int target = to == "next"
                            ? _phaseIndex + 1
                            : Array.FindIndex(
                                active.Phases, phase => phase.Id == to);
                        events.Add(
                            $"go-{active.Phases[target].Id}[c{rule + 1}]");
                        _phaseIndex = target;
                        _phaseEntryTick = tick;
                    }
                    break;
                }
            }
        }
        IReadOnlySet<string> tags = _active is null
            ? EmptyTags
            : new HashSet<string>(StringComparer.Ordinal)
            {
                _active.Id,
                $"{_active.Id}/{_active.Phases[_phaseIndex].Id}",
            };
        return new View(
            tags,
            ActiveTacticId,
            ActivePhaseId,
            _active is null ? 0 : tick - _phaseEntryTick,
            events.Count == 0 ? null : string.Join("+", events),
            _active is null
                ? []
                : _active.Phases[_phaseIndex].CustodyGroups,
            pickupTactic?.Id,
            pickupTactic is null
                ? null
                : pickupTactic.Phases[pickupPhaseIndex].Id,
            pickupTactic is null
                ? []
                : pickupTactic.Phases[pickupPhaseIndex].CustodyGroups);
    }

    /// <summary>What the mind needs each tick: which tags are lit, the
    /// active pair for attribution, ticks since phase entry (the phase-entry
    /// evidence anchor), and this tick's transition marker if any.</summary>
    internal sealed record View(
        IReadOnlySet<string> EnabledTags,
        string? TacticId,
        string? PhaseId,
        int PhaseTicks,
        string? Marker,
        int[][] CustodyGroups,
        string? PickupTacticId,
        string? PickupPhaseId,
        int[][] PickupCustodyGroups);
}
