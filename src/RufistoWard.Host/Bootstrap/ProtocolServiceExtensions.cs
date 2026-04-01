namespace RufistoWard.Core.Bootstrap;

using RufistoWard.Auth.Protocol.Authorization;
using RufistoWard.Auth.Protocol.Device;
using RufistoWard.Auth.Protocol.Token;
using RufistoWard.Auth.Protocol.UserInfo;
using RufistoWard.Auth.Protocol.Validation;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence.Repositories;
using RufistoWard.Tokens.Providers.Identity;

public static class ProtocolServiceExtensions
{
    public static IServiceCollection AddRufistoWardProtocol(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IPersistedGrantRepository, PersistedGrantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Identity
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();

        // Validation
        services.AddScoped<IClientAuthenticator, ClientAuthenticator>();

        // Authorization
        services.AddScoped<LoginHandler>();
        services.AddScoped<AuthorizationRequestHandler>();
        services.AddScoped<AuthorizationCodeGenerator>();
        services.AddScoped<DeviceAuthorizationHandler>();

        // Token building
        services.AddScoped<ITokenBuilder, JwtTokenBuilder>();

        // UserInfo
        services.AddScoped<IAccessTokenValidator, AccessTokenValidator>();
        services.AddScoped<IUserInfoHandler, UserInfoHandler>();

        // Token request handlers (registered as IEnumerable<ITokenRequestHandler>)
        services.AddScoped<ITokenRequestHandler, AuthorizationCodeTokenHandler>();
        services.AddScoped<ITokenRequestHandler, RefreshTokenHandler>();
        services.AddScoped<ITokenRequestHandler, ClientCredentialsTokenHandler>();
        services.AddScoped<ITokenRequestHandler, DeviceCodeTokenHandler>();

        return services;
    }
}
