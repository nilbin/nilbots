namespace BotArena.App.Storage;

public static class ObjectStoreRegistration
{
    public static IServiceCollection AddObjectStore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string backend = configuration["BOTARENA_OBJECT_STORE"]?.Trim().ToLowerInvariant()
            ?? "local";
        switch (backend)
        {
            case "local":
                services.AddSingleton<IObjectStore, LocalObjectStore>();
                break;
            case "s3":
                services.AddSingleton(S3ObjectStoreOptions.FromConfiguration(configuration));
                services.AddSingleton<IObjectStore, S3ObjectStore>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown BOTARENA_OBJECT_STORE '{backend}'. Expected local or s3.");
        }

        return services;
    }
}
