# Transaction Validation Solution Prerequisites

This document describes the prerequisites for building, running, and testing the full Transaction Validation solution.

## 1. .NET SDK
- Install the .NET 8.0 SDK.
- The solution and all projects are targeted at `net8.0`.
- Verify with:
  ```bash
dotnet --version
```

## 2. dotnet CLI
- Ensure the `dotnet` command is available in your PATH.
- Used to create projects, restore packages, build the solution, run the API, and execute tests.

## 3. IDE / Editor
- Visual Studio 2022+ or Visual Studio Code.
- If using VS Code, install the C# extension.

## 4. RabbitMQ
- A local RabbitMQ broker is required for `TransactionValidation.Messaging`.
- You can run RabbitMQ locally or via Docker Compose.

## 5. NuGet Package Dependencies
The implementation uses these NuGet packages across projects:
- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `FluentValidation.AspNetCore`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Exporter.Console`
- `Azure.Monitor.OpenTelemetry.Exporter`
- `Polly`
- `RabbitMQ.Client`
- `Moq`
- `FluentAssertions`

## 6. Optional Infrastructure
- Docker and Docker Compose for local development and infrastructure orchestration.
- A valid Azure Application Insights connection string only if the optional Azure Monitor exporter is enabled.

## 7. Environment and Configuration
- A working development shell with access to the repository root.
- Application settings are expected to be configured in `appsettings.json` and `appsettings.Development.json`.
- `ApplicationInsights:ConnectionString` is optional and only required for Azure monitoring.

## 8. Recommended CLI Commands
```bash
cd /Users/tech/dev/net/TransactionValidation
dotnet restore
dotnet build
```

## 9. Notes
- The workspace currently uses a multi-project `.sln` layout with shared configuration, core, integration, messaging, and mock projects.
- Follow the ordered implementation phases in `docs/implementation/implementation_phases.md` when generating code.
