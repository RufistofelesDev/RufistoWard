namespace RufistoWard.Identity.ValueObjects;

public sealed record ScopeName
{
    public string Value { get; }

    public ScopeName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 200)
            throw new ArgumentException("ScopeName must not exceed 200 characters.", nameof(value));
        if (value.Contains(' '))
            throw new ArgumentException("ScopeName must not contain spaces.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(ScopeName scope) => scope.Value;
}
