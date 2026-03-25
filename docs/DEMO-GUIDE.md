# RufistoWard — Demo Guide

## Prerequisites

- .NET 10 SDK (10.0.x)
- **PostgreSQL** (default) — local instance on port 5432, or Docker
- Alternatively: SQL Server LocalDB (comes with Visual Studio) or a SQL Server instance
- A terminal (PowerShell, bash, or Windows Terminal)

## Database Provider Configuration

RufistoWard supports two database providers. Set the provider in `src/RufistoWard.Host/appsettings.json`:

### PostgreSQL (default)

```json
"Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "Host=localhost;Port=5432;Database=RufistoWard;Username=postgres;Password=postgres",
    "AutoMigrateOnStartup": false
}
```

### SQL Server

```json
"Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=RufistoWard;Trusted_Connection=True;",
    "AutoMigrateOnStartup": false
}
```

> **Note:** When switching providers, you must regenerate EF Core migrations for the target provider. See [Switching Database Providers](#switching-database-providers) below.

---

## Quick Start (5 minutes)

### 1. Build the solution

```bash
cd D:\Code\Rufistofeles.Dev\RufistofelesIdentityServer
dotnet build RufistoWard.slnx
```

### 2. Apply database migrations

```bash
dotnet run --project src/RufistoWard.Host -- migrate
```

This creates the `RufistoWard` database with all tables (Clients, Users, Roles, ApiScopes, etc.) on your configured provider.

### 3. Seed the admin user

```bash
dotnet run --project src/RufistoWard.Host -- seed-admin --email admin@rufistoward.local --password Admin123!
```

### 4. Start the server

```bash
dotnet run --project src/RufistoWard.Host --launch-profile https
```

The server starts at **https://localhost:7037** (HTTPS) and **http://localhost:5069** (HTTP).

> On first startup, a signing key (RS256) is automatically generated.

---

## Demo Walkthrough

### Health Check

```bash
curl https://localhost:7037/health -k
```

Response:
```json
{
  "status": "Healthy",
  "checks": [
    { "name": "database", "status": "Healthy" },
    { "name": "signing-key", "status": "Healthy", "description": "Signing key is active and valid." }
  ]
}
```

### OIDC Discovery

```bash
curl https://localhost:7037/.well-known/openid-configuration -k
```

Returns the full OpenID Connect Discovery 1.0 document with all endpoints.

### JWKS (Public Signing Keys)

```bash
curl https://localhost:7037/.well-known/jwks -k
```

Returns the JSON Web Key Set with the server's public RSA key.

### Login Page

Open **https://localhost:7037/login** in a browser.

- Username: `admin`
- Password: `Admin123!` (or whatever you set in step 3)

### Admin Dashboard

After logging in, visit **https://localhost:7037/admin** to see the admin dashboard with entity counts.

### Admin API — List Clients

```bash
curl https://localhost:7037/api/admin/clients -k
```

Returns `{ "items": [], "totalCount": 0, "page": 1, "pageSize": 25 }`.

### Admin API — List Users

```bash
curl https://localhost:7037/api/admin/users -k
```

### Admin API — List Roles

```bash
curl https://localhost:7037/api/admin/roles -k
```

### Admin API — List Scopes

```bash
curl https://localhost:7037/api/admin/scopes -k
```

### Device Authorization Page

Open **https://localhost:7037/device** in a browser to see the device code verification page.

---

## OIDC Authorization Code Flow (Full Demo)

### Step 1: Start the authorization flow

Open this URL in a browser (replace with your client details):

```
https://localhost:7037/connect/authorize?response_type=code&client_id=demo-app&redirect_uri=https://localhost:3000/callback&scope=openid profile email&state=abc123&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256
```

> **Note:** This will fail with "Unknown or disabled client" until you create a client named `demo-app` via the Admin API. This demonstrates the full validation flow.

### Step 2: Exchange code for tokens

```bash
curl -X POST https://localhost:7037/connect/token \
  -k \
  -d "grant_type=authorization_code&code=YOUR_CODE&redirect_uri=https://localhost:3000/callback&client_id=demo-app&code_verifier=YOUR_VERIFIER"
```

### Step 3: Refresh token

```bash
curl -X POST https://localhost:7037/connect/token \
  -k \
  -d "grant_type=refresh_token&refresh_token=YOUR_REFRESH_TOKEN&client_id=demo-app"
```

### Step 4: Client Credentials (Machine-to-Machine)

```bash
curl -X POST https://localhost:7037/connect/token \
  -k \
  -d "grant_type=client_credentials&client_id=YOUR_CLIENT&client_secret=YOUR_SECRET&scope=api"
```

### Step 5: Introspect a token

```bash
curl -X POST https://localhost:7037/connect/introspect \
  -k \
  -d "token=YOUR_ACCESS_TOKEN&client_id=YOUR_CLIENT&client_secret=YOUR_SECRET"
```

### Step 6: Revoke a token

```bash
curl -X POST https://localhost:7037/connect/revocation \
  -k \
  -d "token=YOUR_TOKEN&client_id=YOUR_CLIENT&client_secret=YOUR_SECRET"
```

### Step 7: Logout

```
https://localhost:7037/connect/endsession?post_logout_redirect_uri=https://localhost:3000
```

---

## CLI Commands

| Command | Description |
|---------|-------------|
| `dotnet run --project src/RufistoWard.Host -- migrate` | Apply pending database migrations |
| `dotnet run --project src/RufistoWard.Host -- seed-admin --email <email> --password <pwd>` | Create admin user |
| `dotnet run --project src/RufistoWard.Host -- is4-migrate --source <connection-string>` | Migrate from IdentityServer4 |

---

## Running Tests

```bash
# All tests (120 total)
dotnet run --project tests/RufistoWard.Domain.Tests
dotnet run --project tests/RufistoWard.Application.Tests
dotnet run --project tests/RufistoWard.Infrastructure.Tests
dotnet run --project tests/RufistoWard.Host.Tests

# Or build + run via dotnet test
dotnet build RufistoWard.slnx
dotnet test RufistoWard.slnx
```

> Tests use an in-memory database and do not require PostgreSQL or SQL Server to be running.

---

## Configuration

All configuration is in `src/RufistoWard.Host/appsettings.json`. Key sections:

| Section | Purpose |
|---------|---------|
| `Database.Provider` | `PostgreSQL` (default) or `SqlServer` |
| `Database.ConnectionString` | Connection string matching the selected provider |
| `Database.AutoMigrateOnStartup` | Set `true` to auto-migrate on start |
| `Token.Issuer` | The issuer URI (must match what clients expect) |
| `Token.AccessTokenLifetimeSeconds` | JWT access token lifetime (default: 3600 = 1 hour) |
| `Token.RefreshTokenLifetimeSeconds` | Refresh token lifetime (default: 86400 = 24 hours) |
| `AdminAuth.Mode` | `Local` (cookie) or `OIDC` (dogfooding) |
| `KeyManagement.SigningAlgorithm` | `RS256` or `ES256` |
| `RateLimits.TokenEndpointPermitLimit` | Max requests per window on /connect/* |

### Environment variable overrides

```bash
# Override via env vars (double underscore for nested keys)
export Database__Provider="PostgreSQL"
export Database__ConnectionString="Host=myserver;Database=RufistoWard;Username=app;Password=secret"
export Token__Issuer="https://id.mycompany.com"
```

---

## Switching Database Providers

The current migrations target the configured provider (PostgreSQL by default). To switch providers:

1. Update `appsettings.json` with the new `Provider` and `ConnectionString`
2. Delete existing migrations:
   ```bash
   rm -rf src/RufistoWard.Infrastructure/Persistence/Migrations/
   ```
3. Regenerate migrations for the new provider:
   ```bash
   dotnet ef migrations add InitialCreate \
     --project src/RufistoWard.Infrastructure \
     --startup-project src/RufistoWard.Host \
     --output-dir Persistence/Migrations
   ```
4. Apply the migration:
   ```bash
   dotnet run --project src/RufistoWard.Host -- migrate
   ```

> The entity configurations are provider-agnostic. Only the generated migration SQL differs between providers.

---

## Architecture Quick Reference

```
src/
├── RufistoWard.Domain/          # Entities, value objects, interfaces
├── RufistoWard.Application/     # Protocol handlers, token building, claim pipeline
├── RufistoWard.Infrastructure/  # EF Core, repositories, key storage providers
└── RufistoWard.Host/            # ASP.NET Core host, endpoints, pages, middleware

tests/
├── RufistoWard.Domain.Tests/          # 57 value object tests
├── RufistoWard.Application.Tests/     # 11 service tests
├── RufistoWard.Infrastructure.Tests/  # 4 provider tests
└── RufistoWard.Host.Tests/            # 48 integration + conformance tests
```

## Endpoints Summary

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/.well-known/openid-configuration` | GET | OIDC Discovery |
| `/.well-known/jwks` | GET | Public signing keys |
| `/connect/authorize` | GET | Authorization (code flow) |
| `/connect/token` | POST | Token exchange (4 grant types) |
| `/connect/deviceauthorization` | POST | Device code request |
| `/connect/revocation` | POST | Token revocation (RFC 7009) |
| `/connect/introspect` | POST | Token introspection (RFC 7662) |
| `/connect/endsession` | GET | Logout |
| `/login` | GET/POST | User login page |
| `/device` | GET/POST | Device verification page |
| `/health` | GET | Health check |
| `/admin` | GET | Admin dashboard |
| `/api/admin/clients` | GET/DELETE | Client management API |
| `/api/admin/scopes` | GET/DELETE | Scope management API |
| `/api/admin/users` | GET | User management API |
| `/api/admin/roles` | GET/DELETE | Role management API |
| `/api/admin/api-resources` | GET | API resource management |
