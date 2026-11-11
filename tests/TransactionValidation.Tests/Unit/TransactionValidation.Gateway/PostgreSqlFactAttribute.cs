using Xunit;

namespace TransactionValidation.Tests.Unit.TransactionValidation.Gateway;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GATEWAY_TEST_POSTGRES")))
        {
            Skip = "Set GATEWAY_TEST_POSTGRES to run PostgreSQL integration coverage.";
        }
    }
}
