namespace RufistoWard.Auth.Protocol.UserInfo;

using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Identity.Interfaces;

public interface IUserInfoHandler
{
    Task<Result<Dictionary<string, object>>> HandleAsync(string bearerToken, CancellationToken cancellationToken = default);
}

public sealed class UserInfoHandler(
    IAccessTokenValidator tokenValidator,
    IUserRepository userRepository) : IUserInfoHandler
{
    private static readonly HashSet<string> ProfileClaimTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "family_name", "given_name", "middle_name", "nickname",
        "preferred_username", "profile", "picture", "website",
        "gender", "birthdate", "zoneinfo", "locale", "updated_at",
    };

    private static readonly HashSet<string> EmailClaimTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "email_verified",
    };

    public async Task<Result<Dictionary<string, object>>> HandleAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        var tokenResult = await tokenValidator.ValidateAsync(bearerToken, cancellationToken);
        if (!tokenResult.IsSuccess)
            return Result<Dictionary<string, object>>.Failure(tokenResult.Error!);

        var validated = tokenResult.Value!;

        // openid scope is required for userinfo
        if (!validated.Scopes.Contains("openid", StringComparer.OrdinalIgnoreCase))
            return Result<Dictionary<string, object>>.Failure(
                ProtocolError.InsufficientScope("The access token must include the openid scope."));

        var user = await userRepository.FindBySubjectIdWithClaimsAsync(validated.Subject, cancellationToken);
        if (user is null || !user.IsActive)
            return Result<Dictionary<string, object>>.Failure(
                ProtocolError.InvalidToken("The user associated with this token no longer exists or is inactive."));

        var claims = new Dictionary<string, object> { ["sub"] = user.SubjectId };
        var hasProfile = validated.Scopes.Contains("profile", StringComparer.OrdinalIgnoreCase);
        var hasEmail = validated.Scopes.Contains("email", StringComparer.OrdinalIgnoreCase);

        // Map standard claims from User entity
        if (hasProfile)
        {
            claims["name"] = user.Username;
            claims["preferred_username"] = user.Username;
        }

        if (hasEmail)
        {
            claims["email"] = user.Email;
            claims["email_verified"] = user.EmailConfirmed;
        }

        // Map additional claims from UserClaim table
        foreach (var claim in user.Claims)
        {
            if (hasProfile && ProfileClaimTypes.Contains(claim.Type))
            {
                claims.TryAdd(claim.Type, claim.Value);
            }
            else if (hasEmail && EmailClaimTypes.Contains(claim.Type))
            {
                claims.TryAdd(claim.Type, claim.Value);
            }
        }

        return Result<Dictionary<string, object>>.Success(claims);
    }
}
