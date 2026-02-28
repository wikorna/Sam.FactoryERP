# Sam.FactoryERP

> **Enterprise Factory ERP — Modular Monolith on .NET 10**
>
> Internal platform for managing factory operations across Sales, Production, Purchasing,
> Inventory, Costing, Quality, Labeling, EDI, and Administration.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture Overview](#2-architecture-overview)
3. [Technology Stack](#3-technology-stack)
4. [Caching Strategy](#4-caching-strategy)
5. [Messaging Strategy](#5-messaging-strategy)
6. [Database Strategy](#6-database-strategy)
7. [Security](#7-security)
8. [Running the Project — Development](#8-running-the-project--development)
9. [Enabling Redis (Optional)](#9-enabling-redis-optional)
10. [Production Deployment Notes](#10-production-deployment-notes)
11. [Project Structure](#11-project-structure)
12. [Contributing Guidelines](#12-contributing-guidelines)

---

## 1. Project Overview

Sam.FactoryERP is an internal enterprise resource planning platform built for a manufacturing
environment. The solution is structured as a **Modular Monolith** — all business modules
compile into a single deployable API host while enforcing strict module boundaries at the
code level, preventing unintended cross-module dependencies at compile time.

The system exposes a REST API (`FactoryERP.ApiHost`) for all client interactions and runs a
background message processor (`FactoryERP.WorkerHost`) that handles asynchronous operations
such as ZPL label printing and event consumption.

### Architectural goals

| Goal | Mechanism |
|---|---|
| Module isolation | Each module has its own Domain / Application / Infrastructure / Api layers; cross-module calls go through integration events only |
| Fail-open resilience | Cache, messaging, and infrastructure failures degrade gracefully — business logic is never blocked |
| Zero-downtime configuration | Redis and RabbitMQ are enabled/disabled via configuration with no code change required |
| Strict code quality | `TreatWarningsAsErrors=true`; all `CS*`, `CA*`, and `SA*` analyzer rules enforced at build time |
| Testability | Clean Architecture layers, `ICommand`/`IQuery` abstractions, and architecture tests enforce dependency rules |

### Key technologies

.NET 10 · ASP.NET Core 10 · EF Core 10 · PostgreSQL 17/18 · MassTransit 8 · RabbitMQ 4 ·
MediatR 14 · HybridCache · StackExchange.Redis · Serilog · FluentValidation · Docker Compose

---

## 2. Architecture Overview

### Modular Monolith

The solution is a single deployable unit composed of independent modules. Each module is a
self-contained vertical slice with its own domain model, application logic, infrastructure,
and API surface. Modules communicate only through **integration events** published and consumed
via MassTransit — they never reference each other's internal types directly.

Architecture tests (`FactoryERP.ArchTests`) enforce that domain assemblies do not take
dependencies on other modules at compile time.

### Clean Architecture layers (per module)

```
┌──────────────────────────────────────────────────────┐
│  Module (e.g. Labeling)                              │
│                                                      │
│  ┌─────────┐  ┌─────────────┐  ┌────────────────┐   │
│  │ Domain  │◄─│ Application │◄─│ Infrastructure │   │
│  │ Entities│  │ Commands    │  │ EF Core        │   │
│  │ Events  │  │ Queries     │  │ Consumers      │   │
│  │ Base    │  │ Handlers    │  │ Services       │   │
│  └─────────┘  └──────┬──────┘  └────────────────┘   │
│                      │                               │
│               ┌──────▼──────┐                        │
│               │    API      │  (Controllers/Minimal) │
│               └─────────────┘                        │
└──────────────────────────────────────────────────────┘
```

**Dependency rule:** Domain ← Application ← Infrastructure ← API. No layer may reference
an outer layer. Hosts only reference Infrastructure and API.

### Host topology

```
                     ┌────────────────────────────────────────────┐
                     │          Single Docker Host                │
                     │                                            │
  HTTP/HTTPS ──────► │  ┌──────────────────┐                     │
                     │  │  FactoryERP.     │  publish events     │
                     │  │  ApiHost         ├─────────────────┐   │
                     │  │  (ASP.NET Core)  │                 │   │
                     │  └──────────────────┘                 │   │
                     │                                        ▼   │
                     │  ┌──────────────────┐  ┌──────────────────┐│
                     │  │  FactoryERP.     │  │   RabbitMQ       ││
                     │  │  WorkerHost      │◄─│   (AMQP broker)  ││
                     │  │  (Worker SDK)    │  └──────────────────┘│
                     │  └──────────────────┘                     │
                     │           │                                │
                     │    ┌──────▼──────────────────────────┐    │
                     │    │  PostgreSQL (single DB, schemas) │    │
                     │    └──────────────────────────────────┘    │
                     │           │                                │
                     │    ┌──────▼──────┐   (optional L2)        │
                     │    │   Redis     │◄── disabled by default  │
                     │    └─────────────┘                        │
                     └────────────────────────────────────────────┘
```

### Building Blocks

| Project | Role |
|---|---|
| `FactoryERP.SharedKernel` | `BaseEntity`, `AuditableEntity`, `IDomainEvent` — shared primitives only |
| `FactoryERP.Abstractions` | Interfaces: `ICacheService`, `IEventBus`, `ICommand<T>`, `IQuery<T>`, MediatR pipeline behaviors |
| `FactoryERP.Contracts` | Integration event message contracts shared across modules |
| `FactoryERP.Infrastructure` | All cross-cutting infrastructure: HybridCache, MassTransit wiring, Redis, health checks |
| `FactoryERP.ReadModel` | Reserved for future read-model / projection infrastructure |

---

## 3. Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET / ASP.NET Core | 10.0.102 (SDK pin via `global.json`) |
| Worker host | Microsoft.NET.Sdk.Worker | 10.0 |
| ORM | Entity Framework Core | 10.0.3 |
| Database | PostgreSQL | 17 (Docker), 18 (production Alpine) |
| CQRS | MediatR | 14.0 |
| Validation | FluentValidation | 12.1.1 |
| Cache L1 | `Microsoft.Extensions.Caching.Hybrid` (`HybridCache`) | 10.3.0 |
| Cache L2 | StackExchange.Redis (optional) | 2.8.41 |
| Messaging | MassTransit | 8.5.8 |
| Message broker | RabbitMQ | 4 (management image) |
| Logging | Serilog | 4.3.0 + ASP.NET Core sink 10.0.0 |
| Authentication | ASP.NET Core Identity + JWT Bearer (RS256) | 10.0.3 |
| API documentation | Swashbuckle + Microsoft.OpenApi | 10.x / 2.3.0 |
| Health checks | `AspNetCore.HealthChecks.*` | 9.0.0 |
| Code quality | .NET analyzers (`CA*`), `TreatWarningsAsErrors=true` | `AnalysisLevel=latest` |
| Architecture tests | NetArchTest.Rules | 1.3.2 |
| Container runtime | Docker Compose | — |

---

## 4. Caching Strategy

### Overview

All caching uses `Microsoft.Extensions.Caching.Hybrid` (`HybridCache`), which provides a
**two-layer cache**:

- **L1** — process-local in-memory (`IMemoryCache`). Always active. Zero network overhead.
- **L2** — distributed (`IDistributedCache`). Backed by Redis when enabled. Shared across
  multiple container instances.

The `ICacheService` abstraction (defined in `FactoryERP.Abstractions`) shields application
and domain layers from any infrastructure concern. The implementation (`HybridCacheService`)
is entirely internal to `FactoryERP.Infrastructure`.

### Fail-open design

Every `ICacheService` method wraps the cache call in a `try/catch`. On any infrastructure
failure the factory delegate is called directly, returning a live result. Business logic
is **never** blocked by cache unavailability.

### Cache key conventions

Cache keys are built using the `CacheKey.Create(…)` helper:

```csharp
// Produces "printjob:status:abc123" (lowercased, colon-separated)
var key = CacheKey.Create("printjob", "status", jobId.ToString());
```

### Tag-based invalidation

Entries may carry one or more tags. All entries sharing a tag can be invalidated atomically:

```csharp
// Write with a tag
await _cache.SetAsync(key, value, new CacheEntrySettings { Tags = ["printjob"] });

// Invalidate all "printjob" entries across L1 and L2
await _cache.RemoveByTagAsync("printjob");
```

### Default TTL

| Setting | Default |
|---|---|
| L2 (distributed) expiration | 60 minutes |
| L1 (in-memory) expiration | 5 minutes |
| Maximum payload | 1 MB per entry |
| Maximum key length | 1024 characters |

Override per entry by passing `CacheEntrySettings` with explicit `Expiration` /
`LocalCacheExpiration` values.

### Redis — disabled by default

Redis is intentionally **disabled by default**. The application runs fully in L1-only mode
without Redis being present. Enabling Redis requires only a configuration change (see
[Section 9](#9-enabling-redis-optional)).

Startup logs indicate the active mode:

```
[INF] Cache mode: HybridCache L1-only (in-memory). Set Cache:Redis:Enabled=true...
# or, when Redis is connected:
[INF] Cache mode: HybridCache L1+L2 (Redis). Connected in 23ms — instance=FactoryERP:
```

---

## 5. Messaging Strategy

### Transport

MassTransit is the messaging abstraction. The transport is configurable:

| `RabbitMQ:Enabled` | Transport used |
|---|---|
| `true` | RabbitMQ (AMQP) |
| `false` | In-memory (development / unit test only) |

### Endpoint naming

Queue names are prefixed with the `RabbitMQ:EnvironmentPrefix` value to isolate environments
sharing the same broker:

```
RabbitMQ:EnvironmentPrefix = "dev"
→ Queue: dev-QrPrintRequested
→ Queue: dev-PrintZplCommand
```

### Outbox / Inbox pattern (EF Core)

MassTransit's EF Core integration provides at-least-once delivery guarantees:

- **Outbox** — messages written inside a database transaction are held in `OutboxState` /
  `OutboxMessage` tables and forwarded to the broker by a background delivery service.
  This guarantees that a message is never lost even if the broker is temporarily unavailable.
- **Inbox** — received messages are recorded in `InboxState` before processing, providing
  idempotent consumer behaviour. Duplicate deliveries are silently discarded.

Both tables live in the same PostgreSQL schema as the owning module (`labeling.*`).

### Host responsibilities

| Host | Role |
|---|---|
| `FactoryERP.ApiHost` | **Publish-only.** Sends integration events from command handlers via `IEventBus`. No consumers are registered. |
| `FactoryERP.WorkerHost` | **Consume only.** Registers `QrPrintRequestedConsumer` and `PrintZplCommandConsumer` from the Labeling module. |

### `IEventBus` abstraction

Application and domain layers publish events through `IEventBus`:

```csharp
public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : class;
}
```

The `MassTransitEventBus` implementation delegates to `IPublishEndpoint` (MassTransit).
Application code has no direct MassTransit dependency.

### Consumer retry policy

`QrPrintRequestedConsumer` uses exponential backoff defined in
`QrPrintRequestedConsumerDefinition`:

- 3 retry attempts
- Backoff interval: 5 s → 15 s → 30 s (exponential, max 30 s)

---

## 6. Database Strategy

### Single database, separated schemas

All modules share a **single PostgreSQL database** (`factoryerp`) but use dedicated schemas
to enforce logical data boundaries. No cross-schema foreign keys exist.

| Schema | Owner | Contents |
|---|---|---|
| `auth` | Auth module | Users, Roles, RefreshTokens, SigningKeys, JtiBlacklist |
| `edi` | EDI module | EdiFileJobs, PartnerProfiles, StagingRecords, OutboxMessages |
| `labeling` | Labeling module | PrintJobs, InboxState, OutboxState, OutboxMessage |

Additional modules (Admin, Costing, Inventory, Production, Purchasing, Quality, Sales) follow
the same convention; their schemas are created by their respective migrations.

### EF Core DbContexts

Each module has its own `DbContext`, scoped to its schema:

| DbContext | Schema | Notes |
|---|---|---|
| `AuthDbContext` | `auth` | Extends `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>` |
| `EdiDbContext` | `edi` | Owns EDI staging pipeline |
| `LabelingDbContext` | `labeling` | Owns MassTransit Outbox/Inbox for the WorkerHost |

### Entity base classes

All domain entities inherit from `BaseEntity` (`FactoryERP.SharedKernel`):

- `Guid Id` — primary key, set on construction
- `byte[] RowVersion` — optimistic concurrency token
- Domain event collection (`IReadOnlyCollection<IDomainEvent>`)

Auditable entities additionally carry `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`.

### Applying migrations

Migrations are per-module. Apply them individually using the `--project` and
`--startup-project` flags:

```bash
# Auth module
dotnet ef database update \
  --project src/Modules/Auth/Auth.Infrastructure \
  --startup-project src/Host/FactoryERP.ApiHost

# EDI module
dotnet ef database update \
  --project src/Modules/EDI/EDI.Infrastructure \
  --startup-project src/Host/FactoryERP.ApiHost

# Labeling module
dotnet ef database update \
  --project src/Modules/Labeling/Labeling.Infrastructure \
  --startup-project src/Host/FactoryERP.WorkerHost
```

### Adding a new migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Modules/<Module>/<Module>.Infrastructure \
  --startup-project src/Host/FactoryERP.ApiHost \
  --output-dir Migrations
```

---

## 7. Security

### JWT — RS256

Authentication uses asymmetric RS256 JSON Web Tokens. The signing key is loaded from PEM
files on disk via `FileKeyStoreService`.

| Setting | Default | Config key |
|---|---|---|
| Issuer | `https://factoryerp.local` | `Jwt:Issuer` |
| Audience | `factoryerp-api` | `Jwt:Audience` |
| Access token lifetime | 15 minutes | `Jwt:AccessTokenLifetimeMinutes` |
| Refresh token lifetime | 7 days | `Jwt:RefreshTokenLifetimeDays` |
| Clock skew | 30 seconds | `Jwt:ClockSkewSeconds` |
| Key directory | `Keys/` | `Jwt:KeyDirectory` |

On first startup, if no PEM file is found in `Keys/`, a 2048-bit RSA key is auto-generated
and saved. In production, provide pre-generated keys and never commit them to source control.

### Key rotation

`FileKeyStoreService` supports multiple PEM files in the key directory. The newest file is
the active signing key. All files are loaded as validation keys, enabling zero-downtime key
rotation: add a new key, wait for all tokens signed with the old key to expire, then remove
the old file.

### JTI blacklist

Every issued token carries a unique `jti` (JWT ID) claim. On token validation,
`IJtiBlacklistService` is queried against the `auth.JtiBlacklist` table. Revoked tokens
are rejected at the middleware layer before reaching any controller. This enables immediate
token revocation (e.g., on logout or compromise).

Tokens supplied via query string (`?access_token=`) are rejected unconditionally.

### Security headers

The following HTTP response headers are set globally on every response:

| Header | Value |
|---|---|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `X-XSS-Protection` | `0` (rely on CSP) |
| `Content-Security-Policy` | `default-src 'self'; frame-ancestors 'none';` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

HSTS is enabled in non-development environments.

### Password policy

ASP.NET Core Identity is configured with the following requirements:

- Minimum length: **12 characters**
- Must contain: digit, lowercase, uppercase, non-alphanumeric character
- Lockout: **5 failed attempts** → 15-minute lockout

### Production security checklist

- [ ] Replace the auto-generated RSA key with a key generated offline and stored securely
- [ ] Set `Jwt:Issuer` and `Jwt:Audience` to production values
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production` (disables Swagger, enforces HSTS)
- [ ] Rotate `RabbitMQ:Connection:Password` — do not use default credentials in production
- [ ] Set `Cache:Redis:AbortOnConnectFail=true` in production if Redis is a mandatory dependency
- [ ] Ensure `ConnectionStrings:DefaultConnection` uses a dedicated application user with
  minimum required privileges — no superuser

---

## 8. Running the Project — Development

### Prerequisites

| Tool | Minimum version |
|---|---|
| .NET SDK | 10.0.102 (enforced by `global.json`) |
| Docker Engine | 24+ |
| Docker Compose | v2 |

### Option A — Docker Compose (recommended)

Starts PostgreSQL, RabbitMQ, Redis, ApiHost (hot-reload), and WorkerHost in one command:

```bash
# From the solution root
docker compose -f docker-compose.dev.yml up --build
```

| Service | URL / Port |
|---|---|
| ApiHost (Swagger UI) | http://localhost:8080 |
| RabbitMQ Management UI | http://localhost:15672 (guest / guest) |
| PostgreSQL | localhost:5432 |
| Redis | localhost:6379 |

Apply all database migrations after the containers start (run once, or after adding new
migrations):

```bash
# Auth
dotnet ef database update \
  --project src/Modules/Auth/Auth.Infrastructure \
  --startup-project src/Host/FactoryERP.ApiHost

# EDI
dotnet ef database update \
  --project src/Modules/EDI/EDI.Infrastructure \
  --startup-project src/Host/FactoryERP.ApiHost

# Labeling
dotnet ef database update \
  --project src/Modules/Labeling/Labeling.Infrastructure \
  --startup-project src/Host/FactoryERP.WorkerHost
```

### Option B — dotnet run (infrastructure via docker-compose.local.yml)

Start only the infrastructure services (PostgreSQL, RabbitMQ, Redis) with the local compose
file, then run the hosts from the IDE or CLI:

```bash
# Start infrastructure only
docker compose -f docker-compose.local.yml up -d

# ApiHost
dotnet run --project src/Host/FactoryERP.ApiHost

# WorkerHost (separate terminal)
dotnet run --project src/Host/FactoryERP.WorkerHost
```

### Running tests

```bash
# All tests
dotnet test

# Architecture tests only
dotnet test tests/FactoryERP.ArchTests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Health check endpoint

```
GET http://localhost:8080/health
```

Returns `200 OK` with body `"OK"` when the host is running. The Redis health check is
registered under the `ready` and `cache` tags and reports:

- **Redis disabled** → `Healthy: Redis caching is disabled.`
- **Redis connected** → `Healthy: Redis PING: 0.4ms`
- **Redis unreachable** → `Unhealthy: Redis is unreachable.`

### Startup diagnostics

Both hosts run `DbFingerprint.LogAsync` at startup before accepting requests. This queries
the active PostgreSQL server version, verifies connectivity, and confirms that the MassTransit
Outbox/Inbox tables are present in the expected schema:

```
[INF] DB Fingerprint => db=factoryerp, user=factoryerp, addr=172.17.0.4:5432 | PostgreSQL 18.2 ...
[INF] DB Tables => labeling.OutboxState=labeling."OutboxState", labeling.InboxState=labeling."InboxState", ...
```

A `NULL` value for any table indicates a missing migration.

---

## 9. Enabling Redis (Optional)

Redis is disabled by default. The application runs correctly in L1-only (in-memory) mode
without it. When multiple instances of ApiHost or WorkerHost run on the same Docker host,
enabling Redis provides a **shared L2 cache** and **cross-instance tag invalidation**.

### Step 1 — Enable in configuration

In `appsettings.json` or an environment-specific override:

```json
{
  "Cache": {
    "Redis": {
      "Enabled": true,
      "ConnectionString": "localhost:6379,password=YOUR_PASSWORD,abortConnect=false",
      "InstanceName": "FactoryERP:",
      "ConnectTimeoutMs": 5000,
      "SyncTimeoutMs": 5000,
      "AbortOnConnectFail": false
    }
  }
}
```

Or via environment variables (Docker Compose / container runtime):

```bash
Cache__Redis__Enabled=true
Cache__Redis__ConnectionString=redis:6379,password=YOUR_PASSWORD,abortConnect=false
Cache__Redis__InstanceName=FactoryERP:
Cache__Redis__AbortOnConnectFail=false
```

### Step 2 — Start Redis

The `docker-compose.dev.yml` already includes a Redis service:

```bash
docker compose -f docker-compose.dev.yml up redis
```

The dev compose file uses `requirepass` and AOF persistence (`appendonly yes`). The default
dev password is `r3d1s_d3v_p@ss`. Override with the `REDIS_PASSWORD` environment variable.

### Fail-fast vs. fail-open

| Setting | Behaviour |
|---|---|
| `AbortOnConnectFail=false` *(default)* | Connection failure is logged as a warning; host starts in L1-only mode. Redis reconnects automatically when the broker becomes available. |
| `AbortOnConnectFail=true` | Connection failure throws `InvalidOperationException` at startup. Use in production where Redis is a mandatory dependency. |

### What changes at runtime when Redis is enabled

- `IDistributedCache` is backed by StackExchange.Redis.
- `HybridCache` automatically uses the registered `IDistributedCache` as L2.
- Cache entries are written to both L1 (in-memory, 5 min TTL) and L2 (Redis, 60 min TTL).
- `RemoveByTagAsync` invalidates tagged entries across all host instances sharing the same
  Redis instance and `InstanceName` prefix.
- The Redis health check reports actual PING latency instead of "disabled".

---

## 10. Production Deployment Notes

### Current topology assumption

The solution is designed for a **single Docker host** running all services via Docker Compose.
This is the intended production topology at current scale. Kubernetes is not assumed.

### Container images

The multi-stage `Dockerfile` (production) builds both hosts:

```bash
docker build -t sam-factoryerp .
```

The `Dockerfile.dev` is for local development only and mounts the source tree for hot-reload
via `dotnet watch run`. It must not be used in production.

### When to enable Redis

| Scenario | Recommendation |
|---|---|
| Single ApiHost instance, no WorkerHost cache sharing needed | Redis optional — L1-only is sufficient |
| Multiple ApiHost instances behind a load balancer | Redis **required** for consistent cache state across instances |
| WorkerHost + ApiHost sharing tagged cache entries | Redis **required** for cross-process tag invalidation |

### Scaling considerations

The current architecture supports horizontal scaling of `ApiHost` behind a reverse proxy
(e.g., nginx) on the same Docker host. Each additional instance must point to the same Redis
and RabbitMQ instances. The `Cache:Redis:InstanceName` prefix must be identical across all
instances to share the L2 cache namespace.

`WorkerHost` should run as a single instance per queue. MassTransit Inbox provides
idempotency for duplicate deliveries, but running multiple WorkerHost instances against the
same queues requires thorough end-to-end idempotency testing.

### Environment variable strategy

Secrets (database passwords, RabbitMQ credentials, Redis password) must **never** be stored
in `appsettings.json` in production. Supply them via:

- Docker Compose `env_file:` pointing to a `.env` file stored outside the repository
- A secrets manager (HashiCorp Vault, Docker Secrets, or equivalent)
- Environment variables injected at container runtime by the orchestrator

### Log output

Both hosts write structured logs via Serilog:

- **Console** — formatted output suitable for log aggregation pipelines
- **File** — rolling daily log files in `Logs/`, retained for 30 days, max 256 MB per file

Configure log levels and sinks in the `Serilog` section of `appsettings.json` without any
code change.

---

## 11. Project Structure

```
Sam.FactoryERP/
├── src/
│   ├── BuildingBlocks/                   # Shared cross-cutting concerns
│   │   ├── FactoryERP.SharedKernel/      # BaseEntity, AuditableEntity, IDomainEvent
│   │   ├── FactoryERP.Abstractions/      # ICacheService, IEventBus, ICommand<T>, pipeline behaviors
│   │   ├── FactoryERP.Contracts/         # Integration event message DTOs (shared across modules)
│   │   ├── FactoryERP.Infrastructure/    # HybridCache, MassTransit, Redis, health checks
│   │   └── FactoryERP.ReadModel/         # (Reserved) Read-model / projection infrastructure
│   │
│   ├── Host/
│   │   ├── FactoryERP.ApiHost/           # ASP.NET Core web host — REST API entry point
│   │   └── FactoryERP.WorkerHost/        # Worker SDK host — message consumer entry point
│   │
│   └── Modules/                          # Business modules (each follows Clean Architecture)
│       ├── Admin/
│       │   ├── Admin.Domain/
│       │   ├── Admin.Application/
│       │   ├── Admin.Infrastructure/
│       │   └── Admin.Api/
│       ├── Auth/                         # Identity, JWT issuance, JTI blacklist, key store
│       ├── Costing/
│       ├── EDI/                          # Electronic Data Interchange file processing pipeline
│       ├── Inventory/
│       ├── Labeling/                     # ZPL label printing via Zebra printers
│       ├── Production/
│       ├── Purchasing/
│       ├── Quality/
│       └── Sales/
│
├── tests/
│   ├── Auth.Tests/                       # Unit tests for Auth module
│   ├── EDI.Tests/                        # Unit tests for EDI module
│   ├── Labeling.Tests/                   # Unit / consumer tests for Labeling module
│   └── FactoryERP.ArchTests/             # Architecture enforcement tests (NetArchTest.Rules)
│
├── deployment/                           # Deployment scripts (shell, PowerShell)
├── docs/                                 # Architecture decision records, analysis documents
├── Directory.Build.props                 # Solution-wide MSBuild properties (nullability, analyzers)
├── Directory.Packages.props              # Centralized NuGet version management
├── global.json                           # .NET SDK version pin (10.0.102)
├── docker-compose.dev.yml               # Full dev stack (PostgreSQL, RabbitMQ, Redis, hosts)
├── docker-compose.local.yml             # Infrastructure-only (PostgreSQL, RabbitMQ, Redis)
├── Dockerfile                            # Multi-stage production image
└── Dockerfile.dev                        # Hot-reload development image (dotnet watch)
```

### Module anatomy

Every module follows this internal structure:

```
Module.Domain/          — Entities, value objects, domain events, domain interfaces
Module.Application/     — Commands, queries, handlers (MediatR), application interfaces
Module.Infrastructure/  — EF Core DbContext + migrations, external service implementations
Module.Api/             — Controllers or minimal API endpoints, request/response DTOs
```

The ApiHost references all `Module.Api` and `Module.Infrastructure` projects. The WorkerHost
references only the infrastructure of the modules whose consumers it hosts (currently
`Labeling.Infrastructure`).

---

## 12. Contributing Guidelines

### Code standards

- Target framework: **net10.0** — do not downgrade.
- Language version: **latestMajor** (C# 14 on .NET 10).
- Nullable reference types are **enabled and enforced** everywhere.
- All analyzer warnings are treated as errors (`TreatWarningsAsErrors=true`). The build will
  fail on any `CS*`, `CA*`, or `SA*` violation.
- Every public type and member requires an XML documentation comment. Suppress only via
  `#pragma warning disable` with a justification comment.

### DI registration rules

| Rule | Rationale |
|---|---|
| Never call `BuildServiceProvider()` during DI registration | Creates a second container, wastes resources, bypasses scoping, and is flagged as a Sonar violation |
| Use `IValidateOptions<T>` with `.ValidateOnStart()` | Surfaces misconfiguration before the first request |
| Register `ICacheService` as `Scoped` | Aligns with EF Core DbContext lifetime; avoids long-lived memory pressure |
| Register `IConnectionMultiplexer` as `Singleton` | One connection per process; `GetAwaiter().GetResult()` is permissible only inside the singleton factory (no `SynchronizationContext`) |
| Use `PostConfigure<TOptions>.Configure<TDep>()` to inject resolved services into options | The only correct pattern that avoids `BuildServiceProvider()` |

### Logging policy

- Use `[LoggerMessage]` source-generated partial methods for all log calls in hot paths.
- Never log secrets, connection strings, or passwords — log metadata only (host, port, key prefix).
- Structured logging only — no string interpolation in log message templates.
- Use `ILoggerFactory.CreateLogger("category")` for infrastructure classes that are not
  injected through the standard DI pipeline.

### Messaging conventions

- Consumers must be **idempotent** — MassTransit Inbox handles deduplication at the transport
  level, but consumer logic must also tolerate re-delivery of the same business event.
- Retry policies are defined in `ConsumerDefinition` classes, not in `Program.cs`.
- `IEventBus` is the only permitted publish path from application-layer code.
- Integration event message contracts live in `FactoryERP.Contracts` — never in module
  assemblies — so that producers and consumers can share the type without circular references.

### Caching conventions

- Build cache keys with `CacheKey.Create(…)` — never construct raw key strings manually.
- Always specify tags when caching entities that may require group-based invalidation.
- Never cache security-sensitive data (tokens, passwords, private keys, PII).
- Provide a `CacheEntrySettings` with explicit TTLs for all entries whose lifetime should
  differ from the global default.

### Branch and PR conventions

- Branch naming: `feature/<module>-<short-description>`, `fix/<issue-id>-<description>`
- The build must produce `0 warnings, 0 errors` before opening a PR.
- Architecture tests (`FactoryERP.ArchTests`) must pass — a cross-module domain dependency
  is a build-breaking failure.
- EF Core migration files must be reviewed by a second developer before merging to `main`.
  Always verify the generated SQL against the target PostgreSQL version before applying to
  a production database.

---

*Last updated: February 2026 — Sam.FactoryERP on .NET 10.0.102*
