using BotArena.Bots.BuiltIn;
using BotArena.Guest;
using BotArena.WasmGuest;

// The built-in opponents artifact: hosts every catalog bot plus deliberately
// hostile test bots used by the runtime contract tests. Player artifacts use
// the single-bot GuestHost.Run(() => new TheirBot()) form instead.
return GuestHost.Run(
    name => name switch
    {
        "guest-faulty" => new FaultyBot(),
        "guest-hog" => new HogBot(),
        "guest-clock" => new ClockBot(),
        "guest-arc" => new ArcProtocolBot(),
        _ => BuiltInBotCatalog.Create(name),
    },
    BuiltInActorBotCatalog.Create);
