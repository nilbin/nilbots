using System.Reflection;
using System.Text.Json;
using BotArena.Engine;
using BotArena.TacticalPlaybooks;

namespace BotArena.App.Sheets;

internal static class TacticalSheetSignatureAuthoringProjection
{
    public static TacticalSheetSignatureAuthoringResponse Project(
        ArcRelaySignatureDefinition signature)
    {
        ArcRelaySignatureAuthoringDescriptor vocabulary =
            ArcRelaySignatureAuthoringCatalog.ForClass(signature.ClassId);
        if (!string.Equals(
                vocabulary.SkillId,
                signature.SignatureId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Signature authoring vocabulary maps '{signature.ClassId}' "
                + $"to '{vocabulary.SkillId}', but the rules resolve "
                + $"'{signature.SignatureId}'.");
        }

        ArcRelaySignatureDefinition.SignatureMetadata metadata =
            ArcRelaySignatureDefinition.MetadataFor(signature.Kind);
        ArcRelaySignatureDefinition.SignatureTimingMetadata timing =
            ArcRelaySignatureDefinition.TimingFor(signature);
        var parameters = signature.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.DeclaredOnly)
            .Where(property => property.PropertyType == typeof(int)
                && property.GetMethod is not null)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(
                property => JsonNamingPolicy.CamelCase.ConvertName(
                    property.Name),
                property => (int)property.GetValue(signature)!,
                StringComparer.Ordinal);

        return new TacticalSheetSignatureAuthoringResponse(
            metadata.Category,
            metadata.ArgumentKind,
            metadata.EngagementRange,
            signature.CooldownTicks,
            Kebab(signature.ActivationTiming.ToString()),
            timing.InitialPhase,
            timing.MaturityPhase,
            timing.CooldownAnchor,
            parameters,
            vocabulary.AllowedNamedTargets.Select(
                ArcRelaySignatureAuthoringCatalog.StableId).ToArray(),
            vocabulary.Allows(
                ArcRelaySignatureTargetCapability.FriendlyRole),
            vocabulary.Allows(
                ArcRelaySignatureTargetCapability.MostExperiencedAlly),
            vocabulary.Allows(
                ArcRelaySignatureTargetCapability.ExactAnchor),
            vocabulary.Allows(
                ArcRelaySignatureTargetCapability.RouteAheadRole),
            vocabulary.Allows(
                ArcRelaySignatureTargetCapability.RouteForward),
            Kebab(vocabulary.GuidanceGeometry.ToString()),
            vocabulary.GuidanceMarkLimit,
            vocabulary.DurableDeployment,
            vocabulary.DefaultCommitment ==
                ArcRelaySignatureCommitmentBehavior.PreserveByDefault
                    ? "preserve-by-default"
                    : "continue-normally",
            new TacticalSheetSignatureOpportunityPresetResponse(
                vocabulary.Preset.Id,
                vocabulary.Preset.Title,
                vocabulary.Preset.Description,
                Kebab(vocabulary.Preset.TargetKind.ToString()),
                vocabulary.Preset.NamedTarget is ArcRelaySignatureNamedTarget named
                    ? ArcRelaySignatureAuthoringCatalog.StableId(named)
                    : null,
                vocabulary.Preset.SuggestsActivationZone));
    }

    private static string Kebab(string value) => string.Concat(
        value.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $"-{char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));
}
