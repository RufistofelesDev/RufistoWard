namespace RufistoWard.Identity.Interfaces;

public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string hashedPassword, string providedPassword);
}
