# TransactionValidation — Partner Integration BFF

A lightweight Backend-For-Frontend (BFF) to mediate partner integrations for transaction verification and routing.

Key points
- Target: .NET 8 (net8.0)
- Purpose: Validate partner requests, enrich/transform payloads, and publish validated messages to RabbitMQ for downstream processing.
- Observability: Serilog + OpenTelemetry (instrumentation ready); optional Application Insights exporter.
- Validation: FluentValidation for request model validation.
- Resilience: Polly for HttpClient resilience strategies.

Current status
- Documentation scaffold, phased implementation plan, Docker examples, and environment-variable conventions are present under `docs/`.
- Implementation code (solution and projects under `src/`) is not yet generated — this README will be updated once the implementation is created.

Getting started (docs)
- Prerequisites and environment setup: docs/implementation/Prerequisites/README.md
- Implementation phases and checklist: docs/implementation/implementation_phases.md
- Scaffold and wiring guidance: docs/implementation/implementation_scaffold.md

Quick local run (after implementation)
1. Ensure .NET 8 SDK is installed:

```bash
dotnet --version
```

2. Start local infra (example):

```bash
docker compose up -d
```

3. Build and run API (example):

```bash
dotnet build
dotnet run --project src/TransactionValidation.Api
```

Update guidance
- Replace this file with detailed build/run instructions, API endpoints, project list, and test commands after the code generation (Phase 1) and initial implementation (Phase 2) are complete.

Contributing
- Follow the phased checklist in docs/implementation/implementation_phases.md.
- Open issues for per-phase tasks if you want them tracked individually.

License
- TBD
