namespace RufistoWard.Auth.Admin.Common;

public interface IValidator<in T>
{
    ValidationResult Validate(T request);
}

public sealed record ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public Dictionary<string, string[]> Errors { get; init; } = [];

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(string field, string message)
        => new() { Errors = new() { [field] = [message] } };

    public static ValidationResult Failure(Dictionary<string, string[]> errors)
        => new() { Errors = errors };
}
