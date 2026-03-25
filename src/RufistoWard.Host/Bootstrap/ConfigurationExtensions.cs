namespace RufistoWard.Core.Bootstrap;

using RufistoWard.Core.Options;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddRufistoWardConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ServerOptions>()
            .BindConfiguration("Server")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .BindConfiguration("Database")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TokenOptions>()
            .BindConfiguration("Token")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AdminAuthOptions>()
            .BindConfiguration("AdminAuth")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<KeyManagementOptions>()
            .BindConfiguration("KeyManagement")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SecurityHeaderOptions>()
            .BindConfiguration("SecurityHeaders")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .BindConfiguration("RateLimits")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
