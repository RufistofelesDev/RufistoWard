# RufistoWard — OIDF Conformance Harness

Runs RufistoWard against the [OpenID Foundation conformance suite](https://gitlab.com/openid/conformance-suite)
for the **Basic** and **Config** OP certification profiles (Story 10.5).

> **Status: Tasks 1–2 done (code/config); Docker-dependent steps pending.** The compose topology,
> OP image, TLS, and config templates are in place, and the `seed-conformance` CLI command now
> seeds the static client + login test user deterministically (Task 2, unit-tested). The headless
> runner (Task 4) and the nightly `conformance.yml` workflow (Task 5) are not done yet. Lines marked
> **TODO** below need a real `docker compose up` to finalize — the stack has not been booted
> end-to-end (no Docker on the authoring machine).

## Topology (why it's shaped this way)

The suite makes **server-to-server** calls to the OP (discovery, token, JWKS, UserInfo) **and** drives a
**browser** through the OP's `/connect/authorize` → `/Login` pages. Both must reach the OP at the **same**
issuer URL. So the OP runs as a container (`rufistoward-op`) on the suite's compose network, and its issuer
is the container DNS name `https://rufistoward-op:5001` — resolvable by the suite's server and browser alike.

```
docker compose project (one network)
├── server      (OIDF suite, Java)        ─┐ from vendored ./conformance-suite/ via `include:`
├── nginx       (suite TLS proxy, :8443)   │  UI at https://localhost.emobix.co.uk:8443
├── mongodb     (suite store)             ─┘
├── postgres    (OP store)
└── rufistoward-op  (system under test, https://rufistoward-op:5001)
```

## Files

| Path | Purpose |
|------|---------|
| `docker-compose.yml` | OP + Postgres; `include`s the vendored suite compose |
| `../../src/RufistoWard.Host/Dockerfile` | OP image (Tailwind skipped — cosmetic) |
| `config/appsettings.Conformance.json` | OP config: issuer, Postgres, auto-migrate, RSA key, relaxed rate limits |
| `certs/generate-certs.sh` | self-signed TLS cert for `rufistoward-op` (op.pfx + op-cert.pem) |
| `seed/seed-conformance.sh` | seeds admin user + conformance client/test user via the `seed-conformance` CLI command |
| `config/test-plans/rufistoward-config.json` | suite config for the **config** plan (discovery only) |
| `config/test-plans/rufistoward-basic.json` | suite config for the **basic** plan (incl. BrowserControl login) |

## One-time setup

```bash
cd tests/conformance

# 1) Vendor + build the suite (it builds from source; pin a release tag, not master).
git clone https://gitlab.com/openid/conformance-suite.git ./conformance-suite
cd ./conformance-suite && git checkout <release-tag>     # see the suite's releases
#    build the jar + images per the suite's own README (mvn package, then its image build)
cd ..

# 2) TLS cert for the OP.
./certs/generate-certs.sh

# 3) Make the suite TRUST the OP cert (see TLS below) — REQUIRED or discovery over https fails.

# 4) Boot everything.
docker compose up --build

# 5) Seed.
./seed/seed-conformance.sh
```

## TLS (the #1 friction point)

The OP serves **https** with a self-signed cert (SAN `DNS:rufistoward-op`). The suite will reject it until
it trusts `certs/op-cert.pem` in **both** places it talks to the OP:

- **Server-to-server (Java):** add `op-cert.pem` to the suite `server` container's truststore (mount the
  PEM and import into the JVM cacerts, or use the suite's documented CA-injection path). **TODO: confirm the
  exact mount/import the pinned suite release expects.**
- **Browser:** the automated browser must also trust the cert. **TODO: confirm whether the pinned release
  runs the browser server-side (needs CA in that container) or expects an external driver.**

If trust is impractical, the fallback is to terminate the OP behind the suite's `nginx` with a cert it
already trusts — heavier; prefer direct trust first.

## Running the plans (Task 4 — not scaffolded)

`config` first (no browser — a topology smoke test), then `basic`:

- **Interactive:** open `https://localhost.emobix.co.uk:8443`, create a test plan
  (`oidcc-config-certification-test-plan`, then `oidcc-basic-certification-test-plan` with variants
  `server_metadata=discovery`, `client_registration=static_client`), paste the matching
  `config/test-plans/*.json`, run.
- **Headless/CI:** drive the suite REST API or `scripts/run-test-plan.py`. The suite runs with
  `--fintechlabs.devmode=true` (no API token needed locally). Export the JSON+HTML bundle to
  `_bmad-output/test-artifacts/conformance/{YYYY-MM-DD}/`.

**Verify the `basic` plan interactively before enabling the `browser` automation** — the
`config/test-plans/rufistoward-basic.json` BrowserControl block is a template; confirm its command syntax
against the suite's `Design/BrowserControl` wiki.

## Deferred to v1.1 (verified reasons)

- `oidcc-formpost-certification-test-plan` — RufistoWard has no `response_mode=form_post`.
- `oidcc-dynamic-certification-test-plan` — RufistoWard has no Dynamic Client Registration endpoint.

## Verified OP facts (so the dev doesn't re-derive)

- Endpoints: `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/.well-known/jwks`,
  discovery `/.well-known/openid-configuration`. Login page route `/Login`.
- Login form selectors: `#username`, `#password`, submit `[data-testid="login-button"]`. No consent screen.
- Issuer config key: `Token:Issuer`. Schema built on boot via `Database:AutoMigrateOnStartup=true`.
- Admin user seeded by the `seed-admin` CLI command; the conformance client + login test user are
  seeded deterministically by the `seed-conformance` CLI command (client `conformance-basic`, secret
  `conformance-basic-secret`, user `testuser@conformance.local` / `Conformance!1`). Scopes are not
  DB-backed — discovery advertises `openid profile email` statically and the authorize/token flow
  passes scopes through, so no scope seeding is needed.
