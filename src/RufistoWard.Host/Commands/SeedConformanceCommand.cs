namespace RufistoWard.Core.Commands;

using System.Security.Cryptography;
using System.Text;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

/// <summary>
/// Seeds the deterministic OpenID Foundation conformance fixtures (Story 10.5): a static
/// confidential client with a known <c>client_id</c>/<c>client_secret</c> and a login-able test
/// user. The Admin API cannot pin a stable <c>client_id</c> (it is server-generated), so the
/// static-client conformance configs rely on this command for reproducible runs.
/// Idempotent: re-running skips fixtures that already exist and still returns success.
/// </summary>
public static class SeedConformanceCommand
{
    // Must match tests/conformance/config/test-plans/rufistoward-basic.json (client block).
    public const string ConformanceClientId = "conformance-basic";
    public const string ConformanceClientSecret = "conformance-basic-secret";

    // The basic plan's `client2` block. oidcc-refresh-token needs a genuinely SEPARATE client (it
    // checks a refresh token issued to one client cannot be redeemed by another), so this must not
    // be an alias of ConformanceClientId. The plan's "client for client_secret_post" block can
    // reuse the primary client — RufistoWard accepts both client_secret_basic and client_secret_post.
    public const string SecondClientId = "conformance-basic-2";
    public const string SecondClientSecret = "conformance-basic-2-secret";

    public const string TestUserSubjectId = "conformance-test-user";
    public const string TestUsername = "testuser";
    public const string TestUserEmail = "testuser@conformance.local";
    public const string TestUserPassword = "Conformance!1";

    public static readonly string[] GrantTypes = ["authorization_code", "refresh_token"];
    public static readonly string[] Scopes = ["openid", "profile", "email"];

    // The suite alias drives the callback path: {base}/test/a/{alias}/callback.
    public const string Alias = "rufistoward-basic";

    // Tunnel-first (Story 10.5): the HOSTED OIDF suite is the primary target. The self-hosted
    // (docker-compose) suite uses the emobix wildcard host and is the deferred path (10.5a).
    public const string HostedSuiteRedirectBase = "https://www.certification.openid.net";
    public const string SelfHostedSuiteRedirectBase = "https://localhost.emobix.co.uk:8443";
    public const string DefaultRedirectBase = HostedSuiteRedirectBase;

    // Override precedence: --redirect-base <url>  >  CONFORMANCE_REDIRECT_BASE env  >  default.
    private const string RedirectBaseEnvVar = "CONFORMANCE_REDIRECT_BASE";

    /// <summary>
    /// The suite registers two callback URIs per alias (one bare, one with dummy query params).
    /// Built from the redirect base so the same command serves the hosted and self-hosted suites.
    /// </summary>
    public static string[] BuildRedirectUris(string redirectBase)
    {
        var callback = $"{redirectBase.TrimEnd('/')}/test/a/{Alias}/callback";
        return [callback, $"{callback}?dummy1=lorem&dummy2=ipsum"];
    }

    /// <summary>Redirect URIs for the default (hosted) suite — convenience for callers/tests.</summary>
    public static string[] DefaultRedirectUris => BuildRedirectUris(DefaultRedirectBase);

    /// <summary>
    /// The only host environment in which seeding is permitted. See the guard in
    /// <see cref="ExecuteAsync"/> for why this is enforced rather than merely documented.
    /// </summary>
    public const string RequiredEnvironment = "Conformance";

