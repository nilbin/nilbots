using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// The team-shared half of a mind observation, computed ONCE per scoring team
/// per tick and handed to every mind on that team
/// (<c>docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md</c> §4.6).
/// <para>
/// This object is the whole architectural win. Under the per-life generation
/// <c>ProjectObservation</c> ran once per live actor with no per-team
/// memoization: <c>VisibleTilesFor</c> scanned the map per sensor,
/// <c>ObserversAt</c> ran per tile per life, <c>SpawnReservationAt</c> ran a
/// <c>SingleOrDefault</c> over the reservation lists per visible tile per life,
/// and <c>ModeWorldView()</c> plus <c>_mode.Project(...)</c> rebuilt the entire
/// world view once per life — producing N byte-identical results. Computing it
/// once turns an <c>O(N^2 x mapArea)</c> per-team-per-tick computation into
/// <c>O(N x mapArea)</c>, which is also what makes a larger map affordable
/// (§15.3).
/// </para>
/// <para>
/// Every member is the EXISTING per-life observation type, unchanged. That is
/// deliberate: it is what makes the §7.2 null pin checkable field by field, and
/// what lets the P2 wrap adapter reconstruct a per-life
/// <c>GenericActorContext</c> as a projection rather than a translation.
/// </para>
/// </summary>
/// <param name="TeamId">The scoring team this union belongs to.</param>
/// <param name="TeamUnits">
/// Every slot on the team, live or not. Team-scoped exactly as today.
/// </param>
/// <param name="Participants">
/// Every participant's status, including fault counts and disqualification.
/// Global, not team-scoped, but delivered here with the rest of the once-work.
/// </param>
public sealed record GenericMindTeamProjection(
    int TeamId,
    ImmutableArray<GenericActorRuntimeObservation.ObservedUnitSlot> TeamUnits,
    ImmutableArray<GenericActorRuntimeObservation.ObservedParticipantStatus>
        Participants,
    ImmutableArray<GenericActorRuntimeObservation.ObservedEnemyState> Enemies,
    ImmutableArray<GenericActorRuntimeObservation.ObservedTile> VisibleTiles,
    ImmutableArray<GenericActorRuntimeObservation.ObservedProjectile>?
        VisibleProjectiles,
    ImmutableArray<GenericActorRuntimeObservation.ObservedEvent> VisibleEvents,
    ImmutableArray<GenericActorRuntimeObservation.ObservedSound>? HeardSounds,
    GenericActorRuntimeObservation.ScoreboardState Scoreboard,
    GenericActorRuntimeObservation.ModeObservationState Mode);
