namespace RufistoWard.Core.Bootstrap;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Tokens.Persistence;

public static class DatabaseMigrationExtensions
{
    public static IServiceCollection AddRufistoWardAutoMigration(this IServiceCollection services, IConfiguration configuration)
    {
        var autoMigrate = configuration.GetSection("Database").GetValue<bool>("AutoMigrateOnStartup");
        if (autoMigrate)
        {
            services.AddHostedService<AutoMigrationHostedService>();
        }
        return services;
    }
}

public sealed class AutoMigrationHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
