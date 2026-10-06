namespace Ordex.Core.Common;

/// <summary>Outcome of a business operation. Services return this instead of throwing for rule violations.</summary>
public class ServiceResult
{
    protected ServiceResult(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }
    public string? Error { get; }

    public static ServiceResult Ok() => new(true, null);
    public static ServiceResult Fail(string error) => new(false, error);
}

public sealed class ServiceResult<T> : ServiceResult
{
    private ServiceResult(bool succeeded, T? value, string? error) : base(succeeded, error) => Value = value;

    public T? Value { get; }

    public static ServiceResult<T> Ok(T value) => new(true, value, null);
    public static new ServiceResult<T> Fail(string error) => new(false, default, error);
}
