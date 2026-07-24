namespace BotArena.App.Storage;

/// <summary>
/// Private immutable-blob boundary. PostgreSQL stores object keys, never
/// host-specific paths; a later S3 implementation can preserve this contract.
/// </summary>
public interface IObjectStore
{
    Task PutAsync(
        string key,
        Stream source,
        string? expectedSha256,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<string> MaterializeAsync(
        string key,
        string expectedSha256,
        CancellationToken cancellationToken = default);

    Task<bool> IsReadyAsync(
        bool requireWrite,
        CancellationToken cancellationToken = default);
}
