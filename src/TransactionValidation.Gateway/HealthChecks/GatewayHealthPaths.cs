using Microsoft.AspNetCore.Http;

namespace TransactionValidation.Gateway.HealthChecks;

public static class GatewayHealthPaths
{
    public const string Legacy = "/healthz";
    public const string Live = "/health/live";
    public const string Ready = "/health/ready";

    public static bool IsHealthProbe(PathString path) =>
        path.Equals(Legacy, StringComparison.OrdinalIgnoreCase)
        || path.Equals(Live, StringComparison.OrdinalIgnoreCase)
        || path.Equals(Ready, StringComparison.OrdinalIgnoreCase);
}
