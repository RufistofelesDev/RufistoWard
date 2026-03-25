namespace RufistoWard.Identity.Entities;

public sealed class DeviceCode
{
    public int Id { get; private set; }
    public string DeviceCodeValue { get; private set; } = default!;
    public string UserCode { get; private set; } = default!;
    public string? SubjectId { get; private set; }
    public string ClientId { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime Expiration { get; private set; }
    public string Data { get; private set; } = default!;

    private DeviceCode() { }
}
