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
}
