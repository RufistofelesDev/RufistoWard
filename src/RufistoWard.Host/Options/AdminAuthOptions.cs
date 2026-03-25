namespace RufistoWard.Core.Options;

using System.ComponentModel.DataAnnotations;

public sealed record AdminAuthOptions : IValidatableObject
{
    [Required]
    public string Mode { get; init; } = "Local";

    public string? OidcAuthority { get; init; }

    public string? OidcClientId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(Mode, "OIDC", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(OidcAuthority))
            {
                yield return new ValidationResult(
                    "OidcAuthority is required when Mode is 'OIDC'.",
                    [nameof(OidcAuthority)]);
            }

            if (string.IsNullOrWhiteSpace(OidcClientId))
            {
                yield return new ValidationResult(
                    "OidcClientId is required when Mode is 'OIDC'.",
                    [nameof(OidcClientId)]);
            }
        }
    }
}
