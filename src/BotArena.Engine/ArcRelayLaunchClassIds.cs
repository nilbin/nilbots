using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>The approved Arc Relay launch-band class identifiers.</summary>
public static class ArcRelayLaunchClassIds
{
    public const string Kestrel = "kestrel";
    public const string Palisade = "palisade";
    public const string Towline = "towline";
    public const string Patchbay = "patchbay";
    public const string Lantern = "lantern";
    public const string Mortar = "mortar";
    public const string Minesmith = "minesmith";
    public const string Hush = "hush";
    public const string Relay = "relay";
    public const string Switchback = "switchback";
    public const string Longshot = "longshot";
    public const string Mason = "mason";
    public const string Sunder = "sunder";
    public const string Repulsor = "repulsor";
    public const string Veil = "veil";
    public const string Nest = "nest";

    public static ImmutableArray<string> All { get; } =
    [
        Kestrel,
        Palisade,
        Towline,
        Patchbay,
        Lantern,
        Mortar,
        Minesmith,
        Hush,
        Relay,
        Switchback,
        Longshot,
        Mason,
        Sunder,
        Repulsor,
        Veil,
        Nest,
    ];
}
