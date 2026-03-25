namespace RufistoWard.Core.Bootstrap;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Core.Options;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;
using RufistoWard.Tokens.Persistence.Repositories;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddRufistoWardInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseSection = configuration.GetSection("Database");
        var provider = databaseSection.GetValue<DatabaseProvider>("Provider");
        var connectionString = databaseSection["ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        services.AddDbContext<RufistoWardDbContext>(options =>
        {
            switch (provider)
            {
                case DatabaseProvider.PostgreSQL:
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorCodesToAdd: null);
                    });
                    break;

                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    });
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider: '{provider}'. Supported values: PostgreSQL, SqlServer.");
            }
        });

        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ISigningKeyRepository, SigningKeyRepository>();

        return services;
    }
}
