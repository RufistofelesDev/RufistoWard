# RufistoWard — OIDF Conformance Harness

Runs RufistoWard against the [OpenID Foundation conformance suite](https://gitlab.com/openid/conformance-suite)
for the **Basic** and **Config** OP certification profiles (Story 10.5).

> **Status (2026-06-05): TUNNEL-FIRST.** The primary path runs RufistoWard locally, exposes it via a
> public HTTPS tunnel, and drives the **hosted** OIDF suite (`certification.openid.net`) against it — no
> Docker. The `seed-conformance` CLI command seeds the static client + login test user deterministically
> (done, unit-tested; default redirect base = the hosted suite). Remaining: run both plans green + capture
> the bundle + add the badge. The **self-hosted docker-compose** path (and unattended nightly CI) is
> **deferred to Story 10.5a** — its scaffold stays in this folder but is not the current path.

## Topology (why it's shaped this way)

The suite makes **server-to-server** calls to the OP (discovery, token, JWKS, UserInfo) **and** drives a
**browser** through the OP's `/connect/authorize` → `/Login` pages. Both must reach the OP at the **same**
issuer URL.

**Primary (tunnel):** the OP runs on `localhost`; a public HTTPS tunnel gives it a stable public URL that
becomes `Token:Issuer`. The hosted suite reaches that one URL for both server calls and browser redirects.
The tunnel provides a **real CA-signed cert**, so there is no truststore work.

```
certification.openid.net  ──HTTPS──▶  cloudflared/ngrok tunnel  ──▶  localhost:RufistoWard.Host
 (hosted suite: server                 https://YOUR-TUNNEL-HOST          + local Postgres
  calls + browser)
```

**Deferred (docker self-host, Story 10.5a):** OP + suite + Mongo on one compose network; issuer = container
DNS `https://rufistoward-op:5001`; the OP's self-signed cert must be added to the suite truststore. Needed
for unattended nightly CI. See [Deferred: docker self-host](#deferred-docker-self-host-story-105a) below.

## Files

| Path | Purpose |
|------|---------|
| `config/appsettings.Conformance.json` | OP config: issuer (= tunnel URL), Postgres, auto-migrate, RSA key, relaxed rate limits |
| `config/test-plans/rufistoward-*.json` | hosted-suite plan templates (substitute `YOUR-TUNNEL-HOST`) |
| `docker-compose.yml` | _(deferred 10.5a)_ OP + Postgres; `include`s the vendored suite compose |
| `../../src/RufistoWard.Host/Dockerfile` | _(deferred 10.5a)_ OP image (Tailwind skipped — cosmetic) |
| `certs/generate-certs.sh` | _(deferred 10.5a)_ self-signed TLS cert for `rufistoward-op` |
| `seed/seed-conformance.sh` | _(deferred 10.5a)_ docker bring-up seeding wrapper; tunnel path calls the CLI directly |

## Primary runbook — tunnel + hosted suite

Prerequisites: a local **Postgres** (the OP is Npgsql-specific, no SQLite), a tunnel tool
(**cloudflared** named tunnel preferred; ngrok with a *reserved* domain works — **not** ngrok-free, see
[Topology caveats](#tunnel-caveats)), and a free account on `certification.openid.net`.

```bash
# 1) Start the tunnel → get a STABLE public https URL (so redirect URIs don't churn).
cloudflared tunnel run rufistoward        # → https://rufistoward.<yourdomain>

# 2) Run the OP locally with the tunnel URL as issuer (+ your local Postgres).
#    Set via tests/conformance/config/appsettings.Conformance.json or env:
#      Token:Issuer = https://rufistoward.<yourdomain>
#      Database:ConnectionString = <local postgres>;  Database:AutoMigrateOnStartup = true
ASPNETCORE_ENVIRONMENT=Conformance dotnet run --project ../../src/RufistoWard.Host

# 3) Seed the conformance client + login test user (default redirect base = the HOSTED suite).
dotnet ../../src/RufistoWard.Host/bin/.../RufistoWard.Host.dll seed-conformance
#    (or `dotnet run --project ../../src/RufistoWard.Host -- seed-conformance`)

# 4) Confirm discovery is publicly reachable over valid https:
curl https://rufistoward.<yourdomain>/.well-known/openid-configuration
```

Then in the hosted suite UI (`https://www.certification.openid.net`):

1. Create **`oidcc-config-certification-test-plan`** (variant `server_metadata=discovery`). Set the
   discovery URL to `https://rufistoward.<yourdomain>/.well-known/openid-configuration`. Run it first — it
   validates discovery + JWKS only (no browser) and proves the topology.
2. Create **`oidcc-basic-certification-test-plan`** (variants `server_metadata=discovery`,
   `client_registration=static_client`). Client = `conformance-basic` / `conformance-basic-secret`. Use
   `config/test-plans/rufistoward-basic.json` as the template (substitute `YOUR-TUNNEL-HOST`). **Verify the
   login flow interactively before relying on the `browser` automation.**
3. Capture the shareable result URL + download the JSON/HTML bundle to
   `_bmad-output/test-artifacts/conformance/{YYYY-MM-DD}/`.

<a id="tunnel-caveats"></a>
**Tunnel caveats:** prefer cloudflared or ngrok-reserved — **ngrok-free** injects a browser interstitial +
requires an `ngrok-skip-browser-warning` header that breaks both the suite's browser and its server calls.
A stable hostname keeps the seeded redirect URIs valid across runs. This exposes a dev instance publicly:
seed only the conformance fixtures, use a throwaway Postgres, tear the tunnel down afterward.

## Deferred: docker self-host (Story 10.5a)

The self-hosted path (suite + Mongo + OP on one Docker network, for **unattended nightly CI**) is carved to
Story 10.5a. Its scaffold lives here (`docker-compose.yml`, `../../src/RufistoWard.Host/Dockerfile`,
`certs/generate-certs.sh`, `seed/seed-conformance.sh`) but is **not** the current path.

<details><summary>Docker bring-up (deferred — reference only)</summary>

```bash
cd tests/conformance
git clone https://gitlab.com/openid/conformance-suite.git ./conformance-suite
cd ./conformance-suite && git checkout <release-tag> && cd ..   # pin a release, not master
./certs/generate-certs.sh
# Make the suite TRUST certs/op-cert.pem (JVM truststore server-side + the browser) — the #1
# friction point of this path; the tunnel path avoids it entirely with a real CA cert.
docker compose up --build
./seed/seed-conformance.sh    # seeds with --redirect-base https://localhost.emobix.co.uk:8443
```
Issuer is the container DNS `https://rufistoward-op:5001`; suite UI at `https://localhost.emobix.co.uk:8443`.
Re-seed the client with the emobix redirect base (`seed-conformance --redirect-base
https://localhost.emobix.co.uk:8443`) since the default base targets the hosted suite.
</details>

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
