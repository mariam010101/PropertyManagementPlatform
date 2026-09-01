namespace PMP.Shared.Common;

/// <summary>
/// A lightweight result wrapper used by application services to communicate
/// success/failure and validation errors to the API layer.
/// </summary>
public class Result
{
    protected Result(bool succeeded, string? error, object? value = null)
    {
        Succeeded = succeeded;
        Error = error;
        Value = value;
    }

    public bool Succeeded { get; }

    public string? Error { get; }

    public object? Value { get; }

    public static Result Ok(object? value = null) => new(true, null, value);

    public static Result Fail(string error) => new(false, error);

    public static Result<T> Ok<T>(T value) => new(true, null, value);

    public static Result<T> Fail<T>(string error) => new(false, error, default);
}

public class Result<T> : Result
{
    public Result(bool succeeded, string? error, T? value)
        : base(succeeded, error, value)
    {
        Data = value;
    }

    public T? Data { get; }
}
