# RufistoWard

**Open-source .NET identity server replacing IdentityServer4.**

*"No token, no passage."*

[![OIDF conformance: Basic OP](https://img.shields.io/badge/OIDF%20conformance-Basic%20OP%20passing-brightgreen)](https://www.certification.openid.net/plan-detail.html?plan=ZyytN9FAD5ZeJ&public=true)
[![OIDF conformance: Config OP](https://img.shields.io/badge/OIDF%20conformance-Config%20OP%20passing-brightgreen)](https://www.certification.openid.net/plan-detail.html?plan=Rc7CUe5MQqD0P&public=true)

RufistoWard is a standalone OAuth2 and OpenID Connect identity server built on .NET 10. It delivers full protocol compliance through a configuration-driven deployment model — no custom code required. A built-in Administration UI gives admins self-service control over clients, resources, and users. Migration tooling provides a direct path from existing IdentityServer4 instances.

## Features

- **Full OIDC/OAuth2 Protocol Surface**
  - Authorization Code + PKCE
  - Client Credentials
  - Refresh Token
  - Device Code (RFC 8628)
  - Token Revocation (RFC 7009)
  - Token Introspection (RFC 7662)
  - OIDC Discovery + JWKS
  - End Session / Logout

- **Admin REST API** for clients, scopes, users, roles, and API resources

- **Admin UI** built with Tailwind CSS v4, HTMX, and custom Razor tag helpers — no heavy JavaScript frameworks

- **Three-Stage Claim Pipeline** with mappings, transforms, and external sources

- **Security**
  - RSA/EC signing key generation and automatic rotation
  - OWASP security headers on every response
  - Content-Security-Policy on every response (`default-src 'self'`, `frame-ancestors 'none'`, no `unsafe-eval`; `unsafe-inline` still required for inline HTMX attributes and styles)
  - Rate limiting on protocol endpoints
  - Encrypted secrets at rest
  - Adaptive password hashing (bcrypt)

- **Database Support** — PostgreSQL (default) and SQL Server

- **IdentityServer4 Migration Tool** — built-in CLI for migrating IS4 configurations

- **Configuration-Driven** — JSON + environment variables, no compilation required

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.x)
- [PostgreSQL](https://www.postgresql.org/download/) (default) or SQL Server
- [Node.js](https://nodejs.org/) (only for Tailwind CSS development builds)

## Quick Start

### 1. Clone and build

```bash
git clone https://github.com/RufistofelesDev/RufistoWard.git
cd RufistoWard
dotnet build RufistoWard.slnx
```

### 2. Configure your database

Edit `src/RufistoWard.Host/appsettings.json`:

**PostgreSQL (default):**
```json
"Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "Host=localhost;Port=5432;Database=RufistoWard;Username=postgres;Password=postgres",
    "AutoMigrateOnStartup": false
}
```

**SQL Server:**
```json
"Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=RufistoWard;Trusted_Connection=True;",
    "AutoMigrateOnStartup": false
}
```

### 3. Apply database migrations

```bash
dotnet run --project src/RufistoWard.Host -- migrate
```

### 4. Seed the admin user

```bash
dotnet run --project src/RufistoWard.Host -- seed-admin \
    --email admin@rufistoward.local \
    --password Admin123!
```

### 5. Start the server

```bash
dotnet run --project src/RufistoWard.Host --launch-profile https
```

The server starts at:
- **https://localhost:7037** (HTTPS)
- **http://localhost:5069** (HTTP)

A signing key (RS256) is generated automatically on first startup.

### 6. Verify

```bash
# Health check
curl https://localhost:7037/health -k

# OIDC Discovery
curl https://localhost:7037/.well-known/openid-configuration -k

# Open the login page
open https://localhost:7037/login

# Open the admin dashboard
open https://localhost:7037/admin
```

## CLI Commands

| Command | Description |
|---------|-------------|
| `dotnet run -- migrate` | Apply pending database migrations |
| `dotnet run -- seed-admin --email <email> --password <pwd>` | Create or update the admin user |
| `dotnet run -- is4-migrate --source <connection-string>` | Migrate data from an IdentityServer4 database |

> All commands are run from the Host project: `dotnet run --project src/RufistoWard.Host -- <command>`

## Configuration

All settings live in `src/RufistoWard.Host/appsettings.json`. Override any value with environment variables using double-underscore notation:

```bash
export Database__Provider="PostgreSQL"
export Database__ConnectionString="Host=prod-db;Database=RufistoWard;..."
export Token__Issuer="https://id.mycompany.com"
```

### Key Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `Database.Provider` | `PostgreSQL` | Database provider: `PostgreSQL` or `SqlServer` |
| `Database.ConnectionString` | *(required)* | Connection string for the selected provider |
| `Database.AutoMigrateOnStartup` | `false` | Apply migrations automatically on app start |
| `Token.Issuer` | `https://localhost:5001` | Issuer URI embedded in tokens |
| `Token.AccessTokenLifetimeSeconds` | `3600` | Access token TTL (1 hour) |
| `Token.RefreshTokenLifetimeSeconds` | `86400` | Refresh token TTL (24 hours) |
| `KeyManagement.SigningAlgorithm` | `RS256` | Signing algorithm (`RS256` or `ES256`) |
| `KeyManagement.RotationIntervalDays` | `90` | Automatic key rotation interval |
| `AdminAuth.Mode` | `Local` | Admin auth mode: `Local` (cookie) or `OIDC` |
| `RateLimits.TokenEndpointPermitLimit` | `100` | Max token requests per window |

## API Endpoints

### Protocol Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/.well-known/openid-configuration` | GET | OIDC Discovery document |
| `/.well-known/jwks` | GET | JSON Web Key Set |
| `/connect/authorize` | GET | Authorization endpoint (code flow + PKCE) |
| `/connect/token` | POST | Token endpoint (4 grant types) |
| `/connect/deviceauthorization` | POST | Device authorization (RFC 8628) |
| `/connect/revocation` | POST | Token revocation (RFC 7009) |
| `/connect/introspect` | POST | Token introspection (RFC 7662) |
| `/connect/endsession` | GET | End session / logout |

### Admin API

| Endpoint | Methods | Description |
|----------|---------|-------------|
| `/api/admin/clients` | GET, DELETE | Client management |
| `/api/admin/scopes` | GET, DELETE | Scope management |
| `/api/admin/users` | GET | User management |
| `/api/admin/roles` | GET, DELETE | Role management |
| `/api/admin/api-resources` | GET | API resource management |

### UI Pages

| Page | Description |
|------|-------------|
| `/login` | User login |
| `/device` | Device code verification |
| `/admin` | Admin dashboard |
| `/health` | Health check |

## Architecture

RufistoWard follows **Clean Architecture** with full DDD for the admin/identity domain and procedural handlers for protocol flows.

```
src/
├── RufistoWard.Domain/          # Entities, value objects, repository interfaces
├── RufistoWard.Application/     # Protocol handlers, token building, claim pipeline
├── RufistoWard.Infrastructure/  # EF Core DbContext, repositories, key storage
└── RufistoWard.Host/            # ASP.NET Core host, endpoints, pages, middleware
```

### Key Design Decisions

- **No SPA** — Server-rendered Razor Pages with HTMX for interactivity. Zero client-side framework dependencies, so the CSP restricts to `'self'` with no `unsafe-eval` and no framing.
- **Manual JWT building** — Uses `System.Security.Cryptography` directly. No dependency on `Microsoft.IdentityModel`.
- **Pluggable boundaries** — Database provider, key storage, and rate limiter are abstracted behind interfaces for future extensibility.
- **HybridCache** — Discovery and JWKS responses are cached with both in-memory and HTTP caching layers.

## Testing

```bash
# Run all 218 tests (xUnit v3 + Microsoft Testing Platform — `dotnet test` does NOT run these)
dotnet run --project tests/RufistoWard.Domain.Tests           # 60 tests
dotnet run --project tests/RufistoWard.Application.Tests      # 22 tests
dotnet run --project tests/RufistoWard.Infrastructure.Tests   # 40 tests
dotnet run --project tests/RufistoWard.Host.Tests             # 96 tests
```

Tests use an in-memory database and do not require PostgreSQL or SQL Server to be running.

## Conformance

RufistoWard passes the OpenID Foundation conformance suite for the **Basic OP** and **Config OP**
profiles — 0 failures across all 35 basic-profile modules and all 36 config-profile checks.

| Profile | Result | Log |
|---------|--------|-----|
| Basic OP | 22 passed · 5 warning · 4 skipped · 4 review · **0 failed** | [public results](https://www.certification.openid.net/plan-detail.html?plan=ZyytN9FAD5ZeJ&public=true) |
| Config OP | 36 checks · **0 failed** | [public results](https://www.certification.openid.net/plan-detail.html?plan=Rc7CUe5MQqD0P&public=true) |

> **Not a certification claim.** These are conformance-suite *test results*, not OpenID Certification.
> As the OpenID Foundation states, such results "are not proof that a deployment is conformant nor that
> it meets the requirements for certification." RufistoWard is **not** listed at
> [openid.net/certification](https://openid.net/certification/); formal certification is a separate
> submission process.

Reproduce it with the runbook in [`tests/conformance/README.md`](tests/conformance/README.md).

**Deferred to v1.1** (verified reasons, not defects):

- `oidcc-formpost-*` — RufistoWard does not implement `response_mode=form_post`.
- `oidcc-dynamic-*` — RufistoWard has no Dynamic Client Registration endpoint.

## Switching Database Providers

The shipped migrations target PostgreSQL. To switch to SQL Server:

1. Update `Provider` and `ConnectionString` in `appsettings.json`
2. Delete existing migrations: `rm -rf src/RufistoWard.Infrastructure/Persistence/Migrations/`
3. Regenerate:
   ```bash
   dotnet ef migrations add InitialCreate \
       --project src/RufistoWard.Infrastructure \
       --startup-project src/RufistoWard.Host \
       --output-dir Persistence/Migrations
   ```
4. Apply: `dotnet run --project src/RufistoWard.Host -- migrate`

Entity configurations are provider-agnostic — only the generated migration SQL differs.

## Comparison

| Feature | RufistoWard | Duende IS | Keycloak | TheIdServer |
|---------|-------------|-----------|----------|-------------|
| License | AGPL-3.0 + commercial | Commercial ($) | Apache 2.0 | Apache 2.0 |
| Every OAuth/OIDC flow free | Yes, permanently | No | Yes | Yes |
| .NET native | Yes | Yes | No (Java) | Yes |
| Built-in Admin UI | Yes | No | Yes | Yes |
| IS4 migration tooling | Yes | Upgrade path | No | No |
| CSP enforced | Yes | Partial | Partial | Partial |
| No JS framework required | Yes (HTMX) | N/A | React | Blazor |

Read that first row honestly: **RufistoWard is the most restrictive open-source licence in
its own comparison table.** Keycloak and TheIdServer are Apache 2.0; this is AGPL. If your
organisation rejects AGPL by policy, that is a real cost and it is on us, not on you — see
[LICENSING.md](LICENSING.md), because there is a commercial licence for exactly that case.

## License

Dual-licensed: **[AGPL-3.0](LICENSE)** for everyone, plus a commercial licence for
organisations that cannot accept the AGPL or that use the commercial tier
(multi-tenancy, tamper-evident audit). See **[LICENSING.md](LICENSING.md)**.

Every OAuth 2.0 and OpenID Connect flow, the Admin UI, the IS4 migration tooling, and
security event logging are free under the AGPL and will never move behind a paywall.
IdentityServer4 was free until it wasn't, and a lot of people found out the hard way.
**RufistoWard does not charge for protocol. It charges for operational convenience.**

If you self-host RufistoWard to authenticate your own users, the AGPL asks nothing of
you. Section 13 only reaches you if you offer a *modified* version to others over a
network. The AGPL is a fence around competitors, not around users.
