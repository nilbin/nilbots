namespace BotArena.App.Shared;

public sealed record ApplicationResult<T>(T? Value, ApplicationError? Error)
{
    public bool Succeeded => Error is null;

    public static ApplicationResult<T> Success(T value) => new(value, null);

    public static ApplicationResult<T> Failure(ApplicationError error) => new(default, error);
}
