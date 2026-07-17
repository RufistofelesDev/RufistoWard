namespace RufistoWard.Core.Bootstrap;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RufistoWard.Core.Options;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;
using RufistoWard.Tokens.Persistence.Repositories;
using RufistoWard.Tokens.Tenancy;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddRufistoWardInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registered with TryAdd so that a deployment supplying its own resolver — the extension
        // point for serving more than one tenant — wins over the single-tenant default rather
        // than silently running alongside it.
        services.TryAddScoped<ITenantResolver, SingleTenantResolver>();
        services.TryAddScoped<ITenantContext, ResolvedTenantContext>();

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
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IScopeRepository, ScopeRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<ISigningKeyRepository, SigningKeyRepository>();

        return services;
    }
}
