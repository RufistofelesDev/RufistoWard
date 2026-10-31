namespace RufistoWard.Identity.ValueObjects;

public sealed record RedirectUri
{
    // Schemes that make a browser execute or read something locally instead of navigating to a
    // client. A redirect_uri is echoed into Location headers and form_post actions, so these must
    // never be registered. Custom schemes (myapp://) stay allowed: native apps depend on them.
    private static readonly string[] ForbiddenSchemes = ["javascript", "data", "vbscript", "file"];

    public string Value { get; }

    public RedirectUri(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var error = Validate(value);
        if (error is not null)
            throw new ArgumentException(error, nameof(value));
        Value = value;
    }

    /// <summary>Why <paramref name="value"/> cannot be registered, or <c>null</c> when it can.</summary>
    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "RedirectUri is required.";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return "RedirectUri must be a valid absolute URI.";
        if (ForbiddenSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
            return $"RedirectUri scheme '{uri.Scheme}:' is not allowed.";
        return null;
    }

    public override string ToString() => Value;
    public static implicit operator string(RedirectUri uri) => uri.Value;
}
