#!/usr/bin/env bash
# Seed the conformance OP with the admin user + (TODO Task 2) the conformance client,
# scopes, and login-able test user. Idempotent where possible.
#
# Run AFTER `docker compose up` is healthy (from tests/conformance/).
set -euo pipefail
cd "$(dirname "$0")/.."

ADMIN_EMAIL="${ADMIN_EMAIL:-admin@conformance.local}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-Conformance!1}"

echo "==> Waiting for rufistoward-op to be healthy..."
# /health returns JSON with overall status; loop until it responds 200.
until docker compose exec -T rufistoward-op \
      sh -c 'wget -q -O- --no-check-certificate https://localhost:5001/health >/dev/null 2>&1'; do
  sleep 2
done

echo "==> Seeding admin user ($ADMIN_EMAIL) via the seed-admin CLI command..."
# Runs a one-off process in the OP container; shares the Postgres DB. Idempotent:
# the command exits non-zero if the admin already exists — tolerate that.
docker compose exec -T rufistoward-op \
  dotnet RufistoWard.Host.dll seed-admin --email "$ADMIN_EMAIL" --password "$ADMIN_PASSWORD" \
  || echo "   (admin may already exist — continuing)"

echo
echo "==> Seeding the conformance client + login test user via the seed-conformance CLI command..."
# Deterministic fixtures (Story 10.5 Task 2): a static confidential client with a KNOWN
# client_id/secret (conformance-basic / conformance-basic-secret), code grant + PKCE,
# scopes 'openid profile email', and the suite's two callback redirect URIs; plus a
# login-able test user (testuser@conformance.local / Conformance!1).
#
# A CLI command (not POST /api/admin/clients) is required because the Admin API generates
# the client_id server-side and cannot pin the stable id the static-client test config needs.
# Idempotent: re-running skips fixtures that already exist and still exits 0.
docker compose exec -T rufistoward-op \
  dotnet RufistoWard.Host.dll seed-conformance

echo
echo "==> Conformance fixtures seeded. Client/user values are defined in"
echo "    src/RufistoWard.Host/Commands/SeedConformanceCommand.cs and must stay in lock-step"
echo "    with ./config/test-plans/rufistoward-basic.json (client block + //redirect1/2)."
