using System.Text.Json;

namespace BotArena.App.Jobs;

public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
}

/// <summary>DB-backed durable job (plan §22). No message broker.</summary>
public class BackgroundJob
{
    public long Id { get; set; }
    public required string Type { get; set; }
    public required string PayloadJson { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int Attempts { get; set; }
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? LockedUntil { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public const string CompileSubmissionType = "CompileSubmission";
    public const string ExecuteMatchType = "ExecuteMatch";

    public static BackgroundJob CompileSubmission(Guid botVersionId) => new()
    {
        Type = CompileSubmissionType,
        PayloadJson = JsonSerializer.Serialize(new { botVersionId }),
    };

    public static BackgroundJob ExecuteMatch(Guid matchId) => new()
    {
        Type = ExecuteMatchType,
        PayloadJson = JsonSerializer.Serialize(new { matchId }),
    };

    public Guid PayloadId(string property) =>
        JsonDocument.Parse(PayloadJson).RootElement.GetProperty(property).GetGuid();
}
