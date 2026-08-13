# Automated Phase Implementation Prompt

Use this prompt to request end-to-end, runnable code for each ordered phase in `docs/implementation/implementation_phases.md`.

## Instructions for the generator

Goal: Generate complete, runnable implementation for TransactionValidation BFF Phase {PHASE_NUMBER} as defined in `docs/implementation/implementation_phases.md`. Output must be concrete files that compile and include tests where required.

Context:
- Repository root: `/Users/tech/dev/net/TransactionValidation`
- Language: C#, Target framework: `net8.0`
- Project layout (must follow):
  - `src/TransactionValidation.Api`
  - `src/TransactionValidation.Configuration`
  - `src/TransactionValidation.Core`
  - `src/TransactionValidation.Integration`
  - `src/TransactionValidation.Messaging`
  - `src/TransactionValidation.Mock`
  - `tests/TransactionValidation.Tests`
- Libraries to use: Serilog, OpenTelemetry (AspNetCore + HttpClient), FluentValidation, Polly, RabbitMQ.Client, xUnit, Moq, FluentAssertions.
- Configuration loading precedence (follow exactly):
  1. `appsettings.json`
  2. `appsettings.{Environment}.json`
  3. `appsettings.Docker.json`
  4. environment variables via `AddEnvironmentVariables()` (prefer UPPER_SNAKE_CASE with `__` for sections)
  5. command-line args
- Error handling: implement simple domain exceptions in Core (`NotFoundException`, `BadRequestException`, `ConflictException`) and centralize mapping to RFC 7807 `ProblemDetails` via an `IExceptionHandler` implementation registered in `TransactionValidation.Configuration`.
- DI/Wiring: implement `ServiceCollectionExtensions.AddTransactionValidationCommonServices(IConfiguration)` and `UseTransactionValidationCommon()` per scaffold; keep `TransactionValidation.Api` minimal.
- Testing: include xUnit tests for code introduced in the phase; tests must build and pass.
- Docker: where relevant, add `Dockerfile`(s) and update `docker-compose.yml` under the repo root.
- No secrets: use `.env.example` placeholders for credentials.

Task (phase-specific):
1. Read the Phase {PHASE_NUMBER} checklist in `docs/implementation/implementation_phases.md` and implement each checklist item for that phase.
2. Create or update files under the appropriate `src/` or `tests/` paths.
3. Ensure `dotnet build` succeeds and `dotnet test` passes for tests created by this phase.
4. Return output as:
   - A single patch (unified diff) that adds/updates files.
   - A short execution plan of commands to run locally to validate (build, test, docker compose).
   - Recommended git commands to apply the changes: branch creation, apply patch, commit, and push.

Constraints:
- Do not change unrelated files beyond the phase's scope.
- Keep code style consistent with the scaffold and minimize comments.
- Use `http://localhost:5002/` as the default mock base URL and make it configurable via `PartnerVerification:BaseUrl`.
- When updating configuration examples, prefer UPPER_SNAKE_CASE env variable names in `.env.example`.

Example invocation (Phase 1):
- Replace `{PHASE_NUMBER}` with `1` and use branch `feature/phase-1-solution-and-project-setup`.
- Expected outputs: `TransactionValidation.sln`, project `.csproj` files under `src/`, test project under `tests/`, and a working `dotnet build`.

## Quick local validation commands

```bash
# from repo root
dotnet build
dotnet test
# if docker files were added/updated:
docker compose up --build -d
```

## Recommended git workflow

```bash
git checkout -b feature/phase-{PHASE_NUMBER}-{short-description}
# apply patch (if provided as a patch file)
# git apply phase-{PHASE_NUMBER}.patch
git add .
git commit -m "feat(phase-{PHASE_NUMBER}): {short-description}"
git push -u origin HEAD
```

---

Place additional generator constraints or project-specific preferences below this file as needed.

## Explicit generator prompt (copy-paste)

Use the exact prompt below when calling an LLM/codegen tool to implement a phase. Replace `{PHASE_NUMBER}` and `{short-description}` where indicated.

```
Goal: Generate complete, runnable implementation for TransactionValidation BFF Phase {PHASE_NUMBER} as defined in docs/implementation/implementation_phases.md. Output must be concrete files that compile and include tests where required.

Context:
- Repository root: /Users/tech/dev/net/TransactionValidation
- Language: C#, Target framework: net8.0
- Project layout (must follow):
  - src/TransactionValidation.Api
  - src/TransactionValidation.Configuration
  - src/TransactionValidation.Core
  - src/TransactionValidation.Integration
  - src/TransactionValidation.Messaging
  - src/TransactionValidation.Mock
  - tests/TransactionValidation.Tests
- Libraries to use: Serilog, OpenTelemetry (AspNetCore + HttpClient), FluentValidation, Polly, RabbitMQ.Client, xUnit, Moq, FluentAssertions.
- Configuration loading precedence (follow exactly):
  1. appsettings.json
  2. appsettings.{Environment}.json
  3. appsettings.Docker.json
  4. environment variables via AddEnvironmentVariables() (prefer UPPER_SNAKE_CASE with __ for sections)
  5. command-line args
- Error handling: implement simple domain exceptions in Core (NotFoundException, BadRequestException, ConflictException) and centralize mapping to RFC 7807 ProblemDetails via an IExceptionHandler implementation registered in TransactionValidation.Configuration.
- DI/Wiring: implement ServiceCollectionExtensions.AddTransactionValidationCommonServices(IConfiguration) and UseTransactionValidationCommon() per scaffold; keep TransactionValidation.Api minimal.
- Testing: include xUnit tests for code introduced in the phase; tests must build and pass.
- Docker: where relevant, add Dockerfile(s) and update docker-compose.yml under the repo root.
- No secrets: use .env.example placeholders for credentials.

Task (phase-specific):
1. Read the Phase {PHASE_NUMBER} checklist in docs/implementation/implementation_phases.md and implement each checklist item for that phase.
2. Create or update files under the appropriate src/ or tests/ paths.
3. Ensure dotnet build succeeds and dotnet test passes for tests created by this phase.
4. Return output as:
   - A single patch (unified diff) that adds/updates files.
   - A short execution plan of commands to run locally to validate (build, test, docker compose).
   - Recommended git commands to apply the changes: branch creation, apply patch, commit, and push.

Constraints:
- Do not change unrelated files beyond the phase's scope.
- Keep code style consistent with the scaffold and minimize comments.
- Use http://localhost:5002/ as the default mock base URL and make it configurable via PartnerVerification:BaseUrl.
- When updating configuration examples, prefer UPPER_SNAKE_CASE env variable names in .env.example.

Example invocation (Phase 1):
- Replace {PHASE_NUMBER} with 1 and use branch feature/phase-1-solution-and-project-setup.
- Expected outputs: TransactionValidation.sln, project .csproj files under src/, test project under tests/, and a working dotnet build.

End.
```