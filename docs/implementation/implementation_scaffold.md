# Transaction Validation BFF Implementation Scaffold

This document describes a full scaffold for the assignment workflow based on:
- `docs/reqs/Senior_net_interview_2026.md`
- `docs/analysis/solution_analysis.md`
- `docs/diagram/use_case_sequence_diagram.md`

For actual implementation, follow the ordered phase plan in `docs/implementation/implementation_phases.md`.

The goal is to build a .NET 8 Web API that:
- Accepts `POST /api/v1/partner/transactions`
- Validates payload
- Verifies `partnerId` against a mock partner verifier
- Publishes the verified transaction to a local RabbitMQ queue
- Uses Polly for retries and timeouts
- Secures the endpoint with API key middleware
- Includes global exception handling and unit tests

---

## 1. Solution layout

Recommended projects inside a new solution called `TransactionValidation.sln`:

- `src/TransactionValidation.Api` — Web API startup and minimal program logic
- `src/TransactionValidation.Configuration` — shared configuration and service registration extensions
- `src/TransactionValidation.Core` — domain models, DTOs, validation, interfaces
- `src/TransactionValidation.Integration` — partner verification client and resilience
- `src/TransactionValidation.Messaging` — message publisher interfaces and RabbitMQ implementation
- `src/TransactionValidation.Mock` — mock partner verification API endpoint
- `tests/TransactionValidation.Tests` — xUnit tests

Example file tree:

```
TransactionValidation.sln
global.json
Directory.Build.props
Directory.Packages.props
.editorconfig
.github/
    workflows/
        ci.yml
        integration.yml
src/
  TransactionValidation.Api/
    Program.cs
    Controllers/PartnerTransactionsController.cs
    appsettings.json
    appsettings.Development.json
  TransactionValidation.Configuration/
    Extensions/ServiceCollectionExtensions.cs
    Middleware/ApiKeyMiddleware.cs
    Middleware/ApiExceptionHandler.cs
    Options/PartnerVerificationOptions.cs
    Options/RabbitMqOptions.cs
    Options/SecurityOptions.cs
    Options/ApiKeyOptions.cs
  TransactionValidation.Core/
    Models/PartnerTransactionRequest.cs
    Models/TransactionEnvelope.cs
    Models/ErrorResponse.cs
    Validation/PartnerTransactionValidator.cs
    Interfaces/IPartnerVerifier.cs
    Interfaces/IMessagePublisher.cs
  TransactionValidation.Integration/
    PartnerVerifierClient.cs
  TransactionValidation.Messaging/
    RabbitMqMessagePublisher.cs
  TransactionValidation.Mock/
    Controllers/MockPartnerVerificationController.cs
tests/
  TransactionValidation.Tests/
        Unit/
            TransactionValidation.Core/
                Models/
                    PlaceholderTests.cs
        Integration/
            IntegrationTest1.cs
```

### Repository-level standards (added)

The scaffold should include these repository-level files to keep builds and dependencies consistent:

- `global.json` - pin SDK to .NET 8 (`8.0.100`, `rollForward: latestMinor`)
- `Directory.Build.props` - shared MSBuild defaults (`net8.0`, nullable, implicit usings, deterministic build)
- `Directory.Packages.props` - central NuGet versions (Central Package Management)
- `.editorconfig` - C# formatting and using-order conventions
- `.github/workflows/ci.yml` - build + unit tests + format verification
- `.github/workflows/integration.yml` - integration tests in a separate workflow (auto on `main` + manual dispatch)

### Shared configuration project

The `TransactionValidation.Configuration` project centralizes Web API configuration and DI wiring that can be shared across services.

It should include:
- option classes for partner verification, RabbitMQ, API keys, and any shared service settings
- `IServiceCollection` extension methods to register external clients, messaging, authentication, and middleware
- common configuration binding and health check registration
- reusable startup wiring so `TransactionValidation.Api` remains minimal

### Configuration loading and override order
The solution should use a layered configuration handler so values override predictably from least-specific to most-specific.

