namespace RufistoWard.Identity.Entities;

using RufistoWard.Identity.Interfaces;

public sealed class SigningKey : ITenantScoped
{
    public int Id { get; private set; }

    /// <summary>
    /// The tenant this row belongs to. Stamped on insert by the persistence layer.
    /// </summary>
    public int TenantId { get; private set; }
    public string KeyId { get; private set; } = default!;
    public string Algorithm { get; private set; } = default!;
    public string KeyType { get; private set; } = default!;
    public string SerializedKey { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? ExpiresAt { get; private set; }

    private SigningKey() { }

    public static SigningKey Create(string keyId, string algorithm, string keyType, string serializedKey)
    {
        return new SigningKey
        {
            KeyId = keyId,
            Algorithm = algorithm,
            KeyType = keyType,
            SerializedKey = serializedKey,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
    }

    public void SetExpiration(DateTime expiresAt)
    {
        ExpiresAt = expiresAt;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
