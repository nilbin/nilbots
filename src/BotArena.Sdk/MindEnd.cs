namespace BotArena.Sdk;

/// <summary>
/// Delivered once, after the terminal tick, to
/// <see cref="IGenericMindBot.EndMatch"/>. Nothing done in response can affect
/// the match — the simulation is already complete and its replay already
/// written — so this exists purely so a mind can flush its own diagnostics.
/// </summary>
/// <param name="Reason">
/// The host's short, non-authoritative label for why the exchange ended. Do not
/// branch on its exact text: it is a diagnostic string, not a typed completion
/// reason, and the authoritative outcome lives in the match result rather than
/// in the guest.
/// </param>
public sealed record MindEnd(string Reason);
