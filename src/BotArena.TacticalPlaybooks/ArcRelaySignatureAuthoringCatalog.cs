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
    ArcRelaySignatureCommitmentBehavior DefaultCommitment)
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
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault),
            Entry("palisade", "prism-wall", BroadTargets),
            Entry("towline", "tractor-hook", BroadTargets,
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault),
            Entry("patchbay", "repair-beam",
                [ArcRelaySignatureNamedTarget.WeakestAlly,
                    ArcRelaySignatureNamedTarget.NearestAlly],
                ArcRelaySignatureTargetCapability.FriendlyRole
                    | ArcRelaySignatureTargetCapability.MostExperiencedAlly,
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault),
            Entry("lantern", "survey-flare", BroadTargets),
            Entry("mortar", "falling-star", BroadTargets,
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault),
            Entry("minesmith", "trip-node", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor),
            Entry("hush", "null-field",
                [ArcRelaySignatureNamedTarget.Self],
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault),
            Entry("relay", "arc-toss", ArcTossTargets,
                ArcRelaySignatureTargetCapability.FriendlyRole
                    | ArcRelaySignatureTargetCapability.MostExperiencedAlly
                    | ArcRelaySignatureTargetCapability.RouteAheadRole),
            Entry("switchback", "exchange",
                [ArcRelaySignatureNamedTarget.HealthiestAlly,
                    ArcRelaySignatureNamedTarget.NearestAlly,
                    ArcRelaySignatureNamedTarget.WeakestAlly],
                ArcRelaySignatureTargetCapability.FriendlyRole
                    | ArcRelaySignatureTargetCapability.MostExperiencedAlly,
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault),
            Entry("longshot", "rail-line", BroadTargets,
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault),
            Entry("mason", "hardlight-block", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor),
            Entry("sunder", "target-paint",
                [ArcRelaySignatureNamedTarget.EnemyCarrier,
                    ArcRelaySignatureNamedTarget.Veteran,
                    ArcRelaySignatureNamedTarget.WeakestEnemy,
                    ArcRelaySignatureNamedTarget.NearestEnemy],
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault),
            Entry("repulsor", "kinetic-burst",
                [ArcRelaySignatureNamedTarget.Self],
                commitment: ArcRelaySignatureCommitmentBehavior
                    .PreserveByDefault),
            Entry("veil", "smoke-canister", BroadTargets),
            Entry("nest", "sentinel-seed", BroadTargets,
                ArcRelaySignatureTargetCapability.ExactAnchor,
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault),
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
            ArcRelaySignatureCommitmentBehavior.ContinueNormally) =>
        new(classId, skillId, namedTargets, capabilities, commitment);
}
