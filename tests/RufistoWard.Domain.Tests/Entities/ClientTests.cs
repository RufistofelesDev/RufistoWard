namespace RufistoWard.Identity.Tests.Entities;

using RufistoWard.Identity.Entities;

[Trait("Category", "Unit")]
public sealed class ClientTests
{
    [Fact]
    public void Update_DisablingPkceOnPublicClient_Throws()
    {
        var client = Client.Create("public-app", "Public App", requireClientSecret: false);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            client.Update(clientName: null, enabled: null, requirePkce: false,
                accessTokenLifetimeSeconds: null, refreshTokenLifetimeSeconds: null));

        Assert.Contains("public client", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(client.RequirePkce); // unchanged
    }

    [Fact]
    public void Update_DisablingPkceOnConfidentialClient_Succeeds()
    {
        var client = Client.Create("confidential-app", "Confidential App", requireClientSecret: true);

        client.Update(clientName: null, enabled: null, requirePkce: false,
            accessTokenLifetimeSeconds: null, refreshTokenLifetimeSeconds: null);

        Assert.False(client.RequirePkce);
    }

    [Fact]
    public void Update_KeepingPkceEnabledOnPublicClient_Succeeds()
    {
        var client = Client.Create("public-app", "Public App", requireClientSecret: false);

        client.Update(clientName: null, enabled: null, requirePkce: true,
            accessTokenLifetimeSeconds: null, refreshTokenLifetimeSeconds: null);

        Assert.True(client.RequirePkce);
    }
}
