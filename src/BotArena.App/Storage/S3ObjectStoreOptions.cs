using BotArena.App.Shared;

namespace BotArena.App.Storage;

public sealed class S3ObjectStoreOptions
{
    public required Uri Endpoint { get; init; }
    public required string Region { get; init; }
    public required string Bucket { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string CacheRoot { get; init; }

    public static S3ObjectStoreOptions FromConfiguration(IConfiguration configuration)
    {
        string endpointValue = Required(configuration, "BOTARENA_S3_ENDPOINT");
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out Uri? endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "BOTARENA_S3_ENDPOINT must be an absolute HTTP or HTTPS URL.");
        }

        bool allowHttp = configuration.GetValue<bool>("BOTARENA_S3_ALLOW_HTTP");
        if (endpoint.Scheme == "http" && !allowHttp)
        {
            throw new InvalidOperationException(
                "An HTTP S3 endpoint requires BOTARENA_S3_ALLOW_HTTP=true. " +
                "Only use this on a private container or overlay network.");
        }

        string bucket = Required(configuration, "BOTARENA_S3_BUCKET");
        if (bucket.Length is < 3 or > 63 ||
            !char.IsAsciiLetterOrDigit(bucket[0]) ||
            !char.IsAsciiLetterOrDigit(bucket[^1]) ||
            bucket.Contains("..", StringComparison.Ordinal) ||
            bucket.Any(c => !(char.IsAsciiLetter(c) && char.IsLower(c) ||
                              char.IsAsciiDigit(c) ||
                              c is '-' or '.')))
        {
            throw new InvalidOperationException(
                "BOTARENA_S3_BUCKET must be a valid 3-63 character S3 bucket name.");
        }

        return new S3ObjectStoreOptions
        {
            Endpoint = endpoint,
            Region = Required(configuration, "BOTARENA_S3_REGION"),
            Bucket = bucket,
            AccessKey = Required(configuration, "BOTARENA_S3_ACCESS_KEY"),
            SecretKey = Required(configuration, "BOTARENA_S3_SECRET_KEY"),
            CacheRoot = Path.GetFullPath(
                configuration["BOTARENA_OBJECT_CACHE"] ?? DataPaths.ObjectCache),
        };
    }

    private static string Required(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"{key} is required for the S3 object store.")
            : configuration[key]!.Trim();
}
