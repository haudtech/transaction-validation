namespace TransactionValidation.Gateway.Contracts.Models;

/// <summary>Explicit finite limits for one client and API, shared across API versions.</summary>
public sealed record ClientApiLimit(
    string ApiId,
    int RatePerMinute,
    int Burst,
    long DailyQuota,
    long MonthlyQuota);