    public static async Task<int> ExecuteAsync(IServiceProvider services, string[] args)
    {
        // SECURITY (Story 10.5b): these fixtures use FIXED credentials that are now PUBLISHED in the
        // OIDF certification logs, and the client is seeded with RequirePkce=false. Seeding them into
        // a real database hands anyone a working confidential client, so refuse anywhere but the
        // Conformance environment. This must stay BEFORE any write below — an exit code alone is not
        // a guard if the rows have already landed.
        var environment = services.GetRequiredService<IHostEnvironment>();
        if (!environment.IsEnvironment(RequiredEnvironment))
        {
            Console.Error.WriteLine(
                $"Refusing to seed: seed-conformance requires ASPNETCORE_ENVIRONMENT={RequiredEnvironment}, "
                + $"but the host is running as '{environment.EnvironmentName}'.");
            Console.Error.WriteLine(
                "These fixtures use fixed, publicly-known credentials (published in the OIDF "
                + "certification logs) and disable PKCE. They must never reach a real database.");
            return 1;
        }

        var redirectBase = ResolveRedirectBase(args);

        using var scope = services.CreateScope();
        var clientRepository = scope.ServiceProvider.GetRequiredService<IClientRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var existingClient = await clientRepository.FindByClientIdAsync(ConformanceClientId);
        if (existingClient is null)
        {
            await clientRepository.AddAsync(BuildClient(redirectBase));
            Console.WriteLine($"Conformance client created: {ConformanceClientId} (redirect base: {redirectBase})");
        }
        else
        {
            Console.WriteLine($"Conformance client '{ConformanceClientId}' already exists — skipping.");
        }

        var existingSecondClient = await clientRepository.FindByClientIdAsync(SecondClientId);
        if (existingSecondClient is null)
        {
            await clientRepository.AddAsync(BuildSecondClient(redirectBase));
            Console.WriteLine($"Conformance second client created: {SecondClientId}");
        }
        else
        {
            Console.WriteLine($"Conformance second client '{SecondClientId}' already exists — skipping.");
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
    /// Builds the static confidential conformance client: authorization-code + refresh-token
    /// grants, the standard OIDC scopes, both suite callbacks (derived from
    /// <paramref name="redirectBase"/>), and a known secret hashed the same way
    /// <c>ClientAuthenticator</c> verifies it. PKCE is NOT required: the OIDF Basic OP profile
    /// never sends code_challenge, so the suite's authorization requests would be rejected.
    /// </summary>
    public static Client BuildClient(string redirectBase)
    {
        var client = Client.Create(ConformanceClientId, "OIDF Conformance — Basic", requireClientSecret: true);
        client.Update(clientName: null, enabled: null, requirePkce: false,
            accessTokenLifetimeSeconds: null, refreshTokenLifetimeSeconds: null);

        foreach (var grantType in GrantTypes)
            client.AddGrantType(grantType);
        foreach (var uri in BuildRedirectUris(redirectBase))
            client.AddRedirectUri(uri);
        foreach (var scope in Scopes)
            client.AddScope(scope);

        client.AddSecret(HashSecret(ConformanceClientSecret), description: "Conformance static secret");

        return client;
    }

    /// <summary>
    /// Builds the plan's <c>client2</c>: same shape as the primary conformance client but a distinct
    /// <c>client_id</c>/secret, so cross-client checks (notably oidcc-refresh-token) are meaningful.
    /// </summary>
    public static Client BuildSecondClient(string redirectBase)
    {
        var client = Client.Create(SecondClientId, "OIDF Conformance — Basic (second client)", requireClientSecret: true);
        client.Update(clientName: null, enabled: null, requirePkce: false,
            accessTokenLifetimeSeconds: null, refreshTokenLifetimeSeconds: null);

        foreach (var grantType in GrantTypes)
            client.AddGrantType(grantType);
        foreach (var uri in BuildRedirectUris(redirectBase))
            client.AddRedirectUri(uri);
        foreach (var scope in Scopes)
            client.AddScope(scope);

        client.AddSecret(HashSecret(SecondClientSecret), description: "Conformance second-client secret");

        return client;
    }

    /// <summary>
    /// Resolves the suite callback base: <c>--redirect-base &lt;url&gt;</c> arg, then the
    /// <c>CONFORMANCE_REDIRECT_BASE</c> env var, then the default (hosted) suite.
    /// </summary>
    private static string ResolveRedirectBase(string[] args)
    {
        for (int i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--redirect-base")
                return args[i + 1];
        }

        var fromEnv = Environment.GetEnvironmentVariable(RedirectBaseEnvVar);
        return string.IsNullOrWhiteSpace(fromEnv) ? DefaultRedirectBase : fromEnv;
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
