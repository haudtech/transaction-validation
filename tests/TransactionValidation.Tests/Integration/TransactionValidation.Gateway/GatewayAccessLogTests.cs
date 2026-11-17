using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;

using TransactionValidation.Gateway.Middleware;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

public sealed class GatewayAccessLogTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public async Task Requests_LogMetadataWithoutCredentialsQueryOrBody()
    {
        await using var backend = new GatewayProxyTests.TestBackend();
        await backend.StartAsync();
        using var provider = new RecordingLoggerProvider();
        using var factory = new GatewayHostFactory(backend.Address, loggerProvider: provider);
        using var client = factory.CreateClient();

        using var validRequest = new HttpRequestMessage(HttpMethod.Post, "/transactions/v1/partner/transactions?secret=query-secret")
        {
            Content = new StringContent("body-secret")
        };
        validRequest.Headers.Authorization = AuthenticationHeaderValue.Parse(GatewayTestCredentials.Authorization);
        validRequest.Headers.Add("Correlation-Id", "test-correlation");
        using var validResponse = await client.SendAsync(validRequest);
        Assert.Equal(HttpStatusCode.Accepted, validResponse.StatusCode);

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Get, "/not-a-route?secret=query-secret");
        invalidRequest.Headers.TryAddWithoutValidation("Authorization", "ApiKey malformed-secret");
        invalidRequest.Headers.Add("Correlation-Id", "test-invalid");
        using var invalidResponse = await client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);

        var records = provider.Records.ToArray();
        Assert.Equal(2, records.Length);
        var validEntry = Assert.Single(records, r => r.Message.Contains("CorrelationId=test-correlation", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, validEntry.Level);
        Assert.Equal(2, validEntry.EventId.Id);
        Assert.Equal("test-client", validEntry.Fields["ClientId"]);
        Assert.Equal("test-key", validEntry.Fields["KeyId"]);
        Assert.Equal("transaction-validation", validEntry.Fields["ApiId"]);
        Assert.Equal("v1", validEntry.Fields["ApiVersion"]);
        Assert.Equal(202, validEntry.Fields["Status"]);
        Assert.IsType<double>(validEntry.Fields["LatencyMs"]);
        var valid = validEntry.Message;
        Assert.Contains("ClientId=test-client", valid, StringComparison.Ordinal);
        Assert.Contains("KeyId=test-key", valid, StringComparison.Ordinal);
        Assert.Contains("ApiId=transaction-validation", valid, StringComparison.Ordinal);
        Assert.Contains("RouteId=transactionValidationV1", valid, StringComparison.Ordinal);
        var invalidEntry = Assert.Single(records, r => r.Message.Contains("CorrelationId=test-invalid", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, invalidEntry.Level);
        Assert.Equal(1, invalidEntry.EventId.Id);
        Assert.DoesNotContain("ClientId", invalidEntry.Fields.Keys);
        var invalid = invalidEntry.Message;
        Assert.DoesNotContain("ClientId=", invalid, StringComparison.Ordinal);
        Assert.DoesNotContain("KeyId=", invalid, StringComparison.Ordinal);
        foreach (var record in records)
        {
            Assert.DoesNotContain(GatewayTestCredentials.ClientSecret, record.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(GatewayTestCredentials.BackendCredentialSecret, record.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("malformed-secret", record.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("query-secret", record.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("body-secret", record.Message, StringComparison.Ordinal);
        }
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AuthenticatedFailure_LogsWarningWithStructuredFields()
    {
        using var provider = new RecordingLoggerProvider();
        using var factory = new GatewayHostFactory("http://127.0.0.1:1/", loggerProvider: provider);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(GatewayTestCredentials.Authorization);

        using var response = await client.PostAsync("/transactions/v1/partner/transactions", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var entry = Assert.Single(provider.Records);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(3, entry.EventId.Id);
        Assert.Equal(502, entry.Fields["Status"]);
        Assert.Equal("test-client", entry.Fields["ClientId"]);
        Assert.Equal("upstream_unavailable", entry.Fields["Decision"]);
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message, IReadOnlyDictionary<string, object?> Fields);

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Records { get; } = new();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(
            categoryName == typeof(GatewayAccessLogMiddleware).FullName ? Records : null);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(ConcurrentQueue<LogEntry>? records) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => records is not null;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (records is not null && state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                records.Enqueue(new LogEntry(logLevel, eventId, formatter(state, exception), fields.ToDictionary(pair => pair.Key, pair => pair.Value)));
            }
        }
    }
}
