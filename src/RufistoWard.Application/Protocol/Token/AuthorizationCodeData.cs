namespace RufistoWard.Auth.Protocol.Token;

using System.Text.Json.Serialization;

public sealed record AuthorizationCodeData
{
    [JsonPropertyName("subject_id")]
    public required string SubjectId { get; init; }

    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("redirect_uri")]
    public required string RedirectUri { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    [JsonPropertyName("code_challenge")]
    public string? CodeChallenge { get; init; }

    [JsonPropertyName("code_challenge_method")]
    public string? CodeChallengeMethod { get; init; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; init; }

    /// <summary>
    /// When the End-User actually authenticated, as Unix seconds — carried from the login session,
    /// NOT from the code's creation time. Two authorization requests over one session must yield the
    /// same <c>auth_time</c> in their id_tokens (OIDCC-2).
    /// </summary>
    [JsonPropertyName("auth_time")]
    public long? AuthTime { get; init; }
}
