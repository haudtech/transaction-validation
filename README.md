# TransactionValidation — Partner Integration BFF

A lightweight Backend-For-Frontend (BFF) to mediate partner integrations for transaction verification and routing.

Technology stack

| Area | Stack |
|---|---|
| Runtime / Framework | .NET 8 (`net8.0`) |
| API | ASP.NET Core Web API |
| Validation | FluentValidation |
| Resilience | Polly |
| Messaging | RabbitMQ |
| Observability | Serilog, OpenTelemetry |
| Architecture | Multi-project solution (`Api`, `Configuration`, `Core`, `Integration`, `Messaging`, `Mock`, `Tests`) |
| Quality gates | Split CI workflows for unit and integration tests with explicit category filters |

## Architecture Overview (Sequence)

Primary architecture flow (mirrors [docs/diagram/use_case_sequence_diagram.md](docs/diagram/use_case_sequence_diagram.md)):

```mermaid
sequenceDiagram
	autonumber
	participant P as Partner/Client
	participant API as TransactionValidation API
	participant Auth as API Key Middleware
	participant Cache as Dedupe Cache
	participant Validator as Request Validator
	participant Verifier as PartnerVerifier
	participant Mock as MockPartnerVerification
	participant Publisher as MessagePublisher
	participant MQ as RabbitMQ

	P->>API: POST /api/v1/partner/transactions
	API->>Auth: Validate X-API-Key
	Auth-->>API: Authorized
	API->>Cache: Check `partnerId|transactionReference`
	alt Duplicate request in TTL window
		Cache-->>API: Already accepted
		API-->>P: 202 Accepted (cached)
	else New request
		API->>Validator: Validate payload
		Validator-->>API: OK / ValidationError
		alt Valid payload
			API->>Verifier: Verify(partnerId)
			Verifier->>Mock: Call mock verification endpoint
			Mock-->>Verifier: Verified / TimeoutException
			alt Verified
				Verifier-->>API: Verified
				API->>Publisher: Publish internal envelope
				Publisher->>MQ: Enqueue persistent message + wait confirms
				MQ-->>Publisher: Ack
				Publisher-->>API: Published
				API->>Cache: Store dedupe key with TTL
				API-->>P: 202 Accepted
			else Verification failed
				Verifier-->>API: Partner not verified
				API-->>P: 503 Service Unavailable / 400
			end
		else Invalid payload
			Validator-->>API: Validation errors
			API-->>P: 400 Bad Request
		end
	end
```

Quick local run
```bash
dotnet restore
dotnet build
dotnet test -v normal
```

Repository entrypoint for implementation, workflow, and architecture documentation.

## Documentation

Start here: [docs/README.md](docs/README.md)

The detailed documentation map, contribution workflow, and topic navigation live in the docs index so the project README stays focused on the codebase and the primary entrypoint.

## License

- TBD
