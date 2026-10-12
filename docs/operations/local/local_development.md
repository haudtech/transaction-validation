# Local Development, Debugging, and Runtime Validation

This guide explains how to launch the local services, validate their runtime behavior, inspect logs, and clean up Docker resources.

## Scope

The local stack contains:

- TransactionValidation API
- TransactionValidation Mock
- RabbitMQ
- Redis
- Optional Docker Compose container network

The API and mock use the following local endpoints:

| Service | Native process URL | Docker Compose URL |
| --- | --- | --- |
| API | http://localhost:5080 | http://localhost:5001 when `API_HOST_PORT=5001` |
| Mock | http://localhost:5081 | http://localhost:5002 |
| RabbitMQ AMQP | 127.0.0.1:5672 | 127.0.0.1:5672 |
| RabbitMQ management | http://localhost:15672 | http://localhost:15672 |
| Redis | 127.0.0.1:6379 | 127.0.0.1:6379 when published by the host |

> The Docker Compose API defaults to port `5000`, but the repository's browser workflow uses `API_HOST_PORT=5001`. The native VS Code launch profiles use ports `5080` and `5081`.

## 1. Launch all required services

### Option A: Docker Compose

Use this option for the closest reproduction of the complete local topology.

#### Prerequisites

- Docker Desktop or Docker Engine with Compose
- The repository root as the working directory
- A local `.env` file when non-default credentials or ports are required

The Compose file builds the API and mock images, starts RabbitMQ and Redis, and starts the mock before the API.

#### Start

```zsh
docker compose up -d --build
```

To use the browser workflow port:

```zsh
API_HOST_PORT=5001 docker compose up -d --build
```

The expected services are:

```text
transactionvalidation-api-1
transactionvalidation-mock-1
transactionvalidation-rabbitmq-1
transactionvalidation-redis-1
```

Check which services are running:

```zsh
docker compose ps
```

#### Compose environment

The Compose configuration reads variables from the repository `.env` file and applies these defaults:

```text
API_HOST_PORT=5000
ASPNETCORE_ENVIRONMENT=Development
SECURITY__APIKEY=local-dev-api-key
REDIS__CONNECTIONSTRING=redis:6379,abortConnect=false
MESSAGING__BROKERTYPE=RabbitMq
RABBITMQ__HOSTNAME=rabbitmq
RABBITMQ__PORT=5672
RABBITMQ__USERNAME=guest
RABBITMQ__PASSWORD=guest
PARTNERVERIFICATION__BASEURL=http://mock:8080/
```

For the browser flow, set the API port explicitly:

```dotenv
API_HOST_PORT=5001
```

The Compose API service uses the internal `redis` hostname and `rabbitmq` hostname. Those names are valid only inside the Compose network.

#### Stop and remove

Stop and remove containers while preserving Redis data:

```zsh
docker compose down
```

Remove containers and persisted Redis data:

```zsh
docker compose down -v
```

#### Rebuild after dependency changes

```zsh
docker compose build --no-cache api mock
docker compose up -d
```

### Option B: Native launch with VS Code

Use this option for native debugging, breakpoint inspection, and the fixed local development ports.

#### Prerequisites

Start RabbitMQ and Redis separately. The API and mock are launched by the VS Code debugger.

Start RabbitMQ with Docker Compose:

```zsh
docker compose up -d rabbitmq
```

Start Redis with Docker Compose. The Compose Redis service is not published to the host by default, so run a temporary host-accessible Redis container when using native processes:

```zsh
docker run -d --name transactionvalidation-host-redis \
  -p 6379:6379 \
  redis:7-alpine redis-server --appendonly yes
```

This `docker run` command starts a **separate Redis container**; it does not expose or reuse the Compose `redis` service. The Compose Redis service is reachable by Compose containers through `redis:6379`, while native API/mock processes run outside that Docker network and need a host-published port such as `127.0.0.1:6379`. The temporary container provides that host endpoint. It has its own Redis data and, because this command does not mount a volume, removing the container removes that data. If you prefer to use the Compose Redis instance, publish its port in the Compose configuration and point the native process to `127.0.0.1:6379` instead; do not run both configurations with the same host port published at once.

