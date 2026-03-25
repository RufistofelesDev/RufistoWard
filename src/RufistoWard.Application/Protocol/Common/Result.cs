namespace RufistoWard.Auth.Protocol.Common;

public sealed class Result<T>
{
    public T? Value { get; }
    public ProtocolError? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value) => Value = value;
    private Result(ProtocolError error) => Error = error;

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(ProtocolError error) => new(error);
}
