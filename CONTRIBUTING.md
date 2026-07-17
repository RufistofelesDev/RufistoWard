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

## Licensing your contribution

RufistoWard is dual-licensed: [AGPL-3.0](LICENSE) for everyone, and a commercial licence
for organisations that cannot accept the AGPL or that use the commercial tier. See
[LICENSING.md](LICENSING.md).

That is why contributions need a Contributor Licence Agreement, and why it has to be in
place **before** your first pull request is merged rather than sorted out later.

**You keep your copyright.** You are not signing your work over to anyone. What you grant
is a licence *to the maintainer* to distribute your contribution under **both** the AGPL
and the commercial licence.

Without that grant, contributed code could only ever ship in the AGPL build. One merged
pull request would split the project in two — an open version containing your work and a
commercial one that legally could not — and the only remedy would be tracking down every
past contributor for a signature on code already released. That is a mess nobody can fix
retroactively, so it is prevented up front.

By opening a pull request you confirm that:

1. You wrote the contribution, or have the right to submit it under these terms.
2. If your employer has rights to work you produce, you have their permission to submit
   it.
3. You grant the maintainer a perpetual, worldwide, irrevocable licence to reproduce,
   modify, and distribute your contribution under the AGPL-3.0 **and** under commercial
   terms.
4. You understand your contribution may ship in a paid build, and you are not owed
   payment for it.

If point 3 or 4 is not something you want, that is completely legitimate — say so in the
issue before you write code. Some contributions can be reshaped so the question does not
arise, and it is far better to find that out before you have spent a weekend on it than
after.

This is the same arrangement used by Grafana, Mattermost, Bitwarden, and Nextcloud.
