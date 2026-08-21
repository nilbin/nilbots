using BotArena.Engine;

namespace BotArena.App.ArcRelay;

public sealed record ArcRelayClassDefinition(
    string Id,
    string Name,
    string SignatureId,
    string ActionId,
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
        var mode = (ArcRelayGameModeDefinition)ArcRelayH0Definition
            .CreateRules(ArcRelayLoopProfile.Current)
            .GameMode;
        IReadOnlyDictionary<string, ArcRelaySignatureDefinition> mechanics =
            mode.Signatures.ToDictionary(
                value => value.ClassId,
                StringComparer.Ordinal);

        All =
        [
            Class(mechanics, ArcRelayLaunchClassIds.Kestrel, "Kestrel", "Vector Dash",
                "Rapid interceptor built to abandon one lane and answer another.", true),
            Class(mechanics, ArcRelayLaunchClassIds.Palisade, "Palisade", "Prism Screen",
                "Convoy shield carrying directional cover through a firing lane.", true),
            Class(mechanics, ArcRelayLaunchClassIds.Towline, "Towline", "Tractor Hook",
                "Formation disruptor with a visible winch, cable and hook.", true),
            Class(mechanics, ArcRelayLaunchClassIds.Patchbay, "Patchbay", "Repair Beam",
                "Field medic that trades its own gun tempo to preserve an ally.", true),
            Class(mechanics, ArcRelayLaunchClassIds.Lantern, "Lantern", "Survey Flare",
                "Mobile sensor mast that buys certainty before a commitment.", true),
            Class(mechanics, ArcRelayLaunchClassIds.Mortar, "Mortar", "Falling Star",
                "Lobbing artillery that punishes formations which stay put.", false),
            Class(mechanics, ArcRelayLaunchClassIds.Minesmith, "Minesmith", "Trip Node",
                "Route controller carrying visible nodes for watched ground.", false),
            Class(mechanics, ArcRelayLaunchClassIds.Hush, "Hush", "Null Field",
                "Dampener array that suppresses signatures instead of racing damage.", true),
            Class(mechanics, ArcRelayLaunchClassIds.Relay, "Relay", "Arc Toss",
                "Core runner with a cradle and arms for one risky long handoff.", true),
            Class(mechanics, ArcRelayLaunchClassIds.Switchback, "Switchback", "Exchange",
                "Paired mirrored frame that swaps which ally occupies danger.", true),
            Class(mechanics, ArcRelayLaunchClassIds.Longshot, "Longshot", "Rail Line",
                "A body-length rail that turns a public corridor into a threat.", false),
            Class(mechanics, ArcRelayLaunchClassIds.Mason, "Mason", "Hardlight Block",
                "Builder rig that bends a route with temporary cover.", false),
            Class(mechanics, ArcRelayLaunchClassIds.Sunder, "Sunder", "Target Paint",
                "Designator optics that tell a team which target matters now.", false),
            Class(mechanics, ArcRelayLaunchClassIds.Repulsor, "Repulsor", "Kinetic Burst",
                "Radial emitters that break a local formation from adjacency.", false),
            Class(mechanics, ArcRelayLaunchClassIds.Veil, "Veil", "Smoke Canister",
                "Smoke-launcher skirmisher that authors uncertainty on the map.", false),
            Class(mechanics, ArcRelayLaunchClassIds.Nest, "Nest", "Sentinel Seed",
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
        IReadOnlyDictionary<string, ArcRelaySignatureDefinition> mechanics,
        string id,
        string name,
        string signatureName,
        string fantasy,
        bool starter)
    {
        ArcRelaySignatureDefinition signature = mechanics.TryGetValue(
            id,
            out ArcRelaySignatureDefinition? value)
                ? value
                : throw new InvalidOperationException(
                    $"Arc Relay class '{id}' has no signature mechanics.");
        return new(
            id,
            name,
            signature.SignatureId,
            signature.ActionId,
            signatureName,
            fantasy,
            starter);
    }
}
