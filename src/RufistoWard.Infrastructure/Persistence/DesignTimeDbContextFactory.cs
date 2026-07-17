namespace RufistoWard.Tokens.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using RufistoWard.Tokens.Tenancy;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RufistoWardDbContext>
{
    public RufistoWardDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "RufistoWard.Host"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var provider = configuration.GetSection("Database").GetValue<string>("Provider") ?? "PostgreSQL";
        var connectionString = configuration.GetSection("Database")["ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is required.");

        var optionsBuilder = new DbContextOptionsBuilder<RufistoWardDbContext>();

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else
        {
            optionsBuilder.UseNpgsql(connectionString);
        }

        // Migration scaffolding only reads the model, never a row, so the tenant here affects
        // nothing that ends up in a migration. The single-tenant resolver keeps it honest anyway.
        return new RufistoWardDbContext(
            optionsBuilder.Options,
            new ResolvedTenantContext(new SingleTenantResolver()));
    }
}
