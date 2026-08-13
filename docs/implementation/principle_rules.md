# Implementation Principle Rules — Scaffolding by AI

This document lists MUST/SHOULD principles and short case studies to guide AI-generated scaffolding and human review. The rules focus on consistent, reviewable, and safe code generation — the AI scaffolds code only; humans verify and commit.

**Scaffolding-only principle**: The AI MUST only generate suggested files and patches. It MUST NOT apply, commit, or push changes. Human operators MUST validate and commit.

---

## 1. Import / Using statement grouping (first rule)

MUST:
- Group `using` directives into three groups, in this order:
  1. SDK / Framework (System.*)
  2. Third‑party libraries (NuGet packages)
  3. Cross‑project / solution references (internal namespaces)
- Separate each group with a single blank line.
- Sort directives alphabetically within each group.

SHOULD:
- Prefer `using` placement consistently across the repo (either `inside_namespace` or `outside_namespace`) and document the choice in `EditorConfig`.
- Avoid unnecessary global usings for small projects; if global usings are used, place them in a clearly named file (e.g., `GlobalUsings.cs`) and document the rationale.

Example (before):

```csharp
using TransactionValidation.Core;
using System;
using FluentValidation;
```

Example (after — correct ordering):

```csharp
using System;
using System.Collections.Generic;

using FluentValidation;
using Polly;

using TransactionValidation.Core;
using TransactionValidation.Configuration;
```

Case study / rationale:
- Grouping makes review faster: reviewers check SDK usage, third‑party surface area, and then internal coupling separately.
- Sorting within groups reduces churn in diffs when adding/removing single usings.

EditorConfig snippet (recommended):

```ini
# enforce sorting and System first
[*.cs]
dotnet_sort_system_directives_first = true
csharp_using_directive_placement = inside_namespace
```

Also use `dotnet format` or Roslyn analyzers in CI to enforce ordering.

---

## 2. File structure and ordering

MUST:
- File layout must follow: file header (optional), `using` groups, namespace, single type per file unless small related types (e.g., DTO+mapper).
- Keep `public` types near top of file and helper/private types below.

SHOULD:
- Keep lines under ~120 characters where practical. Use well‑named helper methods rather than long inline code.
- Prefer immutable DTOs where appropriate.

---

## 3. Dependency injection & service registration

MUST:
- Expose clear `ServiceCollectionExtensions.AddTransactionValidationCommonServices(IServiceCollection, IConfiguration)` for DI wiring.
- Keep DI registrations idempotent and scoped appropriately (`AddSingleton`, `AddScoped`, `AddTransient`).

SHOULD:
- Register interfaces before concrete types in the same extension method for discoverability.
- Register health checks and configuration options (`IOptions<T>`) with `Configure<T>`.

---

## 4. Configuration and secrets

MUST:
- Keep the solution on .NET 8 only and pin the SDK with `global.json`.
- Follow configuration precedence exactly: `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.Docker.json` → environment variables → command‑line args.
- Never scaffold hard-coded secrets; use `.env.example` placeholders.

SHOULD:
- Provide `IConfiguration` section binding helper methods (e.g., `BindPartnerVerificationOptions`) and validate required values at startup.

---

## 4.1 Build and dependency management

MUST:
- Use `Directory.Build.props` for shared MSBuild properties across projects.
- Use `Directory.Packages.props` for Central Package Management.
- Keep package versions centralized; project files SHOULD use versionless `PackageReference` items.

SHOULD:
- Keep project files minimal by inheriting common settings (`TargetFramework`, `Nullable`, `ImplicitUsings`) from shared props files.

---

## 5. Error handling and exceptions

MUST:
- Implement domain exceptions in `Core` (`NotFoundException`, `BadRequestException`, `ConflictException`).
- Centralize translation to `ProblemDetails` via an `IExceptionHandler` registered in `Configuration`.

SHOULD:
- Keep exception types small and descriptive; avoid leaking implementation details in messages.

---

## 6. Logging and observability

MUST:
- Always inject `ILogger<T>` rather than using static loggers.
- Scaffold Serilog and OpenTelemetry wiring in the common startup extension.

SHOULD:
- Include structured logging keys for important contextual data (e.g., `transactionId`, `partnerId`).

---

## 7. Testing

MUST:
- Every generated public API or behavior must include at least one unit test in `tests/TransactionValidation.Tests` for the phase‑scoped changes.
- Use xUnit + FluentAssertions; use Moq for collaborators.
- Include `Microsoft.NET.Test.Sdk` explicitly in the test project.

- Test layout parity: unit test files MUST mirror the `src/` project folder and namespace structure inside `tests/TransactionValidation.Tests`.
  - For example, the class defined at `src/TransactionValidation.Core/Models/Foo.cs` should have its tests at `tests/TransactionValidation.Tests/TransactionValidation.Core/Models/FooTests.cs`.
  - Test namespaces should mirror the source namespace with a `.Tests` suffix (e.g., `TransactionValidation.Core.Models.Tests`).
  - Test class names SHOULD follow `{TypeName}Tests` and test method names SHOULD follow the `MethodName_StateUnderTest_ExpectedBehavior` pattern.
- Integration tests MUST be placed under `tests/TransactionValidation.Tests/Integration` and tagged with `[Trait("Category", "Integration")]`.

SHOULD:
- Keep tests small, deterministic, and fast. Use `Theory` where helpful.

---

## 8. Packaging and Docker

MUST:
- If a Dockerfile is scaffolded, it MUST be small and suited for .NET production images (use official SDK/build image + runtime image multi‑stage build).

SHOULD:
- Add `docker-compose.override.yml` for local dev that maps ports and mounts code for fast iteration.

---

## 9. CI and enforcement

MUST:
- Add checks in CI (or recommend) for `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes`.
- Split test execution policy:
  - `ci.yml` runs unit tests by default using `Category!=Integration`.
  - `integration.yml` runs integration tests using `Category=Integration`.

SHOULD:
- Suggest adding Roslyn analyzer package(s) to enforce API design rules where relevant.

---

## 10. Commit and review workflow for AI scaffolding

MUST:
- AI-generated scaffolding MUST be treated as draft code until manually validated.
- A human maintainer MUST review and commit after verification.

SHOULD:
- Use small, phase‑scoped PRs with descriptive titles: `feat(phase-2): implement partner verifier client interface`.
- Include a checklist in PR description referencing the phase checklist entries implemented.