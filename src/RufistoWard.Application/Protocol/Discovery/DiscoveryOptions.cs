namespace RufistoWard.Auth.Protocol.Discovery;

public sealed record DiscoveryOptions
{
    public string Issuer { get; set; } = default!;
}
