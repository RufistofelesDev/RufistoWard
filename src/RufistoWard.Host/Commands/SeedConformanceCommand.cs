namespace RufistoWard.Core.Commands;

using System.Security.Cryptography;
using System.Text;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

/// <summary>
/// Seeds the deterministic OpenID Foundation conformance fixtures (Story 10.5): a static
/// confidential client with a known <c>client_id</c>/<c>client_secret</c> and a login-able test
/// user. The Admin API cannot pin a stable <c>client_id</c> (it is server-generated), so the
/// static-client conformance configs rely on this command for reproducible nightly runs.
/// Idempotent: re-running skips fixtures that already exist and still returns success.
/// </summary>
public static class SeedConformanceCommand
{
    // Must match tests/conformance/config/test-plans/rufistoward-basic.json (client block).
    public const string ConformanceClientId = "conformance-basic";
    public const string ConformanceClientSecret = "conformance-basic-secret";

    public const string TestUserSubjectId = "conformance-test-user";
    public const string TestUsername = "testuser";
    public const string TestUserEmail = "testuser@conformance.local";
    public const string TestUserPassword = "Conformance!1";

    public static readonly string[] GrantTypes = ["authorization_code", "refresh_token"];
    public static readonly string[] Scopes = ["openid", "profile", "email"];

    // The conformance suite registers two callback URIs per alias (one bare, one with dummy params).
    // Keep these in lock-step with rufistoward-basic.json (//redirect1, //redirect2).
    public static readonly string[] RedirectUris =
    [
        "https://localhost.emobix.co.uk:8443/test/a/rufistoward-basic/callback",
        "https://localhost.emobix.co.uk:8443/test/a/rufistoward-basic/callback?dummy1=lorem&dummy2=ipsum",
    ];

    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        using var scope = services.CreateScope();
        var clientRepository = scope.ServiceProvider.GetRequiredService<IClientRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var existingClient = await clientRepository.FindByClientIdAsync(ConformanceClientId);
        if (existingClient is null)
        {
            await clientRepository.AddAsync(BuildClient());
            Console.WriteLine($"Conformance client created: {ConformanceClientId}");
        }
        else
        {
            Console.WriteLine($"Conformance client '{ConformanceClientId}' already exists — skipping.");
        }

        var existingUser = await userRepository.FindByEmailAsync(TestUserEmail.ToLowerInvariant());
        if (existingUser is null)
        {
            await userRepository.AddAsync(BuildUser(passwordHasher));
            Console.WriteLine($"Conformance test user created: {TestUserEmail}");
        }
        else
        {
            Console.WriteLine($"Conformance test user '{TestUserEmail}' already exists — skipping.");
        }

        return 0;
    }

    /// <summary>
    /// Builds the static confidential conformance client: PKCE-enforced authorization-code +
    /// refresh-token grants, the standard OIDC scopes, both suite callbacks, and a known secret
    /// hashed the same way <c>ClientAuthenticator</c> verifies it.
    /// </summary>
    public static Client BuildClient()
    {
        var client = Client.Create(ConformanceClientId, "OIDF Conformance — Basic", requireClientSecret: true);

        foreach (var grantType in GrantTypes)
            client.AddGrantType(grantType);
        foreach (var uri in RedirectUris)
            client.AddRedirectUri(uri);
        foreach (var scope in Scopes)
            client.AddScope(scope);

        client.AddSecret(HashSecret(ConformanceClientSecret), description: "Conformance static secret");

        return client;
    }

    /// <summary>Builds the login-able conformance test user with a verified email.</summary>
    public static User BuildUser(IPasswordHasher passwordHasher)
    {
        var user = User.Create(
            subjectId: TestUserSubjectId,
            username: TestUsername,
            email: TestUserEmail,
            passwordHash: passwordHasher.Hash(TestUserPassword));

        // email_verified must be present and true for the email-scope claims check.
        user.Update(username: null, email: null, emailConfirmed: true);

        return user;
    }

    /// <summary>
    /// Hashes a client secret identically to <c>ClientAuthenticator</c> (base64 of SHA-256) so the
    /// seeded secret authenticates at the token endpoint.
    /// </summary>
    public static string HashSecret(string secret) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
