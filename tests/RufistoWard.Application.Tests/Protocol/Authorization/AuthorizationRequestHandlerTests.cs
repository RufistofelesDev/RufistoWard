namespace RufistoWard.Auth.Tests.Protocol.Authorization;

using RufistoWard.Auth.Protocol.Authorization;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

/// <summary>
/// Validation coverage for per-client PKCE enforcement (FR12, refined for Story 10.5).
/// The OIDF Basic OP profile never sends code_challenge, so PKCE must be enforced per
/// client policy (<see cref="Client.RequirePkce"/>, default true) rather than globally.
/// Voluntary PKCE from a PKCE-optional client must still be validated and carried through.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthorizationRequestHandlerTests
{
    private const string ClientId = "client-1";
    private const string RedirectUri = "https://example.com/cb";

    [Fact]
    public async Task Pkce_required_client_without_code_challenge_is_rejected()
    {
        var handler = CreateHandler(requirePkce: true);

        var result = await handler.ValidateAsync(Request(codeChallenge: null), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_request", result.Error!.Code);
        Assert.Contains("code_challenge is required", result.Error.Description);
    }

    [Fact]
    public async Task Pkce_optional_client_without_code_challenge_succeeds()
    {
        var handler = CreateHandler(requirePkce: false);

        var result = await handler.ValidateAsync(Request(codeChallenge: null), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.CodeChallenge);
        Assert.Null(result.Value.CodeChallengeMethod);
    }

    [Fact]
    public async Task Pkce_optional_client_with_code_challenge_carries_it_through()
    {
        var handler = CreateHandler(requirePkce: false);

        var result = await handler.ValidateAsync(
            Request(codeChallenge: "abc", codeChallengeMethod: "S256"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("abc", result.Value!.CodeChallenge);
        Assert.Equal("S256", result.Value.CodeChallengeMethod);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Code_challenge_with_non_s256_method_is_rejected_regardless_of_client_policy(bool requirePkce)
    {
        var handler = CreateHandler(requirePkce);

        var result = await handler.ValidateAsync(
            Request(codeChallenge: "abc", codeChallengeMethod: "plain"), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_request", result.Error!.Code);
        Assert.Contains("S256", result.Error.Description);
    }

    [Fact]
    public async Task Pkce_required_client_with_valid_code_challenge_succeeds()
    {
        var handler = CreateHandler(requirePkce: true);

        var result = await handler.ValidateAsync(
            Request(codeChallenge: "abc", codeChallengeMethod: "S256"), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("abc", result.Value!.CodeChallenge);
        Assert.Equal("S256", result.Value.CodeChallengeMethod);
    }

    private static AuthorizationRequestHandler CreateHandler(bool requirePkce)
    {
        var client = Client.Create(ClientId, "Test Client");
        client.AddRedirectUri(RedirectUri);
        client.Update(clientName: null, enabled: null, requirePkce: requirePkce,
            accessTokenLifetimeSeconds: null, refreshTokenLifetimeSeconds: null);
        return new AuthorizationRequestHandler(new FakeClientRepository(client));
    }

    private static AuthorizeRequest Request(string? codeChallenge, string? codeChallengeMethod = null) => new()
    {
        ResponseType = "code",
        ClientId = ClientId,
        RedirectUri = RedirectUri,
        Scope = "openid",
        CodeChallenge = codeChallenge,
        CodeChallengeMethod = codeChallengeMethod,
    };

    private sealed class FakeClientRepository(Client client) : IClientRepository
    {
        public Task<Client?> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Client?>(clientId == client.ClientId ? client : null);

        public Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Client>>([client]);

        public Task AddAsync(Client added, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Client updated, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Client deleted, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
