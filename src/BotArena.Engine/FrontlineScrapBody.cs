namespace BotArena.Engine;

/// <summary>
/// One post-combat body as the scrap kernel reads it: who it is, what form it
/// is wearing at the mode phase (which is what decides whether it participates
/// in the economy at all), and the tile it is standing on. Deliberately its own
/// small public record rather than the internal mode-seam life, so the kernel
/// stays directly testable and the seam stays internal.
/// </summary>
/// <param name="ActorId">Canonical life identity.</param>
/// <param name="FormId">The form this body wears at the mode phase.</param>
/// <param name="Position">Where it ends this tick.</param>
public sealed record FrontlineScrapBody(
    ActorIdentity ActorId,
    string FormId,
    Position Position);

/// <summary>
/// One body destroyed this tick, and the tile it died on. Wreckage needs the
/// destruction itself rather than the damage contact that caused it: a body
/// can die to a contact whose position is not where the body was standing, and
/// the wreck belongs on the death tile.
/// </summary>
/// <param name="ActorId">The destroyed life.</param>
/// <param name="Position">Its death tile.</param>
public sealed record FrontlineScrapDestruction(
    ActorIdentity ActorId,
    Position Position,
    ActorIdentity? SourceActorId = null);
