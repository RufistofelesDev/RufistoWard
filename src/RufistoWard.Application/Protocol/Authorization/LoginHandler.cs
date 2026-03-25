namespace RufistoWard.Auth.Protocol.Authorization;

using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Identity.Interfaces;

public sealed record LoginRequest(string UsernameOrEmail, string Password);

public sealed record LoginResult(string SubjectId, string Username, string Email);

public sealed class LoginHandler(
    IPasswordHasher passwordHasher,
    IUserRepository userRepository)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<Result<LoginResult>> HandleAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<LoginResult>.Failure(new ProtocolError("login_failed", "Invalid credentials."));
        }

        // Find user by username or email
        var user = await userRepository.FindByUsernameAsync(request.UsernameOrEmail, cancellationToken)
            ?? await userRepository.FindByEmailAsync(request.UsernameOrEmail.ToLowerInvariant(), cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Result<LoginResult>.Failure(new ProtocolError("login_failed", "Invalid credentials."));
        }

        // Check lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            return Result<LoginResult>.Failure(new ProtocolError("login_failed", "Invalid credentials."));
        }

        // Verify password
        var verifyResult = passwordHasher.Verify(user.PasswordHash, request.Password);

        if (verifyResult == PasswordVerificationResult.Failed)
        {
            user.RecordFailedLogin(MaxFailedAttempts, LockoutDuration);
            await userRepository.UpdateAsync(user, cancellationToken);
            return Result<LoginResult>.Failure(new ProtocolError("login_failed", "Invalid credentials."));
        }

        // Success — reset failed login count
        user.RecordSuccessfulLogin();
        await userRepository.UpdateAsync(user, cancellationToken);

        return Result<LoginResult>.Success(new LoginResult(user.SubjectId, user.Username, user.Email));
    }
}
