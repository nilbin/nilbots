namespace BotArena.Guest;

internal enum ActorGuestContractGeneration
{
    None = 0,
    LegacyActorV1 = 1,
    GenericActorV2 = 2,
    /// <summary>
    /// The participant-scoped mind profile. An artifact reaches it either by
    /// implementing <c>IGenericMindBot</c> directly or through the
    /// <see cref="WrappedPerLifeMind"/> facade, which hosts an ordinary
    /// per-life bot with no source edits.
    /// </summary>
    GenericMindV1 = 3,
}
