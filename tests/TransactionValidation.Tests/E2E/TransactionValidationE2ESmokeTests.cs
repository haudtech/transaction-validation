using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TransactionValidation.Core.Models;
using Xunit;

namespace TransactionValidation.Tests.E2E;

[CollectionDefinition("E2E", DisableParallelization = true)]
public sealed class E2ECollectionDefinition;

[Collection("E2E")]
/// <summary>
/// Runtime smoke tests that validate the deployed API boundary using real HTTP calls.
/// These tests are intended for docker-backed E2E verification, not in-memory host wiring.
/// </summary>
public sealed class TransactionValidationE2ESmokeTests : IClassFixture<E2ETestFixture>
{
    private readonly E2ETestFixture _fixture;

    public TransactionValidationE2ESmokeTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Trait("Category", "E2E")]
    [Trait("Feature", "RuntimeSmoke")]
    [Fact(DisplayName = "E2E root endpoint returns 200 when API key is valid")]
    /// <summary>
    /// Verifies that the root endpoint is reachable and authorized when a valid API key is sent.
    /// </summary>
    public async Task Root_WithValidApiKey_ReturnsOk()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-API-Key", _fixture.ApiKey);

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Trait("Category", "E2E")]
    [Trait("Feature", "RuntimeSmoke")]
    [Fact(DisplayName = "E2E transaction happy path returns 202 Accepted")]
    /// <summary>
    /// Verifies that a valid transaction request is accepted by the running API.
    /// </summary>
    public async Task CreateTransaction_HappyPath_ReturnsAccepted()
    {
        var id = Guid.NewGuid().ToString("N");
        var response = await PostAcceptedWithTimeoutRetriesAsync(
            CreateValidRequest($"e2e-accepted-{id}"),
            $"e2e-idem-accepted-{id}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Trait("Category", "E2E")]
    [Trait("Feature", "RuntimeSmoke")]
    [Fact(DisplayName = "E2E duplicate request with same Idempotency-Key replays 202 response")]
    /// <summary>
    /// Verifies idempotency replay behavior by asserting the second submission with
    /// the same idempotency key returns the same accepted response.
    /// </summary>
    public async Task CreateTransaction_DuplicateIdempotencyKey_ReplaysAcceptedResponse()
    {
        var id = Guid.NewGuid().ToString("N");
        var payload = CreateValidRequest($"e2e-duplicate-{id}");
        var idempotencyKey = $"e2e-idem-duplicate-{id}";

        using var first = await PostAcceptedWithTimeoutRetriesAsync(payload, idempotencyKey);
        using var second = await PostTransactionAsync(payload, idempotencyKey, includeApiKey: true);
        var firstBody = await first.Content.ReadFromJsonAsync<AcceptedResponseDto>();
        var secondBody = await second.Content.ReadFromJsonAsync<AcceptedResponseDto>();

        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(firstBody?.MessageId, secondBody?.MessageId);
        Assert.Equal(firstBody?.CorrelationId, secondBody?.CorrelationId);
        Assert.Equal("accepted", secondBody?.Status);
    }

    [Trait("Category", "E2E")]
    [Trait("Feature", "RuntimeSmoke")]
    [Fact(DisplayName = "E2E validation error returns 400 ProblemDetails")]
    /// <summary>
    /// Verifies request validation behavior by sending an invalid payload and asserting
    /// an RFC 7807 bad request response.
    /// </summary>
    public async Task CreateTransaction_InvalidPayload_ReturnsBadRequest()
    {
        var id = Guid.NewGuid().ToString("N");

        var payload = new PartnerTransactionRequest
        {
            PartnerId = string.Empty,
            TransactionReference = string.Empty,
            Amount = -5m,
            Currency = "ZZZ",
            Timestamp = DateTime.MinValue
        };

        using var response = await PostTransactionAsync(payload, $"e2e-idem-invalid-{id}", includeApiKey: true);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Bad Request", problem?.Title);
    }

    [Trait("Category", "E2E")]
    [Trait("Feature", "RuntimeSmoke")]
    [Fact(DisplayName = "E2E missing API key returns 401")]
    /// <summary>
    /// Verifies authentication enforcement by sending a request without the API key header.
    /// </summary>
    public async Task CreateTransaction_MissingApiKey_ReturnsUnauthorized()
    {
        var id = Guid.NewGuid().ToString("N");

        using var response = await PostTransactionAsync(
            CreateValidRequest($"e2e-no-key-{id}"),
            $"e2e-idem-no-key-{id}",
            includeApiKey: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAcceptedWithTimeoutRetriesAsync(
        PartnerTransactionRequest payload,
        string idempotencyKey)
    {
        const int maxAttempts = 20;
        HttpResponseMessage? lastResponse = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            lastResponse?.Dispose();
            lastResponse = await PostTransactionAsync(payload, idempotencyKey, includeApiKey: true);

            if (lastResponse.StatusCode == HttpStatusCode.Accepted)
            {
                return lastResponse;
            }

            if (!IsRetryableStatus(lastResponse.StatusCode))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new Xunit.Sdk.XunitException(
            $"Expected 202 Accepted within retries but got {(int?)lastResponse?.StatusCode} {lastResponse?.StatusCode}.");
    }

    private static bool IsRetryableStatus(HttpStatusCode statusCode)
    {
        // In docker runtime, transient startup dependencies can cause temporary
        // errors before the stack stabilizes.
        return statusCode is HttpStatusCode.NotFound
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.BadGateway;
    }

    private async Task<HttpResponseMessage> PostTransactionAsync(
        PartnerTransactionRequest payload,
        string idempotencyKey,
        bool includeApiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/partner/transactions")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        if (includeApiKey)
        {
            request.Headers.Add("X-API-Key", _fixture.ApiKey);
        }

        return await _fixture.Client.SendAsync(request);
    }

    private static PartnerTransactionRequest CreateValidRequest(string transactionReference)
    {
        return new PartnerTransactionRequest
        {
            PartnerId = "partner-e2e",
            TransactionReference = transactionReference,
            Amount = 120.50m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow
        };
    }

    private sealed class AcceptedResponseDto
    {
        public string MessageId { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
}

public sealed class E2ETestFixture : IAsyncLifetime
{
    public HttpClient Client { get; private set; } = null!;

    public string ApiKey { get; private set; } = "local-dev-api-key";

    public async Task InitializeAsync()
    {
        ApiKey = ResolveSetting("E2E_API_KEY")
            ?? ResolveSetting("SECURITY__APIKEY")
            ?? "local-dev-api-key";

        var host = ResolveSetting("E2E_API_HOST");

        if (string.IsNullOrWhiteSpace(host))
        {
            var hostPort = ResolveSetting("API_HOST_PORT") ?? "5000";
            host = $"http://localhost:{hostPort}";
        }

        Client = new HttpClient
        {
            BaseAddress = new Uri(host, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(30)
        };

        await WaitForMockReadinessAsync();
        await WaitForApiReadinessAsync();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    private async Task WaitForApiReadinessAsync()
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(2);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/");
                request.Headers.Add("X-API-Key", ApiKey);

                using var response = await Client.SendAsync(request);

                // Any HTTP response means the API process is reachable.
                if ((int)response.StatusCode >= 100)
                {
                    return;
                }
            }
            catch
            {
                // Ignore transient startup failures and continue polling.
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new Xunit.Sdk.XunitException(
            $"API was not reachable within startup timeout at '{Client.BaseAddress}'. " +
            "Ensure docker compose services are up and E2E_API_HOST/E2E_API_KEY are correct.");
    }

    private static async Task WaitForMockReadinessAsync()
    {
        var mockHost = ResolveSetting("E2E_MOCK_HOST") ?? "http://localhost:5002";
        using var client = new HttpClient { BaseAddress = new Uri(mockHost, UriKind.Absolute), Timeout = TimeSpan.FromSeconds(5) };
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(2);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            try
            {
                using var response = await client.GetAsync("/");
                if ((int)response.StatusCode >= 100)
                {
                    return;
                }
            }
            catch
            {
                // Ignore transient startup failures and continue polling.
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new Xunit.Sdk.XunitException(
            $"Mock verification service was not reachable within startup timeout at '{mockHost}'.");
    }

    private static string? ResolveSetting(string key)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var fromDotEnv = ReadFromDotEnv(key);
        if (!string.IsNullOrWhiteSpace(fromDotEnv))
        {
            return fromDotEnv;
        }

        return null;
    }

    private static string? ReadFromDotEnv(string key)
    {
        var dotEnvPath = FindDotEnvPath();
        if (dotEnvPath is null)
        {
            return null;
        }

        foreach (var rawLine in File.ReadLines(dotEnvPath))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var currentKey = line[..separatorIndex].Trim();
            if (!string.Equals(currentKey, key, StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            {
                value = value[1..^1];
            }

            return value;
        }

        return null;
    }

    private static string? FindDotEnvPath()
    {
        var searchRoots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var root in searchRoots)
        {
            var current = new DirectoryInfo(root);

            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }
        }

        return null;
    }
}
