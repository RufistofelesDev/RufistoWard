namespace RufistoWard.Identity.ValueObjects;

using System.Text.RegularExpressions;

public sealed partial record ClientId
{
    public string Value { get; }

    public ClientId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 200)
            throw new ArgumentException("ClientId must not exceed 200 characters.", nameof(value));
        if (!AllowedPattern().IsMatch(value))
            throw new ArgumentException("ClientId must contain only alphanumeric characters, dashes, and underscores.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(ClientId id) => id.Value;

    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$")]
    private static partial Regex AllowedPattern();
}