Recommended precedence:
1. `appsettings.json`
2. `appsettings.{Environment}.json` (for example `appsettings.Development.json`)
3. `appsettings.Docker.json` or any container-specific JSON config file
4. `.env` file values if the loader is supported
5. runtime environment variables / container environment variables
6. command-line arguments

The centralized handler should build one shared `IConfiguration` instance and pass it into the shared startup extensions.

Example setup in `Program.cs`:
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Docker.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);
```

For `.env` support, load values before `CreateBuilder(args)` or via a helper like `DotNetEnv.Env.Load()`.

When using `.env`, prefer standard ASP.NET Core environment variable names that the configuration provider maps automatically. Example values:
```env
ASPNETCORE_ENVIRONMENT=Development
PARTNERVERIFICATION__BASEURL=https://localhost:5001/
RABBITMQ__HOSTNAME=localhost
RABBITMQ__QUEUENAME=partner-transactions
```

If you use `AddEnvironmentVariables()` without a custom prefix, the SDK will resolve section-style names like `RabbitMq__QueueName` automatically into `RabbitMq:QueueName`.

This handler should ensure:
- default values come from `appsettings.json`
- environment-specific JSON overrides base settings
- Docker/container JSON overrides environment settings when present
- environment variables override JSON
- command-line args have highest precedence

This makes the Web API more reusable and supports a future multi-service architecture.

---

## 2. New solution and projects

Run these commands from the repo root (`/Users/tech/dev/net/TransactionValidation`):

```bash
mkdir -p src tests
cd src

# Create projects
dotnet new webapi -n TransactionValidation.Api -f net8.0
dotnet new classlib -n TransactionValidation.Configuration -f net8.0
dotnet new classlib -n TransactionValidation.Core -f net8.0
dotnet new classlib -n TransactionValidation.Integration -f net8.0
dotnet new classlib -n TransactionValidation.Messaging -f net8.0
dotnet new webapi -n TransactionValidation.Mock -f net8.0
cd ../tests

dotnet new xunit -n TransactionValidation.Tests -f net8.0

# Create solution and add projects
cd ..
dotnet new sln -n TransactionValidation
dotnet sln add src/TransactionValidation.Api/TransactionValidation.Api.csproj
dotnet sln add src/TransactionValidation.Configuration/TransactionValidation.Configuration.csproj
dotnet sln add src/TransactionValidation.Core/TransactionValidation.Core.csproj
dotnet sln add src/TransactionValidation.Integration/TransactionValidation.Integration.csproj
dotnet sln add src/TransactionValidation.Messaging/TransactionValidation.Messaging.csproj
dotnet sln add src/TransactionValidation.Mock/TransactionValidation.Mock.csproj
dotnet sln add tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj

# Add project references
cd src/TransactionValidation.Api
dotnet add reference ../TransactionValidation.Configuration/TransactionValidation.Configuration.csproj
dotnet add reference ../TransactionValidation.Core/TransactionValidation.Core.csproj
dotnet add reference ../TransactionValidation.Integration/TransactionValidation.Integration.csproj
dotnet add reference ../TransactionValidation.Messaging/TransactionValidation.Messaging.csproj
cd ../TransactionValidation.Configuration
dotnet add reference ../TransactionValidation.Core/TransactionValidation.Core.csproj
cd ../TransactionValidation.Integration
dotnet add reference ../TransactionValidation.Core/TransactionValidation.Core.csproj
cd ../TransactionValidation.Messaging
dotnet add reference ../TransactionValidation.Core/TransactionValidation.Core.csproj
cd ../TransactionValidation.Mock
dotnet add reference ../TransactionValidation.Core/TransactionValidation.Core.csproj
cd ../../tests/TransactionValidation.Tests

dotnet add reference ../../src/TransactionValidation.Api/TransactionValidation.Api.csproj
dotnet add reference ../../src/TransactionValidation.Core/TransactionValidation.Core.csproj
dotnet add reference ../../src/TransactionValidation.Integration/TransactionValidation.Integration.csproj
cd ../../..
```

---

## 3. Add required NuGet packages

Use Central Package Management with `Directory.Packages.props`.
Define package versions once at the repository root, then reference packages in project files without inline `Version` attributes.

Add the following packages:

```bash
cd src/TransactionValidation.Api
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console

