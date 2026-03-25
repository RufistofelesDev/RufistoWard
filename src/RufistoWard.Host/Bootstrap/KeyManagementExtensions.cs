namespace RufistoWard.Core.Bootstrap;

using RufistoWard.Auth.Protocol.Keys;
using RufistoWard.Core.Options;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Providers.KeyStorage;

public static class KeyManagementExtensions
{
    public static IServiceCollection AddRufistoWardKeyManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration.GetSection("KeyManagement")["KeyStorageProvider"] ?? "FileSystem";
        var storagePath = configuration.GetSection("KeyManagement")["StoragePath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RufistoWard", "keys");

        switch (provider)
        {
            case "CertificateStore":
                services.AddSingleton<IKeyStorageProvider, CertificateStoreKeyStorageProvider>();
                break;
            default:
                services.AddSingleton<IKeyStorageProvider>(new FileSystemKeyStorageProvider(storagePath));
                break;
        }

        services.AddScoped<IKeyGenerationService, KeyGenerationService>();
        services.AddScoped<IKeyRotationService, KeyRotationService>();
        services.AddHostedService<KeyInitializationHostedService>();

        return services;
    }
}

public sealed class KeyInitializationHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISigningKeyRepository>();
        var existingKey = await repo.GetActiveKeyAsync(cancellationToken);

        if (existingKey is not null)
        {
            return;
        }

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var algorithm = config.GetSection("KeyManagement")["SigningAlgorithm"] ?? "RS256";

        var keyGen = scope.ServiceProvider.GetRequiredService<IKeyGenerationService>();
        await keyGen.GenerateKeyAsync(algorithm, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
