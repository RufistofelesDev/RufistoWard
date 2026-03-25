namespace RufistoWard.Core.Options;

public sealed record SecurityHeaderOptions
{
    public bool EnableHsts { get; init; } = true;

    public bool EnableCsp { get; init; } = true;
}