If the API uses the in-memory idempotency store, Redis is optional. For the complete transaction lifecycle, Redis must be reachable.

#### Launch profiles

In VS Code:

1. Open the Command Palette with `Cmd+Shift+P`.
2. Select **Debug: Start Debugging**.
3. Choose one of the compound profiles:
   - **Debug Full Flow (API + Mock)**
   - **Debug Full Flow + Browser**

The profiles launch:

- `TransactionValidation.Api` on `http://localhost:5080`
- `TransactionValidation.Mock` on `http://localhost:5081`
- Swagger on `http://localhost:5080/swagger`
- The mock root on `http://localhost:5081`

The API launch profile sets:

```text
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:5080
RabbitMq__HostName=127.0.0.1
RabbitMq__Port=5672
PartnerVerification__BaseUrl=http://localhost:5081/
```

The mock launch profile sets:

```text
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:5081
RabbitMqConsumer__HostName=127.0.0.1
RabbitMqConsumer__Port=5672
RabbitMqAuditConsumer__HostName=127.0.0.1
RabbitMqAuditConsumer__Port=5672
```

The API and mock are built before launch by the `build` pre-launch task.

#### Native CLI equivalent

Run the API from the repository root:

```zsh
ASPNETCORE_ENVIRONMENT=Development \
dotnet run --no-build --project src/TransactionValidation.Api/TransactionValidation.Api.csproj \
  --no-launch-profile \
  --urls http://localhost:5080 \
  --Redis:ConnectionString=127.0.0.1:6379,abortConnect=false \
  --RabbitMq:HostName=127.0.0.1 \
  --RabbitMq:Port=5672 \
  --PartnerVerification:BaseUrl=http://localhost:5081/ \
  --Security:ApiKey=local-dev-api-key \
  --Security:HeaderName=X-API-Key
```

Run the mock from the repository root:

```zsh
ASPNETCORE_ENVIRONMENT=Development \
dotnet run --no-build --project src/TransactionValidation.Mock/TransactionValidation.Mock.csproj \
  --no-launch-profile \
  --urls http://localhost:5081 \
  --RabbitMqConsumer:HostName=127.0.0.1 \
  --RabbitMqConsumer:Port=5672 \
  --RabbitMqAuditConsumer:HostName=127.0.0.1 \
  --RabbitMqAuditConsumer:Port=5672 \
  --Logging:LogLevel:Default=Warning
```

The command-line values override configuration sources. They are useful for reproducing the launch profile without relying on shell environment variables.

#### Stop native processes

Use the process list to identify the API and mock workers:

```zsh
ps -axo pid,ppid,command | grep TransactionValidation.Api
grep -v grep
ps -axo pid,ppid,command | grep TransactionValidation.Mock
grep -v grep
```

Stop the matching processes:

```zsh
kill <api-worker-pid> <mock-worker-pid>
```

If using VS Code, stop the compound profile instead so the debugger can terminate all configured processes.

## 2. Validate each required service

### API

#### Root endpoint

```zsh
curl -i http://localhost:5080/
```

Expected result:

```text
HTTP/1.1 200 OK
TransactionValidation API
```

The root endpoint is in-process and does not require the API key.

#### Health endpoint

```zsh
curl -i http://localhost:5080/healthz
```

Expected result: `200 OK` when Redis and the messaging publisher are healthy.

The health response contains checks for:

- messaging publisher registration
- Redis connectivity

A `503` response indicates one or more dependencies are unhealthy. Inspect the JSON body and API logs for the failing check.

#### API authentication

```zsh
curl -i http://localhost:5080/ \
  -H 'X-API-Key: local-dev-api-key'
```

Expected result: `200 OK`.

The API key is configured by the `Security` section. Do not commit production secrets. The local development value is `local-dev-api-key`.

#### Swagger

```zsh
open http://localhost:5080/swagger
```

Swagger is enabled only when `ASPNETCORE_ENVIRONMENT=Development`, but the API-key middleware also protects `/swagger` and `/swagger/v1/swagger.json`. Opening the URL directly in a browser sends no `X-API-Key` header, so it returns HTTP `401` with `{"error":"Unauthorized"}`. The current OpenAPI document does not declare an API-key security scheme, so Swagger UI cannot show an **Authorize** control or attach the key itself. To retrieve the raw OpenAPI document, send the configured key explicitly:

