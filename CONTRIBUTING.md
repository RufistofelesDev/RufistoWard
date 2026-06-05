# Contributing to RufistoWard

Thanks for your interest in contributing! RufistoWard is an open-source .NET 10 OAuth 2.0
/ OpenID Connect identity server. Because it sits on the critical path for authentication,
correctness and security take priority over speed. This guide explains how to get set up
and what we expect from contributions.

By participating you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to contribute

- **Report bugs** — open an issue with a minimal reproduction. For **security** issues, do
  **not** open a public issue; follow [SECURITY.md](SECURITY.md) instead.
- **Improve docs** — README, configuration reference, and inline XML docs.
- **Fix or implement** — pick an open issue (ideally one labelled `good first issue` or
  `help wanted`) and comment before starting so we don't duplicate work.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.x)
- [PostgreSQL](https://www.postgresql.org/download/) (default provider) or SQL Server
- [Node.js](https://nodejs.org/) — only for Tailwind CSS development builds of the Admin UI

## Build

```bash
git clone <your-fork-url>
cd RufistoWard
dotnet build RufistoWard.slnx -warnaserror
```

The build must be **0 warnings, 0 errors**. Warnings are treated as errors in CI.

## Run the tests

> **Important:** this repository uses xUnit v3 with the Microsoft Testing Platform, so the
> test projects are executables. **`dotnet test` does not run them.** Run each test project
> directly:

```bash
dotnet run --project tests/RufistoWard.Domain.Tests
dotnet run --project tests/RufistoWard.Application.Tests
dotnet run --project tests/RufistoWard.Infrastructure.Tests
dotnet run --project tests/RufistoWard.Host.Tests
```

To run a single class:

```bash
dotnet run --project tests/RufistoWard.Host.Tests -- -class <FullyQualifiedClassName>
```

Tests use an in-memory database and do not require PostgreSQL or SQL Server to be running.
**All tests must pass before you open a PR.** New code must come with tests.

## Architecture & conventions

RufistoWard follows **Clean Architecture** (Domain → Application → Infrastructure → Host).
There are two distinct sub-projects in one repo — treat them differently:

- **Protocol flows** (token, authorize, discovery, userinfo, jwks, introspection,
  revocation, device, logout) are **procedural and spec-driven**. The acceptance bar is RFC
  compliance and OpenID Foundation conformance. **JWT is built manually with
  `System.Security.Cryptography` — do not introduce `Microsoft.IdentityModel` into
  production code** (it is allowed in tests as a reference oracle only).
- **Admin / identity domain** (users, clients, scopes, consents, roles, claims pipeline)
  uses **full DDD** — aggregates, value objects, domain events.

Other conventions:

- **C# 14 / .NET 10** idioms: primary constructors, collection expressions, file-scoped
  namespaces, records where they fit.
- **Frontend** is server-rendered Razor + HTMX with a strict CSP (no `unsafe-inline`,
  no `unsafe-eval`) and **no client-side JS framework**. Keep it that way.
- **Line endings are CRLF** (enforced via `.editorconfig`); shell scripts stay LF. Run
  `dotnet format` before committing so formatting and EOLs are normalized.

## Quality bar

For changes touching auth flows, token issuance, key material, client registration,
session store, or consent storage, the contribution must additionally satisfy the security
and testing standards (differential JWT tests, negative crypto tests, threat-model
coverage). These are non-negotiable for an identity server.

## Submitting a pull request

1. Fork and create a branch from `master`.
2. Make focused, reviewable commits with descriptive messages.
3. Ensure `dotnet build -warnaserror` is clean and **all test projects pass**.
4. Run `dotnet format` (formatting + EOL normalization).
5. Open the PR against `master` with a clear description of *what* changed and *why*. Link
   the issue it resolves. Note any protocol/security-surface impact.
6. Be responsive to review feedback. Security- and protocol-surface changes get extra
   scrutiny — that's expected, not personal.

## License

By contributing, you agree that your contributions are licensed under the project's
[Apache License 2.0](LICENSE).
