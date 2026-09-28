using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace RateLimiting.ExternalHelpers;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddEnterpriseRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
                        .BindConfiguration(RateLimitingOptions.SectionName)
                        .ValidateOnStart();

        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((options, rateLimitingOptions) =>
        {
            var settings = rateLimitingOptions.Value;

            ConfigureGlobalPolicy(options, settings.Global);
            ConfigureBurstPolicy(options, settings.Burst);
            ConfigureConcurrencyPolicy(options, settings.Concurrency);
            ConfigureStrictPolicy(options, settings.Strict);
            ConfigureSlidingPolicy(options, settings.Sliding);
            ConfigureUserBurstPolicy(options, settings.UserBurst);
            ConfigureRejectionHandler(options);
        });

        return services;
    }

    private static void ConfigureGlobalPolicy(RateLimiterOptions options, GlobalRateLimitSettings settings)
    {
        options.AddPolicy(RateLimitingPolicies.Global, httpContext =>
                {
                    var partitionKey = GetPartitionKey(httpContext);

                    return RateLimitPartition.GetFixedWindowLimiter(partitionKey,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.PermitLimit,
                            Window = System.TimeSpan.FromSeconds(settings.WindowSeconds),
                            QueueLimit = settings.QueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true
                        });
                });
    }

    private static void ConfigureBurstPolicy(RateLimiterOptions options, BurstRateLimitSettings settings)
    {
        options.AddTokenBucketLimiter(RateLimitingPolicies.Burst, limiter =>
            {
                limiter.TokenLimit = settings.TokenLimit;
                limiter.TokensPerPeriod = settings.TokensPerPeriod;
                limiter.ReplenishmentPeriod = System.TimeSpan.FromSeconds(settings.ReplenishmentPeriodSeconds);
                limiter.QueueLimit = settings.QueueLimit;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.AutoReplenishment = true;
            });
    }

    private static void ConfigureConcurrencyPolicy(RateLimiterOptions options, ConcurrencyRateLimitSettings settings)
    {
        options.AddConcurrencyLimiter(RateLimitingPolicies.Expensive, limiter =>
            {
                limiter.PermitLimit = settings.PermitLimit;
                limiter.QueueLimit = settings.QueueLimit;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
    }

    private static void ConfigureStrictPolicy(RateLimiterOptions options, StrictRateLimitSettings settings)
    {
        options.AddFixedWindowLimiter(RateLimitingPolicies.Strict, limiter =>
            {
                limiter.PermitLimit = settings.PermitLimit;
                limiter.Window = System.TimeSpan.FromSeconds(settings.WindowSeconds);
                limiter.QueueLimit = settings.QueueLimit;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.AutoReplenishment = true;
            });
    }

    private static void ConfigureSlidingPolicy(RateLimiterOptions options, SlidingRateLimitSettings settings)
    {
        options.AddSlidingWindowLimiter(RateLimitingPolicies.Sliding, limiter =>
        {
            limiter.PermitLimit = settings.PermitLimit;
            limiter.Window = TimeSpan.FromSeconds(settings.WindowSeconds);
            limiter.SegmentsPerWindow = settings.SegmentsPerWindow;
            limiter.QueueLimit = settings.QueueLimit;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiter.AutoReplenishment = true;
        });
    }

    private static void ConfigureUserBurstPolicy(RateLimiterOptions options, BurstRateLimitSettings settings)
    {
        options.AddPolicy(RateLimitingPolicies.UserBurst, httpContext =>
        {
            var partitionKey = GetPartitionKey(httpContext);

            return RateLimitPartition.GetTokenBucketLimiter(
                partitionKey,
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = settings.TokenLimit,
                    TokensPerPeriod = settings.TokensPerPeriod,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(
                        settings.ReplenishmentPeriodSeconds),
                    QueueLimit = settings.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        });
    }

    private static void ConfigureRejectionHandler(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, token) =>
        {
            var response = context.HttpContext.Response;
            var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
            var limiterType = policy switch
            {
                RateLimitingPolicies.Global => "fixed-window",
                RateLimitingPolicies.Burst => "token-bucket",
                RateLimitingPolicies.Expensive => "concurrency",
                RateLimitingPolicies.Strict => "fixed-window",
                RateLimitingPolicies.Sliding => "sliding-window",
                RateLimitingPolicies.UserBurst => "token-bucket",
                _ => "unknown"
            };

            response.StatusCode = StatusCodes.Status429TooManyRequests;
            response.ContentType = "application/problem+json";
            response.Headers["X-RateLimit-Exceeded"] = "true";

            int? retryAfterSeconds = null;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
            }

            await response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/429",
                title = "Too Many Requests",
                status = 429,
                detail = $"The '{policy}' rate limit ({limiterType}) has been exceeded.",
                policy,
                limiterType,
                retryAfterSeconds,
                traceId = context.HttpContext.TraceIdentifier
            },
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: token);
        };
    }

    private static string GetPartitionKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return $"user:{userId}";
            }
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        return !string.IsNullOrWhiteSpace(ip) ? $"ip:{ip}" : "anonymous";
    }
}
