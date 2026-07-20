namespace RufistoWard.Auth.Protocol.Token;

using System.Text.Json.Serialization;

/// <summary>
/// Payload persisted against a <c>refresh_token</c> grant. Carries the End-User's original
/// authentication time so refreshed id_tokens keep reporting it: OIDCC-12.2 requires the id_token
/// from a refresh to hold the SAME <c>auth_time</c> as the one issued at authentication.
/// </summary>
public sealed record RefreshTokenData
{
    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    [JsonPropertyName("auth_time")]
    public long? AuthTime { get; init; }
}
