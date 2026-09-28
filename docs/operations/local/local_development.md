# Local Development Guide

## Purpose and Scope

This guide covers the repository's current local development path for building, running, and validating TransactionValidation on a developer machine. It consolidates the practical runtime details from the Compose stack, environment template, port usage, and local health checks.

This guide is the authoritative local operating document. It replaces older prerequisite and port-runbook content that described historical or implementation-phase setup steps.

## Prerequisites

Before running the solution locally, install:

- .NET 8 SDK
- Docker Desktop or Docker Engine with Compose support
- A terminal with access to the repository root
- Optional: VS Code with the C# extension for debugging

## Repository defaults

The repository expects the application to run with the following local defaults:

| Component | Default local value |
|---|---|
| API HTTP port | `5000` |
| Mock HTTP port | `5002` |
| RabbitMQ AMQP | `5672` |
| RabbitMQ management | `15672` |
| Redis | `6379` |
| Broker default | RabbitMQ |

The active Compose stack is defined in `docker-compose.yml`, and the configuration template is in `.env.example`.

## Startup

From the repository root, copy the example file to a local override if needed:

```bash
cp .env.example .env
```

Then start the stack:

```bash
docker compose up --build -d
```

This starts the API, Mock service, Redis, and RabbitMQ as configured by the repository. The API uses `REDIS__CONNECTIONSTRING` and the RabbitMQ settings from the environment file; the Mock service uses a parallel set of broker consumer values and its own HTTP endpoint.

## Runtime checks

After the stack is running, verify the health endpoints and listeners:

```bash
curl -sS http://localhost:5000/healthz
curl -sS http://localhost:5002/healthz
```

The API root and Mock root are also available through their container/host mappings. RabbitMQ management is available at:

```text
http://localhost:15672
```

Use the configured RabbitMQ credentials from `.env.example` or the current local `.env` file when logging in.

## Local host execution

The repo also supports running the API and Mock directly on the host when needed for debugging or local testing. The implementation and tests assume the same environment patterns, but the container stack remains the default local runtime model.

Example for the API:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 \
  dotnet run --project src/TransactionValidation.Api/TransactionValidation.Api.csproj
```

Example for the Mock service:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5081 \
  dotnet run --project src/TransactionValidation.Mock/TransactionValidation.Mock.csproj
```

The repository's local runtime guidance and the Compose environment template are the authoritative source for port and connection constants. Do not assume a single fixed port across all developer machines without checking the active environment file or Compose settings.

## Graceful shutdown

Stop the local stack with:

```bash
docker compose down
```

If local data should also be removed, use:

```bash
docker compose down -v
```

This removes volumes such as the Redis data volume. Use it only when you are intentionally resetting the local runtime state.

## Common maintenance

When a local service refuses to start because a port is in use, prefer identifying the running listener and stopping it gracefully before retrying. Only use force termination as a narrow recovery step when the service cannot be stopped normally.

Useful checks:

```bash
lsof -nP -iTCP -sTCP:LISTEN | grep -E ':(5000|5002|5672|6379|15672)\b' || echo 'no repo-local listeners'
```

## Related documentation

- [../features/local-platform/README.md](../features/local-platform/README.md)
- [../features/runtime-and-api/README.md](../features/runtime-and-api/README.md)
- [../features/messaging/README.md](../features/messaging/README.md)
- [../features/testing-and-quality/README.md](../features/testing-and-quality/README.md)
- [../azure_deployment/README.md](../azure_deployment/README.md)
