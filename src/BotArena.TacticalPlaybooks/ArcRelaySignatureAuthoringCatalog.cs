namespace BotArena.TacticalPlaybooks;

/// <summary>
/// Stable Orders-v2 names that may resolve a signature target without naming
/// an authored role. This is authoring vocabulary, not signature mechanics;
/// the App combines it with the resolved Engine definition for display.
/// </summary>
public enum ArcRelaySignatureNamedTarget
{
    Self,
    OwnReactor,
    NearestEnemy,
    WeakestEnemy,
    EnemyCarrier,
    NearestAlly,
    WeakestAlly,
    HealthiestAlly,
    NearestAllyTowardOwnReactor,
    Veteran,
}

[Flags]
public enum ArcRelaySignatureTargetCapability
{
    None = 0,
    FriendlyRole = 1 << 0,
    MostExperiencedAlly = 1 << 1,
    ExactAnchor = 1 << 2,
    RouteAheadRole = 1 << 3,
    RouteForward = 1 << 4,
}

public enum ArcRelaySignatureCommitmentBehavior
{
    ContinueNormally,
    PreserveByDefault,
}

public enum ArcRelaySignaturePresetTargetKind
{
    NamedSelector,
    FriendlyRole,
    MostExperiencedAlly,
    ExactAnchor,
    RouteAheadRole,
    RouteForward,
}

/// <summary>Editable discovery defaults; never compiler or runtime identity.</summary>
public sealed record ArcRelaySignatureOpportunityPreset(
    string Id,
    string Title,
    string Description,
    ArcRelaySignaturePresetTargetKind TargetKind,
    ArcRelaySignatureNamedTarget? NamedTarget = null,
    bool SuggestsActivationZone = true);

/// <summary>
/// The complete engine-independent authoring shape for one launch signature.
/// Every compiler and editor capability must originate here rather than from a
/// skill-id switch in its own layer.
/// </summary>
public sealed record ArcRelaySignatureAuthoringDescriptor(
    string ClassId,
    string SkillId,
    IReadOnlyList<ArcRelaySignatureNamedTarget> AllowedNamedTargets,
    ArcRelaySignatureTargetCapability Capabilities,
    ArcRelaySignatureCommitmentBehavior DefaultCommitment,
    ArcRelaySignatureOpportunityPreset Preset,
    bool DurableDeployment)
{
    public bool Allows(ArcRelaySignatureTargetCapability capability) =>
        (Capabilities & capability) == capability;

    public bool AllowsNamedTarget(string value) =>
        ArcRelaySignatureAuthoringCatalog.TryParseNamedTarget(
            value, out ArcRelaySignatureNamedTarget target)
        && AllowedNamedTargets.Contains(target);
}

/// <summary>
/// Orders-v2's one authoritative class/signature/target vocabulary. The
/// TacticalPlaybooks project intentionally has no Engine reference; numeric
/// mechanics and lifecycle timing remain Engine contract data.
/// </summary>
public static class ArcRelaySignatureAuthoringCatalog
{
    private static readonly ArcRelaySignatureNamedTarget[] BroadTargets =
    [
        ArcRelaySignatureNamedTarget.NearestEnemy,
        ArcRelaySignatureNamedTarget.WeakestEnemy,
        ArcRelaySignatureNamedTarget.EnemyCarrier,
        ArcRelaySignatureNamedTarget.NearestAlly,
        ArcRelaySignatureNamedTarget.WeakestAlly,
        ArcRelaySignatureNamedTarget.Self,
        ArcRelaySignatureNamedTarget.OwnReactor,
        ArcRelaySignatureNamedTarget.HealthiestAlly,
        ArcRelaySignatureNamedTarget.NearestAllyTowardOwnReactor,
        ArcRelaySignatureNamedTarget.Veteran,
    ];

    private static readonly ArcRelaySignatureNamedTarget[] ArcTossTargets =
    [
        ArcRelaySignatureNamedTarget.NearestAllyTowardOwnReactor,
        ArcRelaySignatureNamedTarget.NearestAlly,
        ArcRelaySignatureNamedTarget.OwnReactor,
        ArcRelaySignatureNamedTarget.WeakestAlly,
        ArcRelaySignatureNamedTarget.HealthiestAlly,
        ArcRelaySignatureNamedTarget.Self,
        ArcRelaySignatureNamedTarget.NearestEnemy,
        ArcRelaySignatureNamedTarget.WeakestEnemy,
        ArcRelaySignatureNamedTarget.EnemyCarrier,
        ArcRelaySignatureNamedTarget.Veteran,
    ];

    private static readonly IReadOnlyDictionary<string,
        ArcRelaySignatureAuthoringDescriptor> ByClass;
    private static readonly IReadOnlyDictionary<string,
        ArcRelaySignatureAuthoringDescriptor> BySkill;

