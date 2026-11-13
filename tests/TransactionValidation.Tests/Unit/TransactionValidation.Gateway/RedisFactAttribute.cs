using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RedisFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "GATEWAY_TEST_REDIS";

    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Set {EnvironmentVariable} (for example localhost:6380) to run Redis limiter coverage.";
        }
    }
}
