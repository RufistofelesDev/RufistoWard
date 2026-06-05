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

    /// <summary>
    /// Identifies the originating-grant family. The root grant of an auth-code or device-code
    /// exchange stamps a new value; every refresh-rotation descendant inherits its parent's value
    /// unchanged. Reuse of any grant in a family revokes the whole family (RFC 6819 §5.2.2.3).
    /// </summary>
    public Guid FamilyId { get; private set; }

    private PersistedGrant() { }

    public static PersistedGrant Create(string id, string type, string clientId, string? subjectId, string data, DateTime? expiration = null, Guid? familyId = null)
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
            FamilyId = familyId ?? Guid.NewGuid(),
        };
    }

    public void UpdateData(string data) => Data = data;
    public void MarkConsumed() => ConsumedAt = DateTime.UtcNow;
    public bool IsConsumed => ConsumedAt.HasValue;
    public bool IsExpired => Expiration.HasValue && Expiration.Value < DateTime.UtcNow;
}