    static ArcRelaySignatureAuthoringCatalog()
    {
        All =
        [
            Entry("kestrel", "vector-dash", BroadTargets,
                ArcRelaySignatureTargetCapability.RouteForward,
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault,
                Preset("straight-route-dash", "Dash a straight route window",
                    "Advance only when a selected terminal route offers safe forward progress.",
                    ArcRelaySignaturePresetTargetKind.RouteForward)),
            Entry("palisade", "prism-wall", BroadTargets,
                durableDeployment: true,
                preset: Preset("guard-lane-wall", "Face the pressured lane",
                    "Raise Prism Wall from a guard area against the nearest hostile approach.",
                    ArcRelaySignaturePresetTargetKind.NamedSelector,
                    ArcRelaySignatureNamedTarget.NearestEnemy)),
            Entry("towline", "tractor-hook", BroadTargets,
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault,
                preset: Preset("hook-carrier", "Hook the carrier through a sightline",
                    "Pull the enemy carrier when it enters this watched line.",
                    ArcRelaySignaturePresetTargetKind.NamedSelector,
                    ArcRelaySignatureNamedTarget.EnemyCarrier)),
            Entry("patchbay", "repair-beam",
                [ArcRelaySignatureNamedTarget.WeakestAlly,
                    ArcRelaySignatureNamedTarget.NearestAlly],
                ArcRelaySignatureTargetCapability.FriendlyRole
                    | ArcRelaySignatureTargetCapability.MostExperiencedAlly,
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault,
                Preset("protect-role", "Protect a named role",
                    "Maintain Repair Beam on the role this plan is protecting.",
                    ArcRelaySignaturePresetTargetKind.FriendlyRole)),
            Entry("lantern", "survey-flare", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor,
                preset: Preset("reveal-approach", "Reveal an exact approach",
                    "Launch a Survey Flare at a plotted approach before commitment.",
                    ArcRelaySignaturePresetTargetKind.ExactAnchor)),
            Entry("mortar", "falling-star", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor,
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault,
                preset: Preset("punish-staging-area", "Punish a staged area",
                    "Call Falling Star onto an exact watched hostile position.",
                    ArcRelaySignaturePresetTargetKind.ExactAnchor)),
            Entry("minesmith", "trip-node", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor,
                durableDeployment: true,
                preset: Preset("mine-alternate-route", "Mine an alternate route",
                    "Place a Trip Node at one exact route point before continuing.",
                    ArcRelaySignaturePresetTargetKind.ExactAnchor)),
            Entry("hush", "null-field",
                [ArcRelaySignatureNamedTarget.Self],
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault,
                preset: Preset("null-local-overload", "Null a local overload",
                    "Trigger Null Field after enough enemies enter this defended area.",
                    ArcRelaySignaturePresetTargetKind.NamedSelector,
                    ArcRelaySignatureNamedTarget.Self)),
            Entry("relay", "arc-toss", ArcTossTargets,
                ArcRelaySignatureTargetCapability.FriendlyRole
                    | ArcRelaySignatureTargetCapability.MostExperiencedAlly
                    | ArcRelaySignatureTargetCapability.RouteAheadRole,
                preset: Preset("toss-forward-receiver", "Toss to a forward receiver",
                    "Pass to a named Relay only after it earns useful progress on a route.",
                    ArcRelaySignaturePresetTargetKind.RouteAheadRole)),
            Entry("switchback", "exchange",
                [ArcRelaySignatureNamedTarget.HealthiestAlly,
                    ArcRelaySignatureNamedTarget.NearestAlly,
                    ArcRelaySignatureNamedTarget.WeakestAlly],
                ArcRelaySignatureTargetCapability.FriendlyRole
                    | ArcRelaySignatureTargetCapability.MostExperiencedAlly,
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault,
                Preset("exchange-endangered-role", "Exchange with an endangered role",
                    "Swap with the named role this plan is meant to rescue.",
                    ArcRelaySignaturePresetTargetKind.FriendlyRole)),
            Entry("longshot", "rail-line", BroadTargets,
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault,
                preset: Preset("rail-carrier-lane", "Rail the carrier lane",
                    "Hold a selected firing area against the enemy carrier.",
                    ArcRelaySignaturePresetTargetKind.NamedSelector,
                    ArcRelaySignatureNamedTarget.EnemyCarrier)),
            Entry("mason", "hardlight-block", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor,
                durableDeployment: true,
                preset: Preset("block-exact-choke", "Block an exact choke",
                    "Place Hardlight at one plotted choke while the plan continues elsewhere.",
                    ArcRelaySignaturePresetTargetKind.ExactAnchor)),
            Entry("sunder", "target-paint",
                [ArcRelaySignatureNamedTarget.EnemyCarrier,
                    ArcRelaySignatureNamedTarget.Veteran,
                    ArcRelaySignatureNamedTarget.WeakestEnemy,
                    ArcRelaySignatureNamedTarget.NearestEnemy],
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault,
                preset: Preset("paint-carrier", "Paint the enemy carrier",
                    "Mark the carrier inside this committed fight.",
                    ArcRelaySignaturePresetTargetKind.NamedSelector,
                    ArcRelaySignatureNamedTarget.EnemyCarrier)),
            Entry("repulsor", "kinetic-burst",
                [ArcRelaySignatureNamedTarget.Self],
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault,
                preset: Preset("burst-surrounded", "Burst when surrounded",
                    "Push outward after enemies crowd this defended area.",
                    ArcRelaySignaturePresetTargetKind.NamedSelector,
                    ArcRelaySignatureNamedTarget.Self)),
            Entry("veil", "smoke-canister", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor,
                preset: Preset("smoke-crossing", "Smoke an exact crossing",
                    "Cover a plotted crossing before this transit episode continues.",
                    ArcRelaySignaturePresetTargetKind.ExactAnchor)),
            Entry("nest", "sentinel-seed", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor,
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault,
                Preset("seed-guard-post", "Seed an exact guard post",
                    "Deploy Sentinel Seed at a plotted post before holding the lane.",
                    ArcRelaySignaturePresetTargetKind.ExactAnchor),
                durableDeployment: true),
        ];

        ByClass = All.ToDictionary(value => value.ClassId,
            StringComparer.Ordinal);
        BySkill = All.ToDictionary(value => value.SkillId,
            StringComparer.Ordinal);
        if (ByClass.Count != All.Count || BySkill.Count != All.Count)
        {
            throw new InvalidOperationException(
                "Signature authoring catalog contains a duplicate class or skill.");
        }
    }