cd ../TransactionValidation.Configuration
dotnet add package FluentValidation.AspNetCore --version 11.0.0
dotnet add package OpenTelemetry.Extensions.Hosting --version 1.6.0
dotnet add package OpenTelemetry.Instrumentation.AspNetCore --version 1.6.0
dotnet add package OpenTelemetry.Instrumentation.Http --version 1.6.0
dotnet add package OpenTelemetry.Exporter.Console --version 1.6.0
dotnet add package Azure.Monitor.OpenTelemetry.Exporter --version 1.0.0

dotnet add package Polly --version 8.1.0

cd ../TransactionValidation.Integration
dotnet add package Polly --version 8.1.0

cd ../TransactionValidation.Messaging
dotnet add package RabbitMQ.Client --version 7.5.0

cd ../../tests/TransactionValidation.Tests

dotnet add package Microsoft.NET.Test.Sdk
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Moq
dotnet add package Castle.Core
dotnet add package FluentAssertions
```

Current centrally-managed versions in this repository:
- `Microsoft.AspNetCore.OpenApi` = `8.0.0`
- `Serilog.AspNetCore` = `9.0.0`
- `Serilog.Sinks.Console` = `6.0.0`
- `Microsoft.NET.Test.Sdk` = `17.11.1`
- `xunit` = `2.5.3`
- `xunit.runner.visualstudio` = `2.5.0`
- `Moq` = `4.20.72`
- `Castle.Core` = `5.1.1`
- `FluentAssertions` = `6.9.0`

The project standard is .NET 8 only.

## 3.1 Test structure and execution policy

The test project should be separated into `Unit/` and `Integration/` folders.

- Unit test files should mirror `src/` structure under `tests/TransactionValidation.Tests/Unit/`.
- Integration tests should be under `tests/TransactionValidation.Tests/Integration/`.
- Integration tests should use a category trait: `[Trait("Category", "Integration")]`.

Execution policy:
- Main CI (`ci.yml`) runs unit tests by default using filter: `Category!=Integration`.
- Integration CI (`integration.yml`) runs integration tests using filter: `Category=Integration`.

---

## 4. Core project implementation

### File: `src/TransactionValidation.Core/Models/PartnerTransactionRequest.cs`

```csharp
namespace TransactionValidation.Core.Models;

public sealed class PartnerTransactionRequest
{
    public required string PartnerId { get; init; }
    public required string TransactionReference { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTime Timestamp { get; init; }
}
```

### File: `src/TransactionValidation.Core/Models/TransactionEnvelope.cs`

```csharp
namespace TransactionValidation.Core.Models;

public sealed class TransactionEnvelope
{
    public required string MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public required PartnerTransactionRequest Transaction { get; init; }
    public bool PartnerVerified { get; init; }
}
```

### File: `src/TransactionValidation.Core/Models/ErrorResponse.cs`

```csharp
namespace TransactionValidation.Core.Models;

public sealed class ErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public List<FieldError>? Errors { get; init; }
}

public sealed class FieldError
{
    public required string Field { get; init; }
    public required string Message { get; init; }
}
```

### File: `src/TransactionValidation.Core/Interfaces/IPartnerVerifier.cs`

```csharp
namespace TransactionValidation.Core.Interfaces;

public interface IPartnerVerifier
{
    Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default);
}
```

### File: `src/TransactionValidation.Core/Interfaces/IMessagePublisher.cs`

```csharp
using TransactionValidation.Core.Models;

namespace TransactionValidation.Core.Interfaces;

public interface IMessagePublisher
{
    Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default);
}
```

### File: `src/TransactionValidation.Core/Validation/PartnerTransactionValidator.cs`

```csharp
using TransactionValidation.Core.Models;

namespace TransactionValidation.Core.Validation;

