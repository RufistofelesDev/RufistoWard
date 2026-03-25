namespace RufistoWard.Identity.Entities;

public sealed class PersistedGrant
{
    public string Id { get; private set; } = default!;
    public string Type { get; private set; } = default!;
    public string? SubjectId { get; private set; }
    public string ClientId { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? Expiration { get; private set; }
    public string Data { get; private set; } = default!;
    public DateTime? ConsumedAt { get; private set; }

    private PersistedGrant() { }

    public static PersistedGrant Create(string id, string type, string clientId, string? subjectId, string data, DateTime? expiration = null)
    {
        return new PersistedGrant
        {
            Id = id,
            Type = type,
            ClientId = clientId,
            SubjectId = subjectId,
            Data = data,
            CreatedAt = DateTime.UtcNow,
            Expiration = expiration,
        };
    }

    public void MarkConsumed() => ConsumedAt = DateTime.UtcNow;
    public bool IsConsumed => ConsumedAt.HasValue;
    public bool IsExpired => Expiration.HasValue && Expiration.Value < DateTime.UtcNow;
}
