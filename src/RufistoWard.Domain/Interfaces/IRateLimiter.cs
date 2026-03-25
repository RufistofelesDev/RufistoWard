namespace RufistoWard.Identity.Interfaces;

public interface IRateLimiter
{
    Task<RateLimitResult> CheckAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record RateLimitResult(bool IsAllowed, int RetryAfterSeconds = 0);
