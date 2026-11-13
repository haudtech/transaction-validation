namespace TransactionValidation.Gateway.PolicyStore.Entities;

public sealed class GatewayClientApiLimitEntity
{
    public required string ClientId { get; set; }

    public required string ApiId { get; set; }

    public required int RatePerMinute { get; set; }

    public required int Burst { get; set; }

    public required long DailyQuota { get; set; }

    public required long MonthlyQuota { get; set; }

    public GatewayClientEntity Client { get; set; } = null!;
}