```zsh
curl -i http://localhost:5080/swagger/v1/swagger.json \
  -H "X-API-Key: $API_KEY"
```

The header name can be changed through `Security:HeaderName`; locally it defaults to `X-API-Key`. The same header is required for protected API operations. `/healthz` remains exempt from API-key enforcement. Making the interactive Swagger UI work in a browser requires an application change to permit the Swagger assets and configure an OpenAPI API-key security scheme; this guide documents the current behavior only.

### Mock

#### Root endpoint

```zsh
curl -i http://localhost:5081/
```

Expected result:

```text
HTTP/1.1 200 OK
TransactionValidation Mock
```

If `curl` returns error 7 (`Couldn't connect to server`), no process is listening at that address. Start the native Mock through **Debug Full Flow (API + Mock)** (native port `5081`), or start the Compose stack and use `http://localhost:5002` for the Mock. Check the listener and service state with:

```zsh
lsof -nP -iTCP:5081 -sTCP:LISTEN
docker compose ps mock
docker compose logs --tail=100 mock
```

An earlier successful request to `localhost:5081` only proves that the native Mock was listening at that time; a later connection refusal usually means that process has stopped. This is a Mock host availability issue, not the RabbitMQ `list_consumers` count behavior described below.

#### Health endpoint

```zsh
curl -i http://localhost:5081/healthz
```

Expected result: `200 OK`.

The mock health endpoint is a general ASP.NET Core health endpoint. It verifies the host and endpoint pipeline, but it does not verify RabbitMQ consumer startup.

#### Partner verification

```zsh
curl -i http://localhost:5081/api/v1/mock/partner-verification/verify/partner-browser
```

Expected result: `200 OK` with:

```json
{
  "partnerId": "partner-browser",
  "verified": true
}
```

The mock may return `408` when its simulated timeout path is selected. The call can be forced to timeout with:

```zsh
curl -i 'http://localhost:5081/api/v1/mock/partner-verification/verify/partner-browser?forceTimeout=true'
```

#### Consumer observations

```zsh
curl -i http://localhost:5081/api/v1/mock/consumer-observations/primary
```

Expected result: `200 OK` and an empty array before the first accepted transaction. Messages sent to the primary consumer are recorded here.

### RabbitMQ

#### Docker Compose

```zsh
docker compose ps rabbitmq
```

Check the management UI:

```text
http://localhost:15672
```

The default credentials are `guest` / `guest` unless overridden in `.env`.

#### AMQP connectivity

```zsh
docker compose exec -T rabbitmq rabbitmq-diagnostics -q ping
```

Expected result: `Ping succeeded`.

Verify queue and exchange declarations in the management UI:

- Exchange: `partner.transactions`
- Type: `topic`
- Queue: `partner-transactions`
- Routing pattern: `partner.transaction.#`

#### Interpret RabbitMQ connection and consumer counts