public static class PartnerTransactionValidator
{
    private static readonly HashSet<string> ValidCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "JPY", "CAD", "AUD"
    };

    public static List<FieldError> Validate(PartnerTransactionRequest request)
    {
        var errors = new List<FieldError>();

        if (string.IsNullOrWhiteSpace(request.PartnerId))
        {
            errors.Add(new FieldError { Field = nameof(request.PartnerId), Message = "partnerId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.TransactionReference))
        {
            errors.Add(new FieldError { Field = nameof(request.TransactionReference), Message = "transactionReference is required." });
        }

        if (request.Amount <= 0)
        {
            errors.Add(new FieldError { Field = nameof(request.Amount), Message = "amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || !ValidCurrencies.Contains(request.Currency))
        {
            errors.Add(new FieldError { Field = nameof(request.Currency), Message = "currency is required and must be a supported ISO code." });
        }

        if (request.Timestamp == default)
        {
            errors.Add(new FieldError { Field = nameof(request.Timestamp), Message = "timestamp is required and must be a valid UTC time." });
        }

        return errors;
    }
}
```

### File: `src/TransactionValidation.Core/Validation/PartnerTransactionRequestValidator.cs`

```csharp
using FluentValidation;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Core.Validation;

public sealed class PartnerTransactionRequestValidator : AbstractValidator<PartnerTransactionRequest>
{
    private static readonly string[] ValidCurrencies = { "USD", "EUR", "GBP", "JPY", "CAD", "AUD" };

    public PartnerTransactionRequestValidator()
    {
        RuleFor(x => x.PartnerId)
            .NotEmpty().WithMessage("partnerId is required.");

        RuleFor(x => x.TransactionReference)
            .NotEmpty().WithMessage("transactionReference is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("currency is required.")
            .Must(currency => ValidCurrencies.Contains(currency, StringComparer.OrdinalIgnoreCase))
            .WithMessage("currency must be a supported ISO code.");

        RuleFor(x => x.Timestamp)
            .NotEmpty().WithMessage("timestamp is required.");
    }
}
```

---

## 5. Configuration project implementation

The `TransactionValidation.Configuration` project centralizes startup wiring, configuration binding, and shared middleware so the API project remains light and reusable.

### File: `src/TransactionValidation.Configuration/Options/ApiKeyOptions.cs`

```csharp
namespace TransactionValidation.Configuration.Options;

public sealed class ApiKeyOptions
{
    public required string HeaderName { get; init; }
    public required string ApiKey { get; init; }
}
```

### File: `src/TransactionValidation.Configuration/Options/PartnerVerificationOptions.cs`

```csharp
namespace TransactionValidation.Configuration.Options;

public sealed class PartnerVerificationOptions
{
    public required string BaseUrl { get; init; }
    public int RetryCount { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 2;
    public int CircuitBreakerFailures { get; init; } = 5;
    public int CircuitBreakerDurationSeconds { get; init; } = 60;
}
```

### File: `src/TransactionValidation.Configuration/Options/RabbitMqOptions.cs`

```csharp
namespace TransactionValidation.Configuration.Options;

public sealed class RabbitMqOptions
{
    public required string HostName { get; init; }
    public int Port { get; init; } = 5672;
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public required string QueueName { get; init; }
}
```

### File: `src/TransactionValidation.Configuration/Extensions/ServiceCollectionExtensions.cs`

```csharp
using FluentValidation.AspNetCore;
using OpenTelemetry.Trace;
using TransactionValidation.Configuration.Middleware;
using TransactionValidation.Configuration.Options;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;
using TransactionValidation.Integration;
using TransactionValidation.Messaging;

namespace TransactionValidation.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionValidationCommonServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PartnerVerificationOptions>(configuration.GetSection("PartnerVerification"));
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.Configure<ApiKeyOptions>(configuration.GetSection("Security"));

        services.AddHttpClient<IPartnerVerifier, PartnerVerifierClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["PartnerVerification:BaseUrl"]!);
        });

        services.AddSingleton<IMessagePublisher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqMessagePublisher(options);
        });

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("TransactionValidation"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddConsoleExporter();
            });

        var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            services.AddOpenTelemetry().UseAzureMonitor();
        }

        services.AddValidatorsFromAssemblyContaining<PartnerTransactionRequestValidator>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddSingleton<ApiKeyOptions>(sp => sp.GetRequiredService<IOptions<ApiKeyOptions>>().Value);

        return services;
    }

    public static IApplicationBuilder UseTransactionValidationCommon(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<ApiKeyMiddleware>();
        return app;
    }
}
```

### File: `src/TransactionValidation.Configuration/Middleware/ApiKeyMiddleware.cs`

```csharp
using Microsoft.AspNetCore.Http;
using TransactionValidation.Configuration.Options;

