# Shared Engineering Principles

This document defines the common engineering standards that apply across repositories and teams. It is intentionally technology-agnostic and reusable, and it is separate from the repository-specific architecture rules.

The repo-specific architecture decisions live in [repo_architecture_rules.md](repo_architecture_rules.md). This file keeps the broader standard practices that are useful outside a single project.

---

## 1. Import / Using statement grouping

MUST:
- Group `using` directives into three groups, in this order:
  1. SDK / Framework (System.*)
  2. Third-party libraries (NuGet packages)
  3. Cross-project / solution references (internal namespaces)
- Separate each group with a single blank line.
- Sort directives alphabetically within each group.

SHOULD:
- Prefer `using` placement consistently across the repo and document the choice in `EditorConfig`.
- Avoid unnecessary global usings for small projects.

Example:

```csharp
using System;
using System.Collections.Generic;

using FluentValidation;
using Microsoft.Extensions.Http.Resilience;

using TransactionValidation.Core;
using TransactionValidation.Configuration;
```

---

## 2. File structure and ordering

MUST:
- File layout must follow: file header (optional), `using` groups, namespace, single type per file unless small related types are intentionally grouped.
- Keep `public` types near the top of the file and helper/private types below.

SHOULD:
- Keep lines under roughly 120 characters where practical.
- Prefer clear helper methods instead of long inline logic.
- Prefer immutable DTOs where appropriate.

---

## 2.1 Code documentation strictness for new members

MUST:
- Every newly added class must include a concise XML `<summary>` that explains its responsibility.
- Every newly added method (public, internal, private, and test methods) must include a concise XML `<summary>` that explains behavior intent.
- Helper/nested types and helper methods must also include `<summary>` documentation when introduced.

SHOULD:
- Keep summaries purpose-focused and short.
- Describe expected behavior and boundary/intent, not line-by-line implementation detail.
- For test methods, describe the scenario and expected outcome in the summary.

---

## 3. Dependency injection and service registration

MUST:
- Keep DI registrations explicit and appropriate for lifetimes.
- Keep registrations idempotent and easy to review.
- Separate shared infrastructure registrations from broker-specific or implementation-specific registrations.
- Use a single runtime selection point when one of several transport implementations can be active.

SHOULD:
- Register interfaces before concrete types in the same extension method.
- Register health checks and configuration options with `Configure<T>`.
- Keep the common service extension focused on truly shared concerns and avoid leaking message-broker implementation details into it.

Example of the preferred pattern:

```csharp
builder.Services.AddTransactionValidationCommonServices(builder.Configuration);

builder.Services.AddConfiguredBroker(
    builder.Configuration,
    AddRabbitMqMessagingServices,
    AddAzureServiceBusMessagingServices);

static void AddRabbitMqMessagingServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
    services.AddSingleton<IRabbitMqClientAdapter, RabbitMqClientAdapter>();
    services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
}

static void AddAzureServiceBusMessagingServices(IServiceCollection services, IConfiguration configuration)
{
    // Future Azure Service Bus wiring stays isolated here.
}
```

Preferred decomposition for the same idea:

```csharp
builder.Services.AddConfiguredBroker(
    builder.Configuration,
    RegisterRabbitMqServices,
    RegisterAzureServiceBusServices);

static void RegisterRabbitMqServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
    services.AddHostedService<RabbitMqNoOpConsumerService>();
}

static void RegisterAzureServiceBusServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<ServiceBusPrimaryConsumerOptions>(
        configuration.GetSection(ServiceBusPrimaryConsumerOptions.SectionName));
    services.AddHostedService<ServiceBusPrimaryConsumerService>();
}
```

This rule applies to every new broker migration: keep the broker switch in one place, keep each broker-specific registration in its own method, and avoid passing implementation details through the shared application startup.

Why this is required:
- the broker selection switch lives in one place
- only one transport is active at runtime
- the shared infrastructure project does not depend on broker-specific app types
- future broker migrations can happen without rewriting the surrounding application startup

---

## 4. Configuration and secrets

MUST:
- Follow a clear configuration precedence model.
- Never hard-code secrets.
- Use environment variables or secure configuration sources for credentials and endpoints.

SHOULD:
- Validate required configuration values at startup.
- Keep configuration sections small and typed.

---

## 5. Build and dependency management

MUST:
- Keep package versions centralized when using central package management.
- Prefer shared MSBuild properties for common settings.

SHOULD:
- Keep project files minimal and inherit common conventions from shared props files.

---

## 6. Error handling and exceptions

MUST:
- Use explicit exception types for domain or request errors.
- Centralize translation from infrastructure exceptions to stable response models.

SHOULD:
- Keep exception messages descriptive but not overly implementation-specific.

---

## 7. Logging and observability

MUST:
- Inject `ILogger<T>` instead of using static loggers.
- Capture important request context and correlation data.

SHOULD:
- Use structured logging fields such as correlation ID, request ID, trace ID, and domain identifiers.

---

## 8. Testing

MUST:
- Add tests for public behavior and contract-level logic.
- Keep tests deterministic and focused.
- Use xUnit and FluentAssertions for the standard project test style.

SHOULD:
- Keep unit tests fast and independent from external systems.
- Separate integration tests from unit tests via clear project or trait structure.
- Use `Theory` where helpful for data-driven validation.

---

## 9. CI and enforcement

MUST:
- Run build and test checks in CI.
- Prefer format and analyzer checks as a gate for code quality.

SHOULD:
- Enforce targeted validation for unit and integration flow separately where helpful.

---

## 10. AI-generated scaffolding and review workflow

MUST:
- Treat AI-generated scaffolding as draft work until human review and validation.
- Keep the generated changes small and reviewable.
- Let a human validate, review, and commit.

SHOULD:
- Use phased PRs and tie each PR to a specific implementation milestone or checklist item.
- Include a short summary of what was generated and what was manually verified.

---

## 11. Review checklist for shared engineering work

Before merging any engineering standard or code change, confirm:

- [ ] The change is clearly scoped and reviewable
- [ ] It matches the relevant repo or shared standard, not both by accident
- [ ] The design is simple and explicit
- [ ] Security-sensitive values are not hard-coded
- [ ] Logging and error handling are consistent
- [ ] Relevant tests exist or are updated
- [ ] Documentation reflects the actual implementation

---

## Final principle

Shared engineering standards should guide quality, safety, and maintainability across projects. Repository architecture rules should define the actual decision-making for this solution. Separating them makes both more reliable.
