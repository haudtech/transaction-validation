# Port command runbook for this repository

This document covers the operational commands used in this repo to check ports, run services, and stop listeners.

## Port map used by this repo

- API default debug port: `5080`
- Mock default debug port: `5081`
- Common legacy/fallback ports to check during cleanup: `5000`, `5001`

## 1) Check which process is listening on repo ports

```bash
lsof -nP -iTCP -sTCP:LISTEN | grep -E ':(5080|5081|5000|5001)\b' || echo "none"
```

Check one specific port:

```bash
lsof -nP -iTCP:5080 -sTCP:LISTEN
lsof -nP -iTCP:5081 -sTCP:LISTEN
```

Get only PID(s):

```bash
lsof -nP -tiTCP:5080 -sTCP:LISTEN
lsof -nP -tiTCP:5081 -sTCP:LISTEN
```

## 2) Run services manually with fixed ports

From repo root.

Run API:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 \
dotnet run --project src/TransactionValidation.Api/TransactionValidation.Api.csproj
```

Run Mock:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5081 \
dotnet run --project src/TransactionValidation.Mock/TransactionValidation.Mock.csproj
```

Run both in two terminals.

## 3) Quick health checks after startup

```bash
curl -sS -o /dev/null -w "api:%{http_code}\n" http://localhost:5080/
curl -sS -o /dev/null -w "mock:%{http_code}\n" http://localhost:5081/
```

Expected results:
- API root often returns `401` when API key auth is active.
- Mock root should return `200`.

## 4) Stop services by Ctrl+C (preferred)

If you started each service in its own terminal, stop each process with `Ctrl+C`.

## 5) Force-stop all repo ports

Stop the common repo ports in one command:

```bash
ports=(5080 5081 5000 5001)
for p in $ports; do
  pids=$(lsof -nP -tiTCP:$p -sTCP:LISTEN 2>/dev/null)
  if [[ -n "$pids" ]]; then
    echo "Stopping port $p (PIDs: $pids)"
    kill -9 $pids
  fi
done

lsof -nP -iTCP -sTCP:LISTEN | grep -E ':(5080|5081|5000|5001)\b' || echo "none"
```

## 6) Recover from "address already in use"

If startup fails with `Failed to bind to address ... address already in use`:

1. Run the port check command.
2. Kill the process on the conflicting port.
3. Re-run the service with explicit `ASPNETCORE_URLS`.

Example for Mock on `5081`:

```bash
kill -9 $(lsof -nP -tiTCP:5081 -sTCP:LISTEN)
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5081 \
dotnet run --project src/TransactionValidation.Mock/TransactionValidation.Mock.csproj
```

## 7) Validate solution after restart

```bash
dotnet build --nologo -m:1 TransactionValidation.sln
dotnet test --nologo -m:1
```

## Notes

- `kill -9` is forceful; prefer `Ctrl+C` first when possible.
- If another local app reclaims `5000`, keep this repo pinned to `5080/5081`.
- If running via VS Code compound launch, stop the compound session before manual runs.
