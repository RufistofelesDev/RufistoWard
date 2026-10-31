namespace RufistoWard.Core.Commands;

using RufistoWard.Core.Bootstrap;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public static class SeedAdminCommand
{
    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        string? email = null;
        string? password = null;
        var username = "admin";

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
            else if (args[i] == "--username" && i + 1 < args.Length)
            {
                username = args[++i];
            }
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Usage: seed-admin --email <email> --password <password> [--username <name>]");
            return 1;
        }

        using var scope = services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var adminRole = await roleRepository.FindByNameAsync(AdminAuthorization.RoleName);
        if (adminRole is null)
        {
            adminRole = Role.Create(AdminAuthorization.RoleName, "Full access to the admin UI and admin API.");
            await roleRepository.AddAsync(adminRole);
        }

        // An existing account is granted the role instead of being refused. Before v1.0-rc2 the
        // admin UI had no role check at all, so installs seeded then hold an admin without it —
        // re-running seed-admin is their upgrade path. The password is left untouched.
        var existing = await userRepository.FindByEmailAsync(email.ToLowerInvariant());
        if (existing is not null)
        {
            existing.AssignRole(adminRole.Id);
            await userRepository.UpdateAsync(existing);
            Console.WriteLine($"Granted '{AdminAuthorization.RoleName}' to existing user: {email} (password unchanged)");
            return 0;
        }

        if (await userRepository.FindByUsernameAsync(username) is not null)
        {
            Console.Error.WriteLine($"Username '{username}' is taken. Pass --username to seed another admin.");
            return 1;
        }

        var hashedPassword = passwordHasher.Hash(password);
        var user = User.Create(
            subjectId: Guid.NewGuid().ToString("N"),
            username: username,
            email: email,
            passwordHash: hashedPassword);
        user.AssignRole(adminRole.Id);

        await userRepository.AddAsync(user);

        Console.WriteLine($"Admin user created: {email}");
        return 0;
    }
}