    public static IReadOnlyList<ArcRelaySignatureAuthoringDescriptor> All
    { get; }

    public static ArcRelaySignatureAuthoringDescriptor ForClass(
        string classId) => ByClass.TryGetValue(
            classId, out ArcRelaySignatureAuthoringDescriptor? value)
                ? value
                : throw new ArgumentException(
                    $"Unknown Arc Relay class '{classId}'.", nameof(classId));

    public static ArcRelaySignatureAuthoringDescriptor ForSkill(
        string skillId) => BySkill.TryGetValue(
            skillId, out ArcRelaySignatureAuthoringDescriptor? value)
                ? value
                : throw new ArgumentException(
                    $"Unknown Arc Relay signature '{skillId}'.", nameof(skillId));

    public static bool TryForSkill(
        string skillId,
        out ArcRelaySignatureAuthoringDescriptor descriptor) =>
        BySkill.TryGetValue(skillId, out descriptor!);

    public static string StableId(ArcRelaySignatureNamedTarget target) =>
        target switch
        {
            ArcRelaySignatureNamedTarget.Self => "self",
            ArcRelaySignatureNamedTarget.OwnReactor => "own-reactor",
            ArcRelaySignatureNamedTarget.NearestEnemy => "nearest-enemy",
            ArcRelaySignatureNamedTarget.WeakestEnemy => "weakest-enemy",
            ArcRelaySignatureNamedTarget.EnemyCarrier => "enemy-carrier",
            ArcRelaySignatureNamedTarget.NearestAlly => "nearest-ally",
            ArcRelaySignatureNamedTarget.WeakestAlly => "weakest-ally",
            ArcRelaySignatureNamedTarget.HealthiestAlly => "healthiest-ally",
            ArcRelaySignatureNamedTarget.NearestAllyTowardOwnReactor =>
                "nearest-ally-toward-own-reactor",
            ArcRelaySignatureNamedTarget.Veteran => "veteran",
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

    public static bool TryParseNamedTarget(
        string value,
        out ArcRelaySignatureNamedTarget target)
    {
        foreach (ArcRelaySignatureNamedTarget candidate in
                 Enum.GetValues<ArcRelaySignatureNamedTarget>())
        {
            if (string.Equals(
                    StableId(candidate), value, StringComparison.Ordinal))
            {
                target = candidate;
                return true;
            }
        }
        target = default;
        return false;
    }

    private static ArcRelaySignatureAuthoringDescriptor Entry(
        string classId,
        string skillId,
        IReadOnlyList<ArcRelaySignatureNamedTarget> namedTargets,
        ArcRelaySignatureTargetCapability capabilities =
            ArcRelaySignatureTargetCapability.None,
        ArcRelaySignatureCommitmentBehavior commitment =
            ArcRelaySignatureCommitmentBehavior.ContinueNormally,
        ArcRelaySignatureOpportunityPreset? preset = null,
        bool durableDeployment = false) =>
        new(classId, skillId, namedTargets, capabilities, commitment,
            preset ?? Preset($"use-{skillId}", $"Use {skillId}",
                "Try this signature when its parent plan is active.",
                ArcRelaySignaturePresetTargetKind.NamedSelector,
                namedTargets.FirstOrDefault()), durableDeployment);

    private static ArcRelaySignatureOpportunityPreset Preset(
        string id,
        string title,
        string description,
        ArcRelaySignaturePresetTargetKind targetKind,
        ArcRelaySignatureNamedTarget? namedTarget = null,
        bool suggestsActivationZone = true) =>
        new(id, title, description, targetKind, namedTarget,
            suggestsActivationZone);
}
