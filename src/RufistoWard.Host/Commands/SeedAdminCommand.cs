namespace RufistoWard.Core.Commands;

using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public static class SeedAdminCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        string? email = null;
        string? password = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--email" && i + 1 < args.Length)
            {
                email = args[++i];
            }
            else if (args[i] == "--password" && i + 1 < args.Length)
            {
                password = args[++i];
            }
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Usage: seed-admin --email <email> --password <password>");
            return 1;
        }

        using var scope = services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Check if admin already exists
        var existing = await userRepository.FindByEmailAsync(email.ToLowerInvariant());
        if (existing is not null)
        {
            Console.Error.WriteLine($"Admin user with email '{email}' already exists.");
            return 1;
        }

        var hashedPassword = passwordHasher.Hash(password);
        var user = User.Create(
            subjectId: Guid.NewGuid().ToString("N"),
            username: "admin",
            email: email,
            passwordHash: hashedPassword);

        await userRepository.AddAsync(user);

        Console.WriteLine($"Admin user created: {email}");
        return 0;
    }
}