namespace TransactionValidation.Configuration.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string DefaultHeader = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ApiKeyOptions options)
    {
        var headerName = string.IsNullOrWhiteSpace(options.HeaderName) ? DefaultHeader : options.HeaderName;

        if (!context.Request.Headers.TryGetValue(headerName, out var key) ||
            string.IsNullOrWhiteSpace(key) ||
            !string.Equals(key, options.ApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = "Unauthorized", message = "API key is missing or invalid." });
            return;
        }

        await _next(context);
    }
}
```

### File: `src/TransactionValidation.Configuration/Middleware/ApiExceptionHandler.cs`

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TransactionValidation.Core.Exceptions;

namespace TransactionValidation.Configuration.Middleware;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            BadRequestException badRequest => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = badRequest.Message,
                Type = "https://httpstatuses.com/400"
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = notFound.Message,
                Type = "https://httpstatuses.com/404"
            },
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = conflict.Message,
                Type = "https://httpstatuses.com/409"
            },
            UnauthorizedAccessException unauthorized => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = unauthorized.Message,
                Type = "https://httpstatuses.com/401"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred.",
                Type = "https://httpstatuses.com/500"
            }
        };

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken: cancellationToken);
        return true;
    }
}
```

    ### Exception handling best practices

    Design and implement domain-specific exception types that map cleanly to HTTP status codes (for example `NotFoundException` → 404, `BadRequestException` → 400, `ConflictException` → 409). This keeps controller code thin and preserves a single centralized mapping layer.

    In .NET 8, prefer the built-in `IExceptionHandler` contract over a custom middleware `try/catch` pipeline. The handler should inspect the exception type, choose the appropriate HTTP status code, and write a standardized RFC 7807 `ProblemDetails` response body.

    Example guidance:
    - Define simple, sealed domain exception types in `TransactionValidation.Core.Exceptions` (for example `NotFoundException`, `BadRequestException`, `ConflictException`).
    - Implement an `IExceptionHandler` in `TransactionValidation.Configuration/Middleware` that:
      - switches on the exception type and chooses the correct `StatusCode`
      - writes a `ProblemDetails` object with `type`, `title`, `status`, `detail`, and optional `instance`/extension values
      - preserves a clear single source of truth for API error mapping
    - Register the handler in the shared configuration wiring with `services.AddExceptionHandler<ApiExceptionHandler>();`
    - Call `app.UseExceptionHandler();` early in the ASP.NET Core pipeline before custom middleware such as `ApiKeyMiddleware`

    Notes:
    - Keep domain exceptions simple and avoid embedding transport concerns (HTTP codes) inside them; mapping belongs to the exception handler.
    - Use `ProblemDetails` to stay compatible with client libraries and tooling that understand RFC 7807.
    - Add unit tests that assert exception → status code mapping and validate the expected `ProblemDetails` fields.


### File: `src/TransactionValidation.Api/Program.cs`

```csharp
using TransactionValidation.Configuration.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddTransactionValidationCommonServices(builder.Configuration);

var app = builder.Build();

app.UseTransactionValidationCommon();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
```

---

## 6. Mock verification API implementation

### File: `src/TransactionValidation.Mock/Controllers/MockPartnerVerificationController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace TransactionValidation.Mock.Controllers;

[ApiController]
[Route("api/v1/mock/partner-verification")]
public sealed class MockPartnerVerificationController : ControllerBase
{
    private static readonly Random Random = new();

