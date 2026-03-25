namespace RufistoWard.Identity.ValueObjects;

public sealed record RedirectUri
{
    public string Value { get; }

    public RedirectUri(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Uri.TryCreate(value, UriKind.Absolute, out _))
            throw new ArgumentException("RedirectUri must be a valid absolute URI.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(RedirectUri uri) => uri.Value;
}
