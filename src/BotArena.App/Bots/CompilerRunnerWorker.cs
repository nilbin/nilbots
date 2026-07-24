using BotArena.Toolchain;

namespace BotArena.App.Bots;

/// <summary>
/// Runs inside the unprivileged, networkless compiler container. Only bounded
/// request/result files cross this boundary; the runner has no database,
/// object-store, authentication, or Docker credentials.
/// </summary>
public sealed class CompilerRunnerWorker(
    IConfiguration configuration,
    ILogger<CompilerRunnerWorker> logger)
    : BackgroundService
{
    private readonly string root = configuration["BOTARENA_COMPILER_IPC"]
        ?? throw new InvalidOperationException("BOTARENA_COMPILER_IPC is required.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CompilerQueue.EnsureDirectories(root);
        RecoverInterruptedRequests();
        logger.LogInformation("Networkless compiler runner started");

        while (!stoppingToken.IsCancellationRequested)
        {
            string? working = TryClaimRequest();
            if (working is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
                continue;
            }

            await ProcessRequest(working, stoppingToken);
        }
    }

    private string? TryClaimRequest()
    {
        foreach (string requestPath in Directory
                     .EnumerateFiles(CompilerQueue.RequestDirectory(root), "*.json")
                     .Order(StringComparer.Ordinal))
        {
            string workingPath = Path.Combine(
                CompilerQueue.WorkingDirectory(root),
                Path.GetFileName(requestPath));
            try
            {
                File.Move(requestPath, workingPath);
                return workingPath;
            }
            catch (IOException)
            {
                // Another runner won the atomic rename.
            }
        }
        return null;
    }

    private async Task ProcessRequest(string workingPath, CancellationToken stoppingToken)
    {
        Guid requestId = Guid.ParseExact(Path.GetFileNameWithoutExtension(workingPath), "N");
        string? cacheDirectory = null;
        try
        {
            CompilerQueueRequest request =
                await CompilerQueue.ReadJsonAsync<CompilerQueueRequest>(workingPath, stoppingToken);
            if (request.RequestId != requestId)
                throw new InvalidDataException("Compiler request ID did not match its filename.");

            string cacheKey = BotBuilder.ComputeCacheKey(request.Sources, request.EntryType);
            cacheDirectory = Path.Combine(ToolchainInfo.CacheRoot, cacheKey[..24]);
            BuiltBot built = BotBuilder.BuildFromSources(
                request.Sources,
                request.EntryType,
                request.DisplayName,
                noCache: true,
                quiet: true);
            string artifactPath = CompilerQueue.ArtifactPath(root, requestId);
            string temporaryArtifact = $"{artifactPath}.tmp-{Guid.NewGuid():N}";
            try
            {
                File.Copy(built.WasmPath, temporaryArtifact);
                File.Move(temporaryArtifact, artifactPath, overwrite: true);
            }
            finally
            {
                File.Delete(temporaryArtifact);
            }

            string logPath = Path.Combine(cacheDirectory, "build.log");
            string buildLog = File.Exists(logPath) ? File.ReadAllText(logPath) : "";
            await CompilerQueue.WriteResultAsync(
                root,
                new CompilerQueueResult(
                    requestId,
                    Succeeded: true,
                    built.ArtifactHash,
                    built.CacheKey,
                    Tail(buildLog),
                    Error: null),
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (BotBuildException ex)
        {
            await PublishFailure(requestId, ex.Message, ex.BuildLog);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Compiler request {RequestId} failed", requestId);
            await PublishFailure(requestId, "The isolated compiler failed.", ex.Message);
        }
        finally
        {
            if (stoppingToken.IsCancellationRequested &&
                !File.Exists(CompilerQueue.ResultPath(root, requestId)))
            {
                try
                {
                    File.Move(
                        workingPath,
                        CompilerQueue.RequestPath(root, requestId),
                        overwrite: false);
                }
                catch (IOException)
                {
                    File.Delete(workingPath);
                }
            }
            else
            {
                File.Delete(workingPath);
            }
            if (cacheDirectory is not null && Directory.Exists(cacheDirectory))
            {
                try
                {
                    Directory.Delete(cacheDirectory, recursive: true);
                }
                catch (IOException ex)
                {
                    logger.LogWarning(ex, "Could not clean compiler workspace {Path}", cacheDirectory);
                }
            }
        }
    }

    private Task PublishFailure(Guid requestId, string error, string log) =>
        CompilerQueue.WriteResultAsync(
            root,
            new CompilerQueueResult(
                requestId,
                Succeeded: false,
                ArtifactHash: null,
                CacheKey: null,
                Tail(log),
                error),
            CancellationToken.None);

    private void RecoverInterruptedRequests()
    {
        foreach (string workingPath in Directory.EnumerateFiles(
                     CompilerQueue.WorkingDirectory(root),
                     "*.json"))
        {
            Guid id = Guid.ParseExact(Path.GetFileNameWithoutExtension(workingPath), "N");
            if (File.Exists(CompilerQueue.ResultPath(root, id)))
            {
                File.Delete(workingPath);
                continue;
            }

            string requestPath = CompilerQueue.RequestPath(root, id);
            try
            {
                File.Move(workingPath, requestPath, overwrite: false);
            }
            catch (IOException)
            {
                File.Delete(workingPath);
            }
        }
    }

    private static string Tail(string value) =>
        value.Length <= 64 * 1024 ? value : value[^(64 * 1024)..];
}