    [HttpGet("verify/{partnerId}")]
    public async Task<IActionResult> VerifyPartner(string partnerId)
    {
        // Simulate 30% timeout behavior.
        if (Random.NextDouble() < 0.30)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            throw new TimeoutException("Partner verification timed out.");
        }

        return Ok(new { partnerId, verified = true });
    }
}
```

> The mock endpoint is hosted in the `TransactionValidation.Mock` project and used by the `PartnerVerifierClient`.

---

## 6. Integration project implementation

### File: `src/TransactionValidation.Integration/PartnerVerificationOptions.cs`

```csharp
namespace TransactionValidation.Integration;

public sealed class PartnerVerificationOptions
{
    public required string BaseUrl { get; init; }
    public int RetryCount { get; init; } = 3;
    public int TimeoutSeconds { get; init; } = 2;
    public int CircuitBreakerFailures { get; init; } = 5;
    public int CircuitBreakerDurationSeconds { get; init; } = 60;
}
```

### File: `src/TransactionValidation.Integration/PartnerVerifierClient.cs`

```csharp
using System.Net.Http.Json;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using TransactionValidation.Core.Interfaces;

namespace TransactionValidation.Integration;

public sealed class PartnerVerifierClient : IPartnerVerifier
{
    private readonly HttpClient _httpClient;
    private readonly AsyncPolicyWrap _policy;

    public PartnerVerifierClient(HttpClient httpClient, PartnerVerificationOptions options)
    {
        _httpClient = httpClient;
        var timeout = Policy.TimeoutAsync(TimeSpan.FromSeconds(options.TimeoutSeconds));

        var retry = Policy.Handle<Exception>()
            .WaitAndRetryAsync(options.RetryCount, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    // Add logging if needed
                });

        var breaker = Policy.Handle<Exception>()
            .CircuitBreakerAsync(options.CircuitBreakerFailures, TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds));

        _policy = Policy.WrapAsync(retry, timeout, breaker);
    }

    public async Task<bool> VerifyAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return await _policy.ExecuteAsync(async ct =>
        {
            var response = await _httpClient.GetAsync($"api/v1/mock/partner-verification/verify/{partnerId}", ct);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<PartnerVerificationResponse>(cancellationToken: ct);
            return payload?.Verified ?? false;
        }, cancellationToken);
    }

    private sealed class PartnerVerificationResponse
    {
        public required string PartnerId { get; init; }
        public bool Verified { get; init; }
    }
}
```

### Notes
- The HTTP client base address is configured in the API project.
- The policy wraps retry, timeout, and circuit breaker behavior.

---

## 7. Messaging project implementation

### File: `src/TransactionValidation.Messaging/RabbitMqOptions.cs`

```csharp
namespace TransactionValidation.Messaging;

public sealed class RabbitMqOptions
{
    public required string HostName { get; init; }
    public int Port { get; init; } = 5672;
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public required string QueueName { get; init; }
}
```

### File: `src/TransactionValidation.Messaging/RabbitMqMessagePublisher.cs`

```csharp
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Messaging;

public sealed class RabbitMqMessagePublisher : IMessagePublisher
{
    private readonly RabbitMqOptions _options;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqMessagePublisher(RabbitMqOptions options)
    {
        _options = options;
        var factory = new ConnectionFactory()
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            DispatchConsumersAsync = false
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.ConfirmSelect();
    }

    public Task PublishAsync(TransactionEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.CorrelationId = envelope.CorrelationId;

        _channel.BasicPublish(exchange: string.Empty,
            routingKey: _options.QueueName,
            basicProperties: properties,
            body: body);

        if (!_channel.WaitForConfirms(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException("RabbitMQ did not confirm the published message.");
        }

        return Task.CompletedTask;
    }
}
```

> This publisher uses RabbitMQ publisher confirms and durable queue declaration.

---

## 8. API project implementation

### File: `src/TransactionValidation.Api/Program.cs`

```csharp
using Serilog;
using TransactionValidation.Configuration.Extensions;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services
    .AddControllers()
    .AddTransactionValidationCommonServices(builder.Configuration);

var app = builder.Build();

app.UseTransactionValidationCommon();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
```

> Note: this sample now uses a shared configuration project for DI registration, Serilog, and middleware wiring.

### File: `src/TransactionValidation.Api/Controllers/PartnerTransactionsController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using TransactionValidation.Core.Interfaces;
using TransactionValidation.Core.Models;
using TransactionValidation.Core.Validation;

namespace TransactionValidation.Api.Controllers;

[ApiController]
[Route("api/v1/partner/transactions")]
public sealed class PartnerTransactionsController : ControllerBase
{
    private readonly IPartnerVerifier _partnerVerifier;
    private readonly IMessagePublisher _messagePublisher;

