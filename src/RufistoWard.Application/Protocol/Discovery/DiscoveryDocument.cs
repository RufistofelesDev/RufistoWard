namespace RufistoWard.Auth.Protocol.Discovery;

using System.Text.Json.Serialization;

public sealed record DiscoveryDocument
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    [JsonPropertyName("userinfo_endpoint")]
    public required string UserinfoEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public required string JwksUri { get; init; }

    [JsonPropertyName("end_session_endpoint")]
    public required string EndSessionEndpoint { get; init; }

    [JsonPropertyName("revocation_endpoint")]
    public required string RevocationEndpoint { get; init; }

    [JsonPropertyName("introspection_endpoint")]
    public required string IntrospectionEndpoint { get; init; }

    [JsonPropertyName("device_authorization_endpoint")]
    public required string DeviceAuthorizationEndpoint { get; init; }

    [JsonPropertyName("response_types_supported")]
    public required string[] ResponseTypesSupported { get; init; }

    [JsonPropertyName("grant_types_supported")]
    public required string[] GrantTypesSupported { get; init; }

    [JsonPropertyName("subject_types_supported")]
    public required string[] SubjectTypesSupported { get; init; }

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public required string[] IdTokenSigningAlgValuesSupported { get; init; }

    [JsonPropertyName("scopes_supported")]
    public required string[] ScopesSupported { get; init; }

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public required string[] TokenEndpointAuthMethodsSupported { get; init; }

    [JsonPropertyName("claims_supported")]
    public required string[] ClaimsSupported { get; init; }

    [JsonPropertyName("code_challenge_methods_supported")]
    public required string[] CodeChallengeMethodsSupported { get; init; }
}
