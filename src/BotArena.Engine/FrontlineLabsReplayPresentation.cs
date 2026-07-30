using System.Collections.Immutable;

namespace BotArena.Engine;

/// <summary>
/// Renderer-owned presentation paired with Frontline Labs contracts.
///
/// The resolved actor definition remains gameplay-only. This descriptor is
/// supplied only when a replay is projected, so changing artwork cannot
/// change contract or map fingerprints.
/// </summary>
public static class FrontlineLabsReplayPresentation
{
    public const string ThemeId = "ember-forge";
    public const string BoundaryWallFamily = "perimeter";
    public const string InteriorWallFamily = "cover";

    public static GenericActorReplayPresentation Create(
        ActorResolvedMatchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ImmutableArray<GenericActorReplayPresentation.FormPresentation> forms =
            definition.Rules.Forms
                .Select(Form)
                .Where(form => form is not null)
                .Cast<GenericActorReplayPresentation.FormPresentation>()
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

    private static GenericActorReplayPresentation.FormPresentation? Form(
        ActorFormDefinition form)
    {
        string mobileLook;
        string projectileLook;
        string? emplacedLook = null;
        string? stanceLook = null;

        if (form.Id.StartsWith("striker-", StringComparison.Ordinal))
        {
            mobileLook = "trident-wasp";
            projectileLook = "trident-spark";
            stanceLook = "trident-wasp-volley";
        }
        else if (form.Id.StartsWith("bulwark-", StringComparison.Ordinal))
        {
            mobileLook = "aegis-tortoise";
            projectileLook = "rebound-diamond";
            emplacedLook = "aegis-tortoise-turret";
            stanceLook = "aegis-tortoise-shell";
        }
        else if (form.Id.StartsWith("fabricator-", StringComparison.Ordinal))
        {
            mobileLook = "lattice-loom";
            projectileLook = "lattice-rivet";
        }
        else
        {
            return null;
        }

        string look = form.Id.EndsWith("-turret", StringComparison.Ordinal)
            ? emplacedLook ?? mobileLook
            : form.Id.EndsWith("-stance", StringComparison.Ordinal) ||
              form.Id.EndsWith("-aegis-shell", StringComparison.Ordinal)
                ? stanceLook ?? mobileLook
                : mobileLook;
        return new GenericActorReplayPresentation.FormPresentation(
            form.Id,
            look,
            projectileLook);
    }
}
