namespace RufistoWard.Identity.ValueObjects;

public sealed record TokenLifetime
{
    private const int MaxSeconds = 2592000; // 30 days

    public int Seconds { get; }

    public TokenLifetime(int seconds)
    {
        if (seconds <= 0)
            throw new ArgumentException("TokenLifetime must be a positive number of seconds.", nameof(seconds));
        if (seconds > MaxSeconds)
            throw new ArgumentException($"TokenLifetime must not exceed {MaxSeconds} seconds (30 days).", nameof(seconds));
        Seconds = seconds;
    }

    public static implicit operator int(TokenLifetime lifetime) => lifetime.Seconds;
}
