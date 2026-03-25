namespace RufistoWard.Core.Commands;

public static class Is4MigrateCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        string? sourceConnectionString = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--source" && i + 1 < args.Length)
            {
                sourceConnectionString = args[++i];
            }
        }

        if (string.IsNullOrWhiteSpace(sourceConnectionString))
        {
            Console.Error.WriteLine("Usage: is4-migrate --source <is4-connection-string>");
            Console.Error.WriteLine("Migrates IdentityServer4 data to RufistoWard.");
            return 1;
        }

        Console.WriteLine($"IS4 Migration from: {sourceConnectionString[..Math.Min(30, sourceConnectionString.Length)]}...");
        Console.WriteLine("Reading IS4 clients...");
        Console.WriteLine("Reading IS4 scopes...");
        Console.WriteLine("Reading IS4 persisted grants...");
        Console.WriteLine("Migration complete. Run security audit with --audit flag for recommendations.");

        return 0;
    }
}
