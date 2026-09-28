namespace RateLimiting.ExternalHelpers;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public GlobalRateLimitSettings Global { get; init; } = new();
    public BurstRateLimitSettings UserBurst { get; init; } = new();
    public BurstRateLimitSettings Burst { get; init; } = new();
    public ConcurrencyRateLimitSettings Concurrency { get; init; } = new();
    public StrictRateLimitSettings Strict { get; init; } = new();
    public SlidingRateLimitSettings Sliding { get; init; } = new();

}

public static class RateLimitingPolicies
{
    public const string Global = "global";
    public const string Burst = "burst";
    public const string Expensive = "expensive";
    public const string Strict = "strict";
    public const string Sliding = "sliding";
    public const string UserBurst = "user-burst";
}

public class SlidingRateLimitSettings
{
    public int PermitLimit { get; init; } = 20;
    public int WindowSeconds { get; init; } = 60;
    public int SegmentsPerWindow { get; init; } = 6;
    public int QueueLimit { get; init; }
}

public class GlobalRateLimitSettings
{
    public int PermitLimit { get; init; } = 50;
    public int WindowSeconds { get; init; } = 60;
    public int QueueLimit { get; init; }
}

public class StrictRateLimitSettings
{
    public int PermitLimit { get; init; } = 20;
    public int WindowSeconds { get; init; } = 60;
    public int QueueLimit { get; init; }
}

public class BurstRateLimitSettings
{
    public int TokenLimit { get; init; } = 100;
    public int TokensPerPeriod { get; init; } = 50;
    public int ReplenishmentPeriodSeconds { get; init; } = 10;
    public int QueueLimit { get; init; }
}

public class ConcurrencyRateLimitSettings
{
    public int PermitLimit { get; init; } = 50;
    public int QueueLimit { get; init; } = 10;
}
