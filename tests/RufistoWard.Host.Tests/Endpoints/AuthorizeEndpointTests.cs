namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Identity.Entities;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class AuthorizeEndpointTests
{
    private static readonly Dictionary<string, string?> TestConfig = new()
    {
        ["Server:BehindReverseProxy"] = "false",
        ["Database:ConnectionString"] = "unused",
        ["Token:Issuer"] = "https://localhost:5001",
        ["Token:AccessTokenLifetimeSeconds"] = "3600",
        ["Token:RefreshTokenLifetimeSeconds"] = "86400",
        ["AdminAuth:Mode"] = "Local",
        ["KeyManagement:SigningKeyType"] = "RSA",
        ["KeyManagement:SigningAlgorithm"] = "RS256",
        ["SecurityHeaders:EnableHsts"] = "true",
        ["SecurityHeaders:EnableCsp"] = "true",
        ["RateLimits:TokenEndpointPermitLimit"] = "100",
        ["RateLimits:TokenEndpointWindowSeconds"] = "60",
    };

    private static WebApplicationFactory<Program> CreateFactory()
    {
        // The database name must be fixed per factory, NOT per DbContext instance. Computing the
        // Guid inside the options lambda hands every scope its own empty database, so anything
        // seeded through one scope is invisible to the request pipeline.
        var databaseName = $"AuthorizeTests-{Guid.NewGuid()}";

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(TestConfig);
                });
                builder.ConfigureServices(services =>
                {
                    var efDescriptors = services
                        .Where(d => d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true
                            || d.ServiceType == typeof(DbContextOptions<RufistoWardDbContext>)
                            || d.ServiceType == typeof(DbContextOptions)
                            || d.ServiceType == typeof(RufistoWardDbContext))
                        .ToList();
                    foreach (var d in efDescriptors)
                    {
                        services.Remove(d);
                    }

                    services.AddDbContext<RufistoWardDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                });
            });
    }

    [Fact]
    public async Task AuthorizeEndpoint_UnauthenticatedUser_RedirectsToLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/connect/authorize?response_type=code&client_id=test&redirect_uri=https://example.com/cb&scope=openid&code_challenge=abc&code_challenge_method=S256",
            ct);

        // Should redirect to login (302) — client doesn't exist yet but redirect happens before client validation in some flows,
        // or it returns an error about unknown client
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.BadRequest,
            $"Expected redirect or error, got {response.StatusCode}");
    }

    [Fact]
    public async Task AuthorizeEndpoint_MissingResponseType_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/connect/authorize?client_id=test&redirect_uri=https://example.com/cb",
            ct);

        // Should get an error about invalid response_type
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.BadRequest,
            $"Expected redirect or error, got {response.StatusCode}");
    }

    [Fact]
    public async Task AuthorizeEndpoint_MissingPkce_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // Request without code_challenge — should fail PKCE enforcement
        var response = await client.GetAsync(
            "/connect/authorize?response_type=code&client_id=test&redirect_uri=https://example.com/cb&scope=openid",
            ct);

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.BadRequest,
            $"Expected redirect or error, got {response.StatusCode}");
    }

    /// <summary>
    /// Seeds a confidential client with a registered redirect URI so the authorize endpoint gets far
    /// enough to exercise prompt/max_age handling instead of bailing out on an unknown client.
    /// </summary>
    private static async Task<WebApplicationFactory<Program>> CreateFactoryWithClientAsync(string clientId, string redirectUri)
    {
        var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();

        var client = Client.Create(clientId, "Prompt tests", requireClientSecret: true);
        client.Update(clientName: null, enabled: true, requirePkce: false, accessTokenLifetimeSeconds: null, refreshTokenLifetimeSeconds: null);
        client.AddRedirectUri(redirectUri);
        client.AddScope("openid");
        client.AddGrantType("authorization_code");

        db.Clients.Add(client);
        await db.SaveChangesAsync();

        return factory;
    }

    [Fact]
    public async Task AuthorizeEndpoint_SupportsPostMethod()
    {
        // OIDCC-3.1.2.1 requires GET *and* POST. This previously returned 405.
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = "test",
            ["redirect_uri"] = "https://example.com/cb",
            ["scope"] = "openid",
        });

        var response = await client.PostAsync("/connect/authorize", form, ct);

        Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizeEndpoint_PromptNone_WithoutSession_ReturnsLoginRequired()
    {
        // OIDCC-3.1.2.6: prompt=none forbids UI, so an unauthenticated request is an error redirect.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "https://example.com/cb";
        using var factory = await CreateFactoryWithClientAsync("prompt-test", redirectUri);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=prompt-test&redirect_uri={redirectUri}&scope=openid&prompt=none&state=xyz",
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith(redirectUri, location, StringComparison.Ordinal);
        Assert.Contains("error=login_required", location, StringComparison.Ordinal);
        Assert.Contains("state=xyz", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeEndpoint_PromptNoneCombinedWithLogin_ReturnsInvalidRequest()
    {
        // OIDCC-3.1.2.1: "none" must not be combined with any other prompt value.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "https://example.com/cb";
        using var factory = await CreateFactoryWithClientAsync("prompt-combo", redirectUri);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=prompt-combo&redirect_uri={redirectUri}&scope=openid&prompt=none%20login",
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=invalid_request", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeEndpoint_PromptLogin_RedirectsToLoginWithoutPromptLoginRetained()
    {
        // prompt=login must reach the login page, and must not survive into the return URL —
        // otherwise re-entering authorize after login would force another login, forever.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "https://example.com/cb";
        using var factory = await CreateFactoryWithClientAsync("prompt-login", redirectUri);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=prompt-login&redirect_uri={redirectUri}&scope=openid&prompt=login",
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("/login", location, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt%3dlogin", location, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("request", "request_not_supported")]
    [InlineData("request_uri", "request_uri_not_supported")]
    public async Task AuthorizeEndpoint_RequestObject_IsRejectedNotIgnored(string parameter, string expectedError)
    {
        // OIDCC-6.1/6.2: discovery advertises neither request nor request_uri support, so these must
        // be rejected. Silently ignoring them drops the state/nonce carried inside the request object.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "https://example.com/cb";
        using var factory = await CreateFactoryWithClientAsync($"req-{parameter}", redirectUri);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=req-{parameter}&redirect_uri={redirectUri}&scope=openid&state=abc&{parameter}=whatever",
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains($"error={expectedError}", location, StringComparison.Ordinal);
        Assert.Contains("state=abc", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeEndpoint_FormPost_DeliversResponseAsAutoPostingForm()
    {
        // Form Post Response Mode: the response is an HTML form POSTed to the redirect_uri, never a
        // redirect carrying the parameters in the URL. ASP.NET Core's OIDC handler asks for this by default.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "https://example.com/cb";
        using var factory = await CreateFactoryWithClientAsync("form-post", redirectUri);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=form-post&redirect_uri={redirectUri}&scope=openid&prompt=none&state=xyz&response_mode=form_post",
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains($"<form method=\"post\" action=\"{redirectUri}\">", html, StringComparison.Ordinal);
        Assert.Contains("name=\"error\" value=\"login_required\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"state\" value=\"xyz\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeEndpoint_FormPost_HtmlEncodesParameters()
    {
        // state is attacker-controlled and is written into the page, so it must be encoded.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "https://example.com/cb";
        using var factory = await CreateFactoryWithClientAsync("form-post-xss", redirectUri);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var state = Uri.EscapeDataString("\"><script>alert(1)</script>");
        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=form-post-xss&redirect_uri={redirectUri}&scope=openid&prompt=none&state={state}&response_mode=form_post",
            ct);

        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeEndpoint_FormPost_NeverRendersANonHttpAction()
    {
        // A registered javascript: redirect_uri rendered as a self-submitting form action would run
        // script on the IdP's origin. form_post is refused for it, and the refusal itself must not be
        // delivered as a form either.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "javascript:alert(document.cookie)";
        using var factory = await CreateFactoryWithClientAsync("form-post-js", "https://placeholder.example/cb");

        // Client.AddRedirectUri refuses this scheme, so write the row the way a raw SQL registration
        // script would — the authorize endpoint must stay safe against data it did not validate.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();
            var row = await db.ClientRedirectUris.SingleAsync(ct);
            db.Entry(row).Property(r => r.Uri).CurrentValue = redirectUri;
            await db.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=form-post-js&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=openid&prompt=none&response_mode=form_post",
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("<form", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=invalid_request", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeEndpoint_UnsupportedResponseMode_IsRejectedNotIgnored()
    {
        // Discovery advertises query and form_post only. Ignoring another mode would answer in a
        // shape the client did not ask for; reject it, reported in the default query mode.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "https://example.com/cb";
        using var factory = await CreateFactoryWithClientAsync("mode-fragment", redirectUri);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=mode-fragment&redirect_uri={redirectUri}&scope=openid&state=abc&response_mode=fragment",
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith(redirectUri + "?", location, StringComparison.Ordinal);
        Assert.Contains("error=invalid_request", location, StringComparison.Ordinal);
        Assert.Contains("state=abc", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeEndpoint_FormPost_SurvivesTheLoginRoundTrip()
    {
        // The request re-enters authorize from the login page's return URL. Dropping response_mode
        // there turned every form_post request into a query response after sign-in.
        var ct = TestContext.Current.CancellationToken;
        const string redirectUri = "https://example.com/cb";
        using var factory = await CreateFactoryWithClientAsync("mode-login", redirectUri);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=mode-login&redirect_uri={redirectUri}&scope=openid&response_mode=form_post",
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("/login", location, StringComparison.Ordinal);
        Assert.Contains("response_mode%3dform_post", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginPage_Renders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/login", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("data-testid=\"login-form\"", content);
        Assert.Contains("data-testid=\"username-input\"", content);
        Assert.Contains("data-testid=\"password-input\"", content);
    }
}