The Mock's RabbitMQ consumers poll their queues with RabbitMQ's `BasicGetAsync`; they do not register push consumers with `BasicConsume`. Therefore, `rabbitmqctl list_consumers` (and the management UI's consumer count) can report **zero consumers even while the Mock is connected and polling**. A recent local snapshot showed `connection: 3`, `channel: 3`, `exchange: 9`, `queue: 3`, and `consumer: 0`. This is consistent with the polling design: the primary, audit, and unrouted queues exist, while no push consumers are registered. Connection and channel totals can change as applications and diagnostic commands connect or exit; these counts are observations, not fixed expected values. The HTTP 200 response from the Mock root only confirms that its web host is running; it does not confirm broker connectivity.

Inspect queue declaration and message state separately:

```zsh
docker compose exec -T rabbitmq rabbitmqctl list_queues name consumers messages_ready messages_unacknowledged
```

The primary and audit queues should be `partner-transactions` and `partner-transactions.audit`. With the polling consumers, their `consumers` column may remain `0`; `messages_ready` should decrease after a matching message is published, and `messages_unacknowledged` is normally brief because messages are acknowledged after observation. Check that the Mock recorded delivery:

```zsh
curl -i http://localhost:5081/api/v1/mock/consumer-observations/primary
curl -i http://localhost:5081/api/v1/mock/consumer-observations/audit
```

If queues are missing, observations stay empty, or ready messages do not drain, verify the Mock's `RabbitMqConsumer:Enabled` and `RabbitMqAuditConsumer:Enabled` settings, broker host/credentials, and Mock logs. Native Mock processes must connect to `127.0.0.1:5672`; a Mock running in Compose must use `rabbitmq:5672`.

#### Native host connectivity

```zsh
nc -vz 127.0.0.1 5672
```

Expected result: the connection succeeds.

### Redis

#### Docker Compose

```zsh
docker compose ps redis
docker compose exec -T redis redis-cli ping
```

Expected result: `PONG`.

#### Temporary host Redis container

```zsh
docker exec transactionvalidation-host-redis redis-cli ping
```

Expected result: `PONG`.

### API transaction lifecycle

Use a unique idempotency key and the configured API key:

```zsh
run_id="browser-$(date +%s)"
base="http://localhost:5080/api/v1/partner/transactions"
payload=$(cat <<EOF
{
  "partnerId": "partner-browser",
  "transactionReference": "ref-$run_id",
  "amount": 120.50,
  "currency": "USD",
  "timestamp": "2026-10-08T05:00:00Z"
}
EOF
)

curl -i -X POST "$base" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'X-API-Key: local-dev-api-key' \
  -H "Idempotency-Key: $run_id" \
  --data "$payload"
```

Expected result: `202 Accepted` with a `messageId` and `correlationId`.

Repeat the exact request to verify idempotency handling:

```zsh
curl -i -X POST "$base" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/problem+json' \
  -H 'X-API-Key: local-dev-api-key' \
  -H "Idempotency-Key: $run_id" \
  --data "$payload"
```

Expected result: `202 Accepted` when the API uses the stored response, or `409 Conflict` when the request fingerprint differs. The current implementation returns the stored accepted response for an identical request.

Submit the same idempotency key with a changed payload:

```zsh
changed_payload=$(cat <<EOF
{
  "partnerId": "partner-browser",
  "transactionReference": "ref-$run_id",
  "amount": 99.99,
  "currency": "USD",
  "timestamp": "2026-10-08T05:00:01Z"
}
EOF
)

curl -i -X POST "$base" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/problem+json' \
  -H 'X-API-Key: local-dev-api-key' \
  -H "Idempotency-Key: $run_id" \
  --data "$changed_payload"
```

Expected result: `409 Conflict` with a payload-mismatch detail.

Check the mock verification endpoint:

```zsh
curl -i 'http://localhost:5081/api/v1/mock/partner-verification/verify/partner-browser'
```

Expected result: `200 OK` and `verified: true`.

Check the mock consumer observations:

```zsh
curl -i http://localhost:5081/api/v1/mock/consumer-observations/primary
```

The observation list is empty until the primary consumer records the accepted message. If it remains empty, inspect the RabbitMQ queue and the mock consumer logs.

## 3. Inspect logs and process state

### Native API and mock

The native launch profiles write process output to the VS Code integrated terminal and the Debug Console. A standalone CLI run can capture output:

```zsh
ASPNETCORE_ENVIRONMENT=Development \
dotnet run --no-build --project src/TransactionValidation.Api/TransactionValidation.Api.csproj \
  --no-launch-profile \
  --urls http://localhost:5080 \
  --Redis:ConnectionString=127.0.0.1:6379,abortConnect=false \
  --RabbitMq:HostName=127.0.0.1 \
  --RabbitMq:Port=5672 \
  --PartnerVerification:BaseUrl=http://localhost:5081/ \
  --Security:ApiKey=local-dev-api-key \
  --Security:HeaderName=X-API-Key \
  > /tmp/transactionvalidation-api.log 2>&1
```

```zsh
ASPNETCORE_ENVIRONMENT=Development \
dotnet run --no-build --project src/TransactionValidation.Mock/TransactionValidation.Mock.csproj \
  --no-launch-profile \
  --urls http://localhost:5081 \
  --RabbitMqConsumer:HostName=127.0.0.1 \
  --RabbitMqConsumer:Port=5672 \
  --RabbitMqAuditConsumer:HostName=127.0.0.1 \
  --RabbitMqAuditConsumer:Port=5672 \
  --Logging:LogLevel:Default=Warning \
  > /tmp/transactionvalidation-mock.log 2>&1
```

Inspect the latest entries:

```zsh
tail -n 100 /tmp/transactionvalidation-api.log
tail -n 100 /tmp/transactionvalidation-mock.log
```

Search for request failures, exceptions, and correlation identifiers:

```zsh
grep -E 'ERR|Exception|StatusCode: Error|Transaction request received|correlation_id' \
  /tmp/transactionvalidation-api.log
```

### Docker Compose logs

```zsh
docker compose logs --tail=100 api
docker compose logs --tail=100 mock
docker compose logs --tail=100 rabbitmq
docker compose logs --tail=100 redis
```

Follow logs while services are running:

```zsh
docker compose logs -f api mock rabbitmq redis
```

### API application log

The development API writes structured logs to:

```text
src/TransactionValidation.Api/logs/app-*.txt
```

Inspect the current log:

```zsh
ls -lt src/TransactionValidation.Api/logs/app-*.txt | head

tail -n 200 "$(ls -t src/TransactionValidation.Api/logs/app-*.txt | head -n 1)"
```

Use the request `correlationId` from the API response to find the corresponding trace and log entries.

## 4. Common failure patterns

| Symptom | Likely cause | Check |
| --- | --- | --- |
| `401 Unauthorized` | Wrong API key or header | Verify `X-API-Key` and the `Security:ApiKey` value |
| `503` from `/healthz` | Redis or messaging publisher unavailable | Check Redis `PING`, broker health, and API logs |
| `500` on transaction | Redis connection or downstream exception | Check the exception stack and `correlationId` |
| `curl: (7) Couldn't connect to localhost:5081` | Native Mock is stopped or not bound to port `5081` | Start the native Mock; for Compose, use `http://localhost:5002`; inspect `lsof`, Compose status, and Mock logs |
| API starts but mock does not | RabbitMQ consumer configuration | Check `RabbitMqConsumer` and `RabbitMqAuditConsumer` settings |
| API service cannot resolve `redis` | Native process uses Compose hostname | Pass `127.0.0.1:6379` or run Redis on the host |
| API or mock cannot connect to RabbitMQ | Wrong broker host or credentials | Check `RabbitMq__HostName`, `RabbitMq__Port`, and `.env` values |
| Browser opens a stale endpoint | Native process is not running or port changed | Verify listeners with `lsof -nP -iTCP:5080` and `lsof -nP -iTCP:5081` |

## 5. Cleanup

### Free Docker RAM and disk space

RAM and disk space are reclaimed differently. First inspect usage:

```zsh
docker stats --no-stream
docker system df -v
```

`docker stats` reports running-container RAM/CPU use; `docker system df -v` reports Docker disk usage by images, containers, volumes, and build cache.

To stop this project's containers and release their active container RAM while preserving named volumes:

```zsh
docker compose down
```

If Docker Desktop continues to show memory reserved after the containers stop, quit and restart Docker Desktop; its Linux VM can retain allocated memory until the VM is restarted. This stops other Docker workloads too, so do it only when they are not needed.

To reclaim disk space, start with the non-volume cleanup. Docker will show what it plans to remove and ask for confirmation:

```zsh
docker system prune
```

This removes stopped containers, unused networks, dangling images, and build cache; it does not remove volumes. To also remove unused images (including images not currently used by a container), use:

```zsh
docker system prune -a
```

Use the following only when you intentionally want to delete **all unused volumes and their persisted data** as well:

```zsh
docker system prune -a --volumes
```

Likewise, `docker compose down -v` removes this Compose project's volumes, including its persisted Redis data. Do not use either volume-removal option if you need to keep database contents. Recheck with `docker system df -v` afterward.

Stop the native debugger profile with the VS Code stop button or the compound profile's stop action.

Stop Docker Compose services:

```zsh
docker compose down
```

Remove the temporary host Redis container:

```zsh
docker rm -f transactionvalidation-host-redis
```

If the process was started with the standalone CLI commands, stop the API and mock workers before removing the temporary dependency containers.
