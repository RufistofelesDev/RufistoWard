namespace RufistoWard.Identity.ValueObjects;

public sealed record EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 256)
            throw new ArgumentException("EmailAddress must not exceed 256 characters.", nameof(value));
        if (!value.Contains('@') || !value.Contains('.'))
            throw new ArgumentException("EmailAddress must be a valid email format.", nameof(value));
        Value = value.ToLowerInvariant();
    }

    public override string ToString() => Value;
    public static implicit operator string(EmailAddress email) => email.Value;
}
