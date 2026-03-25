namespace RufistoWard.Core.Commands;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Tokens.Persistence;

public static class MigrateCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();

        Console.WriteLine("Applying pending migrations...");
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("Migrations applied successfully.");

        return 0;
    }
}
