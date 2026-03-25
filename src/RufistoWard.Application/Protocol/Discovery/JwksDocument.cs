namespace RufistoWard.Auth.Protocol.Discovery;

using System.Text.Json.Serialization;

public sealed record JwksDocument
{
    [JsonPropertyName("keys")]
    public required JsonWebKey[] Keys { get; init; }
}

public sealed record JsonWebKey
{
    [JsonPropertyName("kid")]
    public required string Kid { get; init; }

    [JsonPropertyName("kty")]
    public required string Kty { get; init; }

    [JsonPropertyName("use")]
    public required string Use { get; init; }

    [JsonPropertyName("alg")]
    public required string Alg { get; init; }

    // RSA fields
    [JsonPropertyName("n")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? N { get; init; }

    [JsonPropertyName("e")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? E { get; init; }

    // EC fields
    [JsonPropertyName("crv")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Crv { get; init; }

    [JsonPropertyName("x")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? X { get; init; }

    [JsonPropertyName("y")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Y { get; init; }
}
