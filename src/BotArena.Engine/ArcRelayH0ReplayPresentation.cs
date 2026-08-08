using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Renderer-owned presentation paired with the immutable Arc Relay H0
/// contract. Artwork changes replay identity but never the gameplay contract
/// or its component fingerprints.
/// </summary>
public static class ArcRelayH0ReplayPresentation
{
    public const string ThemeId = "ember-forge";
    public const string BoundaryWallFamily = "perimeter";
    public const string InteriorWallFamily = "cover";
    public const string LookPrefix = "arc-";
    public const string ProjectileLookId = "arc-pulse";

    public static GenericActorReplayPresentation Create(
        ActorResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ImmutableArray<GenericActorReplayPresentation.FormPresentation> forms =
            definition.Rules.Forms
                .Where(form => form.Id.StartsWith(
                    ArcRelayH0Definition.FormPrefix,
                    StringComparison.Ordinal))
                .Select(form =>
                {
                    string classId = form.Id[
                        ArcRelayH0Definition.FormPrefix.Length..];
                    return new GenericActorReplayPresentation.FormPresentation(
                        form.Id,
                        LookPrefix + classId,
                        ProjectileLookId);
                })
                .OrderBy(form => form.FormId, StringComparer.Ordinal)
                .ToImmutableArray();

        return new GenericActorReplayPresentation(
            ThemeId,
            new GenericActorReplayPresentation.MapPresentation(
                BoundaryWallFamily,
                InteriorWallFamily,
                []),
            forms);
    }
}