    public PartnerTransactionsController(IPartnerVerifier partnerVerifier, IMessagePublisher messagePublisher)
    {
        _partnerVerifier = partnerVerifier;
        _messagePublisher = messagePublisher;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] PartnerTransactionRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var verified = await _partnerVerifier.VerifyAsync(request.PartnerId, cancellationToken);
        if (!verified)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse
            {
                Code = "PartnerVerificationFailed",
                Message = "Partner verification failed or timed out."
            });
        }

        var envelope = new TransactionEnvelope
        {
            MessageId = Guid.NewGuid().ToString("D"),
            CorrelationId = Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId)
                ? correlationId.ToString()
                : Guid.NewGuid().ToString("D"),
            ReceivedAt = DateTimeOffset.UtcNow,
            Transaction = request,
            PartnerVerified = true
        };

        await _messagePublisher.PublishAsync(envelope, cancellationToken);

        return Accepted(new { envelope.MessageId, envelope.CorrelationId });
    }
}
```

### File: `src/TransactionValidation.Api/Middleware/ApiKeyMiddleware.cs`

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace TransactionValidation.Api.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var key) || string.IsNullOrWhiteSpace(key))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = "Unauthorized", message = "API key is missing." });
            return;
        }

        var configuredKey = configuration["Security:ApiKey"];
        if (!string.Equals(key, configuredKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = "Unauthorized", message = "API key is invalid." });
            return;
        }

        await _next(context);
    }
}
```

### File: `src/TransactionValidation.Api/Middleware/GlobalExceptionHandlerMiddleware.cs`

```csharp
using System.Net;
using System.Text.Json;
using TransactionValidation.Core.Models;

namespace TransactionValidation.Api.Middleware;

public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (TimeoutException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Code = "TimeoutError",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Code = "InternalServerError",
                Message = "An unexpected error occurred."
            });
        }
    }
}
```

---

## 9. Configuration examples

### File: `src/TransactionValidation.Api/appsettings.json`

```json
{
  "PartnerVerification": {
    "BaseUrl": "https://localhost:5002/",
    "RetryCount": 3,
    "TimeoutSeconds": 2,
    "CircuitBreakerFailures": 5,
    "CircuitBreakerDurationSeconds": 60
  },
  "RabbitMq": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "QueueName": "partner-transactions"
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" }
    ]
  },
  "OpenTelemetry": {
    "Resource": {
      "Service": {
        "Name": "TransactionValidation.Api",
        "Version": "1.0.0"
      }
    }
  },
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://centralus-0.in.applicationinsights.azure.com/"
  },
  "Security": {
    "ApiKey": "secret-api-key"
  }
}
```
### File: `src/TransactionValidation.Api/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## 10. Tests project outline

### File: `tests/TransactionValidation.Tests/ValidationTests.cs`

```csharp
using FluentAssertions;
using TransactionValidation.Core.Models;
using TransactionValidation.Core.Validation;

namespace TransactionValidation.Tests;

public class ValidationTests
{
    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsNoErrors()
    {
        var request = new PartnerTransactionRequest
        {
            PartnerId = "P-1001",
            TransactionReference = "TXN-99823",
            Amount = 250.00m,
            Currency = "USD",
            Timestamp = DateTime.Parse("2024-05-10T14:30:00Z")
        };

        var errors = PartnerTransactionValidator.Validate(request);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenAmountIsZero_ReturnsError()
    {
        var request = new PartnerTransactionRequest
        {
            PartnerId = "P-1001",
            TransactionReference = "TXN-99823",
            Amount = 0m,
            Currency = "USD",
            Timestamp = DateTime.Parse("2024-05-10T14:30:00Z")
        };

        var errors = PartnerTransactionValidator.Validate(request);

        errors.Should().ContainSingle(e => e.Field == nameof(request.Amount));
    }
}
```

