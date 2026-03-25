namespace RufistoWard.Auth.Protocol.Device;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Discovery;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed record DeviceAuthorizationResponse
{
    [JsonPropertyName("device_code")]
    public required string DeviceCode { get; init; }

    [JsonPropertyName("user_code")]
    public required string UserCode { get; init; }

    [JsonPropertyName("verification_uri")]
    public required string VerificationUri { get; init; }

    [JsonPropertyName("verification_uri_complete")]
    public required string VerificationUriComplete { get; init; }

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }

    [JsonPropertyName("interval")]
    public int Interval { get; init; } = 5;
}

public sealed record DeviceCodeData
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    [JsonPropertyName("user_code")]
    public required string UserCode { get; init; }

    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("denied")]
    public bool Denied { get; set; }

    [JsonPropertyName("subject_id")]
    public string? SubjectId { get; set; }

    [JsonPropertyName("last_polled")]
    public DateTime? LastPolled { get; set; }
}

public sealed class DeviceAuthorizationHandler(
    IPersistedGrantRepository grantRepository,
    IClientRepository clientRepository,
    IOptions<DiscoveryOptions> discoveryOptions)
{
    private const int DeviceCodeExpirationMinutes = 15;

    public async Task<Result<DeviceAuthorizationResponse>> HandleAsync(
        string? clientId,
        string? scope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result<DeviceAuthorizationResponse>.Failure(
                ProtocolError.InvalidRequest("client_id is required."));
        }

        var client = await clientRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (client is null || !client.Enabled)
        {
            return Result<DeviceAuthorizationResponse>.Failure(
                ProtocolError.InvalidClient("Unknown or disabled client."));
        }

        var deviceCode = Guid.NewGuid().ToString("N");
        var userCode = GenerateUserCode();
        var issuer = discoveryOptions.Value.Issuer.TrimEnd('/');

        var codeData = new DeviceCodeData
        {
            ClientId = clientId,
            Scope = scope ?? "openid",
            UserCode = userCode,
        };

        var grant = PersistedGrant.Create(
            id: deviceCode,
            type: "device_code",
            clientId: clientId,
            subjectId: null,
            data: JsonSerializer.Serialize(codeData),
            expiration: DateTime.UtcNow.AddMinutes(DeviceCodeExpirationMinutes));

        await grantRepository.AddAsync(grant, cancellationToken);

        return Result<DeviceAuthorizationResponse>.Success(new DeviceAuthorizationResponse
        {
            DeviceCode = deviceCode,
            UserCode = userCode,
            VerificationUri = $"{issuer}/device",
            VerificationUriComplete = $"{issuer}/device?user_code={userCode}",
            ExpiresIn = DeviceCodeExpirationMinutes * 60,
        });
    }

    private static string GenerateUserCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No I,O,0,1 for readability
        var bytes = RandomNumberGenerator.GetBytes(8);
        var code = new char[8];
        for (int i = 0; i < 8; i++)
        {
            code[i] = chars[bytes[i] % chars.Length];
        }
        return $"{new string(code, 0, 4)}-{new string(code, 4, 4)}";
    }
}
