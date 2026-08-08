using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace BotArena.App.Shared;

/// <summary>HTTP content and cache policy for compiled soundtrack packs.</summary>
public static partial class SoundtrackStaticAssets
{
    private const string SoundtrackPrefix = "/soundtracks";
    private const string CatalogPath = "/soundtracks/index.json";

    public static StaticFileOptions CreateOptions(IFileProvider fileProvider)
    {
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".m4a"] = "audio/mp4";
        contentTypes.Mappings[".ogg"] = "audio/ogg";
        contentTypes.Mappings[".glb"] = "model/gltf-binary";
        contentTypes.Mappings[".gltf"] = "model/gltf+json";
        contentTypes.Mappings[".ktx2"] = "image/ktx2";

        return new StaticFileOptions
        {
            FileProvider = fileProvider,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context =>
            {
                PathString path = context.Context.Request.Path;
                if (!path.StartsWithSegments(SoundtrackPrefix))
                    return;

                context.Context.Response.Headers.CacheControl =
                    path.Equals(CatalogPath, StringComparison.Ordinal)
                        ? "no-cache"
                        : HasContentAddress(path)
                            ? "public, max-age=31536000, immutable"
                            : "no-cache";
            },
        };
    }

    private static bool HasContentAddress(PathString path)
    {
        string? value = path.Value;
        if (value is null)
            return false;

        return value
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => ContentAddressSegment().IsMatch(segment));
    }

    [GeneratedRegex(@"^v[0-9]+-[0-9a-f]{16,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ContentAddressSegment();
}
