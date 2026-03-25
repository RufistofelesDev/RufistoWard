namespace RufistoWard.Core.Options;

using System.ComponentModel.DataAnnotations;

public sealed record KeyManagementOptions
{
    [Required]
    public string SigningKeyType { get; init; } = "RSA";

    [Required]
    public string SigningAlgorithm { get; init; } = "RS256";

    public string? StoragePath { get; init; }

    [Range(1, 3650)]
    public int RotationIntervalDays { get; init; } = 90;

    [Required]
    public string KeyStorageProvider { get; init; } = "FileSystem";
}
