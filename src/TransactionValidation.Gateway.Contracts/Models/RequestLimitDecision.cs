namespace TransactionValidation.Gateway.Contracts.Models;

public enum RequestLimitDenial
{
    None,
    RateLimit,
    DailyQuota,
    MonthlyQuota
}

public sealed record RequestLimitDecision(RequestLimitDenial Denial, TimeSpan RetryAfter)
{
    public bool Allowed => Denial == RequestLimitDenial.None;

    public static RequestLimitDecision Allow { get; } = new(RequestLimitDenial.None, TimeSpan.Zero);
}
