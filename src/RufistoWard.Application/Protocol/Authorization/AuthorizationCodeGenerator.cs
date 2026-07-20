namespace RufistoWard.Auth.Protocol.Authorization;

using System.Security.Cryptography;
using System.Text.Json;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Token;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class AuthorizationCodeGenerator(IPersistedGrantRepository grantRepository)
{
    public async Task<string> GenerateAsync(
        string subjectId,
        ValidatedAuthorizeRequest request,
        DateTimeOffset? authTime = null,
        CancellationToken cancellationToken = default)
    {
        var codeId = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));

        var codeData = new AuthorizationCodeData
        {
            SubjectId = subjectId,
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            Scope = request.Scope,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            Nonce = request.Nonce,
            AuthTime = authTime?.ToUnixTimeSeconds(),
        };

        var grant = PersistedGrant.Create(
            id: codeId,
            type: "authorization_code",
            clientId: request.ClientId,
            subjectId: subjectId,
            data: JsonSerializer.Serialize(codeData),
            expiration: DateTime.UtcNow.AddMinutes(5));

        await grantRepository.AddAsync(grant, cancellationToken);
        return codeId;
    }
}
