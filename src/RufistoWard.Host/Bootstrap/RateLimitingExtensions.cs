namespace RufistoWard.Core.Bootstrap;

using System.Threading.RateLimiting;
using RufistoWard.Core.Options;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddRufistoWardRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new RateLimitOptions();
        configuration.GetSection("RateLimits").Bind(options);

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Only rate limit /connect/* endpoints
                if (!context.Request.Path.StartsWithSegments("/connect"))
                {
                    return RateLimitPartition.GetNoLimiter("no-limit");
                }

                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.TokenEndpointPermitLimit,
                    Window = TimeSpan.FromSeconds(options.TokenEndpointWindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                });
            });

            limiterOptions.OnRejected = async (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = options.TokenEndpointWindowSeconds.ToString();
                await Task.CompletedTask;
            };
        });

        return services;
    }
}
