namespace TransactionValidation.Gateway.Errors;

public static class GatewayErrorCodes
{
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string PayloadTooLarge = "payload_too_large";
    public const string RateLimitExceeded = "rate_limit_exceeded";
    public const string QuotaExceeded = "quota_exceeded";
    public const string PolicyUnavailable = "policy_unavailable";
    public const string LimiterUnavailable = "limiter_unavailable";
    public const string UpstreamUnavailable = "upstream_unavailable";
    public const string UpstreamTimeout = "upstream_timeout";
    public const string InternalError = "internal_error";
}
