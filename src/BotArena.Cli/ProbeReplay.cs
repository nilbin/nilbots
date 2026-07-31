using System.Text.Json;

namespace BotArena.Cli;

/// <summary>
/// One body's tick as a qualification analyzer needs to see it, read from
/// EITHER turn shape a replay-v3 document can carry.
///
/// <para>A per-life document stores one <c>actorTurns</c> entry per live body,
/// each embedding that body's own observation. A mind document stores one
/// <c>mindTurns</c> entry per PARTICIPANT, with the team-shared union carried
/// once and one resolution per own live body. The probes measure behaviour —
/// did it shoot, did it hold the point, did it dodge — and behaviour is the
/// same question on both, so the analyzers should not have to care which
/// document they were handed. This is the specialization that lets them not
/// care, and it is the same projection the replay viewer performs.</para>
///
/// <para>What it deliberately does NOT do is hide the difference where the
/// difference is real: <see cref="Observation"/> on a mind turn is the mind's
/// union rather than one body's private view, because under the mind there is
/// no per-body view — that is a fact about the profile, not an artefact of
/// this projection.</para>
/// </summary>
internal readonly record struct ProbeTurn(
    int Tick,
    int ParticipantId,
    int UnitId,
    int LifeId,
    JsonElement Observation,
    JsonElement Self,
    JsonElement SubmittedDecision,
    JsonElement ActionResolution);

internal static class ProbeReplay
{
    /// <summary>
    /// Every body's turn in one recorded tick, in document order, whichever
    /// turn shape the document carries.
    /// </summary>
    public static IEnumerable<ProbeTurn> Turns(JsonElement tick)
    {
        if (tick.TryGetProperty("actorTurns", out JsonElement actorTurns)
            && actorTurns.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement turn in actorTurns.EnumerateArray())
            {
                JsonElement observation = turn.GetProperty("observation");
                JsonElement self = observation.GetProperty("self");
                JsonElement actorId = self.GetProperty("actorId");
                yield return new ProbeTurn(
                    turn.GetProperty("tick").GetInt32(),
                    turn.GetProperty("participantId").GetInt32(),
                    actorId.GetProperty("unitId").GetInt32(),
                    actorId.GetProperty("lifeId").GetInt32(),
                    observation,
                    self,
                    turn.GetProperty("submittedDecision"),
                    turn.GetProperty("actionResolution"));
            }
            yield break;
        }

        if (!tick.TryGetProperty("mindTurns", out JsonElement mindTurns)
            || mindTurns.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement turn in mindTurns.EnumerateArray())
        {
            int participantId = turn.GetProperty("participantId").GetInt32();
            int turnTick = turn.GetProperty("tick").GetInt32();
            JsonElement observation = turn.GetProperty("observation");
            var bodies = new Dictionary<(int, int), JsonElement>();
            foreach (JsonElement body in observation
                         .GetProperty("bodies")
                         .EnumerateArray())
            {
                JsonElement actorId = body.GetProperty("actorId");
                bodies[(
                    actorId.GetProperty("unitId").GetInt32(),
                    actorId.GetProperty("lifeId").GetInt32())] = body;
            }

            foreach (JsonElement resolution in turn
                         .GetProperty("resolutions")
                         .EnumerateArray())
            {
                int unitId = resolution.GetProperty("unitId").GetInt32();
                int lifeId = resolution.GetProperty("lifeId").GetInt32();
                if (!bodies.TryGetValue(
                        (unitId, lifeId),
                        out JsonElement body))
                {
                    continue;
                }
                yield return new ProbeTurn(
                    turnTick,
                    participantId,
                    unitId,
                    lifeId,
                    observation,
                    body,
                    resolution.GetProperty("submittedDecision"),
                    resolution.GetProperty("actionResolution"));
            }
        }
    }

    /// <summary>
    /// Whether the mind that owns <paramref name="tick"/>'s turns trapped on
    /// it. A per-life fault is recorded on the body's own turn and is already
    /// visible through <see cref="ProbeTurn.ActionResolution"/>; a mind fault
    /// is participant-scoped and can land on a tick where the participant owns
    /// no body at all, which is precisely the tick a per-body scan would miss.
    /// </summary>
    public static bool MindFaulted(JsonElement tick, int participantId)
    {
        if (!tick.TryGetProperty("mindTurns", out JsonElement mindTurns)
            || mindTurns.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (JsonElement turn in mindTurns.EnumerateArray())
        {
            if (turn.GetProperty("participantId").GetInt32() != participantId)
                continue;
            if (turn.GetProperty("runtimeFault").ValueKind
                != JsonValueKind.Null)
            {
                return true;
            }
        }
        return false;
    }
}