### File: `tests/TransactionValidation.Tests/PartnerVerifierTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;
using TransactionValidation.Integration;
using TransactionValidation.Core.Interfaces;

namespace TransactionValidation.Tests;

public class PartnerVerifierTests
{
    [Fact]
    public async Task VerifyAsync_WhenServiceReturnsOk_ReturnsTrue()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { partnerId = "P-1001", verified = true })
            });

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://localhost/") };
        var options = new PartnerVerificationOptions { BaseUrl = "https://localhost/", RetryCount = 0, TimeoutSeconds = 2, CircuitBreakerFailures = 5, CircuitBreakerDurationSeconds = 60 };
        var verifier = new PartnerVerifierClient(client, options);

        var result = await verifier.VerifyAsync("P-1001");

        result.Should().BeTrue();
    }
}
```

> Further tests can cover timeout handling, retry behavior, and publisher confirm failures.

---

## 11. Docker support (bonus)

### File: `docker-compose.yml`

```yaml
version: '3.9'
services:
  api:
    build:
      context: ./src/TransactionValidation.Api
      dockerfile: Dockerfile
    ports:
      - '5000:80'
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
            - RABBITMQ__HOSTNAME=rabbitmq
            - RABBITMQ__USERNAME=guest
            - RABBITMQ__PASSWORD=guest
            - RABBITMQ__QUEUENAME=partner-transactions
            - PARTNERVERIFICATION__BASEURL=http://mock:80/
            - SECURITY__APIKEY=secret-api-key
    depends_on:
      - rabbitmq
      - mock

  mock:
    build:
      context: ./src/TransactionValidation.Mock
      dockerfile: Dockerfile
    ports:
      - '5002:80'

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - '5672:5672'
      - '15672:15672'
```

### File: `src/TransactionValidation.Api/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TransactionValidation.Api/TransactionValidation.Api.csproj", "TransactionValidation.Api/"]
COPY ["TransactionValidation.Core/TransactionValidation.Core.csproj", "TransactionValidation.Core/"]
COPY ["TransactionValidation.Integration/TransactionValidation.Integration.csproj", "TransactionValidation.Integration/"]
COPY ["TransactionValidation.Messaging/TransactionValidation.Messaging.csproj", "TransactionValidation.Messaging/"]
RUN dotnet restore "TransactionValidation.Api/TransactionValidation.Api.csproj"
COPY . .
WORKDIR "/src/TransactionValidation.Api"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TransactionValidation.Api.dll"]
```

### File: `src/TransactionValidation.Mock/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TransactionValidation.Mock/TransactionValidation.Mock.csproj", "TransactionValidation.Mock/"]
COPY ["TransactionValidation.Core/TransactionValidation.Core.csproj", "TransactionValidation.Core/"]
RUN dotnet restore "TransactionValidation.Mock/TransactionValidation.Mock.csproj"
COPY . .
WORKDIR "/src/TransactionValidation.Mock"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TransactionValidation.Mock.dll"]
```

---

## 12. How to run

From the repository root:

```bash
cd /Users/tech/dev/net/TransactionValidation
dotnet build
cd src/TransactionValidation.Api
dotnet run
```

Or for Docker:

```bash
docker compose up --build
```

Then call `POST http://localhost:5000/api/v1/partner/transactions` with `X-API-Key: secret-api-key`.

---

## 13. Notes and alignment with the sequence diagram

This scaffold implements the end-to-end workflow from the sequence diagram:
- Request validation
- Partner verification via mock endpoint
- Retry and timeout handling
- Message publish to RabbitMQ
- API key-based request security
- 202 Accepted response after publish confirmation

If you want, I can also generate the actual file contents inside the `src/` tree instead of only documenting the scaffold.
