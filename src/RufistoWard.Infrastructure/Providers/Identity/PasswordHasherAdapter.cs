namespace RufistoWard.Tokens.Providers.Identity;

using Microsoft.AspNetCore.Identity;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

using DomainResult = RufistoWard.Identity.Interfaces.PasswordVerificationResult;
using IdentityResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password)
        => _inner.HashPassword(null!, password);

    public DomainResult Verify(string hashedPassword, string providedPassword)
    {
        var result = _inner.VerifyHashedPassword(null!, hashedPassword, providedPassword);
        return result switch
        {
            IdentityResult.Success => DomainResult.Success,
            IdentityResult.SuccessRehashNeeded => DomainResult.SuccessRehashNeeded,
            _ => DomainResult.Failed,
        };
    }
}
