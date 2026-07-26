namespace BotArena.Engine;

/// <summary>
/// Pure deterministic Frontline pressure, decay, advancement, redeploy, and
/// breach math. Combat code supplies only binary eligible team presence.
/// </summary>
public static class FrontlineControlSystem
{
    public static FrontlineControlState CreateInitial(
        FrontlineRules rules,
        int firstTick = 0)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ValidateControlRules(rules);
        if (firstTick < 0)
            throw new ArgumentOutOfRangeException(nameof(firstTick));

        return new FrontlineControlState(
            NextTick: firstTick,
            ActivePositionIndex: rules.FrontlinePositionCount / 2,
            ClaimingTeamId: null,
            CaptureProgress: 0,
            DecayTicksElapsed: 0,
            ControlResumesAtTick: firstTick,
            WinnerTeamId: null);
    }

    public static FrontlineControlStepResult Step(
        FrontlineRules rules,
        FrontlineControlState state,
        int tick,
        FrontlineTeamPresence presence)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(state);
        ValidateControlRules(rules);
        ValidateState(rules, state);

        if (state.WinnerTeamId is not null)
            throw new InvalidOperationException("A breached Frontline match cannot step again.");
        if (tick != state.NextTick)
        {
            throw new ArgumentException(
                $"Expected Frontline tick {state.NextTick}, received {tick}.",
                nameof(tick));
        }

        if (tick < state.ControlResumesAtTick)
        {
            return new FrontlineControlStepResult(
                state with { NextTick = tick + 1 },
                Transition: null);
        }

        int? soleTeamId = presence.SoleTeamId;
        if (soleTeamId is int teamId)
            return ApplySoleControl(rules, state, tick, teamId);

        return ApplyDecay(rules, state, tick);
    }

    private static FrontlineControlStepResult ApplySoleControl(
        FrontlineRules rules,
        FrontlineControlState state,
        int tick,
        int teamId)
    {
        int? claimingTeamId = state.ClaimingTeamId;
        int progress = state.CaptureProgress;

        if (claimingTeamId is null)
        {
            claimingTeamId = teamId;
            progress = rules.CaptureGainPerSoleTeamTick;
        }
        else if (claimingTeamId == teamId)
        {
            long increased =
                (long)progress + rules.CaptureGainPerSoleTeamTick;
            if (increased >= rules.CaptureThreshold)
                return CompleteCapture(rules, state, tick, teamId);
            progress = (int)increased;
        }
        else
        {
            long remaining =
                (long)progress - rules.CaptureGainPerSoleTeamTick;
            if (remaining <= 0)
            {
                claimingTeamId = null;
                progress = 0;
            }
            else
            {
                progress = (int)remaining;
            }
        }

        if (claimingTeamId == teamId && progress >= rules.CaptureThreshold)
            return CompleteCapture(rules, state, tick, teamId);

        return new FrontlineControlStepResult(
            state with
            {
                NextTick = tick + 1,
                ClaimingTeamId = claimingTeamId,
                CaptureProgress = progress,
                DecayTicksElapsed = 0,
            },
            Transition: null);
    }

    private static FrontlineControlStepResult ApplyDecay(
        FrontlineRules rules,
        FrontlineControlState state,
        int tick)
    {
        if (state.ClaimingTeamId is null)
        {
            return new FrontlineControlStepResult(
                state with
                {
                    NextTick = tick + 1,
                    DecayTicksElapsed = 0,
                },
                Transition: null);
        }

        int decayTicks = state.DecayTicksElapsed + 1;
        if (decayTicks < rules.CaptureDecayIntervalTicks)
        {
            return new FrontlineControlStepResult(
                state with
                {
                    NextTick = tick + 1,
                    DecayTicksElapsed = decayTicks,
                },
                Transition: null);
        }

        int progress = Math.Max(
            0,
            state.CaptureProgress - rules.CaptureDecayAmount);
        return new FrontlineControlStepResult(
            state with
            {
                NextTick = tick + 1,
                ClaimingTeamId = progress == 0 ? null : state.ClaimingTeamId,
                CaptureProgress = progress,
                DecayTicksElapsed = 0,
            },
            Transition: null);
    }

    private static FrontlineControlStepResult CompleteCapture(
        FrontlineRules rules,
        FrontlineControlState state,
        int tick,
        int teamId)
    {
        int direction = teamId == 0 ? 1 : -1;
        int breachEdge = teamId == 0
            ? rules.FrontlinePositionCount - 1
            : 0;
        if (state.ActivePositionIndex == breachEdge)
        {
            return new FrontlineControlStepResult(
                state with
                {
                    NextTick = tick + 1,
                    ClaimingTeamId = null,
                    CaptureProgress = 0,
                    DecayTicksElapsed = 0,
                    WinnerTeamId = teamId,
                },
                new FrontlineBaseBreached(
                    tick,
                    teamId,
                    state.ActivePositionIndex));
        }

        int nextPosition = state.ActivePositionIndex + direction;
        return new FrontlineControlStepResult(
            state with
            {
                NextTick = tick + 1,
                ActivePositionIndex = nextPosition,
                ClaimingTeamId = null,
                CaptureProgress = 0,
                DecayTicksElapsed = 0,
                ControlResumesAtTick = tick + 1 + rules.RedeployPauseTicks,
            },
            new FrontlinePositionAdvanced(
                tick,
                teamId,
                state.ActivePositionIndex,
                nextPosition));
    }

    private static void ValidateControlRules(FrontlineRules rules)
    {
        if (rules.TeamCount != 2)
            throw new ArgumentException("Frontline control requires exactly two teams.", nameof(rules));
        if (rules.FrontlinePositionCount < 3
            || rules.FrontlinePositionCount % 2 == 0
            || rules.FrontlinePositionCount != (rules.PushesToBreach * 2) - 1)
        {
            throw new ArgumentException(
                "Frontline positions must be odd and match PushesToBreach.",
                nameof(rules));
        }
        if (rules.CaptureThreshold <= 0
            || rules.CaptureGainPerSoleTeamTick <= 0
            || rules.CaptureDecayAmount <= 0
            || rules.CaptureDecayIntervalTicks <= 0
            || rules.RedeployPauseTicks < 0)
        {
            throw new ArgumentException(
                "Frontline capture, decay, and redeploy values are invalid.",
                nameof(rules));
        }
    }

    private static void ValidateState(
        FrontlineRules rules,
        FrontlineControlState state)
    {
        bool invalid =
            state.NextTick < 0
            || state.ActivePositionIndex < 0
            || state.ActivePositionIndex >= rules.FrontlinePositionCount
            || state.ClaimingTeamId is not (null or 0 or 1)
            || state.WinnerTeamId is not (null or 0 or 1)
            || state.CaptureProgress < 0
            || state.CaptureProgress >= rules.CaptureThreshold
            || state.DecayTicksElapsed < 0
            || state.DecayTicksElapsed >= rules.CaptureDecayIntervalTicks
            || state.ControlResumesAtTick < 0
            || state.ClaimingTeamId is null && state.CaptureProgress != 0
            || state.ClaimingTeamId is null && state.DecayTicksElapsed != 0
            || state.ClaimingTeamId is not null && state.CaptureProgress == 0
            || state.NextTick < state.ControlResumesAtTick
                && (state.ClaimingTeamId is not null
                    || state.CaptureProgress != 0
                    || state.DecayTicksElapsed != 0)
            || state.WinnerTeamId is int winnerTeamId
                && (state.ActivePositionIndex !=
                        (winnerTeamId == 0
                            ? rules.FrontlinePositionCount - 1
                            : 0)
                    || state.ClaimingTeamId is not null
                    || state.CaptureProgress != 0
                    || state.DecayTicksElapsed != 0
                    || state.ControlResumesAtTick > state.NextTick);

        if (invalid)
        {
            throw new ArgumentException(
                "Frontline control state violates its canonical invariants.",
                nameof(state));
        }
    }
}
