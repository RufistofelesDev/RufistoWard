namespace RufistoWard.Identity.Entities;

using RufistoWard.Identity.Interfaces;

public sealed class DeviceCode : ITenantScoped
{
    public int Id { get; private set; }

    /// <summary>
    /// The tenant this row belongs to. Stamped on insert by the persistence layer.
    /// </summary>
    public int TenantId { get; private set; }
    public string DeviceCodeValue { get; private set; } = default!;
    public string UserCode { get; private set; } = default!;
    public string? SubjectId { get; private set; }
    public string ClientId { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime Expiration { get; private set; }
    public string Data { get; private set; } = default!;

    private DeviceCode() { }
}
