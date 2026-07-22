# RufistoWard — OIDF Conformance Harness

Runs RufistoWard against the [OpenID Foundation conformance suite](https://gitlab.com/openid/conformance-suite)
for the **Basic** and **Config** OP certification profiles (Story 10.5).

> **Status (2026-07-19): BOTH PLANS GREEN AND PUBLISHED.** See [Results](#results). The path runs
> RufistoWard locally, exposes it via a public HTTPS tunnel, and drives the **hosted** OIDF suite
> (`www.certification.openid.net`) against it — no Docker. The **self-hosted docker-compose** path (and
> unattended nightly CI) is **deferred to Story 10.5a** — its scaffold stays in this folder but is not the
> current path.
>
> **Read this before assuming the harness alone got it green.** Story 10.5 was scoped as "external
> conformance harness + docs, no production protocol code." That turned out to be wrong: the basic plan
> surfaced **eight MUST-level protocol gaps** that had to be implemented before it could pass. They are
> listed in [Protocol gaps this run exposed](#protocol-gaps-this-run-exposed). Six of the eight were
> invisible until an earlier fix or a missing fixture unblocked them — **incomplete plan config masks real
> gaps instead of failing loudly**, which is the single most important lesson for 10.5a's nightly run.

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
#    ASPNETCORE_ENVIRONMENT=Conformance is REQUIRED — seed-conformance refuses to run anywhere
#    else and exits 1 (see "Why the seeder refuses to run" below). This is a SEPARATE process
#    from step 2, so it does NOT inherit that step's inline environment variable.
ASPNETCORE_ENVIRONMENT=Conformance \
  dotnet ../../src/RufistoWard.Host/bin/.../RufistoWard.Host.dll seed-conformance
#    (or `ASPNETCORE_ENVIRONMENT=Conformance dotnet run --project ../../src/RufistoWard.Host -- seed-conformance`)

# 4) Confirm discovery is publicly reachable over valid https:
curl https://rufistoward.<yourdomain>/.well-known/openid-configuration
```

Then in the hosted suite UI (`https://www.certification.openid.net`):

1. Create **`oidcc-config-certification-test-plan`** (variant `server_metadata=discovery`). Set the
   discovery URL to `https://rufistoward.<yourdomain>/.well-known/openid-configuration`. Run it first — it
   validates discovery + JWKS only (no browser) and proves the topology.
2. Create **`oidcc-basic-certification-test-plan`** (variants `server_metadata=discovery`,
   `client_registration=static_client`). Use `config/test-plans/rufistoward-basic.json` as the template
   (substitute `YOUR-TUNNEL-HOST`).

   > ⚠️ **Fill in ALL THREE client blocks before creating the plan.** Plan config is **immutable** — a
   > missing block cannot be added later, and the only remedy is a brand-new plan and a full 35-module
   > re-run. Worse, the affected modules fail with a *config* error that looks like an OP defect:
   >
   > | Form block | Value | Needed by |
   > |---|---|---|
   > | **Client** | `conformance-basic` / `conformance-basic-secret` | everything |
   > | **Client for client_secret_post** | `conformance-basic` / `conformance-basic-secret` (reuse is fine — RufistoWard accepts both auth methods) | `oidcc-server-client-secret-post` |
   > | **Second client** | `conformance-basic-2` / `conformance-basic-2-secret` (must be a genuinely different client) | `oidcc-refresh-token` |

3. Run the ~35 modules. Most are a single browser redirect; see [Driving the run](#driving-the-run) for
   the four that are not.
4. Capture the shareable result URL + the bundle into
   `_bmad-output/test-artifacts/conformance/{YYYY-MM-DD}/`. "Publish for certification" downloads the
   full submission zip — but see the warning under [Results](#results) first.

<a id="tunnel-caveats"></a>
**Tunnel caveats:** prefer cloudflared or ngrok-reserved — **ngrok-free** injects a browser interstitial +
requires an `ngrok-skip-browser-warning` header that breaks both the suite's browser and its server calls.
A stable hostname keeps the seeded redirect URIs valid across runs. This exposes a dev instance publicly:
seed only the conformance fixtures, use a throwaway Postgres, tear the tunnel down afterward.

<a id="why-the-seeder-refuses"></a>
**Why the seeder refuses to run.** `seed-conformance` exits 1 unless `ASPNETCORE_ENVIRONMENT=Conformance`.
This is deliberate (Story 10.5b). The fixtures use fixed credentials — `conformance-basic` /
`conformance-basic-secret` and `testuser@conformance.local` / `Conformance!1` — which are **published in
the OIDF certification logs**, and the client is seeded with `RequirePkce=false`. Seeding them into a
real database hands anyone a working confidential client. If you see

```
Refusing to seed: seed-conformance requires ASPNETCORE_ENVIRONMENT=Conformance, but the host is
running as 'Development'.
```

set the variable *on the seeding command itself* — it is a separate process from the running OP and does
not inherit the OP's environment.

A caveat when running the OP in the Conformance environment: the host will load
`src/RufistoWard.Host/appsettings.Conformance.json` if you have created one (the runbook tells you to;
it is git-ignored). That file sets `Database:AutoMigrateOnStartup=true`. That is what you want for a real
run, but it means the environment carries real database behaviour with it — which is exactly why the
seeder tests pin the flag off rather than relying on config-source clearing.

<a id="driving-the-run"></a>
## Driving the run

Most modules are a plain browser redirect: open the "Browser Interaction" URL, and if the OP asks for
credentials sign in as `testuser@conformance.local` / `Conformance!1`. Four need special handling.

| Module | What it needs |
|---|---|
| `oidcc-prompt-none-not-logged-in` | Clear the OP session **first** — visit `/connect/endsession`. The suite explicitly says "remove any cookies you may have received". With a live session the test's premise is void. |
| `oidcc-prompt-login`, `oidcc-max-age-1`, `oidcc-ensure-registered-redirect-uri`, `oidcc-ensure-request-object-with-redirect-uri` | Each needs an **uploaded screenshot** (second login page / redirect-URI error page). Upload at `upload.html?log=<testId>`; the "Upload Images" button opens that page in a **new tab**. These land on verdict **REVIEW with 0 FAILURE**, which is the correct terminal state — an OIDF human reviews the image. |
| `oidcc-ensure-post-request-succeeds` | **The suite allows only 30 seconds** for this interaction. A hand-driven login blows the budget; the late callback then throws `Illegal test state change: FINISHED -> RUNNING` and the module fails for a reason that has nothing to do with the OP. **Reliable trick:** sign in to the OP first, then submit a form to `/connect/authorize` **from a page already on the OP's own origin** (e.g. `/admin`). The `SameSite=Lax` session cookie then rides along, the OP redirects straight to the callback, and no login is needed. (A cross-site POST from the suite deliberately does *not* send that cookie, which is why the login appears at all.) |

> **Never fabricate the screenshots.** They are certification evidence. If the OP does not actually show
> a second login page, the correct action is to leave the module unsatisfied and fix the OP — which is
> exactly how the `prompt=login` and `max_age` gaps below were found.

**A failed module can be re-run on its own** with **Repeat Test** — the plan picks up the new result, so
one fix does not cost a 35-module re-run. (Creating a *new plan* does, because plan config is immutable.)

<a id="protocol-gaps-this-run-exposed"></a>
## Protocol gaps this run exposed

All eight were MUST-level and all are fixed. Recorded because the story predicted "no production protocol
code should need changes" — that prediction was wrong, and the next profile (formpost/dynamic, or FAPI)
should budget for the same.

| # | Gap | Spec | Fix |
|---|---|---|---|
| 1 | `auth_time` taken from the authorization **code's** creation time, so every id_token got a fresh value | OIDCC-2 | Stamp `auth_time` at login; carry via `AuthorizationCodeData.AuthTime` |
| 2 | `prompt=login` did not re-authenticate | OIDCC-3.1.2.1 | Force re-auth; **strip `prompt=login` from the login return URL or it loops forever** |
| 3 | `prompt=none` rendered the login page instead of erroring | OIDCC-3.1.2.6 | Return `login_required` |
| 4 | `max_age` did not force re-authentication | OIDCC-3.1.2.1 | Compare `now - auth_time` against `max_age` |
| 5 | `POST /connect/authorize` returned **405** | OIDCC-3.1.2.1 | Support GET *and* POST; a POST needing login resumes as a GET |
| 6 | `request` / `request_uri` silently **ignored**, dropping the state/nonce inside them | OIDCC-6.1/6.2 | Return `request_not_supported` / `request_uri_not_supported` |
| 7 | Token endpoint omitted `Cache-Control: no-store` | RFC 6749 §5.1 | Set `no-store` + `Pragma: no-cache` on every token response |
| 8 | Refreshed id_token carried a **moving** `auth_time` (same bug as #1, second site) | OIDCC-12.2 | `RefreshTokenData` persists auth_time through **every** rotation — verify across two hops, a naive fix survives only one |

**Ordering matters:** #7 and #8 were undiscoverable until `client2` existed, because `oidcc-refresh-token`
could not start without it. Treat a "green" plan with unfilled config blocks as **untested**, not passing.

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

<a id="results"></a>
## Results (2026-07-19)

| Plan | Plan ID | Outcome |
|---|---|---|
| `oidcc-config-certification-test-plan` | `Rc7CUe5MQqD0P` | 36 SUCCESS / **0 FAILURE** |
| `oidcc-basic-certification-test-plan` | `ZyytN9FAD5ZeJ` | 35 modules — 22 PASSED, 5 WARNING, 4 SKIPPED, 4 REVIEW, **0 FAILED** |

Both plans are published; the URLs below were verified to render for an **anonymous** visitor:

- Basic: <https://www.certification.openid.net/plan-detail.html?plan=ZyytN9FAD5ZeJ&public=true>
- Config: <https://www.certification.openid.net/plan-detail.html?plan=Rc7CUe5MQqD0P&public=true>

Submission zips in `_bmad-output/test-artifacts/conformance/2026-07-19/`:
`oidcc-basic-certification-test-plan-discovery-static_client-ZyytN9FAD5ZeJ-20-Jul-2026.zip` (1.35 MB) and
`oidcc-config-certification-test-plan-Rc7CUe5MQqD0P-20-Jul-2026.zip` (23 KB).

> ⚠️ **"Publish for certification" is irreversible.** It publishes the plan, makes **all keys and secrets
> publicly visible**, and marks the plan **permanently immutable** — you can never run another test under
> it. Do it only when the plan is final. The seeded `conformance-basic*` secrets are now public: they are
> throwaway fixtures on a torn-down tunnel, but must never be reused anywhere real.

### The 13 non-PASSED results are all legitimate

Not defects — do not "fix" these by inventing fixture data:

- **4 REVIEW** — screenshot-backed modules (see [Driving the run](#driving-the-run)). 0 FAILURE each.
- **4 SKIPPED** — `scope-address` / `scope-phone` / `scope-all` are not in `scopes_supported`;
  `oidcc-unsigned-request-object-…` skips *because* the OP now correctly rejects request objects.
- **5 WARNING**, all SHOULD-level or fixture-shaped:
  - `scope-profile`, `claims-essential` — the seeded user has no `name`/profile claims, and the `claims`
    request parameter is correctly advertised as unsupported. `scope-email` passes precisely *because*
    that user does have email claims, which is the proof this is fixture data and not a UserInfo bug.
  - `userinfo-post-body` — access token in the POST body (RFC 6750 §2.2) is an **optional** transport;
    only the `Authorization` header is required.
  - `ensure-request-with-acr-values` — no `acr` claim returned; SHOULD, not MUST (OIDCC-15.1).
  - `codereuse-30seconds` — access token not revoked on code replay; SHOULD (RFC 6749 §4.1.2).

Known cosmetic issue: the redirect-URI error page renders as **raw JSON**, not a styled page. It satisfies
the security requirement (it does not redirect to an unregistered URI) but is a poor end-user experience.

## Deferred to v1.1 (verified reasons)

- `oidcc-formpost-certification-test-plan` — RufistoWard has no `response_mode=form_post`.
- `oidcc-dynamic-certification-test-plan` — RufistoWard has no Dynamic Client Registration endpoint.

## Verified OP facts (so the dev doesn't re-derive)

- Endpoints: `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/.well-known/jwks`,
  discovery `/.well-known/openid-configuration`. Login page route `/login` (lowercase — the suite's
  WebRunner URL match is case-sensitive; `/Login*` fails with "unexpected url for task: Login").
- Login form selectors: `#username`, `#password`, submit `[data-testid="login-button"]`. No consent screen.
- Issuer config key: `Token:Issuer`. Schema built on boot via `Database:AutoMigrateOnStartup=true`.
- Admin user seeded by the `seed-admin` CLI command; the conformance fixtures are seeded
  deterministically by the `seed-conformance` CLI command:
  - client `conformance-basic` / `conformance-basic-secret`
  - **second client `conformance-basic-2` / `conformance-basic-2-secret`** — required by
    `oidcc-refresh-token`, which checks a refresh token issued to one client cannot be redeemed by
    another. It must be a genuinely separate client, not an alias.
  - user `testuser@conformance.local` / `Conformance!1`
  Scopes are not DB-backed — discovery advertises `openid profile email` statically and the
  authorize/token flow passes scopes through, so no scope seeding is needed.
- The OP accepts **both** `client_secret_basic` and `client_secret_post` (`TokenEndpoints` falls back to
  the form `client_id`), and discovery advertises both — so the plan's "client for client_secret_post"
  block can safely reuse the primary client.
- `Token:Issuer` must match the tunnel host exactly. The suite UI **remembers the last plan's config**,
  so on a new tunnel the pre-filled discovery URL is stale — overwrite it or the run silently targets a
  dead host.
