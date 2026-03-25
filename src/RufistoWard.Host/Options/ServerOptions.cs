namespace RufistoWard.Core.Options;

using System.ComponentModel.DataAnnotations;

public sealed record ServerOptions : IValidatableObject
{
    [Required]
    public bool BehindReverseProxy { get; init; }

    public string[] KnownProxyNetworks { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BehindReverseProxy && KnownProxyNetworks.Length == 0)
        {
            yield return new ValidationResult(
                "KnownProxyNetworks must contain at least one entry when BehindReverseProxy is true.",
                [nameof(KnownProxyNetworks)]);
        }
    }
}
