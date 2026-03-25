namespace RufistoWard.Identity.ValueObjects;

public sealed record HashedSecret
{
    public string Value { get; }

    public HashedSecret(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => "***";
    public static implicit operator string(HashedSecret secret) => secret.Value;
}
