namespace RufistoWard.Identity.ValueObjects;

public sealed record GrantType
{
    public static readonly string AuthorizationCode = "authorization_code";
    public static readonly string ClientCredentials = "client_credentials";
    public static readonly string RefreshToken = "refresh_token";
    public static readonly string DeviceCode = "urn:ietf:params:oauth:grant-type:device_code";

    public string Value { get; }

    public GrantType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(GrantType grantType) => grantType.Value;
}
