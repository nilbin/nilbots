using BotArena.Sdk;

namespace BotArena.Guest;

internal sealed record GenericActorMatchStartEnvelope(
    string BotName,
    GenericActorMatchStart Start);
