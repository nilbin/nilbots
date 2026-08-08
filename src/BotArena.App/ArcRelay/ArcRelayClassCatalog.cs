using BotArena.Engine;

namespace BotArena.App.ArcRelay;

public sealed record ArcRelayClassDefinition(
    string Id,
    string Name,
    string SignatureName,
    string Fantasy,
    bool Starter);

/// <summary>
/// Product catalog for the approved sixteen-class launch band. Unlocks add
/// tactical breadth only; no entry carries an upgrade tier or power value.
/// </summary>
public sealed class ArcRelayClassCatalog
{
    public const string EntitlementPrefix = "arc-relay-class:";

    public static ArcRelayClassCatalog Default { get; } = new();

    private readonly IReadOnlyDictionary<string, ArcRelayClassDefinition>
        byId;

    private ArcRelayClassCatalog()
    {
        All =
        [
            Class(ArcRelayLaunchClassIds.Kestrel, "Kestrel", "Vector Dash",
                "Rapid interceptor built to abandon one lane and answer another.", true),
            Class(ArcRelayLaunchClassIds.Palisade, "Palisade", "Prism Wall",
                "Convoy shield whose projector face blanks a firing lane.", true),
            Class(ArcRelayLaunchClassIds.Towline, "Towline", "Tractor Hook",
                "Formation disruptor with a visible winch, cable and hook.", true),
            Class(ArcRelayLaunchClassIds.Patchbay, "Patchbay", "Repair Beam",
                "Field medic that trades its own gun tempo to preserve an ally.", true),
            Class(ArcRelayLaunchClassIds.Lantern, "Lantern", "Survey Flare",
                "Mobile sensor mast that buys certainty before a commitment.", true),
            Class(ArcRelayLaunchClassIds.Mortar, "Mortar", "Falling Star",
                "Lobbing artillery that punishes formations which stay put.", false),
            Class(ArcRelayLaunchClassIds.Minesmith, "Minesmith", "Trip Node",
                "Route controller carrying visible nodes for watched ground.", false),
            Class(ArcRelayLaunchClassIds.Hush, "Hush", "Null Field",
                "Dampener array that suppresses signatures instead of racing damage.", true),
            Class(ArcRelayLaunchClassIds.Relay, "Relay", "Arc Toss",
                "Core runner with a cradle and arms for one risky long handoff.", true),
            Class(ArcRelayLaunchClassIds.Switchback, "Switchback", "Exchange",
                "Paired mirrored frame that swaps which ally occupies danger.", true),
            Class(ArcRelayLaunchClassIds.Longshot, "Longshot", "Rail Line",
                "A body-length rail that turns a public corridor into a threat.", false),
            Class(ArcRelayLaunchClassIds.Mason, "Mason", "Hardlight Block",
                "Builder rig that bends a route with temporary cover.", false),
            Class(ArcRelayLaunchClassIds.Sunder, "Sunder", "Target Paint",
                "Designator optics that tell a team which target matters now.", false),
            Class(ArcRelayLaunchClassIds.Repulsor, "Repulsor", "Kinetic Burst",
                "Radial emitters that break a local formation from adjacency.", false),
            Class(ArcRelayLaunchClassIds.Veil, "Veil", "Smoke Canister",
                "Smoke-launcher skirmisher that authors uncertainty on the map.", false),
            Class(ArcRelayLaunchClassIds.Nest, "Nest", "Sentinel Seed",
                "Pod carrier that leaves one persistent, killable guard behind.", false),
        ];
        byId = All.ToDictionary(value => value.Id, StringComparer.Ordinal);
        if (!All.Select(value => value.Id).SequenceEqual(
                ArcRelayLaunchClassIds.All))
        {
            throw new InvalidOperationException(
                "Arc Relay product catalog must exactly cover the engine launch band.");
        }
    }

    public IReadOnlyList<ArcRelayClassDefinition> All { get; }

    public IReadOnlySet<string> StarterIds { get; } = new HashSet<string>(
        [
            ArcRelayLaunchClassIds.Kestrel,
            ArcRelayLaunchClassIds.Palisade,
            ArcRelayLaunchClassIds.Towline,
            ArcRelayLaunchClassIds.Patchbay,
            ArcRelayLaunchClassIds.Lantern,
            ArcRelayLaunchClassIds.Hush,
            ArcRelayLaunchClassIds.Relay,
            ArcRelayLaunchClassIds.Switchback,
        ],
        StringComparer.Ordinal);

    public bool Contains(string id) => byId.ContainsKey(id);

    public ArcRelayClassDefinition Get(string id) =>
        byId.TryGetValue(id, out ArcRelayClassDefinition? value)
            ? value
            : throw new ArgumentException(
                $"Unknown Arc Relay class '{id}'.",
                nameof(id));

    public static string EntitlementKey(string classId) =>
        EntitlementPrefix + classId;

    private static ArcRelayClassDefinition Class(
        string id,
        string name,
        string signatureName,
        string fantasy,
        bool starter) =>
        new(id, name, signatureName, fantasy, starter);
}
