namespace RufistoWard.Identity.Entities;

public sealed class ClientSecret
{
    public int Id { get; private set; }
    public int ClientId { get; private set; }
    public string Value { get; private set; } = default!;
    public string? Description { get; private set; }
    public DateTime? Expiration { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ClientSecret() { }
}
